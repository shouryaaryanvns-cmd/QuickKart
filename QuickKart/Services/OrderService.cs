using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using QuickKart.Data;
using QuickKart.Models;

namespace QuickKart.Services
{
    public sealed class OrderService
    {
        private static readonly HashSet<string> AllowedPaymentMethods =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Cash on Delivery", "Cash", "Card", "UPI"
            };

        public IList<CategoryOption> GetCategories()
        {
            var categories = new List<CategoryOption>
            {
                new CategoryOption { CategoryId = 0, CategoryName = "All categories" }
            };

            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(@"
SELECT CategoryId, CategoryName
FROM dbo.Categories
WHERE IsActive = 1
ORDER BY CategoryName;", connection))
            {
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        categories.Add(new CategoryOption
                        {
                            CategoryId = reader.GetInt32(0),
                            CategoryName = reader.GetString(1)
                        });
                    }
                }
            }

            return categories;
        }

        public IList<ProductCatalogItem> GetProducts(string searchText, int categoryId)
        {
            var products = new List<ProductCatalogItem>();
            const string sql = @"
SELECT p.ProductId, p.ProductName, c.CategoryName, p.Brand, p.Unit,
       p.Price, p.Discount,
       CONVERT(decimal(10,2), ROUND(p.Price * (100 - p.Discount) / 100, 2)) AS SellingPrice,
       p.Stock
FROM dbo.Products p
JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
WHERE p.IsAvailable = 1
  AND c.IsActive = 1
  AND (@CategoryId = 0 OR p.CategoryId = @CategoryId)
  AND (@Search = N'' OR p.ProductName LIKE N'%' + @Search + N'%'
       OR ISNULL(p.Brand, N'') LIKE N'%' + @Search + N'%')
ORDER BY p.ProductName;";

            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@CategoryId", SqlDbType.Int).Value = categoryId;
                command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value =
                    (searchText ?? string.Empty).Trim();
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        products.Add(new ProductCatalogItem
                        {
                            ProductId = reader.GetInt32(0),
                            ProductName = reader.GetString(1),
                            CategoryName = reader.GetString(2),
                            Brand = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            Unit = reader.GetString(4),
                            OriginalPrice = reader.GetDecimal(5),
                            DiscountPercent = reader.GetDecimal(6),
                            SellingPrice = reader.GetDecimal(7),
                            Stock = reader.GetInt32(8)
                        });
                    }
                }
            }

            return products;
        }

        public IList<CartLine> GetCart(int userId)
        {
            var lines = new List<CartLine>();
            const string sql = @"
SELECT ci.CartItemId, ci.ProductId, p.ProductName, p.Unit,
       ci.Price, ci.Quantity, p.Stock,
       CONVERT(decimal(10,2), ci.Price * ci.Quantity) AS LineTotal
FROM dbo.Cart c
JOIN dbo.CartItems ci ON ci.CartId = c.CartId
JOIN dbo.Products p ON p.ProductId = ci.ProductId
WHERE c.UserId = @UserId
ORDER BY ci.CartItemId;";

            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lines.Add(new CartLine
                        {
                            CartItemId = reader.GetInt32(0),
                            ProductId = reader.GetInt32(1),
                            ProductName = reader.GetString(2),
                            Unit = reader.GetString(3),
                            Price = reader.GetDecimal(4),
                            Quantity = reader.GetInt32(5),
                            AvailableStock = reader.GetInt32(6),
                            LineTotal = reader.GetDecimal(7)
                        });
                    }
                }
            }

            return lines;
        }

        public void AddToCart(int userId, int productId, int quantity)
        {
            if (quantity <= 0)
                throw new InvalidOperationException("Quantity must be greater than zero.");

            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int cartId = EnsureCart(connection, transaction, userId);
                        decimal sellingPrice;
                        int stock;

                        using (SqlCommand productCommand = new SqlCommand(@"
SELECT CONVERT(decimal(10,2), ROUND(Price * (100 - Discount) / 100, 2)), Stock
FROM dbo.Products WITH (UPDLOCK, ROWLOCK)
WHERE ProductId = @ProductId AND IsAvailable = 1;", connection, transaction))
                        {
                            productCommand.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;
                            using (SqlDataReader reader = productCommand.ExecuteReader())
                            {
                                if (!reader.Read())
                                    throw new InvalidOperationException("The selected product is unavailable.");
                                sellingPrice = reader.GetDecimal(0);
                                stock = reader.GetInt32(1);
                            }
                        }

                        int existingQuantity = 0;
                        using (SqlCommand existingCommand = new SqlCommand(@"
SELECT Quantity FROM dbo.CartItems WITH (UPDLOCK, ROWLOCK)
WHERE CartId = @CartId AND ProductId = @ProductId;", connection, transaction))
                        {
                            existingCommand.Parameters.Add("@CartId", SqlDbType.Int).Value = cartId;
                            existingCommand.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;
                            object value = existingCommand.ExecuteScalar();
                            if (value != null)
                                existingQuantity = Convert.ToInt32(value);
                        }

                        if (existingQuantity + quantity > stock)
                            throw new InvalidOperationException(
                                "Requested quantity exceeds the available stock of " + stock + ".");

                        if (existingQuantity == 0)
                        {
                            using (SqlCommand insertCommand = new SqlCommand(@"
INSERT dbo.CartItems (CartId, ProductId, Quantity, Price)
VALUES (@CartId, @ProductId, @Quantity, @Price);", connection, transaction))
                            {
                                insertCommand.Parameters.Add("@CartId", SqlDbType.Int).Value = cartId;
                                insertCommand.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;
                                insertCommand.Parameters.Add("@Quantity", SqlDbType.Int).Value = quantity;
                                insertCommand.Parameters.Add("@Price", SqlDbType.Decimal).Value = sellingPrice;
                                insertCommand.Parameters["@Price"].Precision = 10;
                                insertCommand.Parameters["@Price"].Scale = 2;
                                insertCommand.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            using (SqlCommand updateCommand = new SqlCommand(@"
UPDATE dbo.CartItems
SET Quantity = Quantity + @Quantity, Price = @Price
WHERE CartId = @CartId AND ProductId = @ProductId;", connection, transaction))
                            {
                                updateCommand.Parameters.Add("@Quantity", SqlDbType.Int).Value = quantity;
                                updateCommand.Parameters.Add("@Price", SqlDbType.Decimal).Value = sellingPrice;
                                updateCommand.Parameters["@Price"].Precision = 10;
                                updateCommand.Parameters["@Price"].Scale = 2;
                                updateCommand.Parameters.Add("@CartId", SqlDbType.Int).Value = cartId;
                                updateCommand.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;
                                updateCommand.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public void UpdateCartQuantity(int userId, int cartItemId, int quantity)
        {
            if (quantity <= 0)
                throw new InvalidOperationException("Quantity must be greater than zero.");

            const string sql = @"
UPDATE ci
SET Quantity = @Quantity,
    Price = CONVERT(decimal(10,2), ROUND(p.Price * (100 - p.Discount) / 100, 2))
FROM dbo.CartItems ci
JOIN dbo.Cart c ON c.CartId = ci.CartId
JOIN dbo.Products p ON p.ProductId = ci.ProductId
WHERE ci.CartItemId = @CartItemId
  AND c.UserId = @UserId
  AND p.IsAvailable = 1
  AND p.Stock >= @Quantity;";

            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Quantity", SqlDbType.Int).Value = quantity;
                command.Parameters.Add("@CartItemId", SqlDbType.Int).Value = cartItemId;
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                connection.Open();
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException(
                        "The quantity could not be updated. Check current stock.");
            }
        }

        public void RemoveCartItem(int userId, int cartItemId)
        {
            const string sql = @"
DELETE ci
FROM dbo.CartItems ci
JOIN dbo.Cart c ON c.CartId = ci.CartId
WHERE ci.CartItemId = @CartItemId AND c.UserId = @UserId;";

            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@CartItemId", SqlDbType.Int).Value = cartItemId;
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        public IList<AddressOption> GetAddresses(int userId)
        {
            var addresses = new List<AddressOption>();
            const string sql = @"
SELECT AddressId,
       CONCAT(ISNULL(NULLIF(HouseNo, N''), N''),
              CASE WHEN NULLIF(HouseNo, N'') IS NULL THEN N'' ELSE N', ' END,
              ISNULL(NULLIF(Street, N''), N''),
              CASE WHEN NULLIF(Street, N'') IS NULL THEN N'' ELSE N', ' END,
              City, N', ', State, N' - ', Pincode,
              CASE WHEN NULLIF(Landmark, N'') IS NULL THEN N'' ELSE N' (' + Landmark + N')' END)
FROM dbo.Address
WHERE UserId = @UserId
ORDER BY AddressId;";

            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        addresses.Add(new AddressOption
                        {
                            AddressId = reader.GetInt32(0),
                            DisplayText = reader.GetString(1)
                        });
                    }
                }
            }
            return addresses;
        }

        public int AddAddress(int userId, AddressRequest request)
        {
            if (request == null)
                throw new ArgumentNullException("request");
            if (string.IsNullOrWhiteSpace(request.City) ||
                string.IsNullOrWhiteSpace(request.State) ||
                string.IsNullOrWhiteSpace(request.Pincode))
                throw new InvalidOperationException("City, state and pincode are required.");
            if (request.Pincode.Trim().Length < 5 || request.Pincode.Trim().Length > 10)
                throw new InvalidOperationException("Enter a valid pincode.");

            const string sql = @"
INSERT dbo.Address (UserId, HouseNo, Street, City, State, Pincode, Landmark)
VALUES (@UserId, @HouseNo, @Street, @City, @State, @Pincode, @Landmark);
SELECT CONVERT(int, SCOPE_IDENTITY());";

            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                AddNullableString(command, "@HouseNo", 100, request.HouseNo);
                AddNullableString(command, "@Street", 200, request.Street);
                command.Parameters.Add("@City", SqlDbType.NVarChar, 100).Value = request.City.Trim();
                command.Parameters.Add("@State", SqlDbType.NVarChar, 100).Value = request.State.Trim();
                command.Parameters.Add("@Pincode", SqlDbType.VarChar, 10).Value = request.Pincode.Trim();
                AddNullableString(command, "@Landmark", 200, request.Landmark);
                connection.Open();
                return (int)command.ExecuteScalar();
            }
        }

        public int PlaceOrder(int userId, int addressId, string paymentMethod)
        {
            if (!AllowedPaymentMethods.Contains(paymentMethod ?? string.Empty))
                throw new InvalidOperationException("Select a valid payment method.");

            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        string addressSnapshot = GetAddressSnapshot(
                            connection, transaction, userId, addressId);
                        var checkoutLines = new List<CartLine>();

                        using (SqlCommand cartCommand = new SqlCommand(@"
SELECT ci.ProductId, p.ProductName, p.Unit, ci.Quantity, p.Stock,
       CONVERT(decimal(10,2), ROUND(p.Price * (100 - p.Discount) / 100, 2)) AS CurrentPrice
FROM dbo.Cart c
JOIN dbo.CartItems ci ON ci.CartId = c.CartId
JOIN dbo.Products p WITH (UPDLOCK, ROWLOCK) ON p.ProductId = ci.ProductId
WHERE c.UserId = @UserId AND p.IsAvailable = 1
ORDER BY ci.CartItemId;", connection, transaction))
                        {
                            cartCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                            using (SqlDataReader reader = cartCommand.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int quantity = reader.GetInt32(3);
                                    int stock = reader.GetInt32(4);
                                    if (quantity > stock)
                                        throw new InvalidOperationException(
                                            reader.GetString(1) + " has only " + stock + " item(s) in stock.");

                                    checkoutLines.Add(new CartLine
                                    {
                                        ProductId = reader.GetInt32(0),
                                        ProductName = reader.GetString(1),
                                        Unit = reader.GetString(2),
                                        Quantity = quantity,
                                        AvailableStock = stock,
                                        Price = reader.GetDecimal(5)
                                    });
                                }
                            }
                        }

                        if (checkoutLines.Count == 0)
                            throw new InvalidOperationException("Your cart is empty.");

                        decimal total = 0m;
                        foreach (CartLine line in checkoutLines)
                            total += line.Price * line.Quantity;
                        total = Math.Round(total, 2, MidpointRounding.AwayFromZero);

                        int orderId;
                        string paymentStatus = paymentMethod == "Cash on Delivery" ? "Pending" : "Paid";
                        using (SqlCommand orderCommand = new SqlCommand(@"
INSERT dbo.Orders
    (UserId, AddressId, OrderDate, TotalAmount, OrderStatus,
     PaymentStatus, PaymentMethod, DeliveryAddressSnapshot)
VALUES
    (@UserId, @AddressId, GETDATE(), @TotalAmount, N'Pending',
     @PaymentStatus, @PaymentMethod, @AddressSnapshot);
SELECT CONVERT(int, SCOPE_IDENTITY());", connection, transaction))
                        {
                            orderCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                            orderCommand.Parameters.Add("@AddressId", SqlDbType.Int).Value = addressId;
                            AddMoney(orderCommand, "@TotalAmount", total);
                            orderCommand.Parameters.Add("@PaymentStatus", SqlDbType.NVarChar, 30).Value = paymentStatus;
                            orderCommand.Parameters.Add("@PaymentMethod", SqlDbType.NVarChar, 50).Value = paymentMethod;
                            orderCommand.Parameters.Add("@AddressSnapshot", SqlDbType.NVarChar, 600).Value = addressSnapshot;
                            orderId = (int)orderCommand.ExecuteScalar();
                        }

                        foreach (CartLine line in checkoutLines)
                        {
                            using (SqlCommand itemCommand = new SqlCommand(@"
INSERT dbo.OrderItems (OrderId, ProductId, Quantity, Price, ProductName)
VALUES (@OrderId, @ProductId, @Quantity, @Price, @ProductName);", connection, transaction))
                            {
                                itemCommand.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                                itemCommand.Parameters.Add("@ProductId", SqlDbType.Int).Value = line.ProductId;
                                itemCommand.Parameters.Add("@Quantity", SqlDbType.Int).Value = line.Quantity;
                                AddMoney(itemCommand, "@Price", line.Price);
                                itemCommand.Parameters.Add("@ProductName", SqlDbType.NVarChar, 200).Value = line.ProductName;
                                itemCommand.ExecuteNonQuery();
                            }

                            using (SqlCommand stockCommand = new SqlCommand(@"
UPDATE dbo.Products
SET Stock = Stock - @Quantity
WHERE ProductId = @ProductId AND Stock >= @Quantity;", connection, transaction))
                            {
                                stockCommand.Parameters.Add("@Quantity", SqlDbType.Int).Value = line.Quantity;
                                stockCommand.Parameters.Add("@ProductId", SqlDbType.Int).Value = line.ProductId;
                                if (stockCommand.ExecuteNonQuery() != 1)
                                    throw new InvalidOperationException(
                                        line.ProductName + " no longer has sufficient stock.");
                            }

                            using (SqlCommand inventoryCommand = new SqlCommand(@"
INSERT dbo.InventoryTransactions
    (ProductId, TransactionType, QuantityChange, ReferenceId, Remarks)
VALUES
    (@ProductId, N'Sale', -@Quantity, @OrderId, N'Customer order');", connection, transaction))
                            {
                                inventoryCommand.Parameters.Add("@ProductId", SqlDbType.Int).Value = line.ProductId;
                                inventoryCommand.Parameters.Add("@Quantity", SqlDbType.Int).Value = line.Quantity;
                                inventoryCommand.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                                inventoryCommand.ExecuteNonQuery();
                            }
                        }

                        using (SqlCommand paymentCommand = new SqlCommand(@"
INSERT dbo.Payments
    (OrderId, PaymentMethod, TransactionId, Amount, PaymentDate, PaymentStatus)
VALUES
    (@OrderId, @PaymentMethod, @TransactionId, @Amount, GETDATE(), @PaymentStatus);", connection, transaction))
                        {
                            paymentCommand.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                            paymentCommand.Parameters.Add("@PaymentMethod", SqlDbType.NVarChar, 50).Value = paymentMethod;
                            paymentCommand.Parameters.Add("@TransactionId", SqlDbType.NVarChar, 100).Value =
                                paymentStatus == "Paid" ? (object)("QK-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant()) : DBNull.Value;
                            AddMoney(paymentCommand, "@Amount", total);
                            paymentCommand.Parameters.Add("@PaymentStatus", SqlDbType.NVarChar, 30).Value = paymentStatus;
                            paymentCommand.ExecuteNonQuery();
                        }

                        using (SqlCommand clearCommand = new SqlCommand(@"
DELETE ci
FROM dbo.CartItems ci
JOIN dbo.Cart c ON c.CartId = ci.CartId
WHERE c.UserId = @UserId;", connection, transaction))
                        {
                            clearCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                            clearCommand.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        return orderId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public IList<OrderRecord> GetOrders(string status)
        {
            var orders = new List<OrderRecord>();
            const string sql = @"
SELECT o.OrderId, o.OrderDate, u.FullName, o.TotalAmount,
       o.OrderStatus, o.PaymentStatus, o.PaymentMethod,
       o.DeliveryAddressSnapshot
FROM dbo.Orders o
JOIN dbo.Users u ON u.UserId = o.UserId
WHERE @Status = N'All' OR o.OrderStatus = @Status
ORDER BY o.OrderDate DESC, o.OrderId DESC;";

            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value =
                    string.IsNullOrWhiteSpace(status) ? "All" : status;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        orders.Add(new OrderRecord
                        {
                            OrderId = reader.GetInt32(0),
                            OrderDate = reader.GetDateTime(1),
                            CustomerName = reader.GetString(2),
                            TotalAmount = reader.GetDecimal(3),
                            OrderStatus = reader.GetString(4),
                            PaymentStatus = reader.GetString(5),
                            PaymentMethod = reader.GetString(6),
                            DeliveryAddress = reader.GetString(7)
                        });
                    }
                }
            }
            return orders;
        }

        public IList<OrderItemRecord> GetOrderItems(int orderId)
        {
            var items = new List<OrderItemRecord>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(@"
SELECT ProductName, Price, Quantity,
       CONVERT(decimal(10,2), Price * Quantity)
FROM dbo.OrderItems
WHERE OrderId = @OrderId
ORDER BY OrderItemId;", connection))
            {
                command.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new OrderItemRecord
                        {
                            ProductName = reader.GetString(0),
                            Price = reader.GetDecimal(1),
                            Quantity = reader.GetInt32(2),
                            LineTotal = reader.GetDecimal(3)
                        });
                    }
                }
            }
            return items;
        }

        public OrderRecord GetOrder(int orderId)
        {
            const string sql = @"
SELECT o.OrderId,o.OrderDate,u.FullName,o.TotalAmount,o.OrderStatus,
       o.PaymentStatus,o.PaymentMethod,o.DeliveryAddressSnapshot
FROM dbo.Orders o
JOIN dbo.Users u ON u.UserId=o.UserId
WHERE o.OrderId=@OrderId;";
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("Order was not found.");
                    return new OrderRecord
                    {
                        OrderId = reader.GetInt32(0), OrderDate = reader.GetDateTime(1),
                        CustomerName = reader.GetString(2), TotalAmount = reader.GetDecimal(3),
                        OrderStatus = reader.GetString(4), PaymentStatus = reader.GetString(5),
                        PaymentMethod = reader.GetString(6), DeliveryAddress = reader.GetString(7)
                    };
                }
            }
        }

        public void UpdateOrderStatus(int orderId, string newStatus, int adminId)
        {
            string[] validStatuses = { "Pending", "Confirmed", "Packed", "Delivered", "Cancelled" };
            if (Array.IndexOf(validStatuses, newStatus) < 0)
                throw new InvalidOperationException("Select a valid order status.");

            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        string currentStatus;
                        string paymentStatus;
                        string paymentMethod;
                        using (SqlCommand find = new SqlCommand(@"
SELECT OrderStatus,PaymentStatus,PaymentMethod
FROM dbo.Orders WITH (UPDLOCK,ROWLOCK)
WHERE OrderId=@OrderId;", connection, transaction))
                        {
                            find.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                            using (SqlDataReader reader = find.ExecuteReader())
                            {
                                if (!reader.Read())
                                    throw new InvalidOperationException("Order was not found.");
                                currentStatus = reader.GetString(0);
                                paymentStatus = reader.GetString(1);
                                paymentMethod = reader.GetString(2);
                            }
                        }

                        if (currentStatus == newStatus)
                        {
                            transaction.Commit();
                            return;
                        }
                        if (!IsAllowedTransition(currentStatus, newStatus))
                            throw new InvalidOperationException(
                                "Order cannot move from " + currentStatus + " to " + newStatus + ".");

                        if (newStatus == "Cancelled")
                        {
                            var lines = new List<CartLine>();
                            using (SqlCommand items = new SqlCommand(@"
SELECT ProductId,ProductName,Quantity
FROM dbo.OrderItems WHERE OrderId=@OrderId;", connection, transaction))
                            {
                                items.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                                using (SqlDataReader reader = items.ExecuteReader())
                                    while (reader.Read())
                                        lines.Add(new CartLine
                                        {
                                            ProductId = reader.GetInt32(0),
                                            ProductName = reader.GetString(1),
                                            Quantity = reader.GetInt32(2)
                                        });
                            }

                            foreach (CartLine line in lines)
                            {
                                using (SqlCommand restore = new SqlCommand(
                                    "UPDATE dbo.Products SET Stock=Stock+@Quantity WHERE ProductId=@ProductId;",
                                    connection, transaction))
                                {
                                    restore.Parameters.Add("@Quantity", SqlDbType.Int).Value = line.Quantity;
                                    restore.Parameters.Add("@ProductId", SqlDbType.Int).Value = line.ProductId;
                                    restore.ExecuteNonQuery();
                                }
                                using (SqlCommand inventory = new SqlCommand(@"
INSERT dbo.InventoryTransactions
    (ProductId,TransactionType,QuantityChange,ReferenceId,Remarks,AdminId)
VALUES
    (@ProductId,N'OrderCancellation',@Quantity,@OrderId,N'Order cancelled',@AdminId);",
                                    connection, transaction))
                                {
                                    inventory.Parameters.Add("@ProductId", SqlDbType.Int).Value = line.ProductId;
                                    inventory.Parameters.Add("@Quantity", SqlDbType.Int).Value = line.Quantity;
                                    inventory.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                                    inventory.Parameters.Add("@AdminId", SqlDbType.Int).Value = adminId;
                                    inventory.ExecuteNonQuery();
                                }
                            }

                            string cancelledPaymentStatus = paymentStatus == "Paid" ? "Refunded" : "Failed";
                            using (SqlCommand payment = new SqlCommand(@"
UPDATE dbo.Payments SET PaymentStatus=@Status WHERE OrderId=@OrderId;
UPDATE dbo.Orders SET PaymentStatus=@Status WHERE OrderId=@OrderId;",
                                connection, transaction))
                            {
                                payment.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = cancelledPaymentStatus;
                                payment.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                                payment.ExecuteNonQuery();
                            }
                        }
                        else if (newStatus == "Delivered" && paymentMethod == "Cash on Delivery")
                        {
                            using (SqlCommand paid = new SqlCommand(@"
UPDATE dbo.Payments SET PaymentStatus=N'Paid' WHERE OrderId=@OrderId;
UPDATE dbo.Orders SET PaymentStatus=N'Paid' WHERE OrderId=@OrderId;",
                                connection, transaction))
                            {
                                paid.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                                paid.ExecuteNonQuery();
                            }
                        }

                        using (SqlCommand update = new SqlCommand(
                            "UPDATE dbo.Orders SET OrderStatus=@Status WHERE OrderId=@OrderId;",
                            connection, transaction))
                        {
                            update.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = newStatus;
                            update.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
                            update.ExecuteNonQuery();
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static bool IsAllowedTransition(string currentStatus, string newStatus)
        {
            if (currentStatus == "Pending")
                return newStatus == "Confirmed" || newStatus == "Cancelled";
            if (currentStatus == "Confirmed")
                return newStatus == "Packed" || newStatus == "Cancelled";
            if (currentStatus == "Packed")
                return newStatus == "Delivered" || newStatus == "Cancelled";
            return false;
        }

        private static int EnsureCart(
            SqlConnection connection, SqlTransaction transaction, int userId)
        {
            using (SqlCommand findCommand = new SqlCommand(
                "SELECT CartId FROM dbo.Cart WITH (UPDLOCK, HOLDLOCK) WHERE UserId = @UserId;",
                connection, transaction))
            {
                findCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                object value = findCommand.ExecuteScalar();
                if (value != null)
                    return Convert.ToInt32(value);
            }

            using (SqlCommand insertCommand = new SqlCommand(@"
INSERT dbo.Cart (UserId) VALUES (@UserId);
SELECT CONVERT(int, SCOPE_IDENTITY());", connection, transaction))
            {
                insertCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                return (int)insertCommand.ExecuteScalar();
            }
        }

        private static string GetAddressSnapshot(
            SqlConnection connection,
            SqlTransaction transaction,
            int userId,
            int addressId)
        {
            using (SqlCommand command = new SqlCommand(@"
SELECT CONCAT(ISNULL(NULLIF(HouseNo, N''), N''),
              CASE WHEN NULLIF(HouseNo, N'') IS NULL THEN N'' ELSE N', ' END,
              ISNULL(NULLIF(Street, N''), N''),
              CASE WHEN NULLIF(Street, N'') IS NULL THEN N'' ELSE N', ' END,
              City, N', ', State, N' - ', Pincode,
              CASE WHEN NULLIF(Landmark, N'') IS NULL THEN N'' ELSE N' (' + Landmark + N')' END)
FROM dbo.Address
WHERE AddressId = @AddressId AND UserId = @UserId;", connection, transaction))
            {
                command.Parameters.Add("@AddressId", SqlDbType.Int).Value = addressId;
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                object value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException("Select a valid delivery address.");
                return Convert.ToString(value);
            }
        }

        private static void AddMoney(SqlCommand command, string name, decimal value)
        {
            SqlParameter parameter = command.Parameters.Add(name, SqlDbType.Decimal);
            parameter.Precision = 10;
            parameter.Scale = 2;
            parameter.Value = value;
        }

        private static void AddNullableString(
            SqlCommand command, string name, int size, string value)
        {
            command.Parameters.Add(name, SqlDbType.NVarChar, size).Value =
                string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }
    }
}
