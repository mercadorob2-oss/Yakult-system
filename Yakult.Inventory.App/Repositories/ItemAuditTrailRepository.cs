using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Linq;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    public class ItemAuditTrailRepository
    {
        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public ItemAuditTrailRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        public async Task LogActionAsync(ItemAuditTrailDto audit)
        {
            const string sql = @"
            INSERT INTO ItemAuditTrail (
                ItemId, SerialNumber, Action, ActionTime, EmployeeId, EmployeeName,
                DepartmentId, DepartmentName, BranchId, BranchName, Direction,
                Status, Location, ReferenceType, ReferenceId, SetCode, Notes, CreatedBy
            ) VALUES (
                @ItemId, @SerialNumber, @Action, @ActionTime, @EmployeeId, @EmployeeName,
                @DepartmentId, @DepartmentName, @BranchId, @BranchName, @Direction,
                @Status, @Location, @ReferenceType, @ReferenceId, @SetCode, @Notes, @CreatedBy
            );";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ItemId", (object)audit.ItemId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SerialNumber", (object)audit.SerialNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Action", audit.Action);
                cmd.Parameters.AddWithValue("@ActionTime", audit.ActionTime);
                cmd.Parameters.AddWithValue("@EmployeeId", (object)audit.EmployeeId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@EmployeeName", (object)audit.EmployeeName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DepartmentId", (object)audit.DepartmentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DepartmentName", (object)audit.DepartmentName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BranchId", (object)audit.BranchId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BranchName", (object)audit.BranchName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Direction", (object)audit.Direction ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Status", (object)audit.Status ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Location", (object)audit.Location ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ReferenceType", (object)audit.ReferenceType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ReferenceId", (object)audit.ReferenceId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SetCode", (object)audit.SetCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Notes", (object)audit.Notes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedBy", (object)audit.CreatedBy ?? DBNull.Value);

                await con.OpenAsync().ConfigureAwait(false);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task LogActionAsync(ItemAuditTrailDto audit, SqlConnection connection, SqlTransaction transaction)
        {
            const string sql = @"
            INSERT INTO ItemAuditTrail (
                ItemId, SerialNumber, Action, ActionTime, EmployeeId, EmployeeName,
                DepartmentId, DepartmentName, BranchId, BranchName, Direction,
                Status, Location, ReferenceType, ReferenceId, SetCode, Notes, CreatedBy
            ) VALUES (
                @ItemId, @SerialNumber, @Action, @ActionTime, @EmployeeId, @EmployeeName,
                @DepartmentId, @DepartmentName, @BranchId, @BranchName, @Direction,
                @Status, @Location, @ReferenceType, @ReferenceId, @SetCode, @Notes, @CreatedBy
            );";

            using (var cmd = new SqlCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@ItemId", (object)audit.ItemId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SerialNumber", (object)audit.SerialNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Action", audit.Action);
                cmd.Parameters.AddWithValue("@ActionTime", audit.ActionTime);
                cmd.Parameters.AddWithValue("@EmployeeId", (object)audit.EmployeeId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@EmployeeName", (object)audit.EmployeeName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DepartmentId", (object)audit.DepartmentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DepartmentName", (object)audit.DepartmentName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BranchId", (object)audit.BranchId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BranchName", (object)audit.BranchName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Direction", (object)audit.Direction ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Status", (object)audit.Status ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Location", (object)audit.Location ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ReferenceType", (object)audit.ReferenceType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ReferenceId", (object)audit.ReferenceId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SetCode", (object)audit.SetCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Notes", (object)audit.Notes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedBy", (object)audit.CreatedBy ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<List<ItemAuditTrailDto>> GetAuditTrailAsync(string serialNumber = null, int? itemId = null, int top = 500)
        {
            return await GetAuditTrailAsync(serialNumber, itemId, null, null, top).ConfigureAwait(false);
        }

        public async Task<List<ItemAuditTrailDto>> GetAuditTrailAsync(string serialNumber, int? itemId, string referenceType, int? referenceId, int top = 500)
        {
            const string sql = @"
            SELECT TOP (@Top)
                Id, ItemId, SerialNumber, Action, ActionTime, EmployeeId, EmployeeName,
                DepartmentId, DepartmentName, BranchId, BranchName, Direction,
                Status, Location, ReferenceType, ReferenceId, SetCode, Notes, CreatedAt, CreatedBy
            FROM ItemAuditTrail
            WHERE (@SerialNumber IS NULL OR SerialNumber = @SerialNumber)
              AND (@ItemId IS NULL OR ItemId = @ItemId)
              AND (@ReferenceType IS NULL OR ReferenceType = @ReferenceType)
              AND (@ReferenceId IS NULL OR ReferenceId = @ReferenceId)
            ORDER BY ActionTime DESC;";

            var results = new List<ItemAuditTrailDto>();

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SerialNumber", (object)serialNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ItemId", (object)itemId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ReferenceType", (object)referenceType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ReferenceId", (object)referenceId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Top", top);

                await con.OpenAsync().ConfigureAwait(false);
                using (var rdr = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await rdr.ReadAsync().ConfigureAwait(false))
                    {
                        results.Add(new ItemAuditTrailDto
                        {
                            Id = rdr.GetInt32(rdr.GetOrdinal("Id")),
                            ItemId = rdr.IsDBNull(rdr.GetOrdinal("ItemId")) ? (int?)null : rdr.GetInt32(rdr.GetOrdinal("ItemId")),
                            SerialNumber = rdr.IsDBNull(rdr.GetOrdinal("SerialNumber")) ? null : rdr.GetString(rdr.GetOrdinal("SerialNumber")),
                            Action = rdr.GetString(rdr.GetOrdinal("Action")),
                            ActionTime = rdr.GetDateTime(rdr.GetOrdinal("ActionTime")),
                            EmployeeId = rdr.IsDBNull(rdr.GetOrdinal("EmployeeId")) ? (int?)null : rdr.GetInt32(rdr.GetOrdinal("EmployeeId")),
                            EmployeeName = rdr.IsDBNull(rdr.GetOrdinal("EmployeeName")) ? null : rdr.GetString(rdr.GetOrdinal("EmployeeName")),
                            DepartmentId = rdr.IsDBNull(rdr.GetOrdinal("DepartmentId")) ? (int?)null : rdr.GetInt32(rdr.GetOrdinal("DepartmentId")),
                            DepartmentName = rdr.IsDBNull(rdr.GetOrdinal("DepartmentName")) ? null : rdr.GetString(rdr.GetOrdinal("DepartmentName")),
                            BranchId = rdr.IsDBNull(rdr.GetOrdinal("BranchId")) ? (int?)null : rdr.GetInt32(rdr.GetOrdinal("BranchId")),
                            BranchName = rdr.IsDBNull(rdr.GetOrdinal("BranchName")) ? null : rdr.GetString(rdr.GetOrdinal("BranchName")),
                            Direction = rdr.IsDBNull(rdr.GetOrdinal("Direction")) ? null : rdr.GetString(rdr.GetOrdinal("Direction")),
                            Status = rdr.IsDBNull(rdr.GetOrdinal("Status")) ? null : rdr.GetString(rdr.GetOrdinal("Status")),
                            Location = rdr.IsDBNull(rdr.GetOrdinal("Location")) ? null : rdr.GetString(rdr.GetOrdinal("Location")),
                            ReferenceType = rdr.IsDBNull(rdr.GetOrdinal("ReferenceType")) ? null : rdr.GetString(rdr.GetOrdinal("ReferenceType")),
                            ReferenceId = rdr.IsDBNull(rdr.GetOrdinal("ReferenceId")) ? (int?)null : rdr.GetInt32(rdr.GetOrdinal("ReferenceId")),
                            SetCode = rdr.IsDBNull(rdr.GetOrdinal("SetCode")) ? null : rdr.GetString(rdr.GetOrdinal("SetCode")),
                            Notes = rdr.IsDBNull(rdr.GetOrdinal("Notes")) ? null : rdr.GetString(rdr.GetOrdinal("Notes")),
                            CreatedAt = rdr.GetDateTime(rdr.GetOrdinal("CreatedAt")),
                            CreatedBy = rdr.IsDBNull(rdr.GetOrdinal("CreatedBy")) ? null : rdr.GetString(rdr.GetOrdinal("CreatedBy"))
                        });
                    }
                }
            }

            return results.OrderBy(x => x.ActionTime).ToList();
        }
    }
}
