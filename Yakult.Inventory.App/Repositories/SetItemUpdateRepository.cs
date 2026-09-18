using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Repositories
{
    public class SetItemUpdateRepository
    {
        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public SetItemUpdateRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        public async Task<List<SetItemUpdateDto>> GetUnprocessedAsync()
        {
            return await GetFilteredAsync(includeProcessed: false, includeUnprocessed: true);
        }

        public async Task<int> GetUnprocessedCountAsync()
        {
            const string sql = "SELECT COUNT(*) FROM dbo.SetItemUpdate WHERE Processed = 0";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
        }

        public async Task<List<SetItemUpdateDto>> GetFilteredAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool includeProcessed = false,
            bool includeUnprocessed = true,
            string setCode = null,
            string status = null)
        {
            const string sql = @"SELECT 
    u.UpdateId,
    u.SetId,
    u.SetCode,
    u.ItemId,
    u.ItemType,
    u.SerialNumber,
    u.ModelNumber,
    u.PreviousStatus,
    u.NewStatus,
    u.Remark,
    u.UpdatedByUserId,
    u.UpdatedByName,
    u.Source,
    u.CreatedAt,
    u.Processed,
    u.ProcessedBy,
    u.ProcessedAt,
    loc.BranchName,
    loc.DepartmentName,
    CAST(CASE WHEN u.ItemId IS NOT NULL AND i.ItemId IS NULL THEN 1 ELSE 0 END AS BIT) as IsMissing,
    CAST(CASE WHEN arc.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS BIT) as IsArchived
FROM dbo.SetItemUpdate u
LEFT JOIN dbo.Item i ON u.ItemId = i.ItemId
LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Item' AND arc.EntityId = u.ItemId AND arc.IsArchived = 1
OUTER APPLY (
    SELECT TOP (1)
        b.Name AS BranchName,
        d.Name AS DepartmentName
    FROM dbo.[Set] s
    LEFT JOIN dbo.Request r ON r.SetId = s.SetId
    LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
    LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
    LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
    WHERE s.SetId = u.SetId
) loc
WHERE 1=1
{0}
ORDER BY u.CreatedAt DESC";

            var conditions = new List<string>();
            var parameters = new List<SqlParameter>();

            if (fromDate.HasValue)
            {
                conditions.Add("u.CreatedAt >= @FromDate");
                parameters.Add(new SqlParameter("@FromDate", SqlDbType.DateTime) { Value = fromDate.Value });
            }

            if (toDate.HasValue)
            {
                conditions.Add("u.CreatedAt < @ToDate");
                parameters.Add(new SqlParameter("@ToDate", SqlDbType.DateTime) { Value = toDate.Value.AddDays(1) });
            }

            if (!string.IsNullOrWhiteSpace(setCode))
            {
                conditions.Add("u.SetCode LIKE @SetCode");
                parameters.Add(new SqlParameter("@SetCode", SqlDbType.NVarChar, 50) { Value = $"%{setCode}%" });
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status.Equals("Processed", StringComparison.OrdinalIgnoreCase))
                {
                    conditions.Add("u.Processed = 1");
                }
                else if (status.Equals("Unprocessed", StringComparison.OrdinalIgnoreCase))
                {
                    conditions.Add("u.Processed = 0");
                }
            }
            else
            {
                if (includeProcessed && !includeUnprocessed)
                {
                    conditions.Add("u.Processed = 1");
                }
                else if (!includeProcessed && includeUnprocessed)
                {
                    conditions.Add("u.Processed = 0");
                }
                else if (!includeProcessed && !includeUnprocessed)
                {
                    // If both are false, return empty list
                    return new List<SetItemUpdateDto>();
                }
            }

            var finalSql = string.Format(sql, conditions.Count > 0 ? "AND " + string.Join(" AND ", conditions) : string.Empty);

            var results = new List<SetItemUpdateDto>();

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(finalSql, con))
            {
                cmd.Parameters.AddRange(parameters.ToArray());

                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var dto = new SetItemUpdateDto
                        {
                            UpdateId = reader.GetInt32(reader.GetOrdinal("UpdateId")),
                            SetId = reader.IsDBNull(reader.GetOrdinal("SetId"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("SetId")),
                            SetCode = reader.GetString(reader.GetOrdinal("SetCode")),
                            ItemId = reader.IsDBNull(reader.GetOrdinal("ItemId"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("ItemId")),
                            ItemType = reader.IsDBNull(reader.GetOrdinal("ItemType"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ItemType")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            PreviousStatus = reader.IsDBNull(reader.GetOrdinal("PreviousStatus"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("PreviousStatus")),
                            NewStatus = reader.IsDBNull(reader.GetOrdinal("NewStatus"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("NewStatus")),
                            Remark = reader.IsDBNull(reader.GetOrdinal("Remark"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Remark")),
                            UpdatedByUserId = reader.IsDBNull(reader.GetOrdinal("UpdatedByUserId"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("UpdatedByUserId")),
                            UpdatedByName = reader.IsDBNull(reader.GetOrdinal("UpdatedByName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("UpdatedByName")),
                            Source = reader.IsDBNull(reader.GetOrdinal("Source"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Source")),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                            Processed = reader.GetBoolean(reader.GetOrdinal("Processed")),
                            ProcessedBy = reader.IsDBNull(reader.GetOrdinal("ProcessedBy"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ProcessedBy")),
                            ProcessedAt = reader.IsDBNull(reader.GetOrdinal("ProcessedAt"))
                                ? (DateTime?)null
                                : reader.GetDateTime(reader.GetOrdinal("ProcessedAt")),
                            BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("BranchName")),
                            DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("DepartmentName")),
                            IsMissing = reader.GetBoolean(reader.GetOrdinal("IsMissing")),
                            IsArchived = reader.GetBoolean(reader.GetOrdinal("IsArchived"))
                        };

                        results.Add(dto);
                    }
                }
            }

            return results;
        }

        public async Task<List<SetItemUpdateDto>> GetHistoryBySerialAsync(string serialNumber, int top = 200)
        {
            var serial = (serialNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(serial))
            {
                return new List<SetItemUpdateDto>();
            }

            const string sql = @"
SELECT TOP (@Top)
    u.UpdateId,
    u.SetId,
    u.SetCode,
    u.ItemId,
    u.ItemType,
    u.SerialNumber,
    u.ModelNumber,
    u.PreviousStatus,
    u.NewStatus,
    u.Remark,
    u.UpdatedByUserId,
    u.UpdatedByName,
    u.Source,
    u.CreatedAt,
    u.Processed,
    u.ProcessedBy,
    u.ProcessedAt,
    loc.BranchName,
    loc.DepartmentName,
    CAST(CASE WHEN u.ItemId IS NOT NULL AND i.ItemId IS NULL THEN 1 ELSE 0 END AS BIT) as IsMissing,
    CAST(CASE WHEN arc.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS BIT) as IsArchived
FROM dbo.SetItemUpdate u
LEFT JOIN dbo.Item i ON u.ItemId = i.ItemId
LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Item' AND arc.EntityId = u.ItemId AND arc.IsArchived = 1
OUTER APPLY (
    SELECT TOP (1)
        b.Name AS BranchName,
        d.Name AS DepartmentName
    FROM dbo.[Set] s
    LEFT JOIN dbo.Request r ON r.SetId = s.SetId
    LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
    LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
    LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
    WHERE s.SetId = u.SetId
) loc
WHERE u.SerialNumber = @SerialNumber
ORDER BY u.CreatedAt DESC;";

            var results = new List<SetItemUpdateDto>();

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.Add("@Top", SqlDbType.Int).Value = top <= 0 ? 200 : top;
                cmd.Parameters.Add("@SerialNumber", SqlDbType.NVarChar, 200).Value = serial;

                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new SetItemUpdateDto
                        {
                            UpdateId = reader.GetInt32(reader.GetOrdinal("UpdateId")),
                            SetId = reader.IsDBNull(reader.GetOrdinal("SetId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("SetId")),
                            SetCode = reader.IsDBNull(reader.GetOrdinal("SetCode")) ? null : reader.GetString(reader.GetOrdinal("SetCode")),
                            ItemId = reader.IsDBNull(reader.GetOrdinal("ItemId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ItemId")),
                            ItemType = reader.IsDBNull(reader.GetOrdinal("ItemType")) ? null : reader.GetString(reader.GetOrdinal("ItemType")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? null : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            PreviousStatus = reader.IsDBNull(reader.GetOrdinal("PreviousStatus")) ? null : reader.GetString(reader.GetOrdinal("PreviousStatus")),
                            NewStatus = reader.IsDBNull(reader.GetOrdinal("NewStatus")) ? null : reader.GetString(reader.GetOrdinal("NewStatus")),
                            Remark = reader.IsDBNull(reader.GetOrdinal("Remark")) ? null : reader.GetString(reader.GetOrdinal("Remark")),
                            UpdatedByUserId = reader.IsDBNull(reader.GetOrdinal("UpdatedByUserId")) ? null : reader.GetString(reader.GetOrdinal("UpdatedByUserId")),
                            UpdatedByName = reader.IsDBNull(reader.GetOrdinal("UpdatedByName")) ? null : reader.GetString(reader.GetOrdinal("UpdatedByName")),
                            Source = reader.IsDBNull(reader.GetOrdinal("Source")) ? null : reader.GetString(reader.GetOrdinal("Source")),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                            Processed = !reader.IsDBNull(reader.GetOrdinal("Processed")) && reader.GetBoolean(reader.GetOrdinal("Processed")),
                            ProcessedBy = reader.IsDBNull(reader.GetOrdinal("ProcessedBy")) ? null : reader.GetString(reader.GetOrdinal("ProcessedBy")),
                            ProcessedAt = reader.IsDBNull(reader.GetOrdinal("ProcessedAt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ProcessedAt")),
                            BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName")) ? null : reader.GetString(reader.GetOrdinal("BranchName")),
                            DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString(reader.GetOrdinal("DepartmentName")),
                            IsMissing = !reader.IsDBNull(reader.GetOrdinal("IsMissing")) && reader.GetBoolean(reader.GetOrdinal("IsMissing")),
                            IsArchived = !reader.IsDBNull(reader.GetOrdinal("IsArchived")) && reader.GetBoolean(reader.GetOrdinal("IsArchived"))
                        });
                    }
                }
            }

            return results;
        }

        public async Task<bool> MarkAsProcessedAsync(int updateId, int processedByUserId, string processedByName)
        {
            const string sql = @"UPDATE dbo.SetItemUpdate
SET Processed = 1,
    ProcessedBy = @ProcessedBy,
    ProcessedAt = SYSDATETIME()
WHERE UpdateId = @UpdateId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@UpdateId", updateId);
                cmd.Parameters.AddWithValue("@ProcessedBy", (object)processedByName ?? processedByUserId.ToString());

                await con.OpenAsync();
                var rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
        }

        public async Task<int> MarkAsProcessedBatchAsync(IEnumerable<int> updateIds, int processedByUserId, string processedByName)
        {
            if (updateIds == null || !updateIds.Any())
                return 0;

            var idList = string.Join(",", updateIds);
            var sql = $@"UPDATE dbo.SetItemUpdate
SET Processed = 1,
    ProcessedBy = @ProcessedBy,
    ProcessedAt = SYSDATETIME()
WHERE UpdateId IN (SELECT value FROM STRING_SPLIT(@UpdateIds, ','))";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@UpdateIds", idList);
                cmd.Parameters.AddWithValue("@ProcessedBy", (object)processedByName ?? processedByUserId.ToString());

                await con.OpenAsync();
                return await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<SetItemUpdateDto> GetByIdAsync(int updateId)
        {
            const string sql = @"SELECT 
    u.UpdateId,
    u.SetId,
    u.SetCode,
    u.ItemId,
    u.ItemType,
    u.SerialNumber,
    u.ModelNumber,
    u.PreviousStatus,
    u.NewStatus,
    u.Remark,
    u.UpdatedByUserId,
    u.UpdatedByName,
    u.Source,
    u.CreatedAt,
    u.Processed,
    u.ProcessedBy,
    u.ProcessedAt,
    loc.BranchName,
    loc.DepartmentName
FROM dbo.SetItemUpdate u
OUTER APPLY (
    SELECT TOP (1)
        b.Name AS BranchName,
        d.Name AS DepartmentName
    FROM dbo.[Set] s
    LEFT JOIN dbo.Request r ON r.SetId = s.SetId
    LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
    LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
    LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
    WHERE s.SetId = u.SetId
) loc
WHERE u.UpdateId = @UpdateId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@UpdateId", updateId);

                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new SetItemUpdateDto
                        {
                            UpdateId = reader.GetInt32(reader.GetOrdinal("UpdateId")),
                            SetId = reader.IsDBNull(reader.GetOrdinal("SetId"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("SetId")),
                            SetCode = reader.GetString(reader.GetOrdinal("SetCode")),
                            ItemId = reader.IsDBNull(reader.GetOrdinal("ItemId"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("ItemId")),
                            ItemType = reader.IsDBNull(reader.GetOrdinal("ItemType"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ItemType")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            PreviousStatus = reader.IsDBNull(reader.GetOrdinal("PreviousStatus"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("PreviousStatus")),
                            NewStatus = reader.IsDBNull(reader.GetOrdinal("NewStatus"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("NewStatus")),
                            Remark = reader.IsDBNull(reader.GetOrdinal("Remark"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Remark")),
                            UpdatedByUserId = reader.IsDBNull(reader.GetOrdinal("UpdatedByUserId"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("UpdatedByUserId")),
                            UpdatedByName = reader.IsDBNull(reader.GetOrdinal("UpdatedByName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("UpdatedByName")),
                            Source = reader.IsDBNull(reader.GetOrdinal("Source"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Source")),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                            Processed = reader.GetBoolean(reader.GetOrdinal("Processed")),
                            ProcessedBy = reader.IsDBNull(reader.GetOrdinal("ProcessedBy"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ProcessedBy")),
                            ProcessedAt = reader.IsDBNull(reader.GetOrdinal("ProcessedAt"))
                                ? (DateTime?)null
                                : reader.GetDateTime(reader.GetOrdinal("ProcessedAt")),
                            BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("BranchName")),
                            DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("DepartmentName"))
                        };
                    }
                }
            }

            return null;
        }
    }
}

