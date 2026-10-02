/*
    QuickKartDB enhancement migration
    Target: SQL Server 2017 / QuickKartDB

    Adds:
      - Required-column enforcement
      - Validation constraints
      - Uniqueness and performance indexes
      - Inventory transaction history
      - Product-to-offer mapping
      - Historical order address and product-name snapshots
      - Starter administrator, categories, products, customers and orders

    Initial demo credentials:
      Admin:    admin / QuickKart@123
      Customer: arjun.demo@quickkart.local / Customer@123
      Customer: priya.demo@quickkart.local / Customer@123

    Password format:
      PBKDF2-SHA256$iterations$base64Salt$base64Hash
*/

USE [QuickKartDB];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/*
    SQL Server compiles references to columns on existing tables before
    executing the batch. Add new columns in a guarded prerequisite batch
    so all later references compile correctly. These operations are
    additive and safe to rerun.
*/
BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH('dbo.Admin', 'IsActive') IS NULL
        ALTER TABLE dbo.Admin
            ADD IsActive bit NOT NULL
                CONSTRAINT DF_Admin_IsActive DEFAULT (1);

    IF COL_LENGTH('dbo.Admin', 'CreatedDate') IS NULL
        ALTER TABLE dbo.Admin
            ADD CreatedDate datetime2(0) NOT NULL
                CONSTRAINT DF_Admin_CreatedDate DEFAULT (SYSDATETIME());

    IF COL_LENGTH('dbo.Products', 'ReorderLevel') IS NULL
        ALTER TABLE dbo.Products
            ADD ReorderLevel int NOT NULL
                CONSTRAINT DF_Products_ReorderLevel DEFAULT (5);

    IF COL_LENGTH('dbo.Orders', 'DeliveryAddressSnapshot') IS NULL
        ALTER TABLE dbo.Orders ADD DeliveryAddressSnapshot nvarchar(600) NULL;

    IF COL_LENGTH('dbo.OrderItems', 'ProductName') IS NULL
        ALTER TABLE dbo.OrderItems ADD ProductName nvarchar(200) NULL;

    IF COL_LENGTH('dbo.Offers', 'IsActive') IS NULL
        ALTER TABLE dbo.Offers
            ADD IsActive bit NOT NULL
                CONSTRAINT DF_Offers_IsActive DEFAULT (1);

    IF COL_LENGTH('dbo.Offers', 'CreatedDate') IS NULL
        ALTER TABLE dbo.Offers
            ADD CreatedDate datetime2(0) NOT NULL
                CONSTRAINT DF_Offers_CreatedDate DEFAULT (SYSDATETIME());

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    /* ------------------------------------------------------------------
       1. Required columns and defaults
       ------------------------------------------------------------------ */

    UPDATE dbo.Admin
       SET Username = CONCAT(N'admin_', AdminId)
     WHERE NULLIF(LTRIM(RTRIM(Username)), N'') IS NULL;

    UPDATE dbo.Admin
       SET PasswordHash = N'LOGIN_DISABLED'
     WHERE NULLIF(LTRIM(RTRIM(PasswordHash)), N'') IS NULL;

    UPDATE dbo.Admin
       SET FullName = N'Administrator'
     WHERE NULLIF(LTRIM(RTRIM(FullName)), N'') IS NULL;

    UPDATE dbo.Admin
       SET Email = CONCAT(N'admin_', AdminId, N'@quickkart.local')
     WHERE NULLIF(LTRIM(RTRIM(Email)), N'') IS NULL;

    UPDATE dbo.Admin
       SET Role = N'Admin'
     WHERE NULLIF(LTRIM(RTRIM(Role)), N'') IS NULL;

    ALTER TABLE dbo.Admin ALTER COLUMN Username nvarchar(50) NOT NULL;
    ALTER TABLE dbo.Admin ALTER COLUMN PasswordHash nvarchar(255) NOT NULL;
    ALTER TABLE dbo.Admin ALTER COLUMN FullName nvarchar(100) NOT NULL;
    ALTER TABLE dbo.Admin ALTER COLUMN Email nvarchar(100) NOT NULL;
    ALTER TABLE dbo.Admin ALTER COLUMN Role nvarchar(50) NOT NULL;

    IF COL_LENGTH('dbo.Admin', 'IsActive') IS NULL
        ALTER TABLE dbo.Admin
            ADD IsActive bit NOT NULL
                CONSTRAINT DF_Admin_IsActive DEFAULT (1);

    IF COL_LENGTH('dbo.Admin', 'CreatedDate') IS NULL
        ALTER TABLE dbo.Admin
            ADD CreatedDate datetime2(0) NOT NULL
                CONSTRAINT DF_Admin_CreatedDate DEFAULT (SYSDATETIME());

    UPDATE dbo.Categories SET IsActive = 1 WHERE IsActive IS NULL;
    ALTER TABLE dbo.Categories ALTER COLUMN IsActive bit NOT NULL;

    UPDATE dbo.Products SET Discount = 0 WHERE Discount IS NULL;
    UPDATE dbo.Products SET Stock = 0 WHERE Stock IS NULL;
    UPDATE dbo.Products SET Unit = N'Unit' WHERE NULLIF(LTRIM(RTRIM(Unit)), N'') IS NULL;
    UPDATE dbo.Products SET IsAvailable = 1 WHERE IsAvailable IS NULL;
    UPDATE dbo.Products SET CreatedDate = GETDATE() WHERE CreatedDate IS NULL;

    ALTER TABLE dbo.Products ALTER COLUMN Discount decimal(10,2) NOT NULL;
    ALTER TABLE dbo.Products ALTER COLUMN Stock int NOT NULL;
    ALTER TABLE dbo.Products ALTER COLUMN Unit nvarchar(50) NOT NULL;
    ALTER TABLE dbo.Products ALTER COLUMN IsAvailable bit NOT NULL;
    ALTER TABLE dbo.Products ALTER COLUMN CreatedDate datetime NOT NULL;

    IF COL_LENGTH('dbo.Products', 'ReorderLevel') IS NULL
        ALTER TABLE dbo.Products
            ADD ReorderLevel int NOT NULL
                CONSTRAINT DF_Products_ReorderLevel DEFAULT (5);

    UPDATE dbo.Users SET IsActive = 1 WHERE IsActive IS NULL;
    UPDATE dbo.Users SET CreatedDate = GETDATE() WHERE CreatedDate IS NULL;
    ALTER TABLE dbo.Users ALTER COLUMN IsActive bit NOT NULL;
    ALTER TABLE dbo.Users ALTER COLUMN CreatedDate datetime NOT NULL;

    UPDATE dbo.Cart SET CreatedDate = GETDATE() WHERE CreatedDate IS NULL;
    ALTER TABLE dbo.Cart ALTER COLUMN CreatedDate datetime NOT NULL;

    UPDATE dbo.CartItems SET Quantity = 1 WHERE Quantity IS NULL OR Quantity <= 0;
    UPDATE ci
       SET Price = p.Price
      FROM dbo.CartItems AS ci
      JOIN dbo.Products AS p ON p.ProductId = ci.ProductId
     WHERE ci.Price IS NULL OR ci.Price < 0;
    ALTER TABLE dbo.CartItems ALTER COLUMN Quantity int NOT NULL;
    ALTER TABLE dbo.CartItems ALTER COLUMN Price decimal(10,2) NOT NULL;

    UPDATE dbo.Wishlist SET AddedDate = GETDATE() WHERE AddedDate IS NULL;
    ALTER TABLE dbo.Wishlist ALTER COLUMN AddedDate datetime NOT NULL;

    UPDATE dbo.Orders SET OrderDate = GETDATE() WHERE OrderDate IS NULL;
    UPDATE dbo.Orders SET TotalAmount = 0 WHERE TotalAmount IS NULL OR TotalAmount < 0;
    UPDATE dbo.Orders
       SET OrderStatus = N'Pending'
     WHERE NULLIF(LTRIM(RTRIM(OrderStatus)), N'') IS NULL;
    UPDATE dbo.Orders
       SET PaymentStatus = N'Pending'
     WHERE NULLIF(LTRIM(RTRIM(PaymentStatus)), N'') IS NULL;
    UPDATE dbo.Orders
       SET PaymentMethod = N'Cash on Delivery'
     WHERE NULLIF(LTRIM(RTRIM(PaymentMethod)), N'') IS NULL;

    ALTER TABLE dbo.Orders ALTER COLUMN OrderDate datetime NOT NULL;
    ALTER TABLE dbo.Orders ALTER COLUMN TotalAmount decimal(10,2) NOT NULL;
    ALTER TABLE dbo.Orders ALTER COLUMN OrderStatus nvarchar(30) NOT NULL;
    ALTER TABLE dbo.Orders ALTER COLUMN PaymentStatus nvarchar(30) NOT NULL;
    ALTER TABLE dbo.Orders ALTER COLUMN PaymentMethod nvarchar(50) NOT NULL;

    IF COL_LENGTH('dbo.Orders', 'DeliveryAddressSnapshot') IS NULL
        ALTER TABLE dbo.Orders ADD DeliveryAddressSnapshot nvarchar(600) NULL;

    UPDATE o
       SET DeliveryAddressSnapshot =
           CONCAT(
               NULLIF(a.HouseNo, N''), N', ',
               NULLIF(a.Street, N''), N', ',
               NULLIF(a.City, N''), N', ',
               NULLIF(a.State, N''), N' - ',
               NULLIF(a.Pincode, '')
           )
      FROM dbo.Orders AS o
      JOIN dbo.Address AS a ON a.AddressId = o.AddressId
     WHERE NULLIF(LTRIM(RTRIM(o.DeliveryAddressSnapshot)), N'') IS NULL;

    UPDATE dbo.Orders
       SET DeliveryAddressSnapshot = N'Address unavailable'
     WHERE NULLIF(LTRIM(RTRIM(DeliveryAddressSnapshot)), N'') IS NULL;

    ALTER TABLE dbo.Orders
        ALTER COLUMN DeliveryAddressSnapshot nvarchar(600) NOT NULL;

    IF COL_LENGTH('dbo.OrderItems', 'ProductName') IS NULL
        ALTER TABLE dbo.OrderItems ADD ProductName nvarchar(200) NULL;

    UPDATE oi
       SET ProductName = p.ProductName
      FROM dbo.OrderItems AS oi
      JOIN dbo.Products AS p ON p.ProductId = oi.ProductId
     WHERE NULLIF(LTRIM(RTRIM(oi.ProductName)), N'') IS NULL;

    UPDATE dbo.OrderItems
       SET ProductName = N'Unknown Product'
     WHERE NULLIF(LTRIM(RTRIM(ProductName)), N'') IS NULL;

    UPDATE dbo.OrderItems SET Quantity = 1 WHERE Quantity IS NULL OR Quantity <= 0;
    UPDATE oi
       SET Price = p.Price
      FROM dbo.OrderItems AS oi
      JOIN dbo.Products AS p ON p.ProductId = oi.ProductId
     WHERE oi.Price IS NULL OR oi.Price < 0;

    ALTER TABLE dbo.OrderItems ALTER COLUMN ProductName nvarchar(200) NOT NULL;
    ALTER TABLE dbo.OrderItems ALTER COLUMN Quantity int NOT NULL;
    ALTER TABLE dbo.OrderItems ALTER COLUMN Price decimal(10,2) NOT NULL;

    UPDATE dbo.Payments SET PaymentMethod = N'Cash on Delivery'
     WHERE NULLIF(LTRIM(RTRIM(PaymentMethod)), N'') IS NULL;
    UPDATE dbo.Payments SET Amount = 0 WHERE Amount IS NULL OR Amount < 0;
    UPDATE dbo.Payments SET PaymentDate = GETDATE() WHERE PaymentDate IS NULL;
    UPDATE dbo.Payments SET PaymentStatus = N'Pending'
     WHERE NULLIF(LTRIM(RTRIM(PaymentStatus)), N'') IS NULL;

    ALTER TABLE dbo.Payments ALTER COLUMN PaymentMethod nvarchar(50) NOT NULL;
    ALTER TABLE dbo.Payments ALTER COLUMN Amount decimal(10,2) NOT NULL;
    ALTER TABLE dbo.Payments ALTER COLUMN PaymentDate datetime NOT NULL;
    ALTER TABLE dbo.Payments ALTER COLUMN PaymentStatus nvarchar(30) NOT NULL;

    UPDATE dbo.Delivery
       SET DeliveryStatus = N'Assigned'
     WHERE NULLIF(LTRIM(RTRIM(DeliveryStatus)), N'') IS NULL;
    ALTER TABLE dbo.Delivery ALTER COLUMN DeliveryStatus nvarchar(50) NOT NULL;

    IF NOT EXISTS (
        SELECT 1
          FROM sys.default_constraints
         WHERE parent_object_id = OBJECT_ID('dbo.Delivery')
           AND COL_NAME(parent_object_id, parent_column_id) = 'DeliveryStatus'
    )
        ALTER TABLE dbo.Delivery
            ADD CONSTRAINT DF_Delivery_DeliveryStatus DEFAULT (N'Assigned')
                FOR DeliveryStatus;

    UPDATE dbo.DeliveryPartners
       SET PartnerName = CONCAT(N'Delivery Partner ', DeliveryPartnerId)
     WHERE NULLIF(LTRIM(RTRIM(PartnerName)), N'') IS NULL;
    UPDATE dbo.DeliveryPartners SET Mobile = '0000000000'
     WHERE NULLIF(LTRIM(RTRIM(Mobile)), '') IS NULL;
    UPDATE dbo.DeliveryPartners SET IsAvailable = 1 WHERE IsAvailable IS NULL;

    ALTER TABLE dbo.DeliveryPartners ALTER COLUMN PartnerName nvarchar(100) NOT NULL;
    ALTER TABLE dbo.DeliveryPartners ALTER COLUMN Mobile varchar(15) NOT NULL;
    ALTER TABLE dbo.DeliveryPartners ALTER COLUMN IsAvailable bit NOT NULL;

    UPDATE dbo.Offers
       SET OfferTitle = CONCAT(N'Offer ', OfferId)
     WHERE NULLIF(LTRIM(RTRIM(OfferTitle)), N'') IS NULL;
    UPDATE dbo.Offers SET DiscountPercent = 0 WHERE DiscountPercent IS NULL;
    UPDATE dbo.Offers SET StartDate = CONVERT(date, GETDATE()) WHERE StartDate IS NULL;
    UPDATE dbo.Offers SET EndDate = StartDate WHERE EndDate IS NULL OR EndDate < StartDate;

    ALTER TABLE dbo.Offers ALTER COLUMN OfferTitle nvarchar(200) NOT NULL;
    ALTER TABLE dbo.Offers ALTER COLUMN DiscountPercent decimal(5,2) NOT NULL;
    ALTER TABLE dbo.Offers ALTER COLUMN StartDate date NOT NULL;
    ALTER TABLE dbo.Offers ALTER COLUMN EndDate date NOT NULL;

    IF COL_LENGTH('dbo.Offers', 'IsActive') IS NULL
        ALTER TABLE dbo.Offers
            ADD IsActive bit NOT NULL
                CONSTRAINT DF_Offers_IsActive DEFAULT (1);

    IF COL_LENGTH('dbo.Offers', 'CreatedDate') IS NULL
        ALTER TABLE dbo.Offers
            ADD CreatedDate datetime2(0) NOT NULL
                CONSTRAINT DF_Offers_CreatedDate DEFAULT (SYSDATETIME());

    UPDATE dbo.Address SET City = N'Unknown' WHERE NULLIF(LTRIM(RTRIM(City)), N'') IS NULL;
    UPDATE dbo.Address SET State = N'Unknown' WHERE NULLIF(LTRIM(RTRIM(State)), N'') IS NULL;
    UPDATE dbo.Address SET Pincode = '000000' WHERE NULLIF(LTRIM(RTRIM(Pincode)), '') IS NULL;
    ALTER TABLE dbo.Address ALTER COLUMN City nvarchar(100) NOT NULL;
    ALTER TABLE dbo.Address ALTER COLUMN State nvarchar(100) NOT NULL;
    ALTER TABLE dbo.Address ALTER COLUMN Pincode varchar(10) NOT NULL;

    /* ------------------------------------------------------------------
       2. Validation constraints
       ------------------------------------------------------------------ */

    IF OBJECT_ID('dbo.CK_Products_Price', 'C') IS NULL
        ALTER TABLE dbo.Products WITH CHECK
            ADD CONSTRAINT CK_Products_Price CHECK (Price >= 0);

    IF OBJECT_ID('dbo.CK_Products_Discount', 'C') IS NULL
        ALTER TABLE dbo.Products WITH CHECK
            ADD CONSTRAINT CK_Products_Discount CHECK (Discount >= 0 AND Discount <= 100);

    IF OBJECT_ID('dbo.CK_Products_Stock', 'C') IS NULL
        ALTER TABLE dbo.Products WITH CHECK
            ADD CONSTRAINT CK_Products_Stock CHECK (Stock >= 0);

    IF OBJECT_ID('dbo.CK_Products_ReorderLevel', 'C') IS NULL
        ALTER TABLE dbo.Products WITH CHECK
            ADD CONSTRAINT CK_Products_ReorderLevel CHECK (ReorderLevel >= 0);

    IF OBJECT_ID('dbo.CK_CartItems_Quantity', 'C') IS NULL
        ALTER TABLE dbo.CartItems WITH CHECK
            ADD CONSTRAINT CK_CartItems_Quantity CHECK (Quantity > 0);

    IF OBJECT_ID('dbo.CK_CartItems_Price', 'C') IS NULL
        ALTER TABLE dbo.CartItems WITH CHECK
            ADD CONSTRAINT CK_CartItems_Price CHECK (Price >= 0);

    IF OBJECT_ID('dbo.CK_OrderItems_Quantity', 'C') IS NULL
        ALTER TABLE dbo.OrderItems WITH CHECK
            ADD CONSTRAINT CK_OrderItems_Quantity CHECK (Quantity > 0);

    IF OBJECT_ID('dbo.CK_OrderItems_Price', 'C') IS NULL
        ALTER TABLE dbo.OrderItems WITH CHECK
            ADD CONSTRAINT CK_OrderItems_Price CHECK (Price >= 0);

    IF OBJECT_ID('dbo.CK_Orders_TotalAmount', 'C') IS NULL
        ALTER TABLE dbo.Orders WITH CHECK
            ADD CONSTRAINT CK_Orders_TotalAmount CHECK (TotalAmount >= 0);

    IF OBJECT_ID('dbo.CK_Orders_OrderStatus', 'C') IS NULL
        ALTER TABLE dbo.Orders WITH CHECK
            ADD CONSTRAINT CK_Orders_OrderStatus
                CHECK (OrderStatus IN (N'Pending', N'Confirmed', N'Packed', N'Delivered', N'Cancelled'));

    IF OBJECT_ID('dbo.CK_Orders_PaymentStatus', 'C') IS NULL
        ALTER TABLE dbo.Orders WITH CHECK
            ADD CONSTRAINT CK_Orders_PaymentStatus
                CHECK (PaymentStatus IN (N'Pending', N'Paid', N'Failed', N'Refunded'));

    IF OBJECT_ID('dbo.CK_Orders_PaymentMethod', 'C') IS NULL
        ALTER TABLE dbo.Orders WITH CHECK
            ADD CONSTRAINT CK_Orders_PaymentMethod
                CHECK (PaymentMethod IN (N'Cash', N'Card', N'UPI', N'Cash on Delivery'));

    IF OBJECT_ID('dbo.CK_Payments_Amount', 'C') IS NULL
        ALTER TABLE dbo.Payments WITH CHECK
            ADD CONSTRAINT CK_Payments_Amount CHECK (Amount >= 0);

    IF OBJECT_ID('dbo.CK_Payments_Status', 'C') IS NULL
        ALTER TABLE dbo.Payments WITH CHECK
            ADD CONSTRAINT CK_Payments_Status
                CHECK (PaymentStatus IN (N'Pending', N'Paid', N'Failed', N'Refunded'));

    IF OBJECT_ID('dbo.CK_Payments_Method', 'C') IS NULL
        ALTER TABLE dbo.Payments WITH CHECK
            ADD CONSTRAINT CK_Payments_Method
                CHECK (PaymentMethod IN (N'Cash', N'Card', N'UPI', N'Cash on Delivery'));

    IF OBJECT_ID('dbo.CK_Offers_DiscountPercent', 'C') IS NULL
        ALTER TABLE dbo.Offers WITH CHECK
            ADD CONSTRAINT CK_Offers_DiscountPercent
                CHECK (DiscountPercent >= 0 AND DiscountPercent <= 100);

    IF OBJECT_ID('dbo.CK_Offers_DateRange', 'C') IS NULL
        ALTER TABLE dbo.Offers WITH CHECK
            ADD CONSTRAINT CK_Offers_DateRange CHECK (EndDate >= StartDate);

    IF OBJECT_ID('dbo.CK_Delivery_Status', 'C') IS NULL
        ALTER TABLE dbo.Delivery WITH CHECK
            ADD CONSTRAINT CK_Delivery_Status
                CHECK (DeliveryStatus IN (N'Assigned', N'OutForDelivery', N'Delivered', N'Failed', N'Cancelled'));

    IF OBJECT_ID('dbo.CK_Admin_Role', 'C') IS NULL
        ALTER TABLE dbo.Admin WITH CHECK
            ADD CONSTRAINT CK_Admin_Role CHECK (Role = N'Admin');

    /* ------------------------------------------------------------------
       3. Uniqueness and address-ownership enforcement
       ------------------------------------------------------------------ */

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
         WHERE object_id = OBJECT_ID('dbo.Admin') AND name = 'UQ_Admin_Username'
    )
        ALTER TABLE dbo.Admin ADD CONSTRAINT UQ_Admin_Username UNIQUE (Username);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
         WHERE object_id = OBJECT_ID('dbo.Admin') AND name = 'UQ_Admin_Email'
    )
        ALTER TABLE dbo.Admin ADD CONSTRAINT UQ_Admin_Email UNIQUE (Email);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
         WHERE object_id = OBJECT_ID('dbo.Categories') AND name = 'UQ_Categories_CategoryName'
    )
        ALTER TABLE dbo.Categories
            ADD CONSTRAINT UQ_Categories_CategoryName UNIQUE (CategoryName);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
         WHERE object_id = OBJECT_ID('dbo.Cart') AND name = 'UQ_Cart_UserId'
    )
        ALTER TABLE dbo.Cart ADD CONSTRAINT UQ_Cart_UserId UNIQUE (UserId);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
         WHERE object_id = OBJECT_ID('dbo.CartItems') AND name = 'UQ_CartItems_Cart_Product'
    )
        ALTER TABLE dbo.CartItems
            ADD CONSTRAINT UQ_CartItems_Cart_Product UNIQUE (CartId, ProductId);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
         WHERE object_id = OBJECT_ID('dbo.Wishlist') AND name = 'UQ_Wishlist_User_Product'
    )
        ALTER TABLE dbo.Wishlist
            ADD CONSTRAINT UQ_Wishlist_User_Product UNIQUE (UserId, ProductId);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
         WHERE object_id = OBJECT_ID('dbo.Delivery') AND name = 'UQ_Delivery_OrderId'
    )
        ALTER TABLE dbo.Delivery
            ADD CONSTRAINT UQ_Delivery_OrderId UNIQUE (OrderId);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
         WHERE object_id = OBJECT_ID('dbo.Address') AND name = 'UQ_Address_AddressId_UserId'
    )
        ALTER TABLE dbo.Address
            ADD CONSTRAINT UQ_Address_AddressId_UserId UNIQUE (AddressId, UserId);

    IF OBJECT_ID('dbo.FK_Orders_AddressOwner', 'F') IS NULL
        ALTER TABLE dbo.Orders WITH CHECK
            ADD CONSTRAINT FK_Orders_AddressOwner
                FOREIGN KEY (AddressId, UserId)
                REFERENCES dbo.Address (AddressId, UserId);

    /* ------------------------------------------------------------------
       4. Inventory audit and offer mapping
       ------------------------------------------------------------------ */

    IF OBJECT_ID('dbo.InventoryTransactions', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.InventoryTransactions
        (
            InventoryTransactionId int IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_InventoryTransactions PRIMARY KEY,
            ProductId int NOT NULL,
            TransactionType nvarchar(30) NOT NULL,
            QuantityChange int NOT NULL,
            ReferenceId int NULL,
            Remarks nvarchar(250) NULL,
            AdminId int NULL,
            TransactionDate datetime2(0) NOT NULL
                CONSTRAINT DF_InventoryTransactions_TransactionDate DEFAULT (SYSDATETIME()),
            CONSTRAINT FK_InventoryTransactions_Product
                FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
            CONSTRAINT FK_InventoryTransactions_Admin
                FOREIGN KEY (AdminId) REFERENCES dbo.Admin(AdminId),
            CONSTRAINT CK_InventoryTransactions_QuantityChange
                CHECK (QuantityChange <> 0),
            CONSTRAINT CK_InventoryTransactions_Type
                CHECK (TransactionType IN
                    (N'StockIn', N'Sale', N'OrderCancellation', N'Return', N'Damage', N'Adjustment'))
        );
    END;

    IF OBJECT_ID('dbo.ProductOffers', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ProductOffers
        (
            ProductId int NOT NULL,
            OfferId int NOT NULL,
            CreatedDate datetime2(0) NOT NULL
                CONSTRAINT DF_ProductOffers_CreatedDate DEFAULT (SYSDATETIME()),
            CONSTRAINT PK_ProductOffers PRIMARY KEY (ProductId, OfferId),
            CONSTRAINT FK_ProductOffers_Product
                FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId) ON DELETE CASCADE,
            CONSTRAINT FK_ProductOffers_Offer
                FOREIGN KEY (OfferId) REFERENCES dbo.Offers(OfferId) ON DELETE CASCADE
        );
    END;

    /* ------------------------------------------------------------------
       5. Search and relationship indexes
       ------------------------------------------------------------------ */

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Address') AND name = 'IX_Address_UserId')
        CREATE INDEX IX_Address_UserId ON dbo.Address(UserId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Products') AND name = 'IX_Products_CategoryId')
        CREATE INDEX IX_Products_CategoryId ON dbo.Products(CategoryId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Products') AND name = 'IX_Products_ProductName')
        CREATE INDEX IX_Products_ProductName ON dbo.Products(ProductName);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.CartItems') AND name = 'IX_CartItems_ProductId')
        CREATE INDEX IX_CartItems_ProductId ON dbo.CartItems(ProductId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'IX_Orders_UserId_OrderDate')
        CREATE INDEX IX_Orders_UserId_OrderDate ON dbo.Orders(UserId, OrderDate DESC);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'IX_Orders_Status_OrderDate')
        CREATE INDEX IX_Orders_Status_OrderDate ON dbo.Orders(OrderStatus, OrderDate DESC);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'IX_Orders_AddressId_UserId')
        CREATE INDEX IX_Orders_AddressId_UserId ON dbo.Orders(AddressId, UserId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.OrderItems') AND name = 'IX_OrderItems_OrderId')
        CREATE INDEX IX_OrderItems_OrderId ON dbo.OrderItems(OrderId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.OrderItems') AND name = 'IX_OrderItems_ProductId')
        CREATE INDEX IX_OrderItems_ProductId ON dbo.OrderItems(ProductId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Payments') AND name = 'IX_Payments_OrderId')
        CREATE INDEX IX_Payments_OrderId ON dbo.Payments(OrderId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Delivery') AND name = 'IX_Delivery_DeliveryPartnerId')
        CREATE INDEX IX_Delivery_DeliveryPartnerId ON dbo.Delivery(DeliveryPartnerId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.InventoryTransactions') AND name = 'IX_InventoryTransactions_Product_Date')
        CREATE INDEX IX_InventoryTransactions_Product_Date
            ON dbo.InventoryTransactions(ProductId, TransactionDate DESC);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ProductOffers') AND name = 'IX_ProductOffers_OfferId')
        CREATE INDEX IX_ProductOffers_OfferId ON dbo.ProductOffers(OfferId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Offers') AND name = 'IX_Offers_ActiveDates')
        CREATE INDEX IX_Offers_ActiveDates ON dbo.Offers(IsActive, StartDate, EndDate);

    /* ------------------------------------------------------------------
       6. Starter administrator
       ------------------------------------------------------------------ */

    IF NOT EXISTS (SELECT 1 FROM dbo.Admin WHERE Username = N'admin')
    BEGIN
        INSERT dbo.Admin
            (Username, PasswordHash, FullName, Email, Role, IsActive)
        VALUES
            (
                N'admin',
                N'PBKDF2-SHA256$100000$BaXHKAviLes6rMJFNUkuZg==$N0KC5qgBQgbOnq4bxMkVDZMAscCgyqIVNlJLyTEKPQQ=',
                N'QuickKart Administrator',
                N'admin@quickkart.local',
                N'Admin',
                1
            );
    END;

    /* ------------------------------------------------------------------
       7. Categories and products
       ------------------------------------------------------------------ */

    DECLARE @Categories TABLE
    (
        CategoryName nvarchar(100) NOT NULL,
        CategoryImage nvarchar(255) NULL
    );

    INSERT @Categories (CategoryName, CategoryImage)
    VALUES
        (N'Fruits and Vegetables', N'images/categories/fruits-vegetables.png'),
        (N'Dairy Products', N'images/categories/dairy.png'),
        (N'Rice and Grains', N'images/categories/rice-grains.png'),
        (N'Snacks and Beverages', N'images/categories/snacks-beverages.png'),
        (N'Household Essentials', N'images/categories/household.png');

    INSERT dbo.Categories (CategoryName, CategoryImage, IsActive)
    SELECT s.CategoryName, s.CategoryImage, 1
      FROM @Categories AS s
     WHERE NOT EXISTS (
        SELECT 1 FROM dbo.Categories AS c
         WHERE c.CategoryName = s.CategoryName
     );

    DECLARE @ProductsSeed TABLE
    (
        CategoryName nvarchar(100) NOT NULL,
        ProductName nvarchar(200) NOT NULL,
        Description nvarchar(max) NULL,
        Brand nvarchar(100) NULL,
        Price decimal(10,2) NOT NULL,
        Discount decimal(10,2) NOT NULL,
        Stock int NOT NULL,
        Unit nvarchar(50) NOT NULL,
        ProductImage nvarchar(255) NULL,
        ReorderLevel int NOT NULL
    );

    INSERT @ProductsSeed
        (CategoryName, ProductName, Description, Brand, Price, Discount, Stock, Unit, ProductImage, ReorderLevel)
    VALUES
        (N'Fruits and Vegetables', N'Fresh Apples', N'Fresh red apples', N'QuickKart Fresh', 160.00, 0, 45, N'1 kg', N'images/products/apples.png', 10),
        (N'Fruits and Vegetables', N'Bananas', N'Naturally ripened bananas', N'QuickKart Fresh', 60.00, 0, 60, N'1 dozen', N'images/products/bananas.png', 10),
        (N'Fruits and Vegetables', N'Tomatoes', N'Farm fresh tomatoes', N'QuickKart Fresh', 45.00, 0, 50, N'1 kg', N'images/products/tomatoes.png', 10),
        (N'Fruits and Vegetables', N'Potatoes', N'Fresh cooking potatoes', N'QuickKart Fresh', 40.00, 0, 55, N'1 kg', N'images/products/potatoes.png', 10),
        (N'Dairy Products', N'Toned Milk', N'Pasteurized toned milk', N'Amul', 30.00, 0, 78, N'500 ml', N'images/products/milk.png', 15),
        (N'Dairy Products', N'Fresh Curd', N'Plain fresh curd', N'Amul', 45.00, 5, 35, N'400 g', N'images/products/curd.png', 8),
        (N'Dairy Products', N'Paneer', N'Fresh cottage cheese', N'Amul', 95.00, 0, 25, N'200 g', N'images/products/paneer.png', 5),
        (N'Dairy Products', N'Butter', N'Salted table butter', N'Amul', 58.00, 0, 30, N'100 g', N'images/products/butter.png', 5),
        (N'Rice and Grains', N'Basmati Rice', N'Premium long-grain basmati rice', N'India Gate', 350.00, 5, 38, N'5 kg', N'images/products/basmati-rice.png', 8),
        (N'Rice and Grains', N'Wheat Flour', N'Whole wheat atta', N'Aashirvaad', 285.00, 0, 42, N'5 kg', N'images/products/wheat-flour.png', 8),
        (N'Rice and Grains', N'Toor Dal', N'Unpolished toor dal', N'Tata Sampann', 175.00, 0, 35, N'1 kg', N'images/products/toor-dal.png', 8),
        (N'Rice and Grains', N'Sugar', N'Refined white sugar', N'Madhur', 48.00, 0, 50, N'1 kg', N'images/products/sugar.png', 10),
        (N'Snacks and Beverages', N'Glucose Biscuits', N'Classic glucose biscuits', N'Parle-G', 20.00, 0, 100, N'250 g', N'images/products/biscuits.png', 20),
        (N'Snacks and Beverages', N'Potato Chips', N'Classic salted potato chips', N'Lay''s', 20.00, 0, 75, N'52 g', N'images/products/chips.png', 15),
        (N'Snacks and Beverages', N'Orange Juice', N'Ready-to-serve orange juice', N'Real', 110.00, 10, 40, N'1 litre', N'images/products/orange-juice.png', 8),
        (N'Snacks and Beverages', N'Tea', N'Premium black tea', N'Tata Tea', 260.00, 5, 32, N'500 g', N'images/products/tea.png', 7),
        (N'Household Essentials', N'Dishwashing Liquid', N'Lemon dishwashing liquid', N'Vim', 115.00, 0, 28, N'500 ml', N'images/products/dishwash.png', 6),
        (N'Household Essentials', N'Detergent Powder', N'Washing detergent powder', N'Surf Excel', 145.00, 0, 34, N'1 kg', N'images/products/detergent.png', 7),
        (N'Household Essentials', N'Floor Cleaner', N'Disinfectant floor cleaner', N'Lizol', 210.00, 5, 24, N'1 litre', N'images/products/floor-cleaner.png', 5),
        (N'Household Essentials', N'Bath Soap Pack', N'Bathing soap multipack', N'Dove', 190.00, 0, 30, N'4 pieces', N'images/products/soap-pack.png', 6);

    INSERT dbo.Products
        (CategoryId, ProductName, Description, Brand, Price, Discount, Stock,
         Unit, ProductImage, IsAvailable, CreatedDate, ReorderLevel)
    SELECT c.CategoryId, s.ProductName, s.Description, s.Brand, s.Price,
           s.Discount, s.Stock, s.Unit, s.ProductImage, 1, GETDATE(), s.ReorderLevel
      FROM @ProductsSeed AS s
      JOIN dbo.Categories AS c ON c.CategoryName = s.CategoryName
     WHERE NOT EXISTS (
        SELECT 1 FROM dbo.Products AS p
         WHERE p.ProductName = s.ProductName
     );

    /* ------------------------------------------------------------------
       8. Delivery partners and offers
       ------------------------------------------------------------------ */

    IF NOT EXISTS (SELECT 1 FROM dbo.DeliveryPartners WHERE Mobile = '9000000001')
        INSERT dbo.DeliveryPartners (PartnerName, Mobile, VehicleNumber, IsAvailable)
        VALUES (N'Ravi Kumar', '9000000001', N'MH12QK1001', 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.DeliveryPartners WHERE Mobile = '9000000002')
        INSERT dbo.DeliveryPartners (PartnerName, Mobile, VehicleNumber, IsAvailable)
        VALUES (N'Neha Singh', '9000000002', N'MH12QK1002', 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Offers WHERE OfferTitle = N'Fresh Produce Weekend')
        INSERT dbo.Offers
            (OfferTitle, Description, DiscountPercent, StartDate, EndDate, BannerImage, IsActive)
        VALUES
            (N'Fresh Produce Weekend', N'Extra discount on selected fruits and vegetables.', 10, '2026-07-01', '2026-12-31', N'images/offers/fresh-produce.png', 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Offers WHERE OfferTitle = N'Dairy Saver')
        INSERT dbo.Offers
            (OfferTitle, Description, DiscountPercent, StartDate, EndDate, BannerImage, IsActive)
        VALUES
            (N'Dairy Saver', N'Extra discount on selected dairy products.', 5, '2026-07-01', '2026-12-31', N'images/offers/dairy-saver.png', 1);

    INSERT dbo.ProductOffers (ProductId, OfferId)
    SELECT p.ProductId, o.OfferId
      FROM dbo.Products AS p
      CROSS JOIN dbo.Offers AS o
     WHERE p.ProductName IN (N'Fresh Apples', N'Bananas')
       AND o.OfferTitle = N'Fresh Produce Weekend'
       AND NOT EXISTS (
           SELECT 1 FROM dbo.ProductOffers AS po
            WHERE po.ProductId = p.ProductId AND po.OfferId = o.OfferId
       );

    INSERT dbo.ProductOffers (ProductId, OfferId)
    SELECT p.ProductId, o.OfferId
      FROM dbo.Products AS p
      CROSS JOIN dbo.Offers AS o
     WHERE p.ProductName IN (N'Toned Milk', N'Fresh Curd')
       AND o.OfferTitle = N'Dairy Saver'
       AND NOT EXISTS (
           SELECT 1 FROM dbo.ProductOffers AS po
            WHERE po.ProductId = p.ProductId AND po.OfferId = o.OfferId
       );

    /* ------------------------------------------------------------------
       9. Demo customers, addresses and carts
       ------------------------------------------------------------------ */

    DECLARE @CustomerPasswordHash nvarchar(255) =
        N'PBKDF2-SHA256$100000$CwdH7uXPumRSGCz9Omm+XQ==$cOY4R/lgA9I62cGNlzTf7xPDE9kGED4l1+N1rz1oNOo=';

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'arjun.demo@quickkart.local')
        INSERT dbo.Users
            (FullName, Email, PasswordHash, PhoneNumber, Gender, DateOfBirth, ProfileImage, IsActive)
        VALUES
            (N'Arjun Demo', N'arjun.demo@quickkart.local', @CustomerPasswordHash,
             '9000000011', N'Male', '2001-05-15', NULL, 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'priya.demo@quickkart.local')
        INSERT dbo.Users
            (FullName, Email, PasswordHash, PhoneNumber, Gender, DateOfBirth, ProfileImage, IsActive)
        VALUES
            (N'Priya Demo', N'priya.demo@quickkart.local', @CustomerPasswordHash,
             '9000000012', N'Female', '2002-08-21', NULL, 1);

    DECLARE @ArjunUserId int =
        (SELECT UserId FROM dbo.Users WHERE Email = N'arjun.demo@quickkart.local');
    DECLARE @PriyaUserId int =
        (SELECT UserId FROM dbo.Users WHERE Email = N'priya.demo@quickkart.local');

    IF NOT EXISTS (SELECT 1 FROM dbo.Address WHERE UserId = @ArjunUserId)
        INSERT dbo.Address
            (UserId, HouseNo, Street, City, State, Pincode, Landmark)
        VALUES
            (@ArjunUserId, N'12', N'Green Park, MG Road', N'Pune', N'Maharashtra', '411001', N'Near City Library');

    IF NOT EXISTS (SELECT 1 FROM dbo.Address WHERE UserId = @PriyaUserId)
        INSERT dbo.Address
            (UserId, HouseNo, Street, City, State, Pincode, Landmark)
        VALUES
            (@PriyaUserId, N'44B', N'Lake View Colony', N'Pune', N'Maharashtra', '411038', N'Opposite Community Hall');

    IF NOT EXISTS (SELECT 1 FROM dbo.Cart WHERE UserId = @ArjunUserId)
        INSERT dbo.Cart (UserId) VALUES (@ArjunUserId);

    IF NOT EXISTS (SELECT 1 FROM dbo.Cart WHERE UserId = @PriyaUserId)
        INSERT dbo.Cart (UserId) VALUES (@PriyaUserId);

    DECLARE @ArjunCartId int = (SELECT CartId FROM dbo.Cart WHERE UserId = @ArjunUserId);
    DECLARE @OrangeJuiceId int = (SELECT ProductId FROM dbo.Products WHERE ProductName = N'Orange Juice');

    IF @ArjunCartId IS NOT NULL AND @OrangeJuiceId IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM dbo.CartItems
            WHERE CartId = @ArjunCartId AND ProductId = @OrangeJuiceId
       )
        INSERT dbo.CartItems (CartId, ProductId, Quantity, Price)
        SELECT @ArjunCartId, ProductId, 1, Price
          FROM dbo.Products
         WHERE ProductId = @OrangeJuiceId;

    /* ------------------------------------------------------------------
       10. Demo orders, payments, delivery and inventory history
       ------------------------------------------------------------------ */

    DECLARE @ArjunAddressId int =
        (SELECT TOP (1) AddressId FROM dbo.Address WHERE UserId = @ArjunUserId ORDER BY AddressId);
    DECLARE @PriyaAddressId int =
        (SELECT TOP (1) AddressId FROM dbo.Address WHERE UserId = @PriyaUserId ORDER BY AddressId);
    DECLARE @BasmatiId int =
        (SELECT ProductId FROM dbo.Products WHERE ProductName = N'Basmati Rice');
    DECLARE @MilkId int =
        (SELECT ProductId FROM dbo.Products WHERE ProductName = N'Toned Milk');
    DECLARE @AppleId int =
        (SELECT ProductId FROM dbo.Products WHERE ProductName = N'Fresh Apples');

    IF NOT EXISTS (
        SELECT 1 FROM dbo.Orders
         WHERE UserId = @ArjunUserId AND OrderDate = '2026-07-20T10:00:00'
    )
    BEGIN
        INSERT dbo.Orders
            (UserId, AddressId, OrderDate, TotalAmount, OrderStatus,
             PaymentStatus, PaymentMethod, DeliveryAddressSnapshot)
        VALUES
            (@ArjunUserId, @ArjunAddressId, '2026-07-20T10:00:00', 410.00,
             N'Delivered', N'Paid', N'UPI',
             N'12, Green Park, MG Road, Pune, Maharashtra - 411001');

        DECLARE @DeliveredOrderId int = CONVERT(int, SCOPE_IDENTITY());

        INSERT dbo.OrderItems
            (OrderId, ProductId, Quantity, Price, ProductName)
        VALUES
            (@DeliveredOrderId, @BasmatiId, 1, 350.00, N'Basmati Rice'),
            (@DeliveredOrderId, @MilkId, 2, 30.00, N'Toned Milk');

        INSERT dbo.Payments
            (OrderId, PaymentMethod, TransactionId, Amount, PaymentDate, PaymentStatus)
        VALUES
            (@DeliveredOrderId, N'UPI', N'QKDEMO001', 410.00, '2026-07-20T10:05:00', N'Paid');

        INSERT dbo.Delivery
            (OrderId, DeliveryPartnerId, DeliveryDate, DeliveryStatus)
        SELECT TOP (1)
            @DeliveredOrderId, DeliveryPartnerId, '2026-07-20T18:30:00', N'Delivered'
          FROM dbo.DeliveryPartners
         ORDER BY DeliveryPartnerId;

        INSERT dbo.InventoryTransactions
            (ProductId, TransactionType, QuantityChange, ReferenceId, Remarks)
        VALUES
            (@BasmatiId, N'Sale', -1, @DeliveredOrderId, N'Demo delivered order'),
            (@MilkId, N'Sale', -2, @DeliveredOrderId, N'Demo delivered order');
    END;

    IF NOT EXISTS (
        SELECT 1 FROM dbo.Orders
         WHERE UserId = @PriyaUserId AND OrderDate = '2026-07-25T16:15:00'
    )
    BEGIN
        INSERT dbo.Orders
            (UserId, AddressId, OrderDate, TotalAmount, OrderStatus,
             PaymentStatus, PaymentMethod, DeliveryAddressSnapshot)
        VALUES
            (@PriyaUserId, @PriyaAddressId, '2026-07-25T16:15:00', 320.00,
             N'Pending', N'Pending', N'Cash on Delivery',
             N'44B, Lake View Colony, Pune, Maharashtra - 411038');

        DECLARE @PendingOrderId int = CONVERT(int, SCOPE_IDENTITY());

        INSERT dbo.OrderItems
            (OrderId, ProductId, Quantity, Price, ProductName)
        VALUES
            (@PendingOrderId, @AppleId, 2, 160.00, N'Fresh Apples');

        INSERT dbo.Payments
            (OrderId, PaymentMethod, TransactionId, Amount, PaymentDate, PaymentStatus)
        VALUES
            (@PendingOrderId, N'Cash on Delivery', NULL, 320.00, '2026-07-25T16:15:00', N'Pending');

        INSERT dbo.InventoryTransactions
            (ProductId, TransactionType, QuantityChange, ReferenceId, Remarks)
        VALUES
            (@AppleId, N'Sale', -2, @PendingOrderId, N'Demo pending order');
    END;

    COMMIT TRANSACTION;

    PRINT 'QuickKartDB enhancement migration completed successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO
