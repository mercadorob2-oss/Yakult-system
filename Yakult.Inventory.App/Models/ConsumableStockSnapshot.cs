using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// One active dbo.Item catalog row that is linked to a ConsumableModel or a
    /// CartridgeModel, carrying its current StockOnHand and a normalised bucket
    /// (Cartridge / Ink / Toner / Printhead / Other). Polled on a timer and diffed by the
    /// Admin Portal's Consumable Stock Monitor page to build a live change feed.
    /// </summary>
    public class ConsumableStockItemDto
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ModelNumber { get; set; }
        public string RawCategory { get; set; }
        public int StockOnHand { get; set; }
        public int? ConsumableModelId { get; set; }
        public int? CartridgeModelId { get; set; }

        /// <summary>"Cartridge", "Ink", "Toner", "Printhead", or "Other".</summary>
        public string Bucket => ConsumableStockBuckets.Classify(RawCategory, CartridgeModelId.HasValue);
    }

    /// <summary>
    /// One historical dbo.Inventory ledger entry for a consumable / cartridge item, used by
    /// the Consumable Stock Monitor's "Yesterday" / "Last 7 days" ranges. SignedQty is the
    /// movement magnitude with sign applied (Negative entries become negative).
    /// </summary>
    public class ConsumableStockHistoryDto
    {
        public DateTime PostedAtLocal { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ModelNumber { get; set; }
        public string RawCategory { get; set; }
        public bool CartridgeLinked { get; set; }
        public string EntryType { get; set; }
        public int SignedQty { get; set; }
        public string Description { get; set; }
        public string PostedByName { get; set; }
        /// <summary>True for an outstanding dbo.Request line (not a posted ledger movement).</summary>
        public bool IsPending { get; set; }

        public string Bucket => ConsumableStockBuckets.Classify(RawCategory, CartridgeLinked);
    }

    /// <summary>
    /// The four consumable buckets the monitor colour-codes, plus the "Other" catch-all,
    /// and the free-text category classifier that maps a raw dbo.Item.Category /
    /// dbo.ConsumableModel.Category string onto one of them. Matching mirrors the
    /// ink/toner/printhead LIKE logic already in ConsumableModelRepository.
    /// </summary>
    public static class ConsumableStockBuckets
    {
        public const string Cartridge = "Cartridge";
        public const string Ink       = "Ink";
        public const string Toner     = "Toner";
        public const string Printhead = "Printhead";
        public const string Other     = "Other";

        /// <summary>The four monitored buckets, in display order.</summary>
        public static readonly string[] Monitored = { Cartridge, Ink, Toner, Printhead };

        public static string Classify(string rawCategory, bool isCartridgeModelLinked)
        {
            var c = (rawCategory ?? string.Empty).ToLowerInvariant().Replace(" ", string.Empty);

            if (c.Contains("printhead") || c.Contains("printehead")) return Printhead;
            if (c.Contains("toner")) return Toner;
            if (c.Contains("ink")) return Ink;
            if (isCartridgeModelLinked || c.Contains("cartridge")) return Cartridge;
            return Other;
        }
    }
}
