using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Repositories
{
    public class VendorRepository
    {
        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public VendorRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        /// <summary>
        /// Gets all vendors
        /// </summary>
        public async Task<List<VendorDto>> GetAllVendorsAsync()
        {
            var vendors = new List<VendorDto>();

            const string sql = @"
                SELECT
                    v.VendorID,
                    v.VendorName,
                    v.Address,
                    v.IsActive,
                    v.CreatedDate,
                    v.TIN,
                    ISNULL(v.IsRefiller, 0) AS IsRefiller,
                    ISNULL(v.IsDisposer, 0) AS IsDisposer,
                    ISNULL(v.IsBuyer,    0) AS IsBuyer,
                    CASE WHEN arc.ArchiveId IS NULL THEN 0 ELSE 1 END AS IsArchived
                FROM dbo.Vendor v
                LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Vendor' AND arc.EntityId = v.VendorID AND arc.IsArchived = 1
                ORDER BY v.CreatedDate DESC, v.VendorID DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        bool isArchived = reader.GetInt32(9) == 1;
                        vendors.Add(new VendorDto
                        {
                            VendorId = reader.GetInt32(0),
                            VendorName = reader.GetString(1),
                            Address = reader.IsDBNull(2) ? null : reader.GetString(2),
                            IsActive = reader.GetBoolean(3) && !isArchived,
                            CreatedDate = reader.GetDateTime(4),
                            TIN = reader.IsDBNull(5) ? null : reader.GetString(5),
                            IsRefiller = reader.GetBoolean(6),
                            IsDisposer = reader.GetBoolean(7),
                            IsBuyer    = reader.GetBoolean(8),
                            IsArchived = isArchived
                        });
                    }
                }
            }

            return vendors;
        }

        /// <summary>
        /// Gets a vendor by ID
        /// </summary>
        public async Task<VendorDto> GetVendorByIdAsync(int vendorId)
        {
            const string sql = @"
                SELECT
                    VendorID,
                    VendorName,
                    Address,
                    IsActive,
                    CreatedDate,
                    TIN
                FROM dbo.Vendor
                WHERE VendorID = @VendorId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@VendorId", vendorId);
                await con.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new VendorDto
                        {
                            VendorId = reader.GetInt32(0),
                            VendorName = reader.GetString(1),
                            Address = reader.IsDBNull(2) ? null : reader.GetString(2),
                            IsActive = reader.GetBoolean(3),
                            CreatedDate = reader.GetDateTime(4),
                            TIN = reader.IsDBNull(5) ? null : reader.GetString(5)
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Adds a new vendor
        /// </summary>
        public async Task<int> AddVendorAsync(VendorDto vendor)
        {
            const string sql = @"
                INSERT INTO dbo.Vendor (VendorName, Address, IsActive, IsRefiller, IsDisposer, IsBuyer, CreatedDate, TIN)
                VALUES (@VendorName, @Address, @IsActive, @IsRefiller, @IsDisposer, @IsBuyer, @CreatedDate, @TIN);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@VendorName", vendor.VendorName);
                cmd.Parameters.AddWithValue("@Address", (object)vendor.Address ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@IsActive",   vendor.IsActive);
                cmd.Parameters.AddWithValue("@IsRefiller", vendor.IsRefiller);
                cmd.Parameters.AddWithValue("@IsDisposer", vendor.IsDisposer);
                cmd.Parameters.AddWithValue("@IsBuyer",    vendor.IsBuyer);
                cmd.Parameters.AddWithValue("@CreatedDate", vendor.CreatedDate);
                cmd.Parameters.AddWithValue("@TIN", (object)vendor.TIN ?? DBNull.Value);

                await con.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                int newVendorId = Convert.ToInt32(result);
                ActivityLogger.Log(ActivityLogger.Actions.Create, "Vendor", newVendorId, $"Vendor '{vendor.VendorName}' created");
                return newVendorId;
            }
        }

        /// <summary>
        /// Updates an existing vendor
        /// </summary>
        public async Task<bool> UpdateVendorAsync(VendorDto vendor)
        {
            const string sql = @"
                UPDATE dbo.Vendor
                SET VendorName = @VendorName,
                    Address    = @Address,
                    IsActive   = @IsActive,
                    IsRefiller = @IsRefiller,
                    IsDisposer = @IsDisposer,
                    IsBuyer    = @IsBuyer,
                    TIN        = @TIN
                WHERE VendorID = @VendorId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@VendorId", vendor.VendorId);
                cmd.Parameters.AddWithValue("@VendorName", vendor.VendorName);
                cmd.Parameters.AddWithValue("@Address", (object)vendor.Address ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@IsActive",   vendor.IsActive);
                cmd.Parameters.AddWithValue("@IsRefiller", vendor.IsRefiller);
                cmd.Parameters.AddWithValue("@IsDisposer", vendor.IsDisposer);
                cmd.Parameters.AddWithValue("@IsBuyer",    vendor.IsBuyer);
                cmd.Parameters.AddWithValue("@TIN", (object)vendor.TIN ?? DBNull.Value);

                await con.OpenAsync();
                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                    ActivityLogger.Log(ActivityLogger.Actions.Update, "Vendor", vendor.VendorId, $"Vendor '{vendor.VendorName}' updated");
                return rowsAffected > 0;
            }
        }

        public async Task<bool> UpdateItemVendorAsync(int itemId, int? vendorId)
        {
            const string sql = @"
                UPDATE dbo.Item
                SET VendorId = @VendorId
                WHERE ItemId = @ItemId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                cmd.Parameters.AddWithValue("@VendorId", (object)vendorId ?? DBNull.Value);

                await con.OpenAsync();
                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                {
                    string serial = null;
                    string vendorName = null;
                    using (var lookup = new SqlCommand(@"SELECT i.SerialNumber, v.VendorName
                        FROM dbo.Item i
                        LEFT JOIN dbo.Vendor v ON v.VendorID = i.VendorId
                        WHERE i.ItemId = @ItemId", con))
                    {
                        lookup.Parameters.AddWithValue("@ItemId", itemId);
                        using (var reader = await lookup.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                serial = reader.IsDBNull(0) ? null : reader.GetString(0);
                                vendorName = reader.IsDBNull(1) ? null : reader.GetString(1);
                            }
                        }
                    }
                    try
                    {
                        await new ItemAuditTrailRepository().LogActionAsync(new ItemAuditTrailDto
                        {
                            ItemId = itemId,
                            SerialNumber = serial,
                            Action = "Item Vendor Updated",
                            ActionTime = DateTime.Now,
                            Status = "Completed",
                            ReferenceType = "Item",
                            ReferenceId = itemId,
                            Notes = $"Vendor reassigned to '{vendorName ?? "None"}'.",
                            CreatedBy = AppSession.CurrentUserName ?? "System"
                        });
                    }
                    catch { /* best-effort: audit must not break vendor reassignment */ }
                }
                return rowsAffected > 0;
            }
        }

        /// <summary>
        /// Deletes a vendor (soft delete by setting IsActive = 0)
        /// </summary>
        /// <summary>
        /// Marks vendor as inactive (deprecated - use Archive for soft delete)
        /// </summary>
        public async Task<bool> DeleteVendorAsync(int vendorId)
        {
            const string sql = @"
                UPDATE dbo.Vendor
                SET IsActive = 0
                WHERE VendorID = @VendorId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@VendorId", vendorId);
                await con.OpenAsync();
                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                    ActivityLogger.Log(ActivityLogger.Actions.Delete, "Vendor", vendorId, $"Vendor ID {vendorId} deactivated");
                return rowsAffected > 0;
            }
        }

        /// <summary>
        /// Archives a vendor (soft delete)
        /// </summary>
        public async Task<bool> ArchiveVendorAsync(int vendorId, string archivedBy, string reason)
        {
            const string sql = @"
                INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                VALUES ('Vendor', @VendorId, 1, GETDATE(), @ArchivedBy, @Reason)";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@VendorId", vendorId);
                cmd.Parameters.AddWithValue("@ArchivedBy", archivedBy);
                cmd.Parameters.AddWithValue("@Reason", reason ?? "Archived from Vendor page");
                await con.OpenAsync();
                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                    ActivityLogger.Log(ActivityLogger.Actions.Delete, "Vendor", vendorId, $"Vendor ID {vendorId} archived");
                return rowsAffected > 0;
            }
        }

        /// <summary>
        /// Permanently deletes a vendor (hard delete)
        /// </summary>
        public async Task<(bool Success, string Message)> PermanentDeleteVendorAsync(int vendorId)
        {
            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                {
                    await con.OpenAsync();

                    // Check if vendor is used in any Set (invoices)
                    const string checkSql = "SELECT COUNT(*) FROM dbo.[Set] WHERE VendorId = @VendorId";
                    using (var cmd = new SqlCommand(checkSql, con))
                    {
                        cmd.Parameters.AddWithValue("@VendorId", vendorId);
                        int count = (int)await cmd.ExecuteScalarAsync();
                        if (count > 0)
                        {
                            return (false, $"Cannot delete vendor: {count} invoice(s)/set(s) reference this vendor. Please use Archive instead.");
                        }
                    }

                    // No dependencies, safe to delete
                    const string deleteSql = "DELETE FROM dbo.Vendor WHERE VendorID = @VendorId";
                    using (var cmd = new SqlCommand(deleteSql, con))
                    {
                        cmd.Parameters.AddWithValue("@VendorId", vendorId);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        if (rowsAffected > 0)
                        {
                            ActivityLogger.Log(ActivityLogger.Actions.Delete, "Vendor", vendorId, $"Vendor ID {vendorId} permanently deleted");
                            return (true, "Vendor deleted successfully.");
                        }
                        else
                        {
                            return (false, "Vendor not found.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error deleting vendor: {ex.Message}");
            }
        }
    }
}
