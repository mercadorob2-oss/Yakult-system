using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Pages.Admin.AccountManagement
{
    public partial class RoleManagementPage : UserControl
    {
        private SplitContainer splitMain;
        private Panel pnlEmployeeTop;
        private TextBox txtSearch;
        private Button btnRefreshEmployees;
        private DataGridView dgvEmployees;

        private readonly string _connectionString;
        private readonly BindingSource _employeeBindingSource = new BindingSource();
        private List<EmployeeAccountRow> _allEmployees = new List<EmployeeAccountRow>();

        private Panel pnlUserHeader;
        private Label lblUserName;
        private Label lblUserEmail;
        private Label lblIsActive;
        private GroupBox grpRoles;
        private CheckedListBox clbRoles;
        private Panel pnlActions;
        private Button btnCreateUser;
        private Button btnSaveRoles;

        public RoleManagementPage()
        {
            _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            InitializeComponent();
            BuildUi();
            SetRightPanelEnabled(false);

            WireEvents();
            _ = LoadEmployeesAsync();
        }

        private void InitializeComponent()
        {
        }

        private void BuildUi()
        {
            SuspendLayout();
            Controls.Clear();

            Dock = DockStyle.Fill;
            BackColor = Color.White;

            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6
            };
            SplitContainerUtil.BindSafeSplitterDistance(splitMain, () => 380);

            BuildLeftPanel(splitMain.Panel1);
            BuildRightPanel(splitMain.Panel2);

            Controls.Add(splitMain);
            ResumeLayout(performLayout: true);
        }

        private void BuildLeftPanel(Control parent)
        {
            pnlEmployeeTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.White
            };

            txtSearch = new TextBox
            {
                Location = new Point(10, 12),
                Width = 250,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            btnRefreshEmployees = new Button
            {
                Text = "Refresh",
                Width = 90,
                Height = 26,
                Location = new Point(270, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            pnlEmployeeTop.Controls.Add(txtSearch);
            pnlEmployeeTop.Controls.Add(btnRefreshEmployees);

            dgvEmployees = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoGenerateColumns = false
            };

            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "EmployeeNumber",
                HeaderText = "Employee #",
                DataPropertyName = "EmployeeNumber",
                Width = 110
            });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "Name",
                DataPropertyName = "Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "HasUserAccount",
                HeaderText = "Has Account",
                DataPropertyName = "HasUserAccount",
                Width = 110
            });

            parent.Controls.Add(dgvEmployees);
            parent.Controls.Add(pnlEmployeeTop);

            dgvEmployees.DataSource = _employeeBindingSource;
        }

        private void BuildRightPanel(Control parent)
        {
            pnlUserHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 92,
                BackColor = Color.White
            };

            lblUserName = new Label
            {
                Text = "Name: -",
                Location = new Point(12, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            lblUserEmail = new Label
            {
                Text = "Email: -",
                Location = new Point(12, 36),
                AutoSize = true
            };

            lblIsActive = new Label
            {
                Text = "Active: -",
                Location = new Point(12, 58),
                AutoSize = true
            };

            pnlUserHeader.Controls.Add(lblUserName);
            pnlUserHeader.Controls.Add(lblUserEmail);
            pnlUserHeader.Controls.Add(lblIsActive);

            grpRoles = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "Roles"
            };

            clbRoles = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true
            };
            grpRoles.Controls.Add(clbRoles);

            pnlActions = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                BackColor = Color.White
            };

            btnCreateUser = new Button
            {
                Text = "Create User Account",
                Width = 170,
                Height = 30,
                Location = new Point(12, 12)
            };

            btnSaveRoles = new Button
            {
                Text = "Save Roles",
                Width = 120,
                Height = 30,
                Location = new Point(190, 12)
            };

            pnlActions.Controls.Add(btnCreateUser);
            pnlActions.Controls.Add(btnSaveRoles);

            parent.Controls.Add(grpRoles);
            parent.Controls.Add(pnlActions);
            parent.Controls.Add(pnlUserHeader);
        }

        private void SetRightPanelEnabled(bool enabled)
        {
            pnlUserHeader.Enabled = enabled;
            grpRoles.Enabled = enabled;
            btnSaveRoles.Enabled = enabled;
        }

        private void WireEvents()
        {
            btnRefreshEmployees.Click += async (s, e) => await LoadEmployeesAsync();
            txtSearch.TextChanged += (s, e) => ApplyEmployeeFilter();
            dgvEmployees.SelectionChanged += (s, e) => HandleEmployeeSelectionChanged();
            btnCreateUser.Click += async (s, e) => await CreateUserAccountForSelectedEmployeeAsync();
        }

        private async System.Threading.Tasks.Task CreateUserAccountForSelectedEmployeeAsync()
        {
            var row = dgvEmployees.CurrentRow?.DataBoundItem as EmployeeAccountRow;
            if (row == null)
                return;

            if (row.UserId.HasValue)
                return;

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                MessageBox.Show("Connection string not found.", "Config Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (var dialog = new CreateUserAccountDialog(row.Name))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                string username = dialog.Username;
                string tempPassword = dialog.TemporaryPassword;

                if (string.IsNullOrWhiteSpace(username))
                {
                    MessageBox.Show("Username is required.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(tempPassword))
                {
                    MessageBox.Show("Temporary password is required.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                btnCreateUser.Enabled = false;
                btnRefreshEmployees.Enabled = false;
                try
                {
                    string emailAddress = BuildPlaceholderEmailAddress(username, row.EmpId);
                    byte[] salt = PasswordHelper.GenerateSalt();
                    byte[] hash = PasswordHelper.HashPassword(tempPassword, salt);

                    int newUserId;

                    using (var con = new SqlConnection(_connectionString))
                    {
                        await con.OpenAsync();
                        using (var tx = con.BeginTransaction())
                        {
                            try
                            {
                                const string checkUserByNameSql = "SELECT COUNT(*) FROM [User] WHERE Name = @Name";
                                using (var cmd = new SqlCommand(checkUserByNameSql, con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@Name", username.Trim());
                                    int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                                    if (count > 0)
                                    {
                                        throw new InvalidOperationException("Username already exists.");
                                    }
                                }

                                const string checkUserByEmpSql = "SELECT COUNT(*) FROM [User] WHERE EmpId = @EmpId";
                                using (var cmd = new SqlCommand(checkUserByEmpSql, con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@EmpId", row.EmpId);
                                    int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                                    if (count > 0)
                                    {
                                        throw new InvalidOperationException("A user account already exists for this employee.");
                                    }
                                }

                                const string insertUserSql = @"
                                    INSERT INTO [User]
                                        (Name, EmailAddress, EmpId, [Password], PasswordHash, PasswordSalt, IsDeveloper, IsActive, IsTemporaryPassword, MustChangePassword, DateCreated)
                                    VALUES
                                        (@Name, @EmailAddress, @EmpId, CONVERT(VARBINARY(128), @PasswordPlain), @Hash, @Salt, 0, 1, 1, 1, SYSDATETIME());
                                    SELECT SCOPE_IDENTITY();";

                                using (var cmd = new SqlCommand(insertUserSql, con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@Name", username.Trim());
                                    cmd.Parameters.AddWithValue("@EmailAddress", emailAddress);
                                    cmd.Parameters.AddWithValue("@EmpId", row.EmpId);
                                    cmd.Parameters.AddWithValue("@PasswordPlain", tempPassword);
                                    cmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, hash.Length) { Value = hash });
                                    cmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, salt.Length) { Value = salt });

                                    object result = await cmd.ExecuteScalarAsync();
                                    newUserId = Convert.ToInt32(result);
                                }

                                const string requesterRoleSql = "SELECT RoleId FROM Role WHERE RoleName = @RoleName AND IsActive = 1";
                                int requesterRoleId;
                                using (var cmd = new SqlCommand(requesterRoleSql, con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@RoleName", "Requester");
                                    object result = await cmd.ExecuteScalarAsync();
                                    if (result == null || result == DBNull.Value)
                                        throw new InvalidOperationException("Default role 'Requester' not found.");
                                    requesterRoleId = Convert.ToInt32(result);
                                }

                                const string insertUserRoleSql = @"
                                    INSERT INTO UserRole (UserId, RoleId, DateAssigned)
                                    SELECT @UserId, @RoleId, SYSDATETIME()
                                    WHERE NOT EXISTS (
                                        SELECT 1 FROM UserRole WHERE UserId = @UserId AND RoleId = @RoleId
                                    );";
                                using (var cmd = new SqlCommand(insertUserRoleSql, con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@UserId", newUserId);
                                    cmd.Parameters.AddWithValue("@RoleId", requesterRoleId);
                                    await cmd.ExecuteNonQueryAsync();
                                }

                                tx.Commit();
                            }
                            catch
                            {
                                tx.Rollback();
                                throw;
                            }
                        }
                    }

                    MessageBox.Show(
                        $"User account created successfully.\n\nUsername: {username.Trim()}\nTemporary Password: {tempPassword}\n\nPlease provide these credentials to the user securely.",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    await LoadEmployeesAsync();

                    var list = _employeeBindingSource.List;
                    if (list != null)
                    {
                        for (int i = 0; i < dgvEmployees.Rows.Count; i++)
                        {
                            if (dgvEmployees.Rows[i].DataBoundItem is EmployeeAccountRow r && r.EmpId == row.EmpId)
                            {
                                dgvEmployees.ClearSelection();
                                dgvEmployees.Rows[i].Selected = true;
                                dgvEmployees.CurrentCell = dgvEmployees.Rows[i].Cells[0];
                                break;
                            }
                        }
                    }

                    _ = newUserId;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create user account.\n\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    btnCreateUser.Enabled = true;
                    btnRefreshEmployees.Enabled = true;
                }
            }
        }

        private static string BuildPlaceholderEmailAddress(string username, int empId)
        {
            string u = (username ?? string.Empty).Trim();
            if (u.Length == 0)
                return $"noemail.emp{empId}@noemail.local";

            var cleaned = new string(u
                .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '.')
                .ToArray());

            while (cleaned.Contains(".."))
                cleaned = cleaned.Replace("..", ".");

            cleaned = cleaned.Trim('.');
            if (cleaned.Length == 0)
                cleaned = "user";

            return $"{cleaned}.emp{empId}@noemail.local";
        }

        private async System.Threading.Tasks.Task LoadEmployeesAsync()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                MessageBox.Show("Connection string not found.", "Config Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                btnRefreshEmployees.Enabled = false;
                _employeeBindingSource.DataSource = null;

                var results = new List<EmployeeAccountRow>();

                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    const string sql = @"
                        SELECT
                            e.EmpId,
                            e.EmployeeNumber,
                            e.Name,
                            u.UserId
                        FROM Employee e
                        LEFT JOIN [User] u ON u.EmpId = e.EmpId
                        ORDER BY e.Name";

                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int empId = reader.GetInt32(0);
                            string employeeNumber = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            string name = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            int? userId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);

                            results.Add(new EmployeeAccountRow
                            {
                                EmpId = empId,
                                EmployeeNumber = employeeNumber,
                                Name = name,
                                UserId = userId,
                                HasUserAccount = userId.HasValue ? "Yes" : "No"
                            });
                        }
                    }
                }

                _allEmployees = results;
                ApplyEmployeeFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load employees.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnRefreshEmployees.Enabled = true;
            }
        }

        private void ApplyEmployeeFilter()
        {
            string q = (txtSearch.Text ?? "").Trim();

            IEnumerable<EmployeeAccountRow> filtered = _allEmployees;
            if (!string.IsNullOrWhiteSpace(q))
            {
                filtered = filtered.Where(x =>
                    (!string.IsNullOrWhiteSpace(x.EmployeeNumber) && x.EmployeeNumber.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrWhiteSpace(x.Name) && x.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            _employeeBindingSource.DataSource = filtered.ToList();

            if (dgvEmployees.Rows.Count > 0)
            {
                dgvEmployees.ClearSelection();
                dgvEmployees.Rows[0].Selected = true;
            }
        }

        private void HandleEmployeeSelectionChanged()
        {
            if (dgvEmployees.CurrentRow?.DataBoundItem is EmployeeAccountRow row)
            {
                bool hasAccount = row.UserId.HasValue;
                SetRightPanelEnabled(hasAccount);
                btnCreateUser.Enabled = !hasAccount;
                btnSaveRoles.Enabled = hasAccount;

                lblUserName.Text = "Name: -";
                lblUserEmail.Text = "Email: -";
                lblIsActive.Text = "Active: -";

                clbRoles.Items.Clear();
                return;
            }

            SetRightPanelEnabled(false);
            btnCreateUser.Enabled = false;
            btnSaveRoles.Enabled = false;
        }

        private sealed class EmployeeAccountRow
        {
            public int EmpId { get; set; }
            public string EmployeeNumber { get; set; }
            public string Name { get; set; }
            public int? UserId { get; set; }
            public string HasUserAccount { get; set; }
        }

        private sealed class CreateUserAccountDialog : Form
        {
            private readonly TextBox _txtUsername;
            private readonly TextBox _txtTempPassword;
            private readonly Button _btnRegenerate;
            private readonly Button _btnCopy;

            public string Username => (_txtUsername.Text ?? "").Trim();
            public string TemporaryPassword => (_txtTempPassword.Text ?? "").Trim();

            public CreateUserAccountDialog(string employeeName)
            {
                Text = "Create User Account";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                ClientSize = new Size(720, 200);

                const int leftPad = 14;
                const int labelWidth = 120;
                const int fieldLeft = leftPad + labelWidth + 10;
                const int row1Top = 16;
                const int row2Top = 52;
                int rightPad = 14;
                int fieldRight = ClientSize.Width - rightPad;

                var lblUsername = new Label
                {
                    Text = "Username",
                    Left = leftPad,
                    Top = row1Top + 4,
                    Width = labelWidth,
                    AutoSize = false
                };
                _txtUsername = new TextBox { Left = fieldLeft, Top = row1Top, Width = fieldRight - fieldLeft };

                var lblTemp = new Label
                {
                    Text = "Temp Password",
                    Left = leftPad,
                    Top = row2Top + 4,
                    Width = labelWidth,
                    AutoSize = false
                };

                int buttonHeight = 28;
                int btnCopyWidth = 90;
                int btnRegenerateWidth = 130;
                int buttonsTop = row2Top - 2;

                _btnCopy = new Button
                {
                    Text = "Copy",
                    Top = buttonsTop,
                    Width = btnCopyWidth,
                    Height = buttonHeight
                };
                _btnCopy.Left = fieldRight - _btnCopy.Width;

                _btnRegenerate = new Button
                {
                    Text = "Regenerate",
                    Top = buttonsTop,
                    Width = btnRegenerateWidth,
                    Height = buttonHeight
                };
                _btnRegenerate.Left = _btnCopy.Left - 10 - _btnRegenerate.Width;

                _txtTempPassword = new TextBox
                {
                    Left = fieldLeft,
                    Top = row2Top,
                    Width = _btnRegenerate.Left - 10 - fieldLeft,
                    ReadOnly = true
                };

                var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = fieldRight - 170, Top = 140, Width = 75 };
                var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = fieldRight - 85, Top = 140, Width = 75 };

                Controls.Add(lblUsername);
                Controls.Add(_txtUsername);
                Controls.Add(lblTemp);
                Controls.Add(_txtTempPassword);
                Controls.Add(_btnRegenerate);
                Controls.Add(_btnCopy);
                Controls.Add(btnOk);
                Controls.Add(btnCancel);

                AcceptButton = btnOk;
                CancelButton = btnCancel;

                if (!string.IsNullOrWhiteSpace(employeeName))
                    _txtUsername.Text = employeeName.Trim();

                _txtTempPassword.Text = PasswordHelper.GenerateTemporaryPassword();
                _btnRegenerate.Click += (s, e) => { _txtTempPassword.Text = PasswordHelper.GenerateTemporaryPassword(); };
                _btnCopy.Click += (s, e) =>
                {
                    try
                    {
                        Clipboard.SetText(_txtTempPassword.Text);
                    }
                    catch
                    {
                    }
                };
            }
        }
    }
}

