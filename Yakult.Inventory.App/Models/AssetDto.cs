using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Represents a physical device / asset that is tracked independently of renewal SKUs.
    /// Assets are identified by Part Number (ModelNumber) and/or SerialNumber.
    /// At least one of ModelNumber or SerialNumber MUST be provided (DB constraint).
    ///
    /// Relationship:
    ///   Renewals.AssetId FK → dbo.Asset.AssetId
    ///   Renewal SKU lives in dbo.Item (ItemType = Services)
    ///   Physical identity lives here in dbo.Asset
    /// </summary>
    public class AssetDto
    {
        public int AssetId { get; set; }

        /// <summary>
        /// Part Number — identifies the hardware model (e.g. UCSC-C220-M5SX).
        /// NULL allowed when asset is identified only by serial number.
        /// </summary>
        public string ModelNumber { get; set; }

        /// <summary>
        /// Serial Number — unique per physical unit.
        /// NULL allowed when asset is a model-anchor without a tracked unit.
        /// UNIQUE constraint enforced in DB.
        /// </summary>
        public string SerialNumber { get; set; }

        /// <summary>Optional free-text description (hostname, location, etc.).</summary>
        public string Description { get; set; }

        /// <summary>Vendor this asset was purchased from.</summary>
        public int? VendorId { get; set; }
        public string VendorName { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public string CreatedByName { get; set; }

        // ── Display helpers ──────────────────────────────────────────────────
        /// <summary>Combined identifier for list display: "Part# | SN" or whichever is present.</summary>
        public string DisplayName
        {
            get
            {
                bool hasModel  = !string.IsNullOrWhiteSpace(ModelNumber);
                bool hasSerial = !string.IsNullOrWhiteSpace(SerialNumber);
                if (hasSerial)
                    return hasModel ? $"{ModelNumber} - {SerialNumber}" : SerialNumber;
                return hasModel ? ModelNumber : $"Asset #{AssetId}";
            }
        }

        public string PartNumber => ModelNumber;

        /// <summary>For checkbox selection in grid (WPF list-page binding; mirrors the
        /// existing Selected convention used by ItemDto and other grid-bound DTOs).</summary>
        public bool Selected { get; set; }
    }
}
