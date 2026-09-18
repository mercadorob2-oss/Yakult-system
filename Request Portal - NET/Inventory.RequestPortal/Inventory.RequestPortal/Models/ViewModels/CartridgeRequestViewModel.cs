using System.ComponentModel.DataAnnotations;

namespace Inventory.RequestPortal.Models.ViewModels
{
    // CARTRIDGE INBOUND REQUEST FLOW:
    //
    // The typed model number in the "Cartridge Model" textbox is the PRIMARY input.
    // Dropdown selection is only a guide to help jog the user's memory.
    //
    // Request Description format:
    // [PORTAL] [MODEL:XXX] PICKUP → Branch, Department
    //
    // Portal requests are INTENT-ONLY - no inventory allocation at request time.

    /// <summary>
    /// ViewModel for submitting cartridge inbound requests from portal.
    ///
    /// IMPORTANT: TypedModelNumber is the PRIMARY input field.
    /// The dropdown (ModelKey) is only a guide - never the authoritative value.
    /// </summary>
    public class CartridgeRequestViewModel
    {
        /// <summary>
        /// PRIMARY INPUT: The user-typed cartridge model number.
        /// This is the authoritative source - takes precedence over dropdown selection.
        /// Stored in description as [MODEL:XXX].
        /// </summary>
        [Required(ErrorMessage = "Please enter the cartridge model number.")]
        [StringLength(100, ErrorMessage = "Model number cannot exceed 100 characters.")]
        public string TypedModelNumber { get; set; } = string.Empty;

        /// <summary>
        /// Dropdown selection key (guide only, NOT authoritative).
        /// Used to help users find the model they want to type.
        /// </summary>
        public string ModelKey { get; set; } = string.Empty;

        /// <summary>
        /// Display-friendly model number from dropdown selection.
        /// Only used if TypedModelNumber is empty.
        /// </summary>
        public string? ModelNumber { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, 300, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Placeholder ItemId for the request record.
        /// Portal requests don't allocate specific inventory items.
        /// </summary>
        public int ItemId { get; set; }

        /// <summary>
        /// Whether the user is returning the cartridge with the physical cartridge unit.
        /// "With Cartridge" or "Without Cartridge" — stored as the leading part of Remarks.
        /// </summary>
        [Required(ErrorMessage = "Please indicate whether you are returning with or without the cartridge.")]
        public string CartridgeCondition { get; set; } = "With Cartridge";

        // Empty cartridge quantity split (replaces CartridgeCondition)
        // GoodEmptyQty + DamagedEmptyQty cannot exceed Quantity
        [Range(0, 1000, ErrorMessage = "Good Qty must be between 0 and 1000.")]
        public int GoodEmptyQty { get; set; } = 0;

        [Range(0, 1000, ErrorMessage = "Damaged Qty must be between 0 and 1000.")]
        public int DamagedEmptyQty { get; set; } = 0;

        // Employee information (for portal users who are not in the Employee table yet)
        [Required(ErrorMessage = "Please enter your name.")]
        [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
        public string EmployeeName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your position.")]
        [StringLength(100, ErrorMessage = "Position cannot exceed 100 characters.")]
        public string EmployeePosition { get; set; } = string.Empty;

        // Destination (Logistics Target) - for tracking where items should be delivered
        [Required(ErrorMessage = "Please select a company.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a company.")]
        public int DestinationCompanyId { get; set; }

        [Required(ErrorMessage = "Please select a branch.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a branch.")]
        public int DestinationBranchId { get; set; }

        [Required(ErrorMessage = "Please select a department.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a department.")]
        public int DestinationDepartmentId { get; set; }

        // Organization names (from employee master record)
        // Single source of truth - used by fulfillment and SMTP
        public string? DestinationCompanyName { get; set; }
        public string? DestinationBranchName { get; set; }
        public string? DestinationDepartmentName { get; set; }

        // Distribution Method -> stored in Description
        [Required(ErrorMessage = "Please select a distribution method.")]
        public string DistributionMethod { get; set; } = "PICKUP"; // "PICKUP" or "DELIVERY"

        // Received By - only applicable for PICKUP fulfillment
        // EmpId of the person who will physically collect the cartridges at the IT office
        public int? ReceivedById { get; set; }

        // Display name for snapshot confirmation (not saved to DB)
        public string? ReceivedByName { get; set; }

        // Optional additional info
        [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
        public string? AdditionalRemarks { get; set; }

        // Audit fields (populated by controller)
        public int CreatedByUserId { get; set; }
        public DateTime? DateRequested { get; set; } = DateTime.Now;

        // IT Assisted Request only: when true, the request targets a Company/Branch/Department
        // directly (Request.EmpId stays NULL) instead of a specific employee. Company/Branch/
        // Department are all optional in this mode — DestinationCompanyId/BranchId/DepartmentId
        // above are reused for the dept-level target selection.
        public bool IsDeptLevel { get; set; }
    }

    /// <summary>
    /// ViewModel for the New Request page containing form data and dropdown options.
    /// Uses Model + Quantity selection (users don't see serial numbers).
    /// </summary>
    public class NewRequestPageViewModel
    {
        public CartridgeRequestViewModel Request { get; set; } = new CartridgeRequestViewModel();

        public List<CartridgeRequestItemViewModel> RequestItems { get; set; } = new List<CartridgeRequestItemViewModel>();

        /// <summary>
        /// Cartridge models with availability count for dropdown.
        /// Shows: "HP 680 – Black (Available: 3)"
        /// </summary>
        public List<CartridgeModelAvailabilityViewModel> CartridgeModels { get; set; } = new List<CartridgeModelAvailabilityViewModel>();

        /// <summary>
        /// Ink / Printhead / Toner Cartridge models, keyed by Category ("Ink", "Printhead", "Toner Cartridge").
        /// Populated from dbo.ConsumableModel, mirroring CartridgeModels but for non-cartridge consumables.
        /// </summary>
        public Dictionary<string, List<CartridgeModelAvailabilityViewModel>> ConsumableModelsByCategory { get; set; }
            = new Dictionary<string, List<CartridgeModelAvailabilityViewModel>>();

        // [LEGACY] Individual cartridges - kept for backwards compatibility
        public List<CartridgeItemViewModel> Cartridges { get; set; } = new List<CartridgeItemViewModel>();

        public List<CompanyViewModel> Companies { get; set; } = new List<CompanyViewModel>();
        public List<BranchViewModel> Branches { get; set; } = new List<BranchViewModel>();
        public List<DepartmentViewModel> Departments { get; set; } = new List<DepartmentViewModel>();

        // Active employees for the "Received By" dropdown (PICKUP only)
        public List<EmployeeViewModel> Employees { get; set; } = new List<EmployeeViewModel>();

        // Department Account: selected employee to submit on behalf of
        // Required when IsDepartmentAccountSession == true
        public int? SelectedEmployeeId { get; set; }

        // Department Account: filtered employees within the dept account's scope
        public List<EmployeeViewModel> DeptAccountEmployees { get; set; } = new List<EmployeeViewModel>();

        // IT Assisted Request — Manual Authorization fields
        // Pre-loaded approvers are loaded via AJAX when employee is selected.
        public bool    ITUseManualAuth      { get; set; }  // true when IT toggles Manual Authorization ON
        public bool    ITQuickAutoApproved  { get; set; }  // shortcut: record as auto-approved (no approver / decision form). Takes precedence over ITUseManualAuth.
        public int?    ITAuthorizedByEmpId  { get; set; }
        public string? ITDecision           { get; set; }  // "Approved" | "Rejected"
        public string? ITRemarks            { get; set; }
        public List<ITApproverViewModel> ITApprovers { get; set; } = new List<ITApproverViewModel>();
    }

    public class CartridgeRequestItemViewModel
    {
        /// <summary>
        /// Which consumable category this row represents: "Cartridge", "Ink", "Printhead", or "Toner Cartridge".
        /// Defaults to "Cartridge" so existing rows/callers that never set this keep the original behavior.
        /// </summary>
        public string Category { get; set; } = "Cartridge";

        [Required(ErrorMessage = "Please enter the cartridge model number.")]
        [StringLength(100, ErrorMessage = "Model number cannot exceed 100 characters.")]
        public string CartridgeModel { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a model from the list.")]
        public string ModelKey { get; set; } = string.Empty;

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; } = 1;

        // Empty cartridge quantity split (GoodEmptyQty + DamagedEmptyQty cannot exceed Quantity)
        [Range(0, 1000, ErrorMessage = "Good Qty must be between 0 and 1000.")]
        public int GoodEmptyQty { get; set; } = 0;

        [Range(0, 1000, ErrorMessage = "Damaged Qty must be between 0 and 1000.")]
        public int DamagedEmptyQty { get; set; } = 0;
    }

    // IMPORTANT:
    // Users select cartridges by MODEL and QUANTITY for usability.
    // Internally, the system always resolves requests to specific
    // cartridges using ItemId / SerialNumber.
    // Model-based auto-matching is forbidden and must never be reintroduced.

    /// <summary>
    /// ViewModel for displaying cartridge models with availability in the dropdown.
    /// Users see: "[ID:123] HP 680 – Black (Available: 3)"
    /// Item ID is shown for debugging purposes.
    /// </summary>
    public class CartridgeModelAvailabilityViewModel
    {
        /// <summary>
        /// Internal key for grouping (normalized ModelNumber or Name).
        /// Used to resolve to specific ItemIds at submission time.
        /// </summary>
        public string ModelKey { get; set; } = string.Empty;

        /// <summary>
        /// Display-friendly model number (e.g., "HP 680 – Black").
        /// </summary>
        public string ModelNumber { get; set; } = string.Empty;

        /// <summary>
        /// Item name for reference.
        /// </summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Sample Item ID from this model group (for debugging purposes).
        /// Shows one of the ItemIds that belongs to this model.
        /// </summary>
        public int ItemId { get; set; }

        /// <summary>
        /// Count of available cartridges for this model.
        /// Available = Active AND NOT OUTBOUND AND NOT FOR REFILL.
        /// </summary>
        public int AvailableQuantity { get; set; }

        /// <summary>
        /// Whether this model can be selected (has available stock).
        /// </summary>
        public bool IsAvailable => AvailableQuantity > 0;

        public string DisplayName => ModelNumber;
    }
}
