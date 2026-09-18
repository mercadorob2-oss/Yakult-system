-- Replace the narrow IX_CartridgeAuthorization_EmployeeId index with a covering
-- index that includes CreatedDate as a key column (eliminates the post-seek sort)
-- and includes all columns selected by GetHistoryByEmployeeAsync (eliminates key
-- lookups).  The old index is a strict subset of the new one and can be dropped.

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.CartridgeAuthorization')
      AND name = 'IX_CartridgeAuthorization_EmployeeId'
)
BEGIN
    DROP INDEX [IX_CartridgeAuthorization_EmployeeId]
        ON [dbo].[CartridgeAuthorization];
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.CartridgeAuthorization')
      AND name = 'IX_CartridgeAuthorization_EmployeeId_CreatedDate'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CartridgeAuthorization_EmployeeId_CreatedDate]
        ON [dbo].[CartridgeAuthorization] ([EmployeeId] ASC, [CreatedDate] DESC)
        INCLUDE (
            [AuthorizationId],
            [DepartmentId],
            [Status],
            [SignedBySupervisorId],
            [SignedDate],
            [RequestedModels],
            [SubmissionSessionId],
            [Source]
        );
END
