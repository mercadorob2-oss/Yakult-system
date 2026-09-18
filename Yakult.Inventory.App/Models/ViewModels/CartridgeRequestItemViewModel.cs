using System;

namespace Yakult.Inventory.App.Models.ViewModels
{
    /// <summary>
    /// ViewModel for a single cartridge request item in multi-model submissions.
    /// Represents one cartridge model + quantity + condition in the request.
    /// </summary>
    public class CartridgeRequestItemViewModel
    {
        /// <summary>
        /// Which consumable category this row represents: "Cartridge", "Ink", "Printhead", or "Toner Cartridge".
        /// Defaults to "Cartridge" so existing callers that never set this keep the original behavior.
        /// </summary>
        public string Category { get; set; } = "Cartridge";

        /// <summary>
        /// User-typed or selected cartridge model display name.
        /// Example: "HP 680 Black"
        /// </summary>
        public string CartridgeModel { get; set; }

        /// <summary>
        /// Resolved model key for database lookup.
        /// Normalized model number or item name used for grouping.
        /// </summary>
        public string ModelKey { get; set; }

        /// <summary>
        /// Requested quantity for this specific model.
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Number of good (usable) empty cartridges being returned with this request.
        /// </summary>
        public int GoodEmptyQty { get; set; }

        /// <summary>
        /// Number of damaged empty cartridges being returned with this request.
        /// </summary>
        public int DamagedEmptyQty { get; set; }

        /// <summary>
        /// Cartridge return type: "With Cartridge" or "Without Cartridge".
        /// </summary>
        public string CartridgeType { get; set; } = "With Cartridge";

        /// <summary>
        /// Additional remarks specific to this item (optional).
        /// </summary>
        public string ItemRemarks { get; set; }

        /// <summary>
        /// Validates that all required fields are filled.
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(ModelKey)
                && Quantity > 0;
        }

        public override string ToString()
        {
            return $"{CartridgeModel} x{Quantity} (G:{GoodEmptyQty}/D:{DamagedEmptyQty})";
        }
    }
}
