-- ============================================================
-- Migration: Merge duplicate Las Pinas branch rows
-- ============================================================
-- Keeps:   LAS PIÑAS CENTER  (the ñ version)
-- Removes: Las Pinas Center  (the plain-n version)
--
-- BACKGROUND
--   Migration_Branch_MergeDuplicateEnye.sql handled Dasmarinas,
--   Binan, and Paranaque but omitted Las Pinas.  Both branch rows
--   remain active, so BranchDepartmentCompany has duplicate
--   department links — the same department appears twice (once per
--   branch) in any dropdown scoped to that branch/company pair.
--
-- WHAT THIS SCRIPT DOES
--   1. Identifies the two BranchIds.
--   2. Re-maps every FK child reference from the plain-n branch
--      to the ñ branch (BDC, Employee, Set, etc.).
--   3. Removes the now-orphaned CallBranchSmtpProfileLink and
--      CallBranchNotificationRecipient rows for the old branch.
--   4. Hard-deletes the plain-n Branch row.
--
-- SAFE TO RE-RUN   — old branch won't exist after first run; all
--                    UPDATE/DELETE steps quietly affect 0 rows.
-- ROLLBACK         — single transaction; auto-rolls back on error.
-- PREREQUISITES    — Diagnostic_Branch_LasPinasDuplicate.sql
--                    (run it first to confirm D1 returns 2 rows)
-- ============================================================

BEGIN TRANSACTION;
BEGIN TRY

    -- ── Identify the pair ─────────────────────────────────────────────
    DECLARE @OldId INT = (
        SELECT BranchId FROM dbo.Branch
        WHERE  Name LIKE N'%Las Pi%as%' COLLATE Latin1_General_CI_AI
          AND  Name NOT LIKE N'%ñ%'     COLLATE Latin1_General_CI_AS
    );
    DECLARE @NewId INT = (
        SELECT BranchId FROM dbo.Branch
        WHERE  Name LIKE N'%Las Pi%as%' COLLATE Latin1_General_CI_AI
          AND  Name LIKE N'%ñ%'         COLLATE Latin1_General_CI_AS
    );

    IF @OldId IS NULL
    BEGIN
        PRINT 'Las Pinas plain-n branch not found — already merged or does not exist. Nothing to do.';
        COMMIT TRANSACTION;
        RETURN;
    END

    IF @NewId IS NULL
        RAISERROR('LAS PIÑAS CENTER (ñ) branch not found. Cannot proceed.', 16, 1);

    PRINT 'Las Pinas merge: old BranchId = ' + CAST(@OldId AS NVARCHAR) + ', keep BranchId = ' + CAST(@NewId AS NVARCHAR);

    -- ── Employee ──────────────────────────────────────────────────────
    UPDATE dbo.Employee
    SET    BranchId = @NewId
    WHERE  BranchId = @OldId;
    PRINT '  Employee rows re-pointed: ' + CAST(@@ROWCOUNT AS NVARCHAR);

    -- ── CartridgeApproval ─────────────────────────────────────────────
    UPDATE dbo.CartridgeApproval
    SET    BranchId = @NewId
    WHERE  BranchId = @OldId;
    PRINT '  CartridgeApproval rows re-pointed: ' + CAST(@@ROWCOUNT AS NVARCHAR);

    -- ── CartridgeMovement ─────────────────────────────────────────────
    UPDATE dbo.CartridgeMovement
    SET    BranchId = @NewId
    WHERE  BranchId = @OldId;
    PRINT '  CartridgeMovement rows re-pointed: ' + CAST(@@ROWCOUNT AS NVARCHAR);

    -- ── EmptyCartridge ────────────────────────────────────────────────
    UPDATE dbo.EmptyCartridge
    SET    BranchId = @NewId
    WHERE  BranchId = @OldId;
    PRINT '  EmptyCartridge rows re-pointed: ' + CAST(@@ROWCOUNT AS NVARCHAR);

    -- ── CallTicket ────────────────────────────────────────────────────
    UPDATE dbo.CallTicket
    SET    BranchId = @NewId
    WHERE  BranchId = @OldId;
    PRINT '  CallTicket rows re-pointed: ' + CAST(@@ROWCOUNT AS NVARCHAR);

    -- ── ItemAuditTrail ────────────────────────────────────────────────
    UPDATE dbo.ItemAuditTrail
    SET    BranchId = @NewId
    WHERE  BranchId = @OldId;
    PRINT '  ItemAuditTrail rows re-pointed: ' + CAST(@@ROWCOUNT AS NVARCHAR);

    -- ── UnfulfilledCartridgeExchange ──────────────────────────────────
    UPDATE dbo.UnfulfilledCartridgeExchange
    SET    BranchId = @NewId
    WHERE  BranchId = @OldId;
    PRINT '  UnfulfilledCartridgeExchange rows re-pointed: ' + CAST(@@ROWCOUNT AS NVARCHAR);

    -- ── Set.CurrentBranchId ───────────────────────────────────────────
    UPDATE dbo.[Set]
    SET    CurrentBranchId = @NewId
    WHERE  CurrentBranchId = @OldId;
    PRINT '  Set rows re-pointed: ' + CAST(@@ROWCOUNT AS NVARCHAR);

    -- ── SetTransfer ───────────────────────────────────────────────────
    UPDATE dbo.SetTransfer
    SET    FromBranchId = @NewId
    WHERE  FromBranchId = @OldId;
    PRINT '  SetTransfer.FromBranchId rows re-pointed: ' + CAST(@@ROWCOUNT AS NVARCHAR);

    UPDATE dbo.SetTransfer
    SET    ToBranchId = @NewId
    WHERE  ToBranchId = @OldId;
    PRINT '  SetTransfer.ToBranchId rows re-pointed: ' + CAST(@@ROWCOUNT AS NVARCHAR);

    -- ── BranchDepartmentCompany ───────────────────────────────────────
    -- If the ñ branch already has the same (DeptId, CompanyId) BDC row,
    -- drop the old duplicate; otherwise re-point it.
    DELETE bdc
    FROM   dbo.BranchDepartmentCompany bdc
    WHERE  bdc.BranchID = @OldId
      AND  EXISTS (
          SELECT 1 FROM dbo.BranchDepartmentCompany x
          WHERE  x.BranchID    = @NewId
            AND  x.CompanyID   = bdc.CompanyID
            AND  (x.DepartmentID = bdc.DepartmentID
                  OR (x.DepartmentID IS NULL AND bdc.DepartmentID IS NULL))
      );
    PRINT '  BDC rows deleted (canonical already present): ' + CAST(@@ROWCOUNT AS NVARCHAR);

    UPDATE dbo.BranchDepartmentCompany
    SET    BranchID = @NewId
    WHERE  BranchID = @OldId;
    PRINT '  BDC rows re-pointed (no conflict): ' + CAST(@@ROWCOUNT AS NVARCHAR);

    -- ── CallBranchSmtpProfileLink ─────────────────────────────────────
    INSERT INTO dbo.CallBranchSmtpProfileLink (BranchId, ProfileId, IsActive, UpdatedAt, UpdatedByUserId)
    SELECT @NewId, ProfileId, IsActive, UpdatedAt, UpdatedByUserId
    FROM   dbo.CallBranchSmtpProfileLink
    WHERE  BranchId = @OldId
      AND  NOT EXISTS (
          SELECT 1 FROM dbo.CallBranchSmtpProfileLink
          WHERE  BranchId = @NewId
      );
    DELETE FROM dbo.CallBranchSmtpProfileLink WHERE BranchId = @OldId;
    PRINT '  CallBranchSmtpProfileLink migrated.';

    -- ── CallBranchNotificationRecipient ───────────────────────────────
    INSERT INTO dbo.CallBranchNotificationRecipient
        (BranchId, RecipientEmails, EscalationEmails, IsActive, UpdatedAt, UpdatedByUserId)
    SELECT @NewId, RecipientEmails, EscalationEmails, IsActive, UpdatedAt, UpdatedByUserId
    FROM   dbo.CallBranchNotificationRecipient
    WHERE  BranchId = @OldId
      AND  NOT EXISTS (
          SELECT 1 FROM dbo.CallBranchNotificationRecipient
          WHERE  BranchId = @NewId
      );
    DELETE FROM dbo.CallBranchNotificationRecipient WHERE BranchId = @OldId;
    PRINT '  CallBranchNotificationRecipient migrated.';

    -- ── Delete the old branch row ─────────────────────────────────────
    DELETE FROM dbo.Branch WHERE BranchId = @OldId;
    PRINT 'Las Pinas plain-n branch deleted (BranchId = ' + CAST(@OldId AS NVARCHAR) + ').';

    COMMIT TRANSACTION;
    PRINT '=== Migration complete. LAS PIÑAS CENTER is now the single canonical branch. ===';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @Msg NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR(@Msg, 16, 1);
END CATCH;
GO

-- ============================================================
-- Validation — run each block individually after the migration.
-- ============================================================

-- V1: Only one Las Pinas branch remains
PRINT '--- V1: Las Pinas branches remaining ---';
SELECT BranchId, Name, BranchType, Acronym, Active
FROM   dbo.Branch
WHERE  Name LIKE N'%Las Pi%as%' COLLATE Latin1_General_CI_AI;
-- Expected: 1 row — the ñ version only
GO

-- V2: No BDC orphans for the deleted branch
PRINT '--- V2: BDC orphan check ---';
SELECT bdc.BranchDeptCompanyID, bdc.BranchID
FROM   dbo.BranchDepartmentCompany bdc
WHERE  NOT EXISTS (SELECT 1 FROM dbo.Branch b WHERE b.BranchId = bdc.BranchID);
-- Expected: 0 rows
GO

-- V3: No employees still pointing at the old branch
PRINT '--- V3: Employee orphan check ---';
SELECT e.EmpId, e.Name, e.BranchId
FROM   dbo.Employee e
WHERE  NOT EXISTS (SELECT 1 FROM dbo.Branch b WHERE b.BranchId = e.BranchId);
-- Expected: 0 rows
GO

-- V4: Confirm no duplicate department links remain for LAS PIÑAS CENTER
PRINT '--- V4: Unique department links for LAS PIÑAS CENTER ---';
SELECT
    d.Name          AS DepartmentName,
    COUNT(*)        AS LinkCount
FROM       dbo.BranchDepartmentCompany bdc
INNER JOIN dbo.Department              d ON d.DeptId   = bdc.DepartmentID
INNER JOIN dbo.Branch                  b ON b.BranchId = bdc.BranchID
WHERE b.Name LIKE N'%Las Pi%as%' COLLATE Latin1_General_CI_AI
GROUP BY d.Name
HAVING COUNT(*) > 1;
-- Expected: 0 rows
GO
