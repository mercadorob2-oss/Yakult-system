namespace Yakult.SystemsPortal.Models;

public sealed class AdminCardsViewModel
{
    public IReadOnlyList<AdminPortalCardViewModel> Cards { get; init; } = Array.Empty<AdminPortalCardViewModel>();
}

public sealed class AdminDashboardViewModel
{
    public IReadOnlyList<AdminPortalCardViewModel> Cards { get; init; } = Array.Empty<AdminPortalCardViewModel>();
    public IReadOnlyList<PortalAuditLogItem> RecentEvents { get; init; } = Array.Empty<PortalAuditLogItem>();
    public PortalNoticeSettings Notice { get; init; } = new();
}

public sealed class AdminAuditViewModel
{
    public IReadOnlyList<PortalAuditLogItem> Items { get; init; } = Array.Empty<PortalAuditLogItem>();
}

public sealed class PortalAuditLogItem
{
    public int Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public int? EntityId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string Notes { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
}

public sealed class PortalSettingsViewModel
{
    public PortalNoticeSettings Notice { get; init; } = new();
    public string CardsJson { get; init; } = "[]";
    public bool DatabaseSettingsAvailable { get; init; }
    public ConnectionPropertiesViewModel Connection { get; init; } = new();
}

public sealed class ConnectionPropertiesViewModel
{
    public string Server { get; init; } = string.Empty;
    public int? Port { get; init; }
    public string Database { get; init; } = string.Empty;
    public string Authentication { get; init; } = "sql";
    public string Username { get; init; } = string.Empty;
    public bool HasStoredPassword { get; init; }
    public bool Encrypt { get; init; } = true;
    public bool TrustServerCertificate { get; init; }
    public bool Pooling { get; init; } = true;
    public int ConnectionTimeout { get; init; } = 30;
    public bool IsConfigured { get; init; }
}

public sealed class SaveConnectionPropertiesRequest
{
    public string Server { get; set; } = string.Empty;
    public int? Port { get; set; }
    public string Database { get; set; } = string.Empty;
    public string Authentication { get; set; } = "sql";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; }
    public bool Pooling { get; set; } = true;
    public int ConnectionTimeout { get; set; } = 30;
}

public sealed class PortalNoticeSettings
{
    public bool Enabled { get; set; }
    public string Level { get; set; } = "Info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }

    public bool IsMaintenanceMode =>
        string.Equals(Level, "Maintenance", StringComparison.OrdinalIgnoreCase);

    public bool IsActive(DateTime utcNow)
    {
        if (!Enabled) return false;
        if (StartUtc.HasValue && utcNow < DateTime.SpecifyKind(StartUtc.Value, DateTimeKind.Utc)) return false;
        if (EndUtc.HasValue && utcNow > DateTime.SpecifyKind(EndUtc.Value, DateTimeKind.Utc)) return false;
        return !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Message);
    }
}

public sealed class SavePortalNoticeRequest
{
    public bool Enabled { get; set; }
    public string Level { get; set; } = "Info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
}

public sealed class AdminPortalCardViewModel
{
    public string Id { get; init; } = string.Empty;
    public string CardKey { get; init; } = string.Empty;
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Badge { get; init; } = string.Empty;
    public string Theme { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public bool IsRdpDownload { get; init; }
    public bool IsInstallerDownload { get; init; }
    public bool IsOperational { get; init; }
    public string Status { get; init; } = "NotConnected";
    public string MaintenanceNote { get; init; } = string.Empty;
    public bool IsVisible { get; init; } = true;
    public int SortOrder { get; init; }
    public string AllowedRoles { get; init; } = string.Empty;
    public string AllowedUserIds { get; init; } = string.Empty;
    public string HealthCheckUrl { get; init; } = string.Empty;
    public string LastHealthStatus { get; init; } = string.Empty;
    public DateTime? LastHealthCheckedAt { get; init; }
    public DateTime? MaintenanceStartUtc { get; init; }
    public DateTime? MaintenanceEndUtc { get; init; }
    
    public string DeepLink { get; init; } = string.Empty;
    
    public string InstallerPath { get; init; } = string.Empty;
    public string InstallerVersion { get; init; } = string.Empty;
    public string InstallerNotes { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class SavePortalCardRequest
{
    public string CardKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Badge { get; set; } = string.Empty;
    public string Theme { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool IsRdpDownload { get; set; }
    public bool IsInstallerDownload { get; set; }
    public bool IsVisible { get; set; } = true;
    public int SortOrder { get; set; }
    public string AllowedRoles { get; set; } = string.Empty;
    public string AllowedUserIds { get; set; } = string.Empty;
    public string HealthCheckUrl { get; set; } = string.Empty;
    public DateTime? MaintenanceStartUtc { get; set; }
    public DateTime? MaintenanceEndUtc { get; set; }
    
    public string DeepLink { get; set; } = string.Empty;
    
    public string InstallerPath { get; set; } = string.Empty;
    public string InstallerVersion { get; set; } = string.Empty;
    public string InstallerNotes { get; set; } = string.Empty;
    public string Status { get; set; } = "NotConnected";
    public string MaintenanceNote { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ArchivePortalCardRequest
{
    public string CardKey { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}
