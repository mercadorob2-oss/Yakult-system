-- ============================================================
-- Migration: Covering indexes for ViewArchivePage and
--            UserAccountManagementPage query performance
-- ============================================================
-- PURPOSE
--   After the company/branch/department hierarchy normalization
--   (BranchDepartmentCompany junction table), SQL Server
--   invalidated cached execution plans for many queries.
--   Several pages began timing out at the default 30-second
--   command timeout because their queries were now compiled
--   with sub-optimal plans.
--
--   This script adds covering indexes that give the optimizer
--   efficient access paths for the two most-affected pages:
--
--   1. ViewArchivePage
--      Query: SELECT from ArchiveStatus + multi-table LEFT JOINs
--      Missing: index on ArchiveStatus(IsArchived) covering the
--               columns needed for the base scan + filter.
--
--   2. UserAccountManagementPage
--      Query: SELECT from [User] JOIN Employee +
--             correlated STUFF/FOR XML PATH subquery over UserRole
--      Missing: covering index on UserRole(UserId) so the
--               correlated subquery becomes a fast seek per user
--               instead of a full scan or bookmark lookup.
--
-- SAFE TO RE-RUN   — guarded by IF NOT EXISTS
-- ============================================================

SET NOCOUNT ON;

-- ── 1. ArchiveStatus — base scan for the archive page ────────
--   Predicate : WHERE a.IsArchived = 1
--   Ordered by: ORDER BY a.ArchivedAt DESC
--   Fetched   : EntityType, EntityId, ArchivedAt, ArchivedBy, ArchiveReason
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  object_id = OBJECT_ID(N'dbo.ArchiveStatus')
      AND  name      = N'IX_ArchiveStatus_IsArchived_ArchivedAt'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ArchiveStatus_IsArchived_ArchivedAt]
        ON [dbo].[ArchiveStatus] ([IsArchived] ASC, [ArchivedAt] DESC)
        INCLUDE ([EntityType], [EntityId], [ArchivedBy], [ArchiveReason]);

    PRINT 'Created IX_ArchiveStatus_IsArchived_ArchivedAt.';
END
ELSE
    PRINT 'IX_ArchiveStatus_IsArchived_ArchivedAt already exists — skipped.';

-- ── 2. ArchiveStatus — EntityType filter (used in combo box) ──
--   When the user picks a specific EntityType (e.g. "Branch"),
--   the query adds AND a.EntityType = @EntityType. This index
--   makes that filter a seek rather than a partial scan.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  object_id = OBJECT_ID(N'dbo.ArchiveStatus')
      AND  name      = N'IX_ArchiveStatus_EntityType_IsArchived'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ArchiveStatus_EntityType_IsArchived]
        ON [dbo].[ArchiveStatus] ([EntityType] ASC, [IsArchived] ASC)
        INCLUDE ([EntityId], [ArchivedAt], [ArchivedBy], [ArchiveReason]);

    PRINT 'Created IX_ArchiveStatus_EntityType_IsArchived.';
END
ELSE
    PRINT 'IX_ArchiveStatus_EntityType_IsArchived already exists — skipped.';

-- ── 3. UserRole — correlated role lookup per user ─────────────
--   The UserAccountManagementPage runs a STUFF/FOR XML PATH
--   correlated subquery: WHERE ur.UserId = u.UserId AND r.IsActive = 1
--   Without this index, SQL Server does a scan of UserRole per user.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  object_id = OBJECT_ID(N'dbo.UserRole')
      AND  name      = N'IX_UserRole_UserId_RoleId'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_UserRole_UserId_RoleId]
        ON [dbo].[UserRole] ([UserId] ASC)
        INCLUDE ([RoleId]);

    PRINT 'Created IX_UserRole_UserId_RoleId.';
END
ELSE
    PRINT 'IX_UserRole_UserId_RoleId already exists — skipped.';

-- ── 4. Role — IsActive filter used in the correlated subquery ─
--   WHERE r.IsActive = 1 JOIN on RoleId — covering RoleName
--   avoids a key lookup into the clustered index for each match.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  object_id = OBJECT_ID(N'dbo.Role')
      AND  name      = N'IX_Role_IsActive_RoleId'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Role_IsActive_RoleId]
        ON [dbo].[Role] ([IsActive] ASC, [RoleId] ASC)
        INCLUDE ([RoleName]);

    PRINT 'Created IX_Role_IsActive_RoleId.';
END
ELSE
    PRINT 'IX_Role_IsActive_RoleId already exists — skipped.';

-- ── 5. UPDATE STATISTICS on affected tables ───────────────────
--   After the normalization migration statistics may be stale.
--   Forcing a full scan update resets the optimizer's estimates.
UPDATE STATISTICS dbo.ArchiveStatus WITH FULLSCAN;
PRINT 'Updated statistics: ArchiveStatus.';

UPDATE STATISTICS dbo.UserRole WITH FULLSCAN;
PRINT 'Updated statistics: UserRole.';

UPDATE STATISTICS dbo.Role WITH FULLSCAN;
PRINT 'Updated statistics: Role.';

UPDATE STATISTICS dbo.[User] WITH FULLSCAN;
PRINT 'Updated statistics: [User].';

UPDATE STATISTICS dbo.Employee WITH FULLSCAN;
PRINT 'Updated statistics: Employee.';

UPDATE STATISTICS dbo.Branch WITH FULLSCAN;
PRINT 'Updated statistics: Branch.';

UPDATE STATISTICS dbo.Department WITH FULLSCAN;
PRINT 'Updated statistics: Department.';

UPDATE STATISTICS dbo.Company WITH FULLSCAN;
PRINT 'Updated statistics: Company.';

PRINT '=== Index + statistics migration completed. ===';
GO
