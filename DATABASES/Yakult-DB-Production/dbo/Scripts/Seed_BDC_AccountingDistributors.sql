-- ============================================================
-- Seed: Link Accounting Dept Distributors → BranchDepartmentCompany
-- ============================================================
-- PURPOSE
--   The branches listed below are distributor companies managed
--   by the Accounting Department.  This script links them in
--   dbo.BranchDepartmentCompany so the application can query
--   "which branches belong to Accounting?" via the junction table.
--
-- NOTE ON ParentDeptId
--   ParentDeptId on dbo.Department is for DEPARTMENT → DEPARTMENT
--   hierarchy (e.g. "Accounting Subsidiaries" as a child dept of
--   "Accounting Department").  It has no role here.
--   These are Branch records, not Department records.
--
-- SAFE TO RE-RUN — the SP handles INSERT vs UPDATE internally.
-- ============================================================

-- ── Step 0a: FUZZY search — find actual names stored in the DB ───────
-- Run this first. Copy the exact BranchName values from the results
-- into Step 0b below, then into the assignment block in Step 1.
SELECT
    b.[BranchId],
    b.[Name]    AS BranchName,
    b.[ComId],
    c.[Name]    AS CompanyName,
    b.[Active],
    b.[DeptId]  AS LegacyDeptId,
    d.[Name]    AS LegacyDeptName
FROM       [dbo].[Branch]     b
LEFT JOIN  [dbo].[Company]    c ON c.[ComId]    = b.[ComId]
LEFT JOIN  [dbo].[Department] d ON d.[DeptId]   = b.[DeptId]
WHERE
    b.[Name] LIKE N'%Synbiotic%'
    OR b.[Name] LIKE N'%Prohealth%'
    OR b.[Name] LIKE N'%Probiotic%'
    OR b.[Name] LIKE N'%Prebiotic%'
    OR b.[Name] LIKE N'%Micromate%'
    OR b.[Name] LIKE N'%LCS Caraga%'
    OR b.[Name] LIKE N'%Lactobacillus%'
    OR b.[Name] LIKE N'%Lacto Frontier%'
    OR b.[Name] LIKE N'%Isabela%'
    OR b.[Name] LIKE N'%Healthy Tummy%'
    OR b.[Name] LIKE N'%Gensbio%'
    OR b.[Name] LIKE N'%Galacto%'
    OR b.[Name] LIKE N'%Biomate%'
    OR b.[Name] LIKE N'%Bellyfit%'
    OR b.[Name] LIKE N'%Actigen%'
ORDER BY b.[Name];
GO

-- ── Step 0b: Full-state diagnostic using the exact names from 0a ─────
-- After confirming the real names above, paste them into the IN list
-- here to see their current BranchDepartmentCompany status.
SELECT
    b.[BranchId],
    b.[Name]                         AS BranchName,
    b.[ComId],
    c.[Name]                         AS CompanyName,
    b.[DeptId]                       AS LegacyDeptId,
    d_leg.[Name]                     AS LegacyDeptName,
    bdc.[BranchDeptCompanyID],
    bdc.[DepartmentID]               AS BDC_DeptId,
    d_bdc.[Name]                     AS BDC_DeptName,
    CASE
        WHEN bdc.[BranchDeptCompanyID] IS NULL THEN 'NOT IN BDC — will be inserted'
        WHEN bdc.[DepartmentID] = 66           THEN 'Already linked to Accounting'
        ELSE                                        'Linked to different dept'
    END                              AS Status
FROM       [dbo].[Branch]               b
LEFT JOIN  [dbo].[Company]              c     ON c.[ComId]     = b.[ComId]
LEFT JOIN  [dbo].[Department]           d_leg ON d_leg.[DeptId] = b.[DeptId]
LEFT JOIN  [dbo].[BranchDepartmentCompany] bdc
               ON bdc.[BranchID]  = b.[BranchId]
              AND bdc.[CompanyID] = b.[ComId]
LEFT JOIN  [dbo].[Department]           d_bdc ON d_bdc.[DeptId] = bdc.[DepartmentID]
WHERE
    -- !! Replace with the exact BranchName values from Step 0a !!
    b.[Name] LIKE N'%Synbiotic%'
    OR b.[Name] LIKE N'%Prohealth%'
    OR b.[Name] LIKE N'%Probiotic%'
    OR b.[Name] LIKE N'%Prebiotic%'
    OR b.[Name] LIKE N'%Micromate%'
    OR b.[Name] LIKE N'%LCS Caraga%'
    OR b.[Name] LIKE N'%Lactobacillus%'
    OR b.[Name] LIKE N'%Lacto Frontier%'
    OR b.[Name] LIKE N'%Isabela%'
    OR b.[Name] LIKE N'%Healthy Tummy%'
    OR b.[Name] LIKE N'%Gensbio%'
    OR b.[Name] LIKE N'%Galacto%'
    OR b.[Name] LIKE N'%Biomate%'
    OR b.[Name] LIKE N'%Bellyfit%'
    OR b.[Name] LIKE N'%Actigen%'
ORDER BY b.[Name];
GO

-- !! STOP HERE !!
-- Review the diagnostic output above.
--   • Confirm DeptId 66 = Accounting Department (verify with the query below)
--   • Confirm the branch names match exactly (check for spacing/typo variants)
--   • Note any branches with Status = 'Linked to different dept' and decide
--     whether to reassign them.
-- ============================================================

-- Quick check: confirm DeptId = 66 is actually Accounting Department
SELECT [DeptId], [Name], [ComId], [Active]
FROM   [dbo].[Department]
WHERE  [DeptId] = 66;
GO


-- ============================================================
-- Step 1: Bulk-assign via the stored procedure
-- ============================================================
-- Calls dbo.usp_AssignBranchDeptCompany for each distributor.
-- The SP will INSERT if not present, UPDATE email if already there.
-- BranchEmail is left NULL here — update per-branch as needed.
--
-- !! Replace DeptId = 66 if Accounting is a different DeptId !!
-- ============================================================

DECLARE @AccountingDeptId INT = 66;  -- ← verify this matches your DB

-- Build a working set of (BranchId, ComId) for the named branches
DECLARE @Targets TABLE (
    BranchID  INT NOT NULL,
    CompanyID INT NOT NULL
);

-- Uses LIKE so minor name differences (punctuation, spacing) still match.
-- After running Step 0a, replace the LIKE patterns below with exact
-- names using = if you want stricter control.
INSERT INTO @Targets (BranchID, CompanyID)
SELECT b.[BranchId], b.[ComId]
FROM   [dbo].[Branch] b
WHERE (
    b.[Name] LIKE N'%Synbiotic%'
    OR b.[Name] LIKE N'%Prohealth%'
    OR b.[Name] LIKE N'%Probiotic%'
    OR b.[Name] LIKE N'%Prebiotic%'
    OR b.[Name] LIKE N'%Micromate%'
    OR b.[Name] LIKE N'%LCS Caraga%'
    OR b.[Name] LIKE N'%Lactobacillus%'
    OR b.[Name] LIKE N'%Lacto Frontier%'
    OR b.[Name] LIKE N'%Isabela%'
    OR b.[Name] LIKE N'%Healthy Tummy%'
    OR b.[Name] LIKE N'%Gensbio%'
    OR b.[Name] LIKE N'%Galacto%'
    OR b.[Name] LIKE N'%Biomate%'
    OR b.[Name] LIKE N'%Bellyfit%'
    OR b.[Name] LIKE N'%Actigen%'
)
  AND b.[ComId] IS NOT NULL   -- must have a company to link
  AND b.[Active] = 1;

PRINT CONCAT(CAST(@@ROWCOUNT AS VARCHAR), ' target branch(es) found.');

-- Call the SP row-by-row
DECLARE @BranchID  INT;
DECLARE @CompanyID INT;
DECLARE @Inserted  INT = 0;
DECLARE @Updated   INT = 0;
DECLARE @Action    NVARCHAR(10);

DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT BranchID, CompanyID FROM @Targets;

OPEN cur;
FETCH NEXT FROM cur INTO @BranchID, @CompanyID;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Capture the action returned by the SP
    DECLARE @Result TABLE (BranchDeptCompanyID INT, Action NVARCHAR(10));

    INSERT INTO @Result
    EXEC [dbo].[usp_AssignBranchDeptCompany]
        @BranchID     = @BranchID,
        @CompanyID    = @CompanyID,
        @DepartmentID = @AccountingDeptId,
        @BranchEmail  = NULL;

    SELECT @Action = [Action] FROM @Result;
    DELETE FROM @Result;

    IF @Action = 'INSERTED' SET @Inserted += 1;
    IF @Action = 'UPDATED'  SET @Updated  += 1;

    FETCH NEXT FROM cur INTO @BranchID, @CompanyID;
END

CLOSE cur;
DEALLOCATE cur;

PRINT CONCAT('Done. Inserted: ', @Inserted, '  Updated: ', @Updated);
GO


-- ============================================================
-- Step 2: Verification — confirm all links are in BDC
-- ============================================================
SELECT
    b.[BranchId],
    b.[Name]      AS BranchName,
    c.[Name]      AS CompanyName,
    d.[Name]      AS LinkedDepartment,
    bdc.[BranchDeptCompanyID],
    bdc.[CreatedDate],
    bdc.[UpdatedDate]
FROM       [dbo].[Branch]               b
JOIN       [dbo].[Company]              c   ON c.[ComId]     = b.[ComId]
LEFT JOIN  [dbo].[BranchDepartmentCompany] bdc
               ON bdc.[BranchID]     = b.[BranchId]
              AND bdc.[CompanyID]    = b.[ComId]
              AND bdc.[DepartmentID] = 66   -- Accounting
LEFT JOIN  [dbo].[Department]           d   ON d.[DeptId]    = bdc.[DepartmentID]
WHERE (
    b.[Name] LIKE N'%Synbiotic%'
    OR b.[Name] LIKE N'%Prohealth%'
    OR b.[Name] LIKE N'%Probiotic%'
    OR b.[Name] LIKE N'%Prebiotic%'
    OR b.[Name] LIKE N'%Micromate%'
    OR b.[Name] LIKE N'%LCS Caraga%'
    OR b.[Name] LIKE N'%Lactobacillus%'
    OR b.[Name] LIKE N'%Lacto Frontier%'
    OR b.[Name] LIKE N'%Isabela%'
    OR b.[Name] LIKE N'%Healthy Tummy%'
    OR b.[Name] LIKE N'%Gensbio%'
    OR b.[Name] LIKE N'%Galacto%'
    OR b.[Name] LIKE N'%Biomate%'
    OR b.[Name] LIKE N'%Bellyfit%'
    OR b.[Name] LIKE N'%Actigen%'
)
ORDER BY b.[Name];
-- Expected: every row has a BranchDeptCompanyID and LinkedDepartment = 'Accounting Department'
GO


-- ============================================================
-- APPENDIX: What ParentDeptId IS actually for
-- ============================================================
-- If the Accounting Department later splits into sub-departments
-- (e.g. the old Section='ACTG SUBS' rows), you would set
-- ParentDeptId on those child departments, like this:
--
--   -- Make "Accounting Subsidiaries Dept" a child of "Accounting Dept"
--   UPDATE dbo.Department
--   SET    ParentDeptId = 66          -- Accounting Department
--   WHERE  Section = N'ACTG SUBS'
--     AND  ParentDeptId IS NULL;      -- idempotent
--
-- That is entirely separate from the Branch assignments above.
-- ParentDeptId = Department → Department relationship only.
-- ============================================================
