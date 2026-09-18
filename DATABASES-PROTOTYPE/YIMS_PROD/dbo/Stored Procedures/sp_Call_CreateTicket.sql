
/* 4) Update Create Ticket proc */
CREATE   PROCEDURE dbo.sp_Call_CreateTicket
    @ComId            INT            = NULL,
    @DeptId           INT            = NULL,
    @BranchId         INT            = NULL,
    @CallerName       NVARCHAR(150),
    @Issue            NVARCHAR(2000),
    @ProvidedSolution NVARCHAR(2000) = NULL,
    @IssueType        NVARCHAR(20)   = NULL,
    @Priority         NVARCHAR(20)   = NULL,
    @AssignedToEmpId  INT            = NULL,
    @CreatedByUserId  INT            = NULL,
    @TicketSource     NVARCHAR(20)   = NULL,
    @CreatedAtUtc     DATETIME2(2)   = NULL,
    @LastContactAtUtc DATETIME2(2)   = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @CallerName IS NULL OR LTRIM(RTRIM(@CallerName)) = '' THROW 50001, 'CallerName is required.', 1;
    IF @Issue IS NULL OR LTRIM(RTRIM(@Issue)) = '' THROW 50002, 'Issue is required.', 1;

    IF @Priority IS NULL OR LTRIM(RTRIM(@Priority)) = ''
        SET @Priority = 'Medium';

    SET @Priority = LTRIM(RTRIM(@Priority));
    DECLARE @PriorityCanonical NVARCHAR(20) =
        CASE UPPER(@Priority)
            WHEN 'LOW' THEN 'Low'
            WHEN 'MEDIUM' THEN 'Medium'
            WHEN 'HIGH' THEN 'High'
            WHEN 'CRITICAL' THEN 'Critical'
            ELSE NULL
        END;

    IF @PriorityCanonical IS NULL
        THROW 50014, 'Invalid priority. Allowed: Low, Medium, High, Critical.', 1;

    IF @AssignedToEmpId IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.Employee WHERE EmpId = @AssignedToEmpId)
        THROW 50016, 'AssignedToEmpId is invalid (employee not found).', 1;

    IF @BranchId IS NOT NULL
       AND OBJECT_ID('dbo.Branch', 'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.Branch WHERE BranchId = @BranchId)
        THROW 50017, 'BranchId is invalid (branch not found).', 1;

    DECLARE @TicketId INT;

    BEGIN TRAN;

    INSERT dbo.CallTicket
    (
        ComId, DeptId, BranchId, CallerName, Issue, ProvidedSolution,
        IssueType, Priority, Status,
        AssignedToEmpId,
        CreatedByUserId,
        LastContactAt,
        TicketSource
    )
    VALUES
    (
        @ComId, @DeptId, @BranchId, @CallerName, @Issue, @ProvidedSolution,
        @IssueType, @PriorityCanonical, 'Pending',
        @AssignedToEmpId,
        @CreatedByUserId,
        COALESCE(@LastContactAtUtc, @CreatedAtUtc, SYSUTCDATETIME()),
        @TicketSource
    );

    SET @TicketId = SCOPE_IDENTITY();

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@TicketId, @CreatedByUserId, 'Created', NULL, NULL, 'Ticket created');

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue)
    VALUES
        (@TicketId, @CreatedByUserId, 'Status',   NULL, 'Pending'),
        (@TicketId, @CreatedByUserId, 'Priority', NULL, @PriorityCanonical);

    IF @AssignedToEmpId IS NOT NULL
        INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue)
        VALUES (@TicketId, @CreatedByUserId, 'AssignedToEmpId', NULL, CONVERT(NVARCHAR(50), @AssignedToEmpId));

    IF @BranchId IS NOT NULL
        INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue)
        VALUES (@TicketId, @CreatedByUserId, 'BranchId', NULL, CONVERT(NVARCHAR(50), @BranchId));

    COMMIT;

    SELECT
        t.TicketId,
        t.TicketCode,
        t.Status,
        t.Priority,
        t.CreatedAt
    FROM dbo.CallTicket t
    WHERE t.TicketId = @TicketId;
END
