-- Migration: sp_RepairPortal_UpdateRequestedBy — lets a technician edit the "Requested By"
-- (Department or Employee) on an existing ticket from the Detail window, mirroring the same
-- validation block already used by sp_RepairPortal_CreateTicket.
-- CREATE OR ALTER is idempotent — safe to re-run.

GO
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_UpdateRequestedBy
    @RepairTicketId     INT,
    @RequestedByType     NVARCHAR(20)  = NULL,
    @RequestedByDeptId   INT           = NULL,
    @RequestedByEmpId    INT           = NULL,
    @ChangedByUserId     INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @RepairTicketId IS NULL THROW 51200, 'RepairTicketId is required.', 1;

    -- Requested By: must be exactly one of Department/Employee, consistent with type — same
    -- validation as sp_RepairPortal_CreateTicket.
    IF @RequestedByType IS NULL OR LTRIM(RTRIM(@RequestedByType)) = ''
        THROW 51201, 'RequestedByType is required.', 1;

    SET @RequestedByType = LTRIM(RTRIM(@RequestedByType));

    IF @RequestedByType NOT IN ('Department', 'Employee')
        THROW 51202, 'Invalid RequestedByType. Allowed: Department, Employee.', 1;

    IF @RequestedByType = 'Department' AND @RequestedByDeptId IS NULL
        THROW 51203, 'RequestedByDeptId is required when RequestedByType = Department.', 1;

    IF @RequestedByType = 'Employee' AND @RequestedByEmpId IS NULL
        THROW 51204, 'RequestedByEmpId is required when RequestedByType = Employee.', 1;

    IF @RequestedByType = 'Department' AND @RequestedByDeptId IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.Department WHERE DeptId = @RequestedByDeptId)
        THROW 51205, 'RequestedByDeptId is invalid (department not found).', 1;

    IF @RequestedByType = 'Employee' AND @RequestedByEmpId IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.Employee WHERE EmpId = @RequestedByEmpId)
        THROW 51206, 'RequestedByEmpId is invalid (employee not found).', 1;

    -- Normalize: only the Id matching the chosen type is kept.
    DECLARE @NewDeptId INT = CASE WHEN @RequestedByType = 'Department' THEN @RequestedByDeptId ELSE NULL END;
    DECLARE @NewEmpId  INT = CASE WHEN @RequestedByType = 'Employee'   THEN @RequestedByEmpId  ELSE NULL END;

    DECLARE @OldSummary NVARCHAR(300), @NewSummary NVARCHAR(300);

    BEGIN TRAN;

    IF NOT EXISTS (SELECT 1 FROM dbo.RepairTicket WITH (UPDLOCK, HOLDLOCK) WHERE RepairTicketId = @RepairTicketId)
    BEGIN
        ROLLBACK;
        THROW 51207, 'Repair ticket not found.', 1;
    END

    SELECT @OldSummary =
        CASE t.RequestedByType
            WHEN 'Department' THEN 'Dept: ' + ISNULL(d.Name, '(unknown)')
            WHEN 'Employee'   THEN 'Employee: ' + ISNULL(e.Name, '(unknown)')
            ELSE 'Not set'
        END
    FROM dbo.RepairTicket t
    LEFT JOIN dbo.Department d ON d.DeptId = t.RequestedByDeptId
    LEFT JOIN dbo.Employee e ON e.EmpId = t.RequestedByEmpId
    WHERE t.RepairTicketId = @RepairTicketId;

    SELECT @NewSummary =
        CASE @RequestedByType
            WHEN 'Department' THEN 'Dept: ' + ISNULL((SELECT Name FROM dbo.Department WHERE DeptId = @NewDeptId), '(unknown)')
            WHEN 'Employee'   THEN 'Employee: ' + ISNULL((SELECT Name FROM dbo.Employee WHERE EmpId = @NewEmpId), '(unknown)')
        END;

    UPDATE dbo.RepairTicket
    SET RequestedByType = @RequestedByType,
        RequestedByDeptId = @NewDeptId,
        RequestedByEmpId = @NewEmpId
    WHERE RepairTicketId = @RepairTicketId;

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairTicketId, @ChangedByUserId, 'RequestedBy', @OldSummary, @NewSummary, NULL);

    COMMIT;
END
GO
