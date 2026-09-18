using System;

namespace Yakult.Inventory.App.Models
{
    public class ItemAuditTrailDto
    {
        public int Id { get; set; }
        public int? ItemId { get; set; }
        public string SerialNumber { get; set; }

        public string Action { get; set; }
        public DateTime ActionTime { get; set; }

        public int? EmployeeId { get; set; }
        public string EmployeeName { get; set; }

        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; }

        public int? BranchId { get; set; }
        public string BranchName { get; set; }

        public string Direction { get; set; }
        public string Status { get; set; }
        public string Location { get; set; }

        public string ReferenceType { get; set; }
        public int? ReferenceId { get; set; }
        public string SetCode { get; set; }

        public string Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
    }
}
