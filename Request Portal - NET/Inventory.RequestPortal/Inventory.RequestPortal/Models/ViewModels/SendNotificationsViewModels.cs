using Inventory.RequestPortal.Models;

namespace Inventory.RequestPortal.Models.ViewModels
{
    /// <summary>
    /// The Send Notifications list page (grid + filters + summary cards) and the currently
    /// selected Set's detail panel. Mirrors desktop's SendNotificationsViewModel two-pane layout
    /// (Wpf/SendNotifications/ViewModels/SendNotificationsViewModel.cs), server-rendered here
    /// with the detail panel refreshed via AJAX (same pattern as CartridgeExchangeWorkspaceViewModel).
    /// </summary>
    public class SendNotificationsWorkspaceViewModel
    {
        public List<FulfilledSetNotificationDto> Rows { get; set; } = new();

        // ── Filters (round-tripped into the view so the toolbar reflects the active query) ──
        public string? SearchText { get; set; }
        public string StatusFilter { get; set; } = "All";
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        // ── Summary cards ────────────────────────────────────────────────────────
        public int PendingCount { get; set; }
        public int UpdatedTodayCount { get; set; }
        public int FulfilledCount { get; set; }
        public int TotalCount { get; set; }

        // ── Selection ────────────────────────────────────────────────────────────
        public int? SelectedSetId { get; set; }
        public SendNotificationDetailViewModel? SelectedDetail { get; set; }

        public static readonly List<string> StatusOptions = new()
        {
            "All", "Fulfilled", "Partially Fulfilled", "Unfulfilled"
        };
    }

    /// <summary>
    /// Detail panel for one selected Set — row data plus the Received By combo (PICKUP only).
    /// Notes and send/save status are handled client-side (see Index.cshtml's snInitPanel) since
    /// they only ever apply to the panel already on screen, never survive a server round trip.
    /// Mirrors desktop's SendNotificationsViewModel detail-card + receiver-combo block.
    /// </summary>
    public class SendNotificationDetailViewModel
    {
        public FulfilledSetNotificationDto Row { get; set; } = null!;
        public List<EmployeeOptionDto> ReceiverOptions { get; set; } = new();
    }
}
