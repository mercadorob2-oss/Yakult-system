namespace Yakult.Inventory.App.WPF.Renewal.LinkRenewal.ViewModels
{
    /// <summary>
    /// A single row in the item-comparison panel of the Link Renewal screen.
    /// Status is one of: "Matched" | "New" | "Missing"
    /// </summary>
    public class SetItemComparisonRow
    {
        public string ItemCode    { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// "Matched"  — item exists in both the expiring and renewal set.<br/>
        /// "New"      — item is in the renewal set but NOT in the expiring set.<br/>
        /// "Missing"  — item is in the expiring set but NOT in the renewal set.
        /// </summary>
        public string Status { get; set; }
    }
}
