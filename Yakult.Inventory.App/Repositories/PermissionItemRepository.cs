using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Repository for the generic dbo.PermissionItem / dbo.UserPermissionItem tables that back
    /// per-user Page / ItemCategory / Action restrictions (see Security.PermissionResolver).
    /// Default-allow model: a row only exists when a user's effective access differs from the
    /// default (allowed).
    /// </summary>
    public class PermissionItemRepository
    {
        private readonly string _connectionString;

        public PermissionItemRepository()
        {
            _connectionString = DatabaseConfig.ConnectionString;
        }

        /// <summary>Returns the pre-seeded PermissionItem rows for a given type (e.g. "Page", "Action").</summary>
        public async Task<List<(int PermissionItemId, string ItemKey, string DisplayName)>> GetPermissionItemsAsync(string permissionType)
        {
            var result = new List<(int, string, string)>();

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(
                    @"SELECT PermissionItemId, ItemKey, DisplayName
                      FROM dbo.PermissionItem
                      WHERE PermissionType = @Type AND IsActive = 1
                      ORDER BY DisplayName", con))
                {
                    cmd.Parameters.AddWithValue("@Type", permissionType);
                    using (var r = await cmd.ExecuteReaderAsync())
                        while (await r.ReadAsync())
                            result.Add((r.GetInt32(0), r.GetString(1), r.GetString(2)));
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the Portal -&gt; Menu -&gt; Page navigation catalog from dbo.PageNavEntry, joined to
        /// its PermissionItem (ItemKey). A single ItemKey can appear more than once (same page
        /// reachable from more than one portal/menu) — each occurrence is its own row here.
        /// </summary>
        public async Task<List<(string ItemKey, string DisplayName, string PortalKey, string MenuGroup)>> GetPageNavEntriesAsync()
        {
            var result = new List<(string, string, string, string)>();

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(
                    @"SELECT pi.ItemKey, ne.DisplayName, ne.PortalKey, ne.MenuGroup
                      FROM dbo.PageNavEntry ne
                      INNER JOIN dbo.PermissionItem pi ON pi.PermissionItemId = ne.PermissionItemId
                      WHERE ne.IsActive = 1 AND pi.IsActive = 1
                      ORDER BY ne.PortalKey, ne.MenuGroup, ne.SortOrder", con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        result.Add((r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3)));
            }

            return result;
        }

        /// <summary>Returns all active item category names, used as the "ItemCategory" column set.</summary>
        public async Task<List<string>> GetItemCategoryNamesAsync()
        {
            var result = new List<string>();

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(
                    "SELECT Name FROM dbo.ItemCategory WHERE Active = 1 ORDER BY Name", con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        result.Add(r.GetString(0));
            }

            return result;
        }

        /// <summary>Returns per-user overrides for a given permission type: (UserId, ItemKey) -> IsGranted.</summary>
        public async Task<Dictionary<(int UserId, string ItemKey), bool>> GetUserOverridesAsync(string permissionType)
        {
            var result = new Dictionary<(int, string), bool>();

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(
                    @"SELECT upi.UserId, pi.ItemKey, upi.IsGranted
                      FROM dbo.UserPermissionItem upi
                      INNER JOIN dbo.PermissionItem pi ON pi.PermissionItemId = upi.PermissionItemId
                      WHERE pi.PermissionType = @Type", con))
                {
                    cmd.Parameters.AddWithValue("@Type", permissionType);
                    using (var r = await cmd.ExecuteReaderAsync())
                        while (await r.ReadAsync())
                            result[(r.GetInt32(0), r.GetString(1))] = r.GetBoolean(2);
                }
            }

            return result;
        }

        /// <summary>
        /// Saves per-user overrides for a permission type. A null value means "no override"
        /// (deletes any existing row, falling back to the default). Upserts the rest.
        /// Creates PermissionItem rows on demand (used by the ItemCategory type, whose keys
        /// come from dbo.ItemCategory rather than being pre-seeded).
        /// </summary>
        public async Task SaveUserOverridesAsync(
            string permissionType,
            IEnumerable<(int UserId, string ItemKey, string DisplayName, bool? Value)> overrides)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var tx = con.BeginTransaction())
                {
                    foreach (var o in overrides)
                    {
                        int permissionItemId = await GetOrCreatePermissionItemIdAsync(con, tx, permissionType, o.ItemKey, o.DisplayName);

                        if (o.Value == null)
                        {
                            using (var del = new SqlCommand(
                                "DELETE FROM dbo.UserPermissionItem WHERE UserId = @UserId AND PermissionItemId = @PermissionItemId", con, tx))
                            {
                                del.Parameters.AddWithValue("@UserId", o.UserId);
                                del.Parameters.AddWithValue("@PermissionItemId", permissionItemId);
                                await del.ExecuteNonQueryAsync();
                            }
                        }
                        else
                        {
                            using (var upsert = new SqlCommand(
                                @"MERGE dbo.UserPermissionItem AS target
                                  USING (SELECT @UserId AS UserId, @PermissionItemId AS PermissionItemId) AS src
                                  ON target.UserId = src.UserId AND target.PermissionItemId = src.PermissionItemId
                                  WHEN MATCHED THEN
                                      UPDATE SET IsGranted = @IsGranted, DateModified = GETDATE(), ModifiedByUserId = @ModifiedByUserId
                                  WHEN NOT MATCHED THEN
                                      INSERT (UserId, PermissionItemId, IsGranted, ModifiedByUserId)
                                      VALUES (@UserId, @PermissionItemId, @IsGranted, @ModifiedByUserId);", con, tx))
                            {
                                upsert.Parameters.AddWithValue("@UserId", o.UserId);
                                upsert.Parameters.AddWithValue("@PermissionItemId", permissionItemId);
                                upsert.Parameters.AddWithValue("@IsGranted", o.Value.Value);
                                upsert.Parameters.AddWithValue("@ModifiedByUserId", AppSession.CurrentUserId);
                                await upsert.ExecuteNonQueryAsync();
                            }
                        }
                    }

                    tx.Commit();
                }
            }
        }

        /// <summary>Returns all active roles, used as the subject list for role-level overrides.</summary>
        public async Task<List<(int RoleId, string RoleName)>> GetRolesAsync()
        {
            var result = new List<(int, string)>();

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(
                    "SELECT RoleId, RoleName FROM dbo.Role WHERE IsActive = 1 ORDER BY RoleName", con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        result.Add((r.GetInt32(0), r.GetString(1)));
            }

            return result;
        }

        /// <summary>Returns per-role overrides for a given permission type: (RoleId, ItemKey) -> IsGranted.</summary>
        public async Task<Dictionary<(int RoleId, string ItemKey), bool>> GetRoleOverridesAsync(string permissionType)
        {
            var result = new Dictionary<(int, string), bool>();

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(
                    @"SELECT rpi.RoleId, pi.ItemKey, rpi.IsGranted
                      FROM dbo.RolePermissionItem rpi
                      INNER JOIN dbo.PermissionItem pi ON pi.PermissionItemId = rpi.PermissionItemId
                      WHERE pi.PermissionType = @Type", con))
                {
                    cmd.Parameters.AddWithValue("@Type", permissionType);
                    using (var r = await cmd.ExecuteReaderAsync())
                        while (await r.ReadAsync())
                            result[(r.GetInt32(0), r.GetString(1))] = r.GetBoolean(2);
                }
            }

            return result;
        }

        /// <summary>
        /// Deletes every override row for a role + permission type (e.g. all "Page" restrictions
        /// for a given role), restoring default-allow across the board for that role.
        /// </summary>
        public async Task ClearRoleOverridesAsync(string permissionType, int roleId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(
                    @"DELETE rpi
                      FROM dbo.RolePermissionItem rpi
                      INNER JOIN dbo.PermissionItem pi ON pi.PermissionItemId = rpi.PermissionItemId
                      WHERE rpi.RoleId = @RoleId AND pi.PermissionType = @Type", con))
                {
                    cmd.Parameters.AddWithValue("@RoleId", roleId);
                    cmd.Parameters.AddWithValue("@Type", permissionType);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>Same semantics as SaveUserOverridesAsync, but writes dbo.RolePermissionItem instead.</summary>
        public async Task SaveRoleOverridesAsync(
            string permissionType,
            IEnumerable<(int RoleId, string ItemKey, string DisplayName, bool? Value)> overrides)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var tx = con.BeginTransaction())
                {
                    foreach (var o in overrides)
                    {
                        int permissionItemId = await GetOrCreatePermissionItemIdAsync(con, tx, permissionType, o.ItemKey, o.DisplayName);

                        if (o.Value == null)
                        {
                            using (var del = new SqlCommand(
                                "DELETE FROM dbo.RolePermissionItem WHERE RoleId = @RoleId AND PermissionItemId = @PermissionItemId", con, tx))
                            {
                                del.Parameters.AddWithValue("@RoleId", o.RoleId);
                                del.Parameters.AddWithValue("@PermissionItemId", permissionItemId);
                                await del.ExecuteNonQueryAsync();
                            }
                        }
                        else
                        {
                            using (var upsert = new SqlCommand(
                                @"MERGE dbo.RolePermissionItem AS target
                                  USING (SELECT @RoleId AS RoleId, @PermissionItemId AS PermissionItemId) AS src
                                  ON target.RoleId = src.RoleId AND target.PermissionItemId = src.PermissionItemId
                                  WHEN MATCHED THEN
                                      UPDATE SET IsGranted = @IsGranted, DateModified = GETDATE(), ModifiedByUserId = @ModifiedByUserId
                                  WHEN NOT MATCHED THEN
                                      INSERT (RoleId, PermissionItemId, IsGranted, ModifiedByUserId)
                                      VALUES (@RoleId, @PermissionItemId, @IsGranted, @ModifiedByUserId);", con, tx))
                            {
                                upsert.Parameters.AddWithValue("@RoleId", o.RoleId);
                                upsert.Parameters.AddWithValue("@PermissionItemId", permissionItemId);
                                upsert.Parameters.AddWithValue("@IsGranted", o.Value.Value);
                                upsert.Parameters.AddWithValue("@ModifiedByUserId", AppSession.CurrentUserId);
                                await upsert.ExecuteNonQueryAsync();
                            }
                        }
                    }

                    tx.Commit();
                }
            }
        }

        private static async Task<int> GetOrCreatePermissionItemIdAsync(SqlConnection con, SqlTransaction tx, string permissionType, string itemKey, string displayName)
        {
            using (var cmd = new SqlCommand(
                "SELECT PermissionItemId FROM dbo.PermissionItem WHERE PermissionType = @Type AND ItemKey = @Key", con, tx))
            {
                cmd.Parameters.AddWithValue("@Type", permissionType);
                cmd.Parameters.AddWithValue("@Key", itemKey);
                var existing = await cmd.ExecuteScalarAsync();
                if (existing != null) return (int)existing;
            }

            using (var insert = new SqlCommand(
                @"INSERT INTO dbo.PermissionItem (PermissionType, ItemKey, DisplayName)
                  OUTPUT INSERTED.PermissionItemId
                  VALUES (@Type, @Key, @DisplayName)", con, tx))
            {
                insert.Parameters.AddWithValue("@Type", permissionType);
                insert.Parameters.AddWithValue("@Key", itemKey);
                insert.Parameters.AddWithValue("@DisplayName", (object)displayName ?? itemKey);
                return (int)await insert.ExecuteScalarAsync();
            }
        }
    }
}
