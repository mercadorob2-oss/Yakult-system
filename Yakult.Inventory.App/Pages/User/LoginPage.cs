using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Data;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Security;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.User
{
    public partial class LoginPage : Form
    {
        private readonly UserRepository _userRepo;
        private bool _passwordVisible = false;
        private bool _isLoggingIn = false;

        public LoginPage(UserRepository userRepo)
        {
            InitializeComponent();
            _userRepo = userRepo;

            // Enable form-level key preview for shortcuts
            this.KeyPreview = true;

            // Mask the password input
            PassLoginField.PasswordChar = '*';

            // ✅ Eye icon (Label-based, no clipping)
            lblTogglePassword.Font = new Font("Segoe MDL2 Assets", 14F);
            lblTogglePassword.Text = "\uE890"; // eye open
            lblTogglePassword.Cursor = Cursors.Hand;

            // Events
            lblTogglePassword.Click += lblTogglePassword_Click;
            lblTogglePassword.MouseEnter += lblTogglePassword_MouseEnter;
            lblTogglePassword.MouseLeave += lblTogglePassword_MouseLeave;

            // Enter key support
            NameLoginField.KeyDown += NameLoginField_KeyDown;
            PassLoginField.KeyDown += PassLoginField_KeyDown;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // TEMPORARY: Ctrl+Shift+D opens the DB switcher from the login screen too.
            // Normally suppressed here because no session exists yet to verify
            // IsDeveloper + IsSuperAdmin — revert to returning true once done testing.
            if (keyData == (Keys.Control | Keys.Shift | Keys.D))
            {
                using (var form = new Pages.Admin.DBConn.DatabaseSetupForm())
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        Application.Restart();
                        Environment.Exit(0);
                    }
                }
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void NameLoginField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                PassLoginField.Focus();
            }
        }

        private void PassLoginField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                LoginBtn_Click(sender, e);
            }
        }

        // 👁 Toggle password visibility
        private void lblTogglePassword_Click(object sender, EventArgs e)
        {
            _passwordVisible = !_passwordVisible;

            if (_passwordVisible)
            {
                PassLoginField.PasswordChar = '\0';
                lblTogglePassword.Text = "\uE890"; // eye closed
            }
            else
            {
                PassLoginField.PasswordChar = '*';
                lblTogglePassword.Text = "\uE890"; // eye open
            }
        }

        // Optional hover effect
        private void lblTogglePassword_MouseEnter(object sender, EventArgs e)
        {
            lblTogglePassword.ForeColor = Color.FromArgb(78, 154, 252);
        }

        private void lblTogglePassword_MouseLeave(object sender, EventArgs e)
        {
            lblTogglePassword.ForeColor = Color.Gray;
        }

        private async void LoginBtn_Click(object sender, EventArgs e)
        {
            if (_isLoggingIn) return;

            string name = NameLoginField.Text.Trim();
            string password = PassLoginField.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show(
                    "Please enter both name and password.",
                    "Login Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }

            _isLoggingIn = true;
            LoginBtn.Enabled = false;
            NameLoginField.Enabled = false;
            PassLoginField.Enabled = false;

            try
            {
                // Try normal user account first
                var (userId, fullName, emailAddr, isDeveloper, isSuperAdmin) =
                    await _userRepo.AuthenticateByName_VarBinaryConvertAsync(name, password);

                if (userId > 0)
                {
                    AppSession.CurrentUserId = userId;
                    AppSession.CurrentUserName = fullName;
                    AppSession.CurrentEmail = emailAddr;
                    AppSession.LoginTime = DateTime.Now;
                    AppSession.IsDeveloper = isDeveloper;
                    AppSession.IsSuperAdmin = isSuperAdmin;

                    // Load roles, employee data, dept account check, and portal access in parallel
                    await System.Threading.Tasks.Task.WhenAll(
                        LoadUserRolesAsync(userId),
                        LoadEmployeeDataAsync(userId),
                        LoadDepartmentAccountDataAsync(userId),
                        PermissionResolver.LoadAsync()
                    );

                    // Load per-user notification preference
                    AppSession.NotificationsEnabled =
                        new UserNotificationSettingRepository().GetNotificationsEnabled(userId);

                    MessageBox.Show(
                        $"Welcome, {fullName}!",
                        "Login Successful",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );

                    this.DialogResult = DialogResult.OK;
                    this.Close();
                    return;
                }

                // Fallback A: dept account with UserId already linked (created via DepartmentAccountsPage)
                // Password stored as SHA256 hash/salt in dbo.[User] — CONVERT VARBINARY check above fails for these.
                if (await TryDeptAccountHashLoginAsync(name, password))
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                    return;
                }

                // Fallback B: legacy dept account (UserId IS NULL — migrate on first login)
                if (await MigrateLegacyDeptAccountAsync(name, password))
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                    return;
                }

                MessageBox.Show(
                    "Invalid name or password.",
                    "Login Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _isLoggingIn = false;
                LoginBtn.Enabled = true;
                NameLoginField.Enabled = true;
                PassLoginField.Enabled = true;
            }
        }

        /// <summary>
        /// Handles dept accounts that already have a UserId linked (created via DepartmentAccountsPage).
        /// Their dbo.[User] row stores PasswordHash/PasswordSalt (SHA256), not the CONVERT VARBINARY password.
        /// Verifies the SHA256 hash and sets the session directly — no migration needed.
        /// </summary>
        private async System.Threading.Tasks.Task<bool> TryDeptAccountHashLoginAsync(string username, string password)
        {
            try
            {
                string cs = DatabaseConfig.ConnectionString;
                if (string.IsNullOrEmpty(cs)) return false;

                using (var con = new SqlConnection(cs))
                {
                    await con.OpenAsync();

                    // Find dept account with an existing linked User row
                    const string sql = @"
                        SELECT
                            da.Id,
                            da.IsActive,
                            u.UserId,
                            u.PasswordHash,
                            u.PasswordSalt,
                            c.ComId,
                            d.DeptId,
                            b.BranchId
                        FROM dbo.DepartmentAccount da
                        INNER JOIN dbo.[User] u ON u.UserId = da.UserId
                        LEFT JOIN dbo.Company    c ON (da.ComId IS NOT NULL AND c.ComId = da.ComId) OR (da.ComId IS NULL AND c.Name = da.CompanyName)
                        LEFT JOIN dbo.Department d ON (da.DeptId IS NOT NULL AND d.DeptId = da.DeptId) OR (da.DeptId IS NULL AND d.Name = da.DepartmentName)
                        LEFT JOIN dbo.Branch     b ON (da.BranchId IS NOT NULL AND b.BranchId = da.BranchId) OR (da.BranchId IS NULL AND b.Name = da.BranchName)
                        WHERE da.Username COLLATE Latin1_General_CS_AS = @Username COLLATE Latin1_General_CS_AS
                          AND da.UserId IS NOT NULL";

                    int accountId, userId;
                    byte[] storedHash, storedSalt;
                    bool isActive;
                    int? companyId, deptId, branchId;

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@Username", username);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync()) return false;

                            accountId  = reader.GetInt32(0);
                            isActive   = !reader.IsDBNull(1) && reader.GetBoolean(1);
                            userId     = reader.GetInt32(2);
                            storedHash = reader.IsDBNull(3) ? null : (byte[])reader.GetValue(3);
                            storedSalt = reader.IsDBNull(4) ? null : (byte[])reader.GetValue(4);
                            companyId  = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
                            deptId     = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6);
                            branchId   = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7);
                        }
                    }

                    if (!isActive || storedHash == null || storedSalt == null) return false;
                    if (!Yakult.Inventory.App.Helpers.PasswordHelper.VerifyPassword(password, storedHash, storedSalt)) return false;

                    AppSession.CurrentUserId                 = userId;
                    AppSession.CurrentUserName               = username;
                    AppSession.LoginTime                     = DateTime.Now;
                    AppSession.CurrentUserRoles              = new List<string> { "Requester" };
                    AppSession.IsDepartmentAccountSession    = true;
                    AppSession.DepartmentAccountId           = accountId;
                    AppSession.DepartmentAccountCompanyId    = companyId;
                    AppSession.DepartmentAccountDepartmentId = deptId;
                    AppSession.DepartmentAccountBranchId     = branchId;
                    await PermissionResolver.LoadAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TryDeptAccountHashLogin error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Handles dept accounts that were created before the UserId column existed.
        /// On first successful login it creates the dbo.[User] row, links it, then sets the session.
        /// After this runs once the account goes through the normal login path forever.
        /// </summary>
        private async System.Threading.Tasks.Task<bool> MigrateLegacyDeptAccountAsync(string username, string password)
        {
            try
            {
                string cs = DatabaseConfig.ConnectionString;
                if (string.IsNullOrEmpty(cs)) return false;

                using (var con = new SqlConnection(cs))
                {
                    await con.OpenAsync();

                    // Only match accounts that have no User row yet
                    const string selectSql = @"
                        SELECT
                            da.Id,
                            da.PasswordHash,
                            da.PasswordSalt,
                            da.IsActive,
                            c.ComId,
                            d.DeptId,
                            b.BranchId
                        FROM dbo.DepartmentAccount da
                        LEFT JOIN dbo.Company    c ON (da.ComId IS NOT NULL AND c.ComId = da.ComId) OR (da.ComId IS NULL AND c.Name = da.CompanyName)
                        LEFT JOIN dbo.Department d ON (da.DeptId IS NOT NULL AND d.DeptId = da.DeptId) OR (da.DeptId IS NULL AND d.Name = da.DepartmentName)
                        LEFT JOIN dbo.Branch     b ON (da.BranchId IS NOT NULL AND b.BranchId = da.BranchId) OR (da.BranchId IS NULL AND b.Name = da.BranchName)
                        WHERE da.Username COLLATE Latin1_General_CS_AS = @Username COLLATE Latin1_General_CS_AS
                          AND da.UserId IS NULL";

                    int accountId;
                    byte[] storedHash, storedSalt;
                    bool isActive;
                    int? companyId, deptId, branchId;

                    using (var cmd = new SqlCommand(selectSql, con))
                    {
                        cmd.Parameters.AddWithValue("@Username", username);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync()) return false;

                            accountId  = reader.GetInt32(0);
                            storedHash = reader.IsDBNull(1) ? null : (byte[])reader.GetValue(1);
                            storedSalt = reader.IsDBNull(2) ? null : (byte[])reader.GetValue(2);
                            isActive   = !reader.IsDBNull(3) && reader.GetBoolean(3);
                            companyId  = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
                            deptId     = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
                            branchId   = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6);
                        }
                    }

                    if (!isActive || storedHash == null || storedSalt == null) return false;
                    if (!Yakult.Inventory.App.Helpers.PasswordHelper.VerifyPassword(password, storedHash, storedSalt)) return false;

                    // Credentials match — create the User row and link it
                    using (var transaction = con.BeginTransaction())
                    {
                        try
                        {
                            const string insertUserSql = @"
                                INSERT INTO dbo.[User] (Name, PasswordHash, PasswordSalt, IsActive, DateCreated)
                                VALUES (@Name, @Hash, @Salt, 1, GETDATE());
                                SELECT CAST(SCOPE_IDENTITY() AS INT);";

                            int newUserId;
                            using (var userCmd = new SqlCommand(insertUserSql, con, transaction))
                            {
                                userCmd.Parameters.AddWithValue("@Name", username);
                                userCmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, storedHash.Length) { Value = storedHash });
                                userCmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, storedSalt.Length) { Value = storedSalt });
                                newUserId = (int)await userCmd.ExecuteScalarAsync();
                            }

                            const string linkSql = "UPDATE dbo.DepartmentAccount SET UserId = @UserId WHERE Id = @Id";
                            using (var linkCmd = new SqlCommand(linkSql, con, transaction))
                            {
                                linkCmd.Parameters.AddWithValue("@UserId", newUserId);
                                linkCmd.Parameters.AddWithValue("@Id", accountId);
                                await linkCmd.ExecuteNonQueryAsync();
                            }

                            transaction.Commit();

                            AppSession.CurrentUserId                 = newUserId;
                            AppSession.CurrentUserName               = username;
                            AppSession.LoginTime                     = DateTime.Now;
                            AppSession.CurrentUserRoles              = new List<string> { "Requester" };
                            AppSession.IsDepartmentAccountSession    = true;
                            AppSession.DepartmentAccountId           = accountId;
                            AppSession.DepartmentAccountCompanyId    = companyId;
                            AppSession.DepartmentAccountDepartmentId = deptId;
                            AppSession.DepartmentAccountBranchId     = branchId;
                            await PermissionResolver.LoadAsync();
                            return true;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MigrateLegacyDeptAccount error: {ex.Message}");
                return false;
            }
        }

        private void TitleLabel_Click(object sender, EventArgs e)
        {
            // Optional
        }

        /// <summary>
        /// Checks whether the logged-in user is a DepartmentAccount.
        /// If so, sets IsDepartmentAccountSession and resolves Company/Dept/Branch IDs.
        /// </summary>
        private async System.Threading.Tasks.Task LoadDepartmentAccountDataAsync(int userId)
        {
            try
            {
                string cs = DatabaseConfig.ConnectionString;
                if (string.IsNullOrEmpty(cs)) return;

                using (var con = new SqlConnection(cs))
                {
                    await con.OpenAsync();

                    const string sql = @"
                        SELECT
                            da.Id,
                            da.IsActive,
                            c.ComId,
                            d.DeptId,
                            b.BranchId
                        FROM dbo.DepartmentAccount da
                        LEFT JOIN dbo.Company    c ON (da.ComId IS NOT NULL AND c.ComId = da.ComId) OR (da.ComId IS NULL AND c.Name = da.CompanyName)
                        LEFT JOIN dbo.Department d ON (da.DeptId IS NOT NULL AND d.DeptId = da.DeptId) OR (da.DeptId IS NULL AND d.Name = da.DepartmentName)
                        LEFT JOIN dbo.Branch     b ON (da.BranchId IS NOT NULL AND b.BranchId = da.BranchId) OR (da.BranchId IS NULL AND b.Name = da.BranchName)
                        WHERE da.UserId = @UserId";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync()) return; // not a dept account

                            AppSession.IsDepartmentAccountSession    = true;
                            AppSession.DepartmentAccountId           = reader.GetInt32(0);
                            AppSession.DepartmentAccountCompanyId    = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                            AppSession.DepartmentAccountDepartmentId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                            AppSession.DepartmentAccountBranchId     = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadDepartmentAccountData error: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadUserRolesAsync(int userId)
        {
            var roles = new List<string>();

            try
            {
                string connectionString = DatabaseConfig.ConnectionString;

                if (string.IsNullOrEmpty(connectionString))
                    return;

                using (var con = new SqlConnection(connectionString))
                {
                    await con.OpenAsync();

                    const string sql = @"
                        SELECT r.RoleName
                        FROM dbo.UserRole ur
                        INNER JOIN dbo.Role r ON ur.RoleId = r.RoleId
                        WHERE ur.UserId = @UserId AND r.IsActive = 1
                        ORDER BY r.RoleName";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                roles.Add(reader.GetString(0));
                            }
                        }
                    }
                }

                // If user is a Developer, ensure Developer role is in the list
                if (AppSession.IsDeveloper && !roles.Any(r => r.Equals("Developer", StringComparison.OrdinalIgnoreCase)))
                {
                    roles.Add("Developer");
                }

                // If no roles assigned, inject default Requester role at runtime
                if (roles.Count == 0)
                {
                    roles.Add("Requester");
                }

                AppSession.CurrentUserRoles = roles;
            }
            catch (Exception ex)
            {
                // Log error but don't block login - default to Requester
                AppSession.CurrentUserRoles = new List<string> { "Requester" };
                System.Diagnostics.Debug.WriteLine($"Error loading user roles: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadEmployeeDataAsync(int userId)
        {
            try
            {
                string connectionString = DatabaseConfig.ConnectionString;

                if (string.IsNullOrEmpty(connectionString))
                    return;

                using (var con = new SqlConnection(connectionString))
                {
                    await con.OpenAsync();

                    const string sql = @"
                        SELECT e.EmpId, e.Name, e.Position,
                               e.ComId,    c.Name AS CompanyName,
                               e.BranchId, b.Name AS BranchName,
                               e.DeptId,   d.Name AS DeptName
                        FROM dbo.[User] u
                        INNER JOIN dbo.Employee e ON u.EmpId = e.EmpId
                        LEFT  JOIN dbo.Company    c ON e.ComId    = c.ComId
                        LEFT  JOIN dbo.Branch     b ON e.BranchId = b.BranchId
                        LEFT  JOIN dbo.Department d ON e.DeptId   = d.DeptId
                        WHERE u.UserId = @UserId AND e.Active = 1";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                AppSession.CurrentEmployeeId       = reader.GetInt32(0);
                                AppSession.CurrentEmployeeName     = reader.GetString(1);
                                AppSession.CurrentEmployeePosition = reader.IsDBNull(2) ? null : reader.GetString(2);
                                AppSession.CurrentCompanyId        = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                                AppSession.CurrentCompanyName      = reader.IsDBNull(4) ? null : reader.GetString(4);
                                AppSession.CurrentBranchId         = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
                                AppSession.CurrentBranchName       = reader.IsDBNull(6) ? null : reader.GetString(6);
                                AppSession.CurrentDepartmentId     = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7);
                                AppSession.CurrentDepartmentName   = reader.IsDBNull(8) ? null : reader.GetString(8);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't block login - employee data is optional
                System.Diagnostics.Debug.WriteLine($"Error loading employee data: {ex.Message}");
            }
        }
    }
}
