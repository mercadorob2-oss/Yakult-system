using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Core;


namespace Yakult.Inventory.App.Pages.Department
{
    public partial class AddDepartmentDialog : Form
    {
        private Panel headerPanel, bodyPanel, footerPanel;
        private FlowLayoutPanel flowDepartments;
        private Button btnAddDepartment, btnSave, btnCancel;
        private Label titleLabel;

        // Store results
        public List<DepartmentDto> ResultDepartments { get; private set; }

        // For backward compatibility - returns first department
        public DepartmentDto ResultDepartment => ResultDepartments?.FirstOrDefault();

        // Optional event (keeps parity with original Submitted usage)
        public event Action<DepartmentDto> Submitted;

        // Edit mode
        private bool isEditMode;
        private DepartmentDto existingDepartment;

        public AddDepartmentDialog(DepartmentDto department = null)
        {
            InitializeComponent();

            isEditMode = department != null;
            existingDepartment = department;

            SetupForm();
            BuildUi();

            if (isEditMode)
            {
                LoadDepartmentData();
            }
        }

        private void SetupForm()
        {
            Text = isEditMode ? "Edit Department" : "Add Department";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(1000, 680);  // Increased width to accommodate wider fields (was 920)
        }

        private void BuildUi()
        {
            BackColor = Color.FromArgb(245, 246, 250);
            Font = new Font("Segoe UI", 9F);

            Controls.Clear();

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor,
                Padding = new Padding(16)
            };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(rootLayout);

            var scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                BackColor = BackColor,
                Padding = new Padding(0)
            };
            rootLayout.Controls.Add(scrollHost, 0, 0);

            var cardPanel = new ReaLTaiizor.Controls.Panel
            {
                BackColor = Color.White,
                EdgeColor = Color.FromArgb(220, 220, 220),
                Dock = DockStyle.Fill,
                AutoSize = false,
                Padding = new Padding(32, 24, 32, 32),
                SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality
            };
            scrollHost.Controls.Add(cardPanel);

            titleLabel = new Label
            {
                AutoSize = false,
                Height = 34,
                Padding = new Padding(0, 0, 0, 6),
                Text = isEditMode ? "Edit Department" : "Add Department",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                Dock = DockStyle.Top
            };

            var titleRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34
            };

            btnAddDepartment = new Button
            {
                Text = "+ Add Department",
                Width = 140,
                Height = 30,
                Visible = !isEditMode,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnAddDepartment.Click += (s, e) => AddDepartmentSection();
            titleRow.Controls.Add(btnAddDepartment);
            titleRow.Resize += (s, e) => btnAddDepartment.Left = titleRow.ClientSize.Width - btnAddDepartment.Width;

            var headerSpacer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 8
            };

            var lblInfo = new Label
            {
                Text = isEditMode
                    ? "Update department details, then click Save."
                    : "Add one or more departments, then click Save.",
                AutoSize = false,
                Height = 40,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(60, 60, 60),
                Padding = new Padding(12, 8, 12, 8),
                BackColor = Color.FromArgb(230, 240, 255),
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Top
            };

            bodyPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                Padding = new Padding(0)
            };

            var listHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(0)
            };

            flowDepartments = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(0, 6, 0, 6)
            };
            listHost.Controls.Add(flowDepartments);
            bodyPanel.Controls.Add(listHost);

            cardPanel.Controls.Add(bodyPanel);
            cardPanel.Controls.Add(lblInfo);
            cardPanel.Controls.Add(headerSpacer);
            cardPanel.Controls.Add(titleRow);
            cardPanel.Controls.Add(titleLabel);

            btnSave = new Button
            {
                Text = "Save",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 42),
                Padding = new Padding(18, 0, 18, 0),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 42),
                Padding = new Padding(18, 0, 18, 0),
                FlatStyle = FlatStyle.Flat
            };

            btnSave.Click += (s, e) => SaveDepartments();
            btnCancel.Click += (s, e) =>
            {
                var result = MessageBox.Show("Are you sure you want to cancel?", "Cancel",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };

            var buttonBar = new FlowLayoutPanel
            {
                Dock = DockStyle.None,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 14, 0, 0),
                Anchor = AnchorStyles.Right
            };
            btnCancel.Margin = new Padding(0);
            btnSave.Margin = new Padding(8, 0, 0, 0);
            buttonBar.Controls.Add(btnCancel);
            buttonBar.Controls.Add(btnSave);
            rootLayout.Controls.Add(buttonBar, 0, 1);

            AddDepartmentSection();
        }

        private void AddDepartmentSection()
        {
            int scrollBarW = SystemInformation.VerticalScrollBarWidth;
            int sectionWidth = Math.Max(0, flowDepartments.ClientSize.Width - (scrollBarW + 32));

            var departmentSection = new AddDepartmentSection
            {
                Width = sectionWidth,
                Margin = new Padding(0, 4, 0, 8),
                ShowRemoveButton = !isEditMode // Don't show remove in edit mode
            };
            departmentSection.RemoveRequested += (sender, e) =>
            {
                if (flowDepartments.Controls.Count > 1)
                {
                    flowDepartments.Controls.Remove(departmentSection);
                }
                else
                {
                    MessageBox.Show("At least one department section is required.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            flowDepartments.Controls.Add(departmentSection);
            flowDepartments.Resize += (s, e) =>
            {
                int sb = SystemInformation.VerticalScrollBarWidth;
                departmentSection.Width = Math.Max(0, flowDepartments.ClientSize.Width - (sb + 32));
            };
        }

        private void LoadDepartmentData()
        {
            if (existingDepartment == null) return;

            // Clear and add single section for edit
            flowDepartments.Controls.Clear();

            var section = new AddDepartmentSection
            {
                Width = Math.Max(0, flowDepartments.ClientSize.Width - (SystemInformation.VerticalScrollBarWidth + 32)),
                Margin = new Padding(0, 4, 0, 8),
                ShowRemoveButton = false // No remove button in edit mode
            };

            // Load existing data
            section.SetDepartmentData(existingDepartment);

            flowDepartments.Controls.Add(section);
            flowDepartments.Resize += (s, e) =>
            {
                int sb = SystemInformation.VerticalScrollBarWidth;
                section.Width = Math.Max(0, flowDepartments.ClientSize.Width - (sb + 32));
            };
        }

        private void SaveDepartments()
        {
            ResultDepartments = new List<DepartmentDto>();

            foreach (var section in flowDepartments.Controls.OfType<AddDepartmentSection>())
            {
                if (string.IsNullOrWhiteSpace(section.DeptName))
                {
                    MessageBox.Show("Please enter a Department Name for all sections.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Split department names by newlines to support multiple departments at once
                var departmentNames = section.DeptName
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(name => name.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();

                if (departmentNames.Count == 0)
                {
                    MessageBox.Show("Please enter at least one Department Name.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // In edit mode, prevent multiple departments with same ID
                if (isEditMode && departmentNames.Count > 1)
                {
                    MessageBox.Show("Cannot enter multiple department names in edit mode. Please enter only one department name.",
                        "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Create a department entry for each name (one per line)
                foreach (var deptName in departmentNames)
                {
                    var department = new DepartmentDto
                    {
                        DeptId = isEditMode ? existingDepartment.DeptId : 0,
                        Name = deptName,
                        Section = section.Section,
                        Description = section.DeptDescription.Trim(),
                        DateCreated = section.DateCreated,
                        CreatedByUserId = isEditMode ? existingDepartment.CreatedByUserId : AppSession.CurrentUserId,
                        CreatedByName = isEditMode ? existingDepartment.CreatedByName : AppSession.CurrentUserName,
                        CompanyId = section.SelectedCompanyId,
                        BranchId = section.SelectedBranchId,
                        Active = section.Active,
                        IsArchived = section.IsArchived
                    };

                    ResultDepartments.Add(department);
                }
            }

            // Save all departments to database
            try
            {
                int savedCount = 0;
                List<string> failedDepartments = new List<string>();

                foreach (var department in ResultDepartments)
                {
                    try
                    {
                        SaveDepartmentToDatabase(department);
                        savedCount++;
                    }
                    catch (Exception ex)
                    {
                        failedDepartments.Add($"{department.Name}: {ex.Message}");
                    }
                }

                // Show detailed success/failure message
                if (savedCount > 0 && failedDepartments.Count == 0)
                {
                    MessageBox.Show($"Successfully saved {savedCount} department(s)!\n\nDepartments added:\n{string.Join("\n", ResultDepartments.Select(d => "• " + d.Name))}",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (savedCount > 0 && failedDepartments.Count > 0)
                {
                    MessageBox.Show($"Saved {savedCount} department(s), but {failedDepartments.Count} failed:\n\n{string.Join("\n", failedDepartments)}",
                        "Partial Success", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show($"Failed to save all departments:\n\n{string.Join("\n", failedDepartments)}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return; // Don't close dialog if all failed
                }

                // Raise Submitted event for first department (backward compatibility)
                if (ResultDepartments.Any())
                {
                    Submitted?.Invoke(ResultDepartments.First());
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}\n\nStack trace:\n{ex.StackTrace}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveDepartmentToDatabase(DepartmentDto dept)
        {
            var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(cs))
                throw new Exception("Connection string not found.");

            using (var con = new SqlConnection(cs))
            {
                con.Open();
                // Departments are global lookups: dbo.Department has no ComId/BrId
                // columns (company/branch links live in BranchDepartmentCompany and
                // DepartmentAccount). The placeholder below handles the association.
                var sql = @"INSERT INTO dbo.Department (Name, Section, Description, DateCreated, Createdby, Active)
                           VALUES (@Name, @Section, @Description, @DateCreated, @Createdby, @Active)";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Name",        dept.Name);
                    cmd.Parameters.AddWithValue("@Section",     (object)dept.Section ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Description", (object)dept.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DateCreated", dept.DateCreated);
                    cmd.Parameters.AddWithValue("@Createdby",   dept.CreatedByUserId);
                    cmd.Parameters.AddWithValue("@Active",      true);
                    cmd.ExecuteNonQuery();
                }

                // If the department is linked to both a Company and Branch, create a placeholder
                // row in DepartmentAccount so it appears on the Department Accounts page immediately
                // (same as what the seed script does for existing departments).
                if (dept.CompanyId.HasValue && dept.BranchId.HasValue)
                {
                    const string placeholderSql = @"
                        INSERT INTO dbo.DepartmentAccount (CompanyName, DepartmentName, BranchName)
                        SELECT c.Name, @DeptName, b.Name
                        FROM dbo.Company c
                        CROSS JOIN dbo.Branch b
                        WHERE c.ComId     = @ComId
                          AND b.BranchId  = @BrId
                          AND NOT EXISTS (
                              SELECT 1 FROM dbo.DepartmentAccount da
                              WHERE da.CompanyName    = c.Name
                                AND da.DepartmentName = @DeptName
                                AND da.BranchName     = b.Name
                          )";

                    using (var cmd = new SqlCommand(placeholderSql, con))
                    {
                        cmd.Parameters.AddWithValue("@DeptName", dept.Name);
                        cmd.Parameters.AddWithValue("@ComId",    dept.CompanyId.Value);
                        cmd.Parameters.AddWithValue("@BrId",     dept.BranchId.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }
    }

    #region AddDepartmentSection Control

    public class AddDepartmentSection : Panel
    {
        private TextBox txtDeptName, txtSection, txtDeptDesc, txtDateCreated;
        private DateTime _dateCreated = DateTime.Now;
        private ComboBox cmbCompany;
        private Label lblCompany;
        private CheckBox chkAssociateCompany;
        private ComboBox cmbBranch;
        private Label lblBranch;
        private CheckBox chkAssociateBranch;
        private CheckBox chkActive, chkIsArchived;
        private Button btnRemove;

        public event EventHandler RemoveRequested;

        public bool ShowRemoveButton
        {
            get => btnRemove.Visible;
            set => btnRemove.Visible = value;
        }

        public string DeptName => txtDeptName.Text;
        public string Section => txtSection.Text.Trim();
        public string DeptDescription => txtDeptDesc.Text;
        public DateTime DateCreated => _dateCreated;
        public int? SelectedCompanyId => (chkAssociateCompany.Checked && cmbCompany.SelectedItem is CompanyItem item && item.Id.HasValue)
            ? item.Id.Value : (int?)null;
        public int? SelectedBranchId => (chkAssociateBranch.Checked && cmbBranch.SelectedItem is BranchItem bitem && bitem.Id.HasValue)
            ? bitem.Id.Value : (int?)null;
        public bool Active => chkActive?.Checked ?? true;
        public bool IsArchived => chkIsArchived?.Checked ?? false;

        public AddDepartmentSection()
        {
            BorderStyle = BorderStyle.FixedSingle;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            BuildControls();
        }

        public void SetDepartmentData(DepartmentDto department)
        {
            txtDeptName.Text = department.Name ?? string.Empty;
            txtSection.Text  = department.Section ?? string.Empty;
            txtDeptDesc.Text = department.Description ?? string.Empty;
            _dateCreated = department.DateCreated;
            txtDateCreated.Text = department.DateCreated.ToString("MM/dd/yyyy");

            // Set company association
            if (department.CompanyId.HasValue)
            {
                chkAssociateCompany.Checked = true;
                LoadCompanies();
                SelectCompanyById(department.CompanyId.Value);
            }

            // Set branch association
            if (department.BranchId.HasValue)
            {
                chkAssociateBranch.Checked = true;
                LoadBranches();
                SelectBranchById(department.BranchId.Value);
            }

            // Load Active and IsArchived
            if (chkActive != null) chkActive.Checked = department.Active;
            if (chkIsArchived != null) chkIsArchived.Checked = department.IsArchived;
        }

        private void SelectCompanyById(int companyId)
        {
            for (int i = 0; i < cmbCompany.Items.Count; i++)
            {
                if (cmbCompany.Items[i] is CompanyItem item && item.Id == companyId)
                {
                    cmbCompany.SelectedIndex = i;
                    break;
                }
            }
        }

        private void SelectBranchById(int branchId)
        {
            for (int i = 0; i < cmbBranch.Items.Count; i++)
            {
                if (cmbBranch.Items[i] is BranchItem item && item.Id == branchId)
                {
                    cmbBranch.SelectedIndex = i;
                    break;
                }
            }
        }

        private void BuildControls()
        {
            int leftA = 8;
            int leftB = 620;  // Moved right to accommodate wider fields (was 460)
            int fieldWidth = 600;  // Wider fields for long department names (was 420)
            int currentTop = 8;

            // Remove button (top right)
            btnRemove = new Button
            {
                Text = "× Remove",
                Width = 90,
                Height = 28,
                Top = 8,
                ForeColor = Color.Red
            };
            btnRemove.Click += (s, e) => RemoveRequested?.Invoke(this, EventArgs.Empty);
            Controls.Add(btnRemove);

            // Department Name - supports multiple names (one per line)
            var lblName = new Label { AutoSize = true, Text = "Department Name(s) * (one per line)", Left = leftA, Top = currentTop + 8 };
            txtDeptName = new TextBox
            {
                Left = leftA,
                Top = currentTop + 28,
                Width = fieldWidth,
                Multiline = true,
                Height = 60,
                ScrollBars = ScrollBars.Vertical
            };
            Controls.AddRange(new Control[] { lblName, txtDeptName });

            // Section (optional — sub-grouping within a department)
            var lblSection = new Label { AutoSize = true, Text = "Section (optional)", Left = leftA, Top = currentTop + 100 };
            txtSection = new TextBox { Left = leftA, Top = currentTop + 120, Width = fieldWidth, Height = 23 };
            Controls.AddRange(new Control[] { lblSection, txtSection });

            // Description
            var lblDesc = new Label { AutoSize = true, Text = "Description", Left = leftA, Top = currentTop + 155 };
            txtDeptDesc = new TextBox { Left = leftA, Top = currentTop + 175, Width = fieldWidth, Multiline = true, Height = 60 };
            Controls.AddRange(new Control[] { lblDesc, txtDeptDesc });

            // Date Created (right side)
            var lblCreated = new Label { AutoSize = true, Text = "Date Created", Left = leftB, Top = currentTop + 8 };
            txtDateCreated = new TextBox
            {
                Left = leftB,
                Top = currentTop + 28,
                Width = 180,
                ReadOnly = true,
                TabStop = false,
                BackColor = SystemColors.Control,
                Text = _dateCreated.ToString("MM/dd/yyyy")
            };
            Controls.AddRange(new Control[] { lblCreated, txtDateCreated });

            currentTop += 190;

            // Associate with Company checkbox
            chkAssociateCompany = new CheckBox
            {
                AutoSize = true,
                Text = "Associate with Company (optional)",
                Left = leftA,
                Top = currentTop,
                Checked = false
            };
            Controls.Add(chkAssociateCompany);

            // Company dropdown label and combobox
            lblCompany = new Label { AutoSize = true, Text = "Select Company", Left = leftA, Top = currentTop + 24, Visible = false };
            cmbCompany = new ComboBox
            {
                Left = leftA,
                Top = currentTop + 44,
                Width = fieldWidth,  // Match department name width
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };

            Controls.AddRange(new Control[] { lblCompany, cmbCompany });

            // Toggle company dropdown visibility
            chkAssociateCompany.CheckedChanged += (s, e) =>
            {
                lblCompany.Visible = chkAssociateCompany.Checked;
                cmbCompany.Visible = chkAssociateCompany.Checked;
                if (chkAssociateCompany.Checked && cmbCompany.Items.Count == 0)
                {
                    LoadCompanies();
                }
            };

            currentTop += 80;

            // Associate with Branch checkbox
            chkAssociateBranch = new CheckBox
            {
                AutoSize = true,
                Text = "Associate with Branch (optional)",
                Left = leftA,
                Top = currentTop,
                Checked = false
            };
            Controls.Add(chkAssociateBranch);

            // Branch dropdown label and combobox
            lblBranch = new Label { AutoSize = true, Text = "Select Branch", Left = leftA, Top = currentTop + 24, Visible = false };
            cmbBranch = new ComboBox
            {
                Left = leftA,
                Top = currentTop + 44,
                Width = fieldWidth,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };

            Controls.AddRange(new Control[] { lblBranch, cmbBranch });

            chkAssociateBranch.CheckedChanged += (s, e) =>
            {
                lblBranch.Visible = chkAssociateBranch.Checked;
                cmbBranch.Visible = chkAssociateBranch.Checked;
                if (chkAssociateBranch.Checked && cmbBranch.Items.Count == 0)
                {
                    LoadBranches();
                }
            };

            currentTop += 80;

            // Active checkbox
            var lblActive = new Label { AutoSize = true, Text = "Active", Left = leftA, Top = currentTop };
            chkActive = new CheckBox { Left = leftA + 100, Top = currentTop, AutoSize = true, Checked = true };
            Controls.AddRange(new Control[] { lblActive, chkActive });

            currentTop += 30;

            // Is Archived checkbox
            var lblIsArchived = new Label { AutoSize = true, Text = "Is Archived", Left = leftA, Top = currentTop };
            chkIsArchived = new CheckBox { Left = leftA + 100, Top = currentTop, AutoSize = true };
            chkIsArchived.CheckedChanged += (s, e) =>
            {
                if (chkIsArchived.Checked)
                {
                    chkActive.Checked = false;
                }
            };
            Controls.AddRange(new Control[] { lblIsArchived, chkIsArchived });

            currentTop += 30;

            // Set panel height
            Height = currentTop + 12;

            // Position remove button
            Resize += (s, e) => btnRemove.Left = Width - btnRemove.Width - 12;
        }

        private void LoadCompanies()
        {
            cmbCompany.Items.Clear();
            cmbCompany.Items.Add(new CompanyItem { Id = null, Name = "-- None --" });

            try
            {
                var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand("SELECT ComId, Name FROM dbo.Company WHERE Active = 1 ORDER BY Name", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbCompany.Items.Add(new CompanyItem
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }

                if (cmbCompany.Items.Count > 0)
                    cmbCompany.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load companies: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadBranches()
        {
            cmbBranch.Items.Clear();
            cmbBranch.Items.Add(new BranchItem { Id = null, Name = "-- None --" });

            try
            {
                var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand("SELECT BranchId, Name FROM dbo.Branch WHERE Active = 1 ORDER BY Name", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbBranch.Items.Add(new BranchItem
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }

                if (cmbBranch.Items.Count > 0)
                    cmbBranch.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load branches: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private class CompanyItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private class BranchItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }
    }

    #endregion
}

