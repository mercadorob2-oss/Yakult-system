-- Migration: Add email-helper columns to dbo.vw_Call_TicketList
-- Date: 2026-09-16
-- Purpose: The server API email helpers (ItcmEmailHelper in call-tickets.ashx,
--          ItcmActionEmailHelper in call-ticket-action.ashx) select AssignedTo,
--          ProvidedSolution, DeptId and LastReminderSentAt from this view in
--          GetTicket(). Those columns were never added to the view, so every
--          API-triggered email died with "Invalid column name" inside the
--          fire-and-forget sender (swallowed by catch{}, no log row written).
--          Symptom: mobile dry run on TCK-000069 produced zero CallEmailLog
--          rows despite HTTP 200s; desktop-sent mail was unaffected.
--          The handlers now also fall back to the base table when the columns
--          are absent, but the durable fix is exposing them here.

IF EXISTS (
    SELECT 1 FROM sys.views WHERE object_id = OBJECT_ID('dbo.vw_Call_TicketList')
)
AND NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.vw_Call_TicketList')
      AND name IN ('AssignedTo', 'ProvidedSolution', 'DeptId', 'LastReminderSentAt')
    HAVING COUNT(1) = 4
)
BEGIN
    EXEC ('
        CREATE OR ALTER VIEW dbo.vw_Call_TicketList
        AS
        SELECT
            t.TicketId,
            t.TicketCode,
            t.ComId,
            c.Name AS Company,
            t.Issue,
            d.Name AS Department,
            t.BranchId,
            t.AssignedToEmpId,
            t.TicketSource,
            CASE
                WHEN b.BranchId IS NULL THEN NULL
                WHEN ISNULL(b.IsCenter, 0) = 1 THEN b.Name + '' (Center)''
                WHEN ISNULL(b.IsDepot, 0) = 1 THEN b.Name + '' (Depot)''
                WHEN ISNULL(b.IsFactory, 0) = 1 THEN b.Name + '' (Factory)''
                WHEN ISNULL(b.IsDistributor, 0) = 1 THEN b.Name + '' (Distributor)''
                ELSE b.Name
            END AS Branch,
            eAssignee.Name AS ResponsiblePerson,
            eAssignee.Name AS AssignedTo,
            t.ProvidedSolution,
            t.DeptId,
            DATEDIFF(DAY, t.CreatedAt, COALESCE(t.SolvedAt, SYSUTCDATETIME())) AS TicketAgeDays,
            COALESCE(t.LastContactAt, t.UpdatedAt, t.CreatedAt) AS LastActivityAt,
            DATEDIFF(DAY, COALESCE(t.LastContactAt, t.UpdatedAt, t.CreatedAt), SYSUTCDATETIME()) AS IdleDays,
            t.LastContactAt,
            t.LastReminderSentAt,
            t.Status,
            t.Priority,
            t.CallerName,
            t.IssueType,
            t.CreatedAt,
            t.UpdatedAt,
            t.SolvedAt
        FROM dbo.CallTicket t
        LEFT JOIN dbo.Company c ON c.ComId = t.ComId
        LEFT JOIN dbo.Department d ON d.DeptId = t.DeptId
        LEFT JOIN dbo.Employee eAssignee ON eAssignee.EmpId = t.AssignedToEmpId
        LEFT JOIN dbo.Branch b ON b.BranchId = t.BranchId;
    ');

    PRINT 'View vw_Call_TicketList extended with AssignedTo/ProvidedSolution/DeptId/LastReminderSentAt.';
END
ELSE
BEGIN
    PRINT 'View vw_Call_TicketList already exposes the email columns, or the view is missing. Skipped.';
END
GO
