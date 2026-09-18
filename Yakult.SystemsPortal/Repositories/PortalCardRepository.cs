using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Services;

namespace Yakult.SystemsPortal.Repositories;

public sealed class PortalCardRepository : IPortalCardRepository
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connected",
        "Maintenance",
        "NotConnected",
        "Disabled"
    };

    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly ILogger<PortalCardRepository> _logger;

    public PortalCardRepository(IConnectionStringProvider connectionStringProvider, ILogger<PortalCardRepository> logger)
    {
        _connectionStringProvider = connectionStringProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PortalSystemLink>> GetVisibleSystemCardsAsync()
    {
        var cards = new List<PortalSystemLink>();

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        var select = await BuildPortalCardSelectListAsync(con, null, includeRowVersion: false);
        var sql = $@"
SELECT {select}
FROM dbo.PortalCard
WHERE IsActive = 1 AND IsVisible = 1
ORDER BY SortOrder, PortalCardId;";
        using var cmd = new SqlCommand(sql, con);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            cards.Add(ReadSystemLink(reader));
        }

        return cards;
    }

    public async Task<IReadOnlyList<AdminPortalCardViewModel>> GetAdminCardsAsync()
    {
        var cards = new List<AdminPortalCardViewModel>();

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        var select = await BuildPortalCardSelectListAsync(con, null, includeRowVersion: true);
        var sql = $@"
SELECT {select}
FROM dbo.PortalCard
WHERE IsActive = 1
ORDER BY SortOrder, PortalCardId;";
        using var cmd = new SqlCommand(sql, con);

        using var reader = await cmd.ExecuteReaderAsync();
        var index = 0;
        while (await reader.ReadAsync())
        {
            cards.Add(ReadAdminCard(reader, index++));
        }

        return cards;
    }

    public async Task<AdminPortalCardViewModel> SaveCardAsync(
        SavePortalCardRequest request,
        int? userId,
        string? userName,
        string? ipAddress)
    {
        NormalizeAndValidate(request);

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        using var tx = (SqlTransaction)await con.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            var current = await GetCardForUpdateAsync(con, tx, request.CardKey)
                ?? null;

            if (current == null)
            {
                if (!string.IsNullOrWhiteSpace(request.RowVersion))
                    throw new InvalidOperationException("Portal card was not found.");

                await InsertCardAsync(con, tx, request, userId);
                var inserted = await GetCardForUpdateAsync(con, tx, request.CardKey)
                    ?? throw new InvalidOperationException("Portal card was created but could not be reloaded.");

                await InsertAuditAsync(con, tx, inserted.PortalCardId, userId, userName, ipAddress, null, CreateAuditSnapshot(inserted), "Create");
                var created = await GetAdminCardAsync(con, tx, request.CardKey)
                    ?? throw new InvalidOperationException("Portal card was created but could not be reloaded.");

                await tx.CommitAsync();
                return created;
            }

            var expectedRowVersion = Convert.FromBase64String(request.RowVersion);
            var nextVisible = request.IsVisible;
            var oldSnapshot = CreateAuditSnapshot(current);
            var newSnapshot = new
            {
                current.PortalCardId,
                current.CardKey,
                request.Name,
                request.Description,
                request.Url,
                request.Badge,
                request.Theme,
                request.Icon,
                request.Status,
                request.MaintenanceNote,
                request.IsRdpDownload,
                request.IsInstallerDownload,
                IsVisible = nextVisible,
                request.SortOrder,
                request.AllowedRoles,
                request.AllowedUserIds,
                request.HealthCheckUrl,
                request.MaintenanceStartUtc,
                request.MaintenanceEndUtc,
                request.DeepLink,
                request.InstallerPath,
                request.InstallerVersion,
                request.InstallerNotes
            };

            var setClauses = new List<string>
            {
                "Name = @Name",
                "Description = @Description",
                "Url = @Url",
                "Badge = @Badge",
                "Theme = @Theme",
                "Icon = @Icon",
                "Status = @Status",
                "MaintenanceNote = @MaintenanceNote",
                "IsRdpDownload = @IsRdpDownload",
                "IsInstallerDownload = @IsInstallerDownload",
                "IsVisible = @IsVisible",
                "SortOrder = @SortOrder",
                "DateModified = sysutcdatetime()",
                "ModifiedByUserId = @ModifiedByUserId"
            };
            var hasAllowedRoles = await ColumnExistsAsync(con, tx, "PortalCard", "AllowedRoles");
            var hasAllowedUserIds = await ColumnExistsAsync(con, tx, "PortalCard", "AllowedUserIds");
            var hasHealthCheckUrl = await ColumnExistsAsync(con, tx, "PortalCard", "HealthCheckUrl");
            var hasMaintenanceStart = await ColumnExistsAsync(con, tx, "PortalCard", "MaintenanceStartUtc");
            var hasMaintenanceEnd = await ColumnExistsAsync(con, tx, "PortalCard", "MaintenanceEndUtc");
            var hasDeepLink = await ColumnExistsAsync(con, tx, "PortalCard", "DeepLink");
            await EnsureInstallerColumnsAsync(con, tx);
            var hasInstallerPath = await ColumnExistsAsync(con, tx, "PortalCard", "InstallerPath");
            var hasInstallerVersion = await ColumnExistsAsync(con, tx, "PortalCard", "InstallerVersion");
            var hasInstallerNotes = await ColumnExistsAsync(con, tx, "PortalCard", "InstallerNotes");
            if (hasAllowedRoles) setClauses.Add("AllowedRoles = @AllowedRoles");
            if (hasAllowedUserIds) setClauses.Add("AllowedUserIds = @AllowedUserIds");
            if (hasHealthCheckUrl) setClauses.Add("HealthCheckUrl = @HealthCheckUrl");
            if (hasMaintenanceStart) setClauses.Add("MaintenanceStartUtc = @MaintenanceStartUtc");
            if (hasMaintenanceEnd) setClauses.Add("MaintenanceEndUtc = @MaintenanceEndUtc");
            if (hasDeepLink) setClauses.Add("DeepLink = @DeepLink");
            if (hasInstallerPath) setClauses.Add("InstallerPath = @InstallerPath");
            if (hasInstallerVersion) setClauses.Add("InstallerVersion = @InstallerVersion");
            if (hasInstallerNotes) setClauses.Add("InstallerNotes = @InstallerNotes");

            var updateSql = $@"
UPDATE dbo.PortalCard
SET {string.Join("," + Environment.NewLine + "    ", setClauses)}
WHERE CardKey = @CardKey AND RowVer = @RowVer;";

            using (var update = new SqlCommand(updateSql, con, tx))
            {
                update.Parameters.AddWithValue("@CardKey", request.CardKey);
                update.Parameters.AddWithValue("@Name", request.Name);
                update.Parameters.AddWithValue("@Description", ToDbNull(request.Description));
                update.Parameters.AddWithValue("@Url", ToDbNull(request.Url));
                update.Parameters.AddWithValue("@Badge", ToDbNull(request.Badge));
                update.Parameters.AddWithValue("@Theme", ToDbNull(request.Theme));
                update.Parameters.AddWithValue("@Icon", ToDbNull(request.Icon));
                update.Parameters.AddWithValue("@Status", request.Status);
                update.Parameters.AddWithValue("@MaintenanceNote", ToDbNull(request.MaintenanceNote));
                update.Parameters.AddWithValue("@IsRdpDownload", request.IsRdpDownload);
                update.Parameters.AddWithValue("@IsInstallerDownload", request.IsInstallerDownload);
                update.Parameters.AddWithValue("@IsVisible", nextVisible);
                update.Parameters.AddWithValue("@SortOrder", request.SortOrder);
                update.Parameters.AddWithValue("@ModifiedByUserId", ToDbNull(userId));
                if (hasAllowedRoles) update.Parameters.AddWithValue("@AllowedRoles", ToDbNull(request.AllowedRoles));
                if (hasAllowedUserIds) update.Parameters.AddWithValue("@AllowedUserIds", ToDbNull(request.AllowedUserIds));
                if (hasHealthCheckUrl) update.Parameters.AddWithValue("@HealthCheckUrl", ToDbNull(request.HealthCheckUrl));
                if (hasMaintenanceStart) update.Parameters.AddWithValue("@MaintenanceStartUtc", ToDbNull(request.MaintenanceStartUtc));
                if (hasMaintenanceEnd) update.Parameters.AddWithValue("@MaintenanceEndUtc", ToDbNull(request.MaintenanceEndUtc));
                if (hasDeepLink) update.Parameters.AddWithValue("@DeepLink", ToDbNull(request.DeepLink));
                if (hasInstallerPath) update.Parameters.AddWithValue("@InstallerPath", ToDbNull(request.InstallerPath));
                if (hasInstallerVersion) update.Parameters.AddWithValue("@InstallerVersion", ToDbNull(request.InstallerVersion));
                if (hasInstallerNotes) update.Parameters.AddWithValue("@InstallerNotes", ToDbNull(request.InstallerNotes));
                update.Parameters.Add("@RowVer", SqlDbType.Timestamp).Value = expectedRowVersion;

                var affected = await update.ExecuteNonQueryAsync();
                if (affected == 0)
                {
                    throw new DBConcurrencyException("This card was changed by another user. Refresh and try again.");
                }
            }

            await InsertAuditAsync(con, tx, current.PortalCardId, userId, userName, ipAddress, oldSnapshot, newSnapshot);
            var saved = await GetAdminCardAsync(con, tx, request.CardKey)
                ?? throw new InvalidOperationException("Portal card was saved but could not be reloaded.");

            await tx.CommitAsync();
            return saved;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task ArchiveCardAsync(
        ArchivePortalCardRequest request,
        int? userId,
        string? userName,
        string? ipAddress)
    {
        request.CardKey = request.CardKey.Trim();
        if (string.IsNullOrWhiteSpace(request.CardKey))
            throw new ArgumentException("Card key is required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion))
            throw new ArgumentException("Card version is required.");

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        using var tx = (SqlTransaction)await con.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            var current = await GetCardForUpdateAsync(con, tx, request.CardKey)
                ?? throw new InvalidOperationException("Portal card was not found.");
            var oldSnapshot = CreateAuditSnapshot(current);
            var expectedRowVersion = Convert.FromBase64String(request.RowVersion);

            const string sql = @"
UPDATE dbo.PortalCard
SET IsActive = 0,
    IsVisible = 0,
    Status = 'Disabled',
    DateModified = sysutcdatetime(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CardKey = @CardKey AND RowVer = @RowVer;";

            using (var cmd = new SqlCommand(sql, con, tx))
            {
                cmd.Parameters.AddWithValue("@CardKey", request.CardKey);
                cmd.Parameters.AddWithValue("@ModifiedByUserId", ToDbNull(userId));
                cmd.Parameters.Add("@RowVer", SqlDbType.Timestamp).Value = expectedRowVersion;

                var affected = await cmd.ExecuteNonQueryAsync();
                if (affected == 0)
                    throw new DBConcurrencyException("This card was changed by another user. Refresh and try again.");
            }

            await InsertAuditAsync(con, tx, current.PortalCardId, userId, userName, ipAddress, oldSnapshot, null, "Archive");
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateHealthStatusAsync(string cardKey, string status, DateTime checkedAtUtc)
    {
        const string sql = @"
UPDATE dbo.PortalCard
SET LastHealthStatus = @LastHealthStatus,
    LastHealthCheckedAt = @LastHealthCheckedAt
WHERE CardKey = @CardKey AND IsActive = 1;";

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@CardKey", cardKey);
        cmd.Parameters.AddWithValue("@LastHealthStatus", status);
        cmd.Parameters.AddWithValue("@LastHealthCheckedAt", checkedAtUtc);

        await con.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task LogPortalEventAsync(
        string action,
        string entityType,
        int? entityId,
        int? userId,
        string? userName,
        string? notes,
        string? ipAddress)
    {
        const string sql = @"
INSERT INTO dbo.AuditTrail
    (Action, EntityId, EntityType, UserId, UserName, Timestamp, Notes, IpAddress)
VALUES
    (@Action, @EntityId, @EntityType, @UserId, @UserName, sysutcdatetime(), @Notes, @IpAddress);";

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Action", action);
        cmd.Parameters.AddWithValue("@EntityId", ToDbNull(entityId));
        cmd.Parameters.AddWithValue("@EntityType", ToDbNull(entityType));
        cmd.Parameters.AddWithValue("@UserId", ToDbNull(userId));
        cmd.Parameters.AddWithValue("@UserName", ToDbNull(userName));
        cmd.Parameters.AddWithValue("@Notes", ToDbNull(notes));
        cmd.Parameters.AddWithValue("@IpAddress", ToDbNull(ipAddress));

        await con.OpenAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<PortalAuditLogItem>> GetRecentAuditAsync(int take = 100)
    {
        const string sql = @"
SELECT TOP (@Take) Id, Action, EntityType, EntityId, UserName, Timestamp, Notes, IpAddress
FROM dbo.AuditTrail
WHERE EntityType IN ('PortalCard', 'PortalDownload', 'Portal', 'EmployeeResource')
ORDER BY Timestamp DESC, Id DESC;";

        var items = new List<PortalAuditLogItem>();
        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Take", Math.Clamp(take, 1, 500));
        await con.OpenAsync();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new PortalAuditLogItem
            {
                Id = reader.GetInt32("Id"),
                Action = reader.GetNullableString("Action"),
                EntityType = reader.GetNullableString("EntityType"),
                EntityId = reader.IsDBNull(reader.GetOrdinal("EntityId")) ? null : reader.GetInt32("EntityId"),
                UserName = reader.GetNullableString("UserName"),
                Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp")),
                Notes = reader.GetNullableString("Notes"),
                IpAddress = reader.GetNullableString("IpAddress")
            });
        }

        return items;
    }

    public async Task<bool> IsPortalSettingsAvailableAsync()
    {
        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        return await TableExistsAsync(con, null, "PortalSetting");
    }

    public async Task<PortalNoticeSettings> GetNoticeSettingsAsync()
    {
        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        var json = await GetSettingAsync(con, null, "PortalNotice");
        if (string.IsNullOrWhiteSpace(json))
            return new PortalNoticeSettings();

        try
        {
            return JsonSerializer.Deserialize<PortalNoticeSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new PortalNoticeSettings();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Portal notice setting contains invalid JSON.");
            return new PortalNoticeSettings();
        }
    }

    public async Task SaveNoticeSettingsAsync(SavePortalNoticeRequest request, int? userId)
    {
        request.Level = NormalizeNoticeLevel(request.Level);
        request.Title = request.Title.Trim();
        request.Message = request.Message.Trim();

        if (request.StartUtc.HasValue && request.EndUtc.HasValue && request.StartUtc.Value > request.EndUtc.Value)
            throw new ArgumentException("Notice start must be before notice end.");

        var notice = new PortalNoticeSettings
        {
            Enabled = request.Enabled,
            Level = request.Level,
            Title = request.Title,
            Message = request.Message,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc
        };

        var json = JsonSerializer.Serialize(notice);
        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        if (!await TableExistsAsync(con, null, "PortalSetting"))
            throw new InvalidOperationException("dbo.PortalSetting is not available. Run Migration_SystemsPortal_FeatureCompletion.sql first.");

        await SaveSettingAsync(con, null, "PortalNotice", json, userId);
    }

    private static void NormalizeAndValidate(SavePortalCardRequest request)
    {
        request.CardKey = request.CardKey.Trim();
        request.Name = request.Name.Trim();
        request.Description = request.Description.Trim();
        request.Url = request.Url.Trim();
        request.Badge = request.Badge.Trim();
        request.Theme = request.Theme.Trim();
        request.Icon = request.Icon.Trim();
        request.Status = request.Status.Trim();
        request.MaintenanceNote = request.MaintenanceNote.Trim();
        request.AllowedRoles = request.AllowedRoles.Trim();
        request.AllowedUserIds = request.AllowedUserIds.Trim();
        request.HealthCheckUrl = request.HealthCheckUrl.Trim();
        request.DeepLink = request.DeepLink.Trim();
        request.InstallerPath = request.InstallerPath.Trim();
        request.InstallerVersion = request.InstallerVersion.Trim();
        request.InstallerNotes = request.InstallerNotes.Trim();

        if (string.IsNullOrWhiteSpace(request.CardKey))
            throw new ArgumentException("Card key is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Card name is required.");
        if (!ValidStatuses.Contains(request.Status))
            throw new ArgumentException("Invalid card status.");
        if (request.SortOrder < 0)
            throw new ArgumentException("Sort order cannot be negative.");
        if (request.MaintenanceStartUtc.HasValue && request.MaintenanceEndUtc.HasValue &&
            request.MaintenanceStartUtc.Value > request.MaintenanceEndUtc.Value)
            throw new ArgumentException("Maintenance start must be before maintenance end.");
        if (!string.IsNullOrWhiteSpace(request.InstallerPath))
            ValidateInstallerPath(request.InstallerPath);
        if (string.IsNullOrWhiteSpace(request.RowVersion) && !IsValidCardKey(request.CardKey))
            throw new ArgumentException("Card key can only contain letters, numbers, dashes, and underscores.");
        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            try
            {
                Convert.FromBase64String(request.RowVersion);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Card version is invalid.", ex);
            }
        }
    }


    private static void ValidateInstallerPath(string value)
    {
        if (Path.IsPathRooted(value) || value.Contains("..", StringComparison.Ordinal) || value.Contains('/') || value.Contains(':'))
            throw new ArgumentException("Installer path must be a safe relative server path.");

        var extension = Path.GetExtension(value);
        if (!extension.Equals(".apk", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".msi", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Installer path must point to an APK, EXE, MSI, or ZIP file.");
    }
    private static bool IsValidCardKey(string cardKey)
    {
        foreach (var c in cardKey)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                return false;
        }

        return true;
    }

    private static async Task InsertCardAsync(SqlConnection con, SqlTransaction tx, SavePortalCardRequest request, int? userId)
    {
        var nextVisible = request.IsVisible;
        var columns = new List<string>
        {
            "CardKey",
            "Name",
            "Description",
            "Url",
            "Badge",
            "Theme",
            "Icon",
            "Status",
            "MaintenanceNote",
            "IsRdpDownload",
            "IsInstallerDownload",
            "IsVisible",
            "IsActive",
            "SortOrder"
        };
        var values = new List<string>
        {
            "@CardKey",
            "@Name",
            "@Description",
            "@Url",
            "@Badge",
            "@Theme",
            "@Icon",
            "@Status",
            "@MaintenanceNote",
            "@IsRdpDownload",
            "@IsInstallerDownload",
            "@IsVisible",
            "1",
            "@SortOrder"
        };

        var hasDateCreated = await ColumnExistsAsync(con, tx, "PortalCard", "DateCreated");
        if (hasDateCreated)
        {
            columns.Add("DateCreated");
            values.Add("sysutcdatetime()");
        }

        var hasCreatedBy = await ColumnExistsAsync(con, tx, "PortalCard", "CreatedByUserId");
        if (hasCreatedBy)
        {
            columns.Add("CreatedByUserId");
            values.Add("@CreatedByUserId");
        }
        await EnsureInstallerColumnsAsync(con, tx);
        var optionalMap = new Dictionary<string, string>
        {
            ["AllowedRoles"] = "@AllowedRoles",
            ["AllowedUserIds"] = "@AllowedUserIds",
            ["HealthCheckUrl"] = "@HealthCheckUrl",
            ["MaintenanceStartUtc"] = "@MaintenanceStartUtc",
            ["MaintenanceEndUtc"] = "@MaintenanceEndUtc",
            ["DeepLink"] = "@DeepLink",
            ["InstallerPath"] = "@InstallerPath",
            ["InstallerVersion"] = "@InstallerVersion",
            ["InstallerNotes"] = "@InstallerNotes"
        };
        foreach (var item in optionalMap)
        {
            if (await ColumnExistsAsync(con, tx, "PortalCard", item.Key))
            {
                columns.Add(item.Key);
                values.Add(item.Value);
            }
        }

        var sql = $@"
INSERT INTO dbo.PortalCard
    ({string.Join(", ", columns)})
VALUES
    ({string.Join(", ", values)});";

        using var cmd = new SqlCommand(sql, con, tx);
        cmd.Parameters.AddWithValue("@CardKey", request.CardKey);
        cmd.Parameters.AddWithValue("@Name", request.Name);
        cmd.Parameters.AddWithValue("@Description", ToDbNull(request.Description));
        cmd.Parameters.AddWithValue("@Url", ToDbNull(request.Url));
        cmd.Parameters.AddWithValue("@Badge", ToDbNull(request.Badge));
        cmd.Parameters.AddWithValue("@Theme", ToDbNull(request.Theme));
        cmd.Parameters.AddWithValue("@Icon", ToDbNull(request.Icon));
        cmd.Parameters.AddWithValue("@Status", request.Status);
        cmd.Parameters.AddWithValue("@MaintenanceNote", ToDbNull(request.MaintenanceNote));
        cmd.Parameters.AddWithValue("@IsRdpDownload", request.IsRdpDownload);
        cmd.Parameters.AddWithValue("@IsInstallerDownload", request.IsInstallerDownload);
        cmd.Parameters.AddWithValue("@IsVisible", nextVisible);
        cmd.Parameters.AddWithValue("@SortOrder", request.SortOrder);
        if (hasCreatedBy)
            cmd.Parameters.AddWithValue("@CreatedByUserId", ToDbNull(userId));
        cmd.Parameters.AddWithValue("@AllowedRoles", ToDbNull(request.AllowedRoles));
        cmd.Parameters.AddWithValue("@AllowedUserIds", ToDbNull(request.AllowedUserIds));
        cmd.Parameters.AddWithValue("@HealthCheckUrl", ToDbNull(request.HealthCheckUrl));
        cmd.Parameters.AddWithValue("@MaintenanceStartUtc", ToDbNull(request.MaintenanceStartUtc));
        cmd.Parameters.AddWithValue("@MaintenanceEndUtc", ToDbNull(request.MaintenanceEndUtc));
        cmd.Parameters.AddWithValue("@DeepLink", ToDbNull(request.DeepLink));
        cmd.Parameters.AddWithValue("@InstallerPath", ToDbNull(request.InstallerPath));
        cmd.Parameters.AddWithValue("@InstallerVersion", ToDbNull(request.InstallerVersion));
        cmd.Parameters.AddWithValue("@InstallerNotes", ToDbNull(request.InstallerNotes));

        try
        {
            await cmd.ExecuteNonQueryAsync();
        }
        catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
        {
            throw new ArgumentException("A card with this key already exists.", ex);
        }
    }

    private static async Task EnsureInstallerColumnsAsync(SqlConnection con, SqlTransaction tx)
    {
        const string sql = @"
IF COL_LENGTH('dbo.PortalCard', 'InstallerPath') IS NULL
    ALTER TABLE dbo.PortalCard ADD InstallerPath nvarchar(500) NULL;
IF COL_LENGTH('dbo.PortalCard', 'InstallerVersion') IS NULL
    ALTER TABLE dbo.PortalCard ADD InstallerVersion nvarchar(100) NULL;
IF COL_LENGTH('dbo.PortalCard', 'InstallerNotes') IS NULL
    ALTER TABLE dbo.PortalCard ADD InstallerNotes nvarchar(1000) NULL;";

        using var cmd = new SqlCommand(sql, con, tx);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<bool> ColumnExistsAsync(SqlConnection con, SqlTransaction? tx, string tableName, string columnName)
    {
        const string sql = @"
SELECT 1
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = @TableName
  AND COLUMN_NAME = @ColumnName;";

        using var cmd = new SqlCommand(sql, con, tx);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        cmd.Parameters.AddWithValue("@ColumnName", columnName);
        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }

    private static async Task<bool> TableExistsAsync(SqlConnection con, SqlTransaction? tx, string tableName)
    {
        const string sql = @"
SELECT 1
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = @TableName;";

        using var cmd = new SqlCommand(sql, con, tx);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }

    private static async Task<string> GetSettingAsync(SqlConnection con, SqlTransaction? tx, string key)
    {
        if (!await TableExistsAsync(con, tx, "PortalSetting"))
            return string.Empty;

        const string sql = "SELECT SettingValue FROM dbo.PortalSetting WHERE SettingKey = @SettingKey;";
        using var cmd = new SqlCommand(sql, con, tx);
        cmd.Parameters.AddWithValue("@SettingKey", key);
        var value = await cmd.ExecuteScalarAsync();
        return value == null || value == DBNull.Value ? string.Empty : value.ToString() ?? string.Empty;
    }

    private static async Task SaveSettingAsync(SqlConnection con, SqlTransaction? tx, string key, string value, int? userId)
    {
        const string sql = @"
MERGE dbo.PortalSetting AS target
USING (SELECT @SettingKey AS SettingKey) AS source
ON target.SettingKey = source.SettingKey
WHEN MATCHED THEN
    UPDATE SET SettingValue = @SettingValue,
               DateModified = sysutcdatetime(),
               ModifiedByUserId = @ModifiedByUserId
WHEN NOT MATCHED THEN
    INSERT (SettingKey, SettingValue, DateModified, ModifiedByUserId)
    VALUES (@SettingKey, @SettingValue, sysutcdatetime(), @ModifiedByUserId);";

        using var cmd = new SqlCommand(sql, con, tx);
        cmd.Parameters.AddWithValue("@SettingKey", key);
        cmd.Parameters.AddWithValue("@SettingValue", value);
        cmd.Parameters.AddWithValue("@ModifiedByUserId", ToDbNull(userId));
        await cmd.ExecuteNonQueryAsync();
    }

    private static string NormalizeNoticeLevel(string level)
    {
        if (level.Equals("Maintenance", StringComparison.OrdinalIgnoreCase)) return "Maintenance";
        if (level.Equals("Warning", StringComparison.OrdinalIgnoreCase)) return "Warning";
        if (level.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return "Critical";
        if (level.Equals("Success", StringComparison.OrdinalIgnoreCase)) return "Success";
        return "Info";
    }

    private static async Task<string> BuildPortalCardSelectListAsync(SqlConnection con, SqlTransaction? tx, bool includeRowVersion)
    {
        var columns = new List<string>
        {
            "PortalCardId",
            "CardKey",
            "Name",
            "Description",
            "Url",
            "Badge",
            "Theme",
            "Icon",
            "Status",
            "MaintenanceNote",
            "IsRdpDownload",
            "IsInstallerDownload",
            "IsVisible",
            "SortOrder"
        };

        var optionalColumns = new[]
        {
            "AllowedRoles",
            "AllowedUserIds",
            "HealthCheckUrl",
            "LastHealthStatus",
            "LastHealthCheckedAt",
            "MaintenanceStartUtc",
            "MaintenanceEndUtc",
            "DeepLink",
            "InstallerPath",
            "InstallerVersion",
            "InstallerNotes"
        };

        foreach (var column in optionalColumns)
        {
            if (await ColumnExistsAsync(con, tx, "PortalCard", column))
                columns.Add(column);
            else if (column.EndsWith("At", StringComparison.OrdinalIgnoreCase) || column.EndsWith("Utc", StringComparison.OrdinalIgnoreCase))
                columns.Add($"CAST(NULL AS DATETIME2) AS {column}");
            else
                columns.Add($"CAST(NULL AS NVARCHAR(500)) AS {column}");
        }

        if (includeRowVersion)
            columns.Add("RowVer");

        return string.Join(", ", columns);
    }

    private static void ValidateVersionRequired(string rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
            throw new ArgumentException("Card version is required.");
    }

    private async Task<PortalCardRecord?> GetCardForUpdateAsync(SqlConnection con, SqlTransaction tx, string cardKey)
    {
        const string sql = @"
SELECT PortalCardId, CardKey, Name, Description, Url, Badge, Theme, Icon, Status,
       MaintenanceNote, IsRdpDownload, IsInstallerDownload, IsVisible, SortOrder, RowVer
FROM dbo.PortalCard WITH (UPDLOCK, ROWLOCK)
WHERE CardKey = @CardKey AND IsActive = 1;";

        using var cmd = new SqlCommand(sql, con, tx);
        cmd.Parameters.AddWithValue("@CardKey", cardKey);

        using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        return await reader.ReadAsync() ? ReadRecord(reader) : null;
    }

    private async Task<AdminPortalCardViewModel?> GetAdminCardAsync(SqlConnection con, SqlTransaction tx, string cardKey)
    {
        var select = await BuildPortalCardSelectListAsync(con, tx, includeRowVersion: true);
        var sql = $@"
SELECT {select}
FROM dbo.PortalCard
WHERE CardKey = @CardKey AND IsActive = 1;";

        using var cmd = new SqlCommand(sql, con, tx);
        cmd.Parameters.AddWithValue("@CardKey", cardKey);

        using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        return await reader.ReadAsync() ? ReadAdminCard(reader, 0) : null;
    }

    private async Task InsertAuditAsync(
        SqlConnection con,
        SqlTransaction tx,
        int portalCardId,
        int? userId,
        string? userName,
        string? ipAddress,
        object? oldSnapshot,
        object? newSnapshot,
        string action = "Update")
    {
        const string sql = @"
INSERT INTO dbo.AuditTrail
    (Action, EntityId, EntityType, UserId, UserName, Timestamp, Notes, OldValues, NewValues, IpAddress)
VALUES
    (@Action, @EntityId, @EntityType, @UserId, @UserName, sysutcdatetime(), @Notes, @OldValues, @NewValues, @IpAddress);";

        using var cmd = new SqlCommand(sql, con, tx);
        cmd.Parameters.AddWithValue("@Action", action);
        cmd.Parameters.AddWithValue("@EntityId", portalCardId);
        cmd.Parameters.AddWithValue("@EntityType", "PortalCard");
        cmd.Parameters.AddWithValue("@UserId", ToDbNull(userId));
        cmd.Parameters.AddWithValue("@UserName", ToDbNull(userName));
        cmd.Parameters.AddWithValue("@Notes", $"Portal card {action.ToLowerInvariant()} from Yakult Systems Portal admin.");
        cmd.Parameters.AddWithValue("@OldValues", oldSnapshot == null ? DBNull.Value : JsonSerializer.Serialize(oldSnapshot));
        cmd.Parameters.AddWithValue("@NewValues", newSnapshot == null ? DBNull.Value : JsonSerializer.Serialize(newSnapshot));
        cmd.Parameters.AddWithValue("@IpAddress", ToDbNull(ipAddress));

        await cmd.ExecuteNonQueryAsync();
        _logger.LogInformation("Portal card {PortalCardId} {Action} by {UserName}", portalCardId, action.ToLowerInvariant(), userName);
    }

    private static object CreateAuditSnapshot(PortalCardRecord card)
    {
        return new
        {
            card.PortalCardId,
            card.CardKey,
            card.Name,
            card.Description,
            card.Url,
            card.Badge,
            card.Theme,
            card.Icon,
            card.Status,
            card.MaintenanceNote,
            card.IsRdpDownload,
            card.IsInstallerDownload,
            card.IsVisible,
            card.SortOrder
        };
    }

    private static PortalSystemLink ReadSystemLink(SqlDataReader reader)
    {
        var status = reader.GetString("Status");
        return new PortalSystemLink
        {
            CardKey = reader.GetString("CardKey"),
            Name = reader.GetString("Name"),
            Description = reader.GetNullableString("Description"),
            Url = reader.GetNullableString("Url"),
            Badge = reader.GetNullableString("Badge"),
            Theme = reader.GetNullableString("Theme"),
            Icon = reader.GetNullableString("Icon"),
            Status = status,
            MaintenanceNote = reader.GetNullableString("MaintenanceNote"),
            IsRdpDownload = reader.GetBoolean("IsRdpDownload"),
            IsInstallerDownload = reader.GetBoolean("IsInstallerDownload"),
            IsVisible = reader.GetBoolean("IsVisible"),
            SortOrder = reader.GetInt32("SortOrder"),
            AllowedRoles = reader.GetNullableString("AllowedRoles"),
            AllowedUserIds = reader.GetNullableString("AllowedUserIds"),
            HealthCheckUrl = reader.GetNullableString("HealthCheckUrl"),
            LastHealthStatus = reader.GetNullableString("LastHealthStatus"),
            LastHealthCheckedAt = reader.GetNullableDateTime("LastHealthCheckedAt"),
            MaintenanceStartUtc = reader.GetNullableDateTime("MaintenanceStartUtc"),
            MaintenanceEndUtc = reader.GetNullableDateTime("MaintenanceEndUtc"),
            DeepLink = reader.GetNullableString("DeepLink"),
            InstallerPath = reader.GetNullableString("InstallerPath"),
            InstallerVersion = reader.GetNullableString("InstallerVersion"),
            InstallerNotes = reader.GetNullableString("InstallerNotes"),
            IsOperational = string.Equals(status, "Connected", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static AdminPortalCardViewModel ReadAdminCard(SqlDataReader reader, int index)
    {
        var status = reader.GetString("Status");
        var cardKey = reader.GetString("CardKey");
        return new AdminPortalCardViewModel
        {
            Id = cardKey,
            CardKey = cardKey,
            Index = index,
            Name = reader.GetString("Name"),
            Description = reader.GetNullableString("Description"),
            Url = reader.GetNullableString("Url"),
            Badge = reader.GetNullableString("Badge"),
            Theme = reader.GetNullableString("Theme"),
            Icon = reader.GetNullableString("Icon"),
            Status = status,
            MaintenanceNote = reader.GetNullableString("MaintenanceNote"),
            IsRdpDownload = reader.GetBoolean("IsRdpDownload"),
            IsInstallerDownload = reader.GetBoolean("IsInstallerDownload"),
            IsVisible = reader.GetBoolean("IsVisible"),
            SortOrder = reader.GetInt32("SortOrder"),
            AllowedRoles = reader.GetNullableString("AllowedRoles"),
            AllowedUserIds = reader.GetNullableString("AllowedUserIds"),
            HealthCheckUrl = reader.GetNullableString("HealthCheckUrl"),
            LastHealthStatus = reader.GetNullableString("LastHealthStatus"),
            LastHealthCheckedAt = reader.GetNullableDateTime("LastHealthCheckedAt"),
            MaintenanceStartUtc = reader.GetNullableDateTime("MaintenanceStartUtc"),
            MaintenanceEndUtc = reader.GetNullableDateTime("MaintenanceEndUtc"),
            DeepLink = reader.GetNullableString("DeepLink"),
            InstallerPath = reader.GetNullableString("InstallerPath"),
            InstallerVersion = reader.GetNullableString("InstallerVersion"),
            InstallerNotes = reader.GetNullableString("InstallerNotes"),
            IsOperational = string.Equals(status, "Connected", StringComparison.OrdinalIgnoreCase),
            RowVersion = Convert.ToBase64String((byte[])reader["RowVer"])
        };
    }

    private static PortalCardRecord ReadRecord(SqlDataReader reader)
    {
        return new PortalCardRecord(
            reader.GetInt32("PortalCardId"),
            reader.GetString("CardKey"),
            reader.GetString("Name"),
            reader.GetNullableString("Description"),
            reader.GetNullableString("Url"),
            reader.GetNullableString("Badge"),
            reader.GetNullableString("Theme"),
            reader.GetNullableString("Icon"),
            reader.GetString("Status"),
            reader.GetNullableString("MaintenanceNote"),
            reader.GetBoolean("IsRdpDownload"),
            reader.GetBoolean("IsInstallerDownload"),
            reader.GetBoolean("IsVisible"),
            reader.GetInt32("SortOrder"),
            (byte[])reader["RowVer"]);
    }

    private static object ToDbNull<T>(T? value)
    {
        if (value is null) return DBNull.Value;
        if (value is string text && string.IsNullOrWhiteSpace(text)) return DBNull.Value;
        return value;
    }

    private sealed record PortalCardRecord(
        int PortalCardId,
        string CardKey,
        string Name,
        string Description,
        string Url,
        string Badge,
        string Theme,
        string Icon,
        string Status,
        string MaintenanceNote,
        bool IsRdpDownload,
        bool IsInstallerDownload,
        bool IsVisible,
        int SortOrder,
        byte[] RowVer);
}

internal static class SqlDataReaderExtensions
{
    public static string GetString(this SqlDataReader reader, string name)
    {
        return reader.GetString(reader.GetOrdinal(name));
    }

    public static string GetNullableString(this SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    public static bool GetBoolean(this SqlDataReader reader, string name)
    {
        return reader.GetBoolean(reader.GetOrdinal(name));
    }

    public static int GetInt32(this SqlDataReader reader, string name)
    {
        return reader.GetInt32(reader.GetOrdinal(name));
    }

    public static DateTime? GetNullableDateTime(this SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }
}
