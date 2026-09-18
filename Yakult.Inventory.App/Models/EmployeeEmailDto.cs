using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Represents the many-to-many relationship between employees and email addresses
    /// </summary>
    public class EmployeeEmailDto
    {
        public int EmpId { get; set; }
        public int EmailId { get; set; }
        public string EmailRole { get; set; } // Personal, BranchAccess, System
        public bool IsPrimary { get; set; }
        public bool IsActive { get; set; }

        // Joined fields for display
        public string EmailAddress { get; set; }
        public string DisplayName { get; set; }
        public string EmployeeName { get; set; }

        // Audit fields
        public DateTime? DateCreated { get; set; }
        public int? CreatedByUserId { get; set; }
        public DateTime? DateModified { get; set; }
        public int? ModifiedByUserId { get; set; }
    }
}
