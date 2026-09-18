-- Migration: Link Repair Technician Portal tickets to their originating IT Call tickets.
-- A CallTicket can be forwarded to at most one RepairTicket. The stored procedure below is
-- idempotent: repeat attempts return the existing linked repair ticket instead of creating a duplicate.

IF OBJECT_ID('dbo.RepairTicket', 'U') IS NULL
    THROW 51240, 'dbo.RepairTicket must exist before applying the IT Call link migration.', 1;

IF OBJECT_ID('dbo.CallTicket', 'U') IS NULL
    THROW 51241, 'dbo.CallTicket must exist before applying the IT Call link migration.', 1;
GO

IF COL_LENGTH('dbo.RepairTicket', 'CallTicketId') IS NULL
BEGIN
    ALTER TABLE dbo.RepairTicket ADD CallTicketId INT NULL;
    PRINT 'Added dbo.RepairTicket.CallTicketId.';
END
ELSE
    PRINT 'dbo.RepairTicket.CallTicketId already exists; skipped.';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_RepairTicket_CallTicket'
      AND parent_object_id = OBJECT_ID('dbo.RepairTicket')
)
BEGIN
    ALTER TABLE dbo.RepairTicket
        ADD CONSTRAINT FK_RepairTicket_CallTicket
        FOREIGN KEY (CallTicketId) REFERENCES dbo.CallTicket (TicketId);
    PRINT 'Created FK_RepairTicket_CallTicket.';
END
ELSE
    PRINT 'FK_RepairTicket_CallTicket already exists; skipped.';
GO

-- One repair workflow is created for a given IT Call. This keeps retrying the forward action safe.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_RepairTicket_CallTicketId'
      AND object_id = OBJECT_ID('dbo.RepairTicket')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_RepairTicket_CallTicketId
        ON dbo.RepairTicket (CallTicketId ASC)
        WHERE CallTicketId IS NOT NULL;
    PRINT 'Created UX_RepairTicket_CallTicketId.';
END
ELSE
    PRINT 'UX_RepairTicket_CallTicketId already exists; skipped.';
GO

CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_CreateTicketFromCallTicket
    @CallTicketId          INT,
    @ItemId                INT,
    @Problem               NVARCHAR(2000),
    @Priority              NVARCHAR(20)  = NULL,
    @SubmittedByEmpId      INT           = NULL,
    @SubmittedByUserId     INT           = NULL,
    @CreatedByUserId       INT           = NULL,
    @RequestedByType       NVARCHAR(20)  = NULL,
    @RequestedByDeptId     INT           = NULL,
    @RequestedByEmpId      INT           = NULL,
    @DateReceived          DATETIME2     = NULL,
    @RequestedByComId      INT           = NULL,
    @RequestedByBranchId   INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @CallTicketId IS NULL OR @CallTicketId <= 0
        THROW 51242, 'CallTicketId is required.', 1;

    DECLARE @ExistingRepairTicketId INT;
    DECLARE @CallTicketCode NVARCHAR(50);
    DECLARE @RepairTicketId INT;

    BEGIN TRAN;

    -- Lock the parent ticket so two operators forwarding the same IT Call cannot create
    -- two repair workflows while they both observe no current link.
    SELECT @CallTicketCode = TicketCode
    FROM dbo.CallTicket WITH (UPDLOCK, HOLDLOCK)
    WHERE TicketId = @CallTicketId;

    IF @@ROWCOUNT = 0
        THROW 51243, 'Source IT Call ticket was not found.', 1;

    SELECT @ExistingRepairTicketId = RepairTicketId
    FROM dbo.RepairTicket WITH (UPDLOCK, HOLDLOCK)
    WHERE CallTicketId = @CallTicketId;

    IF @ExistingRepairTicketId IS NOT NULL
    BEGIN
        COMMIT;
        SELECT RepairTicketId, TicketCode, Status, Priority, CreatedAt
        FROM dbo.RepairTicket
        WHERE RepairTicketId = @ExistingRepairTicketId;
        RETURN;
    END

    DECLARE @Created TABLE
    (
        RepairTicketId INT NOT NULL,
        TicketCode NVARCHAR(50) NULL,
        Status NVARCHAR(20) NULL,
        Priority NVARCHAR(20) NULL,
        CreatedAt DATETIME2 NULL
    );

    INSERT @Created (RepairTicketId, TicketCode, Status, Priority, CreatedAt)
    EXEC dbo.sp_RepairPortal_CreateTicket
        @ItemId = @ItemId,
        @Problem = @Problem,
        @Priority = @Priority,
        @SubmittedByEmpId = @SubmittedByEmpId,
        @SubmittedByUserId = @SubmittedByUserId,
        @CreatedByUserId = @CreatedByUserId,
        @RequestedByType = @RequestedByType,
        @RequestedByDeptId = @RequestedByDeptId,
        @RequestedByEmpId = @RequestedByEmpId,
        @DateReceived = @DateReceived,
        @RequestedByComId = @RequestedByComId,
        @RequestedByBranchId = @RequestedByBranchId;

    SELECT @RepairTicketId = RepairTicketId FROM @Created;
    IF @RepairTicketId IS NULL
        THROW 51244, 'Repair ticket creation did not return a ticket identifier.', 1;

    UPDATE dbo.RepairTicket
    SET CallTicketId = @CallTicketId
    WHERE RepairTicketId = @RepairTicketId;

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES
    (
        @RepairTicketId,
        @CreatedByUserId,
        'SourceITCall',
        NULL,
        CONVERT(NVARCHAR(30), @CallTicketId),
        'Forwarded from IT Call ' + COALESCE(@CallTicketCode, CONVERT(NVARCHAR(30), @CallTicketId))
    );

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES
    (
        @CallTicketId,
        @CreatedByUserId,
        'RepairTicketLink',
        NULL,
        CONVERT(NVARCHAR(30), @RepairTicketId),
        'Forwarded to Repair Ticket ' + (SELECT TicketCode FROM dbo.RepairTicket WHERE RepairTicketId = @RepairTicketId)
    );

    COMMIT;

    SELECT RepairTicketId, TicketCode, Status, Priority, CreatedAt
    FROM dbo.RepairTicket
    WHERE RepairTicketId = @RepairTicketId;
END
GO
