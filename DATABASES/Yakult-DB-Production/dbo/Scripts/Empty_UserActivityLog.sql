-- ============================================================
-- EMPTY: dbo.UserActivityLog
-- Clears all rows and reseeds the IDENTITY to 0 so the next
-- real insert gets ActivityId = 1.
--
-- TRUNCATE is used because:
--   • No foreign keys reference this table.
--   • It reseeds IDENTITY automatically.
--   • It is minimally logged (faster, less transaction log).
--
-- Fallback (commented out) uses DELETE + manual reseed in case
-- a FK constraint is added in future.
-- ============================================================

-- Primary: TRUNCATE (reseeds IDENTITY automatically)
TRUNCATE TABLE dbo.UserActivityLog;

-- Fallback (uncomment if TRUNCATE is blocked by an FK):
-- DELETE FROM dbo.UserActivityLog;
-- DBCC CHECKIDENT ('dbo.UserActivityLog', RESEED, 0);

PRINT 'dbo.UserActivityLog emptied. Next ActivityId = 1.';
