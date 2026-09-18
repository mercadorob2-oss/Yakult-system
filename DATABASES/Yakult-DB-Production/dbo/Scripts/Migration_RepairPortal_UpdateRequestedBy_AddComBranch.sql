-- Migration: Extend sp_RepairPortal_UpdateRequestedBy with @RequestedByComId / @RequestedByBranchId
-- — same Company-required / Branch-optional / BranchDepartmentCompany validation added to
-- sp_RepairPortal_CreateTicket in Migration_RepairPortal_CreateTicket_AddRequestedByComBranch.sql.
-- The human-readable summary written to RepairTicketHistory now folds in Company/Branch, e.g.
-- "Department: IT Department (ACME Corp / Manila Branch)".
-- CREATE OR ALTER is idempotent — safe to re-run.

GO
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_UpdateRequestedBy
    @RepairTicketId       INT,
    @RequestedByType       NVARCHAR(20)  = NULL,
    @RequestedByDeptId     INT           = NULL,
    @RequestedByEmpId      INT           = NULL,
    @ChangedByUserId       INT           = NULL,
    @RequestedByComId      INT           = NULL,
    @RequestedByBranchId   INT           = NULL
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

    -- Department mode: Company is required; Branch optional, but if supplied the full
    -- Company/Branch/Department combination must exist in dbo.BranchDepartmentCompany — mirrors
    -- sp_RepairPortal_CreateTicket / sp_Call_CreateTicket's validation block.
    IF @RequestedByType = 'Department'
    BEGIN
        IF @RequestedByComId IS NULL
            THROW 51210, 'RequestedByComId is required when RequestedByType = Department.', 1;

        IF NOT EXISTS (SELECT 1 FROM dbo.Company WHERE ComId = @RequestedByComId)
            THROW 51211, 'RequestedByComId is invalid (company not found).', 1;

        IF @RequestedByBranchId IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM dbo.Branch WHERE BranchId = @RequestedByBranchId)
            THROW 51212, 'RequestedByBranchId is invalid (branch not found).', 1;

        IF OBJECT_ID('dbo.BranchDepartmentCompany', 'U') IS NOT NULL
        BEGIN
            IF @RequestedByBranchId IS NOT NULL
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM dbo.BranchDepartmentCompany bdc
                    WHERE bdc.CompanyID = @RequestedByComId
                      AND bdc.BranchID = @RequestedByBranchId
                      AND bdc.DepartmentID = @RequestedByDeptId
                )
                    THROW 51213, 'Invalid Company/Branch/Department combination for Requested By.', 1;
            END
            ELSE
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM dbo.BranchDepartmentCompany bdc
                    WHERE bdc.CompanyID = @RequestedByComId
                      AND bdc.DepartmentID = @RequestedByDeptId
                )
                    THROW 51214, 'Invalid Company/Department combination for Requested By.', 1;
            END
        END
    END

    -- Normalize: only the Id/Com/Branch matching the chosen type is kept.
    DECLARE @NewDeptId INT = CASE WHEN @RequestedByType = 'Department' THEN @RequestedByDeptId ELSE NULL END;
    DECLARE @NewEmpId  INT = CASE WHEN @RequestedByType = 'Employee'   THEN @RequestedByEmpId  ELSE NULL END;
    DECLARE @NewComId    INT = CASE WHEN @RequestedByType = 'Department' THEN @RequestedByComId    ELSE NULL END;
    DECLARE @NewBranchId INT = CASE WHEN @RequestedByType = 'Department' THEN @RequestedByBranchId ELSE NULL END;

    DECLARE @OldSummary NVARCHAR(300), @NewSummary NVARCHAR(300);

    BEGIN TRAN;

    IF NOT EXISTS (SELECT 1 FROM dbo.RepairTicket WITH (UPDLOCK, HOLDLOCK) WHERE RepairTicketId = @RepairTicketId)
    BEGIN
        ROLLBACK;
        THROW 51207, 'Repair ticket not found.', 1;
    END

    SELECT @OldSummary =
        CASE t.RequestedByType
            WHEN 'Department' THEN 'Department: ' + ISNULL(d.Name, '(unknown)') +
                CASE WHEN co.Name IS NOT NULL OR br.Name IS NOT NULL
                     THEN ' (' + ISNULL(co.Name, '(unknown company)') +
                          CASE WHEN br.Name IS NOT NULL THEN ' / ' + br.Name ELSE '' END + ')'
                     ELSE ''
                END
            WHEN 'Employee'   THEN 'Employee: ' + ISNULL(e.Name, '(unknown)')
            ELSE 'Not set'
        END
    FROM dbo.RepairTicket t
    LEFT JOIN dbo.Department d ON d.DeptId = t.RequestedByDeptId
    LEFT JOIN dbo.Employee e ON e.EmpId = t.RequestedByEmpId
    LEFT JOIN dbo.Company co ON co.ComId = t.RequestedByComId
    LEFT JOIN dbo.Branch br ON br.BranchId = t.RequestedByBranchId
    WHERE t.RepairTicketId = @RepairTicketId;

    SELECT @NewSummary =
        CASE @RequestedByType
            WHEN 'Department' THEN 'Department: ' + ISNULL((SELECT Name FROM dbo.Department WHERE DeptId = @NewDeptId), '(unknown)') +
                CASE WHEN @NewComId IS NOT NULL
                     THEN ' (' + ISNULL((SELECT Name FROM dbo.Company WHERE ComId = @NewComId), '(unknown company)') +
                          CASE WHEN @NewBranchId IS NOT NULL
                               THEN ' / ' + ISNULL((SELECT Name FROM dbo.Branch WHERE BranchId = @NewBranchId), '(unknown branch)')
                               ELSE '' END + ')'
                     ELSE ''
                END
            WHEN 'Employee'   THEN 'Employee: ' + ISNULL((SELECT Name FROM dbo.Employee WHERE EmpId = @NewEmpId), '(unknown)')
        END;

    UPDATE dbo.RepairTicket
    SET RequestedByType = @RequestedByType,
        RequestedByDeptId = @NewDeptId,
        RequestedByEmpId = @NewEmpId,
        RequestedByComId = @NewComId,
        RequestedByBranchId = @NewBranchId
    WHERE RepairTicketId = @RepairTicketId;

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairTicketId, @ChangedByUserId, 'RequestedBy', @OldSummary, @NewSummary, NULL);

    COMMIT;
END
GO
