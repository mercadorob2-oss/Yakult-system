using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Repositories
{
    public sealed partial class CallMonitoringRepository
    {
        public async Task<string> GetEmployeePrimaryEmailAsync(int empId)
        {
            if (empId <= 0)
                return null;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.EmployeeEmail"))
                    return null;

                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.EmailAddress"))
                    return null;

                const string sql = @"
SELECT TOP 1 e.EmailAddress
FROM dbo.EmployeeEmail ee
INNER JOIN dbo.EmailAddress e ON ee.EmailId = e.EmailId
WHERE ee.EmpId = @EmpId
  AND ee.IsActive = 1
  AND ISNULL(e.IsActive, 1) = 1
ORDER BY ee.IsPrimary DESC, ee.EmailId;";

                try
                {
                    return await connection.ExecuteScalarAsync<string>(sql, new { EmpId = empId });
                }
                catch
                {
                    return null;
                }
            }
        }


        /// <summary>
        /// Resolves the requester contact snapshot for a Call IT ticket.
        /// Personal email wins over branch and department addresses; callers may
        /// enter a ticket-only fallback if this returns no address.
        /// </summary>
        public async Task<CallTicketContactEmailResolution> ResolveTicketContactEmailAsync(
            int? callerEmpId,
            int? comId,
            int? deptId,
            int? branchId)
        {
            if (callerEmpId.HasValue && callerEmpId.Value <= 0) callerEmpId = null;
            if (comId.HasValue && comId.Value <= 0) comId = null;
            if (deptId.HasValue && deptId.Value <= 0) deptId = null;
            if (branchId.HasValue && branchId.Value <= 0) branchId = null;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (callerEmpId.HasValue
                    && await CallSchemaGate.TableExistsAsync(connection, "dbo.EmployeeEmail")
                    && await CallSchemaGate.TableExistsAsync(connection, "dbo.EmailAddress"))
                {
                    const string personalEmailSql = @"
SELECT TOP 1 ea.EmailAddress
FROM dbo.EmployeeEmail ee
INNER JOIN dbo.EmailAddress ea ON ea.EmailId = ee.EmailId
WHERE ee.EmpId = @EmpId
  AND ee.IsPrimary = 1
  AND ee.IsActive = 1
  AND ea.IsActive = 1
ORDER BY ee.EmployeeEmailId;";
                    var personalEmail = await connection.ExecuteScalarAsync<string>(personalEmailSql, new { EmpId = callerEmpId.Value });
                    if (TryNormalizeTicketContactEmail(personalEmail, out var normalizedPersonalEmail))
                    {
                        return new CallTicketContactEmailResolution
                        {
                            Email = normalizedPersonalEmail,
                            Source = "Employee personal email"
                        };
                    }
                }

                if (branchId.HasValue
                    && await CallSchemaGate.TableExistsAsync(connection, "dbo.DepartmentAccount")
                    && await CallSchemaGate.TableExistsAsync(connection, "dbo.EmailAddress")
                    && await CallSchemaGate.ColumnExistsAsync(connection, "dbo.DepartmentAccount", "EmailAddressId")
                    && await CallSchemaGate.ColumnExistsAsync(connection, "dbo.DepartmentAccount", "CompanyName")
                    && await CallSchemaGate.ColumnExistsAsync(connection, "dbo.DepartmentAccount", "DepartmentName")
                    && await CallSchemaGate.ColumnExistsAsync(connection, "dbo.DepartmentAccount", "BranchName"))
                {
                    var branchEmailSql = deptId.HasValue
                        ? @"
SELECT TOP 1 ea.EmailAddress
FROM dbo.DepartmentAccount da
INNER JOIN dbo.EmailAddress ea ON ea.EmailId = da.EmailAddressId
INNER JOIN dbo.Company c ON c.ComId = @ComId
INNER JOIN dbo.Department d ON d.DeptId = @DeptId
INNER JOIN dbo.Branch b ON b.BranchId = @BranchId
WHERE da.CompanyName = c.Name
  AND da.DepartmentName = d.Name
  AND da.BranchName = b.Name
  AND ea.IsActive = 1;"
                        : @"
SELECT TOP 1 ea.EmailAddress
FROM dbo.DepartmentAccount da
INNER JOIN dbo.EmailAddress ea ON ea.EmailId = da.EmailAddressId
INNER JOIN dbo.Company c ON c.ComId = @ComId
INNER JOIN dbo.Branch b ON b.BranchId = @BranchId
WHERE da.CompanyName = c.Name
  AND da.BranchName = b.Name
  AND ea.IsActive = 1
ORDER BY CASE WHEN NULLIF(LTRIM(RTRIM(da.DepartmentName)), '') IS NULL THEN 0 ELSE 1 END, da.EmailAddressId;";
                    var branchEmail = await connection.ExecuteScalarAsync<string>(branchEmailSql, new
                    {
                        ComId = comId,
                        DeptId = deptId,
                        BranchId = branchId
                    });
                    if (TryNormalizeTicketContactEmail(branchEmail, out var normalizedBranchEmail))
                    {
                        return new CallTicketContactEmailResolution
                        {
                            Email = normalizedBranchEmail,
                            Source = "Branch email"
                        };
                    }
                }

                if (branchId.HasValue
                    && await CallSchemaGate.TableExistsAsync(connection, "dbo.Branch")
                    && await CallSchemaGate.TableExistsAsync(connection, "dbo.EmailAddress")
                    && await CallSchemaGate.ColumnExistsAsync(connection, "dbo.Branch", "EmailId"))
                {
                    const string legacyBranchEmailSql = @"
SELECT TOP 1 ea.EmailAddress
FROM dbo.Branch b
INNER JOIN dbo.EmailAddress ea ON ea.EmailId = b.EmailId
WHERE b.BranchId = @BranchId
  AND ISNULL(b.Active, 1) = 1
  AND ea.IsActive = 1;";
                    var legacyBranchEmail = await connection.ExecuteScalarAsync<string>(legacyBranchEmailSql, new { BranchId = branchId.Value });
                    if (TryNormalizeTicketContactEmail(legacyBranchEmail, out var normalizedLegacyBranchEmail))
                    {
                        return new CallTicketContactEmailResolution
                        {
                            Email = normalizedLegacyBranchEmail,
                            Source = "Branch email"
                        };
                    }
                }

                if (comId.HasValue && deptId.HasValue
                    && await CallSchemaGate.TableExistsAsync(connection, "dbo.DepartmentEmail")
                    && await CallSchemaGate.TableExistsAsync(connection, "dbo.EmailAddress")
                    && await CallSchemaGate.ColumnExistsAsync(connection, "dbo.DepartmentEmail", "EmailAddressId")
                    && await CallSchemaGate.ColumnExistsAsync(connection, "dbo.DepartmentEmail", "CompanyName")
                    && await CallSchemaGate.ColumnExistsAsync(connection, "dbo.DepartmentEmail", "DepartmentName"))
                {
                    const string departmentEmailSql = @"
SELECT TOP 1 ea.EmailAddress
FROM dbo.DepartmentEmail de
INNER JOIN dbo.EmailAddress ea ON ea.EmailId = de.EmailAddressId
INNER JOIN dbo.Company c ON c.ComId = @ComId
INNER JOIN dbo.Department d ON d.DeptId = @DeptId
WHERE de.CompanyName = c.Name
  AND de.DepartmentName = d.Name
  AND ea.IsActive = 1;";
                    var departmentEmail = await connection.ExecuteScalarAsync<string>(departmentEmailSql, new { ComId = comId.Value, DeptId = deptId.Value });
                    if (TryNormalizeTicketContactEmail(departmentEmail, out var normalizedDepartmentEmail))
                    {
                        return new CallTicketContactEmailResolution
                        {
                            Email = normalizedDepartmentEmail,
                            Source = "Department email"
                        };
                    }
                }
            }

            return new CallTicketContactEmailResolution();
        }

        private static bool TryNormalizeTicketContactEmail(string value, out string normalized)
        {
            normalized = null;
            var candidate = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            try
            {
                var address = new System.Net.Mail.MailAddress(candidate);
                if (!string.Equals(address.Address, candidate, System.StringComparison.OrdinalIgnoreCase))
                    return false;

                normalized = address.Address;
                return true;
            }
            catch (System.FormatException)
            {
                return false;
            }
        }

        public async Task<List<LookupItem>> GetCompaniesAsync()
        {
            const string sql = @"
SELECT c.ComId AS Id, c.Name
FROM dbo.Company c
WHERE c.Active = 1
ORDER BY c.Name;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync<LookupItem>(sql);
                return rows.ToList();
            }
        }

        public async Task<List<LookupItem>> GetDepartmentsAsync()
        {
            const string sql = @"
 SELECT d.DeptId AS Id, d.Name
 FROM dbo.Department d
 WHERE d.Active = 1
 ORDER BY d.Name;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync<LookupItem>(sql);
                return rows.ToList();
            }
        }

        public async Task<List<LookupItem>> GetDepartmentsAsync(int? comId)
        {
            if (!comId.HasValue || comId.Value <= 0)
                return await GetDepartmentsAsync();

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                const string filteredSql = @"
SELECT d.DeptId AS Id, d.Name
FROM dbo.Department d
WHERE d.Active = 1
  AND EXISTS (
      SELECT 1
      FROM   dbo.BranchDepartmentCompany bdc
      WHERE  bdc.DepartmentID = d.DeptId
        AND  bdc.CompanyID    = @ComId
  )
ORDER BY d.Name;";

                var rows = (await connection.QueryAsync<LookupItem>(filteredSql, new { ComId = comId.Value })).ToList();
                if (rows.Count > 0)
                    return rows;

                // Fallback: if no BDC rows exist for this company yet, show all.
                return await GetDepartmentsAsync();
            }
        }

        public async Task<List<LookupItem>> GetBranchesAsync(int? comId = null, int? deptId = null)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.Branch"))
                    return new List<LookupItem>();

                const string sql = @"
SELECT
    b.BranchId AS Id,
    CASE
        WHEN ISNULL(b.IsCenter, 0) = 1 THEN b.Name + ' (Center)'
        WHEN ISNULL(b.IsDepot, 0) = 1 THEN b.Name + ' (Depot)'
        WHEN ISNULL(b.IsFactory, 0) = 1 THEN b.Name + ' (Factory)'
        WHEN ISNULL(b.IsDistributor, 0) = 1 THEN b.Name + ' (Distributor)'
        ELSE b.Name
    END AS Name
FROM dbo.Branch b
WHERE ISNULL(b.Active, 1) = 1
  AND (@ComId IS NULL OR EXISTS (
      SELECT 1 FROM dbo.BranchDepartmentCompany bdc
      WHERE bdc.BranchID = b.BranchId AND bdc.CompanyID = @ComId
  ))
  AND (
        @DeptId IS NULL
     OR (@DeptId = 0 AND NOT EXISTS (
             SELECT 1 FROM dbo.BranchDepartmentCompany bdc
             WHERE bdc.BranchID = b.BranchId AND bdc.DepartmentID IS NOT NULL
         ))
     OR (@DeptId > 0 AND EXISTS (
             SELECT 1 FROM dbo.BranchDepartmentCompany bdc
             WHERE bdc.BranchID = b.BranchId AND bdc.DepartmentID = @DeptId
         ))
  )
ORDER BY b.Name ASC, b.BranchId ASC;";

                var rows = await connection.QueryAsync<LookupItem>(sql, new { ComId = comId, DeptId = deptId });
                return rows?.ToList() ?? new List<LookupItem>();
            }
        }

        public async Task<List<LookupItem>> GetEmployeesByDeptAndBranchAsync(int? comId, int? deptId, int? branchId)
        {
            if (comId.HasValue && comId.Value <= 0) comId = null;
            if (deptId.HasValue && deptId.Value <= 0) deptId = null;
            if (branchId.HasValue && branchId.Value <= 0) branchId = null;

            // Caller dropdown is scoped by department and/or branch to avoid loading
            // massive global lists - but some employees only have one of the two set
            // (DeptId is NULL, or BranchId is NULL), so require at least one, not
            // specifically department.
            if ((!deptId.HasValue || deptId.Value <= 0) && (!branchId.HasValue || branchId.Value <= 0))
                return new List<LookupItem>();

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.Employee"))
                    return new List<LookupItem>();

                var hasBranchId = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.Employee", "BranchId");
                var hasComId = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.Employee", "ComId");
                var hasBdc = await CallSchemaGate.TableExistsAsync(connection, "dbo.BranchDepartmentCompany");

                // Prefer deriving ComId validity via BranchDepartmentCompany.CompanyID. Only fall back to Employee.ComId
                // when the mapping table (or BranchId) isn't available in this environment.
                var companyGuard =
                    (hasBdc && hasBranchId && deptId.HasValue)
                        ? @"
  AND (@ComId IS NULL OR EXISTS (
      SELECT 1
      FROM dbo.BranchDepartmentCompany bdc
      WHERE bdc.CompanyID = @ComId
        AND bdc.DepartmentID = @DeptId
        AND bdc.BranchID = COALESCE(@BranchId, e.BranchId)
  ))"
                        : (hasComId
                            ? "\n  AND (@ComId IS NULL OR e.ComId = @ComId)"
                            : string.Empty);

                // Match on whichever of DeptId/BranchId was actually supplied. An employee
                // with only a branch (DeptId = NULL) or only a department (BranchId = NULL)
                // must still be selectable, so this is no longer a strict "DeptId AND BranchId"
                // match - each side is only applied when a value was actually passed in.
                var deptFilter = deptId.HasValue
                    ? "\n  AND e.DeptId = @DeptId"
                    : string.Empty;
                var branchFilter = (hasBranchId && branchId.HasValue)
                    ? "\n  AND e.BranchId = @BranchId"
                    : string.Empty;

                var sql = @"
SELECT
    e.EmpId AS Id,
    e.Name
FROM dbo.Employee e
WHERE e.Active = 1" +
                          deptFilter +
                          branchFilter +
                          companyGuard +
@"
ORDER BY e.Name ASC, e.EmpId ASC;";

                var rows = await connection.QueryAsync<LookupItem>(
                    sql,
                    new
                    {
                        ComId = (hasBdc || hasComId) ? comId : null,
                        DeptId = deptId,
                        BranchId = hasBranchId ? branchId : null
                    });

                return rows?.ToList() ?? new List<LookupItem>();
            }
        }

        public async Task<string> GetBranchEmailAsync(int branchId)
        {
            if (branchId <= 0)
                return null;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.Branch"))
                    return null;

                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.EmailAddress"))
                    return null;

                if (!await CallSchemaGate.ColumnExistsAsync(connection, "dbo.Branch", "EmailId"))
                    return null;

                try
                {
                    const string sql = @"
SELECT TOP 1 e.EmailAddress
FROM dbo.Branch b
INNER JOIN dbo.EmailAddress e ON b.EmailId = e.EmailId
WHERE b.BranchId = @BranchId
  AND ISNULL(b.Active, 1) = 1
  AND ISNULL(e.IsActive, 1) = 1;";

                    return await connection.ExecuteScalarAsync<string>(sql, new { BranchId = branchId });
                }
                catch
                {
                    return null;
                }
            }
        }

        public async Task<List<LookupItem>> GetItEmployeesByDepartmentNameAsync(string itDepartmentName)
        {
            const string sql = @"
 SELECT e.EmpId AS Id, e.Name
 FROM dbo.Employee e
 JOIN dbo.Department d ON d.DeptId = e.DeptId
 WHERE e.Active = 1
   AND d.Name = @DeptName
 ORDER BY e.Name;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = (await connection.QueryAsync<LookupItem>(sql, new { DeptName = itDepartmentName })).ToList();
                if (rows.Count > 0)
                    return rows;

                // Fallback: different environments may name the IT department differently.
                const string fallbackSql = @"
SELECT e.EmpId AS Id, e.Name
FROM dbo.Employee e
JOIN dbo.Department d ON d.DeptId = e.DeptId
WHERE e.Active = 1
  AND (
        d.Name LIKE 'IT%'
     OR d.Name LIKE '% IT %'
     OR d.Name LIKE '%I.T.%'
     OR d.Name LIKE '%Information%Technology%'
  )
ORDER BY e.Name;";

                return (await connection.QueryAsync<LookupItem>(fallbackSql)).ToList();
            }
        }

        /// <summary>
        /// Returns current open-ticket counts for the supplied employee IDs in one query.
        /// The Incoming Tickets staff picker uses this to show workload without issuing
        /// one profile query per candidate.
        /// </summary>
        public async Task<Dictionary<int, int>> GetOpenTicketCountsByAssigneeAsync(IEnumerable<int> empIds)
        {
            var ids = (empIds ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var result = ids.ToDictionary(id => id, _ => 0);
            if (ids.Count == 0)
                return result;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicket"))
                    return result;

                const string sql = @"
SELECT
    t.AssignedToEmpId AS EmpId,
    COUNT(1) AS OpenTicketCount
FROM dbo.CallTicket t
WHERE t.AssignedToEmpId IN @EmpIds
  AND ISNULL(t.Status, '') NOT IN ('Solved', 'Resolved (Temporary)', 'Closed')
GROUP BY t.AssignedToEmpId;";

                var rows = await connection.QueryAsync<AssigneeOpenTicketCountRow>(sql, new { EmpIds = ids });
                foreach (var row in rows ?? Enumerable.Empty<AssigneeOpenTicketCountRow>())
                {
                    if (row.EmpId > 0)
                        result[row.EmpId] = Math.Max(0, row.OpenTicketCount);
                }

                return result;
            }
        }

        private sealed class AssigneeOpenTicketCountRow
        {
            public int EmpId { get; set; }
            public int OpenTicketCount { get; set; }
        }

        public async Task<List<LookupItem>> GetCallAssignmentCandidatesByDepartmentNameAsync(string itDepartmentName)
        {
            var allowedRoles = new[]
            {
                "Developer",
                "InventoryManager",
                "Inventory Manager",
                "IT Manager",
                "IT Supervisor",
                "Tech Support"
            };

            var hasEligibilitySettings = await AssignmentEligibilitySettingsEnabledAsync();

            var sql = @"
SELECT
    e.EmpId AS Id,
    e.Name,
    u.Name AS UserName,
    e.Position,
    ISNULL(roleInfo.RoleNames, '') AS RoleNames,
    CAST(ISNULL(roleInfo.HasAllowedRole, 0) AS bit) AS HasAllowedRole,
    " + (hasEligibilitySettings
        ? "CAST(CASE WHEN ae.EmpId IS NULL THEN 0 ELSE 1 END AS bit) AS HasStoredEligibilitySettings, CAST(ae.IsAssignmentEligible AS bit) AS StoredIsAssignmentEligible"
        : "CAST(0 AS bit) AS HasStoredEligibilitySettings, CAST(NULL AS bit) AS StoredIsAssignmentEligible") + @"
FROM dbo.Employee e
INNER JOIN dbo.Department d ON d.DeptId = e.DeptId
INNER JOIN dbo.[User] u ON u.EmpId = e.EmpId
   AND ISNULL(u.IsActive, 1) = 1
OUTER APPLY (
    SELECT
        CAST(CASE WHEN EXISTS (
            SELECT 1
            FROM dbo.UserRole urx
            INNER JOIN dbo.Role rx ON rx.RoleId = urx.RoleId
            WHERE urx.UserId = u.UserId
              AND ISNULL(rx.IsActive, 1) = 1
              AND rx.RoleName IN @RoleNames
        ) THEN 1 ELSE 0 END AS bit) AS HasAllowedRole,
        STUFF((
            SELECT ', ' + r2.RoleName
            FROM dbo.UserRole ur2
            INNER JOIN dbo.Role r2 ON r2.RoleId = ur2.RoleId
            WHERE ur2.UserId = u.UserId
              AND ISNULL(r2.IsActive, 1) = 1
              AND r2.RoleName IN @RoleNames
            ORDER BY r2.RoleName
            FOR XML PATH(''), TYPE
        ).value('.', 'nvarchar(max)'), 1, 2, '') AS RoleNames
) roleInfo
" + (hasEligibilitySettings ? "LEFT JOIN dbo.CallAssignmentEligibility ae ON ae.EmpId = e.EmpId\r\n" : string.Empty) + @"
WHERE e.Active = 1
  AND d.Name = @DeptName
ORDER BY e.Name;";

            var fallbackSql = @"
SELECT
    e.EmpId AS Id,
    e.Name,
    u.Name AS UserName,
    e.Position,
    ISNULL(roleInfo.RoleNames, '') AS RoleNames,
    CAST(ISNULL(roleInfo.HasAllowedRole, 0) AS bit) AS HasAllowedRole,
    " + (hasEligibilitySettings
        ? "CAST(CASE WHEN ae.EmpId IS NULL THEN 0 ELSE 1 END AS bit) AS HasStoredEligibilitySettings, CAST(ae.IsAssignmentEligible AS bit) AS StoredIsAssignmentEligible"
        : "CAST(0 AS bit) AS HasStoredEligibilitySettings, CAST(NULL AS bit) AS StoredIsAssignmentEligible") + @"
FROM dbo.Employee e
INNER JOIN dbo.Department d ON d.DeptId = e.DeptId
INNER JOIN dbo.[User] u ON u.EmpId = e.EmpId
   AND ISNULL(u.IsActive, 1) = 1
OUTER APPLY (
    SELECT
        CAST(CASE WHEN EXISTS (
            SELECT 1
            FROM dbo.UserRole urx
            INNER JOIN dbo.Role rx ON rx.RoleId = urx.RoleId
            WHERE urx.UserId = u.UserId
              AND ISNULL(rx.IsActive, 1) = 1
              AND rx.RoleName IN @RoleNames
        ) THEN 1 ELSE 0 END AS bit) AS HasAllowedRole,
        STUFF((
            SELECT ', ' + r2.RoleName
            FROM dbo.UserRole ur2
            INNER JOIN dbo.Role r2 ON r2.RoleId = ur2.RoleId
            WHERE ur2.UserId = u.UserId
              AND ISNULL(r2.IsActive, 1) = 1
              AND r2.RoleName IN @RoleNames
            ORDER BY r2.RoleName
            FOR XML PATH(''), TYPE
        ).value('.', 'nvarchar(max)'), 1, 2, '') AS RoleNames
) roleInfo
" + (hasEligibilitySettings ? "LEFT JOIN dbo.CallAssignmentEligibility ae ON ae.EmpId = e.EmpId\r\n" : string.Empty) + @"
WHERE e.Active = 1
  AND (
        d.Name LIKE 'IT%'
     OR d.Name LIKE '% IT %'
     OR d.Name LIKE '%I.T.%'
     OR d.Name LIKE '%Information%Technology%'
  )
ORDER BY e.Name;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = (await connection.QueryAsync<CallAssignmentCandidateRow>(
                    sql,
                    new { DeptName = itDepartmentName, RoleNames = allowedRoles })).ToList();

                var items = rows
                    .Where(x => x.IsAssignmentEligible)
                    .Select(x => new LookupItem
                    {
                        Id = x.Id,
                        Name = x.Name,
                        DisplayName = x.Name + " (" + x.UserName + ") - " + x.AccessDisplay
                    })
                    .ToList();

                if (items.Count > 0)
                    return items;

                return (await connection.QueryAsync<CallAssignmentCandidateRow>(
                    fallbackSql,
                    new { RoleNames = allowedRoles }))
                    .Where(x => x.IsAssignmentEligible)
                    .Select(x => new LookupItem
                    {
                        Id = x.Id,
                        Name = x.Name,
                        DisplayName = x.Name + " (" + x.UserName + ") - " + x.AccessDisplay
                    })
                    .ToList();
            }
        }

        public async Task<List<CallAssignmentEligibilityItem>> GetCallAssignmentEligibilityAsync(string itDepartmentName)
        {
            var allowedRoles = new[]
            {
                "Developer",
                "InventoryManager",
                "Inventory Manager",
                "IT Manager",
                "IT Supervisor",
                "Tech Support"
            };

            var hasEligibilitySettings = await AssignmentEligibilitySettingsEnabledAsync();
            var departmentFilter = !string.IsNullOrWhiteSpace(itDepartmentName)
                ? "d.Name = @DeptName"
                : @"(
        d.Name LIKE 'IT%'
     OR d.Name LIKE '% IT %'
     OR d.Name LIKE '%I.T.%'
     OR d.Name LIKE '%Information%Technology%'
  )";

            var sql = @"
SELECT
    e.EmpId,
    e.Name AS EmployeeName,
    e.Position,
    u.UserId,
    u.Name AS UserName,
    ISNULL(roleInfo.RoleNames, '') AS RoleNames,
    CAST(ISNULL(roleInfo.HasAllowedRole, 0) AS bit) AS HasAllowedRole,
    " + (hasEligibilitySettings
        ? "CAST(CASE WHEN ae.EmpId IS NULL THEN 0 ELSE 1 END AS bit) AS HasStoredEligibilitySettings, CAST(ISNULL(ae.IsAssignmentEligible, 0) AS bit) AS IsAssignmentEligible, CAST(ISNULL(ae.IsEscalationEligible, 0) AS bit) AS IsEscalationEligible"
        : "CAST(0 AS bit) AS HasStoredEligibilitySettings, CAST(0 AS bit) AS IsAssignmentEligible, CAST(0 AS bit) AS IsEscalationEligible") + @"
FROM dbo.Employee e
INNER JOIN dbo.Department d ON d.DeptId = e.DeptId
LEFT JOIN dbo.[User] u ON u.EmpId = e.EmpId
   AND ISNULL(u.IsActive, 1) = 1
OUTER APPLY (
    SELECT
        CAST(CASE WHEN EXISTS (
            SELECT 1
            FROM dbo.UserRole urx
            INNER JOIN dbo.Role rx ON rx.RoleId = urx.RoleId
            WHERE urx.UserId = u.UserId
              AND ISNULL(rx.IsActive, 1) = 1
              AND rx.RoleName IN @RoleNames
        ) THEN 1 ELSE 0 END AS bit) AS HasAllowedRole,
        STUFF((
            SELECT ', ' + r2.RoleName
            FROM dbo.UserRole ur2
            INNER JOIN dbo.Role r2 ON r2.RoleId = ur2.RoleId
            WHERE ur2.UserId = u.UserId
              AND ISNULL(r2.IsActive, 1) = 1
              AND r2.RoleName IN @RoleNames
            ORDER BY r2.RoleName
            FOR XML PATH(''), TYPE
        ).value('.', 'nvarchar(max)'), 1, 2, '') AS RoleNames
) roleInfo
" + (hasEligibilitySettings ? "LEFT JOIN dbo.CallAssignmentEligibility ae ON ae.EmpId = e.EmpId\r\n" : string.Empty) + @"
WHERE e.Active = 1
  AND " + departmentFilter + @"
ORDER BY e.Name;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = (await connection.QueryAsync<CallAssignmentEligibilityItem>(
                    sql,
                    new { DeptName = itDepartmentName, RoleNames = allowedRoles })).ToList();

                foreach (var row in rows.Where(x => !x.HasStoredEligibilitySettings && x.CanBeAssigned))
                {
                    row.IsAssignmentEligible = true;
                    row.IsEscalationEligible = true;
                }

                var currentDefault = await GetAutoEscalationAssigneeEmpIdAsync();
                if (currentDefault.HasValue)
                {
                    foreach (var row in rows)
                        row.IsDefaultAutoEscalation = row.EmpId == currentDefault.Value;
                }

                return rows;
            }
        }

        private sealed class CallAssignmentCandidateRow
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string UserName { get; set; }
            public string Position { get; set; }
            public string RoleNames { get; set; }
            public bool HasAllowedRole { get; set; }
            public bool HasStoredEligibilitySettings { get; set; }
            public bool? StoredIsAssignmentEligible { get; set; }

            public bool IsAssignmentEligible
            {
                get
                {
                    var hasAllowedAccess = HasAllowedRole || CallAssignmentEligibilityItem.MatchesAllowedPosition(Position);
                    return HasStoredEligibilitySettings ? StoredIsAssignmentEligible == true : hasAllowedAccess;
                }
            }

            public string AccessDisplay
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(RoleNames))
                        return RoleNames;

                    return CallAssignmentEligibilityItem.MatchesAllowedPosition(Position)
                        ? "Position: " + (Position ?? string.Empty).Trim()
                        : "No allowed access";
                }
            }
        }

        public async Task SaveCallAssignmentEligibilityAsync(IEnumerable<CallAssignmentEligibilityItem> items, int? updatedByUserId)
        {
            var list = (items ?? Enumerable.Empty<CallAssignmentEligibilityItem>())
                .Where(x => x != null && x.EmpId > 0)
                .ToList();

            if (list.Count == 0)
                return;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallAssignmentEligibility"))
                    throw new InvalidOperationException("Assignment eligibility schema is not installed in this database yet.");

                await connection.OpenAsync();
                using (var tx = connection.BeginTransaction())
                {
                    const string sql = @"
MERGE dbo.CallAssignmentEligibility AS tgt
USING (SELECT @EmpId AS EmpId) AS src
    ON tgt.EmpId = src.EmpId
WHEN MATCHED THEN
    UPDATE SET
        IsAssignmentEligible = @IsAssignmentEligible,
        IsEscalationEligible = @IsEscalationEligible,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedByUserId = @UpdatedByUserId
WHEN NOT MATCHED THEN
    INSERT
    (
        EmpId,
        IsAssignmentEligible,
        IsEscalationEligible,
        UpdatedByUserId
    )
    VALUES
    (
        @EmpId,
        @IsAssignmentEligible,
        @IsEscalationEligible,
        @UpdatedByUserId
    );";

                    foreach (var item in list)
                    {
                        var isAssignmentEligible = item.CanBeAssigned && item.IsAssignmentEligible;
                        var isEscalationEligible = isAssignmentEligible && item.IsEscalationEligible;

                        await connection.ExecuteAsync(
                            new CommandDefinition(
                                sql,
                                new
                                {
                                    EmpId = item.EmpId,
                                    IsAssignmentEligible = isAssignmentEligible,
                                    IsEscalationEligible = isEscalationEligible,
                                    UpdatedByUserId = updatedByUserId
                                },
                                transaction: tx));
                    }

                    tx.Commit();
                }
            }
        }

        public async Task<List<CallEmployeeProfileLookup>> GetItEmployeeProfilesAsync(string itDepartmentName)
        {
            const string sql = @"
SELECT
    e.EmpId,
    e.Name AS EmployeeName,
    e.Position,
    u.UserId,
    u.Name AS UserName
FROM dbo.Employee e
JOIN dbo.Department d ON d.DeptId = e.DeptId
LEFT JOIN dbo.[User] u ON u.EmpId = e.EmpId
WHERE e.Active = 1
  AND d.Name = @DeptName
ORDER BY e.Name;";

            const string fallbackSql = @"
SELECT
    e.EmpId,
    e.Name AS EmployeeName,
    e.Position,
    u.UserId,
    u.Name AS UserName
FROM dbo.Employee e
JOIN dbo.Department d ON d.DeptId = e.DeptId
LEFT JOIN dbo.[User] u ON u.EmpId = e.EmpId
WHERE e.Active = 1
  AND (
        d.Name LIKE 'IT%'
     OR d.Name LIKE '% IT %'
     OR d.Name LIKE '%I.T.%'
     OR d.Name LIKE '%Information%Technology%'
  )
ORDER BY e.Name;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = (await connection.QueryAsync<CallEmployeeProfileLookup>(sql, new { DeptName = itDepartmentName })).ToList();
                if (rows.Count > 0)
                    return rows;

                return (await connection.QueryAsync<CallEmployeeProfileLookup>(fallbackSql)).ToList();
            }
        }

        public async Task<CallEmployeeProfileLookup> GetEmployeeProfileLookupByEmpIdAsync(int empId)
        {
            if (empId <= 0) throw new ArgumentOutOfRangeException(nameof(empId));

            const string sql = @"
SELECT TOP 1
    e.EmpId,
    e.Name AS EmployeeName,
    e.Position,
    u.UserId,
    u.Name AS UserName
FROM dbo.Employee e
LEFT JOIN dbo.[User] u ON u.EmpId = e.EmpId
WHERE e.EmpId = @EmpId;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await connection.QuerySingleOrDefaultAsync<CallEmployeeProfileLookup>(sql, new { EmpId = empId });
            }
        }

        public async Task<CallEmployeeOrgInfo> GetEmployeeOrgInfoByEmpIdAsync(int empId)
        {
            if (empId <= 0) throw new ArgumentOutOfRangeException(nameof(empId));

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.Employee"))
                    return null;

                var hasComId = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.Employee", "ComId");
                var hasBranchId = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.Employee", "BranchId");
                var hasBdc = await CallSchemaGate.TableExistsAsync(connection, "dbo.BranchDepartmentCompany");

                // When possible, derive the company from BranchDepartmentCompany using the employee's BranchId+DeptId.
                // This avoids relying on deprecated Branch/Department ownership columns.
                string comIdSelect;
                if (hasBdc && hasBranchId)
                {
                    var fallback = hasComId ? "e.ComId" : "CAST(NULL AS int)";
                    comIdSelect = $@"
    COALESCE(
        (
            SELECT TOP 1 bdc.CompanyID
            FROM dbo.BranchDepartmentCompany bdc
            WHERE bdc.BranchID = e.BranchId
              AND (bdc.DepartmentID = e.DeptId OR bdc.DepartmentID IS NULL)
            ORDER BY
                CASE WHEN bdc.DepartmentID = e.DeptId THEN 0 ELSE 1 END,
                bdc.CompanyID ASC
        ),
        {fallback}
    ) AS ComId,";
                }
                else
                {
                    comIdSelect = hasComId ? "\n    e.ComId AS ComId," : "\n    CAST(NULL AS int) AS ComId,";
                }

                var sql = @"
SELECT TOP 1
    e.EmpId,
    e.Name AS EmployeeName," +
                          comIdSelect +
                          @"
    e.DeptId," +
                          (hasBranchId ? "\n    e.BranchId" : "\n    CAST(NULL AS int) AS BranchId") +
@"
FROM dbo.Employee e
WHERE e.EmpId = @EmpId;";

                return await connection.QuerySingleOrDefaultAsync<CallEmployeeOrgInfo>(sql, new { EmpId = empId });
            }
        }

        public async Task<List<LookupItem>> SearchEmployeesByNameAsync(string name, int maxResults = 20)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2)
                return new List<LookupItem>();

            var searchTerm = name.Trim();

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.Employee"))
                    return new List<LookupItem>();

                var sql = @"
SELECT TOP (@MaxResults)
    e.EmpId AS Id,
    e.Name
FROM dbo.Employee e
WHERE e.Active = 1
  AND e.Name LIKE '%' + @SearchTerm + '%'
ORDER BY
    CASE WHEN e.Name LIKE @SearchTerm + '%' THEN 0 ELSE 1 END,
    e.Name ASC,
    e.EmpId ASC;";

                var rows = await connection.QueryAsync<LookupItem>(
                    sql,
                    new { SearchTerm = searchTerm, MaxResults = maxResults });

                return rows?.ToList() ?? new List<LookupItem>();
            }
        }
    }
}
