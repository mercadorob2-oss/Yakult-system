
CREATE   PROCEDURE dbo.sp_Call_SetTicketEscalationOverride
    @TicketId INT,
    @DaysToSupervisor INT = NULL,
    @DaysToManager INT = NULL,
    @Reason NVARCHAR(400) = NULL,
    @ChangedByUserId INT = NULL,
    @Clear BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Reason = LTRIM(RTRIM(@Reason));
    IF @Reason IS NULL OR @Reason = '' THROW 50021, 'Reason is required.', 1;

    DECLARE @Status NVARCHAR(20);
    SELECT @Status = Status
    FROM dbo.CallTicket WITH (UPDLOCK, HOLDLOCK)
    WHERE TicketId = @TicketId;

    IF @@ROWCOUNT = 0 THROW 50022, 'Ticket not found.', 1;
    IF @Status IN ('Solved', 'Resolved (Temporary)') THROW 50023, 'Cannot change escalation override after ticket is solved.', 1;

    DECLARE @DefaultSup INT, @DefaultMgr INT;
    SELECT TOP 1 @DefaultSup = DaysToSupervisor, @DefaultMgr = DaysToManager
    FROM dbo.CallEscalationSettings
    ORDER BY SettingsId DESC;

    IF @DefaultSup IS NULL SET @DefaultSup = 2;
    IF @DefaultMgr IS NULL SET @DefaultMgr = 3;

    DECLARE @OldSup INT = NULL, @OldMgr INT = NULL;
    SELECT @OldSup = DaysToSupervisor, @OldMgr = DaysToManager
    FROM dbo.CallTicketEscalationOverride
    WHERE TicketId = @TicketId;

    IF @Clear = 1
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.CallTicketEscalationOverride WHERE TicketId = @TicketId)
            DELETE dbo.CallTicketEscalationOverride WHERE TicketId = @TicketId;

        INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
        VALUES
        (
            @TicketId,
            @ChangedByUserId,
            'EscalationOverride',
            CASE WHEN @OldSup IS NULL AND @OldMgr IS NULL THEN NULL ELSE CONCAT('Sup=', @OldSup, ',Mgr=', @OldMgr) END,
            NULL,
            CONCAT('Cleared: ', @Reason)
        );
        RETURN;
    END

    IF @DaysToSupervisor IS NULL OR @DaysToSupervisor < 1 THROW 50024, 'DaysToSupervisor is required.', 1;
    IF @DaysToManager IS NULL OR @DaysToManager < 1 THROW 50025, 'DaysToManager is required.', 1;
    IF @DaysToSupervisor < @DefaultSup THROW 50026, 'DaysToSupervisor cannot be less than default.', 1;
    IF @DaysToManager < @DefaultMgr THROW 50027, 'DaysToManager cannot be less than default.', 1;
    IF @DaysToManager < @DaysToSupervisor THROW 50028, 'DaysToManager must be >= DaysToSupervisor.', 1;

    MERGE dbo.CallTicketEscalationOverride AS tgt
    USING (SELECT @TicketId AS TicketId) AS src
        ON tgt.TicketId = src.TicketId
    WHEN MATCHED THEN
        UPDATE SET
            DaysToSupervisor = @DaysToSupervisor,
            DaysToManager = @DaysToManager,
            Reason = @Reason,
            OverriddenByUserId = @ChangedByUserId,
            OverriddenAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (TicketId, DaysToSupervisor, DaysToManager, Reason, OverriddenByUserId)
        VALUES (@TicketId, @DaysToSupervisor, @DaysToManager, @Reason, @ChangedByUserId);

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES
    (
        @TicketId,
        @ChangedByUserId,
        'EscalationOverride',
        CASE WHEN @OldSup IS NULL AND @OldMgr IS NULL THEN NULL ELSE CONCAT('Sup=', @OldSup, ',Mgr=', @OldMgr) END,
        CONCAT('Sup=', @DaysToSupervisor, ',Mgr=', @DaysToManager),
        @Reason
    );
END
