namespace Yakult.Inventory.App.Models
{
    public class NotificationCreateDto
    {
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string NotificationType { get; set; } = string.Empty;
        public int? ReferenceId { get; set; }

        /// <summary>UserId of whoever performed the action. Optional; leave null for event
        /// types that have no actor.</summary>
        public int? ActorUserId { get; set; }

        /// <summary>Optional JSON payload for a multi-entity summary notification (e.g. a batch
        /// "added N items" row). Leave null for single-entity notifications.</summary>
        public string DetailsJson { get; set; }

        /// <summary>
        /// dbo.Portal.PortalKey of the owning portal. Leave null to let
        /// NotificationRepository.Create resolve it from the type via
        /// NotificationType.PortalKeyFor(); set explicitly to override.
        /// </summary>
        public string PortalKey { get; set; }
    }
}
