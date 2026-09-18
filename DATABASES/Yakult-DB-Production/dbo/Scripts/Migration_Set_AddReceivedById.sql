-- Migration: Add ReceivedById to dbo.[Set]
--
-- Tracks who physically collected the items in a dispatched set.
-- Managed via ViewSetDetailPage (Received By combo) and
-- SetDispatchNotificationPage (can be overridden before sending email).
--
-- Backward compatible: NULL for all existing sets.

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[Set]')
      AND name = 'ReceivedById'
)
BEGIN
    ALTER TABLE dbo.[Set]
        ADD ReceivedById INT NULL
        CONSTRAINT FK_Set_ReceivedBy FOREIGN KEY (ReceivedById)
            REFERENCES dbo.Employee(EmpId);

    PRINT 'Column ReceivedById added to dbo.[Set]';
END
ELSE
BEGIN
    PRINT 'Column ReceivedById already exists on dbo.[Set] — skipped.';
END
