using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Pages
{
    // ===== DTOs (C# 7.3 friendly)

    /// <summary>
    /// A Set that still has its QR code stored only as a local file path (pre-DB-storage migration).
    /// </summary>
    public class QrBackfillCandidateDto
    {
        public int SetId { get; set; }
        public string QRImagePath { get; set; }
    }

    /// <summary>
    /// A receipt document that still has an image stored only as a local file path, with no
    /// corresponding bytes in the database. ImageId is set for rows coming from the multi-image
    /// table (dbo.ReceiptSetDocumentImage); it is null for legacy single-image columns on
    /// dbo.ReceiptSet (SiImage/DrImage/PoImage), which are looked up by ReceiptSetId + DocType instead.
    /// </summary>
    public class ReceiptBackfillCandidateDto
    {
        public int ReceiptSetId { get; set; }
        public int? ImageId { get; set; }
        public string DocType { get; set; }
        public string ImagePath { get; set; }
    }

    public class CompanyDto
    {
        public bool Selected { get; set; }  // For checkbox selection in grid
        public int ComId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime DateCreated { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }

        public bool Active { get; set; } = true;
        public bool IsArchived { get; set; }  // Archive status
        // Optional lists for bulk creation (wizard mode)
        public List<DepartmentDto> Departments { get; set; } = new List<DepartmentDto>();
        public List<BranchDto> Branches { get; set; } = new List<BranchDto>();
    }

    public class DepartmentDto
    {
        public bool Selected { get; set; }  // For checkbox selection in grid
        public int DeptId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime DateCreated { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }

        public bool Active { get; set; } = true;
        public bool IsArchived { get; set; }  // Archive status
        // Optional company association (nullable for independent departments)
        public int? CompanyId { get; set; }
        // Optional branch association (nullable for independent departments)
        public int? BranchId { get; set; }
        // Optional section under the department (e.g. distributor company name)
        public string Section { get; set; }
    }

    public class BranchDto
    {
        public bool Selected { get; set; }  // For checkbox selection in grid
        public int BranchId { get; set; }  // Primary key for edit operations
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime DateCreated { get; set; }

        public bool Active { get; set; } = true;
        public bool IsArchived { get; set; }  // Archive status
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }
        public DateTime? DateModified { get; set; }
        public int? ModifiedByUserId { get; set; }
        public string ModifiedByName { get; set; }

        // Optional associations (nullable for independent branches)
        public int? CompanyId { get; set; }
        public int? DepartmentId { get; set; }  // NEW: Branch can also reference Department

        // Display names (for dropdowns)
        public string CompanyName { get; set; }
        public string DepartmentName { get; set; }

        // Branch Categories (checkboxes)
        public bool IsFactory { get; set; }
        public bool IsDepot { get; set; }
        public bool IsCenter { get; set; }
        public bool IsDistributor { get; set; }

        // Center Region (only applicable if IsCenter = true)
        // Stores the specific region like "NCR", "Region I - Ilocos", etc.
        public string CenterRegion { get; set; }
    }

    public class EmployeeDto
    {
        public int EmpId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime DateCreated { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }

        // Required associations (Company and Branch required; Department is optional)
        public int CompanyId { get; set; }
        public int? DepartmentId { get; set; }
        public int BranchId { get; set; }

        // Optional posting toward an independent distributor's office.
        // Only a selected few employees carry one; everyone else stays null.
        public int? DistributorId { get; set; }

        public string Position { get; set; }
        public string EmployeeNumber { get; set; }
        public int? TitleId { get; set; }
        public string TitleName { get; set; }
        public bool Active { get; set; }
        public bool IsArchived { get; set; }  // Archive status
        
        // For IAM page
        public bool HasAccount { get; set; }
        public int? UserId { get; set; }
        public string CompanyName { get; set; }
        public string DepartmentName { get; set; }
        public string BranchName { get; set; }
        public string DistributorName { get; set; }
    }

    public class RoleDto
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime DateCreated { get; set; }
        
        // For checkbox binding in role assignment
        public bool IsAssigned { get; set; }
    }

    public class UserRoleDto
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public DateTime DateAssigned { get; set; }
    }

    public class UserAccountDto
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string EmailAddress { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeveloper { get; set; }
        public int? EmpId { get; set; }
        public string EmployeeName { get; set; }
        public bool IsTemporaryPassword { get; set; }
        public bool MustChangePassword { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public DateTime DateCreated { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }

    public class ItemDto
    {
        public bool Selected { get; set; }  // For checkbox selection in grid
        public int ItemId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Active { get; set; }
        public bool DbActive { get; set; }  // Original Active value from database (before transaction check)
        public bool IsArchived { get; set; }
        public string Category { get; set; }
        public string SerialNumber { get; set; }

        public string ModelNumber { get; set; }
        public int? CartridgeModelId { get; set; }  // FK to dbo.CartridgeModel (for cartridges only)
        public int? ConsumableModelId { get; set; }  // FK to dbo.ConsumableModel (Ink/Toner/Print Head only)
        public int CategoryId { get; set; }
        public string UnitOfMeasure { get; set; }
        public int StockOnHand { get; set; }

        public string ItemType { get; set; } = "Hardware";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        /// <summary>Optional default Sub-Type classification (Contract/Subscription/License/
        /// Services) — a reporting hint only, distinct from the per-invoice Sub-Type Entries
        /// tracked in dbo.Contract/Subscription/License/ServiceDetail.</summary>
        public string SubType { get; set; }

        // New inventory control columns
        public bool AffectsInventory { get; set; }  // Determines if stock should be increased/decreased
        public string AcquisitionType { get; set; }  // 'Request' | 'Invoice' | 'Both'
        public bool IsTrackedAsset { get; set; }  // Determines if item is subject to warranty/renewal tracking

        public decimal Amount { get; set; }  // Price/Amount for the item
        public DateTime DateCreated { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }
        public DateTime? DateModified { get; set; }
        public int? ModifiedByUserId { get; set; }
        public string ModifiedByName { get; set; }

        // Condition tracking
        public int ConditionId { get; set; }
        public string ConditionName { get; set; }  // Display name: Good, Damaged
        public string Remarks { get; set; }            // Damage remarks/notes

        // Vendor tracking
        public int? VendorId { get; set; }
        public string VendorName { get; set; }  // Display name

        // Warranty tracking
        public int WarrantyYears { get; set; }  // Number of warranty years
        public DateTime? WarrantyStartDate { get; set; }  // Computed: same as DateCreated
        public DateTime? WarrantyEndDate { get; set; }  // Computed: DateCreated + WarrantyYears

        // Duration tracking (for contracts/services)
        public int DurationYears { get; set; }  // Number of duration years
        public DateTime? DurationStartDate { get; set; }  // Start date of duration/contract
        public DateTime? DurationEndDate { get; set; }  // End date of duration/contract

        // Purchase date
        public DateTime? DatePurchased { get; set; }  // Date when the item was purchased

        // License Number (for Software/License and Services items)
        public string LicenseNumber { get; set; }  // Optional license number field

        // Part Number (stored in dbo.Renewals, not dbo.Item)
        public string PartNumber { get; set; }

        // Cell phone tracking (Category = CellPhone only; NULL for every other category)
        public string CellPhoneNumber { get; set; }
        public string IMEI1 { get; set; }
        public string IMEI2 { get; set; }

        // Marks an item as eligible for the Borrow Items "select from list" picker
        public bool? IsBorrowable { get; set; }

        // Added for mobile updates and repair history
        public string LatestStatus { get; set; }
        public string LatestRemark { get; set; }
        public int RepairCount { get; set; }
        public string LastRepairAction { get; set; }

        // Cartridge refill workflow tracking
        public string RefillStatus { get; set; }  // 'For Refill', 'Refilling', 'Refilled', NULL (not applicable)

        // Set to true when a cartridge item was imported without a CartridgeModelId and the default 'Unassigned' model was used
        public bool UsedDefaultCartridgeModel { get; set; }

        // Transient — set only by SearchRepository.SearchItemsAsync when this item matched the
        // home search query via a Sub-Type Group Reference Code (dbo.SetItem.ReferenceCode)
        // rather than Name/Serial/Model. Not a real Item-level field.
        public string MatchedSubTypeReferenceCode { get; set; }

        // Transient — set only by SearchRepository.SearchDirectMatchesAsync when this "item" is
        // really a synthetic stand-in for a Set or Request that matched the home search query
        // directly (e.g. by SetCode), with no underlying dbo.Item row at all. When true, ItemId
        // is meaningless (0) and only Name/Category/ItemType/IsArchived/the fields below carry
        // real data — the search UI must not treat this like a normal Item result.
        public bool IsDirectMatch { get; set; }
        public string DirectMatchStatus { get; set; }
        public string DirectMatchSubtitle { get; set; }

        // Transient — set only by SearchRepository.FuzzySearchFallbackAsync when this result is a
        // "closest match" typo-tolerance suggestion rather than a literal match, so the search UI
        // can visually distinguish it (e.g. a "Did you mean...?" label) instead of presenting it as
        // an exact hit.
        public bool IsFuzzyMatch { get; set; }

        // Valid destinations for home search (populated by SearchRepository)
        public System.Collections.Generic.HashSet<string> ValidDestinations { get; set; } = new System.Collections.Generic.HashSet<string>();

        // Transaction counts per transactional destination (Set, Request, Invoice, Renewal, Renewals (Grouped))
        // Used by the home search to show the correct badge count instead of item-group count.
        public System.Collections.Generic.Dictionary<string, int> DestinationCounts { get; set; } = new System.Collections.Generic.Dictionary<string, int>();
    }

    public class RequestDto
    {
        public bool Selected { get; set; } // For checkbox selection
        public int ReqId { get; set; }
        public DateTime? DateRequested { get; set; }
        public string Description { get; set; }
        public string Remarks { get; set; }
        public string Status { get; set; }
        public string EntryType { get; set; }
        public int Quantity { get; set; }
        public int IssuedQty { get; set; }  // How much of Quantity has actually been issued so far
        public decimal UnitPrice { get; set; }  // Snapshot of Item.Amount at request time
        public DateTime DateCreated { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }
        public int ModifiedByUserId { get; set; }
        public string ModifiedByName { get; set; }

        // Foreign Keys
        public int ItemId { get; set; }
        public int? EmpId { get; set; }
        public int? EmployeeId { get; set; }  // Alias for EmpId (for consistency)
        public int? SetId { get; set; }  // Optional - requests can be part of a Set
        public int? ConditionID { get; set; }  // Optional - only for hardware items with condition tracking

        // Org-unit assignment (used when no specific employee is the requester)
        public int? ComId { get; set; }
        public int? DeptId { get; set; }
        public int? BranchId { get; set; }

        // Independent sales distributor (dbo.Distributor) for dept-level requests.
        // Not under Company/Department/Branch; stored on dbo.Request.DistributorId.
        public int? DistributorId { get; set; }

        // Display names (for UI reference, not saved to DB)
        public string EmployeeName { get; set; }
        public string EmployeeNumber { get; set; }
        public string EmployeePosition { get; set; }  // Employee position from Employee table
        public string CompanyName { get; set; }
        public string DepartmentName { get; set; }
        public string BranchName { get; set; }
        public string DistributorName { get; set; }
        public string ItemName { get; set; }

        public string ModelNumber { get; set; }
        public string Category { get; set; }
        public string SerialNumber { get; set; }

        // Fixed Asset flag from Item
        public bool IsTrackedAsset { get; set; }

        // Multi-model submission tracking (Portal modernization)
        // Links related requests from same submission (e.g., multiple cartridge models requested together)
        public Guid? SubmissionSessionId { get; set; }

        // Request source: 'PORTAL' (self-request), 'PORTAL_ASSISTED' (IT on behalf of employee), 'INTERNAL'
        public string RequestSource { get; set; }

        // Explicit workflow ownership: 'CartridgeManagement' (pure-cartridge submissions) or
        // 'RequestSetManagement' (mixed/Ink/Printhead/Toner submissions). Null for requests
        // outside this two-workflow split (e.g. Cable, Projector). Assigned once at submission
        // time by RequesterPortalService — see dbo.Request.WorkflowType.
        public string WorkflowType { get; set; }

        // Employee who will physically receive the cartridges (required when FulfillmentMethod = "PICKUP")
        public int? ReceivedById { get; set; }
        public string ReceivedByName { get; set; }

        // Display-only enrichment for the Set/Condition this request is tied to
        public string SetCode { get; set; }
        public string ConditionName { get; set; }
    }

    public class SetDto
    {
        public bool Selected { get; set; }  // For checkbox selection in grid
        public int SetId { get; set; }
        public string SetCode { get; set; }  // Computed column: SET-0001, SET-0002, etc.
        public bool IsInvoice { get; set; }
        public bool IsArchived { get; set; }
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid QRToken { get; set; }
        public string QRImagePath { get; set; }
        public byte[] QRImageData { get; set; }
        // Set by list-style queries (GetAllSetsAsync) that don't load the full QRImageData blob per row.
        public bool QRImageDataPresent { get; set; }
        public bool HasQrImage => !string.IsNullOrWhiteSpace(QRImagePath) || QRImageDataPresent || (QRImageData != null && QRImageData.Length > 0);
        public string QRImageDisplay => !HasQrImage
            ? null
            : (!string.IsNullOrWhiteSpace(QRImagePath) ? QRImagePath : "(Stored in database)");
        public string Remarks { get; set; }
        public int ItemCount { get; set; }  // Count of requests in this set
        public int ImageCount { get; set; }  // Count of images attached to this set

        public DateTime? DispatchDate { get; set; }  // Nullable - might not be set yet
        public string SetType { get; set; }

        public string QRData { get; set; }

        public string DocumentNumber { get; set; }
        public string ReferenceNumber { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime EffectiveExpiry
        {
            get { return (EndDate ?? CreatedAt.AddYears(5)); }
        }
        public string UpgradeReason { get; set; }
        public int? DaysLeft { get; set; }
        public string Company { get; set; }
        // Independent sales distributor on the Set header (dbo.Set.DistributorId).
        // Not under Company/Department/Branch.
        public int? DistributorId { get; set; }
        public string DistributorName { get; set; }
        public string Site { get; set; }
        public string Status { get; set; }

        public DateTime? DocumentDate
        {
            get { return DispatchDate; }
            set { DispatchDate = value; }
        }

        // Financial fields for Set calculations
        public decimal Subtotal { get; set; }
        public decimal VatAmount { get; set; }
        public decimal WhtAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmountDue { get; set; }

        // Current ownership tracking (for transfers)
        public int? CurrentEmployeeId { get; set; }
        public int? CurrentBranchId { get; set; }
        public int? CurrentDepartmentId { get; set; }
        public int? CurrentCompanyId { get; set; }

        // Display names for current ownership
        public string CurrentEmployeeName { get; set; }
        public string CurrentEmployeeNumber { get; set; }
        public string CurrentBranchName { get; set; }
        public string CurrentDepartmentName { get; set; }
        public string CurrentCompanyName { get; set; }
        
        // Original request reference (ReqId links to Request -> Employee -> Branch/Dept)
        public int? ReqId { get; set; }

        // Earliest DateRequested among the Requests attached to this Set
        public DateTime? DateRequested { get; set; }

        // Hardware tracking info at the set level
        public string ComputerName { get; set; }
        public string IPAddress { get; set; }

        // Cartridge issuance tracking by condition
        public int? IssuedBrandNewQty { get; set; }
        public int? IssuedRefilledQty { get; set; }

        // Receiver tracking (who collected the items)
        public int? ReceivedById { get; set; }
        public string ReceivedByName { get; set; }

        // Auto-computed Set Status based on QR/PDF generation
        public string SetStatus
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Status))
                    return Status;

                if (string.Equals(SetType, "Cartridge", StringComparison.OrdinalIgnoreCase))
                    return "Pending";

                return string.IsNullOrWhiteSpace(QRImagePath) ? "Pending" : "Dispatched";
            }
        }
    }

    public class SetDetailRequestDto
    {
        public int ReqId { get; set; }
        public int ItemId { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; }
        public string ItemName { get; set; }
        public string Category { get; set; }
        public string SerialNumber { get; set; }

        public string ModelNumber { get; set; }
        public string Remarks { get; set; }

        // Fixed Asset flag from Item
        public bool IsTrackedAsset { get; set; }
    }

    public class ItemLookupDto
    {
        public int ItemId { get; set; }
        public string Name { get; set; }
        public string ModelNumber { get; set; }
        public string Category { get; set; }
        public string SerialNumber { get; set; }
        public int StockOnHand { get; set; }

        public override string ToString()
        {
            var modelPart = string.IsNullOrWhiteSpace(ModelNumber) ? "" : $" ({ModelNumber})";
            var serialPart = string.IsNullOrWhiteSpace(SerialNumber) ? "" : $" - {SerialNumber}";
            return $"{Name}{modelPart}{serialPart}";
        }

        public string DisplayText
        {
            get { return ToString(); }
        }
    }

    public class SetItemUpgradeDto
    {
        public int ReqId { get; set; }
        public int OldItemId { get; set; }
        public int NewItemId { get; set; }
        public int Quantity { get; set; }
    }

    public class EmployeeDetailDto
    {
        public int EmpId { get; set; }
        public string EmployeeName { get; set; }
        public string CompanyName { get; set; }
        public string DepartmentName { get; set; }
        public string BranchName { get; set; }
        public string Position { get; set; }
        public string TitleDescription { get; set; }
        public string EmployeeNumber { get; set; }
    }

    /// <summary>
    /// DTO for Cartridge SetItems (from dbo.SetItem table)
    /// Used for Set-centric Cartridge email templates
    /// </summary>
    public class CartridgeSetItemDto
    {
        public int SetItemId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ModelNumber { get; set; }
        public int Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? Amount { get; set; }
    }

    public class SetItemPdfRow
    {
        public int SetId { get; set; }
        public string ItemName { get; set; }
        public string ModelNumber { get; set; }
        public string SerialNumber { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? Amount { get; set; }
    }

    public class CartridgeExchangeModelSummaryDto
    {
        public string CartridgeModel { get; set; }
        public int ReturnedEmptyQty { get; set; }
        public int IssuedFullQty { get; set; }
        public int UnfulfilledQty { get; set; }
    }

    // ===== Item Category DTO =====
    public class ItemCategoryDto
    {
        public bool Selected { get; set; } // For checkbox selection
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; } = true;
        public DateTime DateCreated { get; set; }
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; }
        public bool IsArchived { get; set; }
        public int ItemCount { get; set; }  // Number of items in this category
    }

    public class CategorySummaryDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int ActiveStock { get; set; }
        public int RequestedItems { get; set; }

        // Condition breakdown columns (simplified to Good and Damaged only)
        public int GoodCount { get; set; }
        public int DamagedCount { get; set; }

        public int SerializedItems { get; set; }
        public int TotalItems { get; set; }
    }

    public class ItemCategoryBreakdownDto
    {
        public string CategoryName { get; set; }
        public int TotalItems { get; set; }
        public int ActiveItems { get; set; }
        public int InactiveItems { get; set; }
        public int TotalStock { get; set; }
    }

    public class ItemStockSummaryDto
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string CategoryName { get; set; }
        public int ActiveStock { get; set; }
        public int RequestedItems { get; set; }
        public int GoodCount { get; set; }
        public int DamagedCount { get; set; }
        public string FixedAsset { get; set; }
    }

    // ===== Condition Lookup DTO =====
    public class ConditionDto
    {
        public int ConditionId { get; set; }
        public string ConditionName { get; set; }  // Good, Damaged
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // ===== Vendor DTOs =====
    public class VendorDto
    {
        public bool Selected { get; set; } // For checkbox selection
        public int VendorId { get; set; }
        public string VendorName { get; set; }
        public string Address { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsArchived { get; set; }  // Archive status
        public bool IsRefiller { get; set; }  // True if vendor performs cartridge refill
        public bool IsDisposer { get; set; }  // True if vendor accepts cartridges for disposal
        public bool IsBuyer    { get; set; }  // True if vendor purchases used cartridges
        public DateTime CreatedDate { get; set; }
        public string TIN { get; set; }  // Tax Identification Number
    }

    public class VendorLinkedItemDto
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string Category { get; set; }
        public string ItemType { get; set; }
        public int StockOnHand { get; set; }
        public bool Active { get; set; }
        public string SerialNumber { get; set; }
        public string ModelNumber { get; set; }
        public string VendorName { get; set; }
    }

    public class VendorItemCategoryDto
    {
        public int VendorItemCategoryId { get; set; }
        public int VendorId { get; set; }
        public int? CategoryId { get; set; }  // Nullable - can be null if ItemType is specified
        public string ItemType { get; set; }  // Nullable - can be null if CategoryId is specified
                                              // Values: "Consumable/Hardware", "Software/License", "Service"
        public DateTime DateCreated { get; set; }
        public int CreatedByUserId { get; set; }
        
        // Display names (for UI reference)
        public string VendorName { get; set; }
        public string CategoryName { get; set; }
    }

    // ===== Set Transfer DTOs =====
    public class SetTransferDto
    {
        public int SetTransferId { get; set; }
        public int SetId { get; set; }
        public int FromBranchId { get; set; }
        public int ToBranchId { get; set; }
        public int? FromDepartmentId { get; set; }
        public int? ToDepartmentId { get; set; }
        public string TransferReason { get; set; }
        public DateTime? TransferDate { get; set; }  // Actual transfer completion date
        public int RequestedBy { get; set; }  // Employee who requested the transfer
        public int? ApprovedBy { get; set; }  // Employee who approved the transfer
        public string Status { get; set; }  // Pending, Approved, Completed, Rejected
        public DateTime DateCreated { get; set; }
        public int CreatedByUserId { get; set; }
        
        // Display names (for UI reference)
        public string SetCode { get; set; }
        public string FromBranchName { get; set; }
        public string ToBranchName { get; set; }
        public string FromDepartmentName { get; set; }
        public string ToDepartmentName { get; set; }
        public string RequestedByName { get; set; }
        public string ApprovedByName { get; set; }
        public string CreatedByName { get; set; }
    }

    public class ReceiptSetDto
    {
        public int ReceiptSetId { get; set; }
        public int? SetId { get; set; }
        public string SetCode { get; set; }
        public int? RenewedFromReceiptSetId { get; set; }
        public string Supplier { get; set; }
        public string SiNumber { get; set; }
        public string DrNumber { get; set; }
        public string PoNumber { get; set; }
        public string SiImagePath { get; set; }
        public string DrImagePath { get; set; }
        public string PoImagePath { get; set; }
        public byte[] SiImage { get; set; }
        public byte[] DrImage { get; set; }
        public byte[] PoImage { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public int? ModifiedBy { get; set; }

        // Optional list-page enrichment (may be NULL depending on query)
        public int? TotalItems { get; set; }
        public int? RenewedItems { get; set; }
    }

    public class ReceiptSetLinkedSetDto
    {
        public int SetId { get; set; }
        public string SetCode { get; set; }

        // Populated only by GetSetLinkInfo -- null/false for callers that don't need them
        // (GetLinkedSets, FindInvoiceSetByDocumentNumber) since their SELECTs don't project these.
        public string DocumentNumber { get; set; }
        public bool IsInvoice { get; set; }
    }

    public class SetItemUpdateDto
    {
        public int UpdateId { get; set; }
        public int? SetId { get; set; }
        public string SetCode { get; set; }
        public int? ItemId { get; set; }
        public string ItemType { get; set; }
        public string SerialNumber { get; set; }
        public string ModelNumber { get; set; }
        public string PreviousStatus { get; set; }
        public string NewStatus { get; set; }
        public string Remark { get; set; }
        public string BranchName { get; set; }
        public string DepartmentName { get; set; }
        public string UpdatedByUserId { get; set; }
        public string UpdatedByName { get; set; }
        public string Source { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Processed { get; set; }
        public string ProcessedBy { get; set; }
        public DateTime? ProcessedAt { get; set; }

        // New flags for validation
        public bool IsArchived { get; set; }
        public bool IsMissing { get; set; }
    }

    public class ItemMovementAuditDto
    {
        public DateTime EventTime { get; set; }
        public string Direction { get; set; }
        public string SerialNumber { get; set; }
        public int? ItemId { get; set; }
        public string ItemName { get; set; }
        public string ModelNumber { get; set; }
        public int? SetId { get; set; }
        public string SetCode { get; set; }
        public int Quantity { get; set; }
        public string EmployeeName { get; set; }
        public string CompanyName { get; set; }
        public string BranchName { get; set; }
        public string DepartmentName { get; set; }
        public string UserName { get; set; }
        public string Source { get; set; }
        public string ReferenceType { get; set; }
        public int? ReferenceId { get; set; }

        public string AuditReferenceType { get; set; }
        public int? AuditReferenceId { get; set; }

        public string Notes { get; set; }

        public string InventoryEntryType { get; set; }
        public string RequestStatus { get; set; }
        public string RequestEntryType { get; set; }

        public string AuditAction { get; set; }
        public string AuditStatus { get; set; }
        public string AuditLocation { get; set; }

        public string MovementCategory { get; set; }
        public string MovementType { get; set; }
        public string MovementPriority { get; set; }

        public string StatusBefore { get; set; }
        public string StatusAfter { get; set; }

        public string PrevSetCode { get; set; }
        public string NewSetCode { get; set; }
        public string PrevBranchName { get; set; }
        public string NewBranchName { get; set; }
        public string PrevDepartmentName { get; set; }
        public string NewDepartmentName { get; set; }
        public string PrevEmployeeName { get; set; }
        public string NewEmployeeName { get; set; }

        public bool IsArchived { get; set; }

    }

}
