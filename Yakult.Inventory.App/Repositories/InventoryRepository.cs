using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using InventoryModel = Yakult.Inventory.App.Models.Inventory;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Repositories
{
    public class InventoryRepository
    {
        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public InventoryRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        /// <summary>
        /// Creates Inventory record using stored procedure
        /// SetId is automatically determined if not provided
        /// </summary>
        public async Task<int> CreateInventoryAsync(InventoryCreateDto dto)
        {
            using (var conn = new SqlConnection(GetConnectionString()))
            {
                try
                {
                    await conn.OpenAsync();

                    using (var cmd = new SqlCommand("dbo.sp_CreateInventory", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@Description", dto.Description ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Quantity", dto.Quantity);
                        cmd.Parameters.AddWithValue("@PostedBy", dto.PostedBy);
                        cmd.Parameters.AddWithValue("@ItemId", dto.ItemId);
                        cmd.Parameters.AddWithValue("@ReqId", dto.ReqId ?? (object)DBNull.Value);
                        
                        var setIdParam = cmd.Parameters.Add("@SetId", SqlDbType.Int);
                        setIdParam.Direction = ParameterDirection.InputOutput;
                        setIdParam.Value = dto.SetId ?? (object)DBNull.Value;

                        var returnValue = cmd.Parameters.Add("@ReturnValue", SqlDbType.Int);
                        returnValue.Direction = ParameterDirection.ReturnValue;

                        await cmd.ExecuteNonQueryAsync();

                        // Get the SetId that was used (might have been auto-determined)
                        if (setIdParam.Value != DBNull.Value)
                            dto.SetId = (int)setIdParam.Value;

                        try
                        {
                            string serialNumber = null;
                            using (var serialCmd = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", conn))
                            {
                                serialCmd.Parameters.AddWithValue("@ItemId", dto.ItemId);
                                var result = serialCmd.ExecuteScalar();
                                serialNumber = result == null || result == DBNull.Value ? null : Convert.ToString(result);
                            }
                            await new ItemAuditTrailRepository().LogActionAsync(new ItemAuditTrailDto
                            {
                                ItemId = dto.ItemId,
                                SerialNumber = serialNumber,
                                Action = "Inventory Entry Added",
                                ActionTime = DateTime.Now,
                                Direction = dto.Quantity < 0 ? "OUT" : "IN",
                                Status = "Completed",
                                ReferenceType = "Item",
                                ReferenceId = dto.ItemId,
                                Notes = dto.Description,
                                CreatedBy = AppSession.CurrentUserName ?? "System"
                            });
                        }
                        catch { /* best-effort: audit must not break inventory entry */ }

                        return (int)returnValue.Value;
                    }
                }
                catch (SqlException ex)
                {
                    throw new InventoryException($"Failed to create inventory record: {ex.Message}", ex);
                }
            }
        }

        public async Task<InventoryModel> GetInventoryByIdAsync(int invId)
        {
            using (var conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        InvId,
                        Description,
                        EntryType,
                        Quantity,
                        DatePosted,
                        PostedBy,
                        ReqId,
                        RowVer,
                        ItemId,
                        SetId
                    FROM dbo.Inventory
                    WHERE InvId = @InvId";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@InvId", invId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new InventoryModel
                            {
                                InvId = reader.GetInt32(0),
                                Description = reader.IsDBNull(1) ? null : reader.GetString(1),
                                EntryType = reader.GetString(2),
                                Quantity = reader.GetInt32(3),
                                DatePosted = reader.GetDateTime(4),
                                PostedBy = reader.GetInt32(5),
                                ReqId = reader.IsDBNull(6) ? null : (int?)reader.GetInt32(6),
                                RowVer = (byte[])reader.GetValue(7),
                                ItemId = reader.IsDBNull(8) ? null : (int?)reader.GetInt32(8),
                                SetId = reader.GetInt32(9)
                            };
                        }
                    }
                }
                return null;
            }
        }

        public async Task<bool> UpdateInventoryAsync(InventoryModel inventory)
        {
            // Validate SetId before update
            if (inventory.SetId <= 0)
                throw new InventoryException("SetId is required and must be > 0");

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();

                string query = @"
                    UPDATE dbo.Inventory
                    SET 
                        Description = @Description,
                        EntryType = @EntryType,
                        Quantity = @Quantity,
                        ReqId = @ReqId,
                        ItemId = @ItemId,
                        SetId = @SetId
                    WHERE InvId = @InvId";

                try
                {
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Description", inventory.Description ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@EntryType", inventory.EntryType);
                        cmd.Parameters.AddWithValue("@Quantity", inventory.Quantity);
                        cmd.Parameters.AddWithValue("@ReqId", inventory.ReqId ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@ItemId", inventory.ItemId ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@SetId", inventory.SetId);
                        cmd.Parameters.AddWithValue("@InvId", inventory.InvId);

                        var affected = await cmd.ExecuteNonQueryAsync();
                        return affected > 0;
                    }
                }
                catch (SqlException ex)
                {
                    throw new InventoryException($"Failed to update inventory: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Runs the stored procedure to fix orphaned inventory records
        /// </summary>
        public async Task<int> FixOrphanedInventoryAsync()
        {
            using (var conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();

                using (var cmd = new SqlCommand("dbo.sp_FixOrphanedInventory", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return reader.GetInt32(0); // RecordsFixed
                        }
                    }
                }
                return 0;
            }
        }

        public async Task<List<InventoryAuditLog>> GetAuditLogsAsync(int? invId = null, int top = 50)
        {
            var logs = new List<InventoryAuditLog>();

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();

                string query = $@"
                    SELECT TOP (@Top)
                        AuditId,
                        InvId,
                        Action,
                        SetId,
                        ItemId,
                        ReqId,
                        ErrorMessage,
                        AuditDate,
                        AuditUser
                    FROM dbo.InventoryAuditLog
                    {(invId.HasValue ? "WHERE InvId = @InvId" : "")}
                    ORDER BY AuditId DESC";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Top", top);
                    if (invId.HasValue)
                        cmd.Parameters.AddWithValue("@InvId", invId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            logs.Add(new InventoryAuditLog
                            {
                                AuditId = reader.GetInt32(0),
                                InvId = reader.IsDBNull(1) ? null : (int?)reader.GetInt32(1),
                                Action = reader.GetString(2),
                                SetId = reader.IsDBNull(3) ? null : (int?)reader.GetInt32(3),
                                ItemId = reader.IsDBNull(4) ? null : (int?)reader.GetInt32(4),
                                ReqId = reader.IsDBNull(5) ? null : (int?)reader.GetInt32(5),
                                ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
                                AuditDate = reader.GetDateTime(7),
                                AuditUser = reader.GetString(8)
                            });
                        }
                    }
                }
            }
            return logs;
        }
    }
}
