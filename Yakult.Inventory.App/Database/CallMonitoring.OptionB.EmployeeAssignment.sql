-- Option B Migration: Assign tickets to Employees (not Users)
-- Run this in SSMS against your YIMS database AFTER the base Call* schema is installed.

USE [YIMS];
GO

/* 1) Add AssignedToEmpId to CallTicket */
IF COL_LENGTH('dbo.CallTicket', 'AssignedToEmpId') IS NULL
BEGIN
    ALTER TABLE dbo.CallTicket
    ADD AssignedToEmpId INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallTicket_AssignedToEmp')
BEGIN
    ALTER TABLE dbo.CallTicket WITH CHECK
    ADD CONSTRAINT FK_CallTicket_AssignedToEmp
    FOREIGN KEY (AssignedToEmpId) REFERENCES dbo.Employee(EmpId);
END
GO

/* 2) Keep AssignedToUserId optional; you can drop it later if you want
      (we keep it for backward compatibility and CreatedByUserId still references dbo.[User]) */

/* 3) Update vw_Call_TicketList to use Employee assignment */
CREATE OR ALTER VIEW dbo.vw_Call_TicketList
AS
SELECT
    t.TicketId,
    t.TicketCode,
    t.Issue,
    d.Name AS Department,
    eAssignee.Name AS ResponsiblePerson,
    DATEDIFF(DAY, t.CreatedAt, COALESCE(t.SolvedAt, SYSUTCDATETIME())) AS TicketAgeDays,
    COALESCE(t.LastContactAt, t.UpdatedAt, t.CreatedAt) AS LastActivityAt,
    DATEDIFF(DAY, COALESCE(t.LastContactAt, t.UpdatedAt, t.CreatedAt), SYSUTCDATETIME()) AS IdleDays,
    t.LastContactAt,
    t.Status,
    t.Priority,
    t.CallerName,
    t.IssueType,
    t.CreatedAt,
    t.UpdatedAt,
    t.SolvedAt
FROM dbo.CallTicket t
LEFT JOIN dbo.Department d ON d.DeptId = t.DeptId
LEFT JOIN dbo.Employee eAssignee ON eAssignee.EmpId = t.AssignedToEmpId;
GO

/* 4) sp_Call_CreateTicket: accept @AssignedToEmpId and insert into CallTicket.AssignedToEmpId */
CREATE OR ALTER PROCEDURE dbo.sp_Call_CreateTicket
    @ComId            INT            = NULL,
    @DeptId           INT            = NULL,
    @CallerName       NVARCHAR(150),
    @Issue            NVARCHAR(2000),
    @ProvidedSolution NVARCHAR(2000) = NULL,
    @IssueType        NVARCHAR(20)   = NULL,
    @Priority         NVARCHAR(20)   = NULL,
    @AssignedToEmpId  INT            = NULL,
    @CreatedByUserId  INT            = NULL
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

    -- Validate org combination via BranchDepartmentCompany (Department no longer owns ComId).
    -- This script does not capture BranchId, so validate Company/Department only.
    IF @ComId IS NOT NULL
       AND @DeptId IS NOT NULL
       AND OBJECT_ID('dbo.BranchDepartmentCompany', 'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM dbo.BranchDepartmentCompany bdc
            WHERE bdc.CompanyID = @ComId
              AND bdc.DepartmentID = @DeptId
        )
            THROW 50032, 'Invalid Company/Department combination.', 1;
    END

    DECLARE @TicketId INT;

    BEGIN TRAN;

    INSERT dbo.CallTicket
    (
        ComId, DeptId, CallerName, Issue, ProvidedSolution,
        IssueType, Priority, Status,
        AssignedToEmpId,
        CreatedByUserId,
        LastContactAt
    )
    VALUES
    (
        @ComId, @DeptId, @CallerName, @Issue, @ProvidedSolution,
        @IssueType, @PriorityCanonical, 'Pending',
        @AssignedToEmpId,
        @CreatedByUserId,
        SYSUTCDATETIME()
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
GO

/* 5) Requires attention view stays the same but now reads ResponsiblePerson from Employee */
CREATE OR ALTER VIEW dbo.vw_Call_RequiresAttention
AS
SELECT *
FROM dbo.vw_Call_TicketList
WHERE Status NOT IN ('Solved', 'Resolved (Temporary)')
  AND (Priority IN ('High','Critical') OR ResponsiblePerson IS NULL);
GO
