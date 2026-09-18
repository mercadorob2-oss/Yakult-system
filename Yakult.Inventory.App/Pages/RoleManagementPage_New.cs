using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Security;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Pages
{
    /// <summary>
    /// Employee-centric Identity & Access Management page.
    /// Single source of truth for user account creation and role assignment.
    /// </summary>
    public partial class RoleManagementPage_New : UserControl
    {
        private readonly string _connectionString;
        private readonly RoleRepository _roleRepository;
        
        // UI Components - Left Panel (Employee List)
        private SplitContainer splitMain;
        private Panel pnlEmployeeSearch;
        private TextBox txtEmployeeSearch;
        private Button btnRefresh;
        private DataGridView dgvEmployees;
        private BindingSource _employeeBindingSource = new BindingSource();
        private List<EmployeeDto> _allEmployees = new List<EmployeeDto>();

        // UI Components - Right Panel (Account Details & Roles)
        private Panel pnlAccountDetails;
        private Label lblEmployeeNameHeader;
        private Label lblEmployeeNumber;
        private Label lblPosition;
        private Label lblDepartment;
        private Label lblAccountStatus;
        private Button btnCreateAccount;
        private Button btnResetPassword;
        private Button btnToggleActive;
        
        private GroupBox grpRoles;
        private Panel pnlRoleCheckboxes;
        private Button btnSaveRoles;
        
        private EmployeeDto _selectedEmployee;
        private UserAccountDto _selectedUserAccount;
        private List<RoleDto> _allRoles = new List<RoleDto>();

        public RoleManagementPage_New()
        {
            _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            _roleRepository = new RoleRepository(_connectionString);

            InitializeComponent();
            BuildUI();
            WireEvents();
            
            _ = LoadDataAsync();
        }

        private void InitializeComponent()
        {
            // Designer placeholder
        }

        private void BuildUI()
        {
            SuspendLayout();
            Controls.Clear();

            Dock = DockStyle.Fill;
            BackColor = Color.White;

            // Main split container
            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 8,
                BackColor = Color.FromArgb(200, 200, 200)
            };
            SplitContainerUtil.BindSafeSplitterDistance(splitMain, () => 450);

            BuildLeftPanel(splitMain.Panel1);
            BuildRightPanel(splitMain.Panel2);

            Controls.Add(splitMain);
            ResumeLayout(true);
        }

        private void BuildLeftPanel(Control parent)
        {
            parent.BackColor = Color.White;

            // Search panel
            pnlEmployeeSearch = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(245, 245, 245),
                Padding = new Padding(10)
            };

            var lblSearch = new Label
            {
                Text = "Search Employees:",
                Location = new Point(10, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            txtEmployeeSearch = new TextBox
            {
                Location = new Point(10, 32),
                Width = 300,
                Font = new Font("Segoe UI", 9F)
            };

            btnRefresh = new Button
            {
                Text = "Refresh",
                Location = new Point(320, 30),
                Width = 80,
                Height = 26,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderSize = 0;

            pnlEmployeeSearch.Controls.AddRange(new Control[] { lblSearch, txtEmployeeSearch, btnRefresh });

            // Employee grid
            dgvEmployees = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                GridColor = Color.FromArgb(230, 230, 230),
                Font = new Font("Segoe UI", 9F)
            };

            dgvEmployees.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 73, 94);
            dgvEmployees.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvEmployees.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgvEmployees.ColumnHeadersHeight = 35;

            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "EmployeeNumber",
                HeaderText = "Employee #",
                DataPropertyName = "EmployeeNumber",
                Width = 100
            });

            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "Name",
                DataPropertyName = "Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 150
            });

            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "HasAccount",
                HeaderText = "Has Account",
                DataPropertyName = "HasAccount",
                Width = 100
            });

            dgvEmployees.DataSource = _employeeBindingSource;

            parent.Controls.Add(dgvEmployees);
            parent.Controls.Add(pnlEmployeeSearch);
        }

        private void BuildRightPanel(Control parent)
        {
            parent.BackColor = Color.White;

            // Account details panel
            pnlAccountDetails = new Panel
            {
                Dock = DockStyle.Top,
                Height = 220,
                BackColor = Color.FromArgb(245, 245, 245),
                Padding = new Padding(15)
            };

            lblEmployeeNameHeader = new Label
            {
                Text = "Select an employee",
                Location = new Point(15, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94)
            };

            lblEmployeeNumber = new Label
            {
                Text = "Employee #: -",
                Location = new Point(15, 45),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };

            lblPosition = new Label
            {
                Text = "Position: -",
                Location = new Point(15, 65),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };

            lblDepartment = new Label
            {
                Text = "Department: -",
                Location = new Point(15, 85),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };

            lblAccountStatus = new Label
            {
                Text = "Account Status: No Account",
                Location = new Point(15, 110),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(231, 76, 60)
            };

            btnCreateAccount = new Button
            {
                Text = "Create User Account",
                Location = new Point(15, 140),
                Width = 160,
                Height = 35,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Visible = false
            };
            btnCreateAccount.FlatAppearance.BorderSize = 0;

            btnResetPassword = new Button
            {
                Text = "Reset Password",
                Location = new Point(185, 140),
                Width = 140,
                Height = 35,
                BackColor = Color.FromArgb(241, 196, 15),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand,
                Visible = false
            };
            btnResetPassword.FlatAppearance.BorderSize = 0;

            btnToggleActive = new Button
            {
                Text = "Disable Account",
                Location = new Point(335, 140),
                Width = 140,
                Height = 35,
                BackColor = Color.FromArgb(231, 76, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand,
                Visible = false
            };
            btnToggleActive.FlatAppearance.BorderSize = 0;

            pnlAccountDetails.Controls.AddRange(new Control[] {
                lblEmployeeNameHeader, lblEmployeeNumber, lblPosition, lblDepartment,
                lblAccountStatus, btnCreateAccount, btnResetPassword, btnToggleActive
            });

            // Roles group box
            grpRoles = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "Role Assignment",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Padding = new Padding(10),
                Enabled = false
            };

            pnlRoleCheckboxes = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            grpRoles.Controls.Add(pnlRoleCheckboxes);

            // Save roles button
            var pnlRoleActions = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            btnSaveRoles = new Button
            {
                Text = "Save Roles",
                Location = new Point(10, 15),
                Width = 150,
                Height = 35,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnSaveRoles.FlatAppearance.BorderSize = 0;

            pnlRoleActions.Controls.Add(btnSaveRoles);

            parent.Controls.Add(grpRoles);
            parent.Controls.Add(pnlRoleActions);
            parent.Controls.Add(pnlAccountDetails);
        }

        private void WireEvents()
        {
            txtEmployeeSearch.TextChanged += (s, e) => FilterEmployees();
            btnRefresh.Click += async (s, e) => await LoadDataAsync();
            dgvEmployees.SelectionChanged += DgvEmployees_SelectionChanged;
            btnCreateAccount.Click += async (s, e) => await CreateUserAccountAsync();
            btnResetPassword.Click += async (s, e) => await ResetPasswordAsync();
            btnToggleActive.Click += async (s, e) => await ToggleAccountActiveAsync();
            btnSaveRoles.Click += async (s, e) => await SaveRolesAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                btnRefresh.Enabled = false;
                
                // Load employees with account status
                _allEmployees = await _roleRepository.GetEmployeesWithAccountStatusAsync();
                
                // Load all roles
                _allRoles = await _roleRepository.GetAllRolesAsync();
                
                FilterEmployees();
                
                MessageBox.Show($"Loaded {_allEmployees.Count} employees and {_allRoles.Count} roles.", 
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnRefresh.Enabled = true;
            }
        }

        private void FilterEmployees()
        {
            string searchText = txtEmployeeSearch.Text?.Trim() ?? "";
            
            var filtered = _allEmployees.AsEnumerable();
            
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(e =>
                    (e.Name?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (e.EmployeeNumber?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            // Add computed property for grid display
            var displayList = filtered.Select(e => new
            {
                e.EmpId,
                e.EmployeeNumber,
                e.Name,
                HasAccount = e.HasAccount ? "Yes" : "No",
                e.UserId,
                e.Position,
                e.DepartmentName,
                e.CompanyName,
                e.BranchName
            }).ToList();

            _employeeBindingSource.DataSource = displayList;
        }

        private async void DgvEmployees_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvEmployees.CurrentRow?.DataBoundItem == null)
            {
                ClearRightPanel();
                return;
            }

            var row = dgvEmployees.CurrentRow.DataBoundItem;
            int empId = (int)row.GetType().GetProperty("EmpId").GetValue(row);
            
            _selectedEmployee = _allEmployees.FirstOrDefault(e => e.EmpId == empId);
            
            if (_selectedEmployee == null)
            {
                ClearRightPanel();
                return;
            }

            await LoadEmployeeDetailsAsync();
        }

        private async Task LoadEmployeeDetailsAsync()
        {
            if (_selectedEmployee == null) return;

            // Update employee info
            lblEmployeeNameHeader.Text = _selectedEmployee.Name ?? "Unknown";
            lblEmployeeNumber.Text = $"Employee #: {_selectedEmployee.EmployeeNumber ?? "N/A"}";
            lblPosition.Text = $"Position: {_selectedEmployee.Position ?? "N/A"}";
            lblDepartment.Text = $"Department: {_selectedEmployee.DepartmentName ?? "N/A"}";

            if (_selectedEmployee.HasAccount && _selectedEmployee.UserId.HasValue)
            {
                // Load user account details
                _selectedUserAccount = await _roleRepository.GetUserAccountByEmployeeIdAsync(_selectedEmployee.EmpId);
                
                if (_selectedUserAccount != null)
                {
                    lblAccountStatus.Text = _selectedUserAccount.IsActive 
                        ? "Account Status: Active" 
                        : "Account Status: Disabled";
                    lblAccountStatus.ForeColor = _selectedUserAccount.IsActive 
                        ? Color.FromArgb(46, 204, 113) 
                        : Color.FromArgb(231, 76, 60);

                    btnCreateAccount.Visible = false;
                    btnResetPassword.Visible = true;
                    btnToggleActive.Visible = true;
                    btnToggleActive.Text = _selectedUserAccount.IsActive ? "Disable Account" : "Enable Account";
                    btnToggleActive.BackColor = _selectedUserAccount.IsActive 
                        ? Color.FromArgb(231, 76, 60) 
                        : Color.FromArgb(46, 204, 113);

                    grpRoles.Enabled = true;
                    btnSaveRoles.Enabled = true;

                    await LoadUserRolesAsync();
                }
            }
            else
            {
                // No account
                _selectedUserAccount = null;
                lblAccountStatus.Text = "Account Status: No Account";
                lblAccountStatus.ForeColor = Color.FromArgb(231, 76, 60);

                btnCreateAccount.Visible = true;
                btnResetPassword.Visible = false;
                btnToggleActive.Visible = false;

                grpRoles.Enabled = false;
                btnSaveRoles.Enabled = false;
                pnlRoleCheckboxes.Controls.Clear();
            }
        }

        private async Task LoadUserRolesAsync()
        {
            if (_selectedUserAccount == null) return;

            pnlRoleCheckboxes.Controls.Clear();

            // Get user's current roles
            var userRoles = await _roleRepository.GetUserRolesAsync(_selectedUserAccount.UserId);
            var assignedRoleIds = new HashSet<int>(userRoles.Select(ur => ur.RoleId));

            int yPos = 10;
            foreach (var role in _allRoles.OrderBy(r => r.RoleName))
            {
                var chk = new CheckBox
                {
                    Text = $"{role.RoleName} - {role.Description ?? ""}",
                    Tag = role,
                    Checked = assignedRoleIds.Contains(role.RoleId),
                    Location = new Point(10, yPos),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9F)
                };

                pnlRoleCheckboxes.Controls.Add(chk);
                yPos += 30;
            }
        }

        private void ClearRightPanel()
        {
            _selectedEmployee = null;
            _selectedUserAccount = null;

            lblEmployeeNameHeader.Text = "Select an employee";
            lblEmployeeNumber.Text = "Employee #: -";
            lblPosition.Text = "Position: -";
            lblDepartment.Text = "Department: -";
            lblAccountStatus.Text = "Account Status: -";

            btnCreateAccount.Visible = false;
            btnResetPassword.Visible = false;
            btnToggleActive.Visible = false;

            grpRoles.Enabled = false;
            btnSaveRoles.Enabled = false;
            pnlRoleCheckboxes.Controls.Clear();
        }

        private async Task CreateUserAccountAsync()
        {
            if (_selectedEmployee == null) return;

            using (var dialog = new CreateAccountDialog(_selectedEmployee.Name))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    btnCreateAccount.Enabled = false;

                    int userId = await _roleRepository.CreateUserAccountAsync(
                        _selectedEmployee.EmpId,
                        dialog.Username,
                        dialog.TempPassword);

                    MessageBox.Show(
                        $"User account created successfully!\n\n" +
                        $"Username: {dialog.Username}\n" +
                        $"Temporary Password: {dialog.TempPassword}\n\n" +
                        $"Please provide these credentials to the user securely.",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    // Reload data
                    await LoadDataAsync();
                    
                    // Reselect the employee
                    var updatedEmp = _allEmployees.FirstOrDefault(e => e.EmpId == _selectedEmployee.EmpId);
                    if (updatedEmp != null)
                    {
                        _selectedEmployee = updatedEmp;
                        await LoadEmployeeDetailsAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating user account: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    btnCreateAccount.Enabled = true;
                }
            }
        }

        private async Task ResetPasswordAsync()
        {
            if (_selectedUserAccount == null) return;

            string newPassword = PasswordHelper.GenerateTemporaryPassword();

            var result = MessageBox.Show(
                $"Reset password for {_selectedEmployee.Name}?\n\n" +
                $"New temporary password: {newPassword}\n\n" +
                $"Click Yes to proceed.",
                "Confirm Password Reset",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            try
            {
                btnResetPassword.Enabled = false;

                await _roleRepository.ResetPasswordAsync(_selectedUserAccount.UserId, newPassword);

                ActivityLogger.Log(ActivityLogger.Actions.ResetPassword,
                    "User", _selectedUserAccount.UserId, $"Password reset for employee '{_selectedEmployee.Name}'");

                MessageBox.Show(
                    $"Password reset successfully!\n\n" +
                    $"New temporary password: {newPassword}\n\n" +
                    $"Please provide this to the user securely.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting password: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnResetPassword.Enabled = true;
            }
        }

        private async Task ToggleAccountActiveAsync()
        {
            if (_selectedUserAccount == null) return;

            bool newStatus = !_selectedUserAccount.IsActive;
            string action = newStatus ? "enable" : "disable";

            var result = MessageBox.Show(
                $"Are you sure you want to {action} the account for {_selectedEmployee.Name}?",
                $"Confirm {action.ToUpper()}",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            try
            {
                btnToggleActive.Enabled = false;

                await _roleRepository.ToggleUserActiveStatusAsync(_selectedUserAccount.UserId, newStatus);

                MessageBox.Show($"Account {action}d successfully!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadEmployeeDetailsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error toggling account status: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnToggleActive.Enabled = true;
            }
        }

        private async Task SaveRolesAsync()
        {
            if (_selectedUserAccount == null) return;

            // Collect selected role IDs
            var selectedRoleIds = new List<int>();
            foreach (Control ctrl in pnlRoleCheckboxes.Controls)
            {
                if (ctrl is CheckBox chk && chk.Checked && chk.Tag is RoleDto role)
                {
                    selectedRoleIds.Add(role.RoleId);
                }
            }

            try
            {
                btnSaveRoles.Enabled = false;

                await _roleRepository.SyncUserRolesAsync(_selectedUserAccount.UserId, selectedRoleIds);

                MessageBox.Show("Roles saved successfully!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadUserRolesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving roles: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSaveRoles.Enabled = true;
            }
        }

        // Helper dialog for creating user accounts
        private class CreateAccountDialog : Form
        {
            private TextBox txtUsername;
            private TextBox txtPassword;
            private Button btnGenerate;

            public string Username => txtUsername.Text?.Trim();
            public string TempPassword => txtPassword.Text?.Trim();

            public CreateAccountDialog(string employeeName)
            {
                Text = "Create User Account";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;
                Size = new Size(450, 220);

                var lblUsername = new Label
                {
                    Text = "Username:",
                    Location = new Point(20, 20),
                    AutoSize = true
                };

                txtUsername = new TextBox
                {
                    Location = new Point(20, 45),
                    Width = 400,
                    Text = employeeName
                };

                var lblPassword = new Label
                {
                    Text = "Temporary Password:",
                    Location = new Point(20, 80),
                    AutoSize = true
                };

                txtPassword = new TextBox
                {
                    Location = new Point(20, 105),
                    Width = 300,
                    ReadOnly = true
                };

                btnGenerate = new Button
                {
                    Text = "Generate",
                    Location = new Point(330, 103),
                    Width = 90,
                    Height = 26
                };
                btnGenerate.Click += (s, e) => txtPassword.Text = PasswordHelper.GenerateTemporaryPassword();

                var btnOk = new Button
                {
                    Text = "Create",
                    DialogResult = DialogResult.OK,
                    Location = new Point(240, 145),
                    Width = 80
                };

                var btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(330, 145),
                    Width = 80
                };

                Controls.AddRange(new Control[] {
                    lblUsername, txtUsername, lblPassword, txtPassword,
                    btnGenerate, btnOk, btnCancel
                });

                AcceptButton = btnOk;
                CancelButton = btnCancel;

                // Generate initial password
                txtPassword.Text = PasswordHelper.GenerateTemporaryPassword();
            }
        }
    }
}

