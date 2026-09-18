namespace Yakult.ITCM.Server.Models;

public sealed class CallEmailLogItem
{
    public long EmailLogId { get; set; }
    public int? TicketId { get; set; }
    public string? Branch { get; set; }
    public string EmailType { get; set; } = string.Empty;
    public string? Recipient { get; set; }
    public string? Subject { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime DateSent { get; set; }
    public int? CreatedByUserId { get; set; }
}
