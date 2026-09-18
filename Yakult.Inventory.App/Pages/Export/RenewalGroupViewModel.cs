using System;
using System.Collections.Generic;
using System.Linq;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Pages.Export
{
    /// <summary>
    /// Grouped-renewal display DTO. Originally defined inline (as an `internal class`) inside
    /// ExportRenewalsGrouped.cs; pulled out to its own file so it stays reachable from
    /// ExportRenewalsGroupedPreviewDialog.cs, CombinedReportPreviewDialog.cs, and
    /// ReportPickerPage.cs after ExportRenewalsGrouped.cs was gutted to a thin WPF-hosting
    /// wrapper. <c>Selected</c>/<c>ChainCount</c>/<c>TotalItems</c>/<c>TotalAmount</c> are used
    /// by the WPF Export Renewals (Grouped) page for checkbox binding and computed columns.
    /// </summary>
    public class RenewalGroupViewModel
    {
        public bool Selected { get; set; }
        public int RootSetId { get; set; }
        public string RootSetCode { get; set; }
        public string CompanyName { get; set; }
        public string SetType { get; set; }
        public string OverallStatus { get; set; }
        public int? DaysUntilExpiry { get; set; }
        public DateTime? RootEndDate { get; set; }
        public bool Active { get; set; }
        public List<RenewalDto> Chain { get; set; }
        public bool IsExpanded { get; set; } = false;
        public Dictionary<int, List<string>> ItemNamesBySetId { get; set; }

        public int ChainCount => Chain?.Count ?? 0;
        public int TotalItems => Chain?.Sum(c => c.ItemCount) ?? 0;
        public decimal TotalAmount => Chain?.Sum(c => c.TotalAmountDue) ?? 0m;
    }
}
