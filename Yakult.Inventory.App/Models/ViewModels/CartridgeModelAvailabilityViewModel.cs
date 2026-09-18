using System;

namespace Yakult.Inventory.App.Models.ViewModels
{
    /// <summary>
    /// ViewModel for displaying cartridge models with aggregated availability.
    /// Used in dropdown selection for model-based cartridge requests.
    /// </summary>
    public class CartridgeModelAvailabilityViewModel
    {
        /// <summary>
        /// Normalized model key used for grouping (ModelNumber or Name if ModelNumber is empty).
        /// </summary>
        public string ModelKey { get; set; }

        /// <summary>
        /// Display model number from the database.
        /// </summary>
        public string ModelNumber { get; set; }

        /// <summary>
        /// Item name from a sample cartridge in this model group.
        /// </summary>
        public string ItemName { get; set; }

        /// <summary>
        /// Sample ItemId from the group (used for debugging/reference).
        /// Represents the first item ID in the model group.
        /// </summary>
        public int ItemId { get; set; }

        /// <summary>
        /// Total available quantity across all cartridges of this model.
        /// Aggregated from StockOnHand of all matching items.
        /// </summary>
        public int AvailableQuantity { get; set; }

        /// <summary>
        /// Display-friendly name for dropdown presentation.
        /// </summary>
        public string DisplayName
        {
            get
            {
                var modelDisplay = !string.IsNullOrWhiteSpace(ModelNumber) ? ModelNumber : ItemName;
                if (string.IsNullOrWhiteSpace(ItemName) ||
                    string.Equals(modelDisplay, ItemName, StringComparison.OrdinalIgnoreCase))
                    return modelDisplay;
                return $"{modelDisplay} - {ItemName}";
            }
        }

        /// <summary>
        /// Short display name for space-constrained views (e.g. dropdowns).
        /// </summary>
        public string ShortDisplayName
        {
            get
            {
                var modelDisplay = !string.IsNullOrWhiteSpace(ModelNumber) ? ModelNumber : ItemName;
                if (string.IsNullOrWhiteSpace(ItemName) ||
                    string.Equals(modelDisplay, ItemName, StringComparison.OrdinalIgnoreCase))
                    return modelDisplay;
                return $"{modelDisplay} - {ItemName}";
            }
        }

        /// <summary>
        /// Returns true if this model is currently available for request.
        /// </summary>
        public bool IsAvailable => AvailableQuantity > 0;

        public override string ToString() => DisplayName;
    }
}
