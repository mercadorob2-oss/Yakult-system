namespace Yakult.ITCM.Server.Models;

public sealed class CallSmtpProfileItem
{
    public int ProfileId { get; set; }
    public string ProfileName { get; set; } = string.Empty;
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public bool UseSsl { get; set; }
    public string? SmtpUsername { get; set; }
    public byte[]? SmtpPasswordEnc { get; set; }
    public string? FromName { get; set; }
    public string? FromEmail { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
