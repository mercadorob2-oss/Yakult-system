using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Presentation-only projection that aggregates dbo.EmptyCartridge rows
    /// sharing the same ReqId + CartridgeModelId into a single grid row.
    ///
    /// The underlying EmptyCartridge rows are NOT modified by this grouping;
    /// bulk Dispose/Sell actions expand back to per-row operations in the repository.
    ///
    /// TEMPORARY: ConditionType ("GOOD" / "DAMAGED") fans out the original group into
    /// two rows so the UI can restrict Sell All to GOOD rows only.
    /// Revert: remove ConditionType, revert GetNonRefillableGroupedAsync to single GROUP BY.
    /// </summary>
    public class NonRefillableGroupedDto
    {
        /// <summary>Nullable — returns with no linked request have ReqId = NULL.</summary>
        public int?     ReqId           { get; set; }
        public int      CartridgeModelId { get; set; }
        public string   CartridgeModel  { get; set; }

        /// <summary>SUM(Quantity) across all underlying EmptyCartridge rows in this group.</summary>
        public int      TotalQuantity   { get; set; }

        /// <summary>COUNT(*) of underlying EmptyCartridge rows — shown in confirmation dialog.</summary>
        public int      RowCount        { get; set; }

        /// <summary>MIN(ReturnedAt) — earliest return date within the group.</summary>
        public DateTime ReturnedAt      { get; set; }

        /// <summary>
        /// TEMPORARY condition split: "GOOD" or "DAMAGED".
        /// GOOD  = ConditionId IS NULL or condition is not 'Damaged' (sellable + disposable).
        /// DAMAGED = condition is explicitly 'Damaged' (dispose-only).
        /// </summary>
        public string   ConditionType   { get; set; }
    }
}
