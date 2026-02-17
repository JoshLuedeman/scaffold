-- Sample database schema for integration tests
-- Creates a realistic small database with various SQL Server features
-- that the assessment engine can analyze.
-- Script is idempotent: safe to run multiple times.

-- Tables
IF OBJECT_ID('dbo.OrderItems', 'U') IS NOT NULL DROP TABLE dbo.OrderItems;
IF OBJECT_ID('dbo.Orders', 'U') IS NOT NULL DROP TABLE dbo.Orders;
IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL DROP TABLE dbo.Products;
IF OBJECT_ID('dbo.AuditLog', 'U') IS NOT NULL DROP TABLE dbo.AuditLog;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
IF OBJECT_ID('dbo.vw_OrderSummary', 'V') IS NOT NULL DROP VIEW dbo.vw_OrderSummary;
IF OBJECT_ID('dbo.vw_ProductSales', 'V') IS NOT NULL DROP VIEW dbo.vw_ProductSales;
IF OBJECT_ID('dbo.sp_GetUserOrders', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_GetUserOrders;
IF OBJECT_ID('dbo.sp_CreateOrder', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_CreateOrder;
IF OBJECT_ID('dbo.sp_UpdateOrderStatus', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_UpdateOrderStatus;
IF OBJECT_ID('dbo.fn_GetOrderTotal', 'FN') IS NOT NULL DROP FUNCTION dbo.fn_GetOrderTotal;

CREATE TABLE dbo.Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL,
    Email NVARCHAR(255) NOT NULL,
    PasswordHash NVARCHAR(500) NOT NULL,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    IsActive BIT DEFAULT 1
);

CREATE TABLE dbo.Products (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX),
    Price DECIMAL(18,2) NOT NULL,
    Category NVARCHAR(100),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);

CREATE TABLE dbo.Orders (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    OrderDate DATETIME2 DEFAULT GETUTCDATE(),
    TotalAmount DECIMAL(18,2) NOT NULL,
    Status NVARCHAR(50) DEFAULT 'Pending',
    CONSTRAINT FK_Orders_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
);

CREATE TABLE dbo.OrderItems (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    OrderId INT NOT NULL,
    ProductId INT NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id),
    CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id)
);

CREATE TABLE dbo.AuditLog (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    TableName NVARCHAR(128) NOT NULL,
    Action NVARCHAR(10) NOT NULL,
    RecordId INT,
    ChangedBy NVARCHAR(100),
    ChangedAt DATETIME2 DEFAULT GETUTCDATE(),
    OldValues NVARCHAR(MAX),
    NewValues NVARCHAR(MAX)
);

-- Indexes
CREATE UNIQUE INDEX IX_Users_Email ON dbo.Users(Email);
CREATE INDEX IX_Users_Username ON dbo.Users(Username);
CREATE INDEX IX_Orders_UserId ON dbo.Orders(UserId);
CREATE INDEX IX_Orders_Status ON dbo.Orders(Status);
CREATE INDEX IX_OrderItems_OrderId ON dbo.OrderItems(OrderId);
CREATE INDEX IX_OrderItems_ProductId ON dbo.OrderItems(ProductId);
CREATE INDEX IX_Products_Category ON dbo.Products(Category);

-- Views
GO
CREATE VIEW dbo.vw_OrderSummary AS
SELECT
    o.Id AS OrderId,
    u.Username,
    u.Email,
    o.OrderDate,
    o.TotalAmount,
    o.Status,
    COUNT(oi.Id) AS ItemCount
FROM dbo.Orders o
JOIN dbo.Users u ON o.UserId = u.Id
LEFT JOIN dbo.OrderItems oi ON o.Id = oi.OrderId
GROUP BY o.Id, u.Username, u.Email, o.OrderDate, o.TotalAmount, o.Status;
GO

CREATE VIEW dbo.vw_ProductSales AS
SELECT
    p.Name AS ProductName,
    p.Category,
    SUM(oi.Quantity) AS TotalQuantitySold,
    SUM(oi.Quantity * oi.UnitPrice) AS TotalRevenue
FROM dbo.Products p
LEFT JOIN dbo.OrderItems oi ON p.Id = oi.ProductId
GROUP BY p.Name, p.Category;
GO

-- Stored Procedures
CREATE PROCEDURE dbo.sp_GetUserOrders
    @UserId INT
AS
BEGIN
    SELECT o.*, u.Username
    FROM dbo.Orders o
    JOIN dbo.Users u ON o.UserId = u.Id
    WHERE o.UserId = @UserId
    ORDER BY o.OrderDate DESC;
END;
GO

CREATE PROCEDURE dbo.sp_CreateOrder
    @UserId INT,
    @TotalAmount DECIMAL(18,2),
    @OrderId INT OUTPUT
AS
BEGIN
    INSERT INTO dbo.Orders (UserId, TotalAmount)
    VALUES (@UserId, @TotalAmount);

    SET @OrderId = SCOPE_IDENTITY();
END;
GO

CREATE PROCEDURE dbo.sp_UpdateOrderStatus
    @OrderId INT,
    @Status NVARCHAR(50)
AS
BEGIN
    UPDATE dbo.Orders SET Status = @Status WHERE Id = @OrderId;
END;
GO

-- Functions
CREATE FUNCTION dbo.fn_GetOrderTotal(@OrderId INT)
RETURNS DECIMAL(18,2)
AS
BEGIN
    DECLARE @Total DECIMAL(18,2);
    SELECT @Total = SUM(Quantity * UnitPrice) FROM dbo.OrderItems WHERE OrderId = @OrderId;
    RETURN ISNULL(@Total, 0);
END;
GO

-- Trigger
CREATE TRIGGER dbo.trg_AuditOrders ON dbo.Orders
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.AuditLog (TableName, Action, RecordId, ChangedAt)
    SELECT 'Orders',
           CASE WHEN EXISTS (SELECT 1 FROM deleted) THEN 'UPDATE' ELSE 'INSERT' END,
           i.Id,
           GETUTCDATE()
    FROM inserted i;
END;
GO

-- Seed data
INSERT INTO dbo.Users (Username, Email, PasswordHash) VALUES
    ('alice', 'alice@example.com', 'hash1'),
    ('bob', 'bob@example.com', 'hash2'),
    ('charlie', 'charlie@example.com', 'hash3');

INSERT INTO dbo.Products (Name, Description, Price, Category) VALUES
    ('Widget A', 'A standard widget', 9.99, 'Widgets'),
    ('Widget B', 'A premium widget', 19.99, 'Widgets'),
    ('Gadget X', 'An advanced gadget', 49.99, 'Gadgets'),
    ('Gadget Y', 'A basic gadget', 29.99, 'Gadgets');

-- Disable trigger temporarily to avoid audit entries during seed
DISABLE TRIGGER dbo.trg_AuditOrders ON dbo.Orders;

INSERT INTO dbo.Orders (UserId, TotalAmount, Status) VALUES
    (1, 29.98, 'Completed'),
    (1, 49.99, 'Shipped'),
    (2, 9.99, 'Pending'),
    (3, 79.98, 'Completed');

INSERT INTO dbo.OrderItems (OrderId, ProductId, Quantity, UnitPrice) VALUES
    (1, 1, 2, 9.99),
    (1, 2, 1, 19.99),
    (2, 3, 1, 49.99),
    (3, 1, 1, 9.99),
    (4, 3, 1, 49.99),
    (4, 4, 1, 29.99);

ENABLE TRIGGER dbo.trg_AuditOrders ON dbo.Orders;
