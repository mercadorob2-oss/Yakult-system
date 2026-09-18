
CREATE   VIEW dbo.vw_Call_TicketList
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
        WHEN ISNULL(b.IsCenter, 0) = 1 THEN b.Name + ' (Center)'
        WHEN ISNULL(b.IsDepot, 0) = 1 THEN b.Name + ' (Depot)'
        WHEN ISNULL(b.IsFactory, 0) = 1 THEN b.Name + ' (Factory)'
        WHEN ISNULL(b.IsDistributor, 0) = 1 THEN b.Name + ' (Distributor)'
        ELSE b.Name
    END AS Branch,
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
LEFT JOIN dbo.Company c ON c.ComId = t.ComId
LEFT JOIN dbo.Department d ON d.DeptId = t.DeptId
LEFT JOIN dbo.Employee eAssignee ON eAssignee.EmpId = t.AssignedToEmpId
LEFT JOIN dbo.Branch b ON b.BranchId = t.BranchId;