using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Request Portal history by department (Company / Branch / Department), for the admin
    /// "Department Request History" page and the Department Accounts page.
    ///
    /// A request belongs to the department saved on its dbo.Request row at submit time
    /// (ComId / BranchId / DeptId), falling back to the employee's for old rows without them.
    /// That is the same rule a Department Account's own Request History uses
    /// (RequesterPortalService.GetPortalRequestsByUser, desktop and web), so an admin sees
    /// exactly what the department account sees, or will see once one is created.
    /// Dept. Level requests are the ones with no employee (EmpId NULL).
    /// </summary>
    public class DepartmentRequestHistoryRepository
    {
        private string ConnStr => DatabaseConfig.ConnectionString;

        public const string RequesterAll       = "All";
        public const string RequesterDeptLevel = "DeptLevel";
        public const string RequesterEmployee  = "Employee";

        // In both queries below:
        //   sc = the department a request belongs to (saved on the Request row, else the employee's);
        //   '[PORTAL' covers '[PORTAL]' (self / desktop assisted) and '[PORTAL_ASSISTED]' (web assisted).

        /// <summary>
        /// Submission counts (one per SubmissionSessionId, like the history grids) per
        /// department, split into Dept. Level and employee submissions.
        /// </summary>
        public async Task<Dictionary<(int ComId, int BranchId, int DeptId), DepartmentRequestCounts>> GetSubmissionCountsAsync()
        {
            const string sql = @"
                SELECT sc.ComId, sc.BranchId, sc.DeptId,
                       COUNT(DISTINCT CASE WHEN r.EmpId IS NULL     THEN k.SubmissionKey END) AS DeptLevelCount,
                       COUNT(DISTINCT CASE WHEN r.EmpId IS NOT NULL THEN k.SubmissionKey END) AS EmployeeCount
                FROM dbo.Request r
                LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
                CROSS APPLY (SELECT COALESCE(r.ComId,    e.ComId)    AS ComId,
                                    COALESCE(r.BranchId, e.BranchId) AS BranchId,
                                    COALESCE(r.DeptId,   e.DeptId)   AS DeptId) sc
                CROSS APPLY (SELECT COALESCE(CONVERT(VARCHAR(36), r.SubmissionSessionId),
                                             'R' + CONVERT(VARCHAR(12), r.ReqId)) AS SubmissionKey) k
                WHERE r.Description LIKE '[[]PORTAL%'
                  AND sc.ComId IS NOT NULL AND sc.BranchId IS NOT NULL AND sc.DeptId IS NOT NULL
                GROUP BY sc.ComId, sc.BranchId, sc.DeptId";

            var result = new Dictionary<(int, int, int), DepartmentRequestCounts>();
            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 })
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result[(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2))] = new DepartmentRequestCounts
                        {
                            DeptLevelCount = reader.GetInt32(3),
                            EmployeeCount  = reader.GetInt32(4)
                        };
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Request lines (one row per dbo.Request) for the given department filter.
        /// Null IDs mean "any". requesterType is one of the Requester* constants.
        /// </summary>
        public async Task<List<DepartmentRequestLineDto>> GetRequestLinesAsync(
            int? comId, int? branchId, int? deptId, string requesterType, DateTime? from, DateTime? to)
        {
            const string sql = @"
                SELECT
                    r.ReqId,
                    r.SubmissionSessionId,
                    r.DateRequested,
                    CASE
                        WHEN r.Status IN ('Under Review', 'Processing')
                             AND OBJECT_ID('dbo.UnfulfilledCartridgeExchange', 'U') IS NOT NULL
                             AND EXISTS (SELECT 1 FROM dbo.UnfulfilledCartridgeExchange uce WHERE uce.ReqId = r.ReqId)
                        THEN
                            CASE
                                WHEN (SELECT SUM(ISNULL(uce2.IssuedFullQty, 0))
                                      FROM dbo.UnfulfilledCartridgeExchange uce2
                                      WHERE uce2.ReqId = r.ReqId) = 0
                                THEN 'Unfulfilled'
                                ELSE 'Partially Fulfilled'
                            END
                        WHEN r.Status = 'Submitted' THEN 'Fulfilled'
                        ELSE r.Status
                    END AS Status,
                    r.Quantity,
                    i.Name AS ItemName,
                    crm.CartridgeModel,
                    ISNULL(crm.GoodEmptyQty, 0)    AS GoodEmptyQty,
                    ISNULL(crm.DamagedEmptyQty, 0) AS DamagedEmptyQty,
                    s.SetCode,
                    r.Description,
                    r.Remarks,
                    e.Name AS EmployeeName,
                    sc.ComId, sc.BranchId, sc.DeptId,
                    c.Name AS CompanyName,
                    b.Name AS BranchName,
                    d.Name AS DepartmentName,
                    COALESCE(ce.Name, cu.Name) AS CreatedByName,
                    r.RequestSource,
                    dacc.Username AS DeptAccountUsername
                FROM dbo.Request r
                LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
                CROSS APPLY (SELECT COALESCE(r.ComId,    e.ComId)    AS ComId,
                                    COALESCE(r.BranchId, e.BranchId) AS BranchId,
                                    COALESCE(r.DeptId,   e.DeptId)   AS DeptId) sc
                INNER JOIN dbo.Item i            ON r.ItemId = i.ItemId
                LEFT JOIN dbo.Company    c       ON c.ComId    = sc.ComId
                LEFT JOIN dbo.Branch     b       ON b.BranchId = sc.BranchId
                LEFT JOIN dbo.Department d       ON d.DeptId   = sc.DeptId
                LEFT JOIN dbo.CartridgeRequestModel crm ON crm.ReqId = r.ReqId
                LEFT JOIN dbo.[Set]      s       ON s.SetId    = r.SetId
                LEFT JOIN dbo.[User]     cu      ON cu.UserId  = r.CreatedBy
                LEFT JOIN dbo.Employee   ce      ON ce.EmpId   = cu.EmpId
                -- The department account covering this department, matched the way login
                -- resolves it: the saved ID when set, otherwise the name.
                OUTER APPLY (
                    SELECT TOP 1 da.Username
                    FROM dbo.DepartmentAccount da
                    WHERE da.Username IS NOT NULL
                      AND (da.ComId    = sc.ComId    OR (da.ComId    IS NULL AND da.CompanyName    = c.Name))
                      AND (da.BranchId = sc.BranchId OR (da.BranchId IS NULL AND da.BranchName     = b.Name))
                      AND (da.DeptId   = sc.DeptId   OR (da.DeptId   IS NULL AND da.DepartmentName = d.Name))
                ) dacc
                WHERE r.Description LIKE '[[]PORTAL%'
                  AND (@ComId    IS NULL OR sc.ComId    = @ComId)
                  AND (@BranchId IS NULL OR sc.BranchId = @BranchId)
                  AND (@DeptId   IS NULL OR sc.DeptId   = @DeptId)
                  AND (@RequesterType = 'All'
                       OR (@RequesterType = 'DeptLevel' AND r.EmpId IS NULL)
                       OR (@RequesterType = 'Employee'  AND r.EmpId IS NOT NULL))
                  AND (@From IS NULL OR r.DateRequested >= @From)
                  AND (@To   IS NULL OR r.DateRequested <  DATEADD(DAY, 1, @To))
                ORDER BY r.DateCreated DESC";

            var list = new List<DepartmentRequestLineDto>();
            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 })
                {
                    cmd.Parameters.AddWithValue("@ComId",         (object)comId    ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@BranchId",      (object)branchId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DeptId",        (object)deptId   ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@RequesterType", requesterType ?? RequesterAll);
                    cmd.Parameters.AddWithValue("@From",          (object)from?.Date ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@To",            (object)to?.Date   ?? DBNull.Value);

                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            list.Add(new DepartmentRequestLineDto
                            {
                                ReqId               = r.GetInt32(0),
                                SubmissionSessionId = r.IsDBNull(1) ? (Guid?)null : r.GetGuid(1),
                                DateRequested       = r.GetDateTime(2),
                                Status              = r.GetString(3),
                                Quantity            = r.GetInt32(4),
                                ItemName            = r.GetString(5),
                                CartridgeModel      = r.IsDBNull(6)  ? null : r.GetString(6),
                                GoodEmptyQty        = r.GetInt32(7),
                                DamagedEmptyQty     = r.GetInt32(8),
                                SetCode             = r.IsDBNull(9)  ? null : r.GetString(9),
                                Description         = r.GetString(10),
                                Remarks             = r.IsDBNull(11) ? null : r.GetString(11),
                                EmployeeName        = r.IsDBNull(12) ? null : r.GetString(12),
                                ComId               = r.IsDBNull(13) ? (int?)null : r.GetInt32(13),
                                BranchId            = r.IsDBNull(14) ? (int?)null : r.GetInt32(14),
                                DeptId              = r.IsDBNull(15) ? (int?)null : r.GetInt32(15),
                                CompanyName         = r.IsDBNull(16) ? null : r.GetString(16),
                                BranchName          = r.IsDBNull(17) ? null : r.GetString(17),
                                DepartmentName      = r.IsDBNull(18) ? null : r.GetString(18),
                                CreatedByName       = r.IsDBNull(19) ? null : r.GetString(19),
                                RequestSource       = r.IsDBNull(20) ? null : r.GetString(20),
                                DeptAccountUsername = r.IsDBNull(21) ? null : r.GetString(21)
                            });
                        }
                    }
                }
            }
            return list;
        }

        /// <summary>Companies, branches and departments for the page's filter dropdowns.</summary>
        public async Task<(List<DeptScopeOption> Companies, List<DeptScopeOption> Branches, List<DeptScopeOption> Departments)> GetFilterOptionsAsync()
        {
            var companies   = new List<DeptScopeOption>();
            var branches    = new List<DeptScopeOption>();
            var departments = new List<DeptScopeOption>();

            const string sql = @"
                SELECT ComId,    Name FROM dbo.Company    ORDER BY Name;
                SELECT BranchId, Name FROM dbo.Branch     ORDER BY Name;
                SELECT DeptId,   Name FROM dbo.Department ORDER BY Name;";

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                using (var r = await cmd.ExecuteReaderAsync())
                {
                    foreach (var target in new[] { companies, branches, departments })
                    {
                        while (await r.ReadAsync())
                            target.Add(new DeptScopeOption { Id = r.GetInt32(0), Name = r.IsDBNull(1) ? "" : r.GetString(1) });
                        await r.NextResultAsync();
                    }
                }
            }
            return (companies, branches, departments);
        }
    }

    public class DepartmentRequestCounts
    {
        public int DeptLevelCount { get; set; }
        public int EmployeeCount  { get; set; }
        public int Total => DeptLevelCount + EmployeeCount;
    }

    public class DeptScopeOption
    {
        public int?   Id   { get; set; }
        public string Name { get; set; }
        public override string ToString() => Name;
    }

    public class DepartmentRequestLineDto
    {
        public int       ReqId               { get; set; }
        public Guid?     SubmissionSessionId { get; set; }
        public DateTime  DateRequested       { get; set; }
        public string    Status              { get; set; }
        public int       Quantity            { get; set; }
        public string    ItemName            { get; set; }
        public string    CartridgeModel      { get; set; }
        public int       GoodEmptyQty        { get; set; }
        public int       DamagedEmptyQty     { get; set; }
        public string    SetCode             { get; set; }
        public string    Description         { get; set; }
        public string    Remarks             { get; set; }
        /// <summary>Null for a Dept. Level request.</summary>
        public string    EmployeeName        { get; set; }
        public int?      ComId               { get; set; }
        public int?      BranchId            { get; set; }
        public int?      DeptId              { get; set; }
        public string    CompanyName         { get; set; }
        public string    BranchName          { get; set; }
        public string    DepartmentName      { get; set; }
        public string    CreatedByName       { get; set; }
        public string    RequestSource       { get; set; }
        /// <summary>Username of the department account covering this department, or null if none yet.</summary>
        public string    DeptAccountUsername { get; set; }
    }
}
