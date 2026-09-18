-- Migration_RepairPortal_MobileLifecycle_StoredProcs.sql
--
-- Manual-install prerequisite for the Scanner Repair Portal's technician-only lifecycle actions.
-- This script is intentionally NOT executed by the application or this change set.
--
-- Run only after the existing Repair Portal schema, part workflow, conclusion disposition,
-- BorrowLog RepairTicketId link, Item.IsBorrowable, and SkipConditionReset migrations.  It adds
-- no tables and changes no existing data at install time; it only creates or updates procedures.
--
-- These procedures intentionally centralize the multi-table operations that the desktop code
-- historically orchestrates across repositories: a temporary spare loan in dbo.BorrowLog and an
-- unrepairable Discard/Replace disposition spanning Item lifecycle/audit, Request, Set, and
-- RepairConclusion.  Mobile callers never send actor identity; the API supplies a JWT-derived
-- @ChangedByUserId / @DecidedByUserId.

IF OBJECT_ID('dbo.RepairTicket', 'U') IS NULL
    THROW 51300, 'Repair Portal schema is not installed (dbo.RepairTicket is missing).', 1;
IF OBJECT_ID('dbo.RepairConclusion', 'U') IS NULL
    THROW 51301, 'Repair Portal conclusion schema is not installed.', 1;
IF OBJECT_ID('dbo.BorrowLog', 'U') IS NULL OR COL_LENGTH('dbo.BorrowLog', 'RepairTicketId') IS NULL
    THROW 51302, 'BorrowLog RepairTicketId migration is not installed.', 1;
IF COL_LENGTH('dbo.Item', 'IsBorrowable') IS NULL
    THROW 51303, 'Item.IsBorrowable migration is not installed.', 1;
GO

-- ── Temporary spare assignment ───────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_AssignSpare
    @RepairTicketId     INT,
    @SpareItemId        INT,
    @ChangedByUserId    INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @RepairTicketId IS NULL OR @RepairTicketId <= 0 THROW 51310, 'RepairTicketId is required.', 1;
    IF @SpareItemId IS NULL OR @SpareItemId <= 0 THROW 51311, 'SpareItemId is required.', 1;
    IF @ChangedByUserId IS NULL OR @ChangedByUserId <= 0 THROW 51312, 'ChangedByUserId is required.', 1;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @TicketCode NVARCHAR(50), @RequestedByType NVARCHAR(20), @RequestedByEmpId INT, @RequestedByDeptId INT;
        SELECT
            @TicketCode = TicketCode,
            @RequestedByType = RequestedByType,
            @RequestedByEmpId = RequestedByEmpId,
            @RequestedByDeptId = RequestedByDeptId
        FROM dbo.RepairTicket WITH (UPDLOCK, HOLDLOCK)
        WHERE RepairTicketId = @RepairTicketId;

        IF @@ROWCOUNT = 0 THROW 51313, 'Repair ticket not found.', 1;

        DECLARE @ItemId INT, @SerialNumber VARCHAR(255), @ItemName NVARCHAR(200), @ItemDescription NVARCHAR(2000), @ModelNumber NVARCHAR(200);
        SELECT
            @ItemId = ItemId,
            @SerialNumber = SerialNumber,
            @ItemName = Name,
            @ItemDescription = Description,
            @ModelNumber = ModelNumber
        FROM dbo.Item WITH (UPDLOCK, HOLDLOCK)
        WHERE ItemId = @SpareItemId
          AND ISNULL(Active, 1) = 1
          AND ISNULL(IsBorrowable, 0) = 1;

        IF @ItemId IS NULL THROW 51314, 'The selected spare is unavailable or is not marked borrowable.', 1;
        IF NULLIF(LTRIM(RTRIM(@SerialNumber)), '') IS NULL THROW 51315, 'A borrowable spare must have a serial number.', 1;

        IF EXISTS (SELECT 1 FROM dbo.BorrowLog WITH (UPDLOCK, HOLDLOCK) WHERE ItemId = @SpareItemId AND ReturnedAtUtc IS NULL)
            THROW 51316, 'The selected spare is already borrowed.', 1;

        -- A temporary spare cannot simultaneously be assigned to an active inventory Set.
        IF EXISTS (
            SELECT 1
            FROM dbo.SetItem si
            INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
            LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
            WHERE si.ItemId = @SpareItemId AND ISNULL(s.Active, 1) = 1 AND archS.EntityId IS NULL
            UNION ALL
            SELECT 1
            FROM dbo.Request r
            INNER JOIN dbo.[Set] s ON s.SetId = r.SetId
            LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
            WHERE r.ItemId = @SpareItemId AND ISNULL(r.Active, 1) = 1
              AND r.SetId IS NOT NULL AND ISNULL(s.Active, 1) = 1 AND archS.EntityId IS NULL
        )
            THROW 51317, 'The selected spare is assigned to an active Set.', 1;

        DECLARE @BorrowedByEmpId INT = NULL, @BorrowedByEmpName NVARCHAR(200), @BorrowedByDeptId INT = NULL, @BorrowedByDeptName NVARCHAR(200);
        IF UPPER(LTRIM(RTRIM(ISNULL(@RequestedByType, '')))) = 'DEPARTMENT'
        BEGIN
            IF @RequestedByDeptId IS NULL THROW 51318, 'Set Requested By before assigning a spare.', 1;
            SELECT @BorrowedByDeptName = Name FROM dbo.Department WHERE DeptId = @RequestedByDeptId AND ISNULL(Active, 1) = 1;
            IF @BorrowedByDeptName IS NULL THROW 51319, 'The ticket requester department is unavailable.', 1;
            SET @BorrowedByDeptId = @RequestedByDeptId;
            SET @BorrowedByEmpName = @BorrowedByDeptName + N' (no specific employee)';
        END
        ELSE
        BEGIN
            IF @RequestedByEmpId IS NULL THROW 51320, 'Set Requested By before assigning a spare.', 1;
            SELECT
                @BorrowedByEmpId = EmpId,
                @BorrowedByEmpName = Name,
                @BorrowedByDeptId = DeptId
            FROM dbo.Employee
            WHERE EmpId = @RequestedByEmpId AND ISNULL(Active, 1) = 1;
            IF @BorrowedByEmpId IS NULL THROW 51321, 'The ticket requester employee is unavailable.', 1;
            SELECT @BorrowedByDeptName = Name FROM dbo.Department WHERE DeptId = @BorrowedByDeptId;
        END

        DECLARE @EncoderName NVARCHAR(200);
        SELECT @EncoderName = Name FROM dbo.[User] WHERE UserId = @ChangedByUserId;
        SET @EncoderName = COALESCE(NULLIF(LTRIM(RTRIM(@EncoderName)), ''), CONVERT(NVARCHAR(20), @ChangedByUserId));

        INSERT dbo.BorrowLog
        (
            ItemId, SerialNumber, ItemName, ItemDescription, ModelNumber,
            BorrowedByEmpId, BorrowedByEmpName, BorrowedByDeptId, BorrowedByDeptName,
            BorrowEncodedByUserId, BorrowEncodedByUserName, BorrowedAtUtc, RepairTicketId
        )
        VALUES
        (
            @ItemId, @SerialNumber, COALESCE(NULLIF(@ItemName, ''), @SerialNumber), @ItemDescription, @ModelNumber,
            @BorrowedByEmpId, @BorrowedByEmpName, @BorrowedByDeptId, @BorrowedByDeptName,
            @ChangedByUserId, @EncoderName, SYSUTCDATETIME(), @RepairTicketId
        );

        DECLARE @BorrowId INT = CONVERT(INT, SCOPE_IDENTITY());
        INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
        VALUES (@RepairTicketId, @ChangedByUserId, 'SpareAssigned', NULL, @SerialNumber, N'Spare assigned via Repair Ticket ' + COALESCE(@TicketCode, CONVERT(NVARCHAR(20), @RepairTicketId)));

        COMMIT;

        SELECT
            @BorrowId AS BorrowId,
            @ItemId AS ItemId,
            @SerialNumber AS SerialNumber,
            @ItemName AS ItemName,
            @ModelNumber AS ModelNumber,
            @BorrowedByEmpName AS BorrowerName,
            @BorrowedByDeptName AS BorrowerDepartmentName;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH
END
GO

-- ── Temporary spare return / unlink ──────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_ReturnSpare
    @RepairTicketId     INT,
    @ChangedByUserId    INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @RepairTicketId IS NULL OR @RepairTicketId <= 0 THROW 51330, 'RepairTicketId is required.', 1;
    IF @ChangedByUserId IS NULL OR @ChangedByUserId <= 0 THROW 51331, 'ChangedByUserId is required.', 1;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @TicketCode NVARCHAR(50);
        SELECT @TicketCode = TicketCode
        FROM dbo.RepairTicket WITH (UPDLOCK, HOLDLOCK)
        WHERE RepairTicketId = @RepairTicketId;
        IF @@ROWCOUNT = 0 THROW 51332, 'Repair ticket not found.', 1;

        DECLARE @BorrowId INT, @SerialNumber VARCHAR(255), @BorrowedByEmpId INT, @BorrowedByEmpName NVARCHAR(200), @BorrowedByDeptId INT, @BorrowedByDeptName NVARCHAR(200);
        SELECT TOP (1)
            @BorrowId = BorrowId,
            @SerialNumber = SerialNumber,
            @BorrowedByEmpId = BorrowedByEmpId,
            @BorrowedByEmpName = BorrowedByEmpName,
            @BorrowedByDeptId = BorrowedByDeptId,
            @BorrowedByDeptName = BorrowedByDeptName
        FROM dbo.BorrowLog WITH (UPDLOCK, HOLDLOCK)
        WHERE RepairTicketId = @RepairTicketId AND ReturnedAtUtc IS NULL
        ORDER BY BorrowedAtUtc DESC, BorrowId DESC;

        IF @BorrowId IS NULL
        BEGIN
            COMMIT;
            SELECT CAST(0 AS bit) AS Returned, CAST(NULL AS int) AS BorrowId;
            RETURN;
        END

        DECLARE @EncoderName NVARCHAR(200);
        SELECT @EncoderName = Name FROM dbo.[User] WHERE UserId = @ChangedByUserId;
        SET @EncoderName = COALESCE(NULLIF(LTRIM(RTRIM(@EncoderName)), ''), CONVERT(NVARCHAR(20), @ChangedByUserId));

        UPDATE dbo.BorrowLog
        SET
            ReturnedByEmpId = @BorrowedByEmpId,
            ReturnedByEmpName = @BorrowedByEmpName,
            ReturnedByDeptId = @BorrowedByDeptId,
            ReturnedByDeptName = @BorrowedByDeptName,
            ReturnEncodedByUserId = @ChangedByUserId,
            ReturnEncodedByUserName = @EncoderName,
            ReturnedAtUtc = SYSUTCDATETIME()
        WHERE BorrowId = @BorrowId AND ReturnedAtUtc IS NULL;

        IF @@ROWCOUNT = 0 THROW 51333, 'The spare was returned by another technician. Refresh the ticket.', 1;

        INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
        VALUES (@RepairTicketId, @ChangedByUserId, 'SpareReturned', @SerialNumber, NULL, N'Spare returned via Repair Ticket ' + COALESCE(@TicketCode, CONVERT(NVARCHAR(20), @RepairTicketId)));

        COMMIT;
        SELECT CAST(1 AS bit) AS Returned, @BorrowId AS BorrowId, @SerialNumber AS SerialNumber;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH
END
GO

-- ── Atomic unrepairable Discard / Replace lifecycle ──────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_SetDispositionAtomic
    @RepairTicketId       INT,
    @Disposition          NVARCHAR(20) = NULL, -- NULL means Unlink / clear
    @ReplacementItemId    INT = NULL,
    @DecidedByUserId      INT,
    @DecidedByDisplayName NVARCHAR(200) = NULL,
    @Note                 NVARCHAR(2000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Disposition = NULLIF(LTRIM(RTRIM(@Disposition)), '');
    IF @Disposition IS NOT NULL AND @Disposition NOT IN ('Discard', 'Replace')
        THROW 51350, 'Disposition must be Discard, Replace, or NULL to unlink.', 1;
    IF @RepairTicketId IS NULL OR @RepairTicketId <= 0 THROW 51351, 'RepairTicketId is required.', 1;
    IF @DecidedByUserId IS NULL OR @DecidedByUserId <= 0 THROW 51352, 'DecidedByUserId is required.', 1;
    IF @Disposition = 'Replace' AND (@ReplacementItemId IS NULL OR @ReplacementItemId <= 0)
        THROW 51353, 'ReplacementItemId is required for Replace.', 1;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE
            @TicketItemId INT,
            @TicketCode NVARCHAR(50),
            @TicketStatus NVARCHAR(20),
            @RequestedByType NVARCHAR(20),
            @RequestedByComId INT,
            @RequestedByBranchId INT,
            @RequestedByDeptId INT,
            @RequestedByEmpId INT,
            @OldDisposition NVARCHAR(20),
            @PriorDispositionItemId INT,
            @PriorReplacementItemId INT,
            @PriorReplacementRequestId INT,
            @PriorReplacementSetId INT,
            @PriorExecutedAt DATETIME2;

        SELECT
            @TicketItemId = ItemId,
            @TicketCode = TicketCode,
            @TicketStatus = Status,
            @RequestedByType = RequestedByType,
            @RequestedByComId = RequestedByComId,
            @RequestedByBranchId = RequestedByBranchId,
            @RequestedByDeptId = RequestedByDeptId,
            @RequestedByEmpId = RequestedByEmpId
        FROM dbo.RepairTicket WITH (UPDLOCK, HOLDLOCK)
        WHERE RepairTicketId = @RepairTicketId;
        IF @@ROWCOUNT = 0 THROW 51354, 'Repair ticket not found.', 1;

        SELECT
            @OldDisposition = Disposition,
            @PriorDispositionItemId = DispositionItemId,
            @PriorReplacementItemId = ReplacementItemId,
            @PriorReplacementRequestId = ReplacementRequestId,
            @PriorReplacementSetId = ReplacementSetId,
            @PriorExecutedAt = DispositionExecutedAt
        FROM dbo.RepairConclusion WITH (UPDLOCK, HOLDLOCK)
        WHERE RepairTicketId = @RepairTicketId;

        -- Repeating the exact same executed choice is idempotent.
        IF @Disposition IS NOT NULL
           AND @PriorExecutedAt IS NOT NULL
           AND @OldDisposition = @Disposition
           AND (@Disposition = 'Discard' OR @PriorReplacementItemId = @ReplacementItemId)
        BEGIN
            COMMIT;
            SELECT @RepairTicketId AS RepairTicketId, @TicketStatus AS Status, @OldDisposition AS Disposition,
                   @PriorReplacementItemId AS ReplacementItemId, @PriorReplacementRequestId AS ReplacementRequestId,
                   @PriorReplacementSetId AS ReplacementSetId;
            RETURN;
        END

        -- First undo an old executed choice.  This is deliberately additive audit reversal rather
        -- than deleting prior lifecycle/audit rows, matching ItemLifecycleDecisionRepository.
        IF @PriorExecutedAt IS NOT NULL AND @PriorDispositionItemId IS NOT NULL
        BEGIN
            DECLARE @PriorDecisionId INT, @PriorQuantity INT, @PriorConditionId INT, @PriorSerialNumber NVARCHAR(100);
            SELECT TOP (1)
                @PriorDecisionId = d.DecisionId,
                @PriorQuantity = d.Quantity,
                @PriorConditionId = d.ConditionId
            FROM dbo.ItemLifecycleDecision d WITH (UPDLOCK, HOLDLOCK)
            WHERE d.ItemId = @PriorDispositionItemId AND d.DecisionStatus = 'Executed'
            ORDER BY d.DecisionId DESC;

            IF @PriorDecisionId IS NOT NULL
            BEGIN
                SELECT @PriorSerialNumber = SerialNumber FROM dbo.Item WITH (UPDLOCK, HOLDLOCK) WHERE ItemId = @PriorDispositionItemId;
                UPDATE dbo.ItemLifecycleDecision SET DecisionStatus = 'Cancelled' WHERE DecisionId = @PriorDecisionId;
                UPDATE dbo.Item
                SET StockOnHand = ISNULL(StockOnHand, 0) + ISNULL(@PriorQuantity, 1),
                    Active = 1,
                    DateModified = SYSUTCDATETIME(),
                    ModifiedBy = @DecidedByUserId
                WHERE ItemId = @PriorDispositionItemId;
                UPDATE dbo.ArchiveStatus
                SET IsArchived = 0, RestoredAt = SYSUTCDATETIME(), RestoredBy = COALESCE(NULLIF(@DecidedByDisplayName, ''), CONVERT(NVARCHAR(20), @DecidedByUserId))
                WHERE EntityType = 'Item' AND EntityId = @PriorDispositionItemId;
                INSERT dbo.Inventory (ItemId, EntryType, Quantity, DatePosted, PostedBy, ReqId, Description, ConditionID, Active)
                VALUES (@PriorDispositionItemId, 'Positive', ISNULL(@PriorQuantity, 1), SYSUTCDATETIME(), @DecidedByUserId, NULL,
                        N'Disposal reversed (Repair Ticket ' + COALESCE(@TicketCode, CONVERT(NVARCHAR(20), @RepairTicketId)) + N')', @PriorConditionId, 1);
                INSERT dbo.ItemAuditTrail
                    (ItemId, SerialNumber, Action, ActionTime, EmployeeId, EmployeeName, DepartmentId, DepartmentName, BranchId, BranchName,
                     Direction, Status, ReferenceType, ReferenceId, Notes, CreatedBy)
                VALUES
                    (@PriorDispositionItemId, @PriorSerialNumber, 'Disposal Reversed', SYSDATETIME(), @DecidedByUserId, @DecidedByDisplayName,
                     NULL, NULL, NULL, NULL, 'IN', 'Completed', 'RepairTicket', @RepairTicketId,
                     N'Disposition reversed via Repair Ticket ' + COALESCE(@TicketCode, CONVERT(NVARCHAR(20), @RepairTicketId)),
                     COALESCE(NULLIF(@DecidedByDisplayName, ''), N'System'));
            END
        END

        -- Undo a replacement Request/Set generated by a prior Replace choice before deleting the
        -- Request itself.  The stock restoration and child cleanup mirror SetRepository's desktop
        -- DeleteSetAndRestoreStock + RequestRepository.DeleteRequest sequence.
        IF @PriorReplacementSetId IS NOT NULL
        BEGIN
            DECLARE @Restored TABLE (ItemId INT NOT NULL, Quantity INT NOT NULL);
            INSERT @Restored (ItemId, Quantity)
            SELECT si.ItemId, si.Quantity
            FROM dbo.SetItem si
            INNER JOIN dbo.Item i ON i.ItemId = si.ItemId
            WHERE si.SetId = @PriorReplacementSetId AND ISNULL(i.AffectsInventory, 0) = 1;

            UPDATE i
            SET StockOnHand = ISNULL(i.StockOnHand, 0) + r.Quantity,
                DateModified = SYSUTCDATETIME(),
                ModifiedBy = @DecidedByUserId
            FROM dbo.Item i
            INNER JOIN @Restored r ON r.ItemId = i.ItemId;

            UPDATE dbo.Request SET SetId = NULL WHERE SetId = @PriorReplacementSetId;
            DELETE FROM dbo.SetItem WHERE SetId = @PriorReplacementSetId;
            DELETE FROM dbo.Inventory WHERE SetId = @PriorReplacementSetId;
            DELETE FROM dbo.[Set] WHERE SetId = @PriorReplacementSetId;
        END
        IF @PriorReplacementRequestId IS NOT NULL
        BEGIN
            DELETE FROM dbo.Inventory WHERE ReqId = @PriorReplacementRequestId;
            DELETE FROM dbo.ArchiveStatus WHERE EntityType = 'Request' AND EntityId = @PriorReplacementRequestId;
            DELETE FROM dbo.Request WHERE ReqId = @PriorReplacementRequestId;
        END

        -- Clear / Unlink stops after reversal.  Match desktop behavior by returning an auto-
        -- completed disposition ticket to Unrepairable so it again needs a repair decision.
        IF @Disposition IS NULL
        BEGIN
            IF @PriorExecutedAt IS NOT NULL
            BEGIN
                UPDATE dbo.RepairConclusion
                SET Disposition = NULL,
                    DispositionItemId = NULL,
                    DispositionDecidedByUserId = @DecidedByUserId,
                    DispositionDecidedAt = SYSUTCDATETIME(),
                    DispositionExecutedAt = NULL,
                    ReplacementItemId = NULL,
                    ReplacementRequestId = NULL,
                    ReplacementSetId = NULL,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE RepairTicketId = @RepairTicketId;

                INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
                VALUES (@RepairTicketId, @DecidedByUserId, 'Disposition', @OldDisposition, NULL, @Note);

                IF @TicketStatus = 'Completed'
                BEGIN
                    UPDATE dbo.RepairTicket SET Status = 'Unrepairable', CompletedAt = NULL WHERE RepairTicketId = @RepairTicketId;
                    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
                    VALUES (@RepairTicketId, @DecidedByUserId, 'Status', 'Completed', 'Unrepairable', N'Reverted: disposition unlinked.');
                    SET @TicketStatus = 'Unrepairable';
                END
            END

            COMMIT;
            SELECT @RepairTicketId AS RepairTicketId, @TicketStatus AS Status, CAST(NULL AS NVARCHAR(20)) AS Disposition,
                   CAST(NULL AS INT) AS ReplacementItemId, CAST(NULL AS INT) AS ReplacementRequestId, CAST(NULL AS INT) AS ReplacementSetId;
            RETURN;
        END

        -- Execute the new disposal decision for the broken physical item.
        DECLARE @StockOnHand INT = 0, @ConditionId INT = NULL, @SerialNumber NVARCHAR(100) = NULL;
        SELECT @StockOnHand = ISNULL(StockOnHand, 0), @ConditionId = ConditionId, @SerialNumber = SerialNumber
        FROM dbo.Item WITH (UPDLOCK, HOLDLOCK)
        WHERE ItemId = @TicketItemId;
        IF @@ROWCOUNT = 0 THROW 51355, 'The repair ticket item no longer exists.', 1;
        IF EXISTS (SELECT 1 FROM dbo.ArchiveStatus WHERE EntityType = 'Item' AND EntityId = @TicketItemId AND IsArchived = 1)
            THROW 51356, 'The repair ticket item is already archived.', 1;
        IF EXISTS (SELECT 1 FROM dbo.ItemLifecycleDecision WHERE ItemId = @TicketItemId AND DecisionStatus = 'Executed')
            THROW 51357, 'The repair ticket item already has an executed lifecycle decision.', 1;

        DECLARE @DecisionTypeId INT;
        SELECT TOP (1) @DecisionTypeId = DecisionTypeId FROM dbo.ItemDecisionType WHERE DecisionTypeName = 'DISPOSE';
        IF @DecisionTypeId IS NULL THROW 51358, 'The DISPOSE item decision type is not configured.', 1;

        INSERT dbo.ItemInspectionLog (ItemId, ConditionId, InspectedAt, InspectedBy, Recommendation, Notes)
        VALUES (@TicketItemId, @ConditionId, SYSUTCDATETIME(), @DecidedByUserId, 'DISPOSE',
                @Disposition + N' via Repair Ticket ' + COALESCE(@TicketCode, CONVERT(NVARCHAR(20), @RepairTicketId)));

        INSERT dbo.ItemLifecycleDecision
            (ItemId, DecisionTypeId, ConditionId, Quantity, DecisionStatus, DecidedAt, DecidedBy, RecipientName, SaleAmount, Remarks)
        VALUES
            (@TicketItemId, @DecisionTypeId, @ConditionId, 1, 'Executed', SYSUTCDATETIME(), @DecidedByUserId, NULL, NULL,
             @Disposition + N' via Repair Ticket ' + COALESCE(@TicketCode, CONVERT(NVARCHAR(20), @RepairTicketId)) + N' — Unrepairable');

        INSERT dbo.Inventory (ItemId, EntryType, Quantity, DatePosted, PostedBy, ReqId, Description, ConditionID, Active)
        VALUES (@TicketItemId, 'Negative', 1, SYSUTCDATETIME(), @DecidedByUserId, NULL,
                N'Item disposed via Repair Ticket ' + COALESCE(@TicketCode, CONVERT(NVARCHAR(20), @RepairTicketId)), @ConditionId, 1);

        UPDATE dbo.Item
        SET StockOnHand = CASE WHEN ISNULL(StockOnHand, 0) <= 0 THEN 0 ELSE ISNULL(StockOnHand, 0) - 1 END,
            Active = 0,
            DateModified = SYSUTCDATETIME(),
            ModifiedBy = @DecidedByUserId
        WHERE ItemId = @TicketItemId;

        MERGE dbo.ArchiveStatus AS target
        USING (SELECT 'Item' AS EntityType, @TicketItemId AS EntityId) AS source
            ON target.EntityType = source.EntityType AND target.EntityId = source.EntityId
        WHEN MATCHED THEN UPDATE SET IsArchived = 1, ArchivedAt = SYSUTCDATETIME(),
            ArchivedBy = COALESCE(NULLIF(@DecidedByDisplayName, ''), CONVERT(NVARCHAR(20), @DecidedByUserId)),
            ArchiveReason = 'Disposed', RestoredAt = NULL, RestoredBy = NULL
        WHEN NOT MATCHED THEN INSERT (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
            VALUES ('Item', @TicketItemId, 1, SYSUTCDATETIME(),
                    COALESCE(NULLIF(@DecidedByDisplayName, ''), CONVERT(NVARCHAR(20), @DecidedByUserId)), 'Disposed');

        INSERT dbo.ItemAuditTrail
            (ItemId, SerialNumber, Action, ActionTime, EmployeeId, EmployeeName, DepartmentId, DepartmentName, BranchId, BranchName,
             Direction, Status, ReferenceType, ReferenceId, Notes, CreatedBy)
        VALUES
            (@TicketItemId, @SerialNumber, 'Item Disposed', SYSDATETIME(), @DecidedByUserId, @DecidedByDisplayName,
             NULL, NULL, NULL, NULL, 'OUT', 'Completed', 'RepairTicket', @RepairTicketId,
             @Disposition + N' via Repair Ticket ' + COALESCE(@TicketCode, CONVERT(NVARCHAR(20), @RepairTicketId)),
             COALESCE(NULLIF(@DecidedByDisplayName, ''), N'System'));

        DECLARE @NewRequestId INT = NULL, @NewSetId INT = NULL;
        IF @Disposition = 'Replace'
        BEGIN
            IF @ReplacementItemId = @TicketItemId THROW 51359, 'The replacement item must differ from the disposed item.', 1;
            IF NOT EXISTS (SELECT 1 FROM dbo.Item WITH (UPDLOCK, HOLDLOCK) WHERE ItemId = @ReplacementItemId AND ISNULL(Active, 1) = 1 AND ISNULL(StockOnHand, 0) > 0)
                THROW 51360, 'The selected replacement item is unavailable.', 1;

            INSERT dbo.Request
            (
                DateRequested, Description, Remarks, Status, EntryType, Quantity, IssuedQty,
                DateCreated, CreatedBy, DateModified, ModifiedBy, ItemId, EmpId, ComId, DeptId, BranchId,
                SubmissionSessionId, ConditionID, RequestSource, ReceivedById, WorkflowType
            )
            VALUES
            (
                SYSUTCDATETIME(), N'Replacement for Repair Ticket ' + COALESCE(@TicketCode, CONVERT(NVARCHAR(20), @RepairTicketId)), NULL,
                'Submitted', 'Negative', 1, 1,
                SYSUTCDATETIME(), @DecidedByUserId, SYSUTCDATETIME(), @DecidedByUserId, @ReplacementItemId,
                CASE WHEN UPPER(LTRIM(RTRIM(ISNULL(@RequestedByType, '')))) = 'DEPARTMENT' THEN NULL ELSE @RequestedByEmpId END,
                @RequestedByComId, @RequestedByDeptId, @RequestedByBranchId,
                NULL, NULL, 'REPAIR_PORTAL', NULL, NULL
            );
            SET @NewRequestId = CONVERT(INT, SCOPE_IDENTITY());

            INSERT dbo.[Set] (CreatedBy, CreatedAt, QRToken, Remarks, DispatchDate, Status)
            VALUES (@DecidedByUserId, GETDATE(), NEWID(), N'Replacement for Repair Ticket ' + COALESCE(@TicketCode, CONVERT(NVARCHAR(20), @RepairTicketId)), NULL, 'Dispatched');
            SET @NewSetId = CONVERT(INT, SCOPE_IDENTITY());

            UPDATE dbo.Request SET SetId = @NewSetId WHERE ReqId = @NewRequestId;
            UPDATE s
            SET s.ReqId = @NewRequestId,
                s.SetType = ISNULL(i.ItemType, 'Hardware')
            FROM dbo.[Set] s
            INNER JOIN dbo.Request r ON r.ReqId = @NewRequestId
            INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
            WHERE s.SetId = @NewSetId AND s.ReqId IS NULL;

            INSERT dbo.SetItem
                (SetId, ItemId, ItemCode, Description, Quantity, UnitOfMeasure, UnitPrice, Amount, LineStartDate, LineEndDate, CreatedBy)
            SELECT r.SetId, r.ItemId, i.ModelNumber, i.Name, r.Quantity, i.UnitOfMeasure, r.UnitPrice,
                   r.Quantity * r.UnitPrice, r.DateRequested, NULL, r.CreatedBy
            FROM dbo.Request r
            INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
            WHERE r.ReqId = @NewRequestId
              AND NOT EXISTS (SELECT 1 FROM dbo.SetItem si WHERE si.SetId = r.SetId AND si.ItemId = r.ItemId);
        END

        IF EXISTS (SELECT 1 FROM dbo.RepairConclusion WHERE RepairTicketId = @RepairTicketId)
        BEGIN
            UPDATE dbo.RepairConclusion
            SET Disposition = @Disposition,
                DispositionItemId = @TicketItemId,
                DispositionDecidedByUserId = @DecidedByUserId,
                DispositionDecidedAt = SYSUTCDATETIME(),
                DispositionExecutedAt = SYSUTCDATETIME(),
                ReplacementItemId = @ReplacementItemId,
                ReplacementRequestId = @NewRequestId,
                ReplacementSetId = @NewSetId,
                UpdatedAt = SYSUTCDATETIME()
            WHERE RepairTicketId = @RepairTicketId;
        END
        ELSE
        BEGIN
            INSERT dbo.RepairConclusion
                (RepairTicketId, Disposition, DispositionItemId, DispositionDecidedByUserId, DispositionDecidedAt,
                 DispositionExecutedAt, ReplacementItemId, ReplacementRequestId, ReplacementSetId)
            VALUES
                (@RepairTicketId, @Disposition, @TicketItemId, @DecidedByUserId, SYSUTCDATETIME(), SYSUTCDATETIME(),
                 @ReplacementItemId, @NewRequestId, @NewSetId);
        END

        INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
        VALUES (@RepairTicketId, @DecidedByUserId, 'Disposition', @OldDisposition, @Disposition, @Note);

        IF @TicketStatus <> 'Completed'
        BEGIN
            UPDATE dbo.RepairTicket
            SET Status = 'Completed', CompletedAt = COALESCE(CompletedAt, SYSUTCDATETIME())
            WHERE RepairTicketId = @RepairTicketId;
            INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
            VALUES (@RepairTicketId, @DecidedByUserId, 'Status', @TicketStatus, 'Completed',
                    N'Auto-completed: ' + @Disposition + N' disposition resolved.');
        END

        COMMIT;
        SELECT @RepairTicketId AS RepairTicketId, 'Completed' AS Status, @Disposition AS Disposition,
               @ReplacementItemId AS ReplacementItemId, @NewRequestId AS ReplacementRequestId, @NewSetId AS ReplacementSetId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH
END
GO
