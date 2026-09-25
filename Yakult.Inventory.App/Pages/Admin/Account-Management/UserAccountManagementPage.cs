﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Drawing.Drawing2D;
using System.Net.Mail;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Data;
using Yakult.Inventory.App.Services;
using HopeButton = ReaLTaiizor.Controls.HopeButton;

namespace Yakult.Inventory.App.Pages.Admin.AccountManagement
{
    public partial class UserAccountManagementPage : UserControl
    {
        private DefaultListPageLayout _layout;
        private DataGridView _dgvUsers;
        private HopeButton _btnAdd;
        private HopeButton _btnEdit;
        private HopeButton _btnResetPassword;
        private HopeButton _btnManualReset;
        private HopeButton _btnDelete;
        private System.Windows.Forms.Button btnFirstPage, btnPrevPage, btnNextPage, btnLastPage;
        private Label lblPageInfo;
        private List<LegacyUserAccountDto> _allUsers;
        private List<LegacyUserAccountDto> _filteredUsers;
        private string _connectionString;
        private int _currentPage = 1;
        private int _pageSize = 15;
        private string _sortColumn = null;
        private bool   _sortAscending = true;
        private readonly Dictionary<string, HashSet<string>> _columnFilters =
            new Dictionary<string, HashSet<string>>();

        public UserAccountManagementPage()
        {
            _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            InitializeComponent();
            BuildUI();
            LoadUsers();
        }

        private void BuildUI()
        {
            SuspendLayout();
            Controls.Clear();

            Dock = DockStyle.Fill;
            BackColor = Color.White;

            _layout = DefaultListPageTemplate.Create(
                "User Account Management",
                "Search by Username, Email, or Employee Name...",
                (s, e) => ApplyFilters(),
                () =>
                {
                    _columnFilters.Clear();
                    if (_dgvUsers != null)
                    {
                        foreach (DataGridViewColumn c in _dgvUsers.Columns)
                            c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                        _dgvUsers.Invalidate();
                    }
                    LoadUsers();
                });

            if (_layout.FilterByComboBox != null)
            {
                _layout.FilterByComboBox.Items.Clear();
                _layout.FilterByComboBox.Items.AddRange(new object[] { "All", "Active Only", "Inactive Only" });
                _layout.FilterByComboBox.SelectedIndex = 0;
                _layout.FilterByComboBox.SelectedIndexChanged += (s, e) => ApplyFilters();
            }

            // Add buttons
            _btnAdd = new HopeButton
            {
                Text = "\u2795  Create Account",
                Font = UiTheme.Fonts.Button,
                Size = new Size(200, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigurePillHopeButton(_btnAdd, UiTheme.Colors.Primary, UiTheme.Colors.PrimaryHover);
            _btnAdd.Click += BtnAdd_Click;

            _btnEdit = new HopeButton
            {
                Text = "\u270F  Edit",
                Font = UiTheme.Fonts.Button,
                Size = new Size(130, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(_btnEdit, UiTheme.Colors.PrimaryHover, UiTheme.Colors.OutlineHoverBack);
            _btnEdit.Click += BtnEdit_Click;

            _btnResetPassword = new HopeButton
            {
                Text = "Reset Password",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Size = new Size(145, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigurePillHopeButton(_btnResetPassword, Color.FromArgb(149, 117, 205), Color.FromArgb(120, 90, 175));
            _btnResetPassword.Click += BtnResetPassword_Click;

            _btnManualReset = new HopeButton
            {
                Text = "Manual Password Reset",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Size = new Size(185, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigurePillHopeButton(_btnManualReset, Color.FromArgb(240, 152, 183), Color.FromArgb(210, 115, 150));
            _btnManualReset.Click += BtnManualReset_Click;

            _btnDelete = new HopeButton
            {
                Text = "Delete",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Size = new Size(90, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigurePillHopeButton(_btnDelete, Color.FromArgb(220, 53, 69), Color.FromArgb(185, 35, 50));
            _btnDelete.Click += BtnDelete_Click;

            DefaultListPageTemplate.AddButtons(_layout.ButtonLeftFlow, new[] { _btnAdd, _btnEdit, _btnResetPassword, _btnManualReset, _btnDelete });

            if (_layout.SummaryPanel != null)
                _layout.SummaryPanel.Visible = false;

            _dgvUsers = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
            };

            UiFactory.StyleGrid(_dgvUsers);
            _dgvUsers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _dgvUsers.AutoSizeRowsMode    = DataGridViewAutoSizeRowsMode.AllCells;
            _dgvUsers.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _dgvUsers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _dgvUsers.MultiSelect = false;
            _dgvUsers.AllowUserToAddRows = false;
            _dgvUsers.AllowUserToDeleteRows = false;
            _dgvUsers.ReadOnly = true;
            _dgvUsers.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) BtnEdit_Click(s, e);
            };
            _dgvUsers.ColumnHeaderMouseClick += DgvUsers_ColumnHeaderMouseClick;
            _dgvUsers.CellPainting           += DgvUsers_CellPainting;

            // Add columns — all sortable via Programmatic so custom painting shows glyphs
            _dgvUsers.Columns.Clear();
            _dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "UserId",       HeaderText = "ID",         DataPropertyName = "UserId",       Width = 60,  Visible = false });
            _dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Username",     HeaderText = "Username",   DataPropertyName = "Username",     FillWeight = 14, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Email",        HeaderText = "Email",      DataPropertyName = "Email",        FillWeight = 22, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", HeaderText = "Employee",   DataPropertyName = "EmployeeName", FillWeight = 20, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Roles",        HeaderText = "Roles",      DataPropertyName = "Roles",        FillWeight = 18, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastLogin",    HeaderText = "Last Login", DataPropertyName = "LastLogin",    FillWeight = 14, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status",       HeaderText = "Status",     DataPropertyName = "Status",       FillWeight = 10, SortMode = DataGridViewColumnSortMode.Programmatic });

            if (_layout.GridCard != null)
            {
                _layout.GridCard.Controls.Clear();
                _layout.GridCard.Controls.Add(_dgvUsers);
            }

            btnFirstPage = new System.Windows.Forms.Button { Text = "<<", Width = 45, Height = 25, Left = 0, Top = 5 };
            btnPrevPage = new System.Windows.Forms.Button { Text = "<", Width = 45, Height = 25, Left = 50, Top = 5 };
            lblPageInfo = new Label { AutoSize = false, Width = 240, Left = 100, Top = 10, Font = new Font("Segoe UI", 9F, FontStyle.Regular), TextAlign = ContentAlignment.MiddleLeft };
            btnNextPage = new System.Windows.Forms.Button { Text = ">", Width = 45, Height = 25, Left = 350, Top = 5 };
            btnLastPage = new System.Windows.Forms.Button { Text = ">>", Width = 45, Height = 25, Left = 400, Top = 5 };

            btnFirstPage.Click += (s, e) => { _currentPage = 1; UpdateDataGridView(); };
            btnPrevPage.Click += (s, e) => { if (_currentPage > 1) { _currentPage--; UpdateDataGridView(); } };
            btnNextPage.Click += (s, e) =>
            {
                int totalPages = (int)Math.Ceiling((_filteredUsers?.Count ?? 0) / (double)_pageSize);
                if (_currentPage < totalPages) { _currentPage++; UpdateDataGridView(); }
            };
            btnLastPage.Click += (s, e) =>
            {
                int totalPages = (int)Math.Ceiling((_filteredUsers?.Count ?? 0) / (double)_pageSize);
                _currentPage = totalPages;
                UpdateDataGridView();
            };

            if (_layout.PaginationPanel != null)
            {
                _layout.PaginationPanel.Controls.Clear();
                _layout.PaginationPanel.Controls.AddRange(new Control[] { btnFirstPage, btnPrevPage, lblPageInfo, btnNextPage, btnLastPage });
            }

            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.SummaryPanel);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_layout.HeaderPanel);
            ResumeLayout(false);

            DefaultListPageTemplate.SetupInitialPageFocus(_layout.SearchBox, _dgvUsers, _btnAdd, _btnEdit, _btnResetPassword, _btnManualReset, _btnDelete);
        }

        private async void LoadUsers()
        {
            try
            {
                _allUsers = new List<LegacyUserAccountDto>();

                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    const string sql = @"
                        SELECT
                            u.UserId,
                            u.Name AS Username,
                            u.EmailAddress,
                            u.IsActive,
                            u.IsDeveloper,
                            u.LastLoginDate,
                            u.MustChangePassword,
                            u.EmpId,
                            e.Name AS EmployeeName,
                            e.EmployeeNumber,
                            STUFF((
                                SELECT ', ' + r.RoleName
                                FROM UserRole ur
                                INNER JOIN Role r ON ur.RoleId = r.RoleId
                                WHERE ur.UserId = u.UserId AND r.IsActive = 1
                                FOR XML PATH(''), TYPE
                            ).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS Roles
                        FROM [User] u
                        LEFT JOIN Employee e ON u.EmpId = e.EmpId
                        ORDER BY u.Name";

                    using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 })
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                _allUsers.Add(new LegacyUserAccountDto
                                {
                                    UserId = reader.GetInt32(0),
                                    Username = reader.GetString(1),
                                    Email = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    IsActive = reader.GetBoolean(3),
                                    IsDeveloper = reader.GetBoolean(4),
                                    LastLoginDate = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                                    MustChangePassword = reader.GetBoolean(6),
                                    EmpId = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                                    EmployeeName = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                    EmployeeNumber = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                    Roles = reader.IsDBNull(10) ? "" : reader.GetString(10)
                                });
                            }
                        }
                    }
                }

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load users: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal static async System.Threading.Tasks.Task<bool> EmailExistsAsync(SqlConnection con, string email, int? excludeUserId)
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM dbo.[User]
                WHERE EmailAddress = @Email
                  AND (@ExcludeUserId IS NULL OR UserId <> @ExcludeUserId)";

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Email", email);
                cmd.Parameters.AddWithValue("@ExcludeUserId", (object)excludeUserId ?? DBNull.Value);
                int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return count > 0;
            }
        }

        internal static bool IsValidEmailAddress(string email)
        {
            try
            {
                _ = new MailAddress(email);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ApplyFilters()
        {
            if (_allUsers == null)
            {
                _filteredUsers = new List<LegacyUserAccountDto>();
                UpdateDataGridView();
                return;
            }

            var searchText = _layout.SearchBox?.Text?.Trim().ToLower() ?? "";
            var filterBy = _layout.FilterByComboBox?.SelectedItem?.ToString() ?? "All";

            _filteredUsers = _allUsers.Where(user =>
            {
                if (!string.IsNullOrEmpty(searchText))
                {
                    bool matches = user.Username.ToLower().Contains(searchText) ||
                                   (user.Email ?? "").ToLower().Contains(searchText) ||
                                   (user.EmployeeName ?? "").ToLower().Contains(searchText) ||
                                   (user.Roles ?? "").ToLower().Contains(searchText);

                    if (!matches) return false;
                }

                if (filterBy == "Active Only" && !user.IsActive) return false;
                if (filterBy == "Inactive Only" && user.IsActive) return false;

                if (_columnFilters.TryGetValue("Email", out var emailSet))
                {
                    string emailVal = string.IsNullOrWhiteSpace(user.Email) ? "(No Email)" : user.Email;
                    if (!emailSet.Contains(emailVal, StringComparer.OrdinalIgnoreCase)) return false;
                }

                if (_columnFilters.TryGetValue("Roles", out var roleSet))
                {
                    // Match if any of the user's individual roles is in the selected set
                    var userRoles = string.IsNullOrWhiteSpace(user.Roles)
                        ? new[] { "(No Role)" }
                        : user.Roles.Split(',').Select(r => r.Trim()).Where(r => r.Length > 0).ToArray();
                    if (!userRoles.Any(r => roleSet.Contains(r, StringComparer.OrdinalIgnoreCase))) return false;
                }

                return true;
            }).ToList();

            _currentPage = 1;
            UpdateDataGridView();
        }

        private void UpdateDataGridView()
        {
            if (_filteredUsers == null)
            {
                _dgvUsers.DataSource = null;
                return;
            }

            IEnumerable<LegacyUserAccountDto> displayList = _filteredUsers;
            if (_sortColumn != null)
            {
                switch (_sortColumn)
                {
                    case "Username":     displayList = _sortAscending ? displayList.OrderBy(u => u.Username ?? "")     : displayList.OrderByDescending(u => u.Username ?? "");     break;
                    case "Email":        displayList = _sortAscending ? displayList.OrderBy(u => u.Email ?? "")        : displayList.OrderByDescending(u => u.Email ?? "");        break;
                    case "EmployeeName": displayList = _sortAscending ? displayList.OrderBy(u => u.EmployeeName ?? "") : displayList.OrderByDescending(u => u.EmployeeName ?? ""); break;
                    case "Roles":        displayList = _sortAscending ? displayList.OrderBy(u => u.Roles ?? "")        : displayList.OrderByDescending(u => u.Roles ?? "");        break;
                    case "LastLogin":    displayList = _sortAscending ? displayList.OrderBy(u => u.LastLoginDate ?? DateTime.MinValue) : displayList.OrderByDescending(u => u.LastLoginDate ?? DateTime.MinValue); break;
                    case "Status":       displayList = _sortAscending ? displayList.OrderBy(u => GetStatusText(u))     : displayList.OrderByDescending(u => GetStatusText(u));      break;
                }
            }

            var pagedUsers = displayList
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.Email,
                    u.EmployeeName,
                    u.Roles,
                    LastLogin = u.LastLoginDate?.ToString("yyyy-MM-dd HH:mm") ?? "Never",
                    Status = GetStatusText(u)
                })
                .ToList();

            _dgvUsers.DataSource = pagedUsers;

            int totalPages = (int)Math.Ceiling(_filteredUsers.Count / (double)_pageSize);

            if (lblPageInfo != null)
                lblPageInfo.Text = totalPages == 0
                    ? "Page 0 of 0 (0 users)"
                    : $"Page {_currentPage} of {totalPages} ({_filteredUsers.Count} users)";

            if (btnFirstPage != null) btnFirstPage.Enabled = _currentPage > 1;
            if (btnPrevPage != null) btnPrevPage.Enabled = _currentPage > 1;
            if (btnNextPage != null) btnNextPage.Enabled = _currentPage < totalPages;
            if (btnLastPage != null) btnLastPage.Enabled = _currentPage < totalPages;

            DefaultListPageTemplate.DisableDefaultRowHighlight(_dgvUsers, _btnAdd, _btnEdit, _btnResetPassword, _btnManualReset, _btnDelete);
        }

        private string GetStatusText(LegacyUserAccountDto user)
        {
            if (!user.IsActive) return "Inactive";
            if (user.IsDeveloper) return "Developer";
            if (user.MustChangePassword) return "Reset Req'd";
            return "Active";
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            using (var dialog = new UserAccountEditDialog(_connectionString))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    LoadUsers();
                }
            }
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            if (_dgvUsers.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a user to edit.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int userId = Convert.ToInt32(_dgvUsers.SelectedRows[0].Cells["UserId"].Value);
            var user = _allUsers.FirstOrDefault(u => u.UserId == userId);

            if (user != null)
            {
                using (var dialog = new UserAccountEditDialog(_connectionString, user))
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        LoadUsers();
                    }
                }
            }
        }

        private async void BtnResetPassword_Click(object sender, EventArgs e)
        {
            if (_dgvUsers.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a user to reset password.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int userId = Convert.ToInt32(_dgvUsers.SelectedRows[0].Cells["UserId"].Value);
            var user = _allUsers.FirstOrDefault(u => u.UserId == userId);

            if (user == null) return;

            var result = MessageBox.Show(
                $"Reset password for user '{user.Username}'?\n\nA temporary password will be generated and the user will be required to change it on next login.",
                "Confirm Password Reset",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            try
            {
                string tempPassword = PasswordHelper.GenerateTemporaryPassword();

                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    const string sql = @"
                        UPDATE [User]
                        SET [Password] = CONVERT(VARBINARY(MAX), @Password),
                            MustChangePassword = 1,
                            IsTemporaryPassword = 1
                        WHERE UserId = @UserId";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@Password", tempPassword);
                        cmd.Parameters.AddWithValue("@UserId", userId);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                ActivityLogger.Log(ActivityLogger.Actions.ResetPassword,
                    "User", userId, $"Password reset for user '{user.Username}'");

                using (var dlg = new PasswordResetSuccessDialog(user.Username, tempPassword))
                {
                    dlg.ShowDialog(this);
                }

                LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to reset password: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnManualReset_Click(object sender, EventArgs e)
        {
            if (_dgvUsers.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a user to reset the password for.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int userId = Convert.ToInt32(_dgvUsers.SelectedRows[0].Cells["UserId"].Value);
            var userRepo = new UserRepository(_connectionString);

            using (var dialog = new ResetPasswordDialog(userRepo, userId))
            {
                dialog.ShowDialog(this.FindForm());
            }
        }

        private async void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_dgvUsers.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a user to delete.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int userId = Convert.ToInt32(_dgvUsers.SelectedRows[0].Cells["UserId"].Value);
            var user = _allUsers.FirstOrDefault(u => u.UserId == userId);

            if (user == null) return;

            // Prevent deleting current user
            if (userId == AppSession.CurrentUserId)
            {
                MessageBox.Show("You cannot delete your own account while logged in.", "Cannot Delete",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Warn about developer accounts
            if (user.IsDeveloper)
            {
                var devResult = MessageBox.Show(
                    $"WARNING: '{user.Username}' is a Developer account with full system access.\n\nAre you sure you want to delete this account?",
                    "Delete Developer Account",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (devResult != DialogResult.Yes) return;
            }

            // Confirm deletion
            string message = $"Are you sure you want to delete the user account '{user.Username}'?\n\n" +
                            "This will permanently remove:\n" +
                            "• The user account and login credentials\n" +
                            "• All role assignments for this user\n\n";

            if (!string.IsNullOrEmpty(user.EmployeeName))
            {
                message += $"The linked employee record '{user.EmployeeName}' will NOT be deleted and will remain in the system.\n\n";
            }

            message += "This action CANNOT be undone.";

            var result = MessageBox.Show(message, "Confirm Account Deletion",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result != DialogResult.Yes) return;

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    using (var transaction = con.BeginTransaction())
                    {
                        try
                        {
                            // Delete user roles first (foreign key constraint)
                            const string deleteRolesSql = "DELETE FROM UserRole WHERE UserId = @UserId";
                            using (var cmd = new SqlCommand(deleteRolesSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // Delete the user account (employee record is NOT deleted)
                            const string deleteUserSql = "DELETE FROM [User] WHERE UserId = @UserId";
                            using (var cmd = new SqlCommand(deleteUserSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                                if (rowsAffected == 0)
                                {
                                    throw new Exception("User account not found or already deleted.");
                                }
                            }

                            transaction.Commit();

                            MessageBox.Show(
                                $"User account '{user.Username}' has been successfully deleted.\n\n" +
                                (!string.IsNullOrEmpty(user.EmployeeName)
                                    ? $"The employee record '{user.EmployeeName}' remains intact."
                                    : ""),
                                "Account Deleted",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            LoadUsers();
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete user account: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Column header click ───────────────────────────────────────────────────

        private void DgvUsers_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = _dgvUsers.Columns[e.ColumnIndex];
            if (col.SortMode != DataGridViewColumnSortMode.Programmatic) return;

            // Rightmost 18 px = filter ▼ zone
            if (e.X >= col.Width - 18)
            {
                ShowColumnFilterPopup(col);
                return;
            }

            // Toggle sort
            if (_sortColumn == col.Name)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumn    = col.Name;
                _sortAscending = true;
            }

            foreach (DataGridViewColumn c in _dgvUsers.Columns)
                c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            col.HeaderCell.SortGlyphDirection = _sortAscending
                ? System.Windows.Forms.SortOrder.Ascending
                : System.Windows.Forms.SortOrder.Descending;

            ApplyFilters();
        }

        // ── Column filter popup ───────────────────────────────────────────────────

        private void ShowColumnFilterPopup(DataGridViewColumn col)
        {
            if (_allUsers == null) return;

            // Only Email and Roles support the filter popup
            List<string> distinctValues;
            switch (col.Name)
            {
                case "Email":
                    distinctValues = _allUsers
                        .Select(u => string.IsNullOrWhiteSpace(u.Email) ? "(No Email)" : u.Email)
                        .Where(v => !string.IsNullOrEmpty(v))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    break;
                case "Roles":
                    // Split comma-separated roles so each individual role appears once
                    distinctValues = _allUsers
                        .SelectMany(u => string.IsNullOrWhiteSpace(u.Roles)
                            ? new[] { "(No Role)" }
                            : u.Roles.Split(',').Select(r => r.Trim()).Where(r => r.Length > 0))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    break;
                default: return;
            }

            _columnFilters.TryGetValue(col.Name, out var currentFilter);

            var headerCell  = _dgvUsers.GetCellDisplayRectangle(col.Index, -1, false);
            var screenBelow = _dgvUsers.PointToScreen(new Point(headerCell.Left, headerCell.Bottom));
            var screenAbove = _dgvUsers.PointToScreen(new Point(headerCell.Left, headerCell.Top));

            using (var popup = new ColumnFilterPopup(col.HeaderText, distinctValues, currentFilter))
            {
                var wa = Screen.FromPoint(screenBelow).WorkingArea;

                int popupY = (screenBelow.Y + popup.Height <= wa.Bottom)
                    ? screenBelow.Y
                    : Math.Max(wa.Top, screenAbove.Y - popup.Height);

                int popupX = Math.Max(wa.Left, Math.Min(screenBelow.X, wa.Right - popup.Width));

                popup.Location = new Point(popupX, popupY);

                popup.ShowDialog(this.FindForm());

                if (popup.Action == ColumnFilterPopup.PopupAction.SortAscending)
                {
                    _sortColumn = col.Name; _sortAscending = true;
                    foreach (DataGridViewColumn c in _dgvUsers.Columns) c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                    col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Ascending;
                    ApplyFilters();
                }
                else if (popup.Action == ColumnFilterPopup.PopupAction.SortDescending)
                {
                    _sortColumn = col.Name; _sortAscending = false;
                    foreach (DataGridViewColumn c in _dgvUsers.Columns) c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                    col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Descending;
                    ApplyFilters();
                }
                else if (popup.Action == ColumnFilterPopup.PopupAction.Filter)
                {
                    if (popup.SelectedValues == null || popup.SelectedValues.Count == 0
                        || popup.SelectedValues.Count >= distinctValues.Count)
                        _columnFilters.Remove(col.Name);
                    else
                        _columnFilters[col.Name] = popup.SelectedValues;

                    ApplyFilters();
                }
            }
        }

        // ── Custom header painting (sort glyphs + filter ▼ icon) ─────────────────

        private void DgvUsers_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 || e.ColumnIndex < 0) return;

            var column = _dgvUsers.Columns[e.ColumnIndex];
            if (column == null) return;

            e.Handled = true;

            e.Graphics.FillRectangle(Brushes.White, e.CellBounds);

            var headerRect = new Rectangle(e.CellBounds.X, e.CellBounds.Y, e.CellBounds.Width - 1, e.CellBounds.Height - 1);
            using (var brush = new SolidBrush(e.CellStyle.BackColor))
                e.Graphics.FillRectangle(brush, headerRect);

            using (var pen = new Pen(Color.White, 1))
            {
                e.Graphics.DrawLine(pen, e.CellBounds.Right - 1, e.CellBounds.Top,    e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                e.Graphics.DrawLine(pen, e.CellBounds.Left,      e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }

            using (var textBrush = new SolidBrush(Color.White))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var  textRect   = e.CellBounds;
                bool isSortable = column.SortMode == DataGridViewColumnSortMode.Programmatic;

                if (isSortable)
                {
                    textRect.Width -= 34;

                    // Sort arrow
                    int glyphX = e.CellBounds.Right - 32;
                    int glyphY = e.CellBounds.Y + (e.CellBounds.Height - 8) / 2;

                    Color arrowColor = column.HeaderCell.SortGlyphDirection != System.Windows.Forms.SortOrder.None
                        ? Color.White
                        : Color.FromArgb(160, 255, 255, 255);

                    using (var arrowPen = new Pen(arrowColor, 2))
                    {
                        if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Ascending)
                        {
                            e.Graphics.DrawLine(arrowPen, glyphX,     glyphY + 6, glyphX + 5,  glyphY);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY,     glyphX + 10, glyphY + 6);
                        }
                        else if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Descending)
                        {
                            e.Graphics.DrawLine(arrowPen, glyphX,     glyphY,     glyphX + 5,  glyphY + 6);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY + 6, glyphX + 10, glyphY);
                        }
                        else
                        {
                            e.Graphics.DrawLine(arrowPen, glyphX + 2, glyphY + 2, glyphX + 5, glyphY - 1);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY - 1, glyphX + 8, glyphY + 2);
                            e.Graphics.DrawLine(arrowPen, glyphX + 2, glyphY + 4, glyphX + 5, glyphY + 7);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY + 7, glyphX + 8, glyphY + 4);
                        }
                    }

                    // Filter ▼ icon — only on Email and Roles columns
                    if (column.Name == "Email" || column.Name == "Roles")
                    {
                        bool hasActiveFilter = _columnFilters.ContainsKey(column.Name);
                        int  filterX    = e.CellBounds.Right - 14;
                        int  filterMidY = e.CellBounds.Y + e.CellBounds.Height / 2;
                        var  filterPts  = new PointF[]
                        {
                            new PointF(filterX,     filterMidY - 4),
                            new PointF(filterX + 9, filterMidY - 4),
                            new PointF(filterX + 4, filterMidY + 3)
                        };
                        using (var filterBrush = new SolidBrush(hasActiveFilter
                            ? Color.FromArgb(255, 230, 80)
                            : Color.FromArgb(140, 255, 255, 255)))
                        {
                            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                            e.Graphics.FillPolygon(filterBrush, filterPts);
                            e.Graphics.SmoothingMode = SmoothingMode.Default;
                        }
                    }
                }

                e.Graphics.DrawString(column.HeaderText, e.CellStyle.Font ?? _dgvUsers.Font, textBrush, textRect, fmt);
            }
        }
    }

    // DTO for User Account data (Legacy - used only in this page)
    public class LegacyUserAccountDto
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeveloper { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public bool MustChangePassword { get; set; }
        public int? EmpId { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeeNumber { get; set; }
        public string Roles { get; set; }

        public string LastLoginDisplay => LastLoginDate?.ToString("yyyy-MM-dd HH:mm") ?? "Never";

        public string StatusDisplay
        {
            get
            {
                if (!IsActive)          return "Inactive";
                if (IsDeveloper)        return "Developer";
                if (MustChangePassword) return "Reset Req'd";
                return "Active";
            }
        }
    }

    // Dialog for creating/editing user accounts
    public class UserAccountEditDialog : Form
    {
        private readonly string _connectionString;
        private readonly LegacyUserAccountDto _user;
        private readonly bool _isEdit;

        private TextBox txtUsername;
        private TextBox txtEmail;
        private Label   _lblSelectedEmployee;
        private Button  _btnBrowseEmployee;
        private Button  _btnClearEmployee;
        private CheckBox chkIsDeveloper;
        private CheckBox chkIsActive;
        private CheckBox chkMustChangePassword;
        private Panel _pnlRoles;
        private Button btnSave;
        private Button btnCancel;

        private int? _selectedEmpId;
        private List<EmployeeLookupResult> _allEmployees = new List<EmployeeLookupResult>();
        private List<RoleLookup> _roles            = new List<RoleLookup>();
        private List<int> _userRoleIds             = new List<int>();

        public UserAccountEditDialog(string connectionString, LegacyUserAccountDto user = null, int? preselectedEmpId = null)
        {
            _connectionString = connectionString;
            _user = user;
            _isEdit = user != null;
            _selectedEmpId = preselectedEmpId;

            InitializeDialog();
            LoadData();
        }

        private void InitializeDialog()
        {
            Text = _isEdit ? "Edit User Account" : "Create New User Account";
            Size = new Size(580, 570);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            int yPos = 20;
            const int labelWidth  = 140;
            const int controlLeft = 170;
            const int controlWidth = 375;

            // ── Username ──────────────────────────────────────────────────────
            Controls.Add(new Label { Text = "Username: *", Location = new Point(20, yPos), Width = labelWidth });
            txtUsername = new TextBox { Location = new Point(controlLeft, yPos), Width = controlWidth };
            Controls.Add(txtUsername);
            yPos += 35;

            // ── Email ─────────────────────────────────────────────────────────
            Controls.Add(new Label { Text = "Email:", Location = new Point(20, yPos), Width = labelWidth });
            txtEmail = new TextBox { Location = new Point(controlLeft, yPos), Width = controlWidth };
            Controls.Add(txtEmail);
            yPos += 35;

            // ── Employee row ──────────────────────────────────────────────────
            Controls.Add(new Label { Text = "Link to Employee:", Location = new Point(20, yPos), Width = labelWidth });

            _lblSelectedEmployee = new Label
            {
                Text      = "(None)",
                Location  = new Point(controlLeft, yPos + 3),
                Width     = 215,
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(120, 120, 120)
            };
            Controls.Add(_lblSelectedEmployee);

            _btnBrowseEmployee = new Button
            {
                Text     = "Browse...",
                Location = new Point(controlLeft + 220, yPos - 1),
                Width    = 80,
                Height   = 26,
                Font     = new Font("Segoe UI", 9F)
            };
            _btnBrowseEmployee.Click += BtnBrowseEmployee_Click;
            Controls.Add(_btnBrowseEmployee);

            _btnClearEmployee = new Button
            {
                Text     = "Clear",
                Location = new Point(controlLeft + 308, yPos - 1),
                Width    = 65,
                Height   = 26,
                Font     = new Font("Segoe UI", 9F)
            };
            _btnClearEmployee.Click += (s, e) =>
            {
                _selectedEmpId = null;
                _lblSelectedEmployee.Text      = "(None)";
                _lblSelectedEmployee.Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic);
                _lblSelectedEmployee.ForeColor = Color.FromArgb(120, 120, 120);
            };
            Controls.Add(_btnClearEmployee);
            yPos += 35;

            // ── Flags ─────────────────────────────────────────────────────────
            // Only a Super Admin may grant/revoke Developer access — a Developer
            // who is not also a Super Admin must not be able to promote themselves
            // or anyone else. The checkbox is shown read-only (disabled) so a
            // non-Super Admin can still see whether an account is a Developer.
            chkIsDeveloper = new CheckBox
            {
                Text     = AppSession.IsSuperAdmin
                    ? "Developer (Full System Access)"
                    : "Developer (Full System Access) — Super Admin only",
                Location = new Point(controlLeft, yPos),
                Width    = controlWidth,
                Enabled  = AppSession.IsSuperAdmin
            };
            Controls.Add(chkIsDeveloper);
            yPos += 30;

            chkIsActive = new CheckBox { Text = "Active", Location = new Point(controlLeft, yPos), Width = controlWidth, Checked = true };
            Controls.Add(chkIsActive);
            yPos += 30;

            chkMustChangePassword = new CheckBox { Text = "Require password change on next login", Location = new Point(controlLeft, yPos), Width = controlWidth, Checked = !_isEdit };
            Controls.Add(chkMustChangePassword);
            yPos += 35;

            // ── Roles ─────────────────────────────────────────────────────────
            var grpRoles = new GroupBox
            {
                Text     = "Roles",
                Location = new Point(20, yPos),
                Width    = 472,
                Height   = 190,
                Font     = new Font("Segoe UI", 9F)
            };
            _pnlRoles = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            grpRoles.Controls.Add(_pnlRoles);
            Controls.Add(grpRoles);
            yPos += 200;

            // ── Info + buttons ────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = _isEdit ? "Note: Password can be reset using the Reset Password button." : "A temporary password will be generated and displayed after creation.",
                Location  = new Point(20, yPos),
                Width     = 470,
                Height    = 30,
                ForeColor = Color.FromArgb(100, 100, 100),
                Font      = new Font("Segoe UI", 8.5F)
            });
            yPos += 40;

            btnSave = new Button { Text = "Save", Location = new Point(290, yPos), Width = 90 };
            btnSave.Click += BtnSave_Click;
            Controls.Add(btnSave);

            btnCancel = new Button { Text = "Cancel", Location = new Point(390, yPos), Width = 90, DialogResult = DialogResult.Cancel };
            Controls.Add(btnCancel);

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private void BtnBrowseEmployee_Click(object sender, EventArgs e)
        {
            using (var dlg = new EmployeeSelectorDialog(_allEmployees, _selectedEmpId))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedEmployee != null)
                {
                    var emp = dlg.SelectedEmployee;
                    _selectedEmpId = emp.EmpId;
                    _lblSelectedEmployee.Text      = $"{emp.Name}  ({emp.EmployeeNumber})";
                    _lblSelectedEmployee.Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                    _lblSelectedEmployee.ForeColor = Color.FromArgb(0, 120, 60);
                    AutoFillEmail(emp);
                }
            }
        }

        private static bool IsPlaceholderEmail(string email)
            => System.Text.RegularExpressions.Regex.IsMatch(
                   email ?? "", @"^user\d+@yakult\.local$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>
        /// Fills the Email box from the linked employee: Personal Email first, then Branch Email.
        /// Only when the box is empty or still a userNNN@yakult.local placeholder, so a
        /// hand-typed address is never replaced.
        /// </summary>
        private void AutoFillEmail(EmployeeLookupResult emp)
        {
            if (emp == null) return;

            string current = (txtEmail.Text ?? "").Trim();
            if (current.Length > 0 && !IsPlaceholderEmail(current)) return;

            string pick = !string.IsNullOrWhiteSpace(emp.PrimaryEmail) ? emp.PrimaryEmail
                        : !string.IsNullOrWhiteSpace(emp.BranchEmail)  ? emp.BranchEmail
                        : null;
            if (pick != null) txtEmail.Text = pick.Trim();
        }

        // ── Data loading ──────────────────────────────────────────────────────

        private async void LoadData()
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    bool hasDeptEmailTable;
                    using (var chk = new SqlCommand("SELECT CAST(OBJECT_ID('dbo.DepartmentEmail') AS INT)", con))
                    {
                        var res = await chk.ExecuteScalarAsync();
                        hasDeptEmailTable = res != null && res != DBNull.Value;
                    }

                    string deptEmailSelect = hasDeptEmailTable ? "dea.EmailAddress AS DepartmentEmail," : "NULL AS DepartmentEmail,";
                    string deptEmailJoin   = hasDeptEmailTable
                        ? @"LEFT JOIN dbo.DepartmentEmail de
                                   ON de.CompanyName = c.Name AND de.DepartmentName = d.Name
                            LEFT JOIN dbo.EmailAddress dea
                                   ON dea.EmailId = de.EmailAddressId"
                        : "";

                    string empSql = $@"
                        SELECT
                            e.EmpId, e.EmployeeNumber, e.Name, e.Position,
                            c.Name AS CompanyName,
                            d.Name AS DepartmentName,
                            b.Name AS BranchName,
                            {deptEmailSelect}
                            branch_ea.EmailAddress AS BranchEmail,
                            emp_ea.EmailAddress    AS PrimaryEmail
                        FROM dbo.Employee e
                        LEFT JOIN dbo.Company    c   ON e.ComId    = c.ComId
                        LEFT JOIN dbo.Branch     b   ON e.BranchId = b.BranchId
                        LEFT JOIN dbo.Department d   ON e.DeptId   = d.DeptId
                        LEFT JOIN dbo.EmployeeEmail ee
                               ON ee.EmpId = e.EmpId AND ee.IsPrimary = 1 AND ee.IsActive = 1
                        LEFT JOIN dbo.EmailAddress emp_ea
                               ON emp_ea.EmailId = ee.EmailId AND emp_ea.IsActive = 1
                        {deptEmailJoin}
                        LEFT JOIN dbo.DepartmentAccount da_br
                               ON da_br.CompanyName    = c.Name
                              AND da_br.DepartmentName = d.Name
                              AND da_br.BranchName     = b.Name
                        LEFT JOIN dbo.EmailAddress branch_ea
                               ON branch_ea.EmailId = da_br.EmailAddressId
                        LEFT JOIN dbo.ArchiveStatus arc
                               ON arc.EntityType = 'Employee' AND arc.EntityId = e.EmpId AND arc.IsArchived = 1
                        WHERE e.Active = 1
                          AND arc.ArchiveId IS NULL
                        ORDER BY e.Name";

                    using (var cmd = new SqlCommand(empSql, con) { CommandTimeout = 120 })
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _allEmployees.Add(new EmployeeLookupResult
                            {
                                EmpId          = reader.GetInt32(0),
                                EmployeeNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Name           = reader.GetString(2),
                                Position       = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                CompanyName    = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                DepartmentName = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                BranchName     = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                DepartmentEmail = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                BranchEmail    = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                PrimaryEmail   = reader.IsDBNull(9) ? "" : reader.GetString(9),
                            });
                        }
                    }

                    if (!_isEdit && _selectedEmpId.HasValue)
                    {
                        var preselected = _allEmployees.FirstOrDefault(e => e.EmpId == _selectedEmpId.Value);
                        if (preselected != null)
                        {
                            _lblSelectedEmployee.Text = $"{preselected.Name}  ({preselected.EmployeeNumber})";
                            _lblSelectedEmployee.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold);
                            _lblSelectedEmployee.ForeColor = System.Drawing.Color.FromArgb(0, 120, 60);
                            AutoFillEmail(preselected);
                        }
                    }

                    const string rolesSql = "SELECT RoleId, RoleName, Description FROM Role WHERE IsActive = 1 ORDER BY RoleName";
                    using (var cmd = new SqlCommand(rolesSql, con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _roles.Add(new RoleLookup
                            {
                                RoleId      = reader.GetInt32(0),
                                RoleName    = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? "" : reader.GetString(2)
                            });
                        }
                    }

                    if (_isEdit)
                    {
                        const string userRolesSql = "SELECT RoleId FROM UserRole WHERE UserId = @UserId";
                        using (var cmd = new SqlCommand(userRolesSql, con))
                        {
                            cmd.Parameters.AddWithValue("@UserId", _user.UserId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                                while (await reader.ReadAsync())
                                    _userRoleIds.Add(reader.GetInt32(0));
                        }

                        // Re-fetch IsDeveloper fresh from the DB rather than trusting the
                        // list page's cached row — privilege approvals (via the hidden
                        // panel) apply directly to the database and don't push updates
                        // back into the list's in-memory data, so a stale cached value
                        // would otherwise show the wrong checkbox state here.
                        using (var cmd = new SqlCommand("SELECT IsDeveloper FROM [User] WHERE UserId = @UserId", con))
                        {
                            cmd.Parameters.AddWithValue("@UserId", _user.UserId);
                            var result = await cmd.ExecuteScalarAsync();
                            if (result != null && result != DBNull.Value)
                                _user.IsDeveloper = (bool)result;
                        }
                    }
                }

                // Populate roles panel
                int itemTop = 0;
                foreach (var role in _roles)
                {
                    bool evenRow = (itemTop / 44) % 2 == 0;
                    var row = new Panel
                    {
                        Location  = new Point(0, itemTop),
                        Width     = 460,
                        Height    = 44,
                        Tag       = role.RoleId,
                        BackColor = evenRow ? Color.White : Color.FromArgb(248, 249, 252)
                    };
                    var chk = new CheckBox { Text = role.RoleName,       Location = new Point(8, 5),  Width = 444, Font = new Font("Segoe UI", 9.5F), Tag = role.RoleId };
                    var lbl = new Label    { Text = role.Description ?? "", Location = new Point(28, 25), Width = 424, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(110, 110, 110) };
                    row.Controls.Add(chk);
                    row.Controls.Add(lbl);
                    _pnlRoles.Controls.Add(row);
                    itemTop += 44;
                }

                if (_isEdit) PopulateFields();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateFields()
        {
            txtUsername.Text              = _user.Username;
            txtEmail.Text                 = _user.Email ?? "";
            chkIsDeveloper.Checked        = _user.IsDeveloper;
            chkIsActive.Checked           = _user.IsActive;
            chkMustChangePassword.Checked = _user.MustChangePassword;

            if (_user.EmpId.HasValue)
            {
                _selectedEmpId = _user.EmpId.Value;
                var emp = _allEmployees.FirstOrDefault(e => e.EmpId == _user.EmpId.Value);
                if (emp != null)
                {
                    _lblSelectedEmployee.Text      = $"{emp.Name}  ({emp.EmployeeNumber})";
                    _lblSelectedEmployee.Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                    _lblSelectedEmployee.ForeColor = Color.FromArgb(0, 120, 60);
                    AutoFillEmail(emp);   // replaces a userNNN@yakult.local placeholder shown in the box
                }
            }

            foreach (Panel row in _pnlRoles.Controls.OfType<Panel>())
            {
                if (row.Tag is int roleId && _userRoleIds.Contains(roleId))
                    foreach (CheckBox chk in row.Controls.OfType<CheckBox>())
                        chk.Checked = true;
            }
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                MessageBox.Show("Please enter a username.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtUsername.Focus();
                return;
            }

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    int? empId = _selectedEmpId;

                    if (empId.HasValue && empId.Value <= 0)
                        empId = null;

                    string username = (txtUsername.Text ?? "").Trim();
                    string email = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text.Trim();

                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        if (email == "0" || !UserAccountManagementPage.IsValidEmailAddress(email))
                        {
                            MessageBox.Show("Please enter a valid email address.", "Validation",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            txtEmail.Focus();
                            DialogResult = DialogResult.None;
                            return;
                        }

                        if (await UserAccountManagementPage.EmailExistsAsync(con, email, _isEdit ? (int?)_user.UserId : null))
                        {
                            MessageBox.Show("Email address already exists. Please enter a different email.", "Validation",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            txtEmail.Focus();
                            DialogResult = DialogResult.None;
                            return;
                        }
                    }

                    if (_isEdit)
                    {
                        // Check if username changed and if new username already exists
                        if (username != _user.Username)
                        {
                            const string checkSql = "SELECT COUNT(*) FROM [User] WHERE Name = @Username AND UserId <> @UserId";
                            using (var cmd = new SqlCommand(checkSql, con))
                            {
                                cmd.Parameters.AddWithValue("@Username", username);
                                cmd.Parameters.AddWithValue("@UserId", _user.UserId);
                                int count = (int)await cmd.ExecuteScalarAsync();
                                if (count > 0)
                                {
                                    MessageBox.Show("Username already exists. Please choose a different username.", "Validation",
                                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    txtUsername.Focus();
                                    DialogResult = DialogResult.None;
                                    return;
                                }
                            }
                        }

                        // A Developer account's employee link (set, changed or cleared) needs a
                        // second stage so it can't be rebound to someone else by accident.
                        if (_user.IsDeveloper && _user.EmpId != empId
                            && !await ConfirmDeveloperLinkChangeAsync(con))
                        {
                            DialogResult = DialogResult.None;
                            return;
                        }

                        // Update user
                        const string sql = @"
                            UPDATE [User]
                            SET Name = @Username,
                                EmailAddress = @Email,
                                EmpId = @EmpId,
                                IsActive = @IsActive,
                                MustChangePassword = @MustChangePassword
                            WHERE UserId = @UserId";

                        using (var cmd = new SqlCommand(sql, con))
                        {
                            cmd.Parameters.AddWithValue("@Username", username);
                            cmd.Parameters.AddWithValue("@UserId", _user.UserId);
                            cmd.Parameters.AddWithValue("@Email", (object)email ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@EmpId", (object)empId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@IsActive", chkIsActive.Checked);
                            cmd.Parameters.AddWithValue("@MustChangePassword", chkMustChangePassword.Checked);

                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Update roles
                        await UpdateUserRoles(con, _user.UserId);

                        MessageBox.Show("User account updated successfully.", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        DialogResult = DialogResult.OK;
                    }
                    else
                    {
                        // Check if username already exists
                        const string checkSql = "SELECT COUNT(*) FROM [User] WHERE Name = @Username";
                        using (var cmd = new SqlCommand(checkSql, con))
                        {
                            cmd.Parameters.AddWithValue("@Username", username);
                            int count = (int)await cmd.ExecuteScalarAsync();
                            if (count > 0)
                            {
                                MessageBox.Show("Username already exists. Please choose a different username.", "Validation",
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                txtUsername.Focus();
                                DialogResult = DialogResult.None;
                                return;
                            }
                        }

                        // Generate temporary password
                        string tempPassword = PasswordHelper.GenerateTemporaryPassword();
                        byte[] salt = PasswordHelper.GenerateSalt();
                        byte[] hash = PasswordHelper.HashPassword(tempPassword, salt);

                        // Create user
                        const string sql = @"
                            INSERT INTO [User] (Name, EmailAddress, EmpId, PasswordHash, PasswordSalt, IsDeveloper, IsActive, MustChangePassword, DateCreated)
                            VALUES (@Username, @Email, @EmpId, @Hash, @Salt, 0, @IsActive, @MustChangePassword, GETDATE());
                            SELECT SCOPE_IDENTITY();";

                        int newUserId;
                        using (var cmd = new SqlCommand(sql, con))
                        {
                            cmd.Parameters.AddWithValue("@Username", username);
                            cmd.Parameters.AddWithValue("@Email", (object)email ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@EmpId", (object)empId ?? DBNull.Value);
                            cmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, hash.Length) { Value = hash });
                            cmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, salt.Length) { Value = salt });
                            cmd.Parameters.AddWithValue("@IsActive", chkIsActive.Checked);
                            cmd.Parameters.AddWithValue("@MustChangePassword", chkMustChangePassword.Checked);

                            newUserId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        // Add roles
                        await UpdateUserRoles(con, newUserId);

                        using (var dlg = new TemporaryPasswordDialog(username, tempPassword))
                        {
                            dlg.ShowDialog(this);
                        }
                        DialogResult = DialogResult.OK;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save user account: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
            }
        }

        private async System.Threading.Tasks.Task UpdateUserRoles(SqlConnection con, int userId)
        {
            // Delete existing roles
            const string deleteSql = "DELETE FROM UserRole WHERE UserId = @UserId";
            using (var cmd = new SqlCommand(deleteSql, con))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                await cmd.ExecuteNonQueryAsync();
            }

            // Insert checked roles
            foreach (Panel row in _pnlRoles.Controls.OfType<Panel>())
            {
                foreach (CheckBox chk in row.Controls.OfType<CheckBox>())
                {
                    if (chk.Checked && chk.Tag is int roleId)
                    {
                        const string insertSql = "INSERT INTO UserRole (UserId, RoleId) VALUES (@UserId, @RoleId)";
                        using (var cmd = new SqlCommand(insertSql, con))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.Parameters.AddWithValue("@RoleId", roleId);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }


        private class RoleLookup
        {
            public int RoleId { get; set; }
            public string RoleName { get; set; }
            public string Description { get; set; }
        }


        /// <summary>
        /// Second stage for changing a Developer account's employee link: asks for the current
        /// system password of that Developer account (dbo.[User]), plus the password of the
        /// database the app is connected to. Under Windows Authentication there is no database
        /// password, so only the account password is asked. Returns true only when all match.
        /// </summary>
        private async System.Threading.Tasks.Task<bool> ConfirmDeveloperLinkChangeAsync(SqlConnection con)
        {
            string dbPassword = null;
            bool   windowsAuth = false;
            try
            {
                var csb = new SqlConnectionStringBuilder(Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString);
                windowsAuth = csb.IntegratedSecurity;
                dbPassword  = csb.Password;
            }
            catch
            {
            }

            bool askDbPassword = !windowsAuth && !string.IsNullOrEmpty(dbPassword);

            string enteredDbPassword, enteredUserPassword;
            using (var dlg = new DeveloperLinkConfirmDialog(_user.Username, askDbPassword))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                enteredDbPassword   = dlg.DatabasePassword;
                enteredUserPassword = dlg.AccountPassword;
            }

            byte[] storedHash = null, storedSalt = null;
            using (var cmd = new SqlCommand("SELECT PasswordHash, PasswordSalt FROM dbo.[User] WHERE UserId = @UserId", con))
            {
                cmd.Parameters.AddWithValue("@UserId", _user.UserId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        storedHash = reader.IsDBNull(0) ? null : (byte[])reader[0];
                        storedSalt = reader.IsDBNull(1) ? null : (byte[])reader[1];
                    }
                }
            }

            // Evaluate both before reporting, and don't say which one failed.
            bool dbOk   = !askDbPassword || FixedTimeEquals(enteredDbPassword, dbPassword);
            bool userOk = PasswordHelper.VerifyPassword(enteredUserPassword, storedHash, storedSalt);
            if (dbOk && userOk) return true;

            MessageBox.Show(
                (askDbPassword
                    ? "The database password or the account password is incorrect."
                    : $"The password of \"{_user.Username}\" is incorrect.") +
                "\n\nThe employee link was not changed.",
                "Verification Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            byte[] x = System.Text.Encoding.UTF8.GetBytes(a ?? "");
            byte[] y = System.Text.Encoding.UTF8.GetBytes(b ?? "");
            int diff = x.Length ^ y.Length;
            for (int i = 0; i < Math.Min(x.Length, y.Length); i++)
                diff |= x[i] ^ y[i];
            return diff == 0;
        }

        private sealed class DeveloperLinkConfirmDialog : Form
        {
            private readonly TextBox _txtDbPassword;
            private readonly TextBox _txtAccountPassword;

            public string DatabasePassword => _txtDbPassword?.Text ?? "";
            public string AccountPassword  => _txtAccountPassword.Text;

            public DeveloperLinkConfirmDialog(string username, bool askDbPassword)
            {
                Text = "Confirm Developer Account Change";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;

                var lblInfo = new Label
                {
                    Text = $"\"{username}\" is a Developer account. To change which employee it is linked to, enter " +
                           (askDbPassword
                               ? "the password of the database this system is connected to and the current password of this account."
                               : "the current password of this account."),
                    Left = 16,
                    Top = 14,
                    Width = 488,
                    Height = 48,
                    AutoSize = false
                };
                Controls.Add(lblInfo);

                int y = 72;
                if (askDbPassword)
                {
                    Controls.Add(new Label { Text = "Database password:", Left = 16, Top = y, Width = 488, AutoSize = false });
                    _txtDbPassword = new TextBox { Left = 16, Top = y + 20, Width = 488, UseSystemPasswordChar = true };
                    Controls.Add(_txtDbPassword);
                    y += 52;
                }

                Controls.Add(new Label { Text = $"Password of \"{username}\":", Left = 16, Top = y, Width = 488, AutoSize = false, AutoEllipsis = true });
                _txtAccountPassword = new TextBox { Left = 16, Top = y + 20, Width = 488, UseSystemPasswordChar = true };
                Controls.Add(_txtAccountPassword);
                y += 62;

                var btnOk     = new Button { Text = "Confirm", Left = 328, Top = y, Width = 84, DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Cancel",  Left = 420, Top = y, Width = 84, DialogResult = DialogResult.Cancel };
                Controls.Add(btnOk);
                Controls.Add(btnCancel);
                ClientSize = new Size(520, y + 40);

                AcceptButton = btnOk;
                CancelButton = btnCancel;
            }
        }

        private sealed class TemporaryPasswordDialog : Form
        {
            private readonly string _password;

            public TemporaryPasswordDialog(string username, string tempPassword)
            {
                _password = tempPassword ?? string.Empty;

                Text = "Success";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                ClientSize = new Size(520, 210);

                var lblInfo = new Label
                {
                    Text = "User account created successfully.",
                    Left = 16,
                    Top = 16,
                    Width = 480,
                    AutoSize = false
                };

                var lblUsername = new Label
                {
                    Text = $"Username: {username}",
                    Left = 16,
                    Top = 48,
                    Width = 480,
                    AutoSize = false
                };

                var lblPwd = new Label
                {
                    Text = "Temporary Password:",
                    Left = 16,
                    Top = 82,
                    Width = 140,
                    AutoSize = false
                };

                var txtPwd = new TextBox
                {
                    Left = 160,
                    Top = 78,
                    Width = 250,
                    ReadOnly = true,
                    Text = _password
                };

                var btnCopy = new Button
                {
                    Text = "Copy",
                    Left = 418,
                    Top = 76,
                    Width = 80,
                    Height = 26
                };
                btnCopy.Click += (s, e) =>
                {
                    try
                    {
                        Clipboard.SetText(_password);
                    }
                    catch
                    {
                    }
                };

                var lblNote = new Label
                {
                    Text = "Please provide these credentials to the user securely.",
                    Left = 16,
                    Top = 120,
                    Width = 480,
                    Height = 30,
                    AutoSize = false
                };

                var btnOk = new Button
                {
                    Text = "OK",
                    Left = 418,
                    Top = 162,
                    Width = 80,
                    DialogResult = DialogResult.OK
                };

                Controls.AddRange(new Control[] { lblInfo, lblUsername, lblPwd, txtPwd, btnCopy, lblNote, btnOk });
                AcceptButton = btnOk;
            }
        }
    }

    // Standalone employee search/selector dialog opened via Browse button
    internal sealed class EmployeeSelectorDialog : Form
    {
        public EmployeeLookupResult SelectedEmployee { get; private set; }

        private readonly List<EmployeeLookupResult> _all;
        private List<EmployeeLookupResult> _filtered = new List<EmployeeLookupResult>();

        private TextBox  _txtSearch;
        private DataGridView _dgv;
        private System.Windows.Forms.Button _btnFirst, _btnPrev, _btnNext, _btnLast;
        private Label    _lblPageInfo;
        private System.Windows.Forms.Button _btnSelect, _btnCancel;

        private int    _page    = 1;
        private const int PageSize = 15;
        private string _sortCol = "Name";
        private bool   _sortAsc = true;
        private readonly Dictionary<string, HashSet<string>> _filters =
            new Dictionary<string, HashSet<string>>();

        public EmployeeSelectorDialog(IEnumerable<EmployeeLookupResult> employees, int? preSelectedEmpId)
        {
            _all = employees.ToList();

            Text            = "Select Employee";
            Size            = new Size(860, 560);
            MinimumSize     = new Size(620, 400);
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = true;
            MinimizeBox     = false;
            WindowState     = FormWindowState.Maximized;

            BuildUI();
            ApplyFilters();

            if (preSelectedEmpId.HasValue)
                PreSelectRow(preSelectedEmpId.Value);
        }

        private void BuildUI()
        {
            // ── Search bar ────────────────────────────────────────────────────
            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 8, 8, 0) };

            pnlTop.Controls.Add(new Label
            {
                Text      = "Search:",
                Location  = new Point(8, 12),
                Width     = 55,
                Font      = new Font("Segoe UI", 9F)
            });

            _txtSearch = new TextBox { Location = new Point(68, 8), Width = 500, Font = new Font("Segoe UI", 9F) };
            _txtSearch.TextChanged += (s, e) => { _page = 1; ApplyFilters(); };
            pnlTop.Controls.Add(_txtSearch);

            Controls.Add(pnlTop);

            // ── Grid ──────────────────────────────────────────────────────────
            _dgv = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                AutoGenerateColumns   = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                ReadOnly              = true,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = false,
                RowHeadersVisible     = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
            };
            Helpers.UiFactory.StyleGrid(_dgv);

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmpId",          Visible = false, DataPropertyName = "EmpId" });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeNumber", HeaderText = "Emp #",        DataPropertyName = "EmployeeNumber",  FillWeight = 7,  SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name",           HeaderText = "Name",         DataPropertyName = "Name",            FillWeight = 16, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Position",       HeaderText = "Position",     DataPropertyName = "Position",        FillWeight = 13, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Company",        HeaderText = "Company",      DataPropertyName = "CompanyName",     FillWeight = 8,  SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Department",     HeaderText = "Department",   DataPropertyName = "DepartmentName",  FillWeight = 13, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Branch",         HeaderText = "Branch",       DataPropertyName = "BranchName",      FillWeight = 9,  SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "DeptEmail",      HeaderText = "Dept Email",   DataPropertyName = "DepartmentEmail", FillWeight = 12, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "BranchEmail",    HeaderText = "Branch Email", DataPropertyName = "BranchEmail",     FillWeight = 12, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "PersonalEmail",  HeaderText = "Personal Email", DataPropertyName = "PrimaryEmail",  FillWeight = 12, SortMode = DataGridViewColumnSortMode.Programmatic });

            _dgv.CellDoubleClick         += (s, e) => { if (e.RowIndex >= 0) CommitSelection(); };
            _dgv.ColumnHeaderMouseClick  += Dgv_ColumnHeaderMouseClick;
            _dgv.CellPainting            += Dgv_CellPainting;
            Controls.Add(_dgv);

            // ── Pagination bar ────────────────────────────────────────────────
            var pnlPage = new Panel { Dock = DockStyle.Bottom, Height = 36 };

            _btnFirst = new System.Windows.Forms.Button { Text = "<<", Width = 40, Height = 24, Left = 8,   Top = 6 };
            _btnPrev  = new System.Windows.Forms.Button { Text = "<",  Width = 40, Height = 24, Left = 52,  Top = 6 };
            _lblPageInfo = new Label { Width = 260, Height = 24, Left = 98, Top = 10, Font = new Font("Segoe UI", 8.5F) };
            _btnNext  = new System.Windows.Forms.Button { Text = ">",  Width = 40, Height = 24, Left = 364, Top = 6 };
            _btnLast  = new System.Windows.Forms.Button { Text = ">>", Width = 40, Height = 24, Left = 408, Top = 6 };

            _btnFirst.Click += (s, e) => { _page = 1; UpdateGrid(); };
            _btnPrev.Click  += (s, e) => { if (_page > 1) { _page--; UpdateGrid(); } };
            _btnNext.Click  += (s, e) =>
            {
                int total = (int)Math.Ceiling(_filtered.Count / (double)PageSize);
                if (_page < total) { _page++; UpdateGrid(); }
            };
            _btnLast.Click += (s, e) =>
            {
                _page = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
                UpdateGrid();
            };

            _btnSelect = new System.Windows.Forms.Button
            {
                Text     = "Select",
                Width    = 90,
                Height   = 26,
                Font     = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnSelect.FlatAppearance.BorderSize = 0;
            _btnSelect.Click += (s, e) => CommitSelection();

            _btnCancel = new System.Windows.Forms.Button
            {
                Text          = "Cancel",
                Width         = 80,
                Height        = 26,
                Font          = new Font("Segoe UI", 9F),
                DialogResult  = DialogResult.Cancel
            };

            pnlPage.Controls.AddRange(new Control[] { _btnFirst, _btnPrev, _lblPageInfo, _btnNext, _btnLast });

            // Anchor Select / Cancel to the right edge so they reposition on resize
            _btnSelect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSelect.Top    = 5;
            _btnCancel.Top    = 5;

            pnlPage.SizeChanged += (s, e) =>
            {
                _btnCancel.Left = pnlPage.Width - _btnCancel.Width - 8;
                _btnSelect.Left = _btnCancel.Left - _btnSelect.Width - 6;
            };

            pnlPage.Controls.Add(_btnSelect);
            pnlPage.Controls.Add(_btnCancel);
            Controls.Add(pnlPage);

            // Ensure search bar sits above the grid (DockStyle.Top processed in Add order)
            Controls.SetChildIndex(pnlPage, 0);
            Controls.SetChildIndex(_dgv,    1);
            Controls.SetChildIndex(pnlTop,  2);

            AcceptButton = _btnSelect;
            CancelButton = _btnCancel;
        }

        private void CommitSelection()
        {
            if (_dgv.SelectedRows.Count == 0) return;
            var empId = (int)_dgv.SelectedRows[0].Cells["EmpId"].Value;
            SelectedEmployee = _all.FirstOrDefault(e => e.EmpId == empId);
            if (SelectedEmployee != null) DialogResult = DialogResult.OK;
        }

        private void ApplyFilters()
        {
            var q = (_txtSearch?.Text ?? "").Trim().ToLower();

            _filtered = _all.Where(e =>
            {
                if (!string.IsNullOrEmpty(q))
                {
                    var haystack = string.Join(" ",
                        e.Name                    ?? "",
                        e.EmployeeNumber          ?? "",
                        e.Position                ?? "",
                        e.CompanyName             ?? "",
                        e.DepartmentName          ?? "",
                        e.BranchName              ?? "",
                        e.PrimaryEmail            ?? "").ToLower();

                    var tokens = q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (!tokens.All(t => haystack.Contains(t)))
                        return false;
                }

                if (_filters.TryGetValue("Position",   out var ps) && !ps.Contains(e.Position       ?? "", StringComparer.OrdinalIgnoreCase)) return false;
                if (_filters.TryGetValue("Company",    out var cs) && !cs.Contains(e.CompanyName    ?? "", StringComparer.OrdinalIgnoreCase)) return false;
                if (_filters.TryGetValue("Department", out var ds) && !ds.Contains(e.DepartmentName ?? "", StringComparer.OrdinalIgnoreCase)) return false;
                if (_filters.TryGetValue("Branch",     out var bs) && !bs.Contains(e.BranchName     ?? "", StringComparer.OrdinalIgnoreCase)) return false;

                return true;
            }).ToList();

            UpdateGrid();
        }

        private void UpdateGrid()
        {
            IEnumerable<EmployeeLookupResult> sorted = _filtered;
            switch (_sortCol)
            {
                case "EmployeeNumber": sorted = _sortAsc ? sorted.OrderBy(e => e.EmployeeNumber ?? "") : sorted.OrderByDescending(e => e.EmployeeNumber ?? ""); break;
                case "Name":           sorted = _sortAsc ? sorted.OrderBy(e => e.Name           ?? "") : sorted.OrderByDescending(e => e.Name           ?? ""); break;
                case "Position":       sorted = _sortAsc ? sorted.OrderBy(e => e.Position       ?? "") : sorted.OrderByDescending(e => e.Position       ?? ""); break;
                case "Company":        sorted = _sortAsc ? sorted.OrderBy(e => e.CompanyName    ?? "") : sorted.OrderByDescending(e => e.CompanyName    ?? ""); break;
                case "Department":     sorted = _sortAsc ? sorted.OrderBy(e => e.DepartmentName ?? "") : sorted.OrderByDescending(e => e.DepartmentName ?? ""); break;
                case "Branch":        sorted = _sortAsc ? sorted.OrderBy(e => e.BranchName      ?? "") : sorted.OrderByDescending(e => e.BranchName      ?? ""); break;
                case "DeptEmail":     sorted = _sortAsc ? sorted.OrderBy(e => e.DepartmentEmail ?? "") : sorted.OrderByDescending(e => e.DepartmentEmail ?? ""); break;
                case "BranchEmail":   sorted = _sortAsc ? sorted.OrderBy(e => e.BranchEmail     ?? "") : sorted.OrderByDescending(e => e.BranchEmail     ?? ""); break;
                case "PersonalEmail": sorted = _sortAsc ? sorted.OrderBy(e => e.PrimaryEmail    ?? "") : sorted.OrderByDescending(e => e.PrimaryEmail    ?? ""); break;
            }

            int totalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
            if (_page > totalPages) _page = totalPages;

            _dgv.DataSource = sorted
                .Skip((_page - 1) * PageSize).Take(PageSize)
                .Select(e => new { e.EmpId, e.EmployeeNumber, e.Name, e.Position, CompanyName = e.CompanyName, DepartmentName = e.DepartmentName, BranchName = e.BranchName, DepartmentEmail = e.DepartmentEmail, BranchEmail = e.BranchEmail, PrimaryEmail = e.PrimaryEmail })
                .ToList();

            _lblPageInfo.Text    = $"Page {_page} of {totalPages} ({_filtered.Count} employees)";
            _btnFirst.Enabled    = _page > 1;
            _btnPrev.Enabled     = _page > 1;
            _btnNext.Enabled     = _page < totalPages;
            _btnLast.Enabled     = _page < totalPages;
        }

        private void PreSelectRow(int empId)
        {
            foreach (DataGridViewRow row in _dgv.Rows)
            {
                if (row.Cells["EmpId"].Value is int id && id == empId)
                { row.Selected = true; _dgv.FirstDisplayedScrollingRowIndex = row.Index; break; }
            }
        }

        private void Dgv_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = _dgv.Columns[e.ColumnIndex];
            if (col.SortMode != DataGridViewColumnSortMode.Programmatic) return;

            if (e.X >= col.Width - 18) { ShowFilterPopup(col); return; }

            if (_sortCol == col.Name) _sortAsc = !_sortAsc;
            else { _sortCol = col.Name; _sortAsc = true; }

            foreach (DataGridViewColumn c in _dgv.Columns)
                c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            col.HeaderCell.SortGlyphDirection = _sortAsc
                ? System.Windows.Forms.SortOrder.Ascending
                : System.Windows.Forms.SortOrder.Descending;

            UpdateGrid();
        }

        private void ShowFilterPopup(DataGridViewColumn col)
        {
            List<string> values;
            switch (col.Name)
            {
                case "Position":   values = _all.Select(e => e.Position       ?? "").Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v).ToList(); break;
                case "Company":    values = _all.Select(e => e.CompanyName    ?? "").Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v).ToList(); break;
                case "Department": values = _all.Select(e => e.DepartmentName ?? "").Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v).ToList(); break;
                case "Branch":     values = _all.Select(e => e.BranchName     ?? "").Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v).ToList(); break;
                default: return;
            }

            _filters.TryGetValue(col.Name, out var current);
            var colRect  = _dgv.GetColumnDisplayRectangle(col.Index, false);
            var screenPt = _dgv.PointToScreen(new Point(colRect.Left, _dgv.ColumnHeadersHeight));

            using (var popup = new Helpers.ColumnFilterPopup(col.HeaderText, values, current))
            {
                popup.Location = screenPt;
                var screen = Screen.FromPoint(screenPt).WorkingArea;
                if (popup.Right  > screen.Right)  popup.Left = Math.Max(screen.Left, screen.Right - popup.Width);
                if (popup.Bottom > screen.Bottom) popup.Top  = Math.Max(screen.Top,  screenPt.Y   - popup.Height - _dgv.ColumnHeadersHeight);
                popup.ShowDialog(this);

                if (popup.Action == Helpers.ColumnFilterPopup.PopupAction.SortAscending)
                { _sortCol = col.Name; _sortAsc = true;  col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Ascending;  ApplyFilters(); }
                else if (popup.Action == Helpers.ColumnFilterPopup.PopupAction.SortDescending)
                { _sortCol = col.Name; _sortAsc = false; col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Descending; ApplyFilters(); }
                else if (popup.Action == Helpers.ColumnFilterPopup.PopupAction.Filter)
                {
                    if (popup.SelectedValues == null || popup.SelectedValues.Count == 0 || popup.SelectedValues.Count >= values.Count)
                        _filters.Remove(col.Name);
                    else
                        _filters[col.Name] = popup.SelectedValues;
                    _page = 1;
                    ApplyFilters();
                }
            }
        }

        private void Dgv_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 || e.ColumnIndex < 0) return;
            var col = _dgv.Columns[e.ColumnIndex];
            if (col == null) return;

            e.Handled = true;
            e.Graphics.FillRectangle(Brushes.White, e.CellBounds);
            using (var b = new SolidBrush(e.CellStyle.BackColor))
                e.Graphics.FillRectangle(b, new Rectangle(e.CellBounds.X, e.CellBounds.Y, e.CellBounds.Width - 1, e.CellBounds.Height - 1));
            using (var p = new Pen(Color.White, 1))
            {
                e.Graphics.DrawLine(p, e.CellBounds.Right - 1, e.CellBounds.Top,    e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                e.Graphics.DrawLine(p, e.CellBounds.Left,      e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }

            bool sortable = col.SortMode == DataGridViewColumnSortMode.Programmatic;
            var  rect     = e.CellBounds;

            using (var tb = new SolidBrush(Color.White))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                if (sortable)
                {
                    rect.Width -= 34;
                    int gx = e.CellBounds.Right - 32, gy = e.CellBounds.Y + (e.CellBounds.Height - 8) / 2;
                    Color ac = col.HeaderCell.SortGlyphDirection != System.Windows.Forms.SortOrder.None ? Color.White : Color.FromArgb(160, 255, 255, 255);
                    using (var ap = new Pen(ac, 2))
                    {
                        if      (col.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Ascending)  { e.Graphics.DrawLine(ap, gx, gy+6, gx+5, gy);   e.Graphics.DrawLine(ap, gx+5, gy,   gx+10, gy+6); }
                        else if (col.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Descending) { e.Graphics.DrawLine(ap, gx, gy,   gx+5, gy+6); e.Graphics.DrawLine(ap, gx+5, gy+6, gx+10, gy); }
                        else { e.Graphics.DrawLine(ap, gx+2, gy+2, gx+5, gy-1); e.Graphics.DrawLine(ap, gx+5, gy-1, gx+8, gy+2);
                               e.Graphics.DrawLine(ap, gx+2, gy+4, gx+5, gy+7); e.Graphics.DrawLine(ap, gx+5, gy+7, gx+8, gy+4); }
                    }

                    if (col.Name == "Position" || col.Name == "Company" || col.Name == "Department" || col.Name == "Branch")
                    {
                        bool active = _filters.ContainsKey(col.Name);
                        int  fx = e.CellBounds.Right - 14, fy = e.CellBounds.Y + e.CellBounds.Height / 2;
                        using (var fb = new SolidBrush(active ? Color.FromArgb(255, 230, 80) : Color.FromArgb(140, 255, 255, 255)))
                        {
                            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                            e.Graphics.FillPolygon(fb, new PointF[] { new PointF(fx, fy-4), new PointF(fx+9, fy-4), new PointF(fx+4, fy+3) });
                            e.Graphics.SmoothingMode = SmoothingMode.Default;
                        }
                    }
                }
                e.Graphics.DrawString(col.HeaderText, e.CellStyle.Font ?? _dgv.Font, tb, rect, fmt);
            }
        }
    }

    // Shared employee lookup result used by UserAccountEditDialog + EmployeeSelectorDialog
    internal sealed class EmployeeLookupResult
    {
        public int    EmpId           { get; set; }
        public string Name            { get; set; }
        public string EmployeeNumber  { get; set; }
        public string Position        { get; set; }
        public string CompanyName     { get; set; }
        public string DepartmentName  { get; set; }
        public string BranchName      { get; set; }
        public string DepartmentEmail { get; set; }
        public string BranchEmail     { get; set; }
        public string PrimaryEmail    { get; set; }
    }

    // Dialog for displaying temporary password after reset with copy functionality
    internal sealed class PasswordResetSuccessDialog : Form
    {
        private readonly string _password;

        public PasswordResetSuccessDialog(string username, string tempPassword)
        {
            _password = tempPassword ?? string.Empty;

            Text = "Password Reset";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 230);
            Font = new Font("Segoe UI", 9.5F);

            var lblInfo = new Label
            {
                Text = "Password reset successfully.",
                Left = 16,
                Top = 16,
                Width = 480,
                Height = 20,
                AutoSize = false,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };

            var lblUsername = new Label
            {
                Text = $"Username: {username}",
                Left = 16,
                Top = 48,
                Width = 480,
                Height = 20,
                AutoSize = false
            };

            var lblPwd = new Label
            {
                Text = "Temporary Password:",
                Left = 16,
                Top = 82,
                Width = 140,
                AutoSize = false
            };

            var txtPwd = new TextBox
            {
                Left = 160,
                Top = 78,
                Width = 250,
                ReadOnly = true,
                Text = _password,
                Font = new Font("Consolas", 10F)
            };

            var btnCopyPwd = new System.Windows.Forms.Button
            {
                Text = "Copy",
                Left = 418,
                Top = 76,
                Width = 80,
                Height = 30
            };
            btnCopyPwd.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(_password);
                    MessageBox.Show("Password copied to clipboard.", "Copied",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var lblNote = new Label
            {
                Text = "Please provide this password to the user securely.\nThey will be required to change it on next login.",
                Left = 16,
                Top = 120,
                Width = 480,
                Height = 40,
                AutoSize = false
            };

            var btnOk = new System.Windows.Forms.Button
            {
                Text = "OK",
                Left = 418,
                Top = 180,
                Width = 80,
                Height = 32,
                DialogResult = DialogResult.OK
            };

            Controls.AddRange(new Control[] { lblInfo, lblUsername, lblPwd, txtPwd, btnCopyPwd, lblNote, btnOk });
            AcceptButton = btnOk;
        }
    }
}

