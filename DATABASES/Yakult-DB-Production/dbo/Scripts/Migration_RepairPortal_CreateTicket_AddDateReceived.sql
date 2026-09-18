-- Migration: Extend sp_RepairPortal_CreateTicket with @DateReceived (new optional param,
-- defaulted so any other caller is unaffected). Lets the technician back-date intake to when the
-- item actually arrived, instead of always defaulting to "now".
-- CREATE OR ALTER is idempotent — safe to re-run. This is the CURRENT version of the proc — it
-- carries forward the @RequestedByType/@RequestedByDeptId/@RequestedByEmpId params added in
-- Migration_RepairPortal_CreateTicket_AddRequestedBy.sql.

GO
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_CreateTicket
    @ItemId             INT,
    @Problem             NVARCHAR(2000),
    @Priority            NVARCHAR(20)  = NULL,
    @SubmittedByEmpId    INT           = NULL,
    @SubmittedByUserId   INT           = NULL,
    @CreatedByUserId     INT           = NULL,
    @RequestedByType     NVARCHAR(20)  = NULL,
    @RequestedByDeptId   INT           = NULL,
    @RequestedByEmpId    INT           = NULL,
    @DateReceived        DATETIME2     = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ItemId IS NULL THROW 51001, 'ItemId is required.', 1;
    SET @Problem = LTRIM(RTRIM(@Problem));
    IF @Problem IS NULL OR @Problem = '' THROW 51002, 'Problem is required.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Item WHERE ItemId = @ItemId)
        THROW 51003, 'Item not found.', 1;

    IF @Priority IS NULL OR LTRIM(RTRIM(@Priority)) = '' SET @Priority = 'Medium';
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
        THROW 51004, 'Invalid priority. Allowed: Low, Medium, High, Critical.', 1;

    -- Requested By: if provided, must be exactly one of Department/Employee, consistent with type.
    IF @RequestedByType IS NOT NULL
    BEGIN
        IF @RequestedByType NOT IN ('Department', 'Employee')
            THROW 51005, 'Invalid RequestedByType. Allowed: Department, Employee.', 1;

        IF @RequestedByType = 'Department' AND @RequestedByDeptId IS NULL
            THROW 51006, 'RequestedByDeptId is required when RequestedByType = Department.', 1;

        IF @RequestedByType = 'Employee' AND @RequestedByEmpId IS NULL
            THROW 51007, 'RequestedByEmpId is required when RequestedByType = Employee.', 1;

        IF @RequestedByType = 'Department' AND @RequestedByDeptId IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM dbo.Department WHERE DeptId = @RequestedByDeptId)
            THROW 51008, 'RequestedByDeptId is invalid (department not found).', 1;

        IF @RequestedByType = 'Employee' AND @RequestedByEmpId IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM dbo.Employee WHERE EmpId = @RequestedByEmpId)
            THROW 51009, 'RequestedByEmpId is invalid (employee not found).', 1;
    END

    -- Resolve the item's current active Set (same "latest active set for item" join used by
    -- RepairedItemsPageViewModel.Actions.cs) so Set/Company/Branch/Department can be snapshotted.
    DECLARE @SetId INT, @SetCode NVARCHAR(20), @ComId INT, @BranchId INT, @DeptId INT;

    SELECT TOP (1)
        @SetId = aset.SetId,
        @SetCode = aset.SetCode,
        @ComId = s.ComId,
        @BranchId = s.CurrentBranchId,
        @DeptId = s.CurrentDepartmentId
    FROM (
        SELECT
            x.ItemId, x.SetId, x.SetCode,
            ROW_NUMBER() OVER (PARTITION BY x.ItemId ORDER BY x.SetCreatedAt DESC, x.SetId DESC) AS rn
        FROM (
            SELECT si.ItemId, s.SetId, s.SetCode, s.CreatedAt AS SetCreatedAt
            FROM dbo.SetItem si
            INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
            LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
            WHERE s.Active = 1 AND archS.EntityId IS NULL

            UNION ALL

            SELECT r.ItemId, s.SetId, s.SetCode, s.CreatedAt AS SetCreatedAt
            FROM dbo.Request r
            INNER JOIN dbo.[Set] s ON s.SetId = r.SetId
            LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
            WHERE r.Active = 1 AND r.SetId IS NOT NULL AND s.Active = 1 AND archS.EntityId IS NULL
        ) x
    ) aset
    INNER JOIN dbo.[Set] s ON s.SetId = aset.SetId
    WHERE aset.ItemId = @ItemId AND aset.rn = 1;

    DECLARE @ItemNameSnapshot NVARCHAR(200), @ItemSerialSnapshot VARCHAR(255);
    SELECT @ItemNameSnapshot = Name, @ItemSerialSnapshot = SerialNumber
    FROM dbo.Item WHERE ItemId = @ItemId;

    DECLARE @RepairTicketId INT;
    DECLARE @DateReceivedResolved DATETIME2 = COALESCE(@DateReceived, SYSUTCDATETIME());

    BEGIN TRAN;

    INSERT dbo.RepairTicket
    (
        ItemId, SetId, SetCode, ComId, BranchId, DeptId,
        ItemNameSnapshot, ItemSerialSnapshot,
        Problem, Priority, Status,
        SubmittedByEmpId, SubmittedByUserId,
        RequestedByType, RequestedByDeptId, RequestedByEmpId,
        DateReceived
    )
    VALUES
    (
        @ItemId, @SetId, @SetCode, @ComId, @BranchId, @DeptId,
        @ItemNameSnapshot, @ItemSerialSnapshot,
        @Problem, @PriorityCanonical, 'Waiting',
        @SubmittedByEmpId, @SubmittedByUserId,
        @RequestedByType, @RequestedByDeptId, @RequestedByEmpId,
        @DateReceivedResolved
    );

    SET @RepairTicketId = SCOPE_IDENTITY();

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairTicketId, @CreatedByUserId, 'Created', NULL, NULL, 'Repair ticket created');

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue)
    VALUES
        (@RepairTicketId, @CreatedByUserId, 'Status', NULL, 'Waiting'),
        (@RepairTicketId, @CreatedByUserId, 'Priority', NULL, @PriorityCanonical);

    COMMIT;

    SELECT
        t.RepairTicketId, t.TicketCode, t.Status, t.Priority, t.CreatedAt
    FROM dbo.RepairTicket t
    WHERE t.RepairTicketId = @RepairTicketId;
END
GO
