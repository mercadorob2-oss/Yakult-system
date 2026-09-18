namespace Inventory.RequestPortal.Models
{
    public class NotificationCreateDto
    {
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string NotificationType { get; set; } = string.Empty;
        public int? ReferenceId { get; set; }

        /// <summary>
        /// dbo.Portal.PortalKey of the portal that owns this notification. Every write
        /// path in this project targets the requester's bell, so it defaults to
        /// "RequesterPortal"; resolved to dbo.Notification.PortalId at INSERT time.
        /// </summary>
        public string PortalKey { get; set; } = "RequesterPortal";
    }
}
