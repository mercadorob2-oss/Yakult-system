-- Migration: Merge duplicate Branch rows (without ñ) into the correct ñ versions.
-- Keeps:   DASMARIÑAS CENTER, BIÑAN CENTER, PARAÑAQUE CENTER
-- Removes: Dasmarinas Center, Binan Center, Paranaque Center
-- All FK references in child tables are re-pointed to the ñ BranchId before deletion.
-- Each pair is processed independently — if one pair cannot be resolved it is skipped
-- and the remaining pairs continue.

BEGIN TRANSACTION;
BEGIN TRY

    -- ── Identify the duplicate pairs ─────────────────────────────────────────
    -- CI_AI finds both variants (ignores case & accent); CI_AS distinguishes ñ from n.
    DECLARE @DasmarinasOldId INT = (
        SELECT BranchId FROM dbo.Branch
        WHERE  Name LIKE N'%Dasmarinas Center%' COLLATE Latin1_General_CI_AI
          AND  Name NOT LIKE N'%ñ%'             COLLATE Latin1_General_CI_AS
    );
    DECLARE @DasmarinasNewId INT = (
        SELECT BranchId FROM dbo.Branch
        WHERE  Name LIKE N'%Dasmarinas Center%' COLLATE Latin1_General_CI_AI
          AND  Name LIKE N'%ñ%'                 COLLATE Latin1_General_CI_AS
    );

    DECLARE @BinanOldId INT = (
        SELECT BranchId FROM dbo.Branch
        WHERE  Name LIKE N'%Binan Center%' COLLATE Latin1_General_CI_AI
          AND  Name NOT LIKE N'%ñ%'        COLLATE Latin1_General_CI_AS
    );
    DECLARE @BinanNewId INT = (
        SELECT BranchId FROM dbo.Branch
        WHERE  Name LIKE N'%Binan Center%' COLLATE Latin1_General_CI_AI
          AND  Name LIKE N'%ñ%'            COLLATE Latin1_General_CI_AS
    );

    DECLARE @ParanaqueOldId INT = (
        SELECT BranchId FROM dbo.Branch
        WHERE  Name LIKE N'%Paranaque Center%' COLLATE Latin1_General_CI_AI
          AND  Name NOT LIKE N'%ñ%'            COLLATE Latin1_General_CI_AS
    );
    DECLARE @ParanaqueNewId INT = (
        SELECT BranchId FROM dbo.Branch
        WHERE  Name LIKE N'%Paranaque Center%' COLLATE Latin1_General_CI_AI
          AND  Name LIKE N'%ñ%'                COLLATE Latin1_General_CI_AS
    );

    -- ── Dasmarinas ────────────────────────────────────────────────────────────
    IF @DasmarinasOldId IS NOT NULL AND @DasmarinasNewId IS NOT NULL
    BEGIN
        PRINT 'Processing Dasmarinas pair (old=' + CAST(@DasmarinasOldId AS NVARCHAR) + ', keep=' + CAST(@DasmarinasNewId AS NVARCHAR) + ') ...';

        UPDATE dbo.Employee            SET BranchId        = @DasmarinasNewId WHERE BranchId        = @DasmarinasOldId;
        UPDATE dbo.CartridgeApproval   SET BranchId        = @DasmarinasNewId WHERE BranchId        = @DasmarinasOldId;
        UPDATE dbo.CartridgeMovement   SET BranchId        = @DasmarinasNewId WHERE BranchId        = @DasmarinasOldId;
        UPDATE dbo.EmptyCartridge      SET BranchId        = @DasmarinasNewId WHERE BranchId        = @DasmarinasOldId;
        UPDATE dbo.CallTicket          SET BranchId        = @DasmarinasNewId WHERE BranchId        = @DasmarinasOldId;
        UPDATE dbo.ItemAuditTrail      SET BranchId        = @DasmarinasNewId WHERE BranchId        = @DasmarinasOldId;
        UPDATE dbo.UnfulfilledCartridgeExchange SET BranchId = @DasmarinasNewId WHERE BranchId      = @DasmarinasOldId;
        UPDATE dbo.[Set]               SET CurrentBranchId = @DasmarinasNewId WHERE CurrentBranchId = @DasmarinasOldId;
        UPDATE dbo.SetTransfer         SET FromBranchId    = @DasmarinasNewId WHERE FromBranchId    = @DasmarinasOldId;
        UPDATE dbo.SetTransfer         SET ToBranchId      = @DasmarinasNewId WHERE ToBranchId      = @DasmarinasOldId;

        -- BranchDepartmentCompany: unique index — drop dupes first, then re-point
        DELETE bdc FROM dbo.BranchDepartmentCompany bdc
        WHERE  bdc.BranchID = @DasmarinasOldId
          AND  EXISTS (SELECT 1 FROM dbo.BranchDepartmentCompany x
                       WHERE  x.BranchID = @DasmarinasNewId AND x.CompanyID = bdc.CompanyID
                         AND  (x.DepartmentID = bdc.DepartmentID OR (x.DepartmentID IS NULL AND bdc.DepartmentID IS NULL)));
        UPDATE dbo.BranchDepartmentCompany SET BranchID = @DasmarinasNewId WHERE BranchID = @DasmarinasOldId;

        -- CallBranchSmtpProfileLink (PK = BranchId)
        INSERT INTO dbo.CallBranchSmtpProfileLink (BranchId, ProfileId, IsActive, UpdatedAt, UpdatedByUserId)
        SELECT @DasmarinasNewId, ProfileId, IsActive, UpdatedAt, UpdatedByUserId
        FROM   dbo.CallBranchSmtpProfileLink
        WHERE  BranchId = @DasmarinasOldId
          AND  NOT EXISTS (SELECT 1 FROM dbo.CallBranchSmtpProfileLink WHERE BranchId = @DasmarinasNewId);
        DELETE FROM dbo.CallBranchSmtpProfileLink WHERE BranchId = @DasmarinasOldId;

        -- CallBranchNotificationRecipient (PK = BranchId)
        INSERT INTO dbo.CallBranchNotificationRecipient (BranchId, RecipientEmails, EscalationEmails, IsActive, UpdatedAt, UpdatedByUserId)
        SELECT @DasmarinasNewId, RecipientEmails, EscalationEmails, IsActive, UpdatedAt, UpdatedByUserId
        FROM   dbo.CallBranchNotificationRecipient
        WHERE  BranchId = @DasmarinasOldId
          AND  NOT EXISTS (SELECT 1 FROM dbo.CallBranchNotificationRecipient WHERE BranchId = @DasmarinasNewId);
        DELETE FROM dbo.CallBranchNotificationRecipient WHERE BranchId = @DasmarinasOldId;

        DELETE FROM dbo.Branch WHERE BranchId = @DasmarinasOldId;
        PRINT 'Dasmarinas done. Removed BranchId ' + CAST(@DasmarinasOldId AS NVARCHAR) + '.';
    END
    ELSE
        PRINT 'Dasmarinas pair not found (already merged or does not exist) — skipped.';

    -- ── Binan ─────────────────────────────────────────────────────────────────
    IF @BinanOldId IS NOT NULL AND @BinanNewId IS NOT NULL
    BEGIN
        PRINT 'Processing Binan pair (old=' + CAST(@BinanOldId AS NVARCHAR) + ', keep=' + CAST(@BinanNewId AS NVARCHAR) + ') ...';

        UPDATE dbo.Employee            SET BranchId        = @BinanNewId WHERE BranchId        = @BinanOldId;
        UPDATE dbo.CartridgeApproval   SET BranchId        = @BinanNewId WHERE BranchId        = @BinanOldId;
        UPDATE dbo.CartridgeMovement   SET BranchId        = @BinanNewId WHERE BranchId        = @BinanOldId;
        UPDATE dbo.EmptyCartridge      SET BranchId        = @BinanNewId WHERE BranchId        = @BinanOldId;
        UPDATE dbo.CallTicket          SET BranchId        = @BinanNewId WHERE BranchId        = @BinanOldId;
        UPDATE dbo.ItemAuditTrail      SET BranchId        = @BinanNewId WHERE BranchId        = @BinanOldId;
        UPDATE dbo.UnfulfilledCartridgeExchange SET BranchId = @BinanNewId WHERE BranchId      = @BinanOldId;
        UPDATE dbo.[Set]               SET CurrentBranchId = @BinanNewId WHERE CurrentBranchId = @BinanOldId;
        UPDATE dbo.SetTransfer         SET FromBranchId    = @BinanNewId WHERE FromBranchId    = @BinanOldId;
        UPDATE dbo.SetTransfer         SET ToBranchId      = @BinanNewId WHERE ToBranchId      = @BinanOldId;

        DELETE bdc FROM dbo.BranchDepartmentCompany bdc
        WHERE  bdc.BranchID = @BinanOldId
          AND  EXISTS (SELECT 1 FROM dbo.BranchDepartmentCompany x
                       WHERE  x.BranchID = @BinanNewId AND x.CompanyID = bdc.CompanyID
                         AND  (x.DepartmentID = bdc.DepartmentID OR (x.DepartmentID IS NULL AND bdc.DepartmentID IS NULL)));
        UPDATE dbo.BranchDepartmentCompany SET BranchID = @BinanNewId WHERE BranchID = @BinanOldId;

        INSERT INTO dbo.CallBranchSmtpProfileLink (BranchId, ProfileId, IsActive, UpdatedAt, UpdatedByUserId)
        SELECT @BinanNewId, ProfileId, IsActive, UpdatedAt, UpdatedByUserId
        FROM   dbo.CallBranchSmtpProfileLink
        WHERE  BranchId = @BinanOldId
          AND  NOT EXISTS (SELECT 1 FROM dbo.CallBranchSmtpProfileLink WHERE BranchId = @BinanNewId);
        DELETE FROM dbo.CallBranchSmtpProfileLink WHERE BranchId = @BinanOldId;

        INSERT INTO dbo.CallBranchNotificationRecipient (BranchId, RecipientEmails, EscalationEmails, IsActive, UpdatedAt, UpdatedByUserId)
        SELECT @BinanNewId, RecipientEmails, EscalationEmails, IsActive, UpdatedAt, UpdatedByUserId
        FROM   dbo.CallBranchNotificationRecipient
        WHERE  BranchId = @BinanOldId
          AND  NOT EXISTS (SELECT 1 FROM dbo.CallBranchNotificationRecipient WHERE BranchId = @BinanNewId);
        DELETE FROM dbo.CallBranchNotificationRecipient WHERE BranchId = @BinanOldId;

        DELETE FROM dbo.Branch WHERE BranchId = @BinanOldId;
        PRINT 'Binan done. Removed BranchId ' + CAST(@BinanOldId AS NVARCHAR) + '.';
    END
    ELSE
        PRINT 'Binan pair not found (already merged or does not exist) — skipped.';

    -- ── Paranaque ─────────────────────────────────────────────────────────────
    IF @ParanaqueOldId IS NOT NULL AND @ParanaqueNewId IS NOT NULL
    BEGIN
        PRINT 'Processing Paranaque pair (old=' + CAST(@ParanaqueOldId AS NVARCHAR) + ', keep=' + CAST(@ParanaqueNewId AS NVARCHAR) + ') ...';

        UPDATE dbo.Employee            SET BranchId        = @ParanaqueNewId WHERE BranchId        = @ParanaqueOldId;
        UPDATE dbo.CartridgeApproval   SET BranchId        = @ParanaqueNewId WHERE BranchId        = @ParanaqueOldId;
        UPDATE dbo.CartridgeMovement   SET BranchId        = @ParanaqueNewId WHERE BranchId        = @ParanaqueOldId;
        UPDATE dbo.EmptyCartridge      SET BranchId        = @ParanaqueNewId WHERE BranchId        = @ParanaqueOldId;
        UPDATE dbo.CallTicket          SET BranchId        = @ParanaqueNewId WHERE BranchId        = @ParanaqueOldId;
        UPDATE dbo.ItemAuditTrail      SET BranchId        = @ParanaqueNewId WHERE BranchId        = @ParanaqueOldId;
        UPDATE dbo.UnfulfilledCartridgeExchange SET BranchId = @ParanaqueNewId WHERE BranchId      = @ParanaqueOldId;
        UPDATE dbo.[Set]               SET CurrentBranchId = @ParanaqueNewId WHERE CurrentBranchId = @ParanaqueOldId;
        UPDATE dbo.SetTransfer         SET FromBranchId    = @ParanaqueNewId WHERE FromBranchId    = @ParanaqueOldId;
        UPDATE dbo.SetTransfer         SET ToBranchId      = @ParanaqueNewId WHERE ToBranchId      = @ParanaqueOldId;

        DELETE bdc FROM dbo.BranchDepartmentCompany bdc
        WHERE  bdc.BranchID = @ParanaqueOldId
          AND  EXISTS (SELECT 1 FROM dbo.BranchDepartmentCompany x
                       WHERE  x.BranchID = @ParanaqueNewId AND x.CompanyID = bdc.CompanyID
                         AND  (x.DepartmentID = bdc.DepartmentID OR (x.DepartmentID IS NULL AND bdc.DepartmentID IS NULL)));
        UPDATE dbo.BranchDepartmentCompany SET BranchID = @ParanaqueNewId WHERE BranchID = @ParanaqueOldId;

        INSERT INTO dbo.CallBranchSmtpProfileLink (BranchId, ProfileId, IsActive, UpdatedAt, UpdatedByUserId)
        SELECT @ParanaqueNewId, ProfileId, IsActive, UpdatedAt, UpdatedByUserId
        FROM   dbo.CallBranchSmtpProfileLink
        WHERE  BranchId = @ParanaqueOldId
          AND  NOT EXISTS (SELECT 1 FROM dbo.CallBranchSmtpProfileLink WHERE BranchId = @ParanaqueNewId);
        DELETE FROM dbo.CallBranchSmtpProfileLink WHERE BranchId = @ParanaqueOldId;

        INSERT INTO dbo.CallBranchNotificationRecipient (BranchId, RecipientEmails, EscalationEmails, IsActive, UpdatedAt, UpdatedByUserId)
        SELECT @ParanaqueNewId, RecipientEmails, EscalationEmails, IsActive, UpdatedAt, UpdatedByUserId
        FROM   dbo.CallBranchNotificationRecipient
        WHERE  BranchId = @ParanaqueOldId
          AND  NOT EXISTS (SELECT 1 FROM dbo.CallBranchNotificationRecipient WHERE BranchId = @ParanaqueNewId);
        DELETE FROM dbo.CallBranchNotificationRecipient WHERE BranchId = @ParanaqueOldId;

        DELETE FROM dbo.Branch WHERE BranchId = @ParanaqueOldId;
        PRINT 'Paranaque done. Removed BranchId ' + CAST(@ParanaqueOldId AS NVARCHAR) + '.';
    END
    ELSE
        PRINT 'Paranaque pair not found (already merged or does not exist) — skipped.';

    COMMIT TRANSACTION;
    PRINT 'Migration complete.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @Msg NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR(@Msg, 16, 1);
END CATCH;
