-- =============================================================================
-- Migration: Add routing columns to dbo.CartridgeAuthorization
-- Date: 2026-04-19
--
-- Adds two nullable FK columns:
--
--   AssignedToUserId  — the User account that should approve this request,
--                       resolved at submission time via RolePriority routing.
--                       NULL means no approver account was found for that
--                       company/branch/dept yet (future-proof: once higher-up
--                       accounts are created, new requests will route to them).
--
--   SubmittedByUserId — tracks which IT user submitted on behalf of the employee
--                       for IT-assisted requests (replaces the old auto-approve
--                       shortcut; IT-assisted requests now go through the normal
--                       approval chain).
--
-- Routing logic (applied at INSERT time in the repository):
--   SELECT TOP 1 u.UserId
--   FROM dbo.[User] u
--   JOIN dbo.Employee ae ON u.EmpId = ae.EmpId
--   JOIN dbo.ApprovalRoleTitle art ON UPPER(LTRIM(RTRIM(ae.Position)))
--                                   = UPPER(LTRIM(RTRIM(art.PositionTitle)))
--                                AND art.IsActive = 1
--   JOIN dbo.Employee re ON re.EmpId = @EmployeeId
--   WHERE u.IsActive = 1
--     AND ae.Active  = 1
--     AND ae.ComId    = re.ComId
--     AND ae.BranchId = re.BranchId
--     AND ae.DeptId   = @DepartmentId
--   ORDER BY art.RolePriority ASC,        -- Manager first, then Supervisor, then Coordinator
--            pending_load ASC,             -- load-balance within same priority
--            u.UserId ASC                  -- deterministic tie-break
--
-- Safe to run multiple times (idempotent).
-- =============================================================================

-- AssignedToUserId
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('dbo.CartridgeAuthorization')
      AND  name      = 'AssignedToUserId'
)
BEGIN
    ALTER TABLE dbo.[CartridgeAuthorization]
        ADD [AssignedToUserId] INT NULL
            CONSTRAINT [FK_CartridgeAuthorization_AssignedUser]
                FOREIGN KEY REFERENCES [dbo].[User] ([UserId]);
    PRINT 'Added column: CartridgeAuthorization.AssignedToUserId';
END
ELSE
    PRINT 'Skipped: CartridgeAuthorization.AssignedToUserId already exists';

-- SubmittedByUserId
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('dbo.CartridgeAuthorization')
      AND  name      = 'SubmittedByUserId'
)
BEGIN
    ALTER TABLE dbo.[CartridgeAuthorization]
        ADD [SubmittedByUserId] INT NULL
            CONSTRAINT [FK_CartridgeAuthorization_SubmittedBy]
                FOREIGN KEY REFERENCES [dbo].[User] ([UserId]);
    PRINT 'Added column: CartridgeAuthorization.SubmittedByUserId';
END
ELSE
    PRINT 'Skipped: CartridgeAuthorization.SubmittedByUserId already exists';

-- Index: fast lookups for a specific approver's pending queue
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  object_id = OBJECT_ID('dbo.CartridgeAuthorization')
      AND  name      = 'IX_CartridgeAuthorization_AssignedToUserId_Status'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CartridgeAuthorization_AssignedToUserId_Status]
        ON [dbo].[CartridgeAuthorization] ([AssignedToUserId] ASC, [Status] ASC);
    PRINT 'Created index: IX_CartridgeAuthorization_AssignedToUserId_Status';
END
ELSE
    PRINT 'Skipped: index IX_CartridgeAuthorization_AssignedToUserId_Status already exists';
