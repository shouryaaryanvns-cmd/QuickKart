using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using QuickKart.Data;
using QuickKart.Models;

namespace QuickKart.Services
{
    public sealed class AdminService
    {
        public IList<CategoryAdminRecord> GetCategories()
        {
            var records = new List<CategoryAdminRecord>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(@"
SELECT c.CategoryId, c.CategoryName, c.CategoryImage, c.IsActive,
       COUNT(p.ProductId) ProductCount
FROM dbo.Categories c
LEFT JOIN dbo.Products p ON p.CategoryId=c.CategoryId
GROUP BY c.CategoryId,c.CategoryName,c.CategoryImage,c.IsActive
ORDER BY c.CategoryName;", connection))
            {
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        records.Add(new CategoryAdminRecord
                        {
                            CategoryId = reader.GetInt32(0),
                            CategoryName = reader.GetString(1),
                            CategoryImage = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            IsActive = reader.GetBoolean(3),
                            ProductCount = reader.GetInt32(4)
                        });
                    }
                }
            }
            return records;
        }

        public int AddCategory(string name, string imagePath)
        {
            ValidateName(name, "Category name");
            try
            {
                using (SqlConnection connection = DbConnectionFactory.Create())
                using (SqlCommand command = new SqlCommand(@"
INSERT dbo.Categories (CategoryName,CategoryImage,IsActive)
VALUES (@Name,@Image,1);
SELECT CONVERT(int,SCOPE_IDENTITY());", connection))
                {
                    command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = name.Trim();
                    AddNullable(command, "@Image", 255, imagePath);
                    connection.Open();
                    return (int)command.ExecuteScalar();
                }
            }
            catch (SqlException exception)
            {
                if (exception.Number == 2601 || exception.Number == 2627)
                    throw new InvalidOperationException("A category with this name already exists.");
                throw;
            }
        }

        public void UpdateCategory(CategoryAdminRecord category)
        {
            if (category == null) throw new ArgumentNullException("category");
            ValidateName(category.CategoryName, "Category name");
            try
            {
                using (SqlConnection connection = DbConnectionFactory.Create())
                using (SqlCommand command = new SqlCommand(@"
UPDATE dbo.Categories
SET CategoryName=@Name,CategoryImage=@Image,IsActive=@IsActive
WHERE CategoryId=@CategoryId;", connection))
                {
                    command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = category.CategoryName.Trim();
                    AddNullable(command, "@Image", 255, category.CategoryImage);
                    command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = category.IsActive;
                    command.Parameters.Add("@CategoryId", SqlDbType.Int).Value = category.CategoryId;
                    connection.Open();
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("Category was not found.");
                }
            }
            catch (SqlException exception)
            {
                if (exception.Number == 2601 || exception.Number == 2627)
                    throw new InvalidOperationException("A category with this name already exists.");
                throw;
            }
        }

        public IList<ProductAdminRecord> GetProducts(string searchText)
        {
            var records = new List<ProductAdminRecord>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(@"
SELECT p.ProductId,p.CategoryId,c.CategoryName,p.ProductName,p.Description,
       p.Brand,p.Price,p.Discount,p.Stock,p.Unit,p.ProductImage,
       p.IsAvailable,p.ReorderLevel
FROM dbo.Products p
JOIN dbo.Categories c ON c.CategoryId=p.CategoryId
WHERE @Search=N'' OR p.ProductName LIKE N'%'+@Search+N'%'
   OR ISNULL(p.Brand,N'') LIKE N'%'+@Search+N'%'
   OR c.CategoryName LIKE N'%'+@Search+N'%'
ORDER BY p.ProductName;", connection))
            {
                command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value =
                    (searchText ?? string.Empty).Trim();
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        records.Add(new ProductAdminRecord
                        {
                            ProductId = reader.GetInt32(0), CategoryId = reader.GetInt32(1),
                            CategoryName = reader.GetString(2), ProductName = reader.GetString(3),
                            Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                            Brand = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                            Price = reader.GetDecimal(6), Discount = reader.GetDecimal(7),
                            Stock = reader.GetInt32(8), Unit = reader.GetString(9),
                            ProductImage = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                            IsAvailable = reader.GetBoolean(11), ReorderLevel = reader.GetInt32(12)
                        });
                    }
                }
            }
            return records;
        }

        public int AddProduct(ProductAdminRecord product, int adminId)
        {
            ValidateProduct(product);
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int productId;
                        using (SqlCommand command = new SqlCommand(@"
INSERT dbo.Products
    (CategoryId,ProductName,Description,Brand,Price,Discount,Stock,Unit,
     ProductImage,IsAvailable,CreatedDate,ReorderLevel)
VALUES
    (@CategoryId,@Name,@Description,@Brand,@Price,@Discount,@Stock,@Unit,
     @Image,@Available,GETDATE(),@ReorderLevel);
SELECT CONVERT(int,SCOPE_IDENTITY());", connection, transaction))
                        {
                            AddProductParameters(command, product, false);
                            productId = (int)command.ExecuteScalar();
                        }
                        if (product.Stock > 0)
                            InsertInventory(connection, transaction, productId, "StockIn",
                                product.Stock, null, "Initial product stock", adminId);
                        transaction.Commit();
                        return productId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public void UpdateProduct(ProductAdminRecord product)
        {
            ValidateProduct(product);
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(@"
UPDATE dbo.Products
SET CategoryId=@CategoryId,ProductName=@Name,Description=@Description,
    Brand=@Brand,Price=@Price,Discount=@Discount,Unit=@Unit,
    ProductImage=@Image,IsAvailable=@Available,ReorderLevel=@ReorderLevel
WHERE ProductId=@ProductId;", connection))
            {
                AddProductParameters(command, product, true);
                connection.Open();
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("Product was not found.");
            }
        }

        public IList<InventoryRecord> GetInventory(string searchText, bool lowStockOnly)
        {
            var records = new List<InventoryRecord>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(@"
SELECT p.ProductId,p.ProductName,c.CategoryName,p.Stock,p.ReorderLevel,
       CASE WHEN p.Stock=0 THEN N'Out of stock'
            WHEN p.Stock<=p.ReorderLevel THEN N'Low stock'
            ELSE N'In stock' END StockStatus,
       p.IsAvailable
FROM dbo.Products p
JOIN dbo.Categories c ON c.CategoryId=p.CategoryId
WHERE (@Search=N'' OR p.ProductName LIKE N'%'+@Search+N'%'
       OR c.CategoryName LIKE N'%'+@Search+N'%')
  AND (@LowOnly=0 OR p.Stock<=p.ReorderLevel)
ORDER BY CASE WHEN p.Stock<=p.ReorderLevel THEN 0 ELSE 1 END,p.Stock,p.ProductName;", connection))
            {
                command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value =
                    (searchText ?? string.Empty).Trim();
                command.Parameters.Add("@LowOnly", SqlDbType.Bit).Value = lowStockOnly;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        records.Add(new InventoryRecord
                        {
                            ProductId = reader.GetInt32(0), ProductName = reader.GetString(1),
                            CategoryName = reader.GetString(2), Stock = reader.GetInt32(3),
                            ReorderLevel = reader.GetInt32(4), StockStatus = reader.GetString(5),
                            IsAvailable = reader.GetBoolean(6)
                        });
                    }
                }
            }
            return records;
        }

        public void AdjustStock(int adminId, int productId, int quantityChange, string remarks)
        {
            if (quantityChange == 0)
                throw new InvalidOperationException("Stock adjustment cannot be zero.");
            if (string.IsNullOrWhiteSpace(remarks))
                throw new InvalidOperationException("Enter a reason for the stock adjustment.");

            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        using (SqlCommand command = new SqlCommand(@"
UPDATE dbo.Products
SET Stock=Stock+@Change
WHERE ProductId=@ProductId AND Stock+@Change>=0;", connection, transaction))
                        {
                            command.Parameters.Add("@Change", SqlDbType.Int).Value = quantityChange;
                            command.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;
                            if (command.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException("Adjustment would make stock negative.");
                        }
                        InsertInventory(connection, transaction, productId,
                            quantityChange > 0 ? "StockIn" : "Adjustment",
                            quantityChange, null, remarks.Trim(), adminId);
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

        public IList<CustomerAdminRecord> GetCustomers(string searchText)
        {
            var records = new List<CustomerAdminRecord>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(@"
SELECT u.UserId,u.FullName,u.Email,u.PhoneNumber,u.IsActive,u.CreatedDate,
       COUNT(o.OrderId) OrderCount,
       COALESCE(SUM(CASE WHEN o.OrderStatus<>N'Cancelled' THEN o.TotalAmount ELSE 0 END),0) TotalSpent
FROM dbo.Users u
LEFT JOIN dbo.Orders o ON o.UserId=u.UserId
WHERE @Search=N'' OR u.FullName LIKE N'%'+@Search+N'%'
   OR u.Email LIKE N'%'+@Search+N'%' OR u.PhoneNumber LIKE '%'+@Search+'%'
GROUP BY u.UserId,u.FullName,u.Email,u.PhoneNumber,u.IsActive,u.CreatedDate
ORDER BY u.CreatedDate DESC;", connection))
            {
                command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value =
                    (searchText ?? string.Empty).Trim();
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        records.Add(new CustomerAdminRecord
                        {
                            UserId = reader.GetInt32(0), FullName = reader.GetString(1),
                            Email = reader.GetString(2), PhoneNumber = reader.GetString(3),
                            IsActive = reader.GetBoolean(4), CreatedDate = reader.GetDateTime(5),
                            OrderCount = reader.GetInt32(6), TotalSpent = reader.GetDecimal(7)
                        });
                    }
                }
            }
            return records;
        }

        public void SetCustomerActive(int userId, bool active)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                if (!active)
                {
                    using (SqlCommand check = new SqlCommand(@"
SELECT COUNT(*) FROM dbo.Orders
WHERE UserId=@UserId AND OrderStatus IN (N'Pending',N'Confirmed',N'Packed');", connection))
                    {
                        check.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                        if ((int)check.ExecuteScalar() > 0)
                            throw new InvalidOperationException(
                                "This customer has an active order and cannot be deactivated.");
                    }
                }
                using (SqlCommand command = new SqlCommand(
                    "UPDATE dbo.Users SET IsActive=@Active WHERE UserId=@UserId;", connection))
                {
                    command.Parameters.Add("@Active", SqlDbType.Bit).Value = active;
                    command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    command.ExecuteNonQuery();
                }
            }
        }

        public SalesSummary GetSalesSummary(DateTime fromDate, DateTime toDate)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(@"
SELECT
 (SELECT COUNT(*) FROM dbo.Orders
   WHERE OrderStatus<>N'Cancelled' AND OrderDate>=@From AND OrderDate<DATEADD(day,1,@To)),
 (SELECT COALESCE(SUM(TotalAmount),0) FROM dbo.Orders
   WHERE OrderStatus<>N'Cancelled' AND OrderDate>=@From AND OrderDate<DATEADD(day,1,@To)),
 (SELECT COALESCE(AVG(TotalAmount),0) FROM dbo.Orders
   WHERE OrderStatus<>N'Cancelled' AND OrderDate>=@From AND OrderDate<DATEADD(day,1,@To)),
 (SELECT COALESCE(SUM(oi.Quantity),0) FROM dbo.OrderItems oi
   JOIN dbo.Orders o ON o.OrderId=oi.OrderId
   WHERE o.OrderStatus<>N'Cancelled' AND o.OrderDate>=@From AND o.OrderDate<DATEADD(day,1,@To)),
 (SELECT COUNT(*) FROM dbo.Products WHERE Stock<=ReorderLevel);", connection))
            {
                command.Parameters.Add("@From", SqlDbType.Date).Value = fromDate.Date;
                command.Parameters.Add("@To", SqlDbType.Date).Value = toDate.Date;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    reader.Read();
                    return new SalesSummary
                    {
                        TotalOrders = reader.GetInt32(0), TotalRevenue = reader.GetDecimal(1),
                        AverageOrderValue = reader.GetDecimal(2), ProductsSold = reader.GetInt32(3),
                        LowStockProducts = reader.GetInt32(4)
                    };
                }
            }
        }

        public IList<DailySalesRecord> GetDailySales(DateTime fromDate, DateTime toDate)
        {
            var records = new List<DailySalesRecord>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(@"
SELECT CONVERT(date,OrderDate),COUNT(*),SUM(TotalAmount)
FROM dbo.Orders
WHERE OrderStatus<>N'Cancelled' AND OrderDate>=@From AND OrderDate<DATEADD(day,1,@To)
GROUP BY CONVERT(date,OrderDate)
ORDER BY CONVERT(date,OrderDate) DESC;", connection))
            {
                command.Parameters.Add("@From", SqlDbType.Date).Value = fromDate.Date;
                command.Parameters.Add("@To", SqlDbType.Date).Value = toDate.Date;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        records.Add(new DailySalesRecord
                        {
                            SalesDate = reader.GetDateTime(0), TotalOrders = reader.GetInt32(1),
                            TotalSales = reader.GetDecimal(2)
                        });
            }
            return records;
        }

        public IList<TopProductRecord> GetTopProducts(DateTime fromDate, DateTime toDate)
        {
            var records = new List<TopProductRecord>();
            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(@"
SELECT TOP 10 oi.ProductName,SUM(oi.Quantity),SUM(oi.Quantity*oi.Price)
FROM dbo.OrderItems oi
JOIN dbo.Orders o ON o.OrderId=oi.OrderId
WHERE o.OrderStatus<>N'Cancelled' AND o.OrderDate>=@From AND o.OrderDate<DATEADD(day,1,@To)
GROUP BY oi.ProductName
ORDER BY SUM(oi.Quantity) DESC,oi.ProductName;", connection))
            {
                command.Parameters.Add("@From", SqlDbType.Date).Value = fromDate.Date;
                command.Parameters.Add("@To", SqlDbType.Date).Value = toDate.Date;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        records.Add(new TopProductRecord
                        {
                            ProductName = reader.GetString(0), QuantitySold = reader.GetInt32(1),
                            SalesAmount = reader.GetDecimal(2)
                        });
            }
            return records;
        }

        private static void ValidateProduct(ProductAdminRecord product)
        {
            if (product == null) throw new ArgumentNullException("product");
            ValidateName(product.ProductName, "Product name");
            if (product.CategoryId <= 0) throw new InvalidOperationException("Select a category.");
            if (string.IsNullOrWhiteSpace(product.Unit)) throw new InvalidOperationException("Unit is required.");
            if (product.Price < 0) throw new InvalidOperationException("Price cannot be negative.");
            if (product.Discount < 0 || product.Discount > 100)
                throw new InvalidOperationException("Discount must be between 0 and 100.");
            if (product.Stock < 0 || product.ReorderLevel < 0)
                throw new InvalidOperationException("Stock values cannot be negative.");
        }

        private static void AddProductParameters(
            SqlCommand command, ProductAdminRecord product, bool includeId)
        {
            command.Parameters.Add("@CategoryId", SqlDbType.Int).Value = product.CategoryId;
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = product.ProductName.Trim();
            AddNullable(command, "@Description", -1, product.Description);
            AddNullable(command, "@Brand", 100, product.Brand);
            AddDecimal(command, "@Price", product.Price);
            AddDecimal(command, "@Discount", product.Discount);
            command.Parameters.Add("@Stock", SqlDbType.Int).Value = product.Stock;
            command.Parameters.Add("@Unit", SqlDbType.NVarChar, 50).Value = product.Unit.Trim();
            AddNullable(command, "@Image", 255, product.ProductImage);
            command.Parameters.Add("@Available", SqlDbType.Bit).Value = product.IsAvailable;
            command.Parameters.Add("@ReorderLevel", SqlDbType.Int).Value = product.ReorderLevel;
            if (includeId)
                command.Parameters.Add("@ProductId", SqlDbType.Int).Value = product.ProductId;
        }

        private static void InsertInventory(SqlConnection connection, SqlTransaction transaction,
            int productId, string type, int change, int? referenceId, string remarks, int? adminId)
        {
            using (SqlCommand command = new SqlCommand(@"
INSERT dbo.InventoryTransactions
    (ProductId,TransactionType,QuantityChange,ReferenceId,Remarks,AdminId)
VALUES (@ProductId,@Type,@Change,@ReferenceId,@Remarks,@AdminId);", connection, transaction))
            {
                command.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;
                command.Parameters.Add("@Type", SqlDbType.NVarChar, 30).Value = type;
                command.Parameters.Add("@Change", SqlDbType.Int).Value = change;
                command.Parameters.Add("@ReferenceId", SqlDbType.Int).Value = referenceId.HasValue ? (object)referenceId.Value : DBNull.Value;
                command.Parameters.Add("@Remarks", SqlDbType.NVarChar, 250).Value = remarks;
                command.Parameters.Add("@AdminId", SqlDbType.Int).Value = adminId.HasValue ? (object)adminId.Value : DBNull.Value;
                command.ExecuteNonQuery();
            }
        }

        private static void ValidateName(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 2)
                throw new InvalidOperationException(label + " is required.");
        }

        private static void AddNullable(SqlCommand command, string name, int size, string value)
        {
            SqlParameter parameter = size == -1
                ? command.Parameters.Add(name, SqlDbType.NVarChar, -1)
                : command.Parameters.Add(name, SqlDbType.NVarChar, size);
            parameter.Value = string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }

        private static void AddDecimal(SqlCommand command, string name, decimal value)
        {
            SqlParameter parameter = command.Parameters.Add(name, SqlDbType.Decimal);
            parameter.Precision = 10; parameter.Scale = 2; parameter.Value = value;
        }
    }
}
