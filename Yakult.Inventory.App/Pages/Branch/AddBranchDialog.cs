using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Services;


namespace Yakult.Inventory.App.Pages.Branch
{
    public partial class AddBranchDialog : Form
    {
        private Panel headerPanel, bodyPanel, footerPanel;
        private FlowLayoutPanel flowBranches;
        private Button btnAddBranch, btnSave, btnCancel;
        private Label titleLabel;

        // Store results
        public List<BranchDto> ResultBranches { get; private set; }

        // For backward compatibility - returns first branch
        public BranchDto ResultBranch => ResultBranches?.FirstOrDefault();

        // Optional event (keeps parity with original Submitted usage)
        public event Action<BranchDto> Submitted;

        // Edit mode
        private bool isEditMode;
        private BranchDto existingBranch;

        public AddBranchDialog(BranchDto branch = null)
        {
            InitializeComponent();

            isEditMode = branch != null;
            existingBranch = branch;

            SetupForm();
            BuildUi();

            if (isEditMode)
            {
                LoadBranchData();
            }
        }

        private void SetupForm()
        {
            Text = isEditMode ? "Edit Branch" : "Add Branch";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(920, 680);
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
                Text = isEditMode ? "Edit Branch" : "Add Branch",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                Dock = DockStyle.Top
            };

            var titleRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34
            };

            btnAddBranch = new Button
            {
                Text = "+ Add Branch",
                Width = 120,
                Height = 30,
                Visible = !isEditMode,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnAddBranch.Click += (s, e) => AddBranchSection();
            titleRow.Controls.Add(btnAddBranch);
            titleRow.Resize += (s, e) => btnAddBranch.Left = titleRow.ClientSize.Width - btnAddBranch.Width;

            var headerSpacer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 8
            };

            var lblInfo = new Label
            {
                Text = isEditMode
                    ? "Update branch details, then click Save."
                    : "Add one or more branches, then click Save.",
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

            // Body - FlowLayoutPanel for multiple branches
            flowBranches = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(0, 6, 0, 6)
            };
            listHost.Controls.Add(flowBranches);
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

            btnSave.Click += (s, e) => SaveBranches();
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

            // Add the first branch section
            AddBranchSection();
        }

        private void AddBranchSection()
        {
            int scrollBarW = SystemInformation.VerticalScrollBarWidth;
            int sectionWidth = Math.Max(0, flowBranches.ClientSize.Width - (scrollBarW + 32));

            var branchSection = new BranchSection
            {
                Width = sectionWidth,
                Margin = new Padding(0, 4, 0, 8),
                ShowRemoveButton = !isEditMode // Don't show remove in edit mode
            };
            branchSection.RemoveRequested += (sender, e) =>
            {
                if (flowBranches.Controls.Count > 1)
                {
                    flowBranches.Controls.Remove(branchSection);
                }
                else
                {
                    MessageBox.Show("At least one branch section is required.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            flowBranches.Controls.Add(branchSection);
            flowBranches.Resize += (s, e) =>
            {
                int sb = SystemInformation.VerticalScrollBarWidth;
                branchSection.Width = Math.Max(0, flowBranches.ClientSize.Width - (sb + 32));
            };
        }

        private void LoadBranchData()
        {
            if (existingBranch == null) return;

            // Clear and add single section for edit
            flowBranches.Controls.Clear();

            var section = new BranchSection
            {
                Width = Math.Max(0, flowBranches.ClientSize.Width - (SystemInformation.VerticalScrollBarWidth + 32)),
                Margin = new Padding(0, 4, 0, 8),
                ShowRemoveButton = false // No remove button in edit mode
            };

            // Load existing data
            section.SetBranchData(existingBranch);

            flowBranches.Controls.Add(section);
            flowBranches.Resize += (s, e) =>
            {
                int sb = SystemInformation.VerticalScrollBarWidth;
                section.Width = Math.Max(0, flowBranches.ClientSize.Width - (sb + 32));
            };
        }

        private void SaveBranches()
        {
            ResultBranches = new List<BranchDto>();

            foreach (var section in flowBranches.Controls.OfType<BranchSection>())
            {
                if (string.IsNullOrWhiteSpace(section.BranchName))
                {
                    MessageBox.Show("Please enter a Branch Name for all sections.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Split branch names by newlines to support multiple branches at once
                var branchNames = section.BranchName
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(name => name.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();

                if (branchNames.Count == 0)
                {
                    MessageBox.Show("Please enter at least one Branch Name.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // In edit mode, prevent multiple branches with same ID
                if (isEditMode && branchNames.Count > 1)
                {
                    MessageBox.Show("Cannot enter multiple branch names in edit mode. Please enter only one branch name.",
                        "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Create a branch entry for each name (one per line)
                foreach (var branchName in branchNames)
                {
                    var branch = new BranchDto
                    {
                        BranchId = isEditMode ? existingBranch.BranchId : 0,
                        Name = branchName,
                        Description = section.BranchDescription.Trim(),
                        DateCreated = section.DateCreated,
                        CreatedByUserId = isEditMode ? existingBranch.CreatedByUserId : AppSession.CurrentUserId,
                        CreatedByName = isEditMode ? existingBranch.CreatedByName : AppSession.CurrentUserName,
                        CompanyId = section.SelectedCompanyId,
                        DepartmentId = section.SelectedDepartmentId,
                        IsFactory = section.IsFactory,
                        IsDepot = section.IsDepot,
                        IsCenter = section.IsCenter,
                        IsDistributor = section.IsDistributor,
                        CenterRegion = section.CenterRegion
                    };

                    ResultBranches.Add(branch);
                }
            }

            // Save all branches to database
            try
            {
                foreach (var branch in ResultBranches)
                {
                    SaveBranchToDatabase(branch);
                }

                MessageBox.Show($"Successfully saved {ResultBranches.Count} branch(es)!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Raise Submitted event for first branch (backward compatibility)
                if (ResultBranches.Any())
                {
                    Submitted?.Invoke(ResultBranches.First());
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving branch(es): {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveBranchToDatabase(BranchDto branch)
        {
            var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(cs))
                throw new Exception("Connection string not found.");

            using (var con = new SqlConnection(cs))
            {
                con.Open();
                var sql = @"INSERT INTO dbo.Branch
                           (Name, Description, IsFactory, IsDepot, IsCenter, CenterRegion, IsDistributor, DateCreated, Createdby, Active)
                           OUTPUT INSERTED.BranchId
                           VALUES
                           (@Name, @Description, @IsFactory, @IsDepot, @IsCenter, @CenterRegion, @IsDistributor, @DateCreated, @Createdby, @Active)";

                int branchId;
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Name", branch.Name);
                    cmd.Parameters.AddWithValue("@Description", (object)branch.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsFactory", branch.IsFactory);
                    cmd.Parameters.AddWithValue("@IsDepot", branch.IsDepot);
                    cmd.Parameters.AddWithValue("@IsCenter", branch.IsCenter);
                    cmd.Parameters.AddWithValue("@CenterRegion", (object)branch.CenterRegion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsDistributor", branch.IsDistributor);
                    cmd.Parameters.AddWithValue("@DateCreated", branch.DateCreated);
                    cmd.Parameters.AddWithValue("@Createdby", branch.CreatedByUserId);
                    cmd.Parameters.AddWithValue("@Active", true);
                    branchId = (int)cmd.ExecuteScalar();
                    ActivityLogger.Log(ActivityLogger.Actions.Create, "Branch", branchId, $"Branch '{branch.Name}' created");
                }

                if (branch.CompanyId.HasValue)
                {
                    using (var bdcCmd = new SqlCommand(@"
                        INSERT INTO dbo.BranchDepartmentCompany (BranchID, DepartmentID, CompanyID)
                        VALUES (@BranchId, @DeptId, @ComId)", con))
                    {
                        bdcCmd.Parameters.AddWithValue("@BranchId", branchId);
                        bdcCmd.Parameters.AddWithValue("@DeptId", branch.DepartmentId.HasValue ? (object)branch.DepartmentId.Value : DBNull.Value);
                        bdcCmd.Parameters.AddWithValue("@ComId", branch.CompanyId.Value);
                        bdcCmd.ExecuteNonQuery();
                    }
                }
            }
        }
    }

    #region BranchSection Control

    public class BranchSection : Panel
    {
        private TextBox txtBranchName, txtBranchDesc, txtCreatedBy;
        private DateTimePicker dtCreated;
        private ComboBox cmbCompany, cmbDepartment, cmbCenterRegion;
        private Label lblCompany, lblDepartment, lblCenterRegion;
        private CheckBox chkAssociateCompany, chkAssociateDepartment;
        private GroupBox grpCategories, grpCenterRegions;
        private CheckBox chkFactory, chkDepots, chkCenters, chkDistributors;
        private Button btnRemove;

        public event EventHandler RemoveRequested;

        public bool ShowRemoveButton
        {
            get => btnRemove.Visible;
            set => btnRemove.Visible = value;
        }

        public string BranchName => txtBranchName.Text;
        public string BranchDescription => txtBranchDesc.Text;
        public DateTime DateCreated => dtCreated.Value;
        public int? SelectedCompanyId => (chkAssociateCompany.Checked && cmbCompany.SelectedItem is CompanyItem compItem && compItem.Id.HasValue)
            ? compItem.Id.Value : (int?)null;
        public int? SelectedDepartmentId => (chkAssociateDepartment.Checked && cmbDepartment.SelectedItem is DepartmentItem deptItem && deptItem.Id.HasValue)
            ? deptItem.Id.Value : (int?)null;
        public bool IsFactory => chkFactory.Checked;
        public bool IsDepot => chkDepots.Checked;
        public bool IsCenter => chkCenters.Checked;
        public bool IsDistributor => chkDistributors.Checked;
        public string CenterRegion => (chkCenters.Checked && cmbCenterRegion.SelectedIndex > 0)
            ? cmbCenterRegion.SelectedItem.ToString() : null;

        public BranchSection()
        {
            BorderStyle = BorderStyle.FixedSingle;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            BuildControls();
        }

        public void SetBranchData(BranchDto branch)
        {
            txtBranchName.Text = branch.Name ?? string.Empty;
            txtBranchDesc.Text = branch.Description ?? string.Empty;
            dtCreated.Value = branch.DateCreated;
            txtCreatedBy.Text = branch.CreatedByName ?? string.Empty;

            // Set company association
            if (branch.CompanyId.HasValue)
            {
                chkAssociateCompany.Checked = true;
                LoadCompanies();
                SelectCompanyById(branch.CompanyId.Value);
            }

            // Set department association
            if (branch.DepartmentId.HasValue)
            {
                chkAssociateDepartment.Checked = true;
                LoadDepartments();
                SelectDepartmentById(branch.DepartmentId.Value);
            }

            // Set categories
            chkFactory.Checked = branch.IsFactory;
            chkDepots.Checked = branch.IsDepot;
            chkCenters.Checked = branch.IsCenter;
            chkDistributors.Checked = branch.IsDistributor;

            // Set center region
            if (!string.IsNullOrWhiteSpace(branch.CenterRegion))
            {
                for (int i = 0; i < cmbCenterRegion.Items.Count; i++)
                {
                    if (cmbCenterRegion.Items[i].ToString() == branch.CenterRegion)
                    {
                        cmbCenterRegion.SelectedIndex = i;
                        break;
                    }
                }
            }
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

        private void SelectDepartmentById(int departmentId)
        {
            for (int i = 0; i < cmbDepartment.Items.Count; i++)
            {
                if (cmbDepartment.Items[i] is DepartmentItem item && item.Id == departmentId)
                {
                    cmbDepartment.SelectedIndex = i;
                    break;
                }
            }
        }

        private void BuildControls()
        {
            int leftA = 8;
            int leftB = 460;
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

            // Branch Name
            var lblName = new Label { AutoSize = true, Text = "Branch Name *", Left = leftA, Top = currentTop + 8 };
            txtBranchName = new TextBox { Left = leftA, Top = currentTop + 28, Width = 420 };
            Controls.AddRange(new Control[] { lblName, txtBranchName });

            // Description
            var lblDesc = new Label { AutoSize = true, Text = "Description", Left = leftA, Top = currentTop + 60 };
            txtBranchDesc = new TextBox { Left = leftA, Top = currentTop + 80, Width = 420, Multiline = true, Height = 60 };
            Controls.AddRange(new Control[] { lblDesc, txtBranchDesc });

            // Date Created (right side)
            var lblCreated = new Label { AutoSize = true, Text = "Date Created", Left = leftB, Top = currentTop + 8 };
            dtCreated = new DateTimePicker { Left = leftB, Top = currentTop + 28, Width = 180, Value = DateTime.Now, Format = DateTimePickerFormat.Custom, CustomFormat = "MM/dd/yyyy" };
            Controls.AddRange(new Control[] { lblCreated, dtCreated });

            // Created By (right side)
            var lblBy = new Label { AutoSize = true, Text = "Created By", Left = leftB, Top = currentTop + 60 };
            txtCreatedBy = new TextBox
            {
                Left = leftB,
                Top = currentTop + 80,
                Width = 180,
                ReadOnly = true,
                TabStop = false,
                BackColor = SystemColors.Window,
                Text = AppSession.CurrentUserName ?? string.Empty
            };
            Controls.AddRange(new Control[] { lblBy, txtCreatedBy });

            currentTop += 160;

            // Associate with Company
            chkAssociateCompany = new CheckBox
            {
                AutoSize = true,
                Text = "Associate with Company (optional)",
                Left = leftA,
                Top = currentTop,
                Checked = false
            };
            Controls.Add(chkAssociateCompany);

            lblCompany = new Label { AutoSize = true, Text = "Select Company", Left = leftA, Top = currentTop + 24, Visible = false };
            cmbCompany = new ComboBox
            {
                Left = leftA,
                Top = currentTop + 44,
                Width = 420,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };
            Controls.AddRange(new Control[] { lblCompany, cmbCompany });

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

            // Associate with Department
            chkAssociateDepartment = new CheckBox
            {
                AutoSize = true,
                Text = "Associate with Department (optional)",
                Left = leftA,
                Top = currentTop,
                Checked = false
            };
            Controls.Add(chkAssociateDepartment);

            lblDepartment = new Label { AutoSize = true, Text = "Select Department", Left = leftA, Top = currentTop + 24, Visible = false };
            cmbDepartment = new ComboBox
            {
                Left = leftA,
                Top = currentTop + 44,
                Width = 420,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };
            Controls.AddRange(new Control[] { lblDepartment, cmbDepartment });

            chkAssociateDepartment.CheckedChanged += (s, e) =>
            {
                lblDepartment.Visible = chkAssociateDepartment.Checked;
                cmbDepartment.Visible = chkAssociateDepartment.Checked;
                if (chkAssociateDepartment.Checked && cmbDepartment.Items.Count == 0)
                {
                    LoadDepartments();
                }
            };

            currentTop += 80;

            // Branch Categories GroupBox
            grpCategories = new GroupBox
            {
                Text = "Branch Categories",
                Left = leftA,
                Top = currentTop,
                Width = 420,
                Height = 145
            };

            chkFactory = new CheckBox { AutoSize = true, Text = "Factory", Left = 10, Top = 25 };
            chkDepots = new CheckBox { AutoSize = true, Text = "Depots", Left = 10, Top = 50 };
            chkCenters = new CheckBox { AutoSize = true, Text = "Centers", Left = 10, Top = 75 };
            chkDistributors = new CheckBox { AutoSize = true, Text = "Distributors", Left = 10, Top = 100 };

            grpCategories.Controls.AddRange(new Control[] { chkFactory, chkDepots, chkCenters, chkDistributors });
            Controls.Add(grpCategories);

            chkCenters.CheckedChanged += (s, e) => grpCenterRegions.Enabled = chkCenters.Checked;

            currentTop += 155;

            // Center Regions GroupBox
            grpCenterRegions = new GroupBox
            {
                Text = "Center Region (if Centers is checked)",
                Left = leftA,
                Top = currentTop,
                Width = 420,
                Height = 90,
                Enabled = false
            };

            lblCenterRegion = new Label { AutoSize = true, Text = "Select Region:", Left = 10, Top = 25 };
            cmbCenterRegion = new ComboBox
            {
                Left = 10,
                Top = 45,
                Width = 390,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            cmbCenterRegion.Items.AddRange(new object[]
            {
                "-- None --",
                "Metro Manila - A",
                "Metro Manila - B",
                "Luzon",
                "Visayas",
                "Mindanao"
            });
            cmbCenterRegion.SelectedIndex = 0;

            grpCenterRegions.Controls.AddRange(new Control[] { lblCenterRegion, cmbCenterRegion });
            Controls.Add(grpCenterRegions);

            currentTop += 100;

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
                    using (var cmd = new SqlCommand("SELECT ComId, Name FROM dbo.Company ORDER BY Name", con))
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

        private void LoadDepartments()
        {
            cmbDepartment.Items.Clear();
            cmbDepartment.Items.Add(new DepartmentItem { Id = null, Name = "-- None --" });

            try
            {
                var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand("SELECT DeptId, Name FROM dbo.Department ORDER BY Name", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbDepartment.Items.Add(new DepartmentItem
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }

                if (cmbDepartment.Items.Count > 0)
                    cmbDepartment.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load departments: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private class CompanyItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private class DepartmentItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }
    }

    #endregion
}
