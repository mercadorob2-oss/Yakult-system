using System;
using System.Collections.Generic;
using System.Linq;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallReplacementLine
    {
        // Zero means that this side was intentionally omitted from the entry.
        public bool HasOldItem => OldItemId > 0 || UnlistedOldItem != null;
        public bool HasNewItem => NewItemId > 0;
        public int OldItemId { get; set; }
        public int NewItemId { get; set; }
        public int Quantity { get; set; }
        public int OldItemConditionId { get; set; }
        public string OldItemConditionRemarks { get; set; }
        public string OldItemRepairAction { get; set; }
        public string Remarks { get; set; }
        public ItemDto UnlistedOldItem { get; set; }

        public static string GetValidationError(IList<CallReplacementLine> lines)
        {
            if (lines == null || lines.Count == 0) return "Add at least one old or new item.";
            if (lines.Any(x => x == null || (!x.HasOldItem && !x.HasNewItem) || x.OldItemId < 0 || x.NewItemId < 0))
                return "Each entry must contain an old item, a new item, or both.";
            if (lines.Any(x => x.Quantity <= 0)) return "Every item must have a positive quantity.";
            if (lines.Any(x => x.HasOldItem && x.OldItemConditionId <= 0)) return "Select a condition for each old item.";
            var oldIds = lines.Where(x => x.OldItemId > 0).Select(x => x.OldItemId).ToList();
            if (oldIds.Count != oldIds.Distinct().Count()) return "Each old item can only be included once.";
            if (lines.Any(x => x.HasNewItem && oldIds.Contains(x.NewItemId)))
                return "An old item cannot also be issued as a new item in this resolution.";
            var serials = lines.Where(x => x.UnlistedOldItem != null && !string.IsNullOrWhiteSpace(x.UnlistedOldItem.SerialNumber))
                .Select(x => x.UnlistedOldItem.SerialNumber.Trim()).ToList();
            if (serials.Distinct(StringComparer.OrdinalIgnoreCase).Count() != serials.Count)
                return "Unlisted old items cannot share the same serial number.";
            return null;
        }

    }
}
