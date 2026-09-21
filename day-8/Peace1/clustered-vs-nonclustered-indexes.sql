CREATE CLUSTERED INDEX CIX_Orders_OrderId ON dbo.Orders (OrderId);

CREATE NONCLUSTERED INDEX IX_Orders_OrderStatus ON dbo.Orders (OrderStatus);

CREATE NONCLUSTERED INDEX IX_Orders_CustomerEmail ON dbo.Orders (CustomerEmail);

SET STATISTICS IO ON;

SELECT OrderId, CustomerEmail, OrderStatus, OrderDate, Amount
FROM dbo.Orders
WHERE OrderId = 55555;

SELECT COUNT(*), AVG(Amount)
FROM dbo.Orders
WHERE OrderStatus = 'Cancelled';

SELECT OrderId, OrderDate, Amount
FROM dbo.Orders
WHERE CustomerEmail = 'customer2500@example.com';

INSERT INTO dbo.Orders (CustomerEmail, OrderStatus, OrderDate, Amount, Notes)
VALUES ('customer9999@example.com', 'Pending', SYSDATETIME(), 42.50, 'write-cost test row');

SET STATISTICS IO OFF;
