namespace Yakult.ITCM.Server.Models;

public sealed class CallEmailTemplateItem
{
    public int TemplateId { get; set; }
    public string TemplateType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
