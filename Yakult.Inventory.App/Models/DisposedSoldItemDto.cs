using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Maps to dbo.vw_DisposedSoldItems.
    /// Represents one Executed dispose/sell lifecycle decision.
    /// </summary>
    public class DisposedSoldItemDto
    {
        // ── Decision ────────────────────────────────────────────────────
        public int      DecisionId       { get; set; }
        public int?     BatchId          { get; set; }
        public DateTime DecidedAt        { get; set; }
        public string   DecisionTypeName { get; set; }   // "DISPOSE" | "SELL"
        public int      Quantity         { get; set; }
        public string   RecipientName    { get; set; }
        public decimal? SaleAmount       { get; set; }
        public string   Remarks          { get; set; }

        // ── Item ────────────────────────────────────────────────────────
        public int    ItemId          { get; set; }
        public string ItemName        { get; set; }
        public string ItemModelNumber { get; set; }
        public string SerialNumber    { get; set; }

        // ── Category ────────────────────────────────────────────────────
        public int    CategoryId   { get; set; }
        public string CategoryName { get; set; }

        // ── Condition at time of decision ───────────────────────────────
        public string ConditionName { get; set; }

        // ── Who executed the decision ───────────────────────────────────
        public string DecidedByName { get; set; }

        // ── Cartridge extension (null when not a cartridge item) ────────
        public int?      EmptyCartridgeId      { get; set; }
        public int?      CartridgeModelId       { get; set; }
        public string    CartridgeModelNumber   { get; set; }
        public string    CartridgeBrand         { get; set; }
        public string    DisposalCompanyName    { get; set; }
        public string    VendorName             { get; set; }
        public DateTime? CartridgeReturnedAt    { get; set; }

        // ── Computed helpers ────────────────────────────────────────────
        public bool IsCartridge => CartridgeModelId.HasValue;
        public bool IsSold      => string.Equals(DecisionTypeName, "SELL",    StringComparison.OrdinalIgnoreCase);
        public bool IsDisposed  => string.Equals(DecisionTypeName, "DISPOSE", StringComparison.OrdinalIgnoreCase);
    }
}
