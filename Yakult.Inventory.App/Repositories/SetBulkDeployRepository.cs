using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Wpf.Set.BulkDeploy;

namespace Yakult.Inventory.App.Repositories
{
    public class SetBulkDeployRepository
    {
        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        public sealed class BundleCommitResult
        {
            public string BundleKey { get; set; }
            public bool Skipped { get; set; }
            public int? SetId { get; set; }
            public string SetCode { get; set; }
            public string Message { get; set; }
        }

        public sealed class BulkDeployResult
        {
            public int Created { get; set; }
            public int Skipped { get; set; }
            public List<BundleCommitResult> Details { get; set; }

            public BulkDeployResult()
            {
                Details = new List<BundleCommitResult>();
            }
        }

        private sealed class UnitInfo
        {
            public BulkDeployRow Row;
            public int ItemId;
            public string SerialTrimmed;
            public string MonitorPending;
            public int? CategoryId;
        }

            private sealed class CommittedRequest
            {
                public int ReqId;
                public int ItemId;
                public string SerialTrimmed;
                public string ItemName;
                public string ModelNumber;
                public int Quantity;
                public int? EmpId;
                public string EmpName;
                public int? ComId;
                public int? BranchId;
                public string BranchName;
            }

        public async Task<HashSet<string>> GetExistingSerialsAsync(List<string> serials)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (serials == null || serials.Count == 0)
                return result;

            var distinct = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in serials)
            {
                if (string.IsNullOrWhiteSpace(s))
                    continue;
                string trimmed = s.Trim();
                if (trimmed.Length == 0)
                    continue;
                if (seen.Add(trimmed))
                    distinct.Add(trimmed);
            }

            if (distinct.Count == 0)
                return result;

            var paramNames = new List<string>(distinct.Count);
            for (int i = 0; i < distinct.Count; i++)
                paramNames.Add("@p" + i);

            string sql = "SELECT SerialNumber FROM dbo.Item WHERE SerialNumber IN (" + string.Join(", ", paramNames) + ")";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                for (int i = 0; i < distinct.Count; i++)
                    cmd.Parameters.AddWithValue("@p" + i, distinct[i]);

                await con.OpenAsync().ConfigureAwait(false);
                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        if (!reader.IsDBNull(0))
                            result.Add(reader.GetString(0));
                    }
                }
            }

            return result;
        }

        public async Task<Dictionary<string, int>> GetDepartmentIdsAsync()
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            const string sql = "SELECT DeptId, Name FROM dbo.Department";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync().ConfigureAwait(false);
                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        int deptId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        string name = reader.IsDBNull(1) ? null : reader.GetString(1);
                        if (deptId > 0 && !string.IsNullOrWhiteSpace(name))
                            result[name.Trim()] = deptId;
                    }
                }
            }

            return result;
        }

        public async Task<Dictionary<string, BulkDeployEmployee>> GetEmployeeLookupAsync()
        {
            var result = new Dictionary<string, BulkDeployEmployee>(StringComparer.OrdinalIgnoreCase);
            const string sql = "SELECT EmpId, Name, EmployeeNumber FROM dbo.Employee";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync().ConfigureAwait(false);
                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        int empId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        string name = reader.IsDBNull(1) ? null : reader.GetString(1);
                        object numObj = reader.IsDBNull(2) ? null : reader.GetValue(2);
                        string number = numObj == null ? null : Convert.ToString(numObj);
                        if (empId <= 0)
                            continue;
                        var emp = new BulkDeployEmployee
                        {
                            EmpId = empId,
                            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
                            EmployeeNumber = string.IsNullOrWhiteSpace(number) ? null : number.Trim()
                        };
                        if (!string.IsNullOrWhiteSpace(emp.Name) && !result.ContainsKey(emp.Name))
                            result[emp.Name] = emp;
                        if (!string.IsNullOrWhiteSpace(emp.EmployeeNumber) && !result.ContainsKey(emp.EmployeeNumber))
                            result[emp.EmployeeNumber] = emp;
                    }
                }
            }

            return result;
        }

        public async Task<Dictionary<string, int>> GetCompanyIdsAsync()
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            const string sql = "SELECT ComId, Name FROM dbo.Company";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync().ConfigureAwait(false);
                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        int id = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        string name = reader.IsDBNull(1) ? null : reader.GetString(1);
                        if (id > 0 && !string.IsNullOrWhiteSpace(name) && !result.ContainsKey(name.Trim()))
                            result[name.Trim()] = id;
                    }
                }
            }

            return result;
        }

        public async Task<Dictionary<string, int>> GetBranchIdsAsync()
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            const string sql = "SELECT BranchId, Name FROM dbo.Branch";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync().ConfigureAwait(false);
                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        int id = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        string name = reader.IsDBNull(1) ? null : reader.GetString(1);
                        if (id > 0 && !string.IsNullOrWhiteSpace(name) && !result.ContainsKey(name.Trim()))
                            result[name.Trim()] = id;
                    }
                }
            }

            return result;
        }

        // Company -> Branch cascade (feature 6/8). dbo.Branch has no direct
        // ComId column - the relationship is resolved through the
        // dbo.BranchDepartmentCompany junction table (confirmed live against
        // YIMS_PROD: 171 rows, BranchID/CompanyID both populated). Matches
        // the company by exact dbo.Company.Name, same convention as
        // GetCompanyIdsAsync/GetBranchIdsAsync above.
        public async Task<List<string>> GetBranchNamesByCompanyAsync(string companyName)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(companyName))
                return result;

            const string sql = @"
SELECT DISTINCT b.Name
FROM dbo.BranchDepartmentCompany bdc
INNER JOIN dbo.Company c ON bdc.CompanyID = c.ComId
INNER JOIN dbo.Branch b ON bdc.BranchID = b.BranchId
WHERE c.Name = @CompanyName
ORDER BY b.Name";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@CompanyName", companyName.Trim());
                await con.OpenAsync().ConfigureAwait(false);
                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        string name = reader.IsDBNull(0) ? null : reader.GetString(0);
                        if (!string.IsNullOrWhiteSpace(name))
                            result.Add(name.Trim());
                    }
                }
            }

            return result;
        }

        // Company <-> Branch pairing check (feature 8): returns the set of
        // (CompanyID, BranchID) pairs that are actually linked in
        // dbo.BranchDepartmentCompany, so ValidateAllAsync can flag rows
        // where the typed Company and Branch don't belong together (e.g.
        // a YPI company paired with a branch that's actually under a
        // different company).
        public async Task<HashSet<(int ComId, int BranchId)>> GetCompanyBranchPairsAsync()
        {
            var result = new HashSet<(int ComId, int BranchId)>();
            const string sql = @"
SELECT DISTINCT CompanyID, BranchID
FROM dbo.BranchDepartmentCompany
WHERE CompanyID IS NOT NULL AND BranchID IS NOT NULL";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync().ConfigureAwait(false);
                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                            result.Add((reader.GetInt32(0), reader.GetInt32(1)));
                    }
                }
            }

            return result;
        }

        public async Task<List<string>> GetCategoryNamesAsync()
        {
            var result = new List<string>();
            const string sql = "SELECT Name FROM dbo.ItemCategory ORDER BY Name";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync().ConfigureAwait(false);
                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        if (!reader.IsDBNull(0))
                        {
                            string name = reader.GetString(0);
                            if (!string.IsNullOrWhiteSpace(name))
                                result.Add(name.Trim());
                        }
                    }
                }
            }

            return result;
        }

        public async Task<HashSet<string>> GetExistingComputerNamesAsync(List<string> names)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (names == null || names.Count == 0)
                return result;

            var distinct = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in names)
            {
                if (string.IsNullOrWhiteSpace(n))
                    continue;
                string trimmed = n.Trim();
                if (trimmed.Length == 0)
                    continue;
                if (seen.Add(trimmed))
                    distinct.Add(trimmed);
            }

            if (distinct.Count == 0)
                return result;

            var paramNames = new List<string>(distinct.Count);
            for (int i = 0; i < distinct.Count; i++)
                paramNames.Add("@p" + i);

            string sql = "SELECT ComputerName FROM dbo.[Set] WHERE ComputerName IN (" + string.Join(", ", paramNames) + ")";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                for (int i = 0; i < distinct.Count; i++)
                    cmd.Parameters.AddWithValue("@p" + i, distinct[i]);

                await con.OpenAsync().ConfigureAwait(false);
                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        if (!reader.IsDBNull(0))
                        {
                            string value = reader.GetString(0);
                            if (!string.IsNullOrWhiteSpace(value))
                                result.Add(value.Trim());
                        }
                    }
                }
            }

            return result;
        }

        public async Task<BulkDeployResult> CreateDeployedSetsAsync(
            List<BulkDeployRow> validRows,
            Dictionary<string, DateTime?> bundleDates,
            int createdByUserId)
        {
            var result = new BulkDeployResult();

            if (validRows == null || validRows.Count == 0)
                return result;

            var groups = new Dictionary<string, List<BulkDeployRow>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in validRows)
            {
                if (row == null)
                    continue;
                string key = row.BundleKey != null ? row.BundleKey.Trim() : "";
                if (key.Length == 0)
                    continue;
                List<BulkDeployRow> list;
                if (!groups.TryGetValue(key, out list))
                {
                    list = new List<BulkDeployRow>();
                    groups[key] = list;
                }
                list.Add(row);
            }

            if (groups.Count == 0)
                return result;

            var datesLookup = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
            if (bundleDates != null)
            {
                foreach (var kvp in bundleDates)
                {
                    string key = kvp.Key != null ? kvp.Key.Trim() : "";
                    if (key.Length == 0)
                        continue;
                    datesLookup[key] = kvp.Value;
                }
            }

            Dictionary<string, int> deptIds;
            try
            {
                deptIds = await GetDepartmentIdsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                foreach (var kvp in groups)
                {
                    result.Details.Add(new BundleCommitResult
                    {
                        BundleKey = kvp.Key,
                        Skipped = false,
                        SetId = null,
                        SetCode = null,
                        Message = "Failed: " + ex.Message
                    });
                }
                return result;
            }

            var bundleComputerNames = new List<string>();
            var bundleComputerSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in groups)
            {
                string pc = FirstNonEmptyComputerName(kvp.Value);
                if (!string.IsNullOrEmpty(pc) && bundleComputerSeen.Add(pc))
                    bundleComputerNames.Add(pc);
            }

            HashSet<string> existingComputers;
            try
            {
                existingComputers = await GetExistingComputerNamesAsync(bundleComputerNames).ConfigureAwait(false);
            }
            catch
            {
                existingComputers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            string createdByName = createdByUserId.ToString();

            foreach (var kvp in groups)
            {
                string bundleKey = kvp.Key;
                List<BulkDeployRow> rows = kvp.Value;

                DateTime? bundleDate = null;
                DateTime? lookedUp;
                if (datesLookup.TryGetValue(bundleKey, out lookedUp))
                    bundleDate = lookedUp;

                string computerName = FirstNonEmptyComputerName(rows);
                string ipAddress = FirstNonEmptyIpAddress(rows);

                if (!string.IsNullOrEmpty(computerName) && existingComputers.Contains(computerName))
                {
                    result.Skipped++;
                    result.Details.Add(new BundleCommitResult
                    {
                        BundleKey = bundleKey,
                        Skipped = true,
                        SetId = null,
                        SetCode = null,
                        Message = "Skipped: ComputerName '" + computerName + "' already imported."
                    });
                    continue;
                }

                int? bundleEmpId = null;
                string bundleEmpName = null;
                int? bundleComId = null;
                int? bundleBranchId = null;
                string bundleBranchName = null;
                foreach (var brow in rows)
                {
                    if (brow == null)
                        continue;
                    if (!bundleEmpId.HasValue && brow.MatchedEmployeeId.HasValue)
                    {
                        bundleEmpId = brow.MatchedEmployeeId.Value;
                        bundleEmpName = brow.MatchedEmployeeName;
                    }
                    if (!bundleComId.HasValue && brow.MatchedCompanyId.HasValue)
                        bundleComId = brow.MatchedCompanyId.Value;
                    if (!bundleBranchId.HasValue && brow.MatchedBranchId.HasValue)
                    {
                        bundleBranchId = brow.MatchedBranchId.Value;
                        bundleBranchName = brow.Branch != null ? brow.Branch.Trim() : null;
                    }
                }

                string rawDept = rows[0] != null ? rows[0].Department : null;
                string canonical;
                int deptId;
                if (!DeptAliasMap.TryCanonicalize(rawDept, out canonical) || !deptIds.TryGetValue(canonical, out deptId))
                {
                    result.Details.Add(new BundleCommitResult
                    {
                        BundleKey = bundleKey,
                        Skipped = false,
                        SetId = null,
                        SetCode = null,
                        Message = "Failed: Unknown department '" + (rawDept ?? "") + "'."
                    });
                    continue;
                }

                DateTime dateRequested = bundleDate.HasValue ? bundleDate.Value : DateTime.Now;
                DateTime now = DateTime.Now;
                Guid sessionId = Guid.NewGuid();
                string setRemarks = canonical + " | BULK-DEPLOY " + bundleKey
                    + (bundleEmpId.HasValue && !string.IsNullOrWhiteSpace(bundleEmpName)
                        ? " | Assigned: " + bundleEmpName.Trim()
                        : "");
                string setStatus = bundleDate.HasValue ? "Dispatched" : "Pending";

                using (var con = new SqlConnection(GetConnectionString()))
                {
                    await con.OpenAsync().ConfigureAwait(false);
                    using (var tx = con.BeginTransaction())
                    {
                        try
                        {
                            var categoryCache = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
                            var units = new List<UnitInfo>(rows.Count);

                            foreach (var row in rows)
                            {
                                string serialTrimmed = string.IsNullOrWhiteSpace(row.SerialNumber)
                                    ? null
                                    : row.SerialNumber.Trim();

                                string monitorPending = null;
                                if (serialTrimmed == null && string.Equals(
                                        row.ItemRole != null ? row.ItemRole.Trim() : "",
                                        "Monitor",
                                        StringComparison.OrdinalIgnoreCase))
                                {
                                    monitorPending = "Monitor serial pending follow-up";
                                }

                                int? categoryId = null;
                                string categoryName = row.Category != null ? row.Category.Trim() : "";
                                if (!string.IsNullOrEmpty(categoryName))
                                {
                                    int? cached;
                                    if (categoryCache.TryGetValue(categoryName, out cached))
                                    {
                                        categoryId = cached;
                                    }
                                    else
                                    {
                                        const string sqlCategory =
                                            "SELECT TOP 1 CategoryId FROM dbo.ItemCategory WHERE Name = @C";
                                        using (var catCmd = new SqlCommand(sqlCategory, con, tx))
                                        {
                                            catCmd.Parameters.AddWithValue("@C", categoryName);
                                            object found = await catCmd.ExecuteScalarAsync().ConfigureAwait(false);
                                            categoryId = (found == null || found == DBNull.Value)
                                                ? (int?)null
                                                : Convert.ToInt32(found);
                                        }
                                        categoryCache[categoryName] = categoryId;
                                    }
                                }

                                int itemId;
                                bool itemCreated = false;

                                if (serialTrimmed != null)
                                {
                                    const string sqlFindItem =
                                        "SELECT TOP 1 ItemId FROM dbo.Item WHERE SerialNumber = @S";
                                    using (var findCmd = new SqlCommand(sqlFindItem, con, tx))
                                    {
                                        findCmd.Parameters.AddWithValue("@S", serialTrimmed);
                                        object found = await findCmd.ExecuteScalarAsync().ConfigureAwait(false);
                                        if (found != null && found != DBNull.Value)
                                        {
                                            itemId = Convert.ToInt32(found);
                                        }
                                        else
                                        {
                                            itemId = await InsertBulkItemAsync(
                                                con, tx, row, serialTrimmed, categoryId, createdByUserId, now).ConfigureAwait(false);
                                            itemCreated = true;
                                        }
                                    }
                                }
                                else
                                {
                                    itemId = await InsertBulkItemAsync(
                                        con, tx, row, null, categoryId, createdByUserId, now).ConfigureAwait(false);
                                    itemCreated = true;
                                }

                                if (itemCreated)
                                {
                                    await InsertBaselineInventoryAsync(
                                        con, tx, itemId, row, createdByUserId, now).ConfigureAwait(false);

                                    ItemAuditTrailWriter.TryLog(con, tx, new ItemAuditTrailDto
                                    {
                                        ItemId = itemId,
                                        SerialNumber = serialTrimmed,
                                        Action = "Item Created",
                                        ActionTime = now,
                                        Direction = "IN",
                                        ReferenceType = "Item",
                                        ReferenceId = itemId,
                                        Notes = "Bulk-deploy created item '" + (row.ItemName ?? "") + "' [BULK-DEPLOY " + bundleKey + "]",
                                        CreatedBy = createdByName
                                    });
                                }

                                units.Add(new UnitInfo
                                {
                                    Row = row,
                                    ItemId = itemId,
                                    SerialTrimmed = serialTrimmed,
                                    MonitorPending = monitorPending,
                                    CategoryId = categoryId
                                });
                            }

                            var committed = new List<CommittedRequest>(units.Count);
                            foreach (var unit in units)
                            {
                                BulkDeployRow row = unit.Row;
                                string itemName = row.ItemName != null ? row.ItemName.Trim() : "";
                                string description = itemName + " [BULK-DEPLOY " + bundleKey + "]";

                                var remarkParts = new List<string>(3);
                                if (!string.IsNullOrWhiteSpace(row.FixedAssetNumber))
                                    remarkParts.Add("FA:" + row.FixedAssetNumber.Trim());
                                if (!string.IsNullOrWhiteSpace(row.Vendor))
                                    remarkParts.Add("Vendor:" + row.Vendor.Trim());
                                if (!string.IsNullOrEmpty(unit.MonitorPending))
                                    remarkParts.Add(unit.MonitorPending);
                                object remarksParam = remarkParts.Count > 0
                                    ? (object)string.Join(" | ", remarkParts.ToArray())
                                    : DBNull.Value;

                                int quantity = row.Quantity < 1 ? 1 : row.Quantity;

                                const string sqlInsertRequest = @"
INSERT INTO dbo.Request
    (DateRequested, Description, Remarks, Status, EntryType, Quantity, IssuedQty,
     DateCreated, CreatedBy, DateModified, ModifiedBy, ItemId, EmpId,
     ComId, DeptId, BranchId,
     SubmissionSessionId, ConditionID, RequestSource, ReceivedById, WorkflowType)
VALUES
    (@DateRequested, @Description, @Remarks, @Status, @EntryType, @Quantity, @IssuedQty,
     @DateCreated, @CreatedBy, @DateModified, @ModifiedBy, @ItemId, @EmpId,
     @ComId, @DeptId, @BranchId,
     @SubmissionSessionId, @ConditionID, @RequestSource, @ReceivedById, @WorkflowType);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                                int newReqId;
                                using (var reqCmd = new SqlCommand(sqlInsertRequest, con, tx))
                                {
                                    reqCmd.Parameters.AddWithValue("@DateRequested", dateRequested);
                                    reqCmd.Parameters.AddWithValue("@Description", description);
                                    reqCmd.Parameters.AddWithValue("@Remarks", remarksParam);
                                    reqCmd.Parameters.AddWithValue("@Status", "Completed");
                                    reqCmd.Parameters.AddWithValue("@EntryType", "Negative");
                                    reqCmd.Parameters.AddWithValue("@Quantity", quantity);
                                    reqCmd.Parameters.AddWithValue("@IssuedQty", quantity);
                                    reqCmd.Parameters.AddWithValue("@DateCreated", now);
                                    reqCmd.Parameters.AddWithValue("@CreatedBy", createdByUserId);
                                    reqCmd.Parameters.AddWithValue("@DateModified", now);
                                    reqCmd.Parameters.AddWithValue("@ModifiedBy", createdByUserId);
                                    reqCmd.Parameters.AddWithValue("@ItemId", unit.ItemId);
                                    reqCmd.Parameters.AddWithValue("@EmpId",
                                        unit.Row.MatchedEmployeeId.HasValue
                                            ? (object)unit.Row.MatchedEmployeeId.Value
                                            : DBNull.Value);
                                    reqCmd.Parameters.AddWithValue("@ComId",
                                        unit.Row.MatchedCompanyId.HasValue
                                            ? (object)unit.Row.MatchedCompanyId.Value
                                            : DBNull.Value);
                                    reqCmd.Parameters.AddWithValue("@DeptId", deptId);
                                    reqCmd.Parameters.AddWithValue("@BranchId",
                                        unit.Row.MatchedBranchId.HasValue
                                            ? (object)unit.Row.MatchedBranchId.Value
                                            : DBNull.Value);
                                    reqCmd.Parameters.AddWithValue("@SubmissionSessionId", sessionId);
                                    reqCmd.Parameters.AddWithValue("@ConditionID", DBNull.Value);
                                    reqCmd.Parameters.AddWithValue("@RequestSource", "INTERNAL");
                                    reqCmd.Parameters.AddWithValue("@ReceivedById", DBNull.Value);
                                    reqCmd.Parameters.AddWithValue("@WorkflowType", DBNull.Value);

                                    object scalar = await reqCmd.ExecuteScalarAsync().ConfigureAwait(false);
                                    newReqId = Convert.ToInt32(scalar);
                                }

                                committed.Add(new CommittedRequest
                                {
                                    ReqId = newReqId,
                                    ItemId = unit.ItemId,
                                    SerialTrimmed = unit.SerialTrimmed,
                                    ItemName = itemName,
                                    ModelNumber = unit.Row.ModelNumber != null ? unit.Row.ModelNumber.Trim() : null,
                                    Quantity = quantity,
                                    EmpId = unit.Row.MatchedEmployeeId,
                                    EmpName = unit.Row.MatchedEmployeeName,
                                    ComId = unit.Row.MatchedCompanyId,
                                    BranchId = unit.Row.MatchedBranchId,
                                    BranchName = unit.Row.Branch != null ? unit.Row.Branch.Trim() : null
                                });
                            }

                            const string sqlInsertSet = @"
INSERT INTO dbo.[Set] (CreatedBy, CreatedAt, QRToken, Remarks, DispatchDate, Status)
VALUES (@CreatedBy, GETDATE(), NEWID(), @Remarks, @DispatchDate, @Status);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                            int newSetId;
                            using (var setCmd = new SqlCommand(sqlInsertSet, con, tx))
                            {
                                setCmd.Parameters.AddWithValue("@CreatedBy", createdByUserId);
                                setCmd.Parameters.AddWithValue("@Remarks", setRemarks);
                                setCmd.Parameters.AddWithValue("@DispatchDate",
                                    bundleDate.HasValue ? (object)bundleDate.Value : DBNull.Value);
                                setCmd.Parameters.AddWithValue("@Status", setStatus);

                                object scalar = await setCmd.ExecuteScalarAsync().ConfigureAwait(false);
                                newSetId = Convert.ToInt32(scalar);
                            }

                            const string sqlSetHardware = @"
UPDATE dbo.[Set]
SET ComputerName = @ComputerName,
    IPAddress = @IPAddress
WHERE SetId = @SetId;";

                            using (var hwCmd = new SqlCommand(sqlSetHardware, con, tx))
                            {
                                hwCmd.Parameters.AddWithValue("@SetId", newSetId);
                                hwCmd.Parameters.AddWithValue("@ComputerName",
                                    string.IsNullOrEmpty(computerName) ? (object)DBNull.Value : computerName);
                                hwCmd.Parameters.AddWithValue("@IPAddress",
                                    string.IsNullOrEmpty(ipAddress) ? (object)DBNull.Value : ipAddress);
                                await hwCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                            }

                            if (bundleEmpId.HasValue)
                            {
                                const string sqlSetEmployee = @"
IF COL_LENGTH('dbo.[Set]', 'CurrentEmployeeId') IS NOT NULL
BEGIN
    EXEC sp_executesql
        N'UPDATE dbo.[Set] SET CurrentEmployeeId = @EmpId WHERE SetId = @SetId;',
        N'@SetId INT, @EmpId INT',
        @SetId = @SetId, @EmpId = @EmpId;
END";
                                using (var empCmd = new SqlCommand(sqlSetEmployee, con, tx))
                                {
                                    empCmd.Parameters.AddWithValue("@SetId", newSetId);
                                    empCmd.Parameters.AddWithValue("@EmpId", bundleEmpId.Value);
                                    await empCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                                }
                            }

                            if (bundleComId.HasValue || bundleBranchId.HasValue)
                            {
                                const string sqlSetOrg = @"
UPDATE dbo.[Set]
SET ComId = @ComId,
    CurrentBranchId = @BranchId
WHERE SetId = @SetId;";
                                using (var orgCmd = new SqlCommand(sqlSetOrg, con, tx))
                                {
                                    orgCmd.Parameters.AddWithValue("@SetId", newSetId);
                                    orgCmd.Parameters.AddWithValue("@ComId",
                                        bundleComId.HasValue ? (object)bundleComId.Value : DBNull.Value);
                                    orgCmd.Parameters.AddWithValue("@BranchId",
                                        bundleBranchId.HasValue ? (object)bundleBranchId.Value : DBNull.Value);
                                    await orgCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                                }
                            }

                            const string sqlLinkRequest = @"
UPDATE dbo.Request
SET SetId = @SetId
WHERE ReqId = @ReqId;";

                            foreach (var c in committed)
                            {
                                using (var linkCmd = new SqlCommand(sqlLinkRequest, con, tx))
                                {
                                    linkCmd.Parameters.AddWithValue("@SetId", newSetId);
                                    linkCmd.Parameters.AddWithValue("@ReqId", c.ReqId);
                                    await linkCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                                }
                            }

                            const string sqlLinkSet = @"
UPDATE dbo.[Set]
SET ReqId = @ReqId,
    SetType = 'Hardware'
WHERE SetId = @SetId;";

                            using (var setLinkCmd = new SqlCommand(sqlLinkSet, con, tx))
                            {
                                setLinkCmd.Parameters.AddWithValue("@SetId", newSetId);
                                setLinkCmd.Parameters.AddWithValue("@ReqId", committed[0].ReqId);
                                await setLinkCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                            }

                            const string sqlInsertSetItem = @"
INSERT INTO dbo.SetItem (
    SetId, ItemId, ItemCode, Description,
    Quantity, UnitOfMeasure, UnitPrice, Amount,
    LineStartDate, CreatedBy
)
VALUES (
    @SetId, @ItemId, @ItemCode, @Description,
    @Quantity, @UnitOfMeasure, @UnitPrice, @Amount,
    @LineStartDate, @CreatedBy
);";

                            foreach (var c in committed)
                            {
                                using (var siCmd = new SqlCommand(sqlInsertSetItem, con, tx))
                                {
                                    siCmd.Parameters.AddWithValue("@SetId", newSetId);
                                    siCmd.Parameters.AddWithValue("@ItemId", c.ItemId);
                                    siCmd.Parameters.AddWithValue("@ItemCode",
                                        string.IsNullOrWhiteSpace(c.ModelNumber) ? (object)DBNull.Value : c.ModelNumber);
                                    siCmd.Parameters.AddWithValue("@Description",
                                        string.IsNullOrWhiteSpace(c.ItemName) ? (object)DBNull.Value : (object)c.ItemName);
                                    siCmd.Parameters.AddWithValue("@Quantity", c.Quantity);
                                    siCmd.Parameters.AddWithValue("@UnitOfMeasure", "pcs");
                                    siCmd.Parameters.AddWithValue("@UnitPrice", 0);
                                    siCmd.Parameters.AddWithValue("@Amount", 0);
                                    siCmd.Parameters.AddWithValue("@LineStartDate", dateRequested);
                                    siCmd.Parameters.AddWithValue("@CreatedBy", createdByUserId);
                                    await siCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                                }
                            }

                            if (bundleDate.HasValue)
                            {
                                const string sqlDispatchSet = @"
UPDATE dbo.[Set]
SET Status = 'Dispatched',
    DispatchDate = ISNULL(DispatchDate, SYSDATETIME())
WHERE SetId = @SetId
  AND ISNULL(Status, '') <> 'Dispatched';";

                                using (var dispCmd = new SqlCommand(sqlDispatchSet, con, tx))
                                {
                                    dispCmd.Parameters.AddWithValue("@SetId", newSetId);
                                    await dispCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                                }

                                const string sqlSyncIssued = @"
UPDATE dbo.Request
SET IssuedQty = Quantity
WHERE SetId = @SetId
  AND IssuedQty < Quantity;";

                                using (var syncCmd = new SqlCommand(sqlSyncIssued, con, tx))
                                {
                                    syncCmd.Parameters.AddWithValue("@SetId", newSetId);
                                    await syncCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                                }
                            }

                            string setCode = null;
                            const string sqlSetCode = "SELECT SetCode FROM dbo.[Set] WHERE SetId = @SetId;";
                            using (var codeCmd = new SqlCommand(sqlSetCode, con, tx))
                            {
                                codeCmd.Parameters.AddWithValue("@SetId", newSetId);
                                object codeObj = await codeCmd.ExecuteScalarAsync().ConfigureAwait(false);
                                if (codeObj != null && codeObj != DBNull.Value)
                                    setCode = Convert.ToString(codeObj);
                            }

                            const string sqlInsertInventory = @"
INSERT INTO dbo.Inventory
    (ItemId, Quantity, EntryType, DatePosted, PostedBy, SetId, ReqId, Description, Active)
VALUES
    (@ItemId, @Quantity, @EntryType, @DatePosted, @PostedBy, @SetId, @ReqId, @Description, 1);";

                            foreach (var c in committed)
                            {
                                using (var invCmd = new SqlCommand(sqlInsertInventory, con, tx))
                                {
                                    invCmd.Parameters.AddWithValue("@ItemId", c.ItemId);
                                    invCmd.Parameters.AddWithValue("@Quantity", c.Quantity);
                                    invCmd.Parameters.AddWithValue("@EntryType", "Negative");
                                    invCmd.Parameters.AddWithValue("@DatePosted", dateRequested);
                                    invCmd.Parameters.AddWithValue("@PostedBy", createdByUserId);
                                    invCmd.Parameters.AddWithValue("@SetId", newSetId);
                                    invCmd.Parameters.AddWithValue("@ReqId", c.ReqId);
                                    invCmd.Parameters.AddWithValue("@Description",
                                        "Deployed " + c.ItemName + " [BULK-DEPLOY " + bundleKey + "] - Request #" + c.ReqId);
                                    await invCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                                }

                                ItemAuditTrailWriter.TryLog(con, tx, new ItemAuditTrailDto
                                {
                                    ItemId = c.ItemId,
                                    SerialNumber = c.SerialTrimmed,
                                    Action = "Request Created",
                                    ActionTime = dateRequested,
                                    Direction = "OUT",
                                    Status = "Completed",
                                    EmployeeId = c.EmpId,
                                    EmployeeName = c.EmpName,
                                    DepartmentId = deptId,
                                    DepartmentName = canonical,
                                    BranchId = c.BranchId,
                                    BranchName = c.BranchName,
                                    ReferenceType = "Request",
                                    ReferenceId = c.ReqId,
                                    SetCode = setCode,
                                    Notes = "Bulk-deploy request for '" + c.ItemName + "' [BULK-DEPLOY " + bundleKey + "] - Request #" + c.ReqId,
                                    CreatedBy = createdByName
                                });
                            }

                            if (bundleDate.HasValue)
                            {
                                CommittedRequest first = committed[0];
                                ItemAuditTrailWriter.TryLog(con, tx, new ItemAuditTrailDto
                                {
                                    ItemId = first.ItemId,
                                    SerialNumber = first.SerialTrimmed,
                                    Action = "Set Dispatched",
                                    ActionTime = now,
                                    Direction = "OUT",
                                    Status = "Completed",
                                    EmployeeId = bundleEmpId,
                                    EmployeeName = bundleEmpName,
                                    DepartmentId = deptId,
                                    DepartmentName = canonical,
                                    ReferenceType = "Set",
                                    ReferenceId = newSetId,
                                    SetCode = setCode,
                                    Notes = "Bulk-deploy " + bundleKey + " dispatched to " + canonical
                                        + (bundleEmpId.HasValue && !string.IsNullOrWhiteSpace(bundleEmpName)
                                            ? " (" + bundleEmpName.Trim() + ")"
                                            : ""),
                                    CreatedBy = createdByName
                                });
                            }

                            tx.Commit();

                            result.Created++;
                            result.Details.Add(new BundleCommitResult
                            {
                                BundleKey = bundleKey,
                                Skipped = false,
                                SetId = newSetId,
                                SetCode = setCode,
                                Message = "Created Set " + (setCode ?? newSetId.ToString()) + " with " + committed.Count + " item(s)."
                            });
                        }
                        catch (Exception ex)
                        {
                            try { tx.Rollback(); }
                            catch { }
                            result.Details.Add(new BundleCommitResult
                            {
                                BundleKey = bundleKey,
                                Skipped = false,
                                SetId = null,
                                SetCode = null,
                                Message = "Failed: " + ex.Message
                            });
                        }
                    }
                }
            }

            return result;
        }

        private static string FirstNonEmptyComputerName(List<BulkDeployRow> rows)
        {
            if (rows == null)
                return null;
            foreach (var row in rows)
            {
                if (row == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(row.ComputerName))
                    return row.ComputerName.Trim();
            }
            return null;
        }

        private static string FirstNonEmptyIpAddress(List<BulkDeployRow> rows)
        {
            if (rows == null)
                return null;
            foreach (var row in rows)
            {
                if (row == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(row.IPAddress))
                    return row.IPAddress.Trim();
            }
            return null;
        }

        private static async Task<int> InsertBulkItemAsync(
            SqlConnection con,
            SqlTransaction tx,
            BulkDeployRow row,
            string serialTrimmed,
            int? categoryId,
            int createdByUserId,
            DateTime now)
        {
            string itemName = row.ItemName != null ? row.ItemName.Trim() : "";
            string description = !string.IsNullOrWhiteSpace(row.FixedAssetNumber)
                ? row.FixedAssetNumber.Trim()
                : itemName;
            string modelNumber = row.ModelNumber != null ? row.ModelNumber.Trim() : null;
            if (string.IsNullOrEmpty(modelNumber))
                modelNumber = null;
            string category = row.Category != null ? row.Category.Trim() : null;
            if (string.IsNullOrEmpty(category))
                category = null;

            int conditionId = await ResolveConditionIdAsync(con, tx, row.Condition).ConfigureAwait(false);

            const string sql = @"
INSERT INTO dbo.Item
    (Name, Description, ModelNumber, Active, CategoryId, Category,
     SerialNumber, UnitOfMeasure, StockOnHand,
     DateCreated, CreatedBy, DateModified, ModifiedBy,
     ItemType, ConditionID, AffectsInventory, AcquisitionType, IsTrackedAsset, IsBorrowable)
VALUES
    (@Name, @Description, @ModelNumber, @Active, @CategoryId, @Category,
     @SerialNumber, @UnitOfMeasure, @StockOnHand,
     @DateCreated, @CreatedBy, @DateModified, @ModifiedBy,
     @ItemType, @ConditionID, @AffectsInventory, @AcquisitionType, @IsTrackedAsset, @IsBorrowable);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var cmd = new SqlCommand(sql, con, tx))
            {
                cmd.Parameters.AddWithValue("@Name", itemName);
                cmd.Parameters.AddWithValue("@Description",
                    string.IsNullOrEmpty(description) ? (object)DBNull.Value : description);
                cmd.Parameters.AddWithValue("@ModelNumber",
                    modelNumber == null ? (object)DBNull.Value : modelNumber);
                cmd.Parameters.AddWithValue("@Active", 1);
                cmd.Parameters.AddWithValue("@CategoryId",
                    categoryId.HasValue ? (object)categoryId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@Category",
                    category == null ? (object)DBNull.Value : category);
                cmd.Parameters.AddWithValue("@SerialNumber",
                    serialTrimmed == null ? (object)DBNull.Value : serialTrimmed);
                cmd.Parameters.AddWithValue("@UnitOfMeasure", "pcs");
                cmd.Parameters.AddWithValue("@StockOnHand", 1);
                cmd.Parameters.AddWithValue("@DateCreated", now);
                cmd.Parameters.AddWithValue("@CreatedBy", createdByUserId);
                cmd.Parameters.AddWithValue("@DateModified", now);
                cmd.Parameters.AddWithValue("@ModifiedBy", createdByUserId);
                cmd.Parameters.AddWithValue("@ItemType", "Hardware");
                cmd.Parameters.AddWithValue("@ConditionID", conditionId);
                cmd.Parameters.AddWithValue("@AffectsInventory", 1);
                cmd.Parameters.AddWithValue("@AcquisitionType", "Both");
                cmd.Parameters.AddWithValue("@IsTrackedAsset", 0);
                cmd.Parameters.AddWithValue("@IsBorrowable", false);

                object scalar = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                return Convert.ToInt32(scalar);
            }
        }

        // Maps the Excel "Condition" column (free text, e.g. "Good", "Damaged")
        // to a real dbo.Condition.ConditionID. Previously this was hardcoded to
        // 1, which does not exist in dbo.Condition (valid IDs: 9=BrandNew,
        // 2=Damaged, 7=Good, 10=Refilled) and always violated FK_Item_Condition.
        // Falls back to "Good" when the row's Condition is blank or unrecognized
        // (BulkDeployValidator.ValidConditions already restricts free-text entry
        // to "Good"/"Damaged", so an unmatched value here means the condition
        // name itself was renamed/removed from dbo.Condition since that list was
        // last reviewed).
        private static async Task<int> ResolveConditionIdAsync(
            SqlConnection con, SqlTransaction tx, string conditionName)
        {
            string name = string.IsNullOrWhiteSpace(conditionName) ? "Good" : conditionName.Trim();

            const string sql = "SELECT TOP 1 ConditionID FROM dbo.Condition WHERE ConditionName = @N";
            using (var cmd = new SqlCommand(sql, con, tx))
            {
                cmd.Parameters.AddWithValue("@N", name);
                object found = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                if (found != null && found != DBNull.Value)
                    return Convert.ToInt32(found);
            }

            if (!string.Equals(name, "Good", StringComparison.OrdinalIgnoreCase))
            {
                using (var cmd = new SqlCommand(sql, con, tx))
                {
                    cmd.Parameters.AddWithValue("@N", "Good");
                    object found = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                    if (found != null && found != DBNull.Value)
                        return Convert.ToInt32(found);
                }
            }

            throw new InvalidOperationException(
                "No 'Good' row found in dbo.Condition - cannot resolve a default ConditionID.");
        }

        private static async Task InsertBaselineInventoryAsync(
            SqlConnection con,
            SqlTransaction tx,
            int itemId,
            BulkDeployRow row,
            int createdByUserId,
            DateTime now)
        {
            string itemName = row.ItemName != null ? row.ItemName.Trim() : "";
            int quantity = row.Quantity < 1 ? 1 : row.Quantity;
            int conditionId = await ResolveConditionIdAsync(con, tx, row.Condition).ConfigureAwait(false);

            const string sql = @"
INSERT INTO dbo.Inventory
    (ItemId, EntryType, Quantity, DatePosted, PostedBy, ReqId, SetId, Description, ConditionID, Active)
VALUES
    (@ItemId, @EntryType, @Quantity, @DatePosted, @PostedBy, NULL, NULL, @Description, @ConditionID, 1);";

            using (var cmd = new SqlCommand(sql, con, tx))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                cmd.Parameters.AddWithValue("@EntryType", "Positive");
                cmd.Parameters.AddWithValue("@Quantity", quantity);
                cmd.Parameters.AddWithValue("@DatePosted", now);
                cmd.Parameters.AddWithValue("@PostedBy", createdByUserId);
                cmd.Parameters.AddWithValue("@Description", itemName);
                cmd.Parameters.AddWithValue("@ConditionID", conditionId);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
    }
}
