using Yakult.Inventory.App.Session;
using System;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;


namespace Yakult.Inventory.App.Pages.Employee
{
    public partial class AddEmployeePage : UserControl
    {
        private Panel headerPanel, bodyPanel, footerPanel;
        private TextBox txtName, txtDescription, txtCreatedBy;
        private DateTimePicker dtCreated;
        private Button btnSave, btnCancel;
        private Label lblInfo;
        private Label titleLabel;

        // Required: ComboBoxes for Company, Department, and Branch
        private ComboBox cmbCompany, cmbDepartment, cmbBranch;
        private Label lblCompany, lblDepartment, lblBranch;

        public event Action<EmployeeDto> Submitted;

        public AddEmployeePage()
        {
            InitializeComponent();
            BuildUi();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;

            AutoScroll = false;
            BackColor = Color.FromArgb(245, 246, 250);
            Font = new Font("Segoe UI", 9F);

            Controls.Clear();

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor,
                Padding = new Padding(24)
            };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(rootLayout);

            var scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BackColor,
                Padding = new Padding(0)
            };
            rootLayout.Controls.Add(scrollHost, 0, 0);

            var cardPanel = new ReaLTaiizor.Controls.Panel
            {
                BackColor = Color.White,
                EdgeColor = Color.FromArgb(220, 220, 220),
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(32, 24, 32, 32),
                SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality
            };
            scrollHost.Controls.Add(cardPanel);

            titleLabel = new Label
            {
                AutoSize = true,
                Text = "Add Employee",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 10)
            };
            cardPanel.Controls.Add(titleLabel);

            lblInfo = new Label
            {
                Text = "Fill in the employee details and required associations, then click Save.",
                AutoSize = false,
                Height = 40,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(60, 60, 60),
                Padding = new Padding(12, 8, 12, 8),
                BackColor = Color.FromArgb(230, 240, 255),
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 20)
            };
            cardPanel.Controls.Add(lblInfo);

            var formGrid = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
            formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            cardPanel.Controls.Add(formGrid);

            void StyleLabel(Control c)
            {
                if (c is Label l)
                {
                    l.AutoSize = false;
                    l.Height = 32;
                    l.TextAlign = ContentAlignment.MiddleLeft;
                    l.ForeColor = Color.FromArgb(60, 60, 60);
                    l.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
                    l.Margin = new Padding(0, 6, 12, 6);
                }
            }

            void StyleField(Control c)
            {
                c.Dock = DockStyle.Fill;
                c.Margin = new Padding(0, 6, 0, 6);
                c.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

                if (c is TextBox tb)
                {
                    tb.Font = new Font("Segoe UI", 9.5F);
                    tb.BorderStyle = BorderStyle.FixedSingle;
                    if (!tb.Multiline)
                    {
                        tb.Height = 28;
                    }
                    else
                    {
                        tb.MinimumSize = new Size(0, 72);
                    }
                }

                if (c is ComboBox cmb)
                {
                    cmb.Font = new Font("Segoe UI", 9.5F);
                    cmb.Height = 28;
                }

                if (c is DateTimePicker dtp)
                {
                    dtp.Font = new Font("Segoe UI", 9.5F);
                    dtp.Height = 28;
                }
            }

            void AddRow(Control label, Control field)
            {
                int row = formGrid.RowCount;
                formGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                formGrid.Controls.Add(label, 0, row);
                formGrid.Controls.Add(field, 1, row);
                formGrid.RowCount++;
                StyleLabel(label);
                StyleField(field);
            }

            var lblName = new Label { Text = "Name *" };
            txtName = new TextBox();

            var lblDescription = new Label { Text = "Description" };
            txtDescription = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 72 };

            var lblCreated = new Label { Text = "Date Created" };
            dtCreated = new DateTimePicker { Value = DateTime.Now, Format = DateTimePickerFormat.Short };

            var lblBy = new Label { Text = "Created By" };
            txtCreatedBy = new TextBox
            {
                ReadOnly = true,
                TabStop = false,
                BackColor = SystemColors.Control,
                Text = AppSession.CurrentUserName ?? string.Empty
            };

            lblCompany = new Label { Text = "Company *" };
            cmbCompany = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

            lblDepartment = new Label { Text = "Department *" };
            cmbDepartment = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

            lblBranch = new Label { Text = "Branch *" };
            cmbBranch = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

            AddRow(lblName, txtName);
            AddRow(lblDescription, txtDescription);
            AddRow(lblCreated, dtCreated);
            AddRow(lblBy, txtCreatedBy);
            AddRow(lblCompany, cmbCompany);
            AddRow(lblDepartment, cmbDepartment);
            AddRow(lblBranch, cmbBranch);

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

            // Load dropdown data
            LoadCompanies();
            LoadDepartments();
            LoadBranches();

            // Wire events
            btnSave.Click += (s, e) => SaveEmployee();
            btnCancel.Click += (s, e) =>
            {
                var result = MessageBox.Show("Are you sure you want to cancel?", "Cancel",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    ClearForm();
                }
            };
        }

        private void LoadCompanies()
        {
            cmbCompany.Items.Clear();
            cmbCompany.Items.Add(new CompanyItem { Id = null, Name = "-- Select Company --" });

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
            cmbDepartment.Items.Add(new DepartmentItem { Id = null, Name = "-- Select Department --" });

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

        private void LoadBranches()
        {
            cmbBranch.Items.Clear();
            cmbBranch.Items.Add(new BranchItem { Id = null, Name = "-- Select Branch --" });

            try
            {
                var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new System.Data.SqlClient.SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT BranchId, Name FROM dbo.Branch ORDER BY Name", con))
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

        private void SaveEmployee()
        {
            // Validation
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Please enter a Name.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
                return;
            }

            // Validate Company selection
            if (!(cmbCompany.SelectedItem is CompanyItem compItem) || !compItem.Id.HasValue)
            {
                MessageBox.Show("Please select a Company.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbCompany.Focus();
                return;
            }

            // Validate Department selection
            if (!(cmbDepartment.SelectedItem is DepartmentItem deptItem) || !deptItem.Id.HasValue)
            {
                MessageBox.Show("Please select a Department.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbDepartment.Focus();
                return;
            }

            // Validate Branch selection
            if (!(cmbBranch.SelectedItem is BranchItem branchItem) || !branchItem.Id.HasValue)
            {
                MessageBox.Show("Please select a Branch.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbBranch.Focus();
                return;
            }

            var employee = new EmployeeDto
            {
                Name = txtName.Text.Trim(),
                Description = txtDescription.Text.Trim(),
                DateCreated = dtCreated.Value,
                CreatedByUserId = AppSession.CurrentUserId,
                CreatedByName = AppSession.CurrentUserName,
                CompanyId = compItem.Id.Value,
                DepartmentId = deptItem.Id.Value,
                BranchId = branchItem.Id.Value
            };

            Submitted?.Invoke(employee);
        }

        private void ClearForm()
        {
            txtName.Clear();
            txtDescription.Clear();
            dtCreated.Value = DateTime.Now;
            cmbCompany.SelectedIndex = 0;
            cmbDepartment.SelectedIndex = 0;
            cmbBranch.SelectedIndex = 0;
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

        private class BranchItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }
    }
}

