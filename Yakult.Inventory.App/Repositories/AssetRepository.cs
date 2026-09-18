using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Dapper;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Data-access layer for dbo.Asset.
    ///
    /// Safety rules:
    ///   - Never deletes assets that have Renewals rows pointing at them.
    ///   - SerialNumber uniqueness is enforced at DB level; the repository surfaces
    ///     the error as a user-friendly InvalidOperationException.
    ///   - At least one of ModelNumber / SerialNumber must be non-null
    ///     (DB CHECK constraint).  Validation is also done here for early feedback.
    /// </summary>
    public class AssetRepository
    {
        public AssetRepository() { }

        private sealed class AssetTableCapabilities
        {
            public bool HasItemId { get; set; }
            public bool HasDescription { get; set; }
            public bool HasCreatedBy { get; set; }

            public bool HasVendorTable { get; set; }
            public bool HasVendorName { get; set; }

            public bool HasUserTable { get; set; }
            public bool HasUserName { get; set; }
        }

        private readonly Dictionary<string, AssetTableCapabilities> _capabilitiesCache =
            new Dictionary<string, AssetTableCapabilities>(StringComparer.OrdinalIgnoreCase);

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        private string GetConnectionStringForYims()
        {
            var cs = GetConnectionString();
            var builder = new SqlConnectionStringBuilder(cs);
            if (string.Equals(builder.InitialCatalog, "YIMS", StringComparison.OrdinalIgnoreCase))
                return cs;
            builder.InitialCatalog = "YIMS";
            return builder.ConnectionString;
        }

        private static bool IsInvalidObjectAsset(SqlException ex)
        {
            return ex != null
                   && ex.Number == 208
                   && !string.IsNullOrWhiteSpace(ex.Message)
                   && ex.Message.IndexOf("dbo.Asset", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetCatalogKey(string connectionString)
        {
            try
            {
                var b = new SqlConnectionStringBuilder(connectionString);
                return b.InitialCatalog ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private AssetTableCapabilities GetCapabilities(SqlConnection con)
        {
            var key = GetCatalogKey(con.ConnectionString);
            if (_capabilitiesCache.TryGetValue(key, out var cached))
                return cached;

            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in con.Query<string>(@"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Asset'"))
                cols.Add(name);

            bool vendorTableExists = con.ExecuteScalar<int>(
                @"SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Vendor'") > 0;

            bool userTableExists = con.ExecuteScalar<int>(
                @"SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'User'") > 0;

            bool vendorNameExists = false;
            if (vendorTableExists)
            {
                vendorNameExists = con.ExecuteScalar<int>(
                    @"SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Vendor' AND COLUMN_NAME = 'VendorName'") > 0;
            }

            bool userNameExists = false;
            if (userTableExists)
            {
                userNameExists = con.ExecuteScalar<int>(
                    @"SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'User' AND COLUMN_NAME = 'Name'") > 0;
            }

            var caps = new AssetTableCapabilities
            {
                HasItemId = cols.Contains("ItemId"),
                HasDescription = cols.Contains("Description"),
                HasCreatedBy = cols.Contains("CreatedBy"),
                HasVendorTable = vendorTableExists,
                HasVendorName = vendorNameExists,
                HasUserTable = userTableExists,
                HasUserName = userNameExists
            };

            _capabilitiesCache[key] = caps;
            return caps;
        }

        private static string BuildAssetBaseSelectSql(AssetTableCapabilities caps)
        {
            string itemIdSelect = caps.HasItemId ? "a.ItemId," : "CAST(NULL AS INT) AS ItemId,";
            string descriptionSelect = caps.HasDescription
                ? "a.Description,"
                : "CAST(NULL AS NVARCHAR(4000)) AS Description,";

            string createdBySelect = caps.HasCreatedBy
                ? "a.CreatedBy,"
                : "CAST(NULL AS INT) AS CreatedBy,";

            string vendorNameSelect = (caps.HasVendorTable && caps.HasVendorName)
                ? "v.VendorName,"
                : "CAST(NULL AS NVARCHAR(255)) AS VendorName,";

            string createdByNameSelect = (caps.HasCreatedBy && caps.HasUserTable && caps.HasUserName)
                ? "u.Name AS CreatedByName"
                : "CAST(NULL AS NVARCHAR(255)) AS CreatedByName";

            string vendorJoin = caps.HasVendorTable
                ? "LEFT JOIN dbo.Vendor v ON a.VendorId = v.VendorID"
                : string.Empty;

            string userJoin = (caps.HasCreatedBy && caps.HasUserTable)
                ? "LEFT JOIN dbo.[User] u ON a.CreatedBy = u.UserId"
                : string.Empty;

            return $@"
                SELECT
                    a.AssetId,
                    {itemIdSelect}
                    a.ModelNumber,
                    a.SerialNumber,
                    {descriptionSelect}
                    a.VendorId,
                    {vendorNameSelect}
                    a.IsActive,
                    a.CreatedAt,
                    {createdBySelect}
                    {createdByNameSelect}
                FROM dbo.Asset a
                {vendorJoin}
                {userJoin}";
        }

        // ────────────────────────────────────────────────────────────────────
        // Queries
        // ────────────────────────────────────────────────────────────────────

        public List<AssetDto> GetAllAssets()
        {
            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                {
                    var caps = GetCapabilities(con);
                    var sql = BuildAssetBaseSelectSql(caps) + "\n                ORDER BY a.CreatedAt DESC";
                    return con.Query<AssetDto>(sql).AsList();
                }
            }
            catch (SqlException ex) when (IsInvalidObjectAsset(ex))
            {
                using (var con = new SqlConnection(GetConnectionStringForYims()))
                {
                    var caps = GetCapabilities(con);
                    var sql = BuildAssetBaseSelectSql(caps) + "\n                ORDER BY a.CreatedAt DESC";
                    return con.Query<AssetDto>(sql).AsList();
                }
            }
        }

        public AssetDto GetAssetById(int assetId)
        {
            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                {
                    var caps = GetCapabilities(con);
                    var sql = BuildAssetBaseSelectSql(caps) + "\n                WHERE a.AssetId = @AssetId";
                    return con.QuerySingleOrDefault<AssetDto>(sql, new { AssetId = assetId });
                }
            }
            catch (SqlException ex) when (IsInvalidObjectAsset(ex))
            {
                using (var con = new SqlConnection(GetConnectionStringForYims()))
                {
                    var caps = GetCapabilities(con);
                    var sql = BuildAssetBaseSelectSql(caps) + "\n                WHERE a.AssetId = @AssetId";
                    return con.QuerySingleOrDefault<AssetDto>(sql, new { AssetId = assetId });
                }
            }
        }

        /// <summary>
        /// Search assets by ModelNumber or SerialNumber fragment (for picker dropdown).
        /// </summary>
        public List<AssetDto> SearchAssets(string term)
        {
            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                {
                    var caps = GetCapabilities(con);
                    var descFilter = caps.HasDescription
                        ? " OR a.Description LIKE '%' + @Term + '%'"
                        : string.Empty;

                    var sql = $@"
                        {BuildAssetBaseSelectSql(caps)}
                        WHERE a.IsActive = 1
                          AND (  a.ModelNumber   LIKE '%' + @Term + '%'
                              OR a.SerialNumber  LIKE '%' + @Term + '%'
                              {descFilter})
                        ORDER BY a.ModelNumber, a.SerialNumber";

                    return con.Query<AssetDto>(sql, new { Term = term ?? "" }).AsList();
                }
            }
            catch (SqlException ex) when (IsInvalidObjectAsset(ex))
            {
                using (var con = new SqlConnection(GetConnectionStringForYims()))
                {
                    var caps = GetCapabilities(con);
                    var descFilter = caps.HasDescription
                        ? " OR a.Description LIKE '%' + @Term + '%'"
                        : string.Empty;

                    var sql = $@"
                        {BuildAssetBaseSelectSql(caps)}
                        WHERE a.IsActive = 1
                          AND (  a.ModelNumber   LIKE '%' + @Term + '%'
                              OR a.SerialNumber  LIKE '%' + @Term + '%'
                              {descFilter})
                        ORDER BY a.ModelNumber, a.SerialNumber";

                    return con.Query<AssetDto>(sql, new { Term = term ?? "" }).AsList();
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Mutations
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new Asset row.  Returns the new AssetId.
        /// Throws InvalidOperationException for duplicate SerialNumber or both-null guard.
        /// </summary>
        public int AddAsset(AssetDto asset, int createdByUserId)
        {
            ValidateAsset(asset);

            try
            {
                try
                {
                    using (var con = new SqlConnection(GetConnectionString()))
                    {
                        var caps = GetCapabilities(con);
                        var cols = new List<string> { "ModelNumber", "SerialNumber", "VendorId", "IsActive", "CreatedAt" };
                        var vals = new List<string> { "@ModelNumber", "@SerialNumber", "@VendorId", "1", "GETDATE()" };

                        if (caps.HasDescription)
                        {
                            cols.Add("Description");
                            vals.Add("@Description");
                        }

                        if (caps.HasCreatedBy)
                        {
                            cols.Add("CreatedBy");
                            vals.Add("@CreatedBy");
                        }

                        var sql = $@"
                            INSERT INTO dbo.Asset
                                ({string.Join(", ", cols)})
                            VALUES
                                ({string.Join(", ", vals)});
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        return con.ExecuteScalar<int>(sql, new
                        {
                            asset.ModelNumber,
                            asset.SerialNumber,
                            asset.Description,
                            asset.VendorId,
                            CreatedBy = createdByUserId
                        });
                    }
                }
                catch (SqlException ex) when (IsInvalidObjectAsset(ex))
                {
                    using (var con = new SqlConnection(GetConnectionStringForYims()))
                    {
                        var caps = GetCapabilities(con);
                        var cols = new List<string> { "ModelNumber", "SerialNumber", "VendorId", "IsActive", "CreatedAt" };
                        var vals = new List<string> { "@ModelNumber", "@SerialNumber", "@VendorId", "1", "GETDATE()" };

                        if (caps.HasDescription)
                        {
                            cols.Add("Description");
                            vals.Add("@Description");
                        }

                        if (caps.HasCreatedBy)
                        {
                            cols.Add("CreatedBy");
                            vals.Add("@CreatedBy");
                        }

                        var sql = $@"
                            INSERT INTO dbo.Asset
                                ({string.Join(", ", cols)})
                            VALUES
                                ({string.Join(", ", vals)});
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        return con.ExecuteScalar<int>(sql, new
                        {
                            asset.ModelNumber,
                            asset.SerialNumber,
                            asset.Description,
                            asset.VendorId,
                            CreatedBy = createdByUserId
                        });
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                throw new InvalidOperationException(
                    $"An asset with serial number '{asset.SerialNumber}' already exists.", ex);
            }
        }

        /// <summary>
        /// Updates ModelNumber, SerialNumber, Description, VendorId, IsActive for an existing asset.
        /// </summary>
        public void UpdateAsset(AssetDto asset)
        {
            ValidateAsset(asset);

            try
            {
                try
                {
                    using (var con = new SqlConnection(GetConnectionString()))
                    {
                        var caps = GetCapabilities(con);
                        var setParts = new List<string>
                        {
                            "ModelNumber  = @ModelNumber",
                            "SerialNumber = @SerialNumber",
                            "VendorId     = @VendorId",
                            "IsActive     = @IsActive"
                        };

                        if (caps.HasDescription)
                            setParts.Insert(2, "Description  = @Description");

                        var sql = $@"
                            UPDATE dbo.Asset
                            SET    {string.Join(",\n                                   ", setParts)}
                            WHERE  AssetId = @AssetId";

                        con.Execute(sql, new
                        {
                            asset.ModelNumber,
                            asset.SerialNumber,
                            asset.Description,
                            asset.VendorId,
                            asset.IsActive,
                            asset.AssetId
                        });
                    }
                }
                catch (SqlException ex) when (IsInvalidObjectAsset(ex))
                {
                    using (var con = new SqlConnection(GetConnectionStringForYims()))
                    {
                        var caps = GetCapabilities(con);
                        var setParts = new List<string>
                        {
                            "ModelNumber  = @ModelNumber",
                            "SerialNumber = @SerialNumber",
                            "VendorId     = @VendorId",
                            "IsActive     = @IsActive"
                        };

                        if (caps.HasDescription)
                            setParts.Insert(2, "Description  = @Description");

                        var sql = $@"
                            UPDATE dbo.Asset
                            SET    {string.Join(",\n                                   ", setParts)}
                            WHERE  AssetId = @AssetId";

                        con.Execute(sql, new
                        {
                            asset.ModelNumber,
                            asset.SerialNumber,
                            asset.Description,
                            asset.VendorId,
                            asset.IsActive,
                            asset.AssetId
                        });
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                throw new InvalidOperationException(
                    $"An asset with serial number '{asset.SerialNumber}' already exists.", ex);
            }
        }

        /// <summary>
        /// Soft-deletes an asset by setting IsActive = 0.
        /// Refuses if the asset is referenced by any Renewal row.
        /// </summary>
        public void DeactivateAsset(int assetId)
        {
            const string checkSql = @"
                SELECT COUNT(1) FROM dbo.Renewals WHERE AssetId = @AssetId";

            const string updateSql = @"
                UPDATE dbo.Asset SET IsActive = 0 WHERE AssetId = @AssetId";

            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                {
                    int usageCount = con.ExecuteScalar<int>(checkSql, new { AssetId = assetId });
                    if (usageCount > 0)
                        throw new InvalidOperationException(
                            "Cannot deactivate this asset because it is referenced by one or more renewal records.");

                    con.Execute(updateSql, new { AssetId = assetId });
                }
            }
            catch (SqlException ex) when (IsInvalidObjectAsset(ex))
            {
                using (var con = new SqlConnection(GetConnectionStringForYims()))
                {
                    int usageCount = con.ExecuteScalar<int>(checkSql, new { AssetId = assetId });
                    if (usageCount > 0)
                        throw new InvalidOperationException(
                            "Cannot deactivate this asset because it is referenced by one or more renewal records.");

                    con.Execute(updateSql, new { AssetId = assetId });
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────────────────────────────

        private static void ValidateAsset(AssetDto asset)
        {
            bool hasModel  = !string.IsNullOrWhiteSpace(asset.ModelNumber);
            bool hasSerial = !string.IsNullOrWhiteSpace(asset.SerialNumber);

            if (!hasModel && !hasSerial)
                throw new ArgumentException(
                    "An asset requires at least a Part Number (ModelNumber) or a Serial Number.");
        }
    }
}
