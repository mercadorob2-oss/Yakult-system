using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Security;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Repositories
{
    public partial class EmployeeRepository
    {
        public static bool CanUpdateLocation => AppSession.IsLoggedIn && AppSession.CurrentUserId > 0
            && PermissionResolver.HasPortalAccess(AppSession.CurrentUserRoles, PermissionResolver.Portal.CallITMonitoring);

        private static void RequireLocationAccess()
        {
            if (!CanUpdateLocation)
                throw new UnauthorizedAccessException("IT Call Monitoring access is required to update employee locations.");
            DatabaseConfig.EnsureConfigured();
        }

        public async Task<EmployeeLocationDto> GetLocationAsync(int empId)
        {
            RequireLocationAccess();
            if (empId <= 0) throw new ArgumentOutOfRangeException(nameof(empId));
            using (var connection = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await connection.OpenAsync();
                return await ReadLocationAsync(connection, null, empId);
            }
        }

        public async Task<EmployeeLocationOptions> GetLocationOptionsAsync()
        {
            RequireLocationAccess();
            using (var connection = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await connection.OpenAsync();
                using (var result = await connection.QueryMultipleAsync(@"
                    SELECT ComId AS Id, Name FROM dbo.Company WHERE Active = 1 ORDER BY Name;
                    SELECT bdc.CompanyID AS CompanyId, c.Name AS CompanyName,
                           bdc.BranchID AS BranchId, b.Name AS BranchName,
                           bdc.DepartmentID AS DepartmentId, d.Name AS DepartmentName
                    FROM dbo.BranchDepartmentCompany bdc
                    INNER JOIN dbo.Company c ON c.ComId = bdc.CompanyID AND c.Active = 1
                    INNER JOIN dbo.Branch b ON b.BranchId = bdc.BranchID AND ISNULL(b.Active, 1) = 1
                    LEFT JOIN dbo.Department d ON d.DeptId = bdc.DepartmentID
                    WHERE bdc.DepartmentID IS NULL OR d.Active = 1;"))
                {
                    return new EmployeeLocationOptions
                    {
                        Companies = (await result.ReadAsync<LookupItem>()).ToList(),
                        Mappings = (await result.ReadAsync<EmployeeLocationMapping>()).ToList()
                    };
                }
            }
        }

        public async Task<EmployeeLocationDto> UpdateLocationAsync(EmployeeLocationUpdate change)
        {
            RequireLocationAccess();
            if (change == null) throw new ArgumentNullException(nameof(change));
            if (change.EmpId <= 0 || change.CompanyId <= 0 || change.BranchId <= 0
                || (change.DepartmentId.HasValue && change.DepartmentId.Value <= 0))
                throw new InvalidOperationException("Select a company, a branch, and a valid department or No department.");
            if (change.ExpectedRowVersion == null || change.ExpectedRowVersion.Length != 8)
                throw new InvalidOperationException("Reload this employee before saving changes.");
            var reason = (change.Reason ?? "").Trim();
            if (reason.Length > 1000) throw new InvalidOperationException("Reason is limited to 1,000 characters.");
            int userId = AppSession.CurrentUserId;

            using (var connection = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    var before = await ReadLocationAsync(connection, transaction, change.EmpId);
                    if (before == null || !before.Active)
                        throw new InvalidOperationException("This employee is inactive or no longer available.");
                    if (!before.RowVersion.SequenceEqual(change.ExpectedRowVersion))
                        throw new EmployeeLocationConflictException(before);
                    RequireLocationAccess();
                    if (AppSession.CurrentUserId != userId)
                        throw new UnauthorizedAccessException("Your session changed. Reopen this employee before saving.");
                    var actor = await connection.QuerySingleOrDefaultAsync<string>(
                        "SELECT Name FROM dbo.[User] WHERE UserId = @Id AND ISNULL(IsActive, 1) = 1;",
                        new { Id = userId }, transaction);
                    if (actor == null) throw new UnauthorizedAccessException("Your account is inactive or no longer available.");

                    bool valid = await connection.ExecuteScalarAsync<bool>(@"
                        SELECT CAST(CASE WHEN EXISTS (
                            SELECT 1 FROM dbo.BranchDepartmentCompany bdc
                            INNER JOIN dbo.Company c ON c.ComId = bdc.CompanyID AND c.Active = 1
                            INNER JOIN dbo.Branch b ON b.BranchId = bdc.BranchID AND ISNULL(b.Active, 1) = 1
                            LEFT JOIN dbo.Department d ON d.DeptId = bdc.DepartmentID
                            WHERE bdc.CompanyID = @CompanyId AND bdc.BranchID = @BranchId
                              AND ((@DepartmentId IS NULL AND bdc.DepartmentID IS NULL)
                                OR (bdc.DepartmentID = @DepartmentId AND d.Active = 1))
                        ) THEN 1 ELSE 0 END AS BIT);", change, transaction);
                    if (!valid) throw new InvalidOperationException("Choose an active company, department, and branch combination from the available assignments.");
                    if (before.CompanyId == change.CompanyId && before.DepartmentId == change.DepartmentId && before.BranchId == change.BranchId)
                        return before;

                    int affected = await connection.ExecuteAsync(@"
                        UPDATE dbo.Employee SET ComId = @CompanyId, DeptId = @DepartmentId,
                            BranchId = @BranchId, ModifiedBy = @UserId, DateModified = SYSUTCDATETIME()
                        WHERE EmpId = @EmpId AND Active = 1 AND RowVer = @ExpectedRowVersion;",
                        new { change.CompanyId, change.DepartmentId, change.BranchId, change.EmpId, change.ExpectedRowVersion, UserId = userId }, transaction);
                    if (affected != 1) throw new InvalidOperationException("The employee changed while saving. Reload and try again.");
                    var after = await ReadLocationAsync(connection, transaction, change.EmpId);
                    var notes = $"Employee location updated from {before.LocationDisplay} to {after.LocationDisplay}." +
                        (reason.Length == 0 ? "" : " Reason: " + reason);
                    await connection.ExecuteAsync(@"
                        INSERT INTO dbo.AuditTrail (Action, EntityId, EntityType, UserId, UserName, Timestamp, Notes, OldValues, NewValues)
                        VALUES ('Update', @EmpId, 'Employee', @UserId, @UserName, SYSUTCDATETIME(), @Notes, @OldValues, @NewValues);",
                        new { change.EmpId, UserId = userId, UserName = actor, Notes = notes,
                            OldValues = SerializeLocation(before), NewValues = SerializeLocation(after) }, transaction);

                    // Match the employee editor's existing transfer audit convention.
                    if (before.DepartmentId != after.DepartmentId || before.BranchId != after.BranchId)
                    {
                        var items = (await connection.QueryAsync<LocationAuditItem>(@"
                            SELECT r.ItemId, i.SerialNumber, s.SetCode
                            FROM dbo.Request r
                            LEFT JOIN dbo.Item i ON i.ItemId = r.ItemId
                            LEFT JOIN dbo.[Set] s ON s.SetId = r.SetId
                            WHERE r.EmpId = @EmpId AND r.ItemId IS NOT NULL;", new { change.EmpId }, transaction)).ToList();
                        var audits = new ItemAuditTrailRepository();
                        string action = before.DepartmentId != after.DepartmentId && before.BranchId != after.BranchId
                            ? "Department/Branch Updated" : before.DepartmentId != after.DepartmentId ? "Department Transfer" : "Branch Relocation";
                        var now = DateTime.Now;
                        foreach (var item in items)
                            await audits.LogActionAsync(new ItemAuditTrailDto
                            {
                                ItemId = item.ItemId, SerialNumber = item.SerialNumber, Action = action, ActionTime = now,
                                Status = "Completed", EmployeeId = after.EmpId, EmployeeName = AuditText(after.Name, 100),
                                DepartmentId = after.DepartmentId, DepartmentName = AuditText(after.DepartmentName, 100),
                                BranchId = after.BranchId, BranchName = AuditText(after.BranchName, 100),
                                ReferenceType = "Employee", ReferenceId = after.EmpId, SetCode = item.SetCode,
                                Notes = notes, CreatedBy = AuditText(actor, 50)
                            }, connection, transaction);
                    }
                    transaction.Commit();
                    return after;
                }
            }
        }

        private static string SerializeLocation(EmployeeLocationDto location) => JsonConvert.SerializeObject(new
        {
            location.CompanyId, location.CompanyName, location.DepartmentId, location.DepartmentName, location.BranchId, location.BranchName
        });
        private static string AuditText(string value, int length) => value != null && value.Length > length ? value.Substring(0, length) : value;

        private static Task<EmployeeLocationDto> ReadLocationAsync(SqlConnection connection, SqlTransaction transaction, int empId)
        {
            return connection.QuerySingleOrDefaultAsync<EmployeeLocationDto>(@"
                SELECT e.EmpId, e.Name, e.EmployeeNumber, e.ComId AS CompanyId, e.DeptId AS DepartmentId,
                       e.BranchId, e.Active, e.RowVer AS RowVersion,
                       c.Name AS CompanyName, d.Name AS DepartmentName, b.Name AS BranchName
                FROM dbo.Employee e" + (transaction == null ? "" : " WITH (UPDLOCK, HOLDLOCK)") + @"
                LEFT JOIN dbo.Company c ON c.ComId = e.ComId
                LEFT JOIN dbo.Department d ON d.DeptId = e.DeptId
                LEFT JOIN dbo.Branch b ON b.BranchId = e.BranchId
                WHERE e.EmpId = @EmpId;", new { EmpId = empId }, transaction);
        }

        private sealed class LocationAuditItem
        {
            public int ItemId { get; set; }
            public string SerialNumber { get; set; }
            public string SetCode { get; set; }
        }
    }
}
