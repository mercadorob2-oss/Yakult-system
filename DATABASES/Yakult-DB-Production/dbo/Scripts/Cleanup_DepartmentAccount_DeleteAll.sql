-- ============================================================
-- Cleanup: Delete all rows from dbo.DepartmentAccount
-- Purpose: Wipe auto-created accounts so the seeding script
--          can re-insert them with the corrected username format.
-- !! Run this BEFORE re-running the seeding script. !!
-- ============================================================

DELETE FROM dbo.DepartmentAccount;
GO

PRINT 'All rows deleted from dbo.DepartmentAccount.';

-- Confirm
SELECT COUNT(*) AS RemainingRows FROM dbo.DepartmentAccount;
