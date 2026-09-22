using System;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Tests
{
    /// <summary>
    /// Standalone assertions for EmployeeDistributorHelper (pure SQL-fragment builders).
    /// Compile + run WITHOUT the app: csc EmployeeDistributorHelperTest.cs ..\Helpers\EmployeeDistributorHelper.cs
    /// Exit code 0 = all pass, 1 = failure. Not referenced by the app csproj (loose file, like the other Tests/).
    /// </summary>
    public static class EmployeeDistributorHelperTest
    {
        private static int _failures;

        private static void Check(bool condition, string name)
        {
            if (condition) { Console.WriteLine("  PASS: " + name); return; }
            Console.WriteLine("  FAIL: " + name);
            _failures++;
        }

        public static int Main()
        {
            Console.WriteLine("== EmployeeDistributorHelper ==");

            string selOn = EmployeeDistributorHelper.BuildDistributorSelectClause(true);
            Check(selOn.Contains("dist.DistributorId"), "select(true) references dist.DistributorId");
            Check(selOn.Contains("DistributorName"), "select(true) exposes DistributorName");

            string selOff = EmployeeDistributorHelper.BuildDistributorSelectClause(false);
            Check(selOff.Contains("CAST(NULL"), "select(false) degrades to CAST(NULL...)");
            Check(!selOff.Contains("dbo.Distributor"), "select(false) touches no Distributor table");

            string joinOn = EmployeeDistributorHelper.BuildDistributorJoinClause(true);
            Check(joinOn.Contains("LEFT JOIN dbo.Distributor dist"), "join(true) left-joins Distributor");
            Check(joinOn.Contains("e.DistributorId"), "join(true) keys on e.DistributorId");
            Check(EmployeeDistributorHelper.BuildDistributorJoinClause(false) == string.Empty, "join(false) is empty");

            var insOn = EmployeeDistributorHelper.BuildDistributorInsertFragments(true);
            Check(insOn.ColumnFragment.Contains("DistributorId"), "insert(true) column has DistributorId");
            Check(insOn.ValueFragment.Contains("@DistributorId"), "insert(true) value has @DistributorId");
            var insOff = EmployeeDistributorHelper.BuildDistributorInsertFragments(false);
            Check(insOff.ColumnFragment == string.Empty && insOff.ValueFragment == string.Empty, "insert(false) is empty");

            Check(EmployeeDistributorHelper.BuildDistributorUpdateSetClause(true).Contains("DistributorId = @DistributorId"),
                "update(true) sets DistributorId = @DistributorId");
            Check(EmployeeDistributorHelper.BuildDistributorUpdateSetClause(false) == string.Empty, "update(false) is empty");

            Check(EmployeeDistributorHelper.NormalizeDistributorId(0) == null, "normalize(0/None) is null");
            Check(EmployeeDistributorHelper.NormalizeDistributorId(-1) == null, "normalize(negative) is null");
            Check(EmployeeDistributorHelper.NormalizeDistributorId(null) == null, "normalize(null) is null");
            Check(EmployeeDistributorHelper.NormalizeDistributorId(27) == 27, "normalize(27) stays 27");

            Console.WriteLine(_failures == 0 ? "ALL PASS" : _failures + " FAILURES");
            return _failures == 0 ? 0 : 1;
        }
    }
}
