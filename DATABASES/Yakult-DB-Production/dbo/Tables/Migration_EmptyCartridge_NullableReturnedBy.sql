-- ============================================================
-- Migration: EmptyCartridge — make ReturnedBy nullable
-- Date:      2026-07-01
-- Reason:    Dept-level cartridge requests have no specific
--            employee. ReturnedBy previously stored an EmpId
--            and was NOT NULL, causing an INSERT failure when
--            fulfilling dept-level requests (empId = 0).
--            Making it nullable mirrors EmpId which is already
--            nullable on this table.
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[EmptyCartridge]') AND name = 'ReturnedBy' AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.[EmptyCartridge]
        ALTER COLUMN [ReturnedBy] INT NULL;
END
GO
