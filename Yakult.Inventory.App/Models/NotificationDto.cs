using System;

namespace Yakult.Inventory.App.Models
{
    public class NotificationDto
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string NotificationType { get; set; } = string.Empty;
        public int? ReferenceId { get; set; }
        /// <summary>UserId of whoever performed the action this notification is about; null when the
        /// event type has no actor (SecurityAlert, request/repair lifecycle rows).</summary>
        public int? ActorUserId { get; set; }
        /// <summary>Optional JSON payload for a notification that summarises several entities (e.g. a
        /// batch "added N items" row): [{"id":123,"name":"...","type":"..."}]. Null for single-entity rows.</summary>
        public string DetailsJson { get; set; }
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
        // Authorization Status — supervisor approved/rejected the employee's cartridge authorization
        public const string AuthorizationApproved     = "AUTHORIZATION_APPROVED";
        public const string AuthorizationRejected     = "AUTHORIZATION_REJECTED";

        // Request Status — IT staff fulfilled, partially fulfilled, or could not fulfill a cartridge request
        public const string RequestApproved           = "REQUEST_APPROVED";
        public const string RequestRejected           = "REQUEST_REJECTED";
        public const string RequestCompleted          = "REQUEST_COMPLETED";
        public const string RequestCancelled          = "REQUEST_CANCELLED";
        public const string RequestStatusChanged      = "REQUEST_STATUS_CHANGED";
        public const string RequestFulfilled          = "REQUEST_FULFILLED";
        public const string RequestPartiallyFulfilled = "REQUEST_PARTIALLY_FULFILLED";
        public const string RequestUnfulfilled        = "REQUEST_UNFULFILLED";

        // Repair Technician Portal — surfaced by RepairPortalNotificationPoller / the shell's
        // notification bell (Wpf\RepairPortal\Notifications\).
        public const string RepairTicketNew           = "REPAIR_TICKET_NEW";
        public const string RepairTicketCompleted     = "REPAIR_TICKET_COMPLETED";

        // "Test Notification" button (desktop + web notification settings).
        public const string Test                      = "TEST";

        // Inventory System "Activity" feed — item lifecycle events surfaced by the home
        // bell's Activity tab (Wpf\NotificationCenter\). Fanned out to every user with
        // Inventory System access via NotificationRepository.CreateForPortalAudience.
        public const string ItemAdded                 = "ITEM_ADDED";
        // A Set / Invoice was created (Build Request Set, Build / Import Invoice). ReferenceId
        // is the dbo.[Set].SetId; DetailsJson.source distinguishes "Request Set" vs "Invoice"
        // and drives which detail page the row opens.
        public const string SetCreated                = "SET_CREATED";
        // An item request was created (ReferenceId = dbo.Request.ReqId) — Activity-feed only,
        // distinct from the Requester Portal's REQUEST_* lifecycle types above.
        public const string RequestCreated            = "REQUEST_CREATED";
        // A renewal was created (ReferenceId = dbo.Renewals.RenewalId).
        public const string RenewalCreated            = "RENEWAL_CREATED";

        // dbo.[User] privilege-flags audit trigger writes these (Migration_User_CreateTrigger_PrivilegeFlagsAudit.sql).
        public const string SecurityAlert             = "SecurityAlert";

        // ── dbo.Portal.PortalKey values ─────────────────────────────────────
        public const string RequesterPortalKey = "RequesterPortal";
        public const string RepairPortalKey    = "RepairPortal";
        public const string AdminPortalKey     = "AdminPortal";
        public const string InventorySystemKey = "InventorySystem";

        // ── Per-portal ownership ─────────────────────────────────────────────
        // dbo.Notification is shared by every portal. Ownership is now the dbo.Notification.PortalId
        // FK column (see Migration_Notification_AddPortalId.sql); a bell reads its own rows with
        // WHERE PortalId = <its portal>. These arrays remain only as the WRITE-side map used by
        // PortalKeyFor() to resolve a new row's PortalId from its type — add a new type to exactly
        // one list (or pass NotificationCreateDto.PortalKey explicitly).

        /// <summary>Types owned by the Requester Portal (desktop RequesterPortalForm + the web
        /// Request Portal) — authorization + request lifecycle events, plus the test ping.</summary>
        public static readonly string[] RequestPortalTypes =
        {
            AuthorizationApproved,
            AuthorizationRejected,
            RequestApproved,
            RequestRejected,
            RequestCompleted,
            RequestCancelled,
            RequestStatusChanged,
            RequestFulfilled,
            RequestPartiallyFulfilled,
            RequestUnfulfilled,
            Test,
        };

        /// <summary>Types owned by the Repair Technician Portal bell.</summary>
        public static readonly string[] RepairPortalTypes =
        {
            RepairTicketNew,
            RepairTicketCompleted,
        };

        /// <summary>Types owned by the Inventory System home bell's Activity tab.</summary>
        public static readonly string[] InventorySystemTypes =
        {
            ItemAdded,
            SetCreated,
            RequestCreated,
            RenewalCreated,
        };

        /// <summary>
        /// Resolves the owning dbo.Portal.PortalKey for a notification type. Unknown types fall
        /// back to the Requester Portal (its bell is the widest and a mis-file there is visible,
        /// unlike a row with no portal which would be invisible everywhere).
        /// </summary>
        public static string PortalKeyFor(string notificationType)
        {
            if (Array.IndexOf(RepairPortalTypes, notificationType) >= 0)    return RepairPortalKey;
            if (Array.IndexOf(InventorySystemTypes, notificationType) >= 0) return InventorySystemKey;
            if (notificationType == SecurityAlert)                          return AdminPortalKey;
            return RequesterPortalKey;
        }
    }
}
