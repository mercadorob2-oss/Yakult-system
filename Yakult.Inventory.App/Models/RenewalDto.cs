using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Data Transfer Object for Renewal tracking
    /// Maps to vw_RenewalStatus view
    /// </summary>
    public class RenewalDto
    {
        public bool Selected { get; set; }
        public int SetId { get; set; }
        public string SetCode { get; set; }
        public string SetType { get; set; }
        public string DocumentNumber { get; set; }
        public string ReferenceNumber { get; set; }
        public DateTime? DocumentDate { get; set; }
        public string Status { get; set; }
        public string Remarks { get; set; }

        /// <summary>The Set's current Renewal Notes (dbo.Renewals.RenewalNotes) — entered once per
        /// renewal action and shared by every item renewed together, so this surfaces whichever
        /// item's current non-archived note is set. Distinct from the Set-level Remarks above.</summary>
        public string RenewalNotes { get; set; }

        // Renewal Period Information
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? DaysUntilExpiry { get; set; }
        public string ExpiryStatus { get; set; }

        // Company Information
        public int? ComId { get; set; }
        public string CompanyName { get; set; }

        // Vendor Information (from SetItems)
        public int? VendorId { get; set; }
        public string VendorName { get; set; }

        // Branch and Department Information
        public int? CurrentBranchId { get; set; }
        public string CurrentBranchName { get; set; }
        public int? CurrentDepartmentId { get; set; }
        public string CurrentDepartmentName { get; set; }

        // Site Information (combined Branch + Department for display)
        public string SiteName { get; set; }
        public string SiteDisplay =>
            !string.IsNullOrEmpty(CurrentBranchName) && !string.IsNullOrEmpty(CurrentDepartmentName)
                ? $"{CurrentBranchName} - {CurrentDepartmentName}"
                : !string.IsNullOrEmpty(CurrentBranchName)
                    ? CurrentBranchName
                    : !string.IsNullOrEmpty(CurrentDepartmentName)
                        ? CurrentDepartmentName
                        : SiteName ?? "N/A";

        // Financial Information
        public decimal Subtotal { get; set; }
        public decimal VatAmount { get; set; }
        public decimal WhtAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmountDue { get; set; }

        // Item Count (total across all statuses)
        public int ItemCount { get; set; }

        // Per-status item counts (from vw_RenewalStatus aggregated subqueries)
        public int TotalActiveItems { get; set; }
        public int ExpiredItemsCount { get; set; }
        public int RenewedItemsCount { get; set; }
        public int ArchivedItemsCount { get; set; }

        // Set-level computed status (accounts for partial renewals)
        // Values: Active | Expiring Soon | Warning | Expired | Partially Renewed | Fully Renewed | No Expiry Date | No Items
        public string SetLevelStatus { get; set; }

        // Part Number (from dbo.Renewals.PartNumber, first non-null for the set)
        public string PartNumber { get; set; }

        // Renewal Tracking
        public DateTime? RenewedDate { get; set; }

        // Archive Status (from Item.Active - if all items in set are archived, renewal is archived)
        public bool Active { get; set; } = true;

        // Renewal chain: non-null when this Set was created as a renewal of another Set
        public int? RenewalOfSetId { get; set; }

        // Audit Information
        public int? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
