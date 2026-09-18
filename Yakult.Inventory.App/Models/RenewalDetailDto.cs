using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Data Transfer Object for Renewal Item Details
    /// Maps to sp_GetRenewalDetailsByItemId stored procedure result
    /// </summary>
    public class RenewalDetailDto
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string Description { get; set; }
        public string ItemType { get; set; }
        public string SerialNumber { get; set; }
        public string ModelNumber { get; set; }
        public string LicenseNumber { get; set; }
        public decimal Amount { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateModified { get; set; }
        public string Category { get; set; }
        public string ItemRemarks { get; set; }
        public bool Active { get; set; }

        // Category info
        public string CategoryName { get; set; }

        // Vendor info (from Vendor table: VendorName, Address, TIN)
        public int? VendorId { get; set; }
        public string VendorName { get; set; }
        public string VendorAddress { get; set; }
        public string VendorTIN { get; set; }

        // Condition info
        public string ConditionName { get; set; }

        // Calculated fields
        public int? DaysUntilExpiry { get; set; }
        public string ExpiryStatus { get; set; }

        // Created by user
        public string CreatedByUsername { get; set; }

        // Current renewal status
        public string CurrentRenewalStatus { get; set; }

        // Asset linkage (nullable — legacy renewals without an AssetId map to null)
        public int? AssetId { get; set; }
        /// <summary>Asset.ModelNumber — Part Number of the physical device being renewed.</summary>
        public string PartNumber { get; set; }
        /// <summary>Asset.SerialNumber — unique device identifier.</summary>
        public string AssetSerialNumber { get; set; }

        // Financial Information (from Set table)
        public decimal Subtotal { get; set; }
        public decimal VatAmount { get; set; }
        public decimal WhtAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmountDue { get; set; }

        // Site Information (from Set table)
        public int? CurrentBranchId { get; set; }
        public string CurrentBranchName { get; set; }
        public int? CurrentDepartmentId { get; set; }
        public string CurrentDepartmentName { get; set; }
        public string SiteName { get; set; }

        // Computed Site Display
        public string SiteDisplay =>
            !string.IsNullOrWhiteSpace(SiteName)
                ? SiteName
                : !string.IsNullOrEmpty(CurrentBranchName) && !string.IsNullOrEmpty(CurrentDepartmentName)
                    ? $"{CurrentBranchName} - {CurrentDepartmentName}"
                    : !string.IsNullOrEmpty(CurrentBranchName)
                        ? CurrentBranchName
                        : !string.IsNullOrEmpty(CurrentDepartmentName)
                            ? CurrentDepartmentName
                            : "N/A";
    }

    /// <summary>
    /// Document header fields from dbo.[Set] — editable after renewal.
    /// DocumentDate maps to dbo.[Set].DispatchDate.
    /// </summary>
    public class SetHeaderDto
    {
        public string SetCode { get; set; }
        public string DocumentNumber { get; set; }
        public string ReferenceNumber { get; set; }
        public DateTime? DocumentDate { get; set; }
        public string CreatedByUsername { get; set; }
    }

    /// <summary>
    /// Lightweight invoice set record for the "Link Existing Invoice Set" picker.
    /// </summary>
    public class InvoiceSetPickerDto
    {
        public int SetId { get; set; }
        public string SetCode { get; set; }
        public string SetType { get; set; }
        public string DocumentNumber { get; set; }
        public string ReferenceNumber { get; set; }
        public string CompanyName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal TotalAmountDue { get; set; }
    }

    /// <summary>
    /// Represents a single entry in the renewal chain (original Set + all subsequent renewal Sets).
    /// Used by the chain strip in ViewRenewalDetailPage.
    /// </summary>
    public class RenewalChainDto
    {
        public int SetId { get; set; }
        public string SetCode { get; set; }
        public string DocumentNumber { get; set; }
        public int? RenewalOfSetId { get; set; }
        /// <summary>1 = original invoice, 2 = first renewal, etc. Assigned in C# after query.</summary>
        public int RenewalNumber { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    /// <summary>
    /// Lightweight per-item data used by the WPF Link Renewal screen for match computation.
    /// </summary>
    public class SetItemSummaryDto
    {
        public int     SetId        { get; set; }
        public int     SetItemId    { get; set; }
        public int     ItemId       { get; set; }
        public string  ItemCode     { get; set; }
        public string  Description  { get; set; }
        public decimal Amount       { get; set; }
        public DateTime? LineStartDate { get; set; }
        public DateTime? LineEndDate   { get; set; }
    }

    /// <summary>
    /// Data Transfer Object for Renewal History
    /// Maps to sp_GetRenewalHistoryByItemId stored procedure result
    /// </summary>
    public class RenewalHistoryDto
    {
        public int RenewalId { get; set; }
        public int ItemId { get; set; }
        public string RenewalStatus { get; set; }
        public DateTime? OnHoldDate { get; set; }
        public DateTime? RenewedDate { get; set; }
        public int RenewalCount { get; set; }
        public DateTime? NewStartDate { get; set; }
        public DateTime? NewEndDate { get; set; }
        public int? RenewalYears { get; set; }
        public bool IsArchived { get; set; }
        public DateTime? ArchivedDate { get; set; }
        public string ArchiveReason { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string RenewalNotes { get; set; }
        public decimal? RenewalAmount { get; set; }
        public string CreatedByUsername { get; set; }
        public string ModifiedByUsername { get; set; }
        public string ItemName { get; set; }
        public string ItemCode { get; set; }
    }
}
