namespace Inventory.RequestPortal.Models
{
    public class NotificationDto
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string NotificationType { get; set; } = string.Empty;
        public int? ReferenceId { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedDate { get; set; }

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.UtcNow - CreatedDate;
                if (diff.TotalMinutes < 1)  return "just now";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24)   return $"{(int)diff.TotalHours}h ago";
                if (diff.TotalDays < 7)     return $"{(int)diff.TotalDays}d ago";
                return CreatedDate.ToLocalTime().ToString("MMM d");
            }
        }
    }

    public static class NotificationType
    {
        // Supervisor signed/rejected a CartridgeAuthorization (sent by desktop app and web portal)
        public const string AuthorizationApproved      = "AUTHORIZATION_APPROVED";
        public const string AuthorizationRejected      = "AUTHORIZATION_REJECTED";

        // Request-level events (sent by ApprovalController and desktop app)
        public const string RequestApproved           = "REQUEST_APPROVED";
        public const string RequestRejected           = "REQUEST_REJECTED";
        public const string RequestCompleted          = "REQUEST_COMPLETED";
        public const string RequestCancelled          = "REQUEST_CANCELLED";
        public const string RequestStatusChanged      = "REQUEST_STATUS_CHANGED";
        public const string RequestFulfilled          = "REQUEST_FULFILLED";
        public const string RequestPartiallyFulfilled = "REQUEST_PARTIALLY_FULFILLED";
        public const string RequestUnfulfilled        = "REQUEST_UNFULFILLED";

        // "Test Notification" button on the Notification Settings page.
        public const string Test                      = "TEST";

        // Ownership of a dbo.Notification row is the PortalId FK, not this type string
        // (see NotificationRepository.RequestPortalOwnershipFilter + the PortalId migrations).
        // The consts above are still used for icon/routing decisions in the bell + Navigate.
    }
}
