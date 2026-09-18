-- Migration: Add indexes to support GetAllSetsAsync / GetCartridgeSetsAsync performance
--
-- GetAllSetsAsync used 10 correlated subqueries, each scanning dbo.Request filtered by
-- SetId and ordered by ReqId. After the OUTER APPLY rewrite, SQL Server needs an efficient
-- seek on (SetId, ReqId) to satisfy the TOP 1 ... ORDER BY ReqId.
--
-- Also adds a covering index on ArchiveStatus(EntityType, EntityId) which is joined twice
-- per row in every Set query and was previously doing a full scan.

-- Index 1: Support OUTER APPLY TOP 1 lookup (SetId seek + ReqId order)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Request')
      AND name = 'IX_Request_SetId_ReqId'
)
BEGIN
    CREATE INDEX IX_Request_SetId_ReqId
        ON dbo.Request (SetId, ReqId)
        INCLUDE (EmpId);
    PRINT 'Index IX_Request_SetId_ReqId created on dbo.Request';
END
ELSE
    PRINT 'Index IX_Request_SetId_ReqId already exists — skipped';

-- Index 2: Support ArchiveStatus lookups (EntityType + EntityId seek)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.ArchiveStatus')
      AND name = 'IX_ArchiveStatus_EntityType_EntityId'
)
BEGIN
    CREATE INDEX IX_ArchiveStatus_EntityType_EntityId
        ON dbo.ArchiveStatus (EntityType, EntityId);
    PRINT 'Index IX_ArchiveStatus_EntityType_EntityId created on dbo.ArchiveStatus';
END
ELSE
    PRINT 'Index IX_ArchiveStatus_EntityType_EntityId already exists — skipped';
