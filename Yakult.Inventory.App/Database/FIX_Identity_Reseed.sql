-- Fix the IDENTITY column on Item table
-- The IDENTITY seed is out of sync with existing data

DECLARE @MaxItemId INT

-- Get the maximum ItemId currently in the table
SELECT @MaxItemId = ISNULL(MAX(ItemId), 0) FROM dbo.Item

PRINT 'Current maximum ItemId: ' + CAST(@MaxItemId AS VARCHAR)

-- Reseed the IDENTITY to start after the maximum existing ID
DBCC CHECKIDENT ('dbo.Item', RESEED, @MaxItemId)

PRINT 'IDENTITY reseeded to: ' + CAST(@MaxItemId AS VARCHAR)
PRINT 'Next ItemId will be: ' + CAST(@MaxItemId + 1 AS VARCHAR)
PRINT ''
PRINT 'FIX COMPLETE - Try adding items now!'
