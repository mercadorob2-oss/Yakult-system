using Yakult.Inventory.App.Session;
using System;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;


namespace Yakult.Inventory.App.Pages.Branch
{
    public partial class AddBranchPage : UserControl
    {
        private Panel headerPanel, bodyPanel, footerPanel;
        private TextBox txtBranchName, txtBranchDesc, txtCreatedBy;
        private DateTimePicker dtCreated;
        private Button btnSave, btnCancel;
        private Label titleLabel;

        // Optional: ComboBox to associate with existing companies and departments
        private ComboBox cmbCompany, cmbDepartment;
        private Label lblCompany, lblDepartment;
        private CheckBox chkAssociateCompany, chkAssociateDepartment;
        
        // Branch Categories
        private GroupBox grpCategories;
        private CheckBox chkFactory, chkDepots, chkCenters, chkDistributors;
        private GroupBox grpCenterRegions;
        private ComboBox cmbCenterRegion;
        private Label lblCenterRegion;

        public event Action<BranchDto> Submitted;

        public AddBranchPage()
        {
            InitializeComponent();
            BuildUi();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            AutoScroll = false;

            // Create panels
            bodyPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };
            footerPanel = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(12, 8, 12, 8) };
            headerPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(12, 8, 12, 0) };

            Controls.Add(bodyPanel);
            Controls.Add(footerPanel);
            Controls.Add(headerPanel);

            // Header
            titleLabel = new Label
            {
                AutoSize = true,
                Text = "Add Branch",
                Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
                Left = 0,
                Top = 0
            };
            headerPanel.Controls.Add(titleLabel);

            // Footer buttons
            btnSave = new Button { Text = "Save", Top = 8, Width = 80 };
            btnCancel = new Button { Text = "Cancel", Top = 8, Width = 80 };
            footerPanel.Controls.AddRange(new Control[] { btnSave, btnCancel });
            footerPanel.Resize += (s, e) =>
            {
                btnCancel.Left = footerPanel.ClientSize.Width - btnCancel.Width - 12;
                btnSave.Left = btnCancel.Left - btnSave.Width - 8;
            };

            // Body - Branch fields
            var lblName = new Label { AutoSize = true, Text = "Branch Name *", Left = 0, Top = 8 };
            txtBranchName = new TextBox { Left = 0, Top = 28, Width = 420 };

            var lblDesc = new Label { AutoSize = true, Text = "Description", Left = 0, Top = 60 };
            txtBranchDesc = new TextBox { Left = 0, Top = 80, Width = 420, Multiline = true, Height = 60 };

            var lblCreated = new Label { AutoSize = true, Text = "Date Created", Left = 450, Top = 8 };
            dtCreated = new DateTimePicker { Left = 450, Top = 28, Width = 180, Value = DateTime.Now };

            var lblBy = new Label { AutoSize = true, Text = "Created By", Left = 450, Top = 60 };
            txtCreatedBy = new TextBox
            {
                Left = 450,
                Top = 80,
                Width = 180,
                ReadOnly = true,
                TabStop = false,
                BackColor = System.Drawing.SystemColors.Window,
                Text = AppSession.CurrentUserName ?? string.Empty
            };

            // Optional company association
            chkAssociateCompany = new CheckBox
            {
                AutoSize = true,
                Text = "Associate with Company (optional)",
                Left = 0,
                Top = 160,
                Checked = false
            };

            lblCompany = new Label { AutoSize = true, Text = "Select Company", Left = 0, Top = 188, Visible = false };
            cmbCompany = new ComboBox
            {
                Left = 0,
                Top = 208,
                Width = 420,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };

            chkAssociateCompany.CheckedChanged += (s, e) =>
            {
                lblCompany.Visible = chkAssociateCompany.Checked;
                cmbCompany.Visible = chkAssociateCompany.Checked;
                if (chkAssociateCompany.Checked)
                {
                    LoadCompanies();
                }
            };

            // Optional department association
            chkAssociateDepartment = new CheckBox
            {
                AutoSize = true,
                Text = "Associate with Department (optional)",
                Left = 0,
                Top = 240,
                Checked = false
            };

            lblDepartment = new Label { AutoSize = true, Text = "Select Department", Left = 0, Top = 268, Visible = false };
            cmbDepartment = new ComboBox
            {
                Left = 0,
                Top = 288,
                Width = 420,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };

            chkAssociateDepartment.CheckedChanged += (s, e) =>
            {
                lblDepartment.Visible = chkAssociateDepartment.Checked;
                cmbDepartment.Visible = chkAssociateDepartment.Checked;
                if (chkAssociateDepartment.Checked)
                {
                    LoadDepartments();
                }
            };

            // Branch Categories GroupBox
            grpCategories = new GroupBox
            {
                Text = "Branch Categories",
                Left = 0,
                Top = 320,
                Width = 420,
                Height = 145
            };

            chkFactory = new CheckBox
            {
                AutoSize = true,
                Text = "Factory",
                Left = 10,
                Top = 25
            };

            chkDepots = new CheckBox
            {
                AutoSize = true,
                Text = "Depots",
                Left = 10,
                Top = 50
            };

            chkCenters = new CheckBox
            {
                AutoSize = true,
                Text = "Centers",
                Left = 10,
                Top = 75
            };

            chkDistributors = new CheckBox
            {
                AutoSize = true,
                Text = "Distributors",
                Left = 10,
                Top = 100
            };

            grpCategories.Controls.AddRange(new Control[] {
                chkFactory, chkDepots, chkCenters, chkDistributors
            });

            // Center Regions GroupBox
            grpCenterRegions = new GroupBox
            {
                Text = "Center Region (if Centers is checked)",
                Left = 0,
                Top = 475,
                Width = 420,
                Height = 90,
                Enabled = false
            };

            lblCenterRegion = new Label
            {
                AutoSize = true,
                Text = "Select Region:",
                Left = 10,
                Top = 25
            };

            cmbCenterRegion = new ComboBox
            {
                Left = 10,
                Top = 50,
                Width = 390,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // Populate Center Regions
            cmbCenterRegion.Items.Add("-- Select Region --");
            cmbCenterRegion.Items.Add("NCR");
            cmbCenterRegion.Items.Add("Region I - Ilocos");
            cmbCenterRegion.Items.Add("Region II - Cagayan Valley");
            cmbCenterRegion.Items.Add("Region III - Central Luzon");
            cmbCenterRegion.Items.Add("Region IV-A - CALABARZON");
            cmbCenterRegion.Items.Add("Region IV-B - MIMAROPA");
            cmbCenterRegion.Items.Add("Region V - Bicol");
            cmbCenterRegion.Items.Add("Region VI - Western Visayas");
            cmbCenterRegion.Items.Add("Region VII - Central Visayas");
            cmbCenterRegion.Items.Add("Region VIII - Eastern Visayas");
            cmbCenterRegion.Items.Add("Region IX - Zamboanga Peninsula");
            cmbCenterRegion.Items.Add("Region X - Northern Mindanao");
            cmbCenterRegion.Items.Add("Region XI - Davao");
            cmbCenterRegion.Items.Add("Region XII - SOCCSKSARGEN");
            cmbCenterRegion.Items.Add("Region XIII - Caraga");
            cmbCenterRegion.Items.Add("CAR - Cordillera");
            cmbCenterRegion.Items.Add("BARMM - Bangsamoro");
            cmbCenterRegion.SelectedIndex = 0;

            grpCenterRegions.Controls.AddRange(new Control[] {
                lblCenterRegion, cmbCenterRegion
            });

            // Enable/disable center regions based on Centers checkbox
            chkCenters.CheckedChanged += (s, e) =>
            {
                grpCenterRegions.Enabled = chkCenters.Checked;
                if (!chkCenters.Checked)
                {
                    cmbCenterRegion.SelectedIndex = 0;
                }
            };

            bodyPanel.Controls.AddRange(new Control[] {
                lblName, txtBranchName, lblDesc, txtBranchDesc,
                lblCreated, dtCreated, lblBy, txtCreatedBy,
                chkAssociateCompany, lblCompany, cmbCompany,
                chkAssociateDepartment, lblDepartment, cmbDepartment,
                grpCategories, grpCenterRegions
            });

            // Wire events
            btnSave.Click += (s, e) => SaveBranch();
            btnCancel.Click += (s, e) =>
            {
                var result = MessageBox.Show("Are you sure you want to cancel?", "Cancel",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    ClearForm();
                }
            };

            // Responsive layout
            bodyPanel.Resize += (s, e) =>
            {
                dtCreated.Left = bodyPanel.ClientSize.Width - dtCreated.Width - 12 - 20; // -20 for scrollbar
                txtCreatedBy.Left = bodyPanel.ClientSize.Width - txtCreatedBy.Width - 12 - 20;
                lblCreated.Left = dtCreated.Left;
                lblBy.Left = txtCreatedBy.Left;

                int maxLeftWidth = bodyPanel.ClientSize.Width - 24 - 200 - 20;
                if (maxLeftWidth < 220) maxLeftWidth = 220;
                txtBranchName.Width = Math.Min(600, maxLeftWidth);
                txtBranchDesc.Width = txtBranchName.Width;
                cmbCompany.Width = txtBranchName.Width;
                cmbDepartment.Width = txtBranchName.Width;
            };
        }

        private void LoadCompanies()
        {
            cmbCompany.Items.Clear();
            cmbCompany.Items.Add(new CompanyItem { Id = null, Name = "-- None --" });

            try
            {
                var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new System.Data.SqlClient.SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT ComId, Name FROM dbo.Company ORDER BY Name", con))
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

                using (var con = new System.Data.SqlClient.SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT DeptId, Name FROM dbo.Department ORDER BY Name", con))
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

        private void SaveBranch()
        {
            if (string.IsNullOrWhiteSpace(txtBranchName.Text))
            {
                MessageBox.Show("Please enter a Branch Name.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtBranchName.Focus();
                return;
            }

            var branch = new BranchDto
            {
                Name = txtBranchName.Text.Trim(),
                Description = txtBranchDesc.Text.Trim(),
                DateCreated = dtCreated.Value,
                CreatedByUserId = AppSession.CurrentUserId,
                CreatedByName = AppSession.CurrentUserName,
                CompanyId = null,
                DepartmentId = null,
                
                // Capture category checkboxes
                IsFactory = chkFactory.Checked,
                IsDepot = chkDepots.Checked,
                IsCenter = chkCenters.Checked,
                IsDistributor = chkDistributors.Checked,
                
                // Capture center region if Centers is checked
                CenterRegion = chkCenters.Checked && cmbCenterRegion.SelectedIndex > 0 
                    ? cmbCenterRegion.SelectedItem.ToString() 
                    : null
            };

            // Check if user wants to associate with company
            if (chkAssociateCompany.Checked && cmbCompany.SelectedItem is CompanyItem compItem && compItem.Id.HasValue)
            {
                branch.CompanyId = compItem.Id.Value;
            }

            // Check if user wants to associate with department
            if (chkAssociateDepartment.Checked && cmbDepartment.SelectedItem is DepartmentItem deptItem && deptItem.Id.HasValue)
            {
                branch.DepartmentId = deptItem.Id.Value;
            }

            Submitted?.Invoke(branch);
        }

        private void ClearForm()
        {
            txtBranchName.Clear();
            txtBranchDesc.Clear();
            dtCreated.Value = DateTime.Now;
            chkAssociateCompany.Checked = false;
            chkAssociateDepartment.Checked = false;
            cmbCompany.SelectedIndex = -1;
            cmbDepartment.SelectedIndex = -1;
            
            // Clear category checkboxes
            chkFactory.Checked = false;
            chkDepots.Checked = false;
            chkCenters.Checked = false;
            chkDistributors.Checked = false;
            
            // Clear center region
            cmbCenterRegion.SelectedIndex = 0;
        }

        // Helper classes for ComboBox items
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
}

