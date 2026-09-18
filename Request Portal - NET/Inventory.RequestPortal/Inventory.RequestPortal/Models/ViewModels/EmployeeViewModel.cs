namespace Inventory.RequestPortal.Models.ViewModels
{
    /// <summary>
    /// ViewModel for employee selection in the portal (e.g., Received By dropdown).
    /// </summary>
    public class EmployeeViewModel
    {
        public int EmpId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EmployeeNumber { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public int ComId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int DeptId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;

        /// <summary>
        /// Display text for the dropdown.
        /// Format: "John Doe (EMP-001) - IT Department"
        /// </summary>
        public string DisplayName
        {
            get
            {
                var empNum = !string.IsNullOrWhiteSpace(EmployeeNumber) ? $" ({EmployeeNumber})" : "";
                var dept   = !string.IsNullOrWhiteSpace(DepartmentName) ? $" - {DepartmentName}" : "";
                return $"{Name}{empNum}{dept}";
            }
        }
    }
}
