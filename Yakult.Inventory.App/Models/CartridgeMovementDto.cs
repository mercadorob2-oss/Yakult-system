using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// DTO for CartridgeMovement records.
    /// Maps to dbo.CartridgeMovement table.
    ///
    /// New columns added:
    /// - BranchId: Branch where the movement occurred
    /// - DeptId: Department where the movement occurred
    /// - ConditionType: Condition of returned cartridge (Empty, Broken, Good)
    /// </summary>
    public sealed class CartridgeMovementDto
    {
        public int MovementId { get; set; }
        public int ItemId { get; set; }
        public int CartridgeTypeId { get; set; }
        public string MovementType { get; set; }  // StockIn, Issued, Returned, RefillIn, Adjustment
        public int Quantity { get; set; }

        // Employee who received/returned the cartridge
        public int? EmployeeId { get; set; }

        // NEW: Location tracking for accountability
        public int? BranchId { get; set; }
        public int? DeptId { get; set; }

        // NEW: Condition of returned cartridge
        public string ConditionType { get; set; }  // Empty, Broken, Good

        // Audit fields
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public string Remarks { get; set; }

        // Display fields (not stored in DB)
        public string ItemName { get; set; }
        public string CartridgeTypeName { get; set; }
        public string EmployeeName { get; set; }
        public string BranchName { get; set; }
        public string DepartmentName { get; set; }
        public string CreatedByName { get; set; }
    }

    /// <summary>
    /// DTO for creating a cartridge return (quantity-based, no serial numbers).
    /// </summary>
    public sealed class CartridgeReturnDto
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public int EmployeeId { get; set; }
        public int BranchId { get; set; }
        public int DeptId { get; set; }
        public string ConditionType { get; set; }  // Empty, Broken, Good
        public string Remarks { get; set; }
        public int CreatedBy { get; set; }
    }

    /// <summary>
    /// DTO for issuing cartridges.
    /// </summary>
    public sealed class CartridgeIssueDto
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public int EmployeeId { get; set; }
        public int? BranchId { get; set; }
        public int? DeptId { get; set; }
        public string Remarks { get; set; }
        public int CreatedBy { get; set; }
    }

    /// <summary>
    /// Condition types for returned cartridges.
    /// </summary>
    public static class CartridgeConditionTypes
    {
        public const string Empty = "Empty";
        public const string Broken = "Broken";
        public const string Good = "Good";

        public static readonly string[] All = { Empty, Broken, Good };
    }

    /// <summary>
    /// Movement types for cartridge tracking.
    /// </summary>
    public static class CartridgeMovementTypes
    {
        public const string StockIn = "StockIn";
        public const string Issued = "Issued";
        public const string Returned = "Returned";
        public const string RefillIn = "RefillIn";
        public const string Adjustment = "Adjustment";

        public static readonly string[] All = { StockIn, Issued, Returned, RefillIn, Adjustment };
    }
}
