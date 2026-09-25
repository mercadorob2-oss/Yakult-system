namespace Inventory.RequestPortal.Models.ViewModels
{
    // ── Ink / Toner / Print Head fulfillment (dbo.Request) ─────────────────────

    /// <summary>
    /// A Set (or standalone request) grouping, mirrors desktop's RequestSessionGroupViewModel /
    /// UnfulfilledRequestsViewModel.GroupIntoSessions.
    /// </summary>
    public class RequestSessionGroupViewModel
    {
        public int? SetId { get; set; }
        public Guid? SubmissionSessionId { get; set; }
        public string? EmployeeName { get; set; }
        public string? BranchName { get; set; }
        public string? DepartmentName { get; set; }
        public List<RequestDto> Rows { get; set; } = new();

        public string GroupLabel => SetId.HasValue ? $"Set #{SetId}" : $"Req #{Rows.FirstOrDefault()?.ReqId}";
        public int TotalPending => Rows.Sum(r => Math.Max(0, r.Quantity - r.IssuedQty));
        public int TotalIssued => Rows.Sum(r => r.IssuedQty);
    }

    /// <summary>
    /// One editable line on the Fulfill form. Mirrors desktop's FulfillRequestRowStateViewModel —
    /// IssueQty is capped server-side at Min(AvailableStock, PendingQty) on POST.
    /// </summary>
    public class FulfillRequestLineViewModel
    {
        public int ReqId { get; set; }
        public int ItemId { get; set; }
        public string? ItemName { get; set; }
        public int Quantity { get; set; }
        public int IssuedQty { get; set; }
        public int AvailableStock { get; set; }
        public int PendingQty => Math.Max(0, Quantity - IssuedQty);
        public int MaxIssuable => Math.Min(AvailableStock, PendingQty);
        public int IssueQty { get; set; }
        public string? Remarks { get; set; }

        // ── Cartridge line of a mixed portal submission ──────────────────────────
        // Issued like the Cartridge Exchange: Brand New / Refilled quantities, each capped by its
        // own stock, with the requester's empties recorded as returned. Mirrors desktop's
        // FulfillRequestRowStateViewModel. The server re-reads stock and pending on POST.
        public bool IsExchangeLine { get; set; }
        public bool ModelRegistered { get; set; }
        public string? ModelNumber { get; set; }
        public int AvailBrandNew { get; set; }
        public int AvailRefilled { get; set; }
        public int BrandNewQty { get; set; }
        public int RefilledQty { get; set; }
        public int DeclaredGood { get; set; }
        public int DeclaredDamaged { get; set; }
        public int ReturnedGood { get; set; }
        public int ReturnedDamaged { get; set; }
        public int MaxBrandNew => Math.Max(0, Math.Min(AvailBrandNew, PendingQty));
        public int MaxRefilled => Math.Max(0, Math.Min(AvailRefilled, PendingQty));
    }

    public class FulfillRequestFormViewModel
    {
        public int? SetId { get; set; }
        public string? EmployeeName { get; set; }
        public string? BranchName { get; set; }
        public string? DepartmentName { get; set; }
        public List<FulfillRequestLineViewModel> Lines { get; set; } = new();
    }

    // ── Cartridge exchange fulfillment (dbo.UnfulfilledCartridgeExchange) ──────

    public class CartridgeExchangeGroupViewModel
    {
        public int? SetId { get; set; }
        public Guid? SubmissionSessionId { get; set; }
        public string? RequesterName { get; set; }
        public string? BranchName { get; set; }
        public string? DepartmentName { get; set; }
        public List<UnfulfilledCartridgeExchangeDto> Rows { get; set; } = new();

        public string GroupLabel => SetId.HasValue ? $"Set #{SetId}" : $"Req #{Rows.FirstOrDefault()?.ReqId}";
        public int TotalUnfulfilled => Rows.Sum(r => r.UnfulfilledQty);
    }

    /// <summary>
    /// One editable line on the Fulfill form. Mirrors desktop's FulfillRowStateViewModel: issuance
    /// is entered as separate Brand New / Refilled quantities (each capped by its own stock pool),
    /// but FulfillExchange itself only ever receives their combined total — no DB schema change,
    /// same single write as before, just with desktop's stock-source picker restored in the UI.
    /// </summary>
    public class FulfillCartridgeLineViewModel
    {
        public int UnfulfilledId { get; set; }
        public string? CartridgeModel { get; set; }
        public int ReturnedEmptyQty { get; set; }
        public int IssuedFullQty { get; set; }
        public int UnfulfilledQty { get; set; }
        public int AvailableIssuableStock { get; set; }
        public int MaxIssuable => Math.Min(AvailableIssuableStock, UnfulfilledQty);

        public int AvailBrandNew { get; set; }
        public int AvailRefilled { get; set; }
        public int MaxBrandNew => Math.Min(AvailBrandNew, UnfulfilledQty);
        public int MaxRefilled => Math.Min(AvailRefilled, UnfulfilledQty);
        public int BrandNewQty { get; set; }
        public int RefilledQty { get; set; }
        public int TotalToIssue => BrandNewQty + RefilledQty;

        public string? Remarks { get; set; }
    }

    public class FulfillCartridgeFormViewModel
    {
        public int? SetId { get; set; }
        public string? RequesterName { get; set; }
        public string? BranchName { get; set; }
        public string? DepartmentName { get; set; }
        public List<FulfillCartridgeLineViewModel> Lines { get; set; } = new();
    }
}
