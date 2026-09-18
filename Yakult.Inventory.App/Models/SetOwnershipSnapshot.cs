namespace Yakult.Inventory.App.Models
{
    public class SetOwnershipSnapshot
    {
        public int? ComId { get; set; }
        public string CompanyName { get; set; }
        public int? CurrentBranchId { get; set; }
        public string BranchName { get; set; }
        public int? CurrentDepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public int? CurrentEmployeeId { get; set; }
        public string EmployeeName { get; set; }
    }
}
