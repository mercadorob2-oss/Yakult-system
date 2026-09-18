-- Migration: Add ReceivedById to dbo.Request
-- Purpose : Stores who is designated to physically pick up cartridges for PICKUP fulfillment requests.
--           Populated at portal submission time; editable by IT staff on the Cartridge Management page
--           before the fulfillment notification email is sent.
-- Note    : NULL is valid for DELIVERY requests and legacy records.

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Request')
      AND name = N'ReceivedById'
)
BEGIN
    ALTER TABLE dbo.Request
        ADD [ReceivedById] INT NULL
        CONSTRAINT [FK_Request_ReceivedBy] FOREIGN KEY REFERENCES dbo.Employee(EmpId);

    PRINT 'Column ReceivedById added to dbo.Request.';
END
ELSE
BEGIN
    PRINT 'Column ReceivedById already exists on dbo.Request. Skipping.';
END
GO
