using System;
using System.Data.SqlClient;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Tests
{
    /// <summary>
    /// Executes the REAL BranchDepartmentCompanyHelper.EnsureMapping against DEV:
    /// temp combo (19,129,NULL) must be inserted once, be a no-op twice, then is
    /// cleaned up. Loose file, not in csproj.
    /// Compile: csc BdcEnsureCheck.cs ..\Helpers\BranchDepartmentCompanyHelper.cs
    /// </summary>
    public static class BdcEnsureCheck
    {
        private const string Cs = "Server=192.168.100.186,50301;Database=Yakult_Inventory_System_DEV;User ID=remote_user;Password=Yakult-ITD;TrustServerCertificate=True;Encrypt=False;";

        private static int Count(SqlConnection con)
        {
            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.BranchDepartmentCompany WHERE CompanyID=19 AND BranchID=129 AND DepartmentID IS NULL", con))
                return (int)cmd.ExecuteScalar();
        }

        public static int Main()
        {
            int failures = 0;
            try
            {
                using (var con = new SqlConnection(Cs))
                {
                    con.Open();
                    // Clean slate (in case a prior run died mid-test).
                    using (var cmd = new SqlCommand("DELETE FROM dbo.BranchDepartmentCompany WHERE CompanyID=19 AND BranchID=129 AND DepartmentID IS NULL", con))
                        cmd.ExecuteNonQuery();

                    bool first = BranchDepartmentCompanyHelper.EnsureMapping(con, 19, 129, null);
                    int afterFirst = Count(con);
                    bool second = BranchDepartmentCompanyHelper.EnsureMapping(con, 19, 129, 0);
                    int afterSecond = Count(con);

                    Console.WriteLine("  first call returned " + first + ", rows=" + afterFirst);
                    Console.WriteLine("  second call returned " + second + ", rows=" + afterSecond);
                    if (!first || afterFirst != 1) { Console.WriteLine("  FAIL: insert path"); failures++; }
                    else Console.WriteLine("  PASS: insert path");
                    if (!second || afterSecond != 1) { Console.WriteLine("  FAIL: no-op path"); failures++; }
                    else Console.WriteLine("  PASS: no-op path");

                    using (var cmd = new SqlCommand("DELETE FROM dbo.BranchDepartmentCompany WHERE CompanyID=19 AND BranchID=129 AND DepartmentID IS NULL", con))
                        cmd.ExecuteNonQuery();
                    int afterCleanup = Count(con);
                    if (afterCleanup != 0) { Console.WriteLine("  FAIL: cleanup"); failures++; }
                    else Console.WriteLine("  PASS: cleanup (DEV left unchanged)");
                }
            }
            catch (Exception ex) { Console.WriteLine("  FAIL: exception: " + ex.Message); failures++; }
            Console.WriteLine(failures == 0 ? "ALL PASS" : failures + " FAILURES");
            return failures == 0 ? 0 : 1;
        }
    }
}
