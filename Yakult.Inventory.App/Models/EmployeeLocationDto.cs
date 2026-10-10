using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Models
{
    public sealed class EmployeeLocationDto
    {
        public int EmpId { get; set; }
        public string Name { get; set; }
        public string EmployeeNumber { get; set; }
        public int CompanyId { get; set; }
        public int? DepartmentId { get; set; }
        public int BranchId { get; set; }
        public string CompanyName { get; set; }
        public string DepartmentName { get; set; }
        public string BranchName { get; set; }
        public bool Active { get; set; }
        public byte[] RowVersion { get; set; }
        public string LocationDisplay => $"{CompanyName ?? "Unknown company"} / {DepartmentName ?? "No department"} / {BranchName ?? "Unknown branch"}";
    }

    public sealed class EmployeeLocationUpdate
    {
        public int EmpId { get; set; }
        public int CompanyId { get; set; }
        public int? DepartmentId { get; set; }
        public int BranchId { get; set; }
        public byte[] ExpectedRowVersion { get; set; }
        public string Reason { get; set; }
    }

    public sealed class EmployeeLocationMapping
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; }
    }

    public sealed class EmployeeLocationOptions
    {
        public List<LookupItem> Companies { get; set; } = new List<LookupItem>();
        public List<EmployeeLocationMapping> Mappings { get; set; } = new List<EmployeeLocationMapping>();
    }

    public sealed class EmployeeLocationConflictException : InvalidOperationException
    {
        public EmployeeLocationDto Latest { get; }
        public EmployeeLocationConflictException(EmployeeLocationDto latest)
            : base("This employee was changed by another user. Review the latest assignment before saving again.")
        {
            Latest = latest;
        }
    }
}
