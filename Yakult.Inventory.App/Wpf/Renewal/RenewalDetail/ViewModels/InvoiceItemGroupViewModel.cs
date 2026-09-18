using System.Collections.ObjectModel;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalDetail.ViewModels
{
    /// <summary>
    /// One Sub-Type Group's worth of invoice items, for the Renewal Detail page's Grouped View.
    /// Keyed on SubType + ReferenceCode — the canonical Sub-Type Group model that
    /// dbo.SetItemSubTypeGroup and the printed reports use.
    /// </summary>
    public class InvoiceItemGroupViewModel : ViewModelBase
    {
        /// <summary>Banner text, e.g. "CONTRACT  |  100316593", or "Ungrouped Items".</summary>
        public string GroupHeader { get; set; }

        /// <summary>"SubType|ReferenceCode", or null for the ungrouped bucket.</summary>
        public string GroupKey { get; set; }

        /// <summary>Flat item list — kept for callers that don't care about the nested Parent
        /// Tag breakdown (e.g. counts). The Grouped View itself renders ParentTagGroups.</summary>
        public ObservableCollection<InvoiceItemRowViewModel> Items { get; }
            = new ObservableCollection<InvoiceItemRowViewModel>();

        /// <summary>This Sub-Type bucket's items, further bucketed by Parent Tag — an
        /// independent, orthogonal grouping nested one level inside Sub-Type, matching
        /// ViewInvoiceDetailPage's hierarchy. Items with no Parent Tag land in a section with
        /// a null Label, which the view collapses (no banner) rather than showing "Ungrouped"
        /// twice.</summary>
        public ObservableCollection<ParentTagSubGroupViewModel> ParentTagGroups { get; }
            = new ObservableCollection<ParentTagSubGroupViewModel>();

        /// <summary>True for the catch-all section holding items with no Sub-Type Group.</summary>
        public bool IsUngrouped => string.IsNullOrEmpty(GroupKey);

        /// <summary>Item count shown beside the banner.</summary>
        public string CountDisplay => Items.Count == 1 ? "1 item" : Items.Count + " items";
    }

    /// <summary>One Parent Tag's worth of items within a single Sub-Type bucket (or within the
    /// Ungrouped bucket) — free-text label, no financial semantics, no reference code/dates.</summary>
    public class ParentTagSubGroupViewModel : ViewModelBase
    {
        /// <summary>Null for the untagged section — the view binds this to a Visibility
        /// converter that collapses the banner entirely rather than showing "Ungrouped".</summary>
        public string Label { get; set; }

        public bool HasLabel => !string.IsNullOrWhiteSpace(Label);

        public ObservableCollection<InvoiceItemRowViewModel> Items { get; }
            = new ObservableCollection<InvoiceItemRowViewModel>();

        public string CountDisplay => Items.Count == 1 ? "1 item" : Items.Count + " items";
    }
}
