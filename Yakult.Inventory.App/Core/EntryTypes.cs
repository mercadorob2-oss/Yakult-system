using System;

namespace Yakult.Inventory.App.Core
{
    /// <summary>
    /// Constants for Inventory EntryTypes
    /// </summary>
    public static class EntryTypes
    {
        public const string Positive = "Positive";      // Stock added
        public const string Negative = "Negative";      // Stock deducted
        public const string None = "None";              // No stock movement (non-inventory items)

        [Obsolete("Use None instead. Fixed Assets is deprecated.")]
        public const string FixedAssets = "Fixed Assets";  // DEPRECATED - kept for backward compatibility
    }
}
