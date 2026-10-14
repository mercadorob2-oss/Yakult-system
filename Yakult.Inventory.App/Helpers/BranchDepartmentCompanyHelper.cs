using System.Data.SqlClient;

namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// Keeps dbo.BranchDepartmentCompany in sync with employee assignments.
    /// The ticket workspace only accepts Company/Branch/Dept combos that have
    /// a mapping row (otherwise it silently remaps the company and the caller
    /// refresh fails), so every employee save path for a new combo must ensure
    /// the row exists first. Pure ADO on an already-open connection — the
    /// caller owns the connection/transaction and error handling.
    /// </summary>
    public static class BranchDepartmentCompanyHelper
    {
        /// <summary>
        /// Inserts the (Company, Branch, Dept) mapping row when missing.
        /// DeptId of null/zero means "no department" (NULL row), matching how
        /// employees without a department are stored. Returns true when a
        /// usable row exists afterwards; throws on SQL errors.
        /// </summary>
        public static bool EnsureMapping(SqlConnection con, int? companyId, int? branchId, int? departmentId)
        {
            if (con == null)
                return false;
            if (!companyId.HasValue || companyId.Value <= 0)
                return false;
            if (!branchId.HasValue || branchId.Value <= 0)
                return false;

            object deptParam = (departmentId.HasValue && departmentId.Value > 0)
                ? (object)departmentId.Value
                : System.DBNull.Value;

            using (var cmd = new SqlCommand(@"
IF NOT EXISTS (
    SELECT 1 FROM dbo.BranchDepartmentCompany
    WHERE CompanyID = @ComId AND BranchID = @BranchId
      AND ((DepartmentID = @DeptId) OR (@DeptId IS NULL AND DepartmentID IS NULL))
)
INSERT INTO dbo.BranchDepartmentCompany (BranchID, DepartmentID, CompanyID, CreatedDate)
VALUES (@BranchId, @DeptId, @ComId, SYSUTCDATETIME());", con))
            {
                cmd.Parameters.AddWithValue("@ComId", companyId.Value);
                cmd.Parameters.AddWithValue("@BranchId", branchId.Value);
                cmd.Parameters.AddWithValue("@DeptId", deptParam);
                cmd.ExecuteNonQuery();
                return true;
            }
        }
    }
}
