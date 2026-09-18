using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net.Mail;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Repositories
{
    public sealed partial class CallMonitoringRepository : ICallMonitoringRepository
    {
        private sealed class TicketListViewCapabilities
        {
            public bool HasComIdColumn { get; set; }
            public bool HasCompanyColumn { get; set; }
            public bool HasBranchIdColumn { get; set; }
            public bool HasBranchNameColumn { get; set; }
            public bool HasIdleDaysColumn { get; set; }
            public bool HasAssignedToEmpIdViewColumn { get; set; }
        }

        private sealed class TicketListViewCapabilitiesCacheEntry
        {
            public TicketListViewCapabilities Value { get; set; }
            public DateTime ExpiresUtc { get; set; }
        }

        public sealed class CallTicketPageResult
        {
            public List<CallTicketListItem> Items { get; set; } = new List<CallTicketListItem>();
            public int TotalCount { get; set; }
            public bool HasNext { get; set; }
        }

        public sealed class IncomingTicketsSummary
        {
            public int Pending { get; set; }
            public int InProgress { get; set; }
            public int Solved { get; set; }
            public int Escalated { get; set; }
            public int Critical { get; set; }
            public int High { get; set; }
            public int Unassigned { get; set; }
            public int OldTickets { get; set; }
            public int TotalCount { get; set; }
        }

        private static readonly object TicketListViewCapabilitiesCacheLock = new object();
        private static readonly SemaphoreSlim TicketListViewCapabilitiesSemaphore = new SemaphoreSlim(1, 1);
        private static readonly Dictionary<string, TicketListViewCapabilitiesCacheEntry> TicketListViewCapabilitiesCache =
            new Dictionary<string, TicketListViewCapabilitiesCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan TicketListViewCapabilitiesCacheDuration = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public CallMonitoringRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
        }

        private string GetConnectionString()
        {
            Yakult.Inventory.App.Core.DatabaseConfig.EnsureConfigured();
            return Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;
        }

        public string ConnectionString => GetConnectionString();

        private static string GetConnectionScope(SqlConnection connection)
        {
            if (connection == null)
                return string.Empty;

            try
            {
                var dataSource = connection.DataSource ?? string.Empty;
                var database = connection.Database ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(dataSource) || !string.IsNullOrWhiteSpace(database))
                    return dataSource + "|" + database;
            }
            catch
            {
            }

            return connection.ConnectionString ?? string.Empty;
        }

        private static bool TryGetTicketListViewCapabilities(string key, out TicketListViewCapabilities value)
        {
            lock (TicketListViewCapabilitiesCacheLock)
            {
                if (!TicketListViewCapabilitiesCache.TryGetValue(key, out var entry))
                {
                    value = null;
                    return false;
                }

                if (entry.ExpiresUtc <= DateTime.UtcNow)
                {
                    TicketListViewCapabilitiesCache.Remove(key);
                    value = null;
                    return false;
                }

                value = entry.Value;
                return value != null;
            }
        }

        private static void SetTicketListViewCapabilities(string key, TicketListViewCapabilities value)
        {
            lock (TicketListViewCapabilitiesCacheLock)
            {
                TicketListViewCapabilitiesCache[key] = new TicketListViewCapabilitiesCacheEntry
                {
                    Value = value,
                    ExpiresUtc = DateTime.UtcNow.Add(TicketListViewCapabilitiesCacheDuration)
                };
            }
        }

        private static async Task<TicketListViewCapabilities> GetTicketListViewCapabilitiesAsync(SqlConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            var cacheKey = GetConnectionScope(connection) + "|VIEWCAPS|dbo.vw_Call_TicketList";
            if (TryGetTicketListViewCapabilities(cacheKey, out var cached))
                return cached;

            await TicketListViewCapabilitiesSemaphore.WaitAsync();
            try
            {
                if (TryGetTicketListViewCapabilities(cacheKey, out cached))
                    return cached;

                var capabilities = new TicketListViewCapabilities
                {
                    HasComIdColumn = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.vw_Call_TicketList", "ComId"),
                    HasCompanyColumn = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.vw_Call_TicketList", "Company"),
                    HasBranchIdColumn = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.vw_Call_TicketList", "BranchId"),
                    HasBranchNameColumn = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.vw_Call_TicketList", "Branch"),
                    HasIdleDaysColumn = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.vw_Call_TicketList", "IdleDays"),
                    HasAssignedToEmpIdViewColumn = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.vw_Call_TicketList", "AssignedToEmpId")
                };

                SetTicketListViewCapabilities(cacheKey, capabilities);
                return capabilities;
            }
            finally
            {
                TicketListViewCapabilitiesSemaphore.Release();
            }
        }

        public async Task<bool> CallSchemaExistsAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicket");
            }
        }

        public async Task<bool> EmployeeAssignmentEnabledAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "AssignedToEmpId");
            }
        }

        public async Task<bool> AssignmentEligibilitySettingsEnabledAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await CallSchemaGate.TableExistsAsync(connection, "dbo.CallAssignmentEligibility");
            }
        }

        public async Task<bool> EscalationSettingsEnabledAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEscalationSettings");
            }
        }

        public async Task<bool> EscalationOverridesEnabledAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketEscalationOverride");
            }
        }

        public async Task<bool> EmailNotificationSchemaExistsAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEmailSettings");
            }
        }

        public async Task<bool> DepartmentSmtpProfileSchemaExistsAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await CallSchemaGate.TableExistsAsync(connection, "dbo.CallDepartmentSmtpProfile");
            }
        }

        public async Task<bool> EmailLogSchemaExistsAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEmailLog");
            }
        }

        public async Task<List<CallTicketListItem>> GetRequiresAttentionAsync(int maxRows = 100)
        {
            var overdueDays = await GetOverdueDaysAsync(defaultDays: 3);

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var capabilities = await GetTicketListViewCapabilitiesAsync(connection);

                // Some environments have older versions of vw_Call_TicketList that don't expose these columns yet.
                // Select NULL placeholders instead of referencing missing columns to avoid hard failures.
                var hasComId = capabilities.HasComIdColumn;
                var hasCompany = capabilities.HasCompanyColumn;
                var hasBranchId = capabilities.HasBranchIdColumn;
                var hasBranch = capabilities.HasBranchNameColumn;
                var hasIdleDays = capabilities.HasIdleDaysColumn;
                var hasAssignedToEmpIdView = capabilities.HasAssignedToEmpIdViewColumn;
                var hasAssignedToEmpIdTable = !hasAssignedToEmpIdView
                    && await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "AssignedToEmpId");

                var joinCallTicket = hasAssignedToEmpIdTable ? "LEFT JOIN dbo.CallTicket t ON t.TicketId = v.TicketId" : string.Empty;
                var assignedToEmpIdExpr = hasAssignedToEmpIdView ? "v.AssignedToEmpId"
                    : hasAssignedToEmpIdTable ? "t.AssignedToEmpId"
                    : "CAST(NULL AS int)";

                var overdueExpr = hasIdleDays ? "v.IdleDays" : "v.TicketAgeDays";

                var sql = $@"
 SELECT TOP (@MaxRows)
     v.TicketId,
     v.TicketCode,
    {(hasComId ? "v.ComId" : "CAST(NULL AS int) AS ComId")},
    {(hasCompany ? "v.Company" : "CAST(NULL AS nvarchar(300)) AS Company")},
    v.Issue,
    v.Department,
    {(hasBranchId ? "v.BranchId" : "CAST(NULL AS int) AS BranchId")},
    {(hasBranch ? "v.Branch" : "CAST(NULL AS nvarchar(300)) AS Branch")},
     v.ResponsiblePerson,
     {assignedToEmpIdExpr} AS AssignedToEmpId,
     v.TicketAgeDays,
     {(hasIdleDays ? "v.IdleDays" : "CAST(0 AS int) AS IdleDays")},
     v.LastContactAt,
     v.Status,
     v.Priority,
     v.CallerName,
     v.IssueType,
    v.CreatedAt,
    v.UpdatedAt,
    v.SolvedAt
FROM dbo.vw_Call_TicketList v
{joinCallTicket}
 WHERE v.Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed')
   AND (
         v.Priority IN ('High', 'Critical')
      OR NULLIF(LTRIM(RTRIM(v.ResponsiblePerson)), '') IS NULL
      OR {overdueExpr} >= @OverdueDays
   )
 ORDER BY
     CASE v.Priority
         WHEN 'Critical' THEN 1
        WHEN 'High' THEN 2
        WHEN 'Medium' THEN 3
        WHEN 'Low' THEN 4
         ELSE 5
     END,
     {overdueExpr} DESC,
     v.CreatedAt DESC;";

                var rows = await connection.QueryAsync<CallTicketListItem>(sql, new { MaxRows = maxRows, OverdueDays = overdueDays });
                return rows.ToList();
            }
        }

        public async Task<List<CallTicketListItem>> GetTicketsAsync(string statusFilter, string searchText, int maxRows = 500)
        {
            // statusFilter: All/Pending/Escalated/Overdue/Solved
            var normalized = (statusFilter ?? "All").Trim();

            var overdueDays = 3;
            if (string.Equals(normalized, "Overdue", StringComparison.OrdinalIgnoreCase))
                overdueDays = await GetOverdueDaysAsync(defaultDays: 3);

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var capabilities = await GetTicketListViewCapabilitiesAsync(connection);
                var overdueExpr = capabilities.HasIdleDaysColumn ? "IdleDays" : "TicketAgeDays";

                string where = "WHERE 1=1";
                if (!string.Equals(normalized, "All", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(normalized, "Overdue", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(normalized, "Solved", StringComparison.OrdinalIgnoreCase))
                    {
                        where += " AND Status IN ('Solved', 'Resolved (Temporary)', 'Closed')";
                    }
                    else if (string.Equals(normalized, "In Progress", StringComparison.OrdinalIgnoreCase))
                    {
                        where += " AND (Status = @Status OR Status IN ('Waiting on Vendor', 'Waiting on Department'))";
                    }
                    else
                    {
                        where += " AND Status = @Status";
                    }
                }

                if (string.Equals(normalized, "Overdue", StringComparison.OrdinalIgnoreCase))
                {
                    where += $" AND Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed') AND {overdueExpr} >= @OverdueDays";
                }

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    where += @"
 AND (
        (@HasTicketIdSearch = 1 AND TicketId = @TicketIdSearch)
     OR
        TicketCode LIKE @Search
     OR Issue LIKE @Search
     OR CallerName LIKE @Search
     OR Department LIKE @Search
     OR ResponsiblePerson LIKE @Search
 )";
                }

                var sql = $@"
SELECT TOP (@MaxRows) *
FROM dbo.vw_Call_TicketList
{where}
ORDER BY CreatedAt DESC;";

                var rows = await connection.QueryAsync<CallTicketListItem>(
                    sql,
                    new
                    {
                        MaxRows = maxRows,
                        Status = normalized,
                        OverdueDays = overdueDays,
                        Search = "%" + (searchText ?? string.Empty).Trim() + "%",
                        HasTicketIdSearch = int.TryParse((searchText ?? string.Empty).Trim(), out var ticketIdSearch) && ticketIdSearch > 0 ? 1 : 0,
                        TicketIdSearch = int.TryParse((searchText ?? string.Empty).Trim(), out var ticketIdSearch2) ? ticketIdSearch2 : 0
                    });

                return rows.ToList();
            }
        }

        public async Task<List<CallTicketListItem>> GetPendingTicketsPageAsync(
            string statusFilter,
            string searchText,
            string assigneeName,
            int? companyId,
            int? branchId,
            bool unassignedOnly,
            int overdueDays,
            int pageIndex,
            int pageSize)
            => (await GetPendingTicketsPageResultAsync(
                statusFilter,
                searchText,
                assigneeName,
                companyId,
                branchId,
                unassignedOnly,
                overdueDays,
                pageIndex,
                pageSize)).Items;

        public async Task<CallTicketPageResult> GetPendingTicketsPageResultAsync(
            string statusFilter,
            string searchText,
            string assigneeName,
            int? companyId,
            int? branchId,
            bool unassignedOnly,
            int overdueDays,
            int pageIndex,
            int pageSize)
        {
            if (pageIndex < 1)
                pageIndex = 1;

            if (pageSize < 1)
                pageSize = 1;

            if (pageSize > 500)
                pageSize = 500;

            var skip = (pageIndex - 1) * pageSize;
            var takePlusOne = pageSize + 1;
            var startRow = skip + 1;
            var endRow = skip + takePlusOne;

            var normalized = (statusFilter ?? "All").Trim();
            var q = (searchText ?? string.Empty).Trim();
            var hasQuery = !string.IsNullOrWhiteSpace(q);

            var hasAssigneeName = !string.IsNullOrWhiteSpace(assigneeName);
            var isOverdueFilter = string.Equals(normalized, "Overdue", StringComparison.OrdinalIgnoreCase);

            // A portal ticket is incoming until IT triages it by assigning it or moving it out of Pending.
            // Once triaged it becomes part of the normal Pending Tickets workspace.
            string where = @"WHERE NOT (
    TicketSource = 'Portal'
    AND Status = 'Pending'
    AND AssignedToEmpId IS NULL
)";

                if (string.Equals(normalized, "All", StringComparison.OrdinalIgnoreCase))
                {
                    where += " AND Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed')";
                }
                else if (isOverdueFilter)
                {
                    where += " AND Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed')";
                }
                else if (string.Equals(normalized, "Solved", StringComparison.OrdinalIgnoreCase))
                {
                    where += " AND Status IN ('Solved', 'Resolved (Temporary)', 'Closed')";
                }
                else if (string.Equals(normalized, "In Progress", StringComparison.OrdinalIgnoreCase))
                {
                    where += " AND (Status = @Status OR Status IN ('Waiting on Vendor', 'Waiting on Department'))";
                }
                else
                {
                    where += " AND Status = @Status";
                }

            if (unassignedOnly)
            {
                where += " AND NULLIF(LTRIM(RTRIM(ResponsiblePerson)), '') IS NULL";
            }
            else if (hasAssigneeName)
            {
                where += " AND UPPER(LTRIM(RTRIM(ResponsiblePerson))) = UPPER(@AssigneeName)";
            }

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var capabilities = await GetTicketListViewCapabilitiesAsync(connection);
                var hasComIdColumn = capabilities.HasComIdColumn;
                var hasCompanyColumn = capabilities.HasCompanyColumn;
                var hasBranchIdColumn = capabilities.HasBranchIdColumn;
                var hasBranchNameColumn = capabilities.HasBranchNameColumn;
                var overdueExpr = capabilities.HasIdleDaysColumn ? "IdleDays" : "TicketAgeDays";

                if (isOverdueFilter)
                {
                    where += $" AND {overdueExpr} >= @OverdueDays";
                }

                if (hasQuery)
                {
                    where += @"
 AND (
        (@HasTicketIdSearch = 1 AND TicketId = @TicketIdSearch)
     OR TicketCode LIKE @Search
     OR Issue LIKE @Search
     OR CallerName LIKE @Search
     OR Department LIKE @Search
     OR ResponsiblePerson LIKE @Search" + (hasBranchNameColumn ? "\n     OR Branch LIKE @Search" : string.Empty) + @"
     " + (hasCompanyColumn ? "OR Company LIKE @Search\n" : string.Empty) + @"
 )";
                }

                if (hasComIdColumn && companyId.HasValue && companyId.Value > 0)
                {
                    where += " AND ComId = @ComId";
                }

                if (hasBranchIdColumn && branchId.HasValue && branchId.Value > 0)
                {
                    where += " AND BranchId = @BranchId";
                }

                var sql = $@"
WITH TicketRows AS
(
    SELECT
        *,
        COUNT(1) OVER() AS TotalCount,
        ROW_NUMBER() OVER (ORDER BY CreatedAt DESC, TicketId DESC) AS rn
    FROM dbo.vw_Call_TicketList
    {where}
)
SELECT *
FROM TicketRows
WHERE rn BETWEEN @StartRow AND @EndRow
ORDER BY rn;";

                var rows = (await connection.QueryAsync<CallTicketListItem>(
                    sql,
                    new
                    {
                        Status = normalized,
                        OverdueDays = overdueDays < 1 ? 1 : overdueDays,
                        AssigneeName = assigneeName,
                        ComId = companyId,
                        BranchId = branchId,
                        Search = "%" + q + "%",
                        HasTicketIdSearch = int.TryParse(q, out var ticketIdSearch) && ticketIdSearch > 0 ? 1 : 0,
                        TicketIdSearch = int.TryParse(q, out var ticketIdSearch2) ? ticketIdSearch2 : 0,
                        StartRow = startRow,
                        EndRow = endRow
                    })).ToList();

                var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;
                var hasNext = rows.Count > pageSize;
                var items = rows.Take(pageSize).ToList();

                return new CallTicketPageResult
                {
                    Items = items,
                    TotalCount = Math.Max(0, totalCount),
                    HasNext = hasNext
                };
            }
        }

        public async Task<int> GetPendingTicketsCountAsync(
            string statusFilter,
            string searchText,
            string assigneeName,
            int? companyId,
            int? branchId,
            bool unassignedOnly,
            int overdueDays)
            => (await GetPendingTicketsPageResultAsync(
                statusFilter,
                searchText,
                assigneeName,
                companyId,
                branchId,
                unassignedOnly,
                overdueDays,
                pageIndex: 1,
                pageSize: 1)).TotalCount;

        public async Task<List<CallTicketListItem>> GetRecentlyResolvedTicketsPageAsync(
            string searchText,
            string assigneeName,
            int? companyId,
            int? branchId,
            bool unassignedOnly,
            int pageIndex,
            int pageSize)
            => (await GetRecentlyResolvedTicketsPageResultAsync(
                searchText,
                assigneeName,
                companyId,
                branchId,
                unassignedOnly,
                pageIndex,
                pageSize)).Items;

        public async Task<CallTicketPageResult> GetRecentlyResolvedTicketsPageResultAsync(
            string searchText,
            string assigneeName,
            int? companyId,
            int? branchId,
            bool unassignedOnly,
            int pageIndex,
            int pageSize)
        {
            if (pageIndex < 1)
                pageIndex = 1;

            if (pageSize < 1)
                pageSize = 1;

            if (pageSize > 500)
                pageSize = 500;

            var skip = (pageIndex - 1) * pageSize;
            var takePlusOne = pageSize + 1;
            var startRow = skip + 1;
            var endRow = skip + takePlusOne;

            var q = (searchText ?? string.Empty).Trim();
            var hasQuery = !string.IsNullOrWhiteSpace(q);
            var hasAssigneeName = !string.IsNullOrWhiteSpace(assigneeName);

            string where = "WHERE Status IN ('Solved', 'Resolved (Temporary)', 'Closed')";

            if (unassignedOnly)
            {
                where += " AND NULLIF(LTRIM(RTRIM(ResponsiblePerson)), '') IS NULL";
            }
            else if (hasAssigneeName)
            {
                where += " AND UPPER(LTRIM(RTRIM(ResponsiblePerson))) = UPPER(@AssigneeName)";
            }

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var capabilities = await GetTicketListViewCapabilitiesAsync(connection);
                var hasComIdColumn = capabilities.HasComIdColumn;
                var hasCompanyColumn = capabilities.HasCompanyColumn;
                var hasBranchIdColumn = capabilities.HasBranchIdColumn;
                var hasBranchNameColumn = capabilities.HasBranchNameColumn;

                if (hasQuery)
                {
                    where += @"
 AND (
        (@HasTicketIdSearch = 1 AND TicketId = @TicketIdSearch)
     OR TicketCode LIKE @Search
     OR Issue LIKE @Search
     OR CallerName LIKE @Search
     OR Department LIKE @Search
     OR ResponsiblePerson LIKE @Search" + (hasBranchNameColumn ? "\n     OR Branch LIKE @Search" : string.Empty) + @"
     " + (hasCompanyColumn ? "OR Company LIKE @Search\n" : string.Empty) + @"
 )";
                }

                if (hasComIdColumn && companyId.HasValue && companyId.Value > 0)
                {
                    where += " AND ComId = @ComId";
                }

                if (hasBranchIdColumn && branchId.HasValue && branchId.Value > 0)
                {
                    where += " AND BranchId = @BranchId";
                }

                var sql = $@"
WITH TicketRows AS
(
    SELECT
        *,
        COUNT(1) OVER() AS TotalCount,
        ROW_NUMBER() OVER (ORDER BY ISNULL(SolvedAt, UpdatedAt) DESC, TicketId DESC) AS rn
    FROM dbo.vw_Call_TicketList
    {where}
)
SELECT *
FROM TicketRows
WHERE rn BETWEEN @StartRow AND @EndRow
ORDER BY rn;";

                var rows = (await connection.QueryAsync<CallTicketListItem>(
                    sql,
                    new
                    {
                        AssigneeName = assigneeName,
                        ComId = companyId,
                        BranchId = branchId,
                        Search = "%" + q + "%",
                        HasTicketIdSearch = int.TryParse(q, out var ticketIdSearch) && ticketIdSearch > 0 ? 1 : 0,
                        TicketIdSearch = int.TryParse(q, out var ticketIdSearch2) ? ticketIdSearch2 : 0,
                        StartRow = startRow,
                        EndRow = endRow
                    })).ToList();

                var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;
                var hasNext = rows.Count > pageSize;
                var items = rows.Take(pageSize).ToList();

                return new CallTicketPageResult
                {
                    Items = items,
                    TotalCount = Math.Max(0, totalCount),
                    HasNext = hasNext
                };
            }
        }

        public async Task<CallTicketPageResult> GetIncomingPortalTicketsPageResultAsync(
            string searchText,
            string statusFilter,
            string priorityFilter,
            bool unassignedOnly,
            int minAgeDays,
            int pageIndex,
            int pageSize)
        {
            if (pageIndex < 1) pageIndex = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 500) pageSize = 500;

            var skip = (pageIndex - 1) * pageSize;
            var takePlusOne = pageSize + 1;
            var startRow = skip + 1;
            var endRow = skip + takePlusOne;

            var q = (searchText ?? string.Empty).Trim();
            var hasQuery = !string.IsNullOrWhiteSpace(q);

            string where = @"WHERE TicketSource = 'Portal'
  AND Status = 'Pending'
  AND AssignedToEmpId IS NULL";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var capabilities = await GetTicketListViewCapabilitiesAsync(connection);

                if (hasQuery)
                {
                    where += @"
 AND (
        (@HasTicketIdSearch = 1 AND TicketId = @TicketIdSearch)
     OR TicketCode LIKE @Search
     OR Issue LIKE @Search
     OR CallerName LIKE @Search
     OR Department LIKE @Search
     OR ResponsiblePerson LIKE @Search" + (capabilities.HasBranchNameColumn ? "\n     OR Branch LIKE @Search" : string.Empty) + @"
     " + (capabilities.HasCompanyColumn ? "OR Company LIKE @Search\n" : string.Empty) + @"
 )";
                }

                if (!string.IsNullOrWhiteSpace(statusFilter) && !string.Equals(statusFilter, "All", StringComparison.OrdinalIgnoreCase))
                {
                    if (statusFilter.Contains(","))
                    {
                        var parts = statusFilter.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => s.Length > 0)
                            .ToList();
                        if (parts.Count > 0)
                        {
                            where += "\n AND Status IN (" + string.Join(", ", parts.Select(p => "'" + p.Replace("'", "''") + "'")) + ")";
                        }
                    }
                    else
                    {
                        where += "\n AND Status = @StatusFilter";
                    }
                }

                if (!string.IsNullOrWhiteSpace(priorityFilter) && !string.Equals(priorityFilter, "All", StringComparison.OrdinalIgnoreCase))
                {
                    where += "\n AND Priority = @PriorityFilter";
                }

                if (unassignedOnly)
                {
                    where += "\n AND AssignedToEmpId IS NULL";
                }

                if (minAgeDays > 0)
                {
                    where += "\n AND DATEDIFF(DAY, CreatedAt, GETDATE()) >= @MinAgeDays";
                }

                var sql = $@"
WITH TicketRows AS
(
    SELECT
        *,
        COUNT(1) OVER() AS TotalCount,
        ROW_NUMBER() OVER (ORDER BY CreatedAt DESC, TicketId DESC) AS rn
    FROM dbo.vw_Call_TicketList
    {where}
)
SELECT *
FROM TicketRows
WHERE rn BETWEEN @StartRow AND @EndRow
ORDER BY rn;";

                var rows = (await connection.QueryAsync<CallTicketListItem>(
                    sql,
                    new
                    {
                        Search = "%" + q + "%",
                        HasTicketIdSearch = int.TryParse(q, out var ticketIdSearch) && ticketIdSearch > 0 ? 1 : 0,
                        TicketIdSearch = int.TryParse(q, out var ticketIdSearch2) ? ticketIdSearch2 : 0,
                        StatusFilter = statusFilter,
                        PriorityFilter = priorityFilter,
                        MinAgeDays = minAgeDays,
                        StartRow = startRow,
                        EndRow = endRow
                    })).ToList();

                var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;
                var hasNext = rows.Count > pageSize;
                var items = rows.Take(pageSize).ToList();

                return new CallTicketPageResult
                {
                    Items = items,
                    TotalCount = Math.Max(0, totalCount),
                    HasNext = hasNext
                };
            }
        }

        public async Task<IncomingTicketsSummary> GetIncomingPortalTicketsSummaryAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var sql = @"
SELECT
    SUM(CASE WHEN Status = 'Pending' THEN 1 ELSE 0 END) AS Pending,
    SUM(CASE WHEN Status = 'In Progress' THEN 1 ELSE 0 END) AS InProgress,
    SUM(CASE WHEN Status = 'Solved' THEN 1 ELSE 0 END) AS Solved,
    SUM(CASE WHEN Status = 'Escalated' THEN 1 ELSE 0 END) AS Escalated,
    SUM(CASE WHEN Priority = 'Critical' THEN 1 ELSE 0 END) AS Critical,
    SUM(CASE WHEN Priority = 'High' THEN 1 ELSE 0 END) AS High,
    SUM(CASE WHEN AssignedToEmpId IS NULL THEN 1 ELSE 0 END) AS Unassigned,
    SUM(CASE WHEN DATEDIFF(DAY, CreatedAt, GETDATE()) >= 7 THEN 1 ELSE 0 END) AS OldTickets,
    COUNT(1) AS TotalCount
FROM dbo.vw_Call_TicketList
WHERE TicketSource = 'Portal'
  AND Status = 'Pending'
  AND AssignedToEmpId IS NULL;";

                var row = await connection.QuerySingleOrDefaultAsync<IncomingTicketsSummary>(sql);
                return row ?? new IncomingTicketsSummary();
            }
        }

        public async Task<int> GetRecentlyResolvedTicketsCountAsync(
            string searchText,
            string assigneeName,
            int? companyId,
            int? branchId,
            bool unassignedOnly)
            => (await GetRecentlyResolvedTicketsPageResultAsync(
                searchText,
                assigneeName,
                companyId,
                branchId,
                unassignedOnly,
                pageIndex: 1,
                pageSize: 1)).TotalCount;

        public async Task<CallTicketListItem> GetTicketByIdAsync(int ticketId)
        {
            if (ticketId <= 0) throw new ArgumentOutOfRangeException(nameof(ticketId));

            const string sql = @"SELECT TOP 1 * FROM dbo.vw_Call_TicketList WHERE TicketId = @TicketId;";
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                try
                {
                    return await connection.QuerySingleOrDefaultAsync<CallTicketListItem>(sql, new { TicketId = ticketId });
                }
                catch
                {
                    // Fallback if the view doesn't exist in older environments.
                    const string fallbackSql = @"SELECT TOP 1 TicketId, TicketCode, Issue, NULL AS Department, NULL AS ResponsiblePerson, 0 AS TicketAgeDays, LastContactAt, Status, Priority, CallerName, IssueType, CreatedAt, UpdatedAt, SolvedAt FROM dbo.CallTicket WHERE TicketId = @TicketId;";
                    return await connection.QuerySingleOrDefaultAsync<CallTicketListItem>(fallbackSql, new { TicketId = ticketId });
                }
            }
        }

        public async Task<List<CallTicketListItem>> GetRecentlySolvedAsync(int maxRows = 50)
        {
            const string sql = @"
SELECT TOP (@MaxRows) *
FROM dbo.vw_Call_TicketList
WHERE Status IN ('Solved', 'Resolved (Temporary)', 'Closed')
ORDER BY SolvedAt DESC, UpdatedAt DESC;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync<CallTicketListItem>(sql, new { MaxRows = maxRows });
                return rows.ToList();
            }
        }

        // --- DASHBOARD DRILL-DOWN METHODS ---

        public async Task<List<CallTicketListItem>> GetOpenTicketsListAsync(int maxRows = 100)
        {
            const string sql = @"
SELECT TOP (@MaxRows) *
FROM dbo.vw_Call_TicketList
WHERE Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed')
ORDER BY CreatedAt DESC;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync<CallTicketListItem>(sql, new { MaxRows = maxRows });
                return rows.ToList();
            }
        }

        public async Task<List<CallTicketListItem>> GetCriticalTicketsListAsync(int maxRows = 100)
        {
            const string sql = @"
SELECT TOP (@MaxRows) *
FROM dbo.vw_Call_TicketList
WHERE Priority = 'Critical' AND Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed')
ORDER BY CreatedAt DESC;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync<CallTicketListItem>(sql, new { MaxRows = maxRows });
                return rows.ToList();
            }
        }

        public async Task<List<CallTicketListItem>> GetTodaysTicketsListAsync(int maxRows = 100)
        {
            const string sql = @"
SELECT TOP (@MaxRows) *
FROM dbo.vw_Call_TicketList
WHERE CAST(CreatedAt AS DATE) = CAST(SYSUTCDATETIME() AS DATE)
ORDER BY CreatedAt DESC;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync<CallTicketListItem>(sql, new { MaxRows = maxRows });
                return rows.ToList();
            }
        }

        public async Task<CallTicketListItem> CreateTicketAsync(
            int? comId,
            int? deptId,
            int? branchId,
            string callerName,
            string issue,
            string providedSolution,
            string issueType,
            string priority,
            int? assignedToEmpId,
            int? createdByUserId,
            DateTime? createdAtUtc = null)
        {
            // UI often passes 0 for "(None)/(Unassigned)" placeholder values.
            // Normalize to NULL so stored procedures don't treat 0 as an invalid foreign key.
            if (comId.HasValue && comId.Value <= 0) comId = null;
            if (deptId.HasValue && deptId.Value <= 0) deptId = null;
            if (branchId.HasValue && branchId.Value <= 0) branchId = null;
            if (assignedToEmpId.HasValue && assignedToEmpId.Value <= 0) assignedToEmpId = null;

            if (createdAtUtc.HasValue)
            {
                var actualUtc = AppTime.AssumeUtc(createdAtUtc.Value);
                if (actualUtc > AppTime.UtcNow.AddMinutes(1))
                    throw new InvalidOperationException("Ticket date/time cannot be in the future.");
            }

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var useEmployeeAssignment = await EmployeeAssignmentEnabledAsync();
                var supportsBranch = await connection.ExecuteScalarAsync<int>(@"
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM sys.parameters p
    INNER JOIN sys.objects o ON o.object_id = p.object_id
    INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
    WHERE s.name = 'dbo'
      AND o.name = 'sp_Call_CreateTicket'
      AND o.type = 'P'
      AND p.name = '@BranchId'
) THEN 1 ELSE 0 END;");

                var parameters = new DynamicParameters();
                parameters.Add("ComId", comId);
                parameters.Add("DeptId", deptId);
                if (supportsBranch == 1)
                    parameters.Add("BranchId", branchId);
                parameters.Add("CallerName", callerName);
                parameters.Add("Issue", issue);
                parameters.Add("ProvidedSolution", providedSolution);
                parameters.Add("IssueType", issueType);
                parameters.Add("Priority", priority);
                parameters.Add("CreatedByUserId", createdByUserId);

                if (useEmployeeAssignment)
                {
                    parameters.Add("AssignedToEmpId", assignedToEmpId);
                }
                else
                {
                    // Backward-compatible: schema not migrated yet; keep assignment unassigned.
                    parameters.Add("AssignedToUserId", (int?)null);
                }

                parameters.Add("TicketSource", "CallIT");

                // Backdate support: CreatedAtUtc and LastContactAtUtc are passed through together
                // so auto-escalation timing remains consistent for backlog entries.
                parameters.Add("CreatedAtUtc", createdAtUtc);
                parameters.Add("LastContactAtUtc", createdAtUtc);

                var result = await connection.QuerySingleAsync<CallTicketListItem>(
                    "dbo.sp_Call_CreateTicket",
                    parameters,
                    commandType: System.Data.CommandType.StoredProcedure);

                return result;
            }
        }

        public async Task AssignTicketEmployeeAsync(int ticketId, int? assignedToEmpId, int? changedByUserId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.ExecuteAsync(
                    "dbo.sp_Call_AssignTicket",
                    new
                    {
                        TicketId = ticketId,
                        AssignedToEmpId = assignedToEmpId,
                        ChangedByUserId = changedByUserId
                    },
                    commandType: System.Data.CommandType.StoredProcedure);
            }
        }

        public async Task<int?> GetTicketAssignedEmployeeIdAsync(int ticketId)
        {
            if (!await EmployeeAssignmentEnabledAsync())
                return null;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await connection.ExecuteScalarAsync<int?>(
                    "SELECT AssignedToEmpId FROM dbo.CallTicket WHERE TicketId = @TicketId;",
                    new { TicketId = ticketId });
            }
        }

        public async Task<List<CallTicketNoteItem>> GetTicketNotesAsync(int ticketId, int maxRows = 200)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var noteDateColumn = await GetFirstExistingColumnAsync(
                    connection,
                    "dbo.CallTicketNote",
                    new[] { "CreatedAt", "CreatedOn", "CreatedDate", "DateCreated", "Created" });

                var userNameColumn = await GetFirstExistingColumnAsync(
                    connection,
                    "dbo.[User]",
                    new[] { "Name", "Username", "UserName" });

                var dateSelect = noteDateColumn != null ? $"n.[{noteDateColumn}]" : "NULL";
                var orderBy = noteDateColumn != null ? $"n.[{noteDateColumn}] DESC, n.NoteId DESC" : "n.NoteId DESC";
                var userNameSelect = userNameColumn != null ? $"u.[{userNameColumn}] AS CreatedByName" : "NULL AS CreatedByName";

                var sql = $@"
SELECT TOP (@MaxRows)
    n.NoteId,
    n.TicketId,
    n.NoteType,
    n.NoteText,
    {dateSelect} AS CreatedAt,
    n.CreatedByUserId,
    {userNameSelect}
FROM dbo.CallTicketNote n
LEFT JOIN dbo.[User] u ON u.UserId = n.CreatedByUserId
WHERE n.TicketId = @TicketId
ORDER BY {orderBy};";

                var rows = await connection.QueryAsync<CallTicketNoteItem>(sql, new { TicketId = ticketId, MaxRows = maxRows });
                return rows.ToList();
            }
        }

        public async Task<List<CallTicketHistoryItem>> GetTicketHistoryAsync(int ticketId, int maxRows = 200)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var historyDateColumn = await GetFirstExistingColumnAsync(
                    connection,
                    "dbo.CallTicketHistory",
                    new[] { "ChangedAt", "CreatedAt", "DateChanged", "DateCreated", "CreatedOn" });

                var userNameColumn = await GetFirstExistingColumnAsync(
                    connection,
                    "dbo.[User]",
                    new[] { "Name", "Username", "UserName" });

                var dateSelect = historyDateColumn != null ? $"h.[{historyDateColumn}]" : "NULL";
                var orderBy = historyDateColumn != null ? $"h.[{historyDateColumn}] DESC, h.HistoryId DESC" : "h.HistoryId DESC";
                var userNameSelect = userNameColumn != null ? $"u.[{userNameColumn}] AS ChangedByName" : "NULL AS ChangedByName";

                var sql = $@"
SELECT TOP (@MaxRows)
    h.HistoryId,
    h.TicketId,
    {dateSelect} AS ChangedAt,
    h.ChangedByUserId,
    {userNameSelect},
    h.FieldName,
    h.OldValue,
    h.NewValue,
    h.Note
FROM dbo.CallTicketHistory h
LEFT JOIN dbo.[User] u ON u.UserId = h.ChangedByUserId
WHERE h.TicketId = @TicketId
ORDER BY {orderBy};";

                var rows = await connection.QueryAsync<CallTicketHistoryItem>(sql, new { TicketId = ticketId, MaxRows = maxRows });
                return rows.ToList();
            }
        }

        public async Task LogTicketResolutionAsync(int ticketId, string resolutionType, string remarks, int? changedByUserId)
        {
            resolutionType = (resolutionType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(resolutionType))
                throw new ArgumentException("Resolution type is required.", nameof(resolutionType));

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var tx = connection.BeginTransaction())
                {
                    try
                    {
                        await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ResolutionType", null, resolutionType, remarks);

                        var snap = await GetTicketSnapshotAsync(connection, tx, ticketId);
                        if (snap != null)
                        {
                            if (!string.IsNullOrWhiteSpace(snap.Department))
                                await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ResolutionDepartment", null, snap.Department, null);
                            if (!string.IsNullOrWhiteSpace(snap.ResponsiblePerson))
                                await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ResolutionResponsiblePerson", null, snap.ResponsiblePerson, null);
                        }

                        if (!string.IsNullOrWhiteSpace(remarks))
                        {
                            await connection.ExecuteAsync(
                                "dbo.sp_Call_AddTicketNote",
                                new
                                {
                                    TicketId = ticketId,
                                    NoteType = "Resolution",
                                    NoteText = $"{resolutionType}: {remarks}",
                                    CreatedByUserId = changedByUserId
                                },
                                commandType: System.Data.CommandType.StoredProcedure,
                                transaction: tx);
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task ApplyTicketReplacementAsync(
            int ticketId,
            int oldItemId,
            int newItemId,
            int quantity,
            string remarks,
            int? changedByUserId,
            int oldItemConditionId,
            string oldItemConditionRemarks,
            string oldItemRepairAction)
        {
            if (oldItemId <= 0) throw new ArgumentOutOfRangeException(nameof(oldItemId));
            if (newItemId <= 0) throw new ArgumentOutOfRangeException(nameof(newItemId));
            if (oldItemId == newItemId) throw new ArgumentException("Old and new items must be different.");
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (!changedByUserId.HasValue || changedByUserId.Value <= 0)
                throw new InvalidOperationException("ChangedByUserId is required for replacement.");
            if (oldItemConditionId <= 0) throw new ArgumentOutOfRangeException(nameof(oldItemConditionId));

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var tx = connection.BeginTransaction())
                {
                    try
                    {
                        var safeOldRepairAction = string.IsNullOrWhiteSpace(oldItemRepairAction) ? "Repaired" : oldItemRepairAction.Trim();
                        var returnOldToStock = !safeOldRepairAction.Equals("Unrepaired", StringComparison.OrdinalIgnoreCase);

                        var oldItemAffectsInventory = await connection.ExecuteScalarAsync<bool?>(
                            new CommandDefinition("SELECT AffectsInventory FROM dbo.Item WHERE ItemId = @ItemId", new { ItemId = oldItemId }, tx));
                        var newItemAffectsInventory = await connection.ExecuteScalarAsync<bool?>(
                            new CommandDefinition("SELECT AffectsInventory FROM dbo.Item WHERE ItemId = @ItemId", new { ItemId = newItemId }, tx));
                        if (!oldItemAffectsInventory.HasValue)
                            throw new InvalidOperationException("Old item not found.");
                        if (!newItemAffectsInventory.HasValue)
                            throw new InvalidOperationException("New item not found.");
                        if (!oldItemAffectsInventory.Value || !newItemAffectsInventory.Value)
                            throw new InvalidOperationException("Replacement items must affect inventory stock.");

                        // Concurrency-safe: do NOT rely on a separate "read then write" stock check.
                        // Two operators could both pass a read check and both subtract, causing negative stock.
                        // Instead, enforce the guard in the UPDATE itself and validate affected rows.

                        var oldEntryType = InventoryHelper.DetermineEntryType(oldItemAffectsInventory.Value, quantity);
                        var newEntryType = InventoryHelper.DetermineEntryType(newItemAffectsInventory.Value, -quantity);
                        var nowUtc = DateTime.UtcNow;

                        // Old unit pullout handling (return to stock vs keep out-of-stock for repair)
                        if (returnOldToStock)
                        {
                            await connection.ExecuteAsync(
                                @"INSERT INTO dbo.Inventory (Description, EntryType, Quantity, DatePosted, PostedBy, ReqId, ItemId)
VALUES (@Description, @EntryType, @Quantity, @NowUtc, @PostedBy, NULL, @ItemId);",
                                new
                                {
                                    Description = $"Call ticket replacement pullout (Ticket {ticketId})",
                                    EntryType = oldEntryType,
                                    Quantity = quantity,
                                    PostedBy = changedByUserId.Value,
                                    ItemId = oldItemId,
                                    NowUtc = nowUtc
                                },
                                transaction: tx);

                            await connection.ExecuteAsync(
                                @"UPDATE dbo.Item
SET StockOnHand = ISNULL(StockOnHand, 0) + @Quantity,
    DateModified = @NowUtc,
    ModifiedBy = @ModifiedBy
WHERE ItemId = @ItemId;",
                                new { Quantity = quantity, ModifiedBy = changedByUserId.Value, ItemId = oldItemId, NowUtc = nowUtc },
                                transaction: tx);
                        }

                        // Allocate new item from inventory
                        await connection.ExecuteAsync(
                            @"INSERT INTO dbo.Inventory (Description, EntryType, Quantity, DatePosted, PostedBy, ReqId, ItemId)
VALUES (@Description, @EntryType, @Quantity, @NowUtc, @PostedBy, NULL, @ItemId);",
                            new
                            {
                                Description = $"Call ticket replacement allocation (Ticket {ticketId})",
                                EntryType = newEntryType,
                                Quantity = quantity,
                                PostedBy = changedByUserId.Value,
                                ItemId = newItemId,
                                NowUtc = nowUtc
                            },
                            transaction: tx);

                        var newStockAffected = await connection.ExecuteAsync(
                            @"UPDATE dbo.Item
SET StockOnHand = ISNULL(StockOnHand, 0) - @Quantity,
    DateModified = @NowUtc,
    ModifiedBy = @ModifiedBy
WHERE ItemId = @ItemId
  AND ISNULL(StockOnHand, 0) >= @Quantity;",
                            new { Quantity = quantity, ModifiedBy = changedByUserId.Value, ItemId = newItemId, NowUtc = nowUtc },
                            transaction: tx);

                        if (newStockAffected <= 0)
                        {
                            var exists = await connection.ExecuteScalarAsync<int>(
                                new CommandDefinition(
                                    "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Item WHERE ItemId = @ItemId) THEN 1 ELSE 0 END;",
                                    new { ItemId = newItemId },
                                    tx));

                            if (exists <= 0)
                                throw new InvalidOperationException("Replacement item not found.");

                            throw new InvalidOperationException("Not enough stock on hand for the selected replacement item.");
                        }

                        // Update old item condition (required)
                        await connection.ExecuteAsync(
                            @"UPDATE dbo.Item
SET ConditionID = @ConditionId,
    StockOnHand = CASE WHEN @ForceStockZero = 1 THEN 0 ELSE StockOnHand END,
    Remarks = CASE WHEN @Remarks IS NULL OR LTRIM(RTRIM(@Remarks)) = '' THEN Remarks ELSE @Remarks END,
    DateModified = @NowUtc,
    ModifiedBy = @ModifiedBy
WHERE ItemId = @ItemId;",
                            new
                            {
                                ConditionId = oldItemConditionId,
                                ForceStockZero = returnOldToStock ? 0 : 1,
                                Remarks = oldItemConditionRemarks,
                                ModifiedBy = changedByUserId.Value,
                                ItemId = oldItemId,
                                NowUtc = nowUtc
                            },
                            transaction: tx);

                        await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ResolutionType", null, "Replacement", remarks);

                        var snap = await GetTicketSnapshotAsync(connection, tx, ticketId);
                        if (snap != null)
                        {
                            if (!string.IsNullOrWhiteSpace(snap.Department))
                                await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ResolutionDepartment", null, snap.Department, null);
                            if (!string.IsNullOrWhiteSpace(snap.ResponsiblePerson))
                                await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ResolutionResponsiblePerson", null, snap.ResponsiblePerson, null);
                        }

                        await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ReplacementOldItemId", null, oldItemId.ToString(), null);
                        await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ReplacementNewItemId", null, newItemId.ToString(), null);
                        await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ReplacementQty", null, quantity.ToString(), null);
                        await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ReplacementOldItemConditionId", null, oldItemConditionId.ToString(), oldItemConditionRemarks);
                        await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ReplacementOldItemRepairAction", null, safeOldRepairAction, null);

                        var oldText = await GetItemDisplayTextAsync(connection, tx, oldItemId);
                        var newText = await GetItemDisplayTextAsync(connection, tx, newItemId);

                        var noteText =
                            $"Replacement swap completed.\r\n" +
                            $"Old: {oldText}\r\n" +
                            $"New: {newText}\r\n" +
                            $"Old ConditionId: {oldItemConditionId}\r\n" +
                            $"Qty: {quantity}\r\n" +
                            (string.IsNullOrWhiteSpace(remarks) ? "" : $"\r\nRemarks: {remarks}");

                        await connection.ExecuteAsync(
                            "dbo.sp_Call_AddTicketNote",
                            new
                            {
                                TicketId = ticketId,
                                NoteType = "Replacement",
                                NoteText = noteText,
                                CreatedByUserId = changedByUserId
                            },
                            commandType: System.Data.CommandType.StoredProcedure,
                            transaction: tx);

                        // Update the active Set to reflect the physical swap (so Set Details shows the new serial).
                        // Best-effort: if no active set is found for the old unit, we do nothing.
                        string swappedSetCode = null;
                        try
                        {
                            var swappedSet = await TrySwapItemInActiveSetAsync(
                                connection,
                                tx,
                                fromItemId: oldItemId,
                                toItemId: newItemId,
                                quantity: quantity,
                                changedByUserId: changedByUserId.Value,
                                ticketId: ticketId,
                                actionLabel: safeOldRepairAction);

                            swappedSetCode = swappedSet?.SetCode;
                        }
                        catch
                        {
                            // ignore (best-effort set update)
                        }

                        // Best-effort: log replacement movement to ItemAuditTrail (for Audit Movement page).
                        try
                        {
                            var oldSerial = await connection.ExecuteScalarAsync<string>(
                                new CommandDefinition("SELECT TOP 1 SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", new { ItemId = oldItemId }, tx));
                            var newSerial = await connection.ExecuteScalarAsync<string>(
                                new CommandDefinition("SELECT TOP 1 SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", new { ItemId = newItemId }, tx));

                            string createdBy = null;
                            try
                            {
                                createdBy = await connection.ExecuteScalarAsync<string>(
                                    new CommandDefinition("SELECT TOP 1 Name FROM dbo.[User] WHERE UserId = @UserId", new { UserId = changedByUserId.Value }, tx));
                            }
                            catch
                            {
                                createdBy = changedByUserId.Value.ToString();
                            }

                            // Use UTC consistently for audit timestamps written by Call Monitoring.
                            var auditTime = nowUtc;

                            // Keep replacement movement logs atomic: either both entries exist, or neither.
                            const string auditSavepoint = "CallMonitoring_ReplacementAuditTrail";
                            tx.Save(auditSavepoint);

                            // Old unit pulled out from deployed set (returned to IT)
                            var okOld = ItemAuditTrailWriter.TryLogWithResult(connection, tx, new ItemAuditTrailDto
                            {
                                ItemId = oldItemId,
                                SerialNumber = oldSerial,
                                Action = "Call Ticket Replacement Pullout",
                                ActionTime = auditTime,
                                Direction = "IN",
                                Status = safeOldRepairAction,
                                EmployeeName = snap?.ResponsiblePerson,
                                DepartmentName = snap?.Department,
                                ReferenceType = "CallTicket",
                                ReferenceId = ticketId,
                                SetCode = swappedSetCode,
                                Notes = $"Pulled out for replacement • Qty {quantity} • Old: {oldText}",
                                CreatedBy = createdBy ?? "System"
                            }, out _);

                            // New unit allocated to replace the old one (deployed)
                            var okNew = ItemAuditTrailWriter.TryLogWithResult(connection, tx, new ItemAuditTrailDto
                            {
                                ItemId = newItemId,
                                SerialNumber = newSerial,
                                Action = "Call Ticket Replacement Allocation",
                                ActionTime = auditTime,
                                Direction = "OUT",
                                Status = "Completed",
                                EmployeeName = snap?.ResponsiblePerson,
                                DepartmentName = snap?.Department,
                                ReferenceType = "CallTicket",
                                ReferenceId = ticketId,
                                SetCode = swappedSetCode,
                                Notes = $"Allocated as replacement • Qty {quantity} • New: {newText} • Replaced: {oldText}",
                                CreatedBy = createdBy ?? "System"
                            }, out _);

                            if (!okOld || !okNew)
                            {
                                try { tx.Rollback(auditSavepoint); } catch { }
                            }
                        }
                        catch
                        {
                            // ignore (best-effort audit movement logging)
                        }

                        // Best-effort: log repair action to ItemRepairHistory for visibility in "View Repair Items".
                        // This is guarded so Call Monitoring can still function against older inventory DBs.
                        try
                        {
                            if (await CallSchemaGate.TableExistsAsync(connection, "dbo.ItemRepairHistory", tx)
                                && await CallSchemaGate.TableExistsAsync(connection, "dbo.SetItemUpdate", tx))
                            {
                                var oldSerial = await connection.ExecuteScalarAsync<string>(
                                    new CommandDefinition("SELECT TOP 1 SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", new { ItemId = oldItemId }, tx));

                                if (!string.IsNullOrWhiteSpace(oldSerial))
                                {
                                    var conditionName = await connection.ExecuteScalarAsync<string>(
                                        new CommandDefinition("SELECT TOP 1 ConditionName FROM dbo.[Condition] WHERE ConditionID = @ConditionId", new { ConditionId = oldItemConditionId }, tx));
                                    var safeConditionName = string.IsNullOrWhiteSpace(conditionName)
                                        ? oldItemConditionId.ToString()
                                        : conditionName.Trim();

                                    // Prefer the actual deployed SetCode so Movement Details can show the correct Set context.
                                    var safeSetCode = swappedSetCode ?? $"TCK-{ticketId}";
                                    var safeRemark = string.IsNullOrWhiteSpace(remarks)
                                        ? $"Call ticket replacement pullout (Ticket {ticketId})"
                                        : $"Call ticket replacement pullout (Ticket {ticketId}) | {remarks.Trim()}";

                                    const string insertUpdateSql = @"
INSERT INTO dbo.SetItemUpdate
    (SetId, SetCode, ItemId, SerialNumber, ModelNumber, PreviousStatus, NewStatus, Remark,
     UpdatedByUserId, UpdatedByName, Source, Processed, ProcessedBy, ProcessedAt)
VALUES
    (NULL, @SetCode, @ItemId, @SerialNumber, NULL, @PreviousStatus, @NewStatus, @Remark,
     @UpdatedByUserId, NULL, @Source, 1, NULL, @NowUtc);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                                    int updateId;
                                    const string repairBridgeSavepoint = "CallMonitoring_RepairHistoryBridge";
                                    tx.Save(repairBridgeSavepoint);

                                    try
                                    {
                                        using (var cmd = new SqlCommand(insertUpdateSql, connection, tx))
                                        {
                                            cmd.Parameters.AddWithValue("@SetCode", safeSetCode);
                                            cmd.Parameters.AddWithValue("@ItemId", oldItemId);
                                            cmd.Parameters.AddWithValue("@SerialNumber", oldSerial);
                                            cmd.Parameters.AddWithValue("@PreviousStatus", "Replacement Pullout");
                                            cmd.Parameters.AddWithValue("@NewStatus", safeOldRepairAction);
                                            cmd.Parameters.AddWithValue("@Remark", (object)safeRemark ?? DBNull.Value);
                                            cmd.Parameters.AddWithValue("@UpdatedByUserId", changedByUserId.Value.ToString());
                                            cmd.Parameters.AddWithValue("@Source", "CallMonitoring");
                                            cmd.Parameters.AddWithValue("@NowUtc", nowUtc);
                                            updateId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                                        }

                                    const string insertHistorySql = @"
INSERT INTO dbo.ItemRepairHistory
    (UpdateId, ItemId, SerialNumber, SetId, SetCode,
     PreviousStatus, NewStatus,
     ConditionId, ConditionName, RepairAction,
     Remark, CreatedAt, ProcessedByUserId, ProcessedByName)
VALUES
    (@UpdateId, @ItemId, @SerialNumber, NULL, @SetCode,
     @PreviousStatus, @NewStatus,
     @ConditionId, @ConditionName, @RepairAction,
     @Remark, @NowUtc, @ProcessedByUserId, NULL);";

                                        using (var cmd = new SqlCommand(insertHistorySql, connection, tx))
                                        {
                                            cmd.Parameters.AddWithValue("@UpdateId", updateId);
                                            cmd.Parameters.AddWithValue("@ItemId", oldItemId);
                                            cmd.Parameters.AddWithValue("@SerialNumber", oldSerial);
                                            cmd.Parameters.AddWithValue("@SetCode", safeSetCode);
                                            cmd.Parameters.AddWithValue("@PreviousStatus", "Replacement Pullout");
                                            cmd.Parameters.AddWithValue("@NewStatus", safeOldRepairAction);
                                            cmd.Parameters.AddWithValue("@ConditionId", oldItemConditionId);
                                            cmd.Parameters.AddWithValue("@ConditionName", safeConditionName);
                                            cmd.Parameters.AddWithValue("@RepairAction", safeOldRepairAction);
                                            cmd.Parameters.AddWithValue("@Remark", (object)safeRemark ?? DBNull.Value);
                                            cmd.Parameters.AddWithValue("@ProcessedByUserId", changedByUserId.Value);
                                            cmd.Parameters.AddWithValue("@NowUtc", nowUtc);
                                            await cmd.ExecuteNonQueryAsync();
                                        }
                                    }
                                    catch
                                    {
                                        // Ensure SetItemUpdate + ItemRepairHistory bridge stays all-or-nothing.
                                        try { tx.Rollback(repairBridgeSavepoint); } catch { }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // ignore (best-effort audit trail)
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        private sealed class ActiveSetPick
        {
            public int SetId { get; set; }
            public string SetCode { get; set; }
            public DateTime? CreatedAt { get; set; }
        }

        private static async Task<ActiveSetPick> TrySwapItemInActiveSetAsync(
            SqlConnection connection,
            SqlTransaction tx,
            int fromItemId,
            int toItemId,
            int quantity,
            int changedByUserId,
            int ticketId,
            string actionLabel)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (fromItemId <= 0) return null;
            if (toItemId <= 0) return null;
            if (fromItemId == toItemId) return null;
            if (quantity < 1) quantity = 1;

            const string swapSavepoint = "CallMonitoring_SetSwap";

            // IMPORTANT: some DBs refer to the reserved Set table as dbo.[Set]; gate both formats.
            var setTableExists =
                await CallSchemaGate.TableExistsAsync(connection, "dbo.[Set]", tx)
                || await CallSchemaGate.TableExistsAsync(connection, "dbo.Set", tx);
            if (!setTableExists)
            {
                // Don't throw here; replacement should still complete even if Set module isn't present.
                try { await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ReplacementSetSwapOutcome", null, "MissingSetTable", null); } catch { }
                return null;
            }

            // ArchiveStatus is optional. If missing, we still attempt to swap (we just can't exclude archived sets).
            var hasArchiveStatus = await CallSchemaGate.TableExistsAsync(connection, "dbo.ArchiveStatus", tx);

            // Some DB variants may not have these columns; build pick/order defensively.
            var hasSetActive = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.[Set]", "Active", tx);
            var hasSetCreatedAt = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.[Set]", "CreatedAt", tx);
            var hasSetCode = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.[Set]", "SetCode", tx);
            var setOrderBy = hasSetCreatedAt ? "s.CreatedAt DESC, s.SetId DESC" : "s.SetId DESC";
            var setSelectCode = hasSetCode ? "s.SetCode AS SetCode" : "CAST(NULL AS nvarchar(50)) AS SetCode";
            var setSelectCreatedAt = hasSetCreatedAt ? "s.CreatedAt AS CreatedAt" : "CAST(NULL AS datetime2) AS CreatedAt";
            var setActiveWhere = hasSetActive ? " AND s.Active = 1" : string.Empty;
            var archiveJoin = hasArchiveStatus
                ? " LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1"
                : string.Empty;
            var archiveWhere = hasArchiveStatus ? " AND archS.EntityId IS NULL" : string.Empty;

            ActiveSetPick pick = null;

            // Prefer Request.SetId (the Set Details screen loads from Request)
            if (await CallSchemaGate.TableExistsAsync(connection, "dbo.Request", tx))
            {
                var requestPickSql = $@"
 SELECT TOP 1
     s.SetId,
     {setSelectCode},
     {setSelectCreatedAt}
 FROM dbo.Request r
 INNER JOIN dbo.[Set] s ON s.SetId = r.SetId
 {archiveJoin}
 WHERE r.ItemId = @ItemId
   AND r.SetId IS NOT NULL
 {setActiveWhere}
 {archiveWhere}
 ORDER BY {setOrderBy};";
                pick = await connection.QuerySingleOrDefaultAsync<ActiveSetPick>(
                    requestPickSql,
                    new { ItemId = fromItemId },
                    transaction: tx);
            }

            // Fallback: SetItem snapshot
            if (pick == null && await CallSchemaGate.TableExistsAsync(connection, "dbo.SetItem", tx))
            {
                var setItemPickSql = $@"
 SELECT TOP 1
     s.SetId,
     {setSelectCode},
     {setSelectCreatedAt}
 FROM dbo.SetItem si
 INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
 {archiveJoin}
 WHERE si.ItemId = @ItemId
 {setActiveWhere}
 {archiveWhere}
 ORDER BY {setOrderBy};";
                pick = await connection.QuerySingleOrDefaultAsync<ActiveSetPick>(
                    setItemPickSql,
                    new { ItemId = fromItemId },
                    transaction: tx);
            }

            if (pick == null || pick.SetId <= 0)
            {
                try { await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ReplacementSetSwapOutcome", null, "NoSetFound", null); } catch { }
                return null;
            }

            // Ensure we don't leave partial swap updates behind if something fails.
            // Caller swallows exceptions to keep replacement flows working, so we must rollback ourselves.
            try { tx.Save(swapSavepoint); } catch { }

            var updatedRequests = 0;
            var updatedSetItems = 0;
            var preReqCount = 0;
            var preSetItemCount = 0;

            // Update Request rows (limit by quantity)
            if (await CallSchemaGate.TableExistsAsync(connection, "dbo.Request", tx))
            {
                try
                {
                    preReqCount = await connection.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            "SELECT COUNT(1) FROM dbo.Request WHERE SetId = @SetId AND ItemId = @ItemId",
                            new { SetId = pick.SetId, ItemId = fromItemId },
                            tx));
                }
                catch
                {
                    preReqCount = 0;
                }

                var hasDateModified = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.Request", "DateModified", tx);
                var hasModifiedBy = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.Request", "ModifiedBy", tx);

                var updateRequestSql = (hasDateModified && hasModifiedBy)
                    ? @"
;WITH Target AS
(
    SELECT TOP (@Qty) r.ReqId
    FROM dbo.Request r
    WHERE r.SetId = @SetId
      AND r.ItemId = @FromItemId
    ORDER BY r.ReqId DESC
)
UPDATE r
SET r.ItemId = @ToItemId,
    r.DateModified = SYSUTCDATETIME(),
    r.ModifiedBy = @ModifiedBy
FROM dbo.Request r
INNER JOIN Target t ON t.ReqId = r.ReqId;"
                    : @"
;WITH Target AS
(
    SELECT TOP (@Qty) r.ReqId
    FROM dbo.Request r
    WHERE r.SetId = @SetId
      AND r.ItemId = @FromItemId
    ORDER BY r.ReqId DESC
)
UPDATE r
SET r.ItemId = @ToItemId
FROM dbo.Request r
INNER JOIN Target t ON t.ReqId = r.ReqId;";

                updatedRequests = await connection.ExecuteAsync(
                    updateRequestSql,
                    new
                    {
                        Qty = quantity,
                        SetId = pick.SetId,
                        FromItemId = fromItemId,
                        ToItemId = toItemId,
                        ModifiedBy = changedByUserId
                    },
                    transaction: tx);
            }

            // Update SetItem snapshot rows (limit by quantity)
            if (await CallSchemaGate.TableExistsAsync(connection, "dbo.SetItem", tx))
            {
                try
                {
                    preSetItemCount = await connection.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            "SELECT COUNT(1) FROM dbo.SetItem WHERE SetId = @SetId AND ItemId = @ItemId",
                            new { SetId = pick.SetId, ItemId = fromItemId },
                            tx));
                }
                catch
                {
                    preSetItemCount = 0;
                }

                const string updateSetItemSql = @"
;WITH Target AS
(
    SELECT TOP (@Qty) si.SetItemId
    FROM dbo.SetItem si
    WHERE si.SetId = @SetId
      AND si.ItemId = @FromItemId
    ORDER BY si.SetItemId DESC
)
UPDATE si
SET si.ItemId = @ToItemId
FROM dbo.SetItem si
INNER JOIN Target t ON t.SetItemId = si.SetItemId;";
                updatedSetItems = await connection.ExecuteAsync(
                    updateSetItemSql,
                    new
                    {
                        Qty = quantity,
                        SetId = pick.SetId,
                        FromItemId = fromItemId,
                        ToItemId = toItemId
                    },
                    transaction: tx);
            }

            // If a target table had matching rows but none were updated, rollback to avoid leaving an inconsistent partial swap.
            var requestExpected = preReqCount > 0;
            var setItemExpected = preSetItemCount > 0;
            var requestOk = !requestExpected || updatedRequests > 0;
            var setItemOk = !setItemExpected || updatedSetItems > 0;

            if (!requestOk || !setItemOk)
            {
                try { tx.Rollback(swapSavepoint); } catch { }
                try { await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ReplacementSetSwapOutcome", null, "NoRowsUpdated", null); } catch { }
                return pick;
            }

            // Ticket history + note for traceability
            await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ReplacementSetId", null, pick.SetId.ToString(), pick.SetCode);
            await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ReplacementSetSwapUpdatedRequests", null, updatedRequests.ToString(), null);
            await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ReplacementSetSwapUpdatedSetItems", null, updatedSetItems.ToString(), null);
            await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ReplacementSetSwapOutcome", null, (updatedRequests > 0 || updatedSetItems > 0) ? "Swapped" : "NoRowsUpdated", null);
            if (!string.IsNullOrWhiteSpace(actionLabel))
                await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "ReplacementOldUnitAction", null, actionLabel.Trim(), null);

            try
            {
                await connection.ExecuteAsync(
                    "dbo.sp_Call_AddTicketNote",
                    new
                    {
                        TicketId = ticketId,
                        NoteType = "Replacement",
                        NoteText = $"Set updated: {pick.SetCode ?? pick.SetId.ToString()} (swap ItemId {fromItemId} -> {toItemId})",
                        CreatedByUserId = changedByUserId
                    },
                    commandType: System.Data.CommandType.StoredProcedure,
                    transaction: tx);
            }
            catch
            {
                // ignore note failures (some DBs restrict notes after certain statuses)
            }

            return pick;
        }

        private static async Task<(int NewItemId, int Quantity, bool AlreadyReturned)> GetTemporaryReplacementDetailsAsync(
            SqlConnection connection,
            SqlTransaction tx,
            int ticketId)
        {
            const string sql = @"
SELECT
    NewItemId = (
        SELECT TOP 1 TRY_CONVERT(int, h.NewValue)
        FROM dbo.CallTicketHistory h
        WHERE h.TicketId = @TicketId AND h.FieldName = 'ReplacementNewItemId'
        ORDER BY h.ChangedAt DESC, h.HistoryId DESC
    ),
    Quantity = (
        SELECT TOP 1 TRY_CONVERT(int, h.NewValue)
        FROM dbo.CallTicketHistory h
        WHERE h.TicketId = @TicketId AND h.FieldName = 'ReplacementQty'
        ORDER BY h.ChangedAt DESC, h.HistoryId DESC
    ),
    AlreadyReturned = CASE WHEN EXISTS (
        SELECT 1
        FROM dbo.CallTicketHistory h
        WHERE h.TicketId = @TicketId AND h.FieldName = 'TemporaryReplacementReturnedAt'
    ) THEN 1 ELSE 0 END;";

            var row = await connection.QuerySingleOrDefaultAsync(sql, new { TicketId = ticketId }, tx);
            if (row == null) return (0, 0, false);

            return ((int?)row.NewItemId ?? 0, (int?)row.Quantity ?? 0, ((int?)row.AlreadyReturned ?? 0) == 1);
        }

        private static async Task<(int OldItemId, int NewItemId, int Quantity, bool AlreadyReturned)> GetLatestReplacementSwapDetailsAsync(
            SqlConnection connection,
            SqlTransaction tx,
            int ticketId)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (ticketId <= 0) return (0, 0, 0, false);

            if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketHistory", tx))
                return (0, 0, 0, false);

            try
            {
                const string sql = @"
SELECT
    OldItemId = (
        SELECT TOP 1 TRY_CONVERT(int, h.NewValue)
        FROM dbo.CallTicketHistory h
        WHERE h.TicketId = @TicketId AND h.FieldName = 'ReplacementOldItemId'
        ORDER BY h.ChangedAt DESC, h.HistoryId DESC
    ),
    NewItemId = (
        SELECT TOP 1 TRY_CONVERT(int, h.NewValue)
        FROM dbo.CallTicketHistory h
        WHERE h.TicketId = @TicketId AND h.FieldName = 'ReplacementNewItemId'
        ORDER BY h.ChangedAt DESC, h.HistoryId DESC
    ),
    Quantity = (
        SELECT TOP 1 TRY_CONVERT(int, h.NewValue)
        FROM dbo.CallTicketHistory h
        WHERE h.TicketId = @TicketId AND h.FieldName = 'ReplacementQty'
        ORDER BY h.ChangedAt DESC, h.HistoryId DESC
    ),
    AlreadyReturned = CASE WHEN EXISTS (
        SELECT 1
        FROM dbo.CallTicketHistory h
        WHERE h.TicketId = @TicketId AND h.FieldName = 'TemporaryReplacementReturnedAt'
    ) THEN 1 ELSE 0 END;";

                var row = await connection.QuerySingleOrDefaultAsync(sql, new { TicketId = ticketId }, tx);
                if (row == null) return (0, 0, 0, false);

                var oldId = (int?)row.OldItemId ?? 0;
                var newId = (int?)row.NewItemId ?? 0;
                var qty = (int?)row.Quantity ?? 0;
                var returned = ((int?)row.AlreadyReturned ?? 0) == 1;
                return (oldId, newId, qty, returned);
            }
            catch
            {
                return (0, 0, 0, false);
            }
        }

        public async Task<(int NewItemId, int Quantity, string NewItemText, bool AlreadyReturned)> GetTemporaryReplacementReturnPreviewAsync(int ticketId)
        {
            if (ticketId <= 0) throw new ArgumentOutOfRangeException(nameof(ticketId));

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();

                var status = await connection.ExecuteScalarAsync<string>(
                    "SELECT Status FROM dbo.CallTicket WHERE TicketId = @TicketId",
                    new { TicketId = ticketId });

                if (!string.Equals(status, "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase))
                    return (0, 0, null, false);

                var (newItemId, qty, alreadyReturned) = await GetTemporaryReplacementDetailsAsync(connection, null, ticketId);
                if (newItemId <= 0 || qty <= 0)
                    return (0, 0, null, alreadyReturned);

                var newItemText = await GetItemDisplayTextAsync(connection, null, newItemId);
                return (newItemId, qty, newItemText, alreadyReturned);
            }
        }

        public async Task ReturnTemporaryReplacementAsync(int ticketId, int? changedByUserId)
        {
            if (ticketId <= 0) throw new ArgumentOutOfRangeException(nameof(ticketId));
            if (!changedByUserId.HasValue || changedByUserId.Value <= 0)
                throw new InvalidOperationException("ChangedByUserId is required.");

            string noteTextForUi = null;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var tx = connection.BeginTransaction())
                {
                    try
                    {
                        var status = await connection.ExecuteScalarAsync<string>(
                            new CommandDefinition("SELECT Status FROM dbo.CallTicket WHERE TicketId = @TicketId", new { TicketId = ticketId }, tx));

                        if (!string.Equals(status, "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Ticket is not Resolved (Temporary).");

                        var (newItemId, qty, alreadyReturned) = await GetTemporaryReplacementDetailsAsync(connection, tx, ticketId);
                        if (alreadyReturned)
                            throw new InvalidOperationException("Temporary replacement item has already been returned for this ticket.");
                        if (newItemId <= 0 || qty <= 0)
                            throw new InvalidOperationException("No replacement allocation was found for this ticket.");

                        var newItemAffectsInventory = await connection.ExecuteScalarAsync<bool?>(
                            new CommandDefinition("SELECT AffectsInventory FROM dbo.Item WHERE ItemId = @ItemId", new { ItemId = newItemId }, tx));
                        if (!newItemAffectsInventory.HasValue)
                            throw new InvalidOperationException("Replacement item not found.");
                        if (!newItemAffectsInventory.Value)
                            throw new InvalidOperationException("Replacement item must affect inventory stock.");

                        var entryType = InventoryHelper.DetermineEntryType(newItemAffectsInventory.Value, qty);
                        var nowUtc = DateTime.UtcNow;

                        await connection.ExecuteAsync(
                            @"INSERT INTO dbo.Inventory (Description, EntryType, Quantity, DatePosted, PostedBy, ReqId, ItemId)
VALUES (@Description, @EntryType, @Quantity, @NowUtc, @PostedBy, NULL, @ItemId);",
                            new
                            {
                                Description = $"Call ticket temporary replacement return (Ticket {ticketId})",
                                EntryType = entryType,
                                Quantity = qty,
                                PostedBy = changedByUserId.Value,
                                ItemId = newItemId,
                                NowUtc = nowUtc
                            },
                            transaction: tx);

                        await connection.ExecuteAsync(
                            @"UPDATE dbo.Item
 SET StockOnHand = ISNULL(StockOnHand, 0) + @Quantity,
     DateModified = @NowUtc,
     ModifiedBy = @ModifiedBy
 WHERE ItemId = @ItemId;",
                            new { Quantity = qty, ModifiedBy = changedByUserId.Value, ItemId = newItemId, NowUtc = nowUtc },
                            transaction: tx);

                        // Best-effort: log return movement to ItemAuditTrail (for Audit Movement page).
                        try
                        {
                            var snap = await GetTicketSnapshotAsync(connection, tx, ticketId);
                            var newSerial = await connection.ExecuteScalarAsync<string>(
                                new CommandDefinition("SELECT TOP 1 SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", new { ItemId = newItemId }, tx));
                            var newItemDisplay = await GetItemDisplayTextAsync(connection, tx, newItemId);

                            string createdBy = null;
                            try
                            {
                                createdBy = await connection.ExecuteScalarAsync<string>(
                                    new CommandDefinition("SELECT TOP 1 Name FROM dbo.[User] WHERE UserId = @UserId", new { UserId = changedByUserId.Value }, tx));
                            }
                            catch
                            {
                                createdBy = changedByUserId.Value.ToString();
                            }

                            ItemAuditTrailWriter.TryLog(connection, tx, new ItemAuditTrailDto
                            {
                                ItemId = newItemId,
                                SerialNumber = newSerial,
                                Action = "Call Ticket Temporary Replacement Return",
                                ActionTime = nowUtc,
                                Direction = "IN",
                                Status = "Completed",
                                EmployeeName = snap?.ResponsiblePerson,
                                DepartmentName = snap?.Department,
                                ReferenceType = "CallTicket",
                                ReferenceId = ticketId,
                                Notes = $"Temporary replacement returned • Qty {qty} • Item: {newItemDisplay}",
                                CreatedBy = createdBy ?? "System"
                            });
                        }
                        catch
                        {
                            // ignore (best-effort audit movement logging)
                        }

                        // Swap set item back (temporary replacement returned: put old unit back in the set).
                        try
                        {
                            var oldItemId = await GetLatestReplacementOldItemIdAsync(connection, tx, ticketId);
                            if (oldItemId > 0)
                            {
                                await TrySwapItemInActiveSetAsync(
                                    connection,
                                    tx,
                                    fromItemId: newItemId,
                                    toItemId: oldItemId,
                                    quantity: qty,
                                    changedByUserId: changedByUserId.Value,
                                    ticketId: ticketId,
                                    actionLabel: "TemporaryReturn");
                            }
                        }
                        catch
                        {
                            // ignore (best-effort set revert)
                        }

                        await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "TemporaryReplacementReturnedAt", null, DateTime.UtcNow.ToString("o"), null);
                        await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "TemporaryReplacementReturnedNewItemId", null, newItemId.ToString(), null);
                        await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "TemporaryReplacementReturnedQty", null, qty.ToString(), null);

                        var newItemText = await GetItemDisplayTextAsync(connection, tx, newItemId);
                        noteTextForUi = $"Temporary replacement returned to inventory.\r\nItem: {newItemText}\r\nQty: {qty}";
                        await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "TemporaryReplacementReturnNote", null, "TemporaryReturn", noteTextForUi);

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }

                // Best-effort: depending on DB rules, notes may be blocked for resolved tickets.
                // We still succeed the return (inventory + history) even if the note insert is rejected.
                if (!string.IsNullOrWhiteSpace(noteTextForUi))
                {
                    try
                    {
                        await connection.ExecuteAsync(
                            "dbo.sp_Call_AddTicketNote",
                            new
                            {
                                TicketId = ticketId,
                                NoteType = "TemporaryReturn",
                                NoteText = noteTextForUi,
                                CreatedByUserId = changedByUserId.Value
                            },
                            commandType: System.Data.CommandType.StoredProcedure);
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
        }

        public async Task<bool> SyncSetToLatestReplacementAsync(int ticketId, int? changedByUserId)
        {
            if (ticketId <= 0) throw new ArgumentOutOfRangeException(nameof(ticketId));
            if (!changedByUserId.HasValue || changedByUserId.Value <= 0)
                throw new InvalidOperationException("ChangedByUserId is required.");

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var tx = connection.BeginTransaction())
                {
                    try
                    {
                        var details = await GetLatestReplacementSwapDetailsAsync(connection, tx, ticketId);
                        if (details.OldItemId <= 0 || details.NewItemId <= 0)
                            throw new InvalidOperationException("No replacement history was found for this ticket.");

                        var qty = details.Quantity > 0 ? details.Quantity : 1;

                        // If temporary replacement was already returned, the set should point back to the old unit.
                        if (details.AlreadyReturned)
                        {
                            await TrySwapItemInActiveSetAsync(
                                connection,
                                tx,
                                fromItemId: details.NewItemId,
                                toItemId: details.OldItemId,
                                quantity: qty,
                                changedByUserId: changedByUserId.Value,
                                ticketId: ticketId,
                                actionLabel: "SetSync (Temp Return)");
                        }
                        else
                        {
                            await TrySwapItemInActiveSetAsync(
                                connection,
                                tx,
                                fromItemId: details.OldItemId,
                                toItemId: details.NewItemId,
                                quantity: qty,
                                changedByUserId: changedByUserId.Value,
                                ticketId: ticketId,
                                actionLabel: "SetSync");
                        }

                        await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "SetSyncAt", null, DateTime.UtcNow.ToString("o"), null);

                        tx.Commit();
                        return true;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        public sealed class SetSwapSyncResult
        {
            public string Outcome { get; set; }
            public int? SetId { get; set; }
            public string SetCode { get; set; }
            public int UpdatedRequests { get; set; }
            public int UpdatedSetItems { get; set; }
        }

        public async Task<SetSwapSyncResult> SyncSetToLatestReplacementWithResultAsync(int ticketId, int? changedByUserId)
        {
            if (ticketId <= 0) throw new ArgumentOutOfRangeException(nameof(ticketId));
            if (!changedByUserId.HasValue || changedByUserId.Value <= 0)
                throw new InvalidOperationException("ChangedByUserId is required.");

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var tx = connection.BeginTransaction())
                {
                    try
                    {
                        var details = await GetLatestReplacementSwapDetailsAsync(connection, tx, ticketId);
                        if (details.OldItemId <= 0 || details.NewItemId <= 0)
                            throw new InvalidOperationException("No replacement history was found for this ticket.");

                        var qty = details.Quantity > 0 ? details.Quantity : 1;

                        if (details.AlreadyReturned)
                        {
                            await TrySwapItemInActiveSetAsync(
                                connection,
                                tx,
                                fromItemId: details.NewItemId,
                                toItemId: details.OldItemId,
                                quantity: qty,
                                changedByUserId: changedByUserId.Value,
                                ticketId: ticketId,
                                actionLabel: "SetSync (Temp Return)");
                        }
                        else
                        {
                            await TrySwapItemInActiveSetAsync(
                                connection,
                                tx,
                                fromItemId: details.OldItemId,
                                toItemId: details.NewItemId,
                                quantity: qty,
                                changedByUserId: changedByUserId.Value,
                                ticketId: ticketId,
                                actionLabel: "SetSync");
                        }

                        await InsertTicketHistoryAsync(connection, tx, ticketId, changedByUserId, "SetSyncAt", null, DateTime.UtcNow.ToString("o"), null);

                        SetSwapSyncResult result = null;
                        if (await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketHistory", tx))
                        {
                            const string sql = @"
SELECT
    SetId = (
        SELECT TOP 1 TRY_CONVERT(int, h.NewValue)
        FROM dbo.CallTicketHistory h
        WHERE h.TicketId = @TicketId AND h.FieldName = 'ReplacementSetId'
        ORDER BY h.ChangedAt DESC, h.HistoryId DESC
    ),
    SetCode = (
        SELECT TOP 1 h.Note
        FROM dbo.CallTicketHistory h
        WHERE h.TicketId = @TicketId AND h.FieldName = 'ReplacementSetId'
        ORDER BY h.ChangedAt DESC, h.HistoryId DESC
    ),
    UpdatedRequests = (
        SELECT TOP 1 TRY_CONVERT(int, h.NewValue)
        FROM dbo.CallTicketHistory h
        WHERE h.TicketId = @TicketId AND h.FieldName = 'ReplacementSetSwapUpdatedRequests'
        ORDER BY h.ChangedAt DESC, h.HistoryId DESC
    ),
    UpdatedSetItems = (
        SELECT TOP 1 TRY_CONVERT(int, h.NewValue)
        FROM dbo.CallTicketHistory h
        WHERE h.TicketId = @TicketId AND h.FieldName = 'ReplacementSetSwapUpdatedSetItems'
        ORDER BY h.ChangedAt DESC, h.HistoryId DESC
    ),
    Outcome = (
        SELECT TOP 1 h.NewValue
        FROM dbo.CallTicketHistory h
        WHERE h.TicketId = @TicketId AND h.FieldName = 'ReplacementSetSwapOutcome'
        ORDER BY h.ChangedAt DESC, h.HistoryId DESC
    );";

                            result = await connection.QuerySingleOrDefaultAsync<SetSwapSyncResult>(
                                sql,
                                new { TicketId = ticketId },
                                transaction: tx);
                        }

                        tx.Commit();
                        return result ?? new SetSwapSyncResult { Outcome = "Unknown" };
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        private static async Task<int> GetLatestReplacementOldItemIdAsync(SqlConnection connection, SqlTransaction tx, int ticketId)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (ticketId <= 0) return 0;
            if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketHistory", tx))
                return 0;

            try
            {
                const string sql = @"
SELECT TOP 1 TRY_CONVERT(int, h.NewValue) AS OldItemId
FROM dbo.CallTicketHistory h
WHERE h.TicketId = @TicketId
  AND h.FieldName = 'ReplacementOldItemId'
ORDER BY h.ChangedAt DESC, h.HistoryId DESC;";
                var val = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(sql, new { TicketId = ticketId }, tx));
                return val.GetValueOrDefault();
            }
            catch
            {
                return 0;
            }
        }

        private static async Task<string> GetFirstExistingColumnAsync(SqlConnection connection, string tableName, IEnumerable<string> candidates)
        {
            foreach (var candidate in candidates ?? Enumerable.Empty<string>())
            {
                if (await CallSchemaGate.ColumnExistsAsync(connection, tableName, candidate, tx: null))
                    return candidate;
            }

            return null;
        }

        private static async Task InsertTicketHistoryAsync(
            SqlConnection connection,
            SqlTransaction tx,
            int ticketId,
            int? changedByUserId,
            string fieldName,
            string oldValue,
            string newValue,
            string note)
        {
            const string sql = @"
INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
VALUES (@TicketId, @ChangedByUserId, @FieldName, @OldValue, @NewValue, @Note);";

            await connection.ExecuteAsync(
                sql,
                new
                {
                    TicketId = ticketId,
                    ChangedByUserId = changedByUserId,
                    FieldName = fieldName,
                    OldValue = (object)oldValue ?? System.DBNull.Value,
                    NewValue = (object)newValue ?? System.DBNull.Value,
                    Note = (object)note ?? System.DBNull.Value
                },
                transaction: tx);
        }

        private sealed class TicketSnapshotRow
        {
            public string Department { get; set; }
            public string ResponsiblePerson { get; set; }
        }

        private static async Task<TicketSnapshotRow> GetTicketSnapshotAsync(SqlConnection connection, SqlTransaction tx, int ticketId)
        {
            const string sql = @"
SELECT TOP 1
    Department,
    ResponsiblePerson
FROM dbo.vw_Call_TicketList
WHERE TicketId = @TicketId;";

            try
            {
                return await connection.QuerySingleOrDefaultAsync<TicketSnapshotRow>(
                    sql,
                    new { TicketId = ticketId },
                    transaction: tx);
            }
            catch
            {
                // Snapshot is best-effort; older DBs may not have the view.
                return null;
            }
        }

        public async Task<string> GetEmployeeEmailBindingAsync(int empId)
        {
            if (empId <= 0) throw new ArgumentOutOfRangeException(nameof(empId));

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(@"
SELECT TOP 1 ea.EmailAddress
FROM dbo.EmployeeEmail ee
INNER JOIN dbo.EmailAddress ea ON ea.EmailId = ee.EmailId
WHERE ee.EmpId = @EmpId
  AND ee.IsActive = 1
  AND ee.IsPrimary = 1
  AND ea.IsActive = 1
ORDER BY ee.EmployeeEmailId;", connection))
                {
                    command.Parameters.AddWithValue("@EmpId", empId);
                    var result = await command.ExecuteScalarAsync();
                    return result == null || result == DBNull.Value ? null : result.ToString().Trim();
                }
            }
        }

        public async Task SaveEmployeeEmailBindingAsync(int empId, string email, int? changedByUserId)
        {
            if (empId <= 0) throw new ArgumentOutOfRangeException(nameof(empId));

            var normalized = (email ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                try { _ = new MailAddress(normalized); }
                catch (FormatException) { throw new ArgumentException("Enter a valid email address.", nameof(email)); }
            }

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(normalized))
                        {
                            using (var clear = new SqlCommand(
                                "UPDATE dbo.EmployeeEmail SET IsPrimary = 0 WHERE EmpId = @EmpId;",
                                connection, transaction))
                            {
                                clear.Parameters.AddWithValue("@EmpId", empId);
                                await clear.ExecuteNonQueryAsync();
                            }
                        }
                        else
                        {
                            int emailId;
                            using (var upsertAddress = new SqlCommand(@"
UPDATE dbo.EmailAddress
SET IsActive = 1, DisplayName = COALESCE(NULLIF(DisplayName, ''), @Email)
WHERE EmailAddress = @Email;

IF NOT EXISTS (SELECT 1 FROM dbo.EmailAddress WHERE EmailAddress = @Email)
    INSERT dbo.EmailAddress (EmailAddress, DisplayName, IsActive, CreatedByUserId)
    VALUES (@Email, @Email, 1, @ChangedByUserId);

SELECT EmailId FROM dbo.EmailAddress WHERE EmailAddress = @Email;",
                                connection, transaction))
                            {
                                upsertAddress.Parameters.AddWithValue("@Email", normalized);
                                upsertAddress.Parameters.AddWithValue("@ChangedByUserId", (object)changedByUserId ?? DBNull.Value);
                                emailId = Convert.ToInt32(await upsertAddress.ExecuteScalarAsync());
                            }

                            using (var clear = new SqlCommand(
                                "UPDATE dbo.EmployeeEmail SET IsPrimary = 0 WHERE EmpId = @EmpId;",
                                connection, transaction))
                            {
                                clear.Parameters.AddWithValue("@EmpId", empId);
                                await clear.ExecuteNonQueryAsync();
                            }

                            using (var save = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM dbo.EmployeeEmail WHERE EmpId = @EmpId AND EmailId = @EmailId)
    UPDATE dbo.EmployeeEmail
    SET IsPrimary = 1, IsActive = 1, CreatedByUserId = COALESCE(@ChangedByUserId, CreatedByUserId)
    WHERE EmpId = @EmpId AND EmailId = @EmailId;
ELSE
    INSERT dbo.EmployeeEmail (EmpId, EmailId, EmailRole, IsPrimary, IsActive, CreatedByUserId)
    VALUES (@EmpId, @EmailId, 'Work', 1, 1, @ChangedByUserId);",
                                connection, transaction))
                            {
                                save.Parameters.AddWithValue("@EmpId", empId);
                                save.Parameters.AddWithValue("@EmailId", emailId);
                                save.Parameters.AddWithValue("@ChangedByUserId", (object)changedByUserId ?? DBNull.Value);
                                await save.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public Task ClearEmployeeEmailBindingAsync(int empId)
        {
            return SaveEmployeeEmailBindingAsync(empId, null, null);
        }

        private static async Task<string> GetItemDisplayTextAsync(SqlConnection connection, SqlTransaction tx, int itemId)
        {
            const string sql = @"
SELECT TOP 1
    Name,
    ModelNumber,
    SerialNumber
FROM dbo.Item
WHERE ItemId = @ItemId;";

            var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
                sql,
                new { ItemId = itemId },
                transaction: tx);

            if (row == null)
                return $"ItemId={itemId}";

            string name = row.Name as string;
            string model = row.ModelNumber as string;
            string serial = row.SerialNumber as string;

            var modelPart = string.IsNullOrWhiteSpace(model) ? "" : $" ({model})";
            var serialPart = string.IsNullOrWhiteSpace(serial) ? "" : $" - {serial}";
            return $"{name}{modelPart}{serialPart}".Trim();
        }
    }
}
