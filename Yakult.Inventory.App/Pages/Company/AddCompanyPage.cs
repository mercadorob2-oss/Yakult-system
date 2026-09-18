using Yakult.Inventory.App.Session;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Controls;

namespace Yakult.Inventory.App.Pages.Company
{
    public partial class AddCompanyPage : UserControl
    {
        // Top-level layout
        private Panel headerPanel, bodyPanel, footerPanel;

        // Main form panel (for company fields)
        private Panel companyFormPanel;

        // Optional sections for departments and branches
        private Panel departmentsSection, branchesSection;
        private FlowLayoutPanel flowDepartments, flowBranches;
        private Button btnAddDept, btnAddBranch;
        private CheckBox chkAddDepartments, chkAddBranches;

        // Buttons
        private Button btnSave, btnCancel;

        // Company fields
        private TextBox txtCompanyName, txtCompanyDesc, txtCreatedBy;
        private DateTimePicker dtCreated;

        private Label titleLabel;

        // Let MainForm subscribe and do the DB save
        public event Action<CompanyDto> Submitted;
        public event Action CancelRequested; // NEW: Event for cancel

        public AddCompanyPage()
        {
            InitializeComponent();
            BuildUi();
        }

        private void BuildUi()
        {
            // Root settings
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
                AutoSize = false,
                Height = 34,
                Padding = new Padding(0, 0, 0, 6),
                Text = "Add Company",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                Dock = DockStyle.Top
            };

            var headerSpacer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 8
            };

            var lblInfo = new Label
            {
                Text = "Enter company details. You may optionally add departments and branches, then click Save.",
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
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0)
            };

            cardPanel.Controls.Add(bodyPanel);
            cardPanel.Controls.Add(lblInfo);
            cardPanel.Controls.Add(headerSpacer);
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

            // ===== Build Company Form (stays at top, always visible)
            BuildCompanyForm();

            // ===== Build Optional Sections (stack below company form)
            BuildDepartmentsSection();
            BuildBranchesSection();

            // Wire events
            btnSave.Click += (s, e) => SaveAll();
            btnCancel.Click += (s, e) => CancelRequested?.Invoke(); // Fire cancel event
        }

        // ===== Company Form (always visible at the top)
        private void BuildCompanyForm()
        {
            companyFormPanel = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 12)
            };

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

            var lblName = new Label { Text = "Company Name *" };
            txtCompanyName = new TextBox();

            var lblDesc = new Label { Text = "Description" };
            txtCompanyDesc = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 72 };

            var lblCreated = new Label { Text = "Date Created" };
            dtCreated = new DateTimePicker { Value = DateTime.Now, Format = DateTimePickerFormat.Short };

            var lblBy = new Label { Text = "Created By (Name)" };
            txtCreatedBy = new TextBox
            {
                ReadOnly = true,
                TabStop = false,
                BackColor = SystemColors.Control,
                Text = AppSession.CurrentUserName ?? string.Empty
            };

            chkAddDepartments = new CheckBox
            {
                AutoSize = true,
                Text = "Add Departments (optional)",
                Checked = false
            };

            chkAddBranches = new CheckBox
            {
                AutoSize = true,
                Text = "Add Branches (optional)",
                Checked = false
            };

            chkAddDepartments.CheckedChanged += (s, e) =>
            {
                departmentsSection.Visible = chkAddDepartments.Checked;
                if (chkAddDepartments.Checked && flowDepartments.Controls.Count == 0)
                {
                    AddDepartmentRow();
                }
            };

            chkAddBranches.CheckedChanged += (s, e) =>
            {
                branchesSection.Visible = chkAddBranches.Checked;
                if (chkAddBranches.Checked && flowBranches.Controls.Count == 0)
                {
                    AddBranchRow();
                }
            };

            AddRow(lblName, txtCompanyName);
            AddRow(lblDesc, txtCompanyDesc);
            AddRow(lblCreated, dtCreated);
            AddRow(lblBy, txtCreatedBy);

            var optionalRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0)
            };
            chkAddDepartments.Margin = new Padding(0, 6, 0, 0);
            chkAddBranches.Margin = new Padding(0, 6, 0, 0);
            optionalRow.Controls.Add(chkAddDepartments);
            optionalRow.Controls.Add(chkAddBranches);
            AddRow(new Label { Text = "" }, optionalRow);

            companyFormPanel.Controls.Add(formGrid);
            bodyPanel.Controls.Add(companyFormPanel);
        }

        // ===== Optional Departments Section
        private void BuildDepartmentsSection()
        {
            departmentsSection = new Panel
            {
                Dock = DockStyle.Top,
                Height = 300,
                Padding = new Padding(0, 4, 20, 12), // Extra right padding for scrollbar
                Visible = false
            };

            // Header bar for departments
            var deptHeader = new Panel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(0, 0, 20, 0) };
            var lblDept = new Label 
            { 
                AutoSize = true, 
                Text = "Departments", 
                Left = 0, 
                Top = 10, 
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold) 
            };
            btnAddDept = new Button { Text = "+ Add", Width = 80, Height = 26, Top = 5, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            deptHeader.Resize += (s, e) => btnAddDept.Left = deptHeader.ClientSize.Width - btnAddDept.Width - 20;
            btnAddDept.Click += (s, e) => AddDepartmentRow();
            deptHeader.Controls.AddRange(new Control[] { lblDept, btnAddDept });

            // FlowLayoutPanel for department rows
            flowDepartments = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(0, 6, 0, 6)
            };
            flowDepartments.Resize += (s, e) =>
            {
                foreach (var d in flowDepartments.Controls.OfType<DepartmentEntry>())
                    d.Width = flowDepartments.ClientSize.Width - 24;
            };

            departmentsSection.Controls.Add(flowDepartments);
            departmentsSection.Controls.Add(deptHeader);

            bodyPanel.Controls.Add(departmentsSection);
        }

        // ===== Optional Branches Section
        private void BuildBranchesSection()
        {
            branchesSection = new Panel
            {
                Dock = DockStyle.Top,
                Height = 300,
                Padding = new Padding(0, 4, 20, 12), // Extra right padding for scrollbar
                Visible = false
            };

            // Header bar for branches
            var branchHeader = new Panel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(0, 0, 20, 0) };
            var lblBranch = new Label 
            { 
                AutoSize = true, 
                Text = "Branches", 
                Left = 0, 
                Top = 10, 
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold) 
            };
            btnAddBranch = new Button { Text = "+ Add", Width = 80, Height = 26, Top = 5, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            branchHeader.Resize += (s, e) => btnAddBranch.Left = branchHeader.ClientSize.Width - btnAddBranch.Width - 20;
            btnAddBranch.Click += (s, e) => AddBranchRow();
            branchHeader.Controls.AddRange(new Control[] { lblBranch, btnAddBranch });

            // FlowLayoutPanel for branch rows
            flowBranches = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(0, 6, 0, 6)
            };
            flowBranches.Resize += (s, e) =>
            {
                foreach (var b in flowBranches.Controls.OfType<BranchEntry>())
                    b.Width = flowBranches.ClientSize.Width - 24;
            };

            branchesSection.Controls.Add(flowBranches);
            branchesSection.Controls.Add(branchHeader);

            bodyPanel.Controls.Add(branchesSection);
        }

        // ===== Row adders
        private void AddDepartmentRow()
        {
            var row = new DepartmentEntry
            {
                Width = flowDepartments.ClientSize.Width - 24,
                Margin = new Padding(0, 4, 0, 4)
            };
            if (row.Height < 84) row.Height = 84;
            flowDepartments.Controls.Add(row);
            row.PerformLayout();
        }

        private void AddBranchRow()
        {
            var row = new BranchEntry
            {
                Width = flowBranches.ClientSize.Width - 24,
                Margin = new Padding(0, 4, 0, 4)
            };
            if (row.Height < 84) row.Height = 84;
            flowBranches.Controls.Add(row);
            row.PerformLayout();
        }

        // ===== Save
        private void SaveAll()
        {
            // Validate company name
            if (string.IsNullOrWhiteSpace(txtCompanyName.Text))
            {
                MessageBox.Show("Please enter a Company Name.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtCompanyName.Focus();
                return;
            }

            var companyDate = dtCreated.Value;
            var currentUserId = AppSession.CurrentUserId;
            var currentUserName = AppSession.CurrentUserName;

            var company = new CompanyDto
            {
                Name = txtCompanyName.Text.Trim(),
                Description = txtCompanyDesc.Text.Trim(),
                DateCreated = companyDate,
                CreatedByUserId = currentUserId,
                CreatedByName = currentUserName,
                Departments = new List<DepartmentDto>(),
                Branches = new List<BranchDto>()
            };

            // Only collect departments if the checkbox is checked
            if (chkAddDepartments.Checked)
            {
                company.Departments = flowDepartments.Controls.OfType<DepartmentEntry>()
                    .Select(d => new DepartmentDto
                    {
                        Name = d.DeptName,
                        Description = d.DeptDescription,
                        DateCreated = companyDate,
                        CreatedByUserId = currentUserId,
                        CreatedByName = currentUserName
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                    .ToList();
            }

            // Only collect branches if the checkbox is checked
            if (chkAddBranches.Checked)
            {
                company.Branches = flowBranches.Controls.OfType<BranchEntry>()
                    .Select(b => new BranchDto
                    {
                        Name = b.BranchName,
                        Description = b.BranchDescription,
                        DateCreated = companyDate,
                        CreatedByUserId = currentUserId,
                        CreatedByName = currentUserName
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                    .ToList();
            }

            Submitted?.Invoke(company);
        }

        private void ClearForm()
        {
            txtCompanyName.Clear();
            txtCompanyDesc.Clear();
            dtCreated.Value = DateTime.Now;
            chkAddDepartments.Checked = false;
            chkAddBranches.Checked = false;
            flowDepartments.Controls.Clear();
            flowBranches.Controls.Clear();
        }
    }
}
