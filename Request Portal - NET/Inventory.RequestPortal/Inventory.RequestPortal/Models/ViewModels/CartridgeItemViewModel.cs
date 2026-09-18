namespace Inventory.RequestPortal.Models.ViewModels
{
    // CARTRIDGE INBOUND REQUEST FLOW:
    //
    // Employees return cartridges by MODEL, not by serial number.
    // This ViewModel now represents DISTINCT MODELS with aggregated stock.
    // The dropdown is a GUIDE only - the typed model is the PRIMARY input.

    /// <summary>
    /// ViewModel for cartridge models with aggregated stock count.
    /// Used as a dropdown guide for the Request Portal.
    ///
    /// IMPORTANT: The typed model number is the PRIMARY input.
    /// This dropdown is only to help jog the user's memory.
    /// </summary>
    public class CartridgeItemViewModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Model number - the key identifier for cartridge inbound requests.
        /// Employees know and return cartridges by model, not serial.
        /// </summary>
        public string? ModelNumber { get; set; }

        /// <summary>
        /// SerialNumber - always "N/A" for model-grouped items.
        /// Cartridge inbound requests don't use individual serials.
        /// </summary>
        public string? SerialNumber { get; set; }

        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Aggregated stock count for this model.
        /// Summed across all items with the same ModelNumber.
        /// </summary>
        public int StockOnHand { get; set; }

        public bool Active { get; set; }

        /// <summary>
        /// Display format: ModelNumber - Name (Stock: X)
        /// Where X is the aggregated stock count for that model.
        /// </summary>
        public string DisplayName
        {
            get
            {
                string model = !string.IsNullOrWhiteSpace(ModelNumber) ? ModelNumber : ItemName;
                return $"{model} - {ItemName} (Stock: {StockOnHand})";
            }
        }
    }
}
