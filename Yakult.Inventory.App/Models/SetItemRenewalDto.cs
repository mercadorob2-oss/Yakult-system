using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Per-item renewal data for the SetItemRenewalDialog.
    /// Represents one row in dbo.SetItem with its current renewal tracking state.
    /// </summary>
    public class SetItemRenewalDto
    {
        public int SetItemId { get; set; }
        public int SetId { get; set; }
        public int ItemId { get; set; }
        public string ItemCode { get; set; }
        public string Description { get; set; }

        /// <summary>dbo.Item.Name for the item this line references — used as a fallback
        /// display when Description is blank.</summary>
        public string ItemName { get; set; }
        public decimal Quantity { get; set; }
        public string UnitOfMeasure { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public DateTime? LineStartDate { get; set; }
        public DateTime? LineEndDate { get; set; }

        /// <summary>
        /// Per-item renewal lifecycle: NULL/'Active' | 'Expired' | 'Renewed' | 'Archived'
        /// </summary>
        public string RenewalStatus { get; set; }

        /// <summary>
        /// SetItemId of the new SetItem created when this item was renewed.
        /// </summary>
        public int? RenewalReferenceId { get; set; }

        /// <summary>
        /// Sub-Type Group this item currently belongs to on dbo.SetItem (NULL = ungrouped).
        /// GroupId is the FK into dbo.SetItemSubTypeGroup; SubType/ReferenceCode/BeginDate/EndDate
        /// are the synced-cache copies also stored directly on dbo.SetItem.
        /// </summary>
        public int? GroupId { get; set; }
        public string SubType { get; set; }
        public string ReferenceCode { get; set; }
        public DateTime? BeginDate { get; set; }
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Parent Tag Group this item currently belongs to (NULL = untagged) — an independent,
        /// orthogonal grouping from Sub-Type, with no financial semantics of its own.
        /// ParentTagGroupId is the FK into dbo.SetItemParentTagGroup; ParentTag is its Label.
        /// </summary>
        public int? ParentTagGroupId { get; set; }
        public string ParentTag { get; set; }

        /// <summary>
        /// Display-friendly status (converts NULL → "Active").
        /// </summary>
        public string DisplayStatus =>
            string.IsNullOrEmpty(RenewalStatus) ? "Active" : RenewalStatus;

        /// <summary>
        /// True when this item can be individually renewed (status is Active or Expired).
        /// </summary>
        public bool CanRenew =>
            string.IsNullOrEmpty(RenewalStatus) || RenewalStatus == "Active" || RenewalStatus == "Expired";
    }
}
