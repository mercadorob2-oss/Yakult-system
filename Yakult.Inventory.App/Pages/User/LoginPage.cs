using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
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
        private Label _loginStatus;
        private Button _passwordToggle;
        private CheckBox _rememberMe;

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

            LoadBrandLogo();
            ApplyWorkspaceLayout();
            LoadRememberedLogin();
        }

        private void ApplyWorkspaceLayout()
        {
            SuspendLayout();
            var navy = ColorTranslator.FromHtml("#182235");
            var ink = ColorTranslator.FromHtml("#202635");
            var muted = ColorTranslator.FromHtml("#677081");
            var accent = ColorTranslator.FromHtml("#B92335");
            var line = ColorTranslator.FromHtml("#E5E6E8");
            Text = "Sign in · Yakult Workspace";
            BackColor = ColorTranslator.FromHtml("#F6F5F2");
            Font = new Font("Segoe UI", 10F);
            ClientSize = new Size(960, 620);
            MinimumSize = new Size(850, 580);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MainPanel.Controls.Clear();
            MainPanel.Padding = Padding.Empty;
            MainPanel.EdgeColor = line;
            MainPanel.Anchor = AnchorStyles.None;

            var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.White };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 61));
            BrandPanel.Dock = DockStyle.Fill;
            BrandPanel.Margin = Padding.Empty;
            BrandPanel.BackColor = navy;
            BrandPanel.EdgeColor = navy;
            logoCard.Dock = DockStyle.None;
            logoCard.Bounds = new Rectangle(28, 36, 160, 58);
            picLogo.Dock = DockStyle.Fill;
            picLogo.Margin = Padding.Empty;
            lblBrandTitle.Text = "Your systems.\nOne workspace.";
            lblBrandTitle.Font = new Font("Segoe UI", 23F, FontStyle.Bold);
            lblBrandTitle.ForeColor = Color.White;
            lblBrandTitle.TextAlign = ContentAlignment.TopLeft;
            lblBrandTitle.AutoSize = false;
            lblBrandSub.Text = "Access the tools that keep your day moving, from inventory to IT support.";
            lblBrandSub.Font = new Font("Segoe UI", 10F);
            lblBrandSub.ForeColor = ColorTranslator.FromHtml("#B8C3D4");
            lblBrandSub.TextAlign = ContentAlignment.TopLeft;
            var brandFooter = new Label { Text = "YAKULT · INTERNAL SYSTEMS", ForeColor = ColorTranslator.FromHtml("#B8C3D4"), Font = new Font("Segoe UI", 8F), AutoSize = false, Height = 34 };
            BrandPanel.Controls.Add(brandFooter);
            void ArrangeBrand()
            {
                double scale = logoCard.Width / 160D;
                int Spacing(int value) => (int)Math.Round(value * scale);
                int width = Math.Max(1, BrandPanel.ClientSize.Width - Spacing(56));
                lblBrandTitle.Bounds = new Rectangle(Spacing(28), Spacing(156), width, Spacing(98));
                lblBrandSub.Bounds = new Rectangle(Spacing(28), Spacing(274), width, Spacing(86));
                brandFooter.Bounds = new Rectangle(Spacing(28), BrandPanel.ClientSize.Height - Spacing(52), width, Spacing(30));
            }
            BrandPanel.SizeChanged += (_, __) => ArrangeBrand();
            split.Controls.Add(BrandPanel, 0, 0);

            var signIn = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White, Padding = new Padding(36, 28, 36, 24), Margin = Padding.Empty };
            var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 0, BackColor = Color.White, Margin = Padding.Empty };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            void Add(Control control, int height, int bottom = 0)
            {
                control.Dock = DockStyle.Top;
                control.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                control.Margin = new Padding(0, 0, 0, bottom);
                control.Height = height;
                int row = form.RowCount++;
                control.TabIndex = row;
                form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                form.Controls.Add(control, 0, row);
            }
            Add(new Label { Text = "WELCOME TO YOUR WORKSPACE", ForeColor = accent, Font = new Font("Segoe UI", 8F, FontStyle.Bold) }, 18, 8);
            TitleLabel.Text = "Welcome back";
            TitleLabel.ForeColor = ink;
            TitleLabel.Font = new Font("Segoe UI", 23F, FontStyle.Bold);
            TitleLabel.AutoSize = false;
            Add(TitleLabel, 42);
            lblSubtitle.Text = "Sign in with your Yakult account.";
            lblSubtitle.ForeColor = muted;
            lblSubtitle.Font = new Font("Segoe UI", 10F);
            Add(lblSubtitle, 25, 14);
            lblUserCaption.ForeColor = muted;
            lblPassCaption.ForeColor = muted;
            Add(lblUserCaption, 21, 3);

            foreach (var field in new[] { NameLoginField, PassLoginField })
            {
                field.BaseColor = Color.White;
                field.BorderColorA = line;
                field.BorderColorB = accent;
                field.ForeColor = ink;
                field.TabStop = true;
                field.Font = new Font("Segoe UI", 11F);
            }
            NameLoginField.AccessibleName = "Username";
            NameLoginField.TabIndex = 0;
            PassLoginField.AccessibleName = "Password";
            PassLoginField.TabIndex = 1;
            Add(NameLoginField, 42, 12);
            Add(lblPassCaption, 21, 3);
            var passwordRow = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty, TabIndex = 1 };
            passwordRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            passwordRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
            passwordRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            PassLoginField.Dock = DockStyle.Fill;
            PassLoginField.Margin = Padding.Empty;
            passwordRow.Controls.Add(PassLoginField, 0, 0);
            _passwordToggle = new Button { Text = "Show", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0), FlatStyle = FlatStyle.Flat, ForeColor = muted, BackColor = Color.White, UseVisualStyleBackColor = false, Cursor = Cursors.Hand, TabIndex = 2, AccessibleName = "Show password" };
            _passwordToggle.FlatAppearance.BorderColor = line;
            _passwordToggle.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#FCF0F1");
            _passwordToggle.Click += lblTogglePassword_Click;
            passwordRow.Controls.Add(_passwordToggle, 1, 0);
            Add(passwordRow, 42);
            _rememberMe = new CheckBox { Text = "Remember me", ForeColor = muted, Font = new Font("Segoe UI", 9.5F), BackColor = Color.White, AutoSize = false, AccessibleName = "Remember me on this computer" };
            Add(_rememberMe, 26, 4);
            _loginStatus = new Label { Text = "", ForeColor = accent, Font = new Font("Segoe UI", 9F), AccessibleName = "Sign-in status" };
            Add(_loginStatus, 30);
            LoginBtn.TabIndex = 3;
            LoginBtn.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            Helpers.UiFactory.ConfigurePillHopeButton(LoginBtn, accent, ColorTranslator.FromHtml("#9E1E2E"));
            Add(LoginBtn, 44, 10);
            lblContactAdmin.ForeColor = muted;
            lblContactAdmin.Text = "Need an account or help signing in?\nContact the Information Technology Department.";
            lblContactAdmin.TextAlign = ContentAlignment.TopLeft;
            Add(lblContactAdmin, 40);
            var support = new LinkLabel { Text = "Contact IT support", AutoSize = false, LinkColor = accent, ActiveLinkColor = accent, VisitedLinkColor = accent, TabIndex = 4 };
            support.LinkClicked += (_, __) =>
            {
                string email = Properties.Settings.Default.ITContactEmail;
                if (string.IsNullOrWhiteSpace(email)) email = "it-support@yakult.com";
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("mailto:" + Uri.EscapeDataString(email.Trim())) { UseShellExecute = true }); }
                catch { _loginStatus.Text = "Contact IT support at " + email; }
            };
            Add(support, 22);
            signIn.Controls.Add(form);
            lblTogglePassword.Visible = false;
            signIn.Controls.Add(lblTogglePassword);
            split.Controls.Add(signIn, 1, 0);
            MainPanel.Controls.Add(split);
            bool arranging = false;
            void CenterCard()
            {
                if (arranging || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
                arranging = true;
                try
                {
                    // AutoScale also scales the controls and padding. Use the same scale
                    // here, then arrange again after startup scaling has completed.
                    double scale = signIn.Padding.Left / 36D;
                    int outerSpace = (int)Math.Round(40 * scale);
                    int width = Math.Max(1, Math.Min((int)Math.Round(920 * scale), ClientSize.Width - outerSpace));
                    int contentHeight = form.PreferredSize.Height + signIn.Padding.Vertical;
                    int height = Math.Max(1, Math.Min(Math.Max((int)Math.Round(548 * scale), contentHeight), ClientSize.Height - outerSpace));
                    MainPanel.SetBounds((ClientSize.Width - width) / 2, (ClientSize.Height - height) / 2, width, height);
                    ArrangeBrand();
                }
                finally { arranging = false; }
            }
            Resize += (_, __) => CenterCard();
            Layout += (_, __) => CenterCard();
            Load += (_, __) => CenterCard();
            CenterCard();
            Shown += (_, __) => NameLoginField.Focus();
            ResumeLayout(true);
        }

        private void LoadBrandLogo()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = new[]
                {
                    Path.Combine(baseDir, "Images", "yakult_Name.png"),
                    Path.Combine(baseDir, "images", "yakult_Name.png"),
                    Path.Combine(baseDir, "yakult_Name.png")
                };
                foreach (string path in candidates)
                {
                    if (File.Exists(path))
                    {
                        picLogo.Image = Image.FromFile(path);
                        break;
                    }
                }
            }
            catch
            {
                // Text-only branding remains; logo is decorative.
            }
        }

        private sealed class RememberedLogin
        {
            public string Username { get; set; }
            public string ProtectedPassword { get; set; }
        }

        private static string RememberedLoginPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Yakult", "Workspace", "remembered_login.json");
        }

        private bool IsRememberMeChecked() => _rememberMe != null && _rememberMe.Checked;

        private void LoadRememberedLogin()
        {
            try
            {
                string path = RememberedLoginPath();
                if (!File.Exists(path)) return;
                var saved = Newtonsoft.Json.JsonConvert.DeserializeObject<RememberedLogin>(File.ReadAllText(path));
                if (saved == null || string.IsNullOrWhiteSpace(saved.Username)) return;
                NameLoginField.Text = saved.Username;
                if (!string.IsNullOrEmpty(saved.ProtectedPassword))
                {
                    string pw = Helpers.SecretProtector.UnprotectToString(Convert.FromBase64String(saved.ProtectedPassword));
                    if (pw != null) PassLoginField.Text = pw;
                }
                if (_rememberMe != null) _rememberMe.Checked = true;
            }
            catch
            {
                // Corrupt or undecryptable file: fall through to a blank login.
            }
        }

        private void PersistRememberedLogin(string username, string password)
        {
            try
            {
                string path = RememberedLoginPath();
                if (!IsRememberMeChecked())
                {
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }
                byte[] protectedPw = Helpers.SecretProtector.ProtectString(password);
                var saved = new RememberedLogin
                {
                    Username = username,
                    ProtectedPassword = protectedPw == null ? null : Convert.ToBase64String(protectedPw)
                };
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(saved));
            }
            catch (Exception ex)
            {
                Logger.LogError("[Login] Could not save remembered login.", ex);
            }
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
            if (_isLoggingIn) return;
            _passwordVisible = !_passwordVisible;
            if (_passwordToggle != null)
            {
                _passwordToggle.Text = _passwordVisible ? "Hide" : "Show";
                _passwordToggle.AccessibleName = _passwordVisible ? "Hide password" : "Show password";
            }

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
            lblTogglePassword.ForeColor = Color.FromArgb(227, 6, 19);
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
                _loginStatus.Text = "Enter your username and password to continue.";
                if (string.IsNullOrWhiteSpace(name)) NameLoginField.Focus();
                else PassLoginField.Focus();
                return;
            }

            _loginStatus.Text = "";
            _isLoggingIn = true;
            LoginBtn.Enabled = false;
            LoginBtn.Text = "Signing in...";
            NameLoginField.Enabled = false;
            PassLoginField.Enabled = false;
            _passwordToggle.Enabled = false;

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

                    PersistRememberedLogin(name, password);
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

                _loginStatus.Text = "The username or password is incorrect. Please try again.";
            }
            catch (Exception ex)
            {
                Logger.LogError("[Login] Sign-in failed.", ex);
                if (!IsDisposed) _loginStatus.Text = "Unable to sign in. Try again or contact IT support.";
            }
            finally
            {
                _isLoggingIn = false;
                if (!IsDisposed)
                {
                    LoginBtn.Enabled = true;
                    LoginBtn.Text = "Sign In";
                    NameLoginField.Enabled = true;
                    PassLoginField.Enabled = true;
                    _passwordToggle.Enabled = true;
                }
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
                    PersistRememberedLogin(username, password);
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
                            PersistRememberedLogin(username, password);
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
