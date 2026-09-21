-- DIRTY READ: reproduced under READ UNCOMMITTED, prevented by READ COMMITTED

-- Session A
BEGIN TRAN;
UPDATE dbo.Orders SET Amount = 999.99 WHERE OrderId = 1;
WAITFOR DELAY '00:00:06';
ROLLBACK;

-- Session B (reproduces the anomaly)
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SELECT OrderId, Amount FROM dbo.Orders WHERE OrderId = 1;
-- returns 999.99 while Session A is uncommitted, even though A later rolls back

-- Session B (prevented)
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
SELECT OrderId, Amount FROM dbo.Orders WHERE OrderId = 1;
-- blocks until Session A finishes, then returns the real value 10.99


-- NON-REPEATABLE READ: reproduced under READ COMMITTED, prevented by REPEATABLE READ

-- Session A (reproduces the anomaly)
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
BEGIN TRAN;
SELECT OrderId, Amount FROM dbo.Orders WHERE OrderId = 2;   -- first read: 11.99
WAITFOR DELAY '00:00:04';
SELECT OrderId, Amount FROM dbo.Orders WHERE OrderId = 2;   -- second read: 555.55
COMMIT;

-- Session B
UPDATE dbo.Orders SET Amount = 555.55 WHERE OrderId = 2;

-- Session A (prevented)
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
BEGIN TRAN;
SELECT OrderId, Amount FROM dbo.Orders WHERE OrderId = 2;   -- first read: 11.99
WAITFOR DELAY '00:00:04';
SELECT OrderId, Amount FROM dbo.Orders WHERE OrderId = 2;   -- second read: still 11.99
COMMIT;
-- Session B's UPDATE blocks until Session A commits


-- PHANTOM READ: reproduced under REPEATABLE READ, prevented by SERIALIZABLE
-- (uses a selective predicate - CustomerEmail, ~20 matching rows - so row locks
-- don't escalate to a table lock and mask the result)

-- Session A (reproduces the anomaly)
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
BEGIN TRAN;
SELECT COUNT(*) FROM dbo.Orders WHERE CustomerEmail = 'customer3333@example.com';  -- 20
WAITFOR DELAY '00:00:05';
SELECT COUNT(*) FROM dbo.Orders WHERE CustomerEmail = 'customer3333@example.com';  -- 21
COMMIT;

-- Session B
INSERT INTO dbo.Orders (CustomerEmail, OrderStatus, OrderDate, Amount, Notes)
VALUES ('customer3333@example.com', 'Shipped', SYSDATETIME(), 20.00, 'phantom test row');
-- succeeds immediately, not blocked

-- Session A (prevented)
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRAN;
SELECT COUNT(*) FROM dbo.Orders WHERE CustomerEmail = 'customer3333@example.com';  -- 20
WAITFOR DELAY '00:00:05';
SELECT COUNT(*) FROM dbo.Orders WHERE CustomerEmail = 'customer3333@example.com';  -- still 20
COMMIT;
-- Session B's INSERT blocks (key-range lock) until Session A commits