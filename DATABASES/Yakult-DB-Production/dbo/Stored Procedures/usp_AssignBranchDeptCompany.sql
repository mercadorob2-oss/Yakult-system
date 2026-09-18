-- ============================================================
-- Stored Procedure: dbo.usp_AssignBranchDeptCompany
-- ============================================================
-- PURPOSE
--   Single entry point for all Branch–Department–Company
--   assignments.  Replaces direct table writes from application
--   code, ensuring both the new mapping table and the legacy
--   columns stay consistent during the deprecation window.
--
-- BEHAVIOR
--   • INSERT if the combination does not exist
--   • UPDATE BranchEmail + UpdatedDate if it already exists
--   • NULL @DepartmentID is valid (YMC case — no department)
--   • Returns the BranchDeptCompanyID of the affected row
--
-- CALLER NOTES (C# side)
--   Use SqlDbType.Int for all ID params; pass DBNull.Value for
--   @DepartmentID when the branch has no department.
--
-- SAFE TO RE-RUN — CREATE OR ALTER replaces any prior version.
-- ============================================================

CREATE   PROCEDURE [dbo].[usp_AssignBranchDeptCompany]
    @BranchID     INT,
    @CompanyID    INT,
    @DepartmentID INT           = NULL,   -- NULL = no department (YMC)
    @BranchEmail  NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- ── Input validation ─────────────────────────────────────────────
    IF @BranchID IS NULL OR @CompanyID IS NULL
        THROW 50010, 'BranchID and CompanyID are required.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM [dbo].[Branch] WHERE [BranchId] = @BranchID AND [Active] = 1
    )
        THROW 50011, 'Branch not found or is inactive.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM [dbo].[Company] WHERE [ComId] = @CompanyID
    )
        THROW 50012, 'Company not found.', 1;

    IF @DepartmentID IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM [dbo].[Department] WHERE [DeptId] = @DepartmentID AND [Active] = 1
    )
        THROW 50013, 'Department not found or is inactive.', 1;

    -- ── Upsert ───────────────────────────────────────────────────────
    -- NULL DepartmentID requires special handling: SQL = cannot use
    -- col = NULL (always false); must use col IS NULL instead.
    DECLARE @ExistingId INT = NULL;

    IF @DepartmentID IS NOT NULL
    BEGIN
        SELECT @ExistingId = [BranchDeptCompanyID]
        FROM   [dbo].[BranchDepartmentCompany]
        WHERE  [BranchID]     = @BranchID
          AND  [CompanyID]    = @CompanyID
          AND  [DepartmentID] = @DepartmentID;
    END
    ELSE
    BEGIN
        SELECT @ExistingId = [BranchDeptCompanyID]
        FROM   [dbo].[BranchDepartmentCompany]
        WHERE  [BranchID]     = @BranchID
          AND  [CompanyID]    = @CompanyID
          AND  [DepartmentID] IS NULL;
    END

    IF @ExistingId IS NOT NULL
    BEGIN
        -- Row exists → update email + timestamp only
        UPDATE [dbo].[BranchDepartmentCompany]
        SET    [BranchEmail]  = @BranchEmail,
               [UpdatedDate]  = SYSUTCDATETIME()
        WHERE  [BranchDeptCompanyID] = @ExistingId;

        SELECT @ExistingId AS [BranchDeptCompanyID], 'UPDATED' AS [Action];
    END
    ELSE
    BEGIN
        -- New combination → insert
        INSERT INTO [dbo].[BranchDepartmentCompany]
            ([BranchID], [CompanyID], [DepartmentID], [BranchEmail], [CreatedDate])
        VALUES
            (@BranchID, @CompanyID, @DepartmentID, @BranchEmail, SYSUTCDATETIME());

        SELECT SCOPE_IDENTITY() AS [BranchDeptCompanyID], 'INSERTED' AS [Action];
    END

END;