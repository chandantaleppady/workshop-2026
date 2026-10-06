CREATE TABLE dbo.Orders
(
    OrderDetailId bigint IDENTITY(1,1) NOT NULL
        CONSTRAINT PK_Orders PRIMARY KEY,
    OrderId uniqueidentifier NOT NULL,
    OrderDate datetime2(0) NOT NULL
        CONSTRAINT DF_Orders_OrderDate DEFAULT SYSUTCDATETIME(),
    ProductId int NOT NULL,
    ProductName nvarchar(100) NOT NULL,
    UnitPrice decimal(18,2) NOT NULL,
    Quantity int NOT NULL,
    LineTotal AS (UnitPrice * Quantity) PERSISTED
);

CREATE INDEX IX_Orders_OrderId ON dbo.Orders (OrderId);