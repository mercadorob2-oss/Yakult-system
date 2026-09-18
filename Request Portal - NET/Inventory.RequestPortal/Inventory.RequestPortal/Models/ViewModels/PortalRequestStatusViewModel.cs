namespace Inventory.RequestPortal.Models.ViewModels
{
    /// <summary>
    /// ViewModel for displaying request status in portal (read-only).
    /// COPIED FROM: Yakult.Inventory.App/Services/RequesterPortalService.cs (PortalRequestStatusDto)
    /// </summary>
    public class PortalRequestStatusViewModel
    {
        public int ReqId { get; set; }
        public DateTime DateRequested { get; set; }
        public string Status { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime DateCreated { get; set; }

        // Parsed fields
        public string DistributionMethod { get; set; } = string.Empty; // PICKUP / DELIVERY
        public int GoodEmptyQty { get; set; }
        public int DamagedEmptyQty { get; set; }

        // Raw fields (for reference)
        public string? FullDescription { get; set; }
        public string? FullRemarks { get; set; }

        // Item details
        public string ItemName { get; set; } = string.Empty;
        public string? ItemModelNumber { get; set; }
        public string? CartridgeName { get; set; }

        // Destination details
        public string DestinationEmployeeName { get; set; } = string.Empty;
        public string DestinationBranch { get; set; } = string.Empty;
        public string DestinationDepartment { get; set; } = string.Empty;
        public string DestinationCompany { get; set; } = string.Empty;

        // Grouping / fulfillment
        public Guid? SubmissionSessionId { get; set; }
        public string? SetCode { get; set; }
    }

    public class RequestDetailItemViewModel
    {
        public string CartridgeModel { get; set; } = "—";
        public int Qty { get; set; }
        public int GoodQty { get; set; }
        public int DamagedQty { get; set; }
    }

    /// <summary>
    /// One row in the Request History table — groups all items from the same submission session.
    /// Mirrors RequestHistoryRowViewModel in the WinForms RequestHistoryViewModel.
    /// </summary>
    public class RequestHistoryRowViewModel
    {
        public string SetCode { get; set; } = "—";
        public DateTime? DateRequested { get; set; }
        public string CartridgeDisplay { get; set; } = string.Empty;
        public int TotalQty { get; set; }
        public string ReturnInfo { get; set; } = string.Empty;
        public string FulfillmentMethod { get; set; } = "—";
        public string DestinationBranch { get; set; } = "—";
        public string Status { get; set; } = string.Empty;

        // Detail modal fields
        public string EmployeeName { get; set; } = "—";
        public string Department { get; set; } = "—";
        public string Company { get; set; } = "—";
        public string Remarks { get; set; } = string.Empty;
        public List<int> ReqIds { get; set; } = new();
        public List<RequestDetailItemViewModel> Items { get; set; } = new();

        public string DateDisplay => DateRequested.HasValue
            ? DateRequested.Value.ToString("MM/dd/yyyy")
            : "—";

        public string StatusCssClass
        {
            get
            {
                var s = Status?.ToUpperInvariant() ?? string.Empty;
                if (s.Contains("UNFULFILLED"))
                    return "text-danger";
                if (s.Contains("PARTIALLY"))
                    return "text-warning";
                if (s.Contains("SUBMITTED") || s.Contains("UNDER REVIEW"))
                    return "text-primary";
                if (s.Contains("AWAITING AUTHORIZATION"))
                    return "status-pending";
                if (s.Contains("RECEIVED"))
                    return "text-warning";
                if (s.Contains("REPLACED") || s.Contains("COMPLETED") || s.Contains("FULFILLED"))
                    return "text-success";
                if (s.Contains("CANCELLED") || s.Contains("REJECTED"))
                    return "text-danger";
                return "text-secondary";
            }
        }
    }

    /// <summary>
    /// ViewModel for the My Requests page.
    /// </summary>
    public class MyRequestsPageViewModel
    {
        public RequestSubmissionSnapshotViewModel? SubmissionSnapshot { get; set; }
        public CartridgeAuthorizationViewModel? RecentAuthorization { get; set; }

        public List<RequestHistoryRowViewModel> History { get; set; } = new();
        public int TotalCount => History.Count;

        // [LEGACY]
        public List<PortalRequestStatusViewModel> Requests { get; set; } = new();
    }
}
