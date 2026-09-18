-- Migration: CartridgeAuthorization — Add Remarks column for IT Manual Authorization
-- Purpose : IT-assisted requests now require manual authorization recording by the IT user.
--           The Remarks field stores optional (Approved) or required (Rejected) notes.
--           SignatureSource = 'IT_MANUAL' identifies these records.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[CartridgeAuthorization]') AND name = 'Remarks'
)
BEGIN
    ALTER TABLE dbo.[CartridgeAuthorization]
        ADD [Remarks] NVARCHAR(500) NULL;
    PRINT 'Added column: CartridgeAuthorization.Remarks';
END
ELSE
BEGIN
    PRINT 'Column already exists: CartridgeAuthorization.Remarks';
END
