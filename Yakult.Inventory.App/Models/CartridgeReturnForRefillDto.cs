using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// DTO for cartridge returns from the Request Portal.
    /// Quantity-based, no serial numbers.
    /// Data sourced from Request table where:
    /// - Description contains "[PORTAL]"
    /// - Remarks contains "With Cartridge" (indicates return for refill)
    /// </summary>
    public sealed class CartridgeReturnForRefillDto
    {
        public int ReqId { get; set; }
        public string CartridgeName { get; set; }
        public int Quantity { get; set; }
        public string RefillStatus { get; set; }  // "For Refill", "Refilling", "Refilled"
        public DateTime DateReturned { get; set; }
        public string Remarks { get; set; }

        // Employee/Location accountability
        public string EmployeeName { get; set; }
        public string BranchName { get; set; }
        public string DepartmentName { get; set; }

        // Original request status from portal
        public string PortalStatus { get; set; }
    }
}
