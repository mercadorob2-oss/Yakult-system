using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Repositories
{
    public class ItemRepairHistoryRepository
    {
        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public ItemRepairHistoryRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        public async Task LogRepairsAsync(
            IEnumerable<SetItemUpdateDto> updates,
            int? conditionId,
            string conditionName,
            int processedByUserId,
            string processedByName,
            string repairAction)
        {
            if (updates == null)
                return;

            var list = new List<SetItemUpdateDto>(updates);
            if (list.Count == 0)
                return;

            const string sql = @"
INSERT INTO dbo.ItemRepairHistory
    (UpdateId, ItemId, SerialNumber, SetId, SetCode,
     PreviousStatus, NewStatus,
     ConditionId, ConditionName, RepairAction,
     Remark, CreatedAt, ProcessedByUserId, ProcessedByName)
VALUES
    (@UpdateId, @ItemId, @SerialNumber, @SetId, @SetCode,
     @PreviousStatus, @NewStatus,
     @ConditionId, @ConditionName, @RepairAction,
     @Remark, SYSUTCDATETIME(), @ProcessedByUserId, @ProcessedByName);";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();

                var condition = await ResolveConditionSnapshotAsync(con, conditionId, conditionName);

                foreach (var dto in list)
                {
                    var action = !string.IsNullOrWhiteSpace(repairAction)
                        ? repairAction
                        : conditionName;

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@UpdateId", dto.UpdateId);
                        cmd.Parameters.AddWithValue("@ItemId", (object)dto.ItemId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@SerialNumber", (object)dto.SerialNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@SetId", (object)dto.SetId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@SetCode", (object)dto.SetCode ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PreviousStatus", (object)dto.PreviousStatus ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@NewStatus", (object)dto.NewStatus ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ConditionId", condition.Id);
                        cmd.Parameters.AddWithValue("@ConditionName", condition.Name);
                        cmd.Parameters.AddWithValue("@RepairAction", action);
                        cmd.Parameters.AddWithValue("@Remark", (object)dto.Remark ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ProcessedByUserId", processedByUserId);
                        cmd.Parameters.AddWithValue("@ProcessedByName", (object)processedByName ?? DBNull.Value);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        private static async Task<(int Id, string Name)> ResolveConditionSnapshotAsync(SqlConnection con, int? conditionId, string conditionName)
        {
            if (conditionId.HasValue && conditionId.Value > 0)
            {
                using (var cmd = new SqlCommand(@"SELECT TOP (1) ConditionID, ConditionName FROM dbo.[Condition] WHERE ConditionID = @ConditionId", con))
                {
                    cmd.Parameters.AddWithValue("@ConditionId", conditionId.Value);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                            return (reader.GetInt32(0), reader.GetString(1));
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(conditionName))
            {
                using (var cmdByName = new SqlCommand(@"SELECT TOP (1) ConditionID, ConditionName FROM dbo.[Condition] WHERE ConditionName = @Name", con))
                {
                    cmdByName.Parameters.AddWithValue("@Name", conditionName);
                    using (var reader = await cmdByName.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                            return (reader.GetInt32(0), reader.GetString(1));
                    }
                }
            }

            using (var cmdAny = new SqlCommand(@"SELECT TOP (1) ConditionID, ConditionName FROM dbo.[Condition] ORDER BY ConditionID", con))
            {
                using (var reader = await cmdAny.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        throw new InvalidOperationException("No conditions configured in dbo.Condition; cannot log repair history.");

                    return (reader.GetInt32(0), reader.GetString(1));
                }
            }
        }

        public async Task LogManualRepairActionAsync(
            int? itemId,
            string serialNumber,
            int? setId,
            string setCode,
            string previousStatus,
            string newStatus,
            int? conditionId,
            string conditionName,
            int processedByUserId,
            string processedByName,
            string repairAction,
            string remark)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();

                var condition = await ResolveConditionSnapshotAsync(con, conditionId, conditionName);

                var safeSetCode = string.IsNullOrWhiteSpace(setCode) ? "MANUAL" : setCode.Trim();
                var safeRepairAction = string.IsNullOrWhiteSpace(repairAction) ? "Repaired" : repairAction.Trim();

                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        const string insertUpdateSql = @"
INSERT INTO dbo.SetItemUpdate
    (SetId, SetCode, ItemId, SerialNumber, ModelNumber, PreviousStatus, NewStatus, Remark,
     UpdatedByUserId, UpdatedByName, Source, Processed, ProcessedBy, ProcessedAt)
VALUES
    (@SetId, @SetCode, @ItemId, @SerialNumber, NULL, @PreviousStatus, @NewStatus, @Remark,
     @UpdatedByUserId, @UpdatedByName, @Source, 1, @ProcessedBy, SYSUTCDATETIME());
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int updateId;
                        using (var cmd = new SqlCommand(insertUpdateSql, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@SetId", (object)setId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@SetCode", safeSetCode);
                            cmd.Parameters.AddWithValue("@ItemId", (object)itemId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@SerialNumber", (object)serialNumber ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@PreviousStatus", (object)previousStatus ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@NewStatus", (object)newStatus ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Remark", (object)remark ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@UpdatedByUserId", processedByUserId.ToString());
                            cmd.Parameters.AddWithValue("@UpdatedByName", (object)processedByName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Source", "ManualRepair");
                            cmd.Parameters.AddWithValue("@ProcessedBy", (object)processedByName ?? DBNull.Value);
                            updateId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        const string insertHistorySql = @"
INSERT INTO dbo.ItemRepairHistory
    (UpdateId, ItemId, SerialNumber, SetId, SetCode,
     PreviousStatus, NewStatus,
     ConditionId, ConditionName, RepairAction,
     Remark, CreatedAt, ProcessedByUserId, ProcessedByName)
VALUES
    (@UpdateId, @ItemId, @SerialNumber, @SetId, @SetCode,
     @PreviousStatus, @NewStatus,
     @ConditionId, @ConditionName, @RepairAction,
     @Remark, SYSUTCDATETIME(), @ProcessedByUserId, @ProcessedByName);";

                        using (var cmd = new SqlCommand(insertHistorySql, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@UpdateId", updateId);
                            cmd.Parameters.AddWithValue("@ItemId", (object)itemId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@SerialNumber", (object)serialNumber ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@SetId", (object)setId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@SetCode", safeSetCode);
                            cmd.Parameters.AddWithValue("@PreviousStatus", (object)previousStatus ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@NewStatus", (object)newStatus ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ConditionId", condition.Id);
                            cmd.Parameters.AddWithValue("@ConditionName", condition.Name);
                            cmd.Parameters.AddWithValue("@RepairAction", safeRepairAction);
                            cmd.Parameters.AddWithValue("@Remark", (object)remark ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ProcessedByUserId", processedByUserId);
                            cmd.Parameters.AddWithValue("@ProcessedByName", (object)processedByName ?? DBNull.Value);

                            await cmd.ExecuteNonQueryAsync();
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
    }
}

