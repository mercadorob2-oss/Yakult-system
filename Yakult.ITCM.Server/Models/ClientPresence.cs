namespace Yakult.ITCM.Server.Models;

public sealed class ClientPresenceItem
{
    public string MachineName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime LastSeenUtc { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public string? ClientVersion { get; set; }
    public string? Module { get; set; }
    public bool Online { get; set; }
}

public sealed class ClientPresenceReport
{
    public List<ClientPresenceItem> Clients { get; set; } = new();
    public int OnlineCount { get; set; }
    public int OnlineMinutesThreshold { get; set; }
}
