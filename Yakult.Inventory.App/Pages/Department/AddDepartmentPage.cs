using Yakult.Inventory.App.Session;
using System;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;


namespace Yakult.Inventory.App.Pages.Department
{
    public partial class AddDepartmentPage : UserControl
    {
        private Panel headerPanel, bodyPanel, footerPanel;
        private TextBox txtDeptName, txtDeptDesc, txtCreatedBy;
        private DateTimePicker dtCreated;
        private Button btnSave, btnCancel;
        private Label titleLabel;

        // Optional: ComboBox to associate with existing companies
        private ComboBox cmbCompany;
        private Label lblCompany;
        private CheckBox chkAssociateCompany;

        public event Action<DepartmentDto> Submitted;

        public AddDepartmentPage()
        {
            InitializeComponent();
            BuildUi();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            AutoScroll = false;

            // Create panels
            bodyPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            footerPanel = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(12, 8, 12, 8) };
            headerPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(12, 8, 12, 0) };

            Controls.Add(bodyPanel);
            Controls.Add(footerPanel);
            Controls.Add(headerPanel);

            // Header
            titleLabel = new Label
            {
                AutoSize = true,
                Text = "Add Department",
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

            // Body - Department fields
            var lblName = new Label { AutoSize = true, Text = "Department Name *", Left = 0, Top = 8 };
            txtDeptName = new TextBox { Left = 0, Top = 28, Width = 420 };

            var lblDesc = new Label { AutoSize = true, Text = "Description", Left = 0, Top = 60 };
            txtDeptDesc = new TextBox { Left = 0, Top = 80, Width = 420, Multiline = true, Height = 60 };

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

            bodyPanel.Controls.AddRange(new Control[] {
                lblName, txtDeptName, lblDesc, txtDeptDesc, 
                lblCreated, dtCreated, lblBy, txtCreatedBy,
                chkAssociateCompany, lblCompany, cmbCompany
            });

            // Wire events
            btnSave.Click += (s, e) => SaveDepartment();
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
                dtCreated.Left = bodyPanel.ClientSize.Width - dtCreated.Width - 12;
                txtCreatedBy.Left = bodyPanel.ClientSize.Width - txtCreatedBy.Width - 12;
                lblCreated.Left = dtCreated.Left;
                lblBy.Left = txtCreatedBy.Left;

                int maxLeftWidth = bodyPanel.ClientSize.Width - 24 - 200;
                if (maxLeftWidth < 220) maxLeftWidth = 220;
                txtDeptName.Width = Math.Min(600, maxLeftWidth);
                txtDeptDesc.Width = txtDeptName.Width;
                cmbCompany.Width = txtDeptName.Width;
            };
        }

        private void LoadCompanies()
        {
            // Load companies from database
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

        private void SaveDepartment()
        {
            if (string.IsNullOrWhiteSpace(txtDeptName.Text))
            {
                MessageBox.Show("Please enter a Department Name.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtDeptName.Focus();
                return;
            }

            var dept = new DepartmentDto
            {
                Name = txtDeptName.Text.Trim(),
                Description = txtDeptDesc.Text.Trim(),
                DateCreated = dtCreated.Value,
                CreatedByUserId = AppSession.CurrentUserId,
                CreatedByName = AppSession.CurrentUserName,
                CompanyId = null // Independent by default
            };

            // Check if user wants to associate with company
            if (chkAssociateCompany.Checked && cmbCompany.SelectedItem is CompanyItem item && item.Id.HasValue)
            {
                dept.CompanyId = item.Id.Value;
            }

            Submitted?.Invoke(dept);
        }

        private void ClearForm()
        {
            txtDeptName.Clear();
            txtDeptDesc.Clear();
            dtCreated.Value = DateTime.Now;
            chkAssociateCompany.Checked = false;
            cmbCompany.SelectedIndex = -1;
        }

        // Helper class for ComboBox items
        private class CompanyItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }
    }
}

