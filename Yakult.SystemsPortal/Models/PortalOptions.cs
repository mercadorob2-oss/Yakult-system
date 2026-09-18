namespace Yakult.SystemsPortal.Models;

public sealed class PortalOptions
{
    public const string SectionName = "Portal";

    public string Title { get; set; } = "Yakult Systems Portal";
    public string Subtitle { get; set; } = string.Empty;
    public List<PortalSystemLink> Systems { get; set; } = new();
    public List<PortalMiniCard> MiniCards { get; set; } = new();
    public RdpOptions? Rdp { get; set; }

    public string InstallerPath { get; set; } = "wwwroot\\installers\\YakultInventory_Setup.exe";
    public string MobileInstallerPath { get; set; } = "installer\\Yakult_Mobile_Device_installer.apk";
    public bool EnableDemoMode { get; set; } = false;
    public List<string> DemoModeAllowedUsernames { get; set; } = new();
}

public sealed class PortalMiniCard
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Badge { get; set; } = string.Empty;
    public string Theme { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool IsDownload { get; set; } = false;
}

public sealed class PortalSystemLink
{
    public string CardKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Destination URL. Only consulted when both <see cref="IsRdpDownload"/>
    /// and <see cref="IsInstallerDownload"/> are <c>false</c>. If empty or
    /// <c>"#"</c>, the card renders as a disabled "[Reserved]" placeholder.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    public string Badge { get; set; } = string.Empty;
    public string Theme { get; set; } = string.Empty;
    public bool IsRdpDownload { get; set; } = false;
    public bool IsInstallerDownload { get; set; } = false;
    public string Icon { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string MaintenanceNote { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public int SortOrder { get; set; }
    public string AllowedRoles { get; set; } = string.Empty;
    public string AllowedUserIds { get; set; } = string.Empty;
    public string HealthCheckUrl { get; set; } = string.Empty;
    public string LastHealthStatus { get; set; } = string.Empty;
    public DateTime? LastHealthCheckedAt { get; set; }
    public DateTime? MaintenanceStartUtc { get; set; }
    public DateTime? MaintenanceEndUtc { get; set; }
    
    public string DeepLink { get; set; } = string.Empty;
    
    public string InstallerPath { get; set; } = string.Empty;
    public string InstallerVersion { get; set; } = string.Empty;
    public string InstallerNotes { get; set; } = string.Empty;

    /// <summary>
    /// When <c>true</c>, the card shows a green "Connected and operational" status
    /// indicator. Defaults to <c>false</c> (shows "Not yet connected" for systems
    /// still being integrated).
    /// </summary>
    public bool IsOperational { get; set; } = false;
}

public sealed class RdpOptions
{
    public string ServerAddress { get; set; } = "192.168.100.186";
    public string Username { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string LauncherPath { get; set; } = "C:\\YakultLauncher.exe";
    public int ScreenWidth { get; set; } = 1920;
    public int ScreenHeight { get; set; } = 1080;
    public int ColorDepth { get; set; } = 24;
    public bool PromptForCredentials { get; set; } = true;
    public string FileName { get; set; } = "YakultInventory.rdp";
}
