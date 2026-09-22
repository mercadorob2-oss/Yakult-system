using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Core;


namespace Yakult.Inventory.App.Pages.Employee
{
    public partial class QuickAddEmployeeDialog : Form
    {
        private TextBox txtName, txtEmployeeNumber, txtPosition, txtDescription;
        private ComboBox cmbCompany, cmbBranch, cmbDepartment, cmbDistributor, cmbPosition, cmbTitle;
        private Button btnSave, btnCancel;
        private ErrorProvider _errors;
        private Label _lblStatus;
        private bool _saving;

        // Suppresses SelectedIndexChanged cascades while ComboBox Items are being rebuilt
        private bool _suppressComboBoxEvents = false;

        public int? NewEmployeeId { get; private set; }
        public string NewEmployeeName { get; private set; }
        public int? NewCompanyId { get; private set; }
        public int? NewBranchId { get; private set; }
        public int? NewDepartmentId { get; private set; }
        public int? NewDistributorId { get; private set; }

        public QuickAddEmployeeDialog()
        {
            InitializeComponent();
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Font = ModernUiHelper.FontNormal;
            Text = "Quick Add Employee";
            ClientSize = new Size(880, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = ModernUiHelper.ColorBackground;

            _errors = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
            _errors.ContainerControl = this;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 1,
                RowCount = 3
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var header = new Label
            {
                Text = "Fields marked * are required. You can type to search Company/Branch/Department.",
                AutoSize = true,
                ForeColor = ModernUiHelper.ColorTextSecondary,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 0, 8),
                Dock = DockStyle.Top
            };
            root.Controls.Add(header, 0, 0);

            var card = ModernUiHelper.CreateCard();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(16);
            root.Controls.Add(card, 0, 1);

            ComboBox NewCombo()
            {
                return new ComboBox
                {
                    Dock = DockStyle.Top,
                    DropDownStyle = ComboBoxStyle.DropDown,
                    AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                    AutoCompleteSource = AutoCompleteSource.ListItems,
                    Font = ModernUiHelper.FontNormal
                };
            }

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            card.Controls.Add(grid);

            var left = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 11,
                Margin = new Padding(0, 0, 10, 0)
            };
            for (int i = 0; i < 11; i++)
                left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(left, 0, 0);

            left.Controls.Add(ModernUiHelper.CreateSectionHeader("Basic Info"), 0, 0);
            left.Controls.Add(ModernUiHelper.CreateLabel("Employee Name *"), 0, 1);
            txtName = ModernUiHelper.CreateTextBox();
            txtName.MaxLength = 150;
            left.Controls.Add(txtName, 0, 2);

            left.Controls.Add(ModernUiHelper.CreateLabel("Employee Number"), 0, 3);
            txtEmployeeNumber = ModernUiHelper.CreateTextBox();
            txtEmployeeNumber.MaxLength = 50;
            left.Controls.Add(txtEmployeeNumber, 0, 4);

            left.Controls.Add(ModernUiHelper.CreateLabel("Title"), 0, 5);
            cmbTitle = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = ModernUiHelper.FontNormal
            };
            left.Controls.Add(cmbTitle, 0, 6);

            left.Controls.Add(ModernUiHelper.CreateLabel("Position"), 0, 7);
            cmbPosition = NewCombo();
            left.Controls.Add(cmbPosition, 0, 8);

            left.Controls.Add(ModernUiHelper.CreateLabel("Description"), 0, 9);
            txtDescription = ModernUiHelper.CreateTextBox();
            txtDescription.Multiline = true;
            txtDescription.ScrollBars = ScrollBars.Vertical;
            txtDescription.Height = 92;
            left.Controls.Add(txtDescription, 0, 10);

            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 10,
                Margin = new Padding(10, 0, 0, 0)
            };
            for (int i = 0; i < 10; i++)
                right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(right, 1, 0);

            right.Controls.Add(ModernUiHelper.CreateSectionHeader("Organization"), 0, 0);
            right.Controls.Add(ModernUiHelper.CreateLabel("Company *"), 0, 1);
            cmbCompany = NewCombo();
            right.Controls.Add(cmbCompany, 0, 2);

            right.Controls.Add(ModernUiHelper.CreateLabel("Branch *"), 0, 3);
            cmbBranch = NewCombo();
            right.Controls.Add(cmbBranch, 0, 4);

            right.Controls.Add(ModernUiHelper.CreateLabel("Department"), 0, 5);
            cmbDepartment = NewCombo();
            right.Controls.Add(cmbDepartment, 0, 6);

            right.Controls.Add(ModernUiHelper.CreateLabel("Distributor (optional)"), 0, 7);
            cmbDistributor = NewCombo();
            right.Controls.Add(cmbDistributor, 0, 8);

            right.Controls.Add(new Label
            {
                Text = "Tip: pick Company first, then Branch.\r\nDepartment and Distributor are optional.",
                AutoSize = true,
                ForeColor = ModernUiHelper.ColorTextSecondary,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 8, 0, 0)
            }, 0, 9);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _lblStatus = new Label
            {
                Text = string.Empty,
                AutoSize = true,
                ForeColor = ModernUiHelper.ColorTextSecondary,
                Dock = DockStyle.Fill,
                Padding = new Padding(2, 10, 0, 0)
            };
            footer.Controls.Add(_lblStatus, 0, 0);

            var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, WrapContents = false };
            btnCancel = ModernUiHelper.CreateSecondaryButton("Cancel", 100);
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Click += (s, e) => Close();

            btnSave = ModernUiHelper.CreatePrimaryButton("Save", 110);
            btnSave.Click += BtnSave_Click;

            btnRow.Controls.Add(btnSave);
            btnRow.Controls.Add(btnCancel);
            footer.Controls.Add(btnRow, 1, 0);

            root.Controls.Add(footer, 0, 2);

            AcceptButton = btnSave;
            CancelButton = btnCancel;

            Shown += (_, __) => txtName.Focus();

            txtName.TextChanged += (_, __) => UpdateSaveEnabled();
            cmbCompany.SelectedIndexChanged += (_, __) => UpdateSaveEnabled();
            cmbCompany.TextChanged += (_, __) => UpdateSaveEnabled();
            cmbCompany.Leave += (_, __) => UpdateSaveEnabled();
            cmbBranch.SelectedIndexChanged += (_, __) => UpdateSaveEnabled();
            cmbBranch.TextChanged += (_, __) => UpdateSaveEnabled();
            cmbBranch.Leave += (_, __) => UpdateSaveEnabled();
            cmbDepartment.SelectedIndexChanged += (_, __) => UpdateSaveEnabled();
            cmbDepartment.TextChanged += (_, __) => UpdateSaveEnabled();
            cmbDepartment.Leave += (_, __) => UpdateSaveEnabled();

            // Cascade dropdowns - fires on list pick (SelectedIndexChanged) AND on typed+tab (Leave)
            cmbCompany.SelectedIndexChanged += (s, e) => LoadBranches();
            cmbCompany.SelectedIndexChanged += (s, e) => LoadDepartments();
            cmbCompany.Leave += (s, e) => { LoadBranches(); LoadDepartments(); };

            UpdateSaveEnabled();
        }

        private void LoadData()
        {
            LoadCompanies();
            LoadTitles();
            LoadPositions();
            LoadDistributors();
        }

        private void LoadTitles()
        {
            cmbTitle.Items.Clear();
            cmbTitle.Items.Add(new ComboItem { Id = null, Name = "-- Select Title --" });

            try
            {
                var cs = DatabaseConfig.ConnectionString;
                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT TitleId, Code, Description FROM dbo.Title ORDER BY TitleId", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbTitle.Items.Add(new ComboItem
                            {
                                Id = reader.GetInt32(0),
                                Name = $"{reader.GetString(1)} - {reader.GetString(2)}"
                            });
                        }
                    }
                }

                cmbTitle.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load titles: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadCompanies()
        {
            cmbCompany.Items.Clear();

            var defaultItem = new ComboItem { Id = null, Name = "-- Select Company --" };
            cmbCompany.Items.Add(defaultItem);

            try
            {
                var cs = DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    try
                    {
                        using (var cmd = new SqlCommand(@"
SELECT
    c.ComId,
    c.Name
FROM dbo.Company c
LEFT JOIN dbo.ArchiveStatus arc
    ON arc.EntityType = 'Company'
   AND arc.EntityId = c.ComId
   AND arc.IsArchived = 1
WHERE arc.ArchiveId IS NULL
  AND ISNULL(c.Active, 1) = 1
ORDER BY c.Name", con))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                cmbCompany.Items.Add(new ComboItem
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1)
                                });
                            }
                        }
                    }
                    catch (SqlException)
                    {
                        using (var cmd = new SqlCommand("SELECT ComId, Name FROM dbo.Company WHERE ISNULL(Active, 1) = 1 ORDER BY Name", con))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                cmbCompany.Items.Add(new ComboItem
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1)
                                });
                            }
                        }
                    }
                }

                if (cmbCompany.Items.Count > 0)
                    cmbCompany.SelectedIndex = 0;

                cmbCompany.Tag = cmbCompany.Items.Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load companies: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadDistributors()
        {
            cmbDistributor.Items.Clear();
            cmbDistributor.Items.Add(new ComboItem { Id = null, Name = "(None)" });

            try
            {
                var cs = DatabaseConfig.ConnectionString;
                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "IF OBJECT_ID('dbo.Distributor', 'U') IS NOT NULL SELECT DistributorId, Name FROM dbo.Distributor WHERE ISNULL(IsActive, 1) = 1 ORDER BY SortOrder, Name", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbDistributor.Items.Add(new ComboItem
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }

                if (cmbDistributor.Items.Count > 0)
                    cmbDistributor.SelectedIndex = 0;

                cmbDistributor.Tag = cmbDistributor.Items.Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load distributors: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadPositions()
        {
            cmbPosition.Items.Clear();

            var defaultItem = new ComboItem { Id = null, Name = "-- Select Position --" };
            cmbPosition.Items.Add(defaultItem);

            try
            {
                var cs = DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT DISTINCT Position FROM dbo.Employee WHERE Position IS NOT NULL AND Position != '' ORDER BY Position", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new ComboItem
                            {
                                Id = null,
                                Name = reader.GetString(0)
                            };
                            cmbPosition.Items.Add(item);
                        }
                    }
                }

                if (cmbPosition.Items.Count > 0)
                    cmbPosition.SelectedIndex = 0;

                cmbPosition.Tag = cmbPosition.Items.Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load positions: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadBranches()
        {
            cmbBranch.Items.Clear();

            var defaultItem = new ComboItem { Id = null, Name = "-- Select Branch --" };
            cmbBranch.Items.Add(defaultItem);

            var selectedCompany = ResolveComboItem(cmbCompany);
            if (selectedCompany == null || !selectedCompany.Id.HasValue)
            {
                cmbBranch.SelectedIndex = 0;
                cmbBranch.Enabled = false;
                return;
            }

            try
            {
                var cs = DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT b.BranchId, b.Name
                        FROM   dbo.Branch b
                        WHERE  b.Active = 1
                          AND  EXISTS (
                              SELECT 1 FROM dbo.BranchDepartmentCompany bdc
                              WHERE bdc.BranchID = b.BranchId AND bdc.CompanyID = @ComId
                          )
                        ORDER BY b.Name", con))
                    {
                        cmd.Parameters.AddWithValue("@ComId", selectedCompany.Id.Value);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var item = new ComboItem
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1)
                                };
                                cmbBranch.Items.Add(item);
                            }
                        }
                    }
                }

                if (cmbBranch.Items.Count > 0)
                    cmbBranch.SelectedIndex = 0;

                cmbBranch.Enabled = true;
            }
            catch (Exception ex)
            {
                cmbBranch.Enabled = true;
                MessageBox.Show($"Failed to load branches: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            UpdateSaveEnabled();
        }

        private void LoadDepartments()
        {
            cmbDepartment.Items.Clear();

            var defaultItem = new ComboItem { Id = null, Name = "-- Select Department --" };
            cmbDepartment.Items.Add(defaultItem);

            var selectedCompany = ResolveComboItem(cmbCompany);
            if (selectedCompany == null || !selectedCompany.Id.HasValue)
            {
                cmbDepartment.SelectedIndex = 0;
                cmbDepartment.Enabled = false;
                return;
            }

            try
            {
                var cs = DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT d.DeptId, d.Name
                        FROM   dbo.Department d
                        WHERE  d.Active = 1
                          AND  EXISTS (
                              SELECT 1 FROM dbo.BranchDepartmentCompany bdc
                              WHERE bdc.DepartmentID = d.DeptId AND bdc.CompanyID = @ComId
                          )
                        ORDER BY d.Name", con))
                    {
                        cmd.Parameters.AddWithValue("@ComId", selectedCompany.Id.Value);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var item = new ComboItem
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1)
                                };
                                cmbDepartment.Items.Add(item);
                            }
                        }
                    }
                }

                if (cmbDepartment.Items.Count > 0)
                    cmbDepartment.SelectedIndex = 0;

                cmbDepartment.Enabled = true;
            }
            catch (Exception ex)
            {
                cmbDepartment.Enabled = true;
                MessageBox.Show($"Failed to load departments: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            UpdateSaveEnabled();
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (_saving) return;
            if (!ValidateInputs(showErrors: true))
                return;

            var selectedCompany = ResolveComboItem(cmbCompany);
            var selectedBranch = ResolveComboItem(cmbBranch);

            // Department is optional — null is stored when not selected
            var selectedDepartment = ResolveComboItem(cmbDepartment);

            // Distributor posting is optional — only a selected few employees carry one
            var selectedDistributor = ResolveComboItem(cmbDistributor);

            try
            {
                _saving = true;
                UseWaitCursor = true;
                if (_lblStatus != null) _lblStatus.Text = "Saving...";
                ToggleInputs(enabled: false);

                var cs = DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs))
                {
                    MessageBox.Show("Connection string not found.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using (var con = new SqlConnection(cs))
                {
                    con.Open();

                    // Distributor is optional and deployment-guarded: older DBs may
                    // not have dbo.Employee.DistributorId yet.
                    bool hasDistributorColumn;
                    using (var checkCmd = new SqlCommand(
                        "SELECT CASE WHEN COL_LENGTH('dbo.Employee', 'DistributorId') IS NULL THEN 0 ELSE 1 END", con))
                    {
                        hasDistributorColumn = Convert.ToInt32(checkCmd.ExecuteScalar()) == 1;
                    }
                    var ins = EmployeeDistributorHelper.BuildDistributorInsertFragments(hasDistributorColumn);

                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.Employee (Name, EmployeeNumber, Position, Description, DateCreated, Createdby, ComId, BranchId, DeptId, TitleId," + ins.ColumnFragment + @")
                        OUTPUT INSERTED.EmpId
                        VALUES (@Name, @EmployeeNumber, @Position, @Description, @DateCreated, @Createdby, @ComId, @BranchId, @DeptId, @TitleId," + ins.ValueFragment + @")", con))
                    {
                        cmd.Parameters.AddWithValue("@Name", txtName.Text.Trim());
                        // EmployeeNumber may be null/empty
                        if (string.IsNullOrWhiteSpace(txtEmployeeNumber.Text))
                            cmd.Parameters.AddWithValue("@EmployeeNumber", DBNull.Value);
                        else
                            cmd.Parameters.AddWithValue("@EmployeeNumber", txtEmployeeNumber.Text.Trim());
                        // Position may be null/empty
                        if (string.IsNullOrWhiteSpace(cmbPosition.Text))
                            cmd.Parameters.AddWithValue("@Position", DBNull.Value);
                        else
                            cmd.Parameters.AddWithValue("@Position", cmbPosition.Text.Trim());

                        cmd.Parameters.AddWithValue("@Description",
                            string.IsNullOrWhiteSpace(txtDescription.Text) ? (object)DBNull.Value : txtDescription.Text.Trim());
                        cmd.Parameters.AddWithValue("@DateCreated", DateTime.Now);
                        cmd.Parameters.AddWithValue("@Createdby", AppSession.CurrentUserId);
                        cmd.Parameters.AddWithValue("@ComId", selectedCompany.Id.Value);
                        cmd.Parameters.AddWithValue("@BranchId", selectedBranch.Id.Value);
                        cmd.Parameters.AddWithValue("@DeptId",
                            (selectedDepartment?.Id.HasValue == true) ? (object)selectedDepartment.Id.Value : DBNull.Value);

                        var selectedTitle = cmbTitle.SelectedItem as ComboItem;
                        cmd.Parameters.AddWithValue("@TitleId",
                            (selectedTitle?.Id.HasValue == true) ? (object)selectedTitle.Id.Value : DBNull.Value);
                        if (hasDistributorColumn)
                            cmd.Parameters.AddWithValue("@DistributorId",
                                (object)EmployeeDistributorHelper.NormalizeDistributorId(selectedDistributor?.Id) ?? DBNull.Value);

                        NewEmployeeId = (int)cmd.ExecuteScalar();
                        NewEmployeeName = txtName.Text.Trim();
                        NewCompanyId = selectedCompany.Id;
                        NewBranchId = selectedBranch.Id;
                        NewDepartmentId = selectedDepartment?.Id;
                        NewDistributorId = hasDistributorColumn
                            ? EmployeeDistributorHelper.NormalizeDistributorId(selectedDistributor?.Id)
                            : null;
                    }
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                if (_lblStatus != null) _lblStatus.Text = string.Empty;
                MessageBox.Show($"Failed to save employee: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _saving = false;
                UseWaitCursor = false;
                ToggleInputs(enabled: true);
                UpdateSaveEnabled();
            }
        }

        private void ToggleInputs(bool enabled)
        {
            if (txtName != null) txtName.Enabled = enabled;
            if (txtEmployeeNumber != null) txtEmployeeNumber.Enabled = enabled;
            if (cmbTitle != null) cmbTitle.Enabled = enabled;
            if (cmbPosition != null) cmbPosition.Enabled = enabled;
            if (txtDescription != null) txtDescription.Enabled = enabled;
            if (cmbCompany != null) cmbCompany.Enabled = enabled;

            var hasCompany = ResolveComboItem(cmbCompany)?.Id.HasValue == true;
            if (cmbBranch != null) cmbBranch.Enabled = enabled && hasCompany;
            if (cmbDepartment != null) cmbDepartment.Enabled = enabled && hasCompany;
            if (cmbDistributor != null) cmbDistributor.Enabled = enabled;

            if (btnCancel != null) btnCancel.Enabled = enabled;
            if (btnSave != null) btnSave.Enabled = enabled && ValidateInputs(showErrors: false);
        }

        private void UpdateSaveEnabled()
        {
            if (btnSave == null) return;
            if (_saving) { btnSave.Enabled = false; return; }
            var canSave = ValidateInputs(showErrors: false);
            btnSave.Enabled = canSave;
            if (_lblStatus != null)
                _lblStatus.Text = canSave ? string.Empty : "Required: Name, Company, Branch";
        }

        private bool ValidateInputs(bool showErrors)
        {
            if (_errors == null) return true;

            if (showErrors)
            {
                _errors.SetError(txtName, string.Empty);
                _errors.SetError(cmbCompany, string.Empty);
                _errors.SetError(cmbBranch, string.Empty);
            }

            var ok = true;

            var name = (txtName?.Text ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                ok = false;
                if (showErrors)
                {
                    _errors.SetError(txtName, "Employee name is required.");
                    txtName.Focus();
                }
            }

            var company = ResolveComboItem(cmbCompany);
            if (company == null || !company.Id.HasValue)
            {
                ok = false;
                if (showErrors)
                {
                    _errors.SetError(cmbCompany, "Company is required.");
                    cmbCompany.Focus();
                }
            }

            var branch = ResolveComboItem(cmbBranch);
            if (branch == null || !branch.Id.HasValue)
            {
                ok = false;
                if (showErrors)
                {
                    _errors.SetError(cmbBranch, "Branch is required.");
                    cmbBranch.Focus();
                }
            }

            return ok;
        }

        /// <summary>
        /// Returns the ComboItem that is currently selected OR whose Name matches the typed text.
        /// Needed because DropDown style allows typing, which may not set SelectedItem.
        /// </summary>
        private ComboItem ResolveComboItem(ComboBox cmb)
        {
            if (cmb.SelectedItem is ComboItem selected && selected.Id.HasValue)
                return selected;

            var typed = cmb.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(typed)) return null;

            foreach (var obj in cmb.Items)
            {
                if (obj is ComboItem ci && ci.Id.HasValue &&
                    ci.Name.Equals(typed, StringComparison.OrdinalIgnoreCase))
                    return ci;
            }
            return null;
        }

        private class ComboItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }
    }
}
