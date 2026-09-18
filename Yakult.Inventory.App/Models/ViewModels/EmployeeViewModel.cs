using System;

namespace Yakult.Inventory.App.Models.ViewModels
{
    /// <summary>
    /// ViewModel for employee selection in Request Portal.
    /// Used when IT staff creates requests on behalf of employees.
    /// </summary>
    public class EmployeeViewModel
    {
        /// <summary>
        /// Employee ID (primary key).
        /// </summary>
        public int EmpId { get; set; }

        /// <summary>
        /// Employee name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Employee number (unique identifier).
        /// </summary>
        public string EmployeeNumber { get; set; }

        /// <summary>
        /// Employee position/title.
        /// </summary>
        public string Position { get; set; }

        /// <summary>
        /// Company ID where employee works.
        /// </summary>
        public int ComId { get; set; }

        /// <summary>
        /// Company name.
        /// </summary>
        public string CompanyName { get; set; }

        /// <summary>
        /// Branch ID where employee is assigned.
        /// </summary>
        public int BranchId { get; set; }

        /// <summary>
        /// Branch name.
        /// </summary>
        public string BranchName { get; set; }

        /// <summary>
        /// Department ID where employee works.
        /// </summary>
        public int DeptId { get; set; }

        /// <summary>
        /// Department name.
        /// </summary>
        public string DepartmentName { get; set; }

        /// <summary>
        /// Whether employee is active.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Display name for dropdown selection.
        /// Format: "John Doe (EMP-001) - IT Department"
        /// </summary>
        public string DisplayName
        {
            get
            {
                var empNumber = !string.IsNullOrWhiteSpace(EmployeeNumber) ? $" ({EmployeeNumber})" : "";
                var dept = !string.IsNullOrWhiteSpace(DepartmentName) ? $" - {DepartmentName}" : "";
                return $"{Name}{empNumber}{dept}";
            }
        }

        /// <summary>
        /// Short display name for space-constrained views.
        /// Format: "John Doe (EMP-001)"
        /// </summary>
        public string ShortDisplayName
        {
            get
            {
                var empNumber = !string.IsNullOrWhiteSpace(EmployeeNumber) ? $" ({EmployeeNumber})" : "";
                return $"{Name}{empNumber}";
            }
        }

        /// <summary>
        /// Full location string for display.
        /// Format: "Manila Branch, IT Department"
        /// </summary>
        public string LocationString
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(BranchName)) parts.Add(BranchName);
                if (!string.IsNullOrWhiteSpace(DepartmentName)) parts.Add(DepartmentName);
                return string.Join(", ", parts);
            }
        }

        public override string ToString() => DisplayName;
    }
}
