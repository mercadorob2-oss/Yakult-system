using System;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Tests
{
    /// <summary>
    /// Renders the exact INSERT text of each Distributor-guarded call site in
    /// both modes and fails on trailing/double commas. Loose file, not in csproj.
    /// Compile: csc SqlRenderCheck.cs ..\Helpers\EmployeeDistributorHelper.cs
    /// </summary>
    public static class SqlRenderCheck
    {
        private static int _failures;

        private static void Check(bool condition, string name, string sql)
        {
            if (condition) { Console.WriteLine("  PASS: " + name); return; }
            Console.WriteLine("  FAIL: " + name);
            Console.WriteLine("  SQL was: " + sql.Replace("\r", "").Replace("\n", "\\n"));
            _failures++;
        }

        private static void CheckSite(string name, string columns, string values)
        {
            string col = string.Join(" ", columns.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            string val = string.Join(" ", values.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            bool ok = !col.Contains(",)") && !col.Contains(", ,") && !col.Contains(",,")
                   && !val.Contains(",)") && !val.Contains(", ,") && !val.Contains(",,");
            Check(ok, name, "COLS(" + col + ") VALS(" + val + ")");
        }

        public static int Main()
        {
            Console.WriteLine("== Insert SQL rendering (both modes) ==");
            foreach (bool has in new[] { true, false })
            {
                string mode = has ? "ON" : "OFF";
                var ins = EmployeeDistributorHelper.BuildDistributorInsertFragments(has);

                // QuickAddEmployeeDialog.cs (must match its concatenation exactly)
                CheckSite("quickadd-" + mode,
                    "INSERT INTO dbo.Employee (Name, EmployeeNumber, Position, Description, DateCreated, Createdby, ComId, BranchId, DeptId, TitleId" + ins.ColumnFragment + ")",
                    "VALUES (@Name, @EmployeeNumber, @Position, @Description, @DateCreated, @Createdby, @ComId, @BranchId, @DeptId, @TitleId" + ins.ValueFragment + ")");

                // EmployeeRepository.cs (must match its concatenation exactly)
                CheckSite("repository-" + mode,
                    "INSERT INTO dbo.Employee (Name, Description, DateCreated, Createdby, ComId, BranchId, DeptId, Position, EmployeeNumber" + ins.ColumnFragment + ")",
                    "VALUES (@Name, @Description, @DateCreated, @Createdby, @ComId, @BranchId, @DeptId, @Position, @EmployeeNumber" + ins.ValueFragment + ")");

                // EmployeePageViewModel.cs SaveEmployee (must match its concatenation exactly)
                CheckSite("viewmodel-" + mode,
                    "INSERT INTO dbo.Employee (Name, Position, Description, DateCreated, Createdby, ComId, DeptId, BranchId" + ins.ColumnFragment + ", EmployeeNumber, TitleId)",
                    "VALUES (@Name, @Position, @Description, @DateCreated, @CreatedBy, @ComId, @DeptId, @BranchId" + ins.ValueFragment + ", @EmployeeNumber, @TitleId)");
            }

            Console.WriteLine(_failures == 0 ? "ALL PASS" : _failures + " FAILURES");
            return _failures == 0 ? 0 : 1;
        }
    }
}
