-- REPRO: two sessions lock the same two rows in opposite order

-- Session A
BEGIN TRAN;
UPDATE dbo.Orders SET Amount = Amount + 1 WHERE OrderId = 100;
WAITFOR DELAY '00:00:03';
UPDATE dbo.Orders SET Amount = Amount + 1 WHERE OrderId = 200;
COMMIT;

-- Session B
BEGIN TRAN;
UPDATE dbo.Orders SET Amount = Amount + 1 WHERE OrderId = 200;
WAITFOR DELAY '00:00:03';
UPDATE dbo.Orders SET Amount = Amount + 1 WHERE OrderId = 100;
COMMIT;

-- Session A's error:
-- Msg 1205, Level 13, State 51
-- Transaction (Process ID 55) was deadlocked on lock resources with another
-- process and has been chosen as the deadlock victim. Rerun the transaction.


-- FIX: both sessions lock rows in the same order (lowest OrderId first)

-- Session A (fixed)
BEGIN TRAN;
UPDATE dbo.Orders SET Amount = Amount + 1 WHERE OrderId = 100;
WAITFOR DELAY '00:00:03';
UPDATE dbo.Orders SET Amount = Amount + 1 WHERE OrderId = 200;
COMMIT;

-- Session B (fixed)
BEGIN TRAN;
UPDATE dbo.Orders SET Amount = Amount + 1 WHERE OrderId = 100;
WAITFOR DELAY '00:00:03';
UPDATE dbo.Orders SET Amount = Amount + 1 WHERE OrderId = 200;
COMMIT;

-- result: both sessions commit successfully, no deadlock, no victim
