-- ============================================================
-- Seed: Link YMC Center Branches → BranchDepartmentCompany
-- ============================================================
-- PURPOSE
--   Assigns the YMC Center branches to Company ID = 17 (YMC)
--   with DepartmentID = NULL (YMC has no department structure).
--
-- COMPANY  : ComId = 17  (YMC)
-- DEPT     : NULL        (YMC case — no department)
-- BRANCH TYPE expected: IsCenter = 1
--
-- SAFE TO RE-RUN — usp_AssignBranchDeptCompany handles INSERT
-- vs UPDATE internally.
-- ============================================================

DECLARE @YMC_CompanyId INT = 17;   -- ← verify: SELECT ComId, Name FROM dbo.Company WHERE ComId = 17

-- Quick sanity check: confirm Company 17 is YMC
SELECT [ComId], [Name] FROM [dbo].[Company] WHERE [ComId] = @YMC_CompanyId;
GO


-- ============================================================
-- STEP 0a: Fuzzy search — find actual names stored in the DB
-- ============================================================
-- Uses two filters:
--   1. Name LIKE '%Center%'          — all center branches
--   2. Location keyword list          — scoped to the 50 listed branches
--
-- Review the output. If a branch is missing, it may be inactive,
-- spelled differently, or not yet in dbo.Branch.
-- ============================================================
SELECT
    b.[BranchId],
    b.[Name]       AS BranchName,
    b.[ComId],
    c.[Name]       AS CurrentCompany,
    b.[IsCenter],
    b.[Active],
    b.[DeptId]     AS LegacyDeptId
FROM       [dbo].[Branch]  b
LEFT JOIN  [dbo].[Company] c ON c.[ComId] = b.[ComId]
WHERE
    b.[Name] LIKE N'%Center%'
    AND (
        b.[Name] LIKE N'%Alabang%'
        OR b.[Name] LIKE N'%Angono%'
        OR b.[Name] LIKE N'%Baclaran%'
        OR b.[Name] LIKE N'%Baliuag%'
        OR b.[Name] LIKE N'%Bi_an%'          -- covers Biñan /
        OR b.[Name] LIKE N'%Binondo%'
        OR b.[Name] LIKE N'%Cainta%'
        OR b.[Name] LIKE N'%Calamba%'
        OR b.[Name] LIKE N'%Chinatown%'
        OR b.[Name] LIKE N'%Cogeo%'
        OR b.[Name] LIKE N'%Commonwealth%'
        OR b.[Name] LIKE N'%Congressional%'
        OR b.[Name] LIKE N'%Cubao%'
        OR b.[Name] LIKE N'%Dasma%'          -- covers Dasmariñas 
        OR b.[Name] LIKE N'%Del Monte%'
        OR b.[Name] LIKE N'%Divisoria%'
        OR b.[Name] LIKE N'%Fairview%'
        OR b.[Name] LIKE N'%Imus%'
        OR b.[Name] LIKE N'%Kalookan%'
        OR b.[Name] LIKE N'%Las Pi_as%'      -- covers Las Piñas 
        OR b.[Name] LIKE N'%Malabon%'
        OR b.[Name] LIKE N'%Malate%'
        OR b.[Name] LIKE N'%Malolos%'
        OR b.[Name] LIKE N'%Mandaluyong%'
        OR b.[Name] LIKE N'%Marikina%'
        OR b.[Name] LIKE N'%Meycauayan%'
        OR b.[Name] LIKE N'%Molino%'
        OR b.[Name] LIKE N'%Navotas%'
        OR b.[Name] LIKE N'%Novaliches%'
        OR b.[Name] LIKE N'%Noveleta%'
        OR b.[Name] LIKE N'%Para_aque%'      -- covers Parañaque / Paranaque
        OR b.[Name] LIKE N'%Pasay%'
        OR b.[Name] LIKE N'%Pasig%'
        OR b.[Name] LIKE N'%Pateros%'
        OR b.[Name] LIKE N'%Quezon%'
        OR b.[Name] LIKE N'%Rodriguez%'
        OR b.[Name] LIKE N'%Sampaloc%'
        OR b.[Name] LIKE N'%San Andres%'
        OR b.[Name] LIKE N'%San Juan%'
        OR b.[Name] LIKE N'%San Pablo%'
        OR b.[Name] LIKE N'%Sangandaan%'
        OR b.[Name] LIKE N'%Sta%Cruz%'
        OR b.[Name] LIKE N'%Sta%Maria%'
        OR b.[Name] LIKE N'%Sta%Rosa%'
        OR b.[Name] LIKE N'%Taguig%'
        OR b.[Name] LIKE N'%Tayuman%'
        OR b.[Name] LIKE N'%Tondo%'
        OR b.[Name] LIKE N'%Trece%'
        OR b.[Name] LIKE N'%Valenzuela%'
        OR b.[Name] LIKE N'%Zabarte%'
    )
ORDER BY b.[Name];
GO


-- ============================================================
-- STEP 0b: Current BDC status for these branches
-- ============================================================
SELECT
    b.[BranchId],
    b.[Name]                    AS BranchName,
    bdc.[BranchDeptCompanyID],
    bdc.[CompanyID]             AS BDC_CompanyId,
    c_bdc.[Name]                AS BDC_CompanyName,
    bdc.[DepartmentID]          AS BDC_DeptId,
    CASE
        WHEN bdc.[BranchDeptCompanyID] IS NULL          THEN 'NOT IN BDC — will be inserted'
        WHEN bdc.[CompanyID] = 17 AND
             bdc.[DepartmentID] IS NULL                 THEN 'Already linked to YMC (no dept)'
        ELSE                                                 'Linked elsewhere'
    END                         AS Status
FROM       [dbo].[Branch]                  b
LEFT JOIN  [dbo].[BranchDepartmentCompany] bdc
               ON bdc.[BranchID]  = b.[BranchId]
              AND bdc.[CompanyID] = 17
              AND bdc.[DepartmentID] IS NULL
LEFT JOIN  [dbo].[Company] c_bdc ON c_bdc.[ComId] = bdc.[CompanyID]
WHERE
    b.[Name] LIKE N'%Center%'
    AND (
        b.[Name] LIKE N'%Alabang%'    OR b.[Name] LIKE N'%Angono%'
        OR b.[Name] LIKE N'%Baclaran%' OR b.[Name] LIKE N'%Baliuag%'
        OR b.[Name] LIKE N'%Bi_an%'    OR b.[Name] LIKE N'%Binondo%'
        OR b.[Name] LIKE N'%Cainta%'   OR b.[Name] LIKE N'%Calamba%'
        OR b.[Name] LIKE N'%Chinatown%' OR b.[Name] LIKE N'%Cogeo%'
        OR b.[Name] LIKE N'%Commonwealth%' OR b.[Name] LIKE N'%Congressional%'
        OR b.[Name] LIKE N'%Cubao%'    OR b.[Name] LIKE N'%Dasma%'
        OR b.[Name] LIKE N'%Del Monte%' OR b.[Name] LIKE N'%Divisoria%'
        OR b.[Name] LIKE N'%Fairview%' OR b.[Name] LIKE N'%Imus%'
        OR b.[Name] LIKE N'%Kalookan%' OR b.[Name] LIKE N'%Las Pi_as%'
        OR b.[Name] LIKE N'%Malabon%'  OR b.[Name] LIKE N'%Malate%'
        OR b.[Name] LIKE N'%Malolos%'  OR b.[Name] LIKE N'%Mandaluyong%'
        OR b.[Name] LIKE N'%Marikina%' OR b.[Name] LIKE N'%Meycauayan%'
        OR b.[Name] LIKE N'%Molino%'   OR b.[Name] LIKE N'%Navotas%'
        OR b.[Name] LIKE N'%Novaliches%' OR b.[Name] LIKE N'%Noveleta%'
        OR b.[Name] LIKE N'%Para_aque%' OR b.[Name] LIKE N'%Pasay%'
        OR b.[Name] LIKE N'%Pasig%'    OR b.[Name] LIKE N'%Pateros%'
        OR b.[Name] LIKE N'%Quezon%'   OR b.[Name] LIKE N'%Rodriguez%'
        OR b.[Name] LIKE N'%Sampaloc%' OR b.[Name] LIKE N'%San Andres%'
        OR b.[Name] LIKE N'%San Juan%' OR b.[Name] LIKE N'%San Pablo%'
        OR b.[Name] LIKE N'%Sangandaan%' OR b.[Name] LIKE N'%Sta%Cruz%'
        OR b.[Name] LIKE N'%Sta%Maria%' OR b.[Name] LIKE N'%Sta%Rosa%'
        OR b.[Name] LIKE N'%Taguig%'   OR b.[Name] LIKE N'%Tayuman%'
        OR b.[Name] LIKE N'%Tondo%'    OR b.[Name] LIKE N'%Trece%'
        OR b.[Name] LIKE N'%Valenzuela%' OR b.[Name] LIKE N'%Zabarte%'
    )
ORDER BY b.[Name];
GO


-- ============================================================
-- STEP 1: Assign branches to YMC (ComId = 17, DeptId = NULL)
-- ============================================================
-- !! Run Steps 0a and 0b first and confirm the results !!
-- !! Adjust @YMC_CompanyId if needed                   !!
-- ============================================================

DECLARE @YMC_CompanyId INT = 17;
DECLARE @Inserted      INT = 0;
DECLARE @Updated       INT = 0;
DECLARE @Skipped       INT = 0;
DECLARE @BranchID      INT;
DECLARE @Action        NVARCHAR(10);

DECLARE @Targets TABLE (BranchID INT NOT NULL);

INSERT INTO @Targets (BranchID)
SELECT b.[BranchId]
FROM   [dbo].[Branch] b
WHERE
    b.[Active]  = 1
    AND b.[Name] LIKE N'%Center%'
    AND (
        b.[Name] LIKE N'%Alabang%'    OR b.[Name] LIKE N'%Angono%'
        OR b.[Name] LIKE N'%Baclaran%' OR b.[Name] LIKE N'%Baliuag%'
        OR b.[Name] LIKE N'%Bi_an%'    OR b.[Name] LIKE N'%Binondo%'
        OR b.[Name] LIKE N'%Cainta%'   OR b.[Name] LIKE N'%Calamba%'
        OR b.[Name] LIKE N'%Chinatown%' OR b.[Name] LIKE N'%Cogeo%'
        OR b.[Name] LIKE N'%Commonwealth%' OR b.[Name] LIKE N'%Congressional%'
        OR b.[Name] LIKE N'%Cubao%'    OR b.[Name] LIKE N'%Dasma%'
        OR b.[Name] LIKE N'%Del Monte%' OR b.[Name] LIKE N'%Divisoria%'
        OR b.[Name] LIKE N'%Fairview%' OR b.[Name] LIKE N'%Imus%'
        OR b.[Name] LIKE N'%Kalookan%' OR b.[Name] LIKE N'%Las Pi_as%'
        OR b.[Name] LIKE N'%Malabon%'  OR b.[Name] LIKE N'%Malate%'
        OR b.[Name] LIKE N'%Malolos%'  OR b.[Name] LIKE N'%Mandaluyong%'
        OR b.[Name] LIKE N'%Marikina%' OR b.[Name] LIKE N'%Meycauayan%'
        OR b.[Name] LIKE N'%Molino%'   OR b.[Name] LIKE N'%Navotas%'
        OR b.[Name] LIKE N'%Novaliches%' OR b.[Name] LIKE N'%Noveleta%'
        OR b.[Name] LIKE N'%Para_aque%' OR b.[Name] LIKE N'%Pasay%'
        OR b.[Name] LIKE N'%Pasig%'    OR b.[Name] LIKE N'%Pateros%'
        OR b.[Name] LIKE N'%Quezon%'   OR b.[Name] LIKE N'%Rodriguez%'
        OR b.[Name] LIKE N'%Sampaloc%' OR b.[Name] LIKE N'%San Andres%'
        OR b.[Name] LIKE N'%San Juan%' OR b.[Name] LIKE N'%San Pablo%'
        OR b.[Name] LIKE N'%Sangandaan%' OR b.[Name] LIKE N'%Sta%Cruz%'
        OR b.[Name] LIKE N'%Sta%Maria%' OR b.[Name] LIKE N'%Sta%Rosa%'
        OR b.[Name] LIKE N'%Taguig%'   OR b.[Name] LIKE N'%Tayuman%'
        OR b.[Name] LIKE N'%Tondo%'    OR b.[Name] LIKE N'%Trece%'
        OR b.[Name] LIKE N'%Valenzuela%' OR b.[Name] LIKE N'%Zabarte%'
    );

PRINT CONCAT(CAST(@@ROWCOUNT AS VARCHAR), ' target branch(es) found.');

DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT BranchID FROM @Targets;

OPEN cur;
FETCH NEXT FROM cur INTO @BranchID;

WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @Result TABLE (BranchDeptCompanyID INT, Action NVARCHAR(10));

    BEGIN TRY
        INSERT INTO @Result
        EXEC [dbo].[usp_AssignBranchDeptCompany]
            @BranchID     = @BranchID,
            @CompanyID    = @YMC_CompanyId,
            @DepartmentID = NULL,         -- YMC: no department
            @BranchEmail  = NULL;

        SELECT @Action = [Action] FROM @Result;
        DELETE FROM @Result;

        IF @Action = 'INSERTED' SET @Inserted += 1;
        IF @Action = 'UPDATED'  SET @Updated  += 1;
    END TRY
    BEGIN CATCH
        PRINT CONCAT('  SKIPPED BranchId ', @BranchID, ': ', ERROR_MESSAGE());
        SET @Skipped += 1;
        DELETE FROM @Result;
    END CATCH;

    FETCH NEXT FROM cur INTO @BranchID;
END

CLOSE cur;
DEALLOCATE cur;

PRINT CONCAT('Done.  Inserted: ', @Inserted,
             '  Updated: ',  @Updated,
             '  Skipped: ',  @Skipped);
GO


-- ============================================================
-- STEP 2: Verification — confirm all 50 centers are in BDC
-- ============================================================
SELECT
    b.[BranchId],
    b.[Name]            AS BranchName,
    c.[Name]            AS Company,
    bdc.[BranchDeptCompanyID],
    bdc.[DepartmentID]  AS DeptId,        -- should be NULL for all YMC rows
    bdc.[CreatedDate],
    bdc.[UpdatedDate],
    CASE
        WHEN bdc.[BranchDeptCompanyID] IS NULL THEN '⚠ MISSING'
        ELSE '✓ OK'
    END                 AS Status
FROM       [dbo].[Branch]                  b
LEFT JOIN  [dbo].[Company]                 c   ON c.[ComId]   = b.[ComId]
LEFT JOIN  [dbo].[BranchDepartmentCompany] bdc
               ON bdc.[BranchID]     = b.[BranchId]
              AND bdc.[CompanyID]    = 17
              AND bdc.[DepartmentID] IS NULL
WHERE
    b.[Name] LIKE N'%Center%'
    AND (
        b.[Name] LIKE N'%Alabang%'    OR b.[Name] LIKE N'%Angono%'
        OR b.[Name] LIKE N'%Baclaran%' OR b.[Name] LIKE N'%Baliuag%'
        OR b.[Name] LIKE N'%Bi_an%'    OR b.[Name] LIKE N'%Binondo%'
        OR b.[Name] LIKE N'%Cainta%'   OR b.[Name] LIKE N'%Calamba%'
        OR b.[Name] LIKE N'%Chinatown%' OR b.[Name] LIKE N'%Cogeo%'
        OR b.[Name] LIKE N'%Commonwealth%' OR b.[Name] LIKE N'%Congressional%'
        OR b.[Name] LIKE N'%Cubao%'    OR b.[Name] LIKE N'%Dasma%'
        OR b.[Name] LIKE N'%Del Monte%' OR b.[Name] LIKE N'%Divisoria%'
        OR b.[Name] LIKE N'%Fairview%' OR b.[Name] LIKE N'%Imus%'
        OR b.[Name] LIKE N'%Kalookan%' OR b.[Name] LIKE N'%Las Pi_as%'
        OR b.[Name] LIKE N'%Malabon%'  OR b.[Name] LIKE N'%Malate%'
        OR b.[Name] LIKE N'%Malolos%'  OR b.[Name] LIKE N'%Mandaluyong%'
        OR b.[Name] LIKE N'%Marikina%' OR b.[Name] LIKE N'%Meycauayan%'
        OR b.[Name] LIKE N'%Molino%'   OR b.[Name] LIKE N'%Navotas%'
        OR b.[Name] LIKE N'%Novaliches%' OR b.[Name] LIKE N'%Noveleta%'
        OR b.[Name] LIKE N'%Para_aque%' OR b.[Name] LIKE N'%Pasay%'
        OR b.[Name] LIKE N'%Pasig%'    OR b.[Name] LIKE N'%Pateros%'
        OR b.[Name] LIKE N'%Quezon%'   OR b.[Name] LIKE N'%Rodriguez%'
        OR b.[Name] LIKE N'%Sampaloc%' OR b.[Name] LIKE N'%San Andres%'
        OR b.[Name] LIKE N'%San Juan%' OR b.[Name] LIKE N'%San Pablo%'
        OR b.[Name] LIKE N'%Sangandaan%' OR b.[Name] LIKE N'%Sta%Cruz%'
        OR b.[Name] LIKE N'%Sta%Maria%' OR b.[Name] LIKE N'%Sta%Rosa%'
        OR b.[Name] LIKE N'%Taguig%'   OR b.[Name] LIKE N'%Tayuman%'
        OR b.[Name] LIKE N'%Tondo%'    OR b.[Name] LIKE N'%Trece%'
        OR b.[Name] LIKE N'%Valenzuela%' OR b.[Name] LIKE N'%Zabarte%'
    )
ORDER BY Status DESC, b.[Name];
-- Expected: 50 rows, all Status = '✓ OK', all DeptId = NULL
GO
