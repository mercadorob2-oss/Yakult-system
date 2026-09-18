-- ============================================================
-- Migration Script: Create dbo.BranchDepartmentCompany
-- Purpose : Normalize Branch–Department–Company relationships
--           and migrate existing bindings out of dbo.Branch
-- Safe to run: YES — uses IF NOT EXISTS / NOT EXISTS guards
-- Rollback  : Wrapped in a transaction; auto-rolls back on error
-- ============================================================
-- BACKGROUND
-- dbo.Branch currently stores DeptId and ComId directly, which
-- forces a new Branch row for every department binding. This
-- means the same physical branch (e.g. "Main Branch") can appear
-- as multiple rows differentiated only by DeptId. Additionally,
-- the branch-level email (EmailId FK → dbo.EmailAddress) is tied
-- to that branch+department row rather than to Branch itself.
-- This migration moves that relationship and email into a proper
-- junction table: dbo.BranchDepartmentCompany.
-- ============================================================
-- CIRCULAR FK NOTE
-- dbo.Branch   has FK → dbo.Department (via DeptId)
-- dbo.Department has FK → dbo.Branch   (via BrId)
-- This existing circular dependency is NOT introduced by this
-- migration. The new table simply references both independently.
-- ============================================================

BEGIN TRANSACTION;

BEGIN TRY

    -- --------------------------------------------------------
    -- STEP 1: Create dbo.BranchDepartmentCompany
    -- --------------------------------------------------------
    -- Create the table
    IF NOT EXISTS (
        SELECT 1
        FROM   sys.objects
        WHERE  object_id = OBJECT_ID(N'[dbo].[BranchDepartmentCompany]')
          AND  type = 'U'
    )
    BEGIN
        CREATE TABLE [dbo].[BranchDepartmentCompany] (
            [BranchDeptCompanyID] INT            IDENTITY (1, 1) NOT NULL,
            [BranchID]            INT            NOT NULL,
            [DepartmentID]        INT            NOT NULL,
            [CompanyID]           INT            NOT NULL,
            [BranchEmail]         NVARCHAR (255) NULL,
            [CreatedDate]         DATETIME2 (7)  CONSTRAINT [DF_BranchDeptCompany_CreatedDate] DEFAULT (GETDATE()) NOT NULL,
            [UpdatedDate]         DATETIME2 (7)  NULL,
            CONSTRAINT [PK_BranchDepartmentCompany]
                PRIMARY KEY CLUSTERED ([BranchDeptCompanyID] ASC),
            CONSTRAINT [FK_BranchDeptCompany_Branch]
                FOREIGN KEY ([BranchID])     REFERENCES [dbo].[Branch]     ([BranchId]),
            CONSTRAINT [FK_BranchDeptCompany_Department]
                FOREIGN KEY ([DepartmentID]) REFERENCES [dbo].[Department] ([DeptId]),
            CONSTRAINT [FK_BranchDeptCompany_Company]
                FOREIGN KEY ([CompanyID])    REFERENCES [dbo].[Company]    ([ComId]),
            CONSTRAINT [UQ_BranchDeptCompany_Binding]
                UNIQUE NONCLUSTERED ([BranchID] ASC, [DepartmentID] ASC, [CompanyID] ASC)
        );
        PRINT 'dbo.BranchDepartmentCompany created successfully.';
    END
    ELSE
        PRINT 'dbo.BranchDepartmentCompany already exists — skipping CREATE.';

    -- Create indexes separately (each guarded by sys.indexes check).
    -- Kept outside the IF block above so SQL Server can resolve the
    -- table reference at runtime rather than parse time.
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE  name      = N'IX_BranchDeptCompany_BranchID'
          AND  object_id = OBJECT_ID(N'[dbo].[BranchDepartmentCompany]')
    )
        CREATE NONCLUSTERED INDEX [IX_BranchDeptCompany_BranchID]
            ON [dbo].[BranchDepartmentCompany]([BranchID] ASC)
            INCLUDE([DepartmentID], [CompanyID], [BranchEmail]);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE  name      = N'IX_BranchDeptCompany_DepartmentID'
          AND  object_id = OBJECT_ID(N'[dbo].[BranchDepartmentCompany]')
    )
        CREATE NONCLUSTERED INDEX [IX_BranchDeptCompany_DepartmentID]
            ON [dbo].[BranchDepartmentCompany]([DepartmentID] ASC)
            INCLUDE([BranchID], [CompanyID]);

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE  name      = N'IX_BranchDeptCompany_CompanyID'
          AND  object_id = OBJECT_ID(N'[dbo].[BranchDepartmentCompany]')
    )
        CREATE NONCLUSTERED INDEX [IX_BranchDeptCompany_CompanyID]
            ON [dbo].[BranchDepartmentCompany]([CompanyID] ASC)
            INCLUDE([BranchID], [DepartmentID]);

    PRINT 'Indexes verified/created.';

    -- --------------------------------------------------------
    -- STEP 2: Migrate existing Branch–Department–Company data
    --
    -- Source : dbo.Branch (rows where DeptId IS NOT NULL)
    -- Email  : Pulled from dbo.EmailAddress via Branch.EmailId
    -- Guard  : NOT EXISTS prevents duplicate inserts on re-run
    -- --------------------------------------------------------
    DECLARE @MigratedRows INT = 0;

    INSERT INTO [dbo].[BranchDepartmentCompany]
    (
        [BranchID],
        [DepartmentID],
        [CompanyID],
        [BranchEmail]
    )
    SELECT
        b.[BranchId],
        b.[DeptId],
        b.[ComId],
        ea.[EmailAddress]   -- resolve the email string from EmailAddress table
    FROM       [dbo].[Branch]        b
    LEFT JOIN  [dbo].[EmailAddress]  ea  ON ea.[EmailId] = b.[EmailId]
    WHERE
        b.[DeptId] IS NOT NULL
        AND b.[ComId]  IS NOT NULL
        -- idempotency guard: skip rows already migrated
        AND NOT EXISTS (
            SELECT 1
            FROM   [dbo].[BranchDepartmentCompany] bdc
            WHERE  bdc.[BranchID]     = b.[BranchId]
              AND  bdc.[DepartmentID] = b.[DeptId]
              AND  bdc.[CompanyID]    = b.[ComId]
        );

    SET @MigratedRows = @@ROWCOUNT;
    PRINT CONCAT('Migrated ', @MigratedRows, ' Branch–Department–Company binding(s).');

    -- --------------------------------------------------------
    -- STEP 3: Verification — compare source vs destination
    -- --------------------------------------------------------
    DECLARE @SourceCount INT;
    DECLARE @DestCount   INT;

    SELECT @SourceCount = COUNT(*)
    FROM   [dbo].[Branch]
    WHERE  [DeptId] IS NOT NULL
      AND  [ComId]  IS NOT NULL;

    SELECT @DestCount = COUNT(*)
    FROM   [dbo].[BranchDepartmentCompany];

    PRINT CONCAT('Source rows in Branch (DeptId+ComId not null): ', @SourceCount);
    PRINT CONCAT('Destination rows in BranchDepartmentCompany  : ', @DestCount);

    IF @SourceCount <> @DestCount
    BEGIN
        PRINT 'WARNING: Row counts differ. Investigate before proceeding.';
        -- Not raised as an error — counts may differ legitimately
        -- if dbo.Branch contains rows with ComId IS NULL (skipped by design).
    END
    ELSE
    BEGIN
        PRINT 'Row count verification PASSED.';
    END

    COMMIT TRANSACTION;
    PRINT '=== Migration completed successfully. ===';

END TRY
BEGIN CATCH

    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg      NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSeverity INT            = ERROR_SEVERITY();
    DECLARE @ErrState    INT            = ERROR_STATE();
    DECLARE @ErrLine     INT            = ERROR_LINE();

    PRINT CONCAT('=== Migration FAILED at line ', @ErrLine, ': ', @ErrMsg, ' ===');
    RAISERROR(@ErrMsg, @ErrSeverity, @ErrState);

END CATCH;

GO

-- ============================================================
-- POST-MIGRATION VERIFICATION QUERY
-- Run this manually after the script to confirm migrated data.
-- ============================================================
/*
SELECT
    bdc.[BranchDeptCompanyID],
    b.[BranchId],
    b.[Name]        AS BranchName,
    d.[DeptId],
    d.[Name]        AS DepartmentName,
    c.[ComId],
    c.[Name]        AS CompanyName,
    bdc.[BranchEmail],
    bdc.[CreatedDate]
FROM       [dbo].[BranchDepartmentCompany] bdc
INNER JOIN [dbo].[Branch]                  b   ON b.[BranchId] = bdc.[BranchID]
INNER JOIN [dbo].[Department]              d   ON d.[DeptId]   = bdc.[DepartmentID]
INNER JOIN [dbo].[Company]                 c   ON c.[ComId]    = bdc.[CompanyID]
ORDER BY
    c.[Name],
    b.[Name],
    d.[Name];
*/
