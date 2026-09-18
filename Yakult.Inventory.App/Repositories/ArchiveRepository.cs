using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Yakult.Inventory.App.Models;
namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Repository for archive operations - safely archives records without deletion
    /// </summary>
    public class ArchiveRepository
    {
        private readonly string _connectionString;

        public ArchiveRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        #region Archive Item

        /// <summary>
        /// Archive an item and mark it as inactive
        /// </summary>
        /// <param name="itemId">Item ID to archive</param>
        /// <param name="archivedBy">Username or ID of user archiving</param>
        /// <param name="archiveReason">Reason for archiving</param>
        /// <returns>Tuple: (Success, Message)</returns>
        public async Task<(bool Success, string Message)> ArchiveItemAsync(int itemId, string archivedBy, string archiveReason)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    using (var cmd = new SqlCommand("dbo.sp_ArchiveItem", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@ItemId", itemId);
                        cmd.Parameters.AddWithValue("@ArchivedBy", archivedBy);
                        cmd.Parameters.AddWithValue("@ArchiveReason", archiveReason);

                        await cmd.ExecuteNonQueryAsync();
                    }

                    await LogItemArchivedAsync(itemId, archivedBy, archiveReason);
                    return (true, $"Item {itemId} archived successfully.");
                }
            }
            catch (SqlException ex)
            {
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Error archiving item: {ex.Message}");
            }
        }

        private async Task LogItemArchivedAsync(int itemId, string archivedBy, string archiveReason)
        {
            try
            {
                string serial = null;
                using (var con = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", con))
                {
                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                    await con.OpenAsync();
                    var result = await cmd.ExecuteScalarAsync();
                    serial = result == null || result == DBNull.Value ? null : Convert.ToString(result);
                }

                await new ItemAuditTrailRepository().LogActionAsync(new ItemAuditTrailDto
                {
                    ItemId = itemId,
                    SerialNumber = serial,
                    Action = "Item Archived",
                    ActionTime = DateTime.Now,
                    Direction = null,
                    Status = "Completed",
                    ReferenceType = "Item",
                    ReferenceId = itemId,
                    Notes = string.IsNullOrWhiteSpace(archiveReason)
                        ? "Archived via sp_ArchiveItem."
                        : $"Archived via sp_ArchiveItem. Reason: {archiveReason}",
                    CreatedBy = string.IsNullOrWhiteSpace(archivedBy) ? "System" : archivedBy
                });
            }
            catch { /* best-effort: audit must not break archiving */ }
        }

        #endregion

        #region Archive Set

        /// <summary>
        /// Archive a set and all related SetItems, mark as inactive
        /// </summary>
        /// <param name="setId">Set ID to archive</param>
        /// <param name="archivedBy">Username or ID of user archiving</param>
        /// <param name="archiveReason">Reason for archiving</param>
        /// <returns>Tuple: (Success, Message)</returns>
        public async Task<(bool Success, string Message)> ArchiveSetAsync(int setId, string archivedBy, string archiveReason)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    using (var cmd = new SqlCommand("dbo.sp_ArchiveSet", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@SetId", setId);
                        cmd.Parameters.AddWithValue("@ArchivedBy", archivedBy);
                        cmd.Parameters.AddWithValue("@ArchiveReason", archiveReason);

                        await cmd.ExecuteNonQueryAsync();
                        return (true, $"Set {setId} archived successfully.");
                    }
                }
            }
            catch (SqlException ex)
            {
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Error archiving set: {ex.Message}");
            }
        }

        #endregion

        #region Archive Request

        /// <summary>
        /// Archive a request and mark it as inactive
        /// </summary>
        /// <param name="reqId">Request ID to archive</param>
        /// <param name="archivedBy">Username or ID of user archiving</param>
        /// <param name="archiveReason">Reason for archiving</param>
        /// <returns>Tuple: (Success, Message)</returns>
        public async Task<(bool Success, string Message)> ArchiveRequestAsync(int reqId, string archivedBy, string archiveReason)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    using (var cmd = new SqlCommand("dbo.sp_ArchiveRequest", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@ReqId", reqId);
                        cmd.Parameters.AddWithValue("@ArchivedBy", archivedBy);
                        cmd.Parameters.AddWithValue("@ArchiveReason", archiveReason);

                        await cmd.ExecuteNonQueryAsync();
                        return (true, $"Request {reqId} archived successfully.");
                    }
                }
            }
            catch (SqlException ex)
            {
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Error archiving request: {ex.Message}");
            }
        }

        #endregion

        #region Archive Inventory

        /// <summary>
        /// Archive an inventory record and mark it as inactive
        /// </summary>
        /// <param name="invId">Inventory ID to archive</param>
        /// <param name="archivedBy">Username or ID of user archiving</param>
        /// <param name="archiveReason">Reason for archiving</param>
        /// <returns>Tuple: (Success, Message)</returns>
        public async Task<(bool Success, string Message)> ArchiveInventoryAsync(int invId, string archivedBy, string archiveReason)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    using (var cmd = new SqlCommand("dbo.sp_ArchiveInventory", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@InvId", invId);
                        cmd.Parameters.AddWithValue("@ArchivedBy", archivedBy);
                        cmd.Parameters.AddWithValue("@ArchiveReason", archiveReason);

                        await cmd.ExecuteNonQueryAsync();
                        return (true, $"Inventory record {invId} archived successfully.");
                    }
                }
            }
            catch (SqlException ex)
            {
                return (false, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Error archiving inventory: {ex.Message}");
            }
        }

        #endregion

        #region Query Archive

        /// <summary>
        /// Get all archived items
        /// </summary>
        public async Task<DataTable> GetArchivedItemsAsync()
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                var sql = @"
                    SELECT 
                        ArchiveId, ItemId, Name, Category, ArchivedAt, ArchivedBy, ArchiveReason
                    FROM dbo.Item_Archive
                    ORDER BY ArchivedAt DESC";

                using (var cmd = new SqlCommand(sql, con))
                using (var adapter = new SqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }

        /// <summary>
        /// Get all archived sets
        /// </summary>
        public async Task<DataTable> GetArchivedSetsAsync()
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                var sql = @"
                    SELECT 
                        ArchiveId, SetId, SetCode, SetType, ArchivedAt, ArchivedBy, ArchiveReason
                    FROM dbo.Set_Archive
                    ORDER BY ArchivedAt DESC";

                using (var cmd = new SqlCommand(sql, con))
                using (var adapter = new SqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }

        /// <summary>
        /// Get all archived requests
        /// </summary>
        public async Task<DataTable> GetArchivedRequestsAsync()
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                var sql = @"
                    SELECT 
                        ArchiveId, ReqId, Description, Status, ArchivedAt, ArchivedBy, ArchiveReason
                    FROM dbo.Request_Archive
                    ORDER BY ArchivedAt DESC";

                using (var cmd = new SqlCommand(sql, con))
                using (var adapter = new SqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }

        /// <summary>
        /// Get archive statistics
        /// </summary>
        public async Task<ArchiveStatistics> GetArchiveStatisticsAsync()
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                var sql = @"
                    SELECT 
                        (SELECT COUNT(*) FROM dbo.Item_Archive) AS ItemCount,
                        (SELECT COUNT(*) FROM dbo.Set_Archive) AS SetCount,
                        (SELECT COUNT(*) FROM dbo.SetItem_Archive) AS SetItemCount,
                        (SELECT COUNT(*) FROM dbo.Request_Archive) AS RequestCount,
                        (SELECT COUNT(*) FROM dbo.Inventory_Archive) AS InventoryCount";

                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new ArchiveStatistics
                        {
                            ItemCount = reader.GetInt32(0),
                            SetCount = reader.GetInt32(1),
                            SetItemCount = reader.GetInt32(2),
                            RequestCount = reader.GetInt32(3),
                            InventoryCount = reader.GetInt32(4)
                        };
                    }
                }
            }

            return new ArchiveStatistics();
        }

        /// <summary>
        /// Check if a record is already archived
        /// </summary>
        public async Task<bool> IsItemArchivedAsync(int itemId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                var sql = "SELECT COUNT(*) FROM dbo.Item_Archive WHERE ItemId = @ItemId";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                    int count = (int)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
            }
        }

        /// <summary>
        /// Check if a set is already archived
        /// </summary>
        public async Task<bool> IsSetArchivedAsync(int setId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                var sql = "SELECT COUNT(*) FROM dbo.Set_Archive WHERE SetId = @SetId";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@SetId", setId);
                    int count = (int)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Archive statistics data model
    /// </summary>
    public class ArchiveStatistics
    {
        public int ItemCount { get; set; }
        public int SetCount { get; set; }
        public int SetItemCount { get; set; }
        public int RequestCount { get; set; }
        public int InventoryCount { get; set; }

        public int TotalCount => ItemCount + SetCount + SetItemCount + RequestCount + InventoryCount;
    }
}
