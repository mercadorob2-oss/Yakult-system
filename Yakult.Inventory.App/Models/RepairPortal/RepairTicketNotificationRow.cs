using System;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    /// <summary>
    /// Lightweight projection of a dbo.RepairTicket row, used only by
    /// RepairPortalNotificationPoller to decide whether a "new ticket" / "ticket repaired"
    /// notification needs to be raised. <see cref="EventAtUtc"/> is CreatedAt for the
    /// created-since scan and CompletedAt for the completed-since scan.
    /// </summary>
    public sealed class RepairTicketNotificationRow
    {
        public int RepairTicketId { get; set; }
        public string TicketCode { get; set; }
        public string ItemName { get; set; }
        public DateTime EventAtUtc { get; set; }
    }
}
