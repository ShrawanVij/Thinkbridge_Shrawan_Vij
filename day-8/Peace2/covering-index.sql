CREATE NONCLUSTERED INDEX IX_Orders_CustomerEmail_Covering
    ON dbo.Orders (CustomerEmail)
    INCLUDE (OrderDate, Amount);

SET STATISTICS IO ON;

SELECT OrderId, OrderDate, Amount
FROM dbo.Orders
WHERE CustomerEmail = 'customer2500@example.com';

SET STATISTICS IO OFF;