using System;
using System.Data;
using System.Data.SqlClient;
using System.Net.Mail;

/// <summary>
/// Shared contact-email resolution for the Call IT mobile API, mirroring
/// CallMonitoringRepository.ResolveTicketContactEmailAsync (personal email,
/// then DepartmentAccount branch email, then legacy Branch email, then
/// DepartmentEmail) and SaveEmployeeEmailBindingAsync (link final contact
/// email back to the caller employee). Every table/column is guarded with
/// OBJECT_ID/COL_LENGTH so missing schema yields "" instead of errors, and
/// the link path is best-effort and never throws.
/// </summary>
public static class CallTicketContactEmail
{
    public static bool TryResolve(SqlConnection con, int? callerEmpId, int? comId, int? deptId, int? branchId, out string email, out string source)
    {
        email = "";
        source = "";
        try
        {
            if (callerEmpId.HasValue && callerEmpId.Value <= 0) callerEmpId = null;
            if (comId.HasValue && comId.Value <= 0) comId = null;
            if (deptId.HasValue && deptId.Value <= 0) deptId = null;
            if (branchId.HasValue && branchId.Value <= 0) branchId = null;

            // 1) Employee primary email.
            if (callerEmpId.HasValue && ObjExists(con, "dbo.EmployeeEmail") && ObjExists(con, "dbo.EmailAddress"))
            {
                const string sql = @"
SELECT TOP 1 ea.EmailAddress
FROM dbo.EmployeeEmail ee
INNER JOIN dbo.EmailAddress ea ON ea.EmailId = ee.EmailId
WHERE ee.EmpId = @EmpId
  AND ee.IsPrimary = 1
  AND ee.IsActive = 1
  AND ea.IsActive = 1
ORDER BY ee.EmployeeEmailId;";
                string v = Scalar(con, sql, cmd => cmd.Parameters.Add("@EmpId", SqlDbType.Int).Value = callerEmpId.Value);
                string n;
                if (TryNormalize(v, out n)) { email = n; source = "Employee personal email"; return true; }
            }

            // 2) DepartmentAccount branch email (dept-specific when deptId set,
            // else branch-level preferring a blank DepartmentName).
            if (branchId.HasValue && ObjExists(con, "dbo.DepartmentAccount") && ObjExists(con, "dbo.EmailAddress")
                && ColExists(con, "dbo.DepartmentAccount", "EmailAddressId")
                && ColExists(con, "dbo.DepartmentAccount", "CompanyName")
                && ColExists(con, "dbo.DepartmentAccount", "DepartmentName")
                && ColExists(con, "dbo.DepartmentAccount", "BranchName"))
            {
                if (deptId.HasValue && ObjExists(con, "dbo.Company") && ObjExists(con, "dbo.Department") && ObjExists(con, "dbo.Branch"))
                {
                    const string sql = @"
SELECT TOP 1 ea.EmailAddress
FROM dbo.DepartmentAccount da
INNER JOIN dbo.EmailAddress ea ON ea.EmailId = da.EmailAddressId
INNER JOIN dbo.Company c ON c.ComId = @ComId
INNER JOIN dbo.Department d ON d.DeptId = @DeptId
INNER JOIN dbo.Branch b ON b.BranchId = @BranchId
WHERE da.CompanyName = c.Name
  AND da.DepartmentName = d.Name
  AND da.BranchName = b.Name
  AND ea.IsActive = 1;";
                    string v = Scalar(con, sql, cmd =>
                    {
                        cmd.Parameters.Add("@ComId", SqlDbType.Int).Value = comId == null ? (object)DBNull.Value : (object)comId.Value;
                        cmd.Parameters.Add("@DeptId", SqlDbType.Int).Value = deptId == null ? (object)DBNull.Value : (object)deptId.Value;
                        cmd.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId == null ? (object)DBNull.Value : (object)branchId.Value;
                    });
                    string n;
                    if (TryNormalize(v, out n)) { email = n; source = "Branch email"; return true; }
                }
                else if (!deptId.HasValue && ObjExists(con, "dbo.Company") && ObjExists(con, "dbo.Branch"))
                {
                    const string sql = @"
SELECT TOP 1 ea.EmailAddress
FROM dbo.DepartmentAccount da
INNER JOIN dbo.EmailAddress ea ON ea.EmailId = da.EmailAddressId
INNER JOIN dbo.Company c ON c.ComId = @ComId
INNER JOIN dbo.Branch b ON b.BranchId = @BranchId
WHERE da.CompanyName = c.Name
  AND da.BranchName = b.Name
  AND ea.IsActive = 1
ORDER BY CASE WHEN NULLIF(LTRIM(RTRIM(da.DepartmentName)), '') IS NULL THEN 0 ELSE 1 END, da.EmailAddressId;";
                    string v = Scalar(con, sql, cmd =>
                    {
                        cmd.Parameters.Add("@ComId", SqlDbType.Int).Value = comId == null ? (object)DBNull.Value : (object)comId.Value;
                        cmd.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId == null ? (object)DBNull.Value : (object)branchId.Value;
                    });
                    string n;
                    if (TryNormalize(v, out n)) { email = n; source = "Branch email"; return true; }
                }
            }

            // 3) Legacy Branch.EmailId email.
            if (branchId.HasValue && ObjExists(con, "dbo.Branch") && ObjExists(con, "dbo.EmailAddress")
                && ColExists(con, "dbo.Branch", "EmailId"))
            {
                const string sql = @"
SELECT TOP 1 ea.EmailAddress
FROM dbo.Branch b
INNER JOIN dbo.EmailAddress ea ON ea.EmailId = b.EmailId
WHERE b.BranchId = @BranchId
  AND ISNULL(b.Active, 1) = 1
  AND ea.IsActive = 1;";
                string v = Scalar(con, sql, cmd => cmd.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId.Value);
                string n;
                if (TryNormalize(v, out n)) { email = n; source = "Branch email"; return true; }
            }

            // 4) DepartmentEmail.
            if (comId.HasValue && deptId.HasValue && ObjExists(con, "dbo.DepartmentEmail") && ObjExists(con, "dbo.EmailAddress")
                && ColExists(con, "dbo.DepartmentEmail", "EmailAddressId")
                && ColExists(con, "dbo.DepartmentEmail", "CompanyName")
                && ColExists(con, "dbo.DepartmentEmail", "DepartmentName")
                && ObjExists(con, "dbo.Company") && ObjExists(con, "dbo.Department"))
            {
                const string sql = @"
SELECT TOP 1 ea.EmailAddress
FROM dbo.DepartmentEmail de
INNER JOIN dbo.EmailAddress ea ON ea.EmailId = de.EmailAddressId
INNER JOIN dbo.Company c ON c.ComId = @ComId
INNER JOIN dbo.Department d ON d.DeptId = @DeptId
WHERE de.CompanyName = c.Name
  AND de.DepartmentName = d.Name
  AND ea.IsActive = 1;";
                string v = Scalar(con, sql, cmd =>
                {
                    cmd.Parameters.Add("@ComId", SqlDbType.Int).Value = comId.Value;
                    cmd.Parameters.Add("@DeptId", SqlDbType.Int).Value = deptId.Value;
                });
                string n;
                if (TryNormalize(v, out n)) { email = n; source = "Department email"; return true; }
            }
        }
        catch { }
        email = "";
        source = "";
        return false;
    }

    /// <summary>
    /// Best-effort mirror of SaveEmployeeEmailBindingAsync (non-empty branch):
    /// upserts dbo.EmailAddress, clears IsPrimary for the employee, then upserts
    /// dbo.EmployeeEmail (EmpId,EmailId,'Work',1,1). Never throws.
    /// </summary>
    public static void TryLinkEmployeeEmail(string connectionString, int empId, string email, int? changedByUserId)
    {
        try
        {
            if (empId <= 0) return;
            string normalized = (email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized)) return;
            string check;
            if (!TryNormalize(normalized, out check)) return;
            normalized = check;
            using (var con = new SqlConnection(connectionString))
            {
                con.Open();
                if (!ObjExists(con, "dbo.EmployeeEmail") || !ObjExists(con, "dbo.EmailAddress")) return;
                if (!ColExists(con, "dbo.EmailAddress", "EmailAddress") || !ColExists(con, "dbo.EmailAddress", "EmailId")) return;
                if (!ColExists(con, "dbo.EmployeeEmail", "EmpId") || !ColExists(con, "dbo.EmployeeEmail", "EmailId")
                    || !ColExists(con, "dbo.EmployeeEmail", "IsPrimary") || !ColExists(con, "dbo.EmployeeEmail", "IsActive")) return;
                bool eaHasDisplay = ColExists(con, "dbo.EmailAddress", "DisplayName");
                bool eaHasActive = ColExists(con, "dbo.EmailAddress", "IsActive");
                bool eaHasBy = ColExists(con, "dbo.EmailAddress", "CreatedByUserId");
                bool eeHasRole = ColExists(con, "dbo.EmployeeEmail", "EmailRole");
                bool eeHasBy = ColExists(con, "dbo.EmployeeEmail", "CreatedByUserId");
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        int emailId;
                        if (eaHasDisplay && eaHasActive && eaHasBy)
                        {
                            using (var upsert = new SqlCommand(@"
UPDATE dbo.EmailAddress
SET IsActive = 1, DisplayName = COALESCE(NULLIF(DisplayName, ''), @Email)
WHERE EmailAddress = @Email;

IF NOT EXISTS (SELECT 1 FROM dbo.EmailAddress WHERE EmailAddress = @Email)
    INSERT dbo.EmailAddress (EmailAddress, DisplayName, IsActive, CreatedByUserId)
    VALUES (@Email, @Email, 1, @ChangedByUserId);

SELECT EmailId FROM dbo.EmailAddress WHERE EmailAddress = @Email;", con, tx))
                            {
                                upsert.Parameters.Add("@Email", SqlDbType.NVarChar, 255).Value = normalized;
                                upsert.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = changedByUserId == null ? (object)DBNull.Value : (object)changedByUserId.Value;
                                emailId = Convert.ToInt32(upsert.ExecuteScalar());
                            }
                        }
                        else
                        {
                            using (var upsert = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM dbo.EmailAddress WHERE EmailAddress = @Email)
    INSERT dbo.EmailAddress (EmailAddress) VALUES (@Email);

SELECT EmailId FROM dbo.EmailAddress WHERE EmailAddress = @Email;", con, tx))
                            {
                                upsert.Parameters.Add("@Email", SqlDbType.NVarChar, 255).Value = normalized;
                                emailId = Convert.ToInt32(upsert.ExecuteScalar());
                            }
                        }

                        using (var clear = new SqlCommand("UPDATE dbo.EmployeeEmail SET IsPrimary = 0 WHERE EmpId = @EmpId;", con, tx))
                        {
                            clear.Parameters.Add("@EmpId", SqlDbType.Int).Value = empId;
                            clear.ExecuteNonQuery();
                        }

                        string saveSql;
                        if (eeHasRole && eeHasBy)
                            saveSql = @"
IF EXISTS (SELECT 1 FROM dbo.EmployeeEmail WHERE EmpId = @EmpId AND EmailId = @EmailId)
    UPDATE dbo.EmployeeEmail
    SET IsPrimary = 1, IsActive = 1, CreatedByUserId = COALESCE(@ChangedByUserId, CreatedByUserId)
    WHERE EmpId = @EmpId AND EmailId = @EmailId;
ELSE
    INSERT dbo.EmployeeEmail (EmpId, EmailId, EmailRole, IsPrimary, IsActive, CreatedByUserId)
    VALUES (@EmpId, @EmailId, 'Work', 1, 1, @ChangedByUserId);";
                        else if (eeHasRole)
                            saveSql = @"
IF EXISTS (SELECT 1 FROM dbo.EmployeeEmail WHERE EmpId = @EmpId AND EmailId = @EmailId)
    UPDATE dbo.EmployeeEmail
    SET IsPrimary = 1, IsActive = 1
    WHERE EmpId = @EmpId AND EmailId = @EmailId;
ELSE
    INSERT dbo.EmployeeEmail (EmpId, EmailId, EmailRole, IsPrimary, IsActive)
    VALUES (@EmpId, @EmailId, 'Work', 1, 1);";
                        else if (eeHasBy)
                            saveSql = @"
IF EXISTS (SELECT 1 FROM dbo.EmployeeEmail WHERE EmpId = @EmpId AND EmailId = @EmailId)
    UPDATE dbo.EmployeeEmail
    SET IsPrimary = 1, IsActive = 1, CreatedByUserId = COALESCE(@ChangedByUserId, CreatedByUserId)
    WHERE EmpId = @EmpId AND EmailId = @EmailId;
ELSE
    INSERT dbo.EmployeeEmail (EmpId, EmailId, IsPrimary, IsActive, CreatedByUserId)
    VALUES (@EmpId, @EmailId, 1, 1, @ChangedByUserId);";
                        else
                            saveSql = @"
IF EXISTS (SELECT 1 FROM dbo.EmployeeEmail WHERE EmpId = @EmpId AND EmailId = @EmailId)
    UPDATE dbo.EmployeeEmail
    SET IsPrimary = 1, IsActive = 1
    WHERE EmpId = @EmpId AND EmailId = @EmailId;
ELSE
    INSERT dbo.EmployeeEmail (EmpId, EmailId, IsPrimary, IsActive)
    VALUES (@EmpId, @EmailId, 1, 1);";
                        using (var save = new SqlCommand(saveSql, con, tx))
                        {
                            save.Parameters.Add("@EmpId", SqlDbType.Int).Value = empId;
                            save.Parameters.Add("@EmailId", SqlDbType.Int).Value = emailId;
                            if (eeHasBy) save.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = changedByUserId == null ? (object)DBNull.Value : (object)changedByUserId.Value;
                            save.ExecuteNonQuery();
                        }

                        tx.Commit();
                    }
                    catch { try { tx.Rollback(); } catch { } }
                }
            }
        }
        catch { }
    }

    static string Scalar(SqlConnection con, string sql, Action<SqlCommand> addParams)
    {
        try
        {
            using (var cmd = new SqlCommand(sql, con))
            {
                if (addParams != null) addParams(cmd);
                object v = cmd.ExecuteScalar();
                return v == null || v == DBNull.Value ? null : v.ToString();
            }
        }
        catch { return null; }
    }

    static bool TryNormalize(string value, out string normalized)
    {
        normalized = null;
        string candidate = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        try
        {
            var a = new MailAddress(candidate);
            if (!string.Equals(a.Address, candidate, StringComparison.OrdinalIgnoreCase)) return false;
            normalized = a.Address;
            return true;
        }
        catch (FormatException) { return false; }
    }

    static bool ObjExists(SqlConnection con, string name)
    {
        try
        {
            using (var cmd = new SqlCommand("SELECT CASE WHEN OBJECT_ID(@N) IS NOT NULL THEN 1 ELSE 0 END", con))
            { cmd.Parameters.Add("@N", SqlDbType.NVarChar).Value = name; return Convert.ToInt32(cmd.ExecuteScalar()) == 1; }
        }
        catch { return false; }
    }

    static bool ColExists(SqlConnection con, string table, string column)
    {
        try
        {
            using (var cmd = new SqlCommand("SELECT CASE WHEN COL_LENGTH(@T,@C) IS NOT NULL THEN 1 ELSE 0 END", con))
            { cmd.Parameters.Add("@T", SqlDbType.NVarChar).Value = table; cmd.Parameters.Add("@C", SqlDbType.NVarChar).Value = column; return Convert.ToInt32(cmd.ExecuteScalar()) == 1; }
        }
        catch { return false; }
    }
}
