using System;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// Centralized helper for Inventory business logic
    /// </summary>
    public static class InventoryHelper
    {
        /// <summary>
        /// Determines the correct EntryType for an Inventory record based on AffectsInventory and Quantity.
        ///
        /// NEW BUSINESS RULE:
        /// - If AffectsInventory = false → ALWAYS "None" (no stock movement)
        /// - If AffectsInventory = true → "Positive" if qty >= 0, "Negative" if qty &lt; 0
        ///
        /// ⚠️ CRITICAL WARNING:
        /// - The dbo.Inventory table has a CHECK constraint: EntryType IN ('Positive', 'Negative', 'Fixed Assets')
        /// - "None" is NOT a valid value for dbo.Inventory and will cause CHECK constraint violations
        /// - NEVER insert into dbo.Inventory when AffectsInventory = false
        /// - Pattern: if (affectsInventory) { var entryType = DetermineEntryType(...); INSERT INTO Inventory; }
        ///
        /// This is the ONLY method that should determine EntryType for Inventory inserts.
        /// All inventory operations must call this method to ensure consistency.
        /// </summary>
        /// <param name="affectsInventory">Whether this item affects inventory stock</param>
        /// <param name="quantity">The quantity being added/removed (can be positive or negative)</param>
        /// <returns>The correct EntryType: "None", "Positive", or "Negative"</returns>
        public static string DetermineEntryType(bool affectsInventory, int quantity)
        {
            // Non-inventory items never affect stock
            // ⚠️ WARNING: "None" is NOT valid for dbo.Inventory table (CHECK constraint violation)
            // Callers MUST check affectsInventory BEFORE inserting into Inventory
            if (!affectsInventory)
            {
                return EntryTypes.None;
            }

            // Inventory items: Positive = adding stock, Negative = removing stock
            return quantity >= 0 ? EntryTypes.Positive : EntryTypes.Negative;
        }

        /// <summary>
        /// DEPRECATED: Determines EntryType based on ItemType (old logic).
        /// Use DetermineEntryType(bool affectsInventory, int quantity) instead.
        ///
        /// Kept for backward compatibility during migration.
        /// </summary>
        [Obsolete("Use DetermineEntryType(bool affectsInventory, int quantity) instead")]
        public static string DetermineEntryType(string itemType, int quantity)
        {
            if (string.IsNullOrWhiteSpace(itemType))
            {
                throw new ArgumentException("ItemType cannot be null or empty", nameof(itemType));
            }

            // Normalize itemType for comparison (trim and case-insensitive)
            string normalizedType = itemType.Trim();

            // OLD LOGIC: Software, License, and Services are ALWAYS "Fixed Assets"
            // These are non-consumable assets tracked individually, not by stock quantity
            if (normalizedType.Equals("Software/License", StringComparison.OrdinalIgnoreCase) ||
                normalizedType.Equals("Software", StringComparison.OrdinalIgnoreCase) ||
                normalizedType.Equals("License", StringComparison.OrdinalIgnoreCase) ||
                normalizedType.Equals("Services", StringComparison.OrdinalIgnoreCase) ||
                normalizedType.Equals("Service", StringComparison.OrdinalIgnoreCase))
            {
                return EntryTypes.FixedAssets;  // Will return "Fixed Assets"
            }

            // Hardware and Consumable items use Positive/Negative based on quantity direction
            // Positive = adding stock, Negative = removing stock
            if (normalizedType.Equals("Hardware", StringComparison.OrdinalIgnoreCase) ||
                normalizedType.Equals("Consumable", StringComparison.OrdinalIgnoreCase) ||
                normalizedType.Equals("Consumables", StringComparison.OrdinalIgnoreCase))
            {
                return quantity >= 0 ? EntryTypes.Positive : EntryTypes.Negative;
            }

            // Fallback for unknown types: treat like Hardware/Consumable
            // Log warning if possible in production code
            return quantity >= 0 ? EntryTypes.Positive : EntryTypes.Negative;
        }
    }
}
