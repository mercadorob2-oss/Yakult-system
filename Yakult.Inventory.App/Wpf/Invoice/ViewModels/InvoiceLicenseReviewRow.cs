using System;
using System.Collections.Generic;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Invoice.ViewModels
{
    /// <summary>
    /// Presentation-layer row wrapper around InvoiceLicenseReviewRepository.ReviewItemDto for the
    /// Invoice License Review grid. Carries the in-memory (not-yet-saved) classification decision
    /// so the grid and the two drop panels can reflect pending changes before Save Changes commits
    /// them to dbo.Item.
    /// </summary>
    public sealed class InvoiceLicenseReviewRow : ViewModelBase
    {
        public int ItemId { get; set; }
        public int SetId { get; set; }
        public int? ReqId { get; set; }
        public string InvoiceNumber { get; set; }
        public string PONumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string Status { get; set; }
        public string Site { get; set; }
        public string CompanyName { get; set; }

        /// <summary>The whole invoice's total (dbo.[Set].TotalAmountDue), not this line's own
        /// amount — the same value repeats across every pending row on the same invoice.</summary>
        public decimal InvoiceTotalAmount { get; set; }

        public string Department { get; set; }
        public string Employee { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public string ItemDescription { get; set; }
        public string ItemType { get; set; }
        public string ModelNumber { get; set; }
        public string SerialNumber { get; set; }
        public string CategoryName { get; set; }
        public string LegacyCategoryText { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public string VendorName { get; set; }
        public string ConditionName { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Every distinct dbo.Item.ItemType found anywhere on this row's invoice (Set) —
        /// not just this row's own Hardware item. E.g. an invoice with both a Database Server
        /// (Hardware) and a matching license (Software/License) shows both here, so the reviewer
        /// can see the full context of what's on the invoice, not just the one pending line.
        /// Populated separately from dbo.vw_InvoiceNonLicensedItems via SetRepository.GetItemTypesBySetIds
        /// since that view deliberately excludes non-Hardware rows.</summary>
        public IReadOnlyList<string> ItemTypesOnInvoice { get; set; } = Array.Empty<string>();

        public string ItemTypesOnInvoiceDisplay =>
            ItemTypesOnInvoice != null && ItemTypesOnInvoice.Count > 0
                ? string.Join(", ", ItemTypesOnInvoice)
                : ItemType;

        /// <summary>Every distinct category found anywhere on this row's invoice (Set) — not just
        /// this row's own item's category. Populated separately from dbo.vw_InvoiceNonLicensedItems
        /// via SetRepository.GetCategoriesBySetIds, same reasoning as ItemTypesOnInvoice.</summary>
        public IReadOnlyList<string> CategoriesOnInvoice { get; set; } = Array.Empty<string>();

        /// <summary>One category per line (not comma-joined) so the grid column reads as a list
        /// rather than a single wrapped run — WPF's TextBlock.Text renders embedded "\n" as a
        /// line break natively, no special markup needed.</summary>
        public string CategoriesOnInvoiceDisplay =>
            CategoriesOnInvoice != null && CategoriesOnInvoice.Count > 0
                ? string.Join("\n", CategoriesOnInvoice)
                : DisplayCategory;

        /// <summary>Effective category shown in the grid — prefers the ItemCategory master name,
        /// falls back to the legacy free-text Item.Category column.</summary>
        public string DisplayCategory =>
            !string.IsNullOrWhiteSpace(CategoryName) ? CategoryName : LegacyCategoryText;

        /// <summary>Null = not yet decided. "Licensed" / "NonLicensed" = queued for Save Changes,
        /// not yet written to the database.</summary>
        private string _pendingDecision;
        public string PendingDecision
        {
            get => _pendingDecision;
            set
            {
                if (SetField(ref _pendingDecision, value))
                {
                    OnPropertyChanged(nameof(HasPendingDecision));
                    OnPropertyChanged(nameof(PendingDecisionDisplay));
                }
            }
        }

        public bool HasPendingDecision => !string.IsNullOrEmpty(PendingDecision);

        public string PendingDecisionDisplay
        {
            get
            {
                switch (PendingDecision)
                {
                    case "Licensed": return "Pending: Licensed";
                    case "NonLicensed": return "Pending: Non-Licensed";
                    default: return "Unreviewed";
                }
            }
        }
    }
}
