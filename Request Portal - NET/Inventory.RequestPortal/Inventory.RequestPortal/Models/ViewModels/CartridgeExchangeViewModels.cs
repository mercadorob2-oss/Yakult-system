using Inventory.RequestPortal.Models;

namespace Inventory.RequestPortal.Models.ViewModels
{
    /// <summary>
    /// Groups pending cartridge requests (first-pass queue, not yet attempted) sharing a
    /// SubmissionSessionId (multi-model portal submission) into one card. Mirrors desktop's
    /// session-card grouping in CartridgeManagementViewModel.BuildSessionGroups. Not to be
    /// confused with CartridgeExchangeGroupViewModel in FulfillmentViewModels.cs, which groups
    /// the second-stage UnfulfilledCartridgeExchange backlog.
    /// </summary>
    public class PendingCartridgeGroupViewModel
    {
        public Guid? SubmissionSessionId { get; set; }
        public string? EmployeeName { get; set; }
        public string? CompanyName { get; set; }
        public string? BranchName { get; set; }
        public string? DepartmentName { get; set; }
        public List<CartridgeRequestDto> Rows { get; set; } = new();

        public bool IsMultiModel => Rows.Count > 1;
        public string GroupLabel => SubmissionSessionId.HasValue
            ? $"Session {Rows.FirstOrDefault()?.ReqId}"
            : $"Req #{Rows.FirstOrDefault()?.ReqId}";

        // Session submission date — mirrors desktop's SessionGroupViewModel.DateCreated (the
        // earliest row in the group), used by the date-range/quick-range filter.
        public DateTime DateCreated => Rows.Count > 0 ? Rows.Min(r => r.DateCreated) : DateTime.MinValue;
    }

    /// <summary>
    /// One editable model line on the Fulfill form — Brand New / Refilled quantity entry,
    /// capped server-side against live stock. Mirrors desktop's ExchangePanelViewModel /
    /// MultiModelRowViewModel (minus the per-serial picker, which desktop doesn't have either
    /// — issuance is FIFO-selected server-side from a quantity).
    /// </summary>
    public class FulfillCartridgeExchangeLineViewModel
    {
        public int ReqId { get; set; }
        public int? RequestModelId { get; set; }
        public int? CartridgeModelId { get; set; }
        public string DisplayModel { get; set; } = string.Empty;
        public string? ConditionType { get; set; }
        public int RequestedQty { get; set; }
        public int GoodEmptyQty { get; set; }
        public int DamagedEmptyQty { get; set; }
        public int AvailableBrandNewStock { get; set; }
        public int AvailableRefilledStock { get; set; }
        public int IssuedBrandNewQty { get; set; }
        public int IssuedRefilledQty { get; set; }

        public int MaxBrandNew => Math.Min(RequestedQty - IssuedRefilledQty, AvailableBrandNewStock);
        public int MaxRefilled => Math.Min(RequestedQty - IssuedBrandNewQty, AvailableRefilledStock);
    }

    /// <summary>
    /// Mirrors desktop's ExchangePanelViewModel summary block. IsMultiModel switches between
    /// desktop's single-model flat panel and its multi-model per-row list (ExchangePanelViewModel
    /// / MultiModelRowViewModel + CartridgeManagementView.xaml's "Step 3A"/"Step 3B" sections).
    /// </summary>
    public class FulfillCartridgeExchangeFormViewModel
    {
        public Guid? SubmissionSessionId { get; set; }
        public string? EmployeeName { get; set; }
        public string? CompanyName { get; set; }
        public string? BranchName { get; set; }
        public string? DepartmentName { get; set; }
        public string? ReceivedByName { get; set; }
        public string? AdditionalRemarks { get; set; }
        public string? DistributionMethod { get; set; }
        public List<FulfillCartridgeExchangeLineViewModel> Lines { get; set; } = new();

        public bool IsMultiModel => Lines.Count > 1;
        public string ReqIdsDisplay => string.Join(", ", Lines.Select(l => l.ReqId));
        public string ModelDisplay => IsMultiModel ? $"Multi-Model ({Lines.Count} models)" : (Lines.FirstOrDefault()?.DisplayModel ?? "");
        public int TotalQuantity => Lines.Sum(l => l.RequestedQty);
        public int TotalGoodEmptyQty => Lines.Sum(l => l.GoodEmptyQty);
        public int TotalDamagedEmptyQty => Lines.Sum(l => l.DamagedEmptyQty);

        // Mirrors ExchangePanelViewModel.LoadSingleModel's WithCartridge mapping exactly.
        public string WithCartridgeDisplay
        {
            get
            {
                if (IsMultiModel) return "Yes";
                var ct = Lines.FirstOrDefault()?.ConditionType;
                if (ct == null) return "N/A";
                if (ct.Contains("Without Cartridge")) return "No";
                if (ct.Contains("With Cartridge")) return "Yes";
                return ct;
            }
        }
    }

    /// <summary>
    /// Combines the pending-request list and the (optional) selected request's fulfillment
    /// form into one workspace page, mirroring desktop's two-pane Cartridge Exchange window —
    /// a persistent list on the left, a persistent action panel on the right that populates
    /// when a row is clicked (server round-trip via ?id=, since desktop itself does a live
    /// stock lookup on selection rather than using stale pre-loaded data).
    /// </summary>
    public class CartridgeExchangeWorkspaceViewModel
    {
        public List<PendingCartridgeGroupViewModel> Groups { get; set; } = new();
        public int? SelectedReqId { get; set; }
        public FulfillCartridgeExchangeFormViewModel? SelectedForm { get; set; }
    }

    /// <summary>
    /// Print-friendly transmittal slip data, rendered right after a successful commit.
    /// PORTED FROM: Yakult.Inventory.App/Wpf/CartridgeManagement/ViewModels/CartridgeTransmittalViewModel.cs
    /// and the physical form layout in Wpf/CartridgeManagement/Views/CartridgeTransmittalPrintView.xaml
    /// (same form used by the desktop Fulfilled Cartridges page's "Reprint" button, via
    /// TransmittalPrintService.ShowPrintDialog — confirmed both flows render this exact form).
    /// Simplified: desktop prints 3 physical copies (Gatepass/Transmittal/File) as 2 pages with a
    /// cut line via a native WPF print pipeline; the web port renders one copy (defaulting the
    /// TRANSMITTAL checkbox) and lets the browser's print dialog handle physical copy count.
    /// NotedBy/ApprovedBy/ApprovalDate/ReceivedDate/Others are always blank here — on desktop
    /// they're filled in by hand in the editable PrepareTransmittalDialog step, which this web
    /// port does not implement (out of scope for this pass); the fields still render on the slip,
    /// matching how an unedited print looks on desktop.
    /// </summary>
    public class CartridgeTransmittalViewModel
    {
        private static readonly HashSet<string> KnownNormalizedModels =
            new(StringComparer.OrdinalIgnoreCase) { "LQ2190", "LX310" };

        public string To { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string From { get; } = "INFORMATION TECHNOLOGY DEPARTMENT";
        public string RequesterName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public int Quantity { get; set; }

        public bool IsYakultPhilippines { get; set; }
        public bool IsYakultMarketing { get; set; }

        public bool IsGatepassCopy { get; set; }
        public bool IsTransmittalCopy { get; set; }
        public bool IsFileCopy { get; set; }

        public bool IsCartridgeRibbon { get; } = true;
        public bool IsLq2190 { get; set; }
        public bool IsLx310 { get; set; }
        public bool IsOtherModel { get; set; }
        public string OtherModelName { get; set; } = string.Empty;

        public string IssuedBy { get; set; } = string.Empty;
        public string ReceivedBy { get; set; } = string.Empty;
        public string ReceivedDate { get; set; } = string.Empty;
        public string NotedBy { get; set; } = string.Empty;
        public string ApprovedBy { get; set; } = string.Empty;
        public string ApprovalDate { get; set; } = string.Empty;
        public string Others { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;

        // Shallow clone for rendering the same data as 3 separate copy-type pages (Gatepass/
        // Transmittal/File) — mirrors desktop's own Clone(), used the same way by
        // TransmittalPrintService.CreateHalfView to stamp a different copy-type flag onto each
        // rendered copy without the copies interfering with each other.
        public CartridgeTransmittalViewModel Clone() => (CartridgeTransmittalViewModel)MemberwiseClone();

        private static string NormalizeModel(string? raw) =>
            System.Text.RegularExpressions.Regex.Replace(raw ?? "", @"[\s\-_.]", "").ToUpperInvariant();

        private static (bool isLq2190, bool isLx310, bool isOther, string otherName) DetectModel(string? rawModel)
        {
            string normalized = NormalizeModel(rawModel);
            bool isLq2190 = normalized.Contains("LQ2190");
            bool isLx310 = normalized.Contains("LX310");
            bool isKnown = isLq2190 || isLx310;
            bool isOther = !isKnown && !string.IsNullOrWhiteSpace(normalized);
            return (isLq2190, isLx310, isOther, isOther ? rawModel?.Trim() ?? "" : "");
        }

        private static (bool isYPI, bool isYMC) DetectCompany(string? companyName)
        {
            string n = NormalizeModel(companyName ?? "");
            bool isYPI = n.Contains("PHILIPPINES") || n == "YPI";
            bool isYMC = n.Contains("MARKETING") || n == "YMC";
            return (isYPI, isYMC);
        }

        private static string BuildToLine(string? dept, string? branch)
        {
            var parts = new[] { dept?.Trim(), branch?.Trim() }.Where(s => !string.IsNullOrWhiteSpace(s));
            return string.Join(", ", parts);
        }

        public static CartridgeTransmittalViewModel FromSession(
            IEnumerable<CartridgeRequestDto> requests, string issuedByName)
        {
            var list = requests?.ToList() ?? new List<CartridgeRequestDto>();
            var anchor = list.FirstOrDefault();
            if (anchor == null) return new CartridgeTransmittalViewModel();

            bool anyLq2190 = false, anyLx310 = false, anyOther = false;
            var otherNames = new List<string>();

            foreach (var req in list)
            {
                var (isLq2190, isLx310, isOther, otherName) = DetectModel(req.DisplayModel);
                anyLq2190 |= isLq2190;
                anyLx310 |= isLx310;
                if (isOther && !otherNames.Contains(otherName, StringComparer.OrdinalIgnoreCase))
                    otherNames.Add(otherName);
                anyOther |= isOther;
            }

            var (isYPI, isYMC) = DetectCompany(anchor.CompanyName);

            return new CartridgeTransmittalViewModel
            {
                To = BuildToLine(anchor.DepartmentName, anchor.BranchName),
                Date = DateTime.Now.ToString("MM/dd/yyyy"),
                RequesterName = anchor.EmployeeName ?? "",
                BranchName = anchor.BranchName ?? "",
                DepartmentName = anchor.DepartmentName ?? "",
                Remarks = anchor.AdditionalRemarks ?? "",
                IssuedBy = issuedByName,
                ReceivedBy = anchor.ReceivedByName ?? anchor.EmployeeName ?? "",
                IsYakultPhilippines = isYPI,
                IsYakultMarketing = isYMC,
                IsLq2190 = anyLq2190,
                IsLx310 = anyLx310,
                IsOtherModel = anyOther,
                OtherModelName = string.Join(", ", otherNames),
                Quantity = list.Sum(r => r.Quantity)
            };
        }

        // Reprint from the Fulfilled Cartridges history list — mirrors desktop's
        // CartridgeTransmittalViewModel.FromHistory(FulfilledCartridgeRowDto row) exactly,
        // including splitting row.CartridgeModel on commas (it's a STRING_AGG of every model in
        // the original request) so every model's checkbox gets checked on the reprint.
        public static CartridgeTransmittalViewModel FromHistory(FulfilledCartridgeRowDto row, string issuedByName)
        {
            var modelNames = (row.CartridgeModel ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToList();

            bool anyLq2190 = false, anyLx310 = false, anyOther = false;
            var otherNames = new List<string>();

            foreach (var modelName in modelNames)
            {
                var (isLq2190, isLx310, isOther, otherName) = DetectModel(modelName);
                anyLq2190 |= isLq2190;
                anyLx310 |= isLx310;
                if (isOther && !otherNames.Contains(otherName, StringComparer.OrdinalIgnoreCase))
                    otherNames.Add(otherName);
                anyOther |= isOther;
            }

            var (isYPI, isYMC) = DetectCompany(row.CompanyName);

            return new CartridgeTransmittalViewModel
            {
                To = BuildToLine(row.DepartmentName, row.BranchName),
                Date = row.FulfilledAtDisplay,
                RequesterName = row.RequesterName,
                BranchName = row.BranchName,
                DepartmentName = row.DepartmentName,
                IssuedBy = issuedByName,
                ReceivedBy = row.ReceivedByName,
                IsYakultPhilippines = isYPI,
                IsYakultMarketing = isYMC,
                IsLq2190 = anyLq2190,
                IsLx310 = anyLx310,
                IsOtherModel = anyOther,
                OtherModelName = string.Join(", ", otherNames),
                Quantity = row.Quantity > 0 ? row.Quantity : row.TotalIssuedQty
            };
        }
    }
}
