-- ============================================================
-- Migration: Covering indexes for employee email lookup query
-- ============================================================
-- PURPOSE
--   EmployeeManagementPage loads all employees joined to
--   DepartmentAccount and DepartmentEmail to resolve branch/dept
--   email addresses.  The existing unique constraints on those
--   tables only cover the key columns used for the JOIN predicate;
--   fetching EmailAddressId requires a separate clustered-index
--   key lookup per matched row.  With many employees all matching
--   a DepartmentAccount/DepartmentEmail row this causes a timeout.
--
--   These covering indexes add EmailAddressId as an INCLUDE column
--   so SQL Server can satisfy the entire JOIN + column fetch in a
--   single index seek with no key lookup.
--
-- SAFE TO RE-RUN   — guarded by IF NOT EXISTS
-- ============================================================

SET NOCOUNT ON;

-- ── 1. DepartmentAccount — cover EmailAddressId ──────────────
--   JOIN predicate: CompanyName + DepartmentName + BranchName
--   Fetched column: EmailAddressId
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  object_id = OBJECT_ID(N'dbo.DepartmentAccount')
      AND  name      = N'IX_DepartmentAccount_EmailLookup'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DepartmentAccount_EmailLookup]
        ON [dbo].[DepartmentAccount] ([CompanyName], [DepartmentName], [BranchName])
        INCLUDE ([EmailAddressId]);

    PRINT 'Created IX_DepartmentAccount_EmailLookup.';
END
ELSE
    PRINT 'IX_DepartmentAccount_EmailLookup already exists — skipped.';

-- ── 2. DepartmentEmail — cover EmailAddressId ─────────────────
--   JOIN predicate: CompanyName + DepartmentName
--   Fetched column: EmailAddressId
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  object_id = OBJECT_ID(N'dbo.DepartmentEmail')
      AND  name      = N'IX_DepartmentEmail_EmailLookup'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DepartmentEmail_EmailLookup]
        ON [dbo].[DepartmentEmail] ([CompanyName], [DepartmentName])
        INCLUDE ([EmailAddressId]);

    PRINT 'Created IX_DepartmentEmail_EmailLookup.';
END
ELSE
    PRINT 'IX_DepartmentEmail_EmailLookup already exists — skipped.';

-- ── 3. Employee — cover org columns used in filter + sort ─────
--   The Employee table has no non-clustered index on Active+Name.
--   Queries that filter WHERE e.Active = 1 ORDER BY e.Name do a
--   full clustered scan.  This index makes that a seek + sort.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  object_id = OBJECT_ID(N'dbo.Employee')
      AND  name      = N'IX_Employee_Active_Name'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Employee_Active_Name]
        ON [dbo].[Employee] ([Active], [Name])
        INCLUDE ([EmpId], [EmployeeNumber], [Position], [ComId], [BranchId], [DeptId]);

    PRINT 'Created IX_Employee_Active_Name.';
END
ELSE
    PRINT 'IX_Employee_Active_Name already exists — skipped.';

PRINT '=== Index migration completed. ===';
GO
