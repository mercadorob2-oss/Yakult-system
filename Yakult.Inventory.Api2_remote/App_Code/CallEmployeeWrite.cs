using System;
using System.Data;
using System.Data.SqlClient;

/// <summary>
/// Shared write guards for the Call IT mobile employee endpoints
/// (call-employee-create.ashx, call-employee-update-location.ashx),
/// mirroring the desktop save paths:
/// QuickAddEmployeeDialog save block (required Name/Company/Branch, blank to
/// NULL, OUTPUT INSERTED.EmpId, TitleId/DistributorId deployment-guarded via
/// COL_LENGTH) and BranchDepartmentCompanyHelper.EnsureMapping (IF NOT EXISTS
/// INSERT with SYSUTCDATETIME, dept NULL when absent). Every schema probe
/// returns false instead of throwing so missing tables/columns yield clean
/// 400/404 responses, never 500 on schema drift.
/// </summary>
public static class CallEmployeeWrite
{
    public static bool TableExists(SqlConnection con, string name)
    {
        try
        {
            using (var cmd = new SqlCommand("SELECT CASE WHEN OBJECT_ID(@N,'U') IS NOT NULL THEN 1 ELSE 0 END", con))
            { cmd.Parameters.Add("@N", SqlDbType.NVarChar).Value = name; return Convert.ToInt32(cmd.ExecuteScalar()) == 1; }
        }
        catch { return false; }
    }

    public static bool ColumnExists(SqlConnection con, string table, string column)
    {
        try
        {
            using (var cmd = new SqlCommand("SELECT CASE WHEN COL_LENGTH(@T,@C) IS NOT NULL THEN 1 ELSE 0 END", con))
            { cmd.Parameters.Add("@T", SqlDbType.NVarChar).Value = table; cmd.Parameters.Add("@C", SqlDbType.NVarChar).Value = column; return Convert.ToInt32(cmd.ExecuteScalar()) == 1; }
        }
        catch { return false; }
    }

    /// <summary>
    /// Same SQL as BranchDepartmentCompanyHelper.EnsureMapping: inserts the
    /// (Company, Branch, Dept) mapping row when missing. Dept null/zero means
    /// "no department" (NULL row). Best-effort: returns false (never throws)
    /// when the mapping table is absent or the insert fails.
    /// </summary>
    public static bool EnsureMapping(SqlConnection con, int companyId, int branchId, int? departmentId)
    {
        try
        {
            if (companyId <= 0 || branchId <= 0) return false;
            if (!TableExists(con, "dbo.BranchDepartmentCompany")) return false;
            object deptParam = (departmentId.HasValue && departmentId.Value > 0)
                ? (object)departmentId.Value
                : DBNull.Value;
            using (var cmd = new SqlCommand(@"
IF NOT EXISTS (
    SELECT 1 FROM dbo.BranchDepartmentCompany
    WHERE CompanyID = @ComId AND BranchID = @BranchId
      AND ((DepartmentID = @DeptId) OR (@DeptId IS NULL AND DepartmentID IS NULL))
)
INSERT INTO dbo.BranchDepartmentCompany (BranchID, DepartmentID, CompanyID, CreatedDate)
VALUES (@BranchId, @DeptId, @ComId, SYSUTCDATETIME());", con))
            {
                cmd.Parameters.AddWithValue("@ComId", companyId);
                cmd.Parameters.AddWithValue("@BranchId", branchId);
                cmd.Parameters.AddWithValue("@DeptId", deptParam);
                cmd.ExecuteNonQuery();
                return true;
            }
        }
        catch { return false; }
    }

    /// <summary>
    /// Reference-row existence check with an Active guard when the table has
    /// an Active column (dbo.Company, dbo.Branch, dbo.Department all do).
    /// Returns false on any schema problem so callers map it to 400.
    /// </summary>
    public static bool RefExists(SqlConnection con, string table, string keyColumn, int id)
    {
        try
        {
            if (id <= 0) return false;
            if (!TableExists(con, table)) return false;
            string sql = "SELECT CASE WHEN EXISTS(SELECT 1 FROM " + table + " WHERE " + keyColumn + "=@Id";
            if (ColumnExists(con, table, "Active")) sql += " AND ISNULL(Active,1)=1";
            sql += ") THEN 1 ELSE 0 END";
            using (var cmd = new SqlCommand(sql, con))
            { cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id; return Convert.ToInt32(cmd.ExecuteScalar()) == 1; }
        }
        catch { return false; }
    }

    /// <summary>
    /// Blank/whitespace strings become DBNull (desktop QuickAdd blank-to-NULL
    /// rule); otherwise the trimmed value. Length is NOT truncated here:
    /// callers validate lengths up front so oversize input gets a clean 400.
    /// </summary>
    public static object NullIfBlank(string value)
    {
        string t = (value ?? string.Empty).Trim();
        if (t.Length == 0) return DBNull.Value;
        return (object)t;
    }
}
