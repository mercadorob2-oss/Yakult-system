using System.Collections.Generic;

using System;

namespace Yakult.Inventory.App.Models.BorrowItems
{
    public sealed class BorrowEmployeeLookup
    {
        public int EmpId { get; set; }
        public string EmployeeName { get; set; }

        public int ComId { get; set; }
        public string CompanyName { get; set; }

        public int DeptId { get; set; }
        public string DepartmentName { get; set; }

        public int BranchId { get; set; }
        public string BranchName { get; set; }

        public string DisplayText
        {
            get
            {
                var name = (EmployeeName ?? string.Empty).Trim();
                var dept = (DepartmentName ?? string.Empty).Trim();
                var branch = (BranchName ?? string.Empty).Trim();
                if (name.Length == 0)
                    name = $"EmpId {EmpId}";

                var scope = new List<string>();
                if (dept.Length > 0)
                    scope.Add(dept);
                if (branch.Length > 0)
                    scope.Add(branch);

                return scope.Count == 0
                    ? name
                    : $"{name} - {string.Join(" / ", scope)}";
            }
        }

        public override string ToString() => DisplayText ?? base.ToString();
    }
}
