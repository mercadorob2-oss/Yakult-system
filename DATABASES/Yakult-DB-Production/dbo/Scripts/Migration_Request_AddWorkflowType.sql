-- Migration_Request_AddWorkflowType.sql
--
-- Adds an explicit workflow-ownership column to dbo.Request, replacing the previous approach
-- of inferring ownership (Cartridge Management vs Request & Set Management) from the
-- existence of a related dbo.CartridgeRequestModel row or from the resolved Item's category.
--
-- WorkflowType is nullable: requests unrelated to either workflow (e.g. Cable, Projector,
-- Furniture) are left NULL rather than force-tagged, since they don't participate in this
-- two-workflow split at all. See Backfill_Request_WorkflowType.sql for populating existing rows.
--
-- Safe to re-run.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Request') AND name = 'WorkflowType'
)
BEGIN
    ALTER TABLE dbo.Request ADD WorkflowType VARCHAR(30) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Request_WorkflowType'
)
BEGIN
    ALTER TABLE dbo.Request
    ADD CONSTRAINT CK_Request_WorkflowType
    CHECK (WorkflowType IS NULL OR WorkflowType IN ('CartridgeManagement', 'RequestSetManagement'));
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Request_WorkflowType' AND object_id = OBJECT_ID('dbo.Request')
)
BEGIN
    CREATE INDEX IX_Request_WorkflowType ON dbo.Request(WorkflowType) WHERE WorkflowType IS NOT NULL;
END
GO
