using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Repositories;
using System;
using System.Windows.Forms;
using System.Drawing;
using System.Threading.Tasks;

namespace Yakult.Inventory.App.Pages.Set
{
    public partial class AddSetPage : Form
    {
        private Panel bodyPanel, footerPanel;
        private Label lblDispatchDate, lblStatus, lblRemarks, lblCreatedBy, lblTitle, lblDistributor;
        private DateTimePicker dtpDispatchDate;
        private ComboBox cboStatus, cboDistributor;
        private TextBox txtRemarks, txtCreatedBy;
        private CheckBox chkSetDispatchDate;
        private Button btnSave, btnCancel;
        private SetRepository _repository;
        private readonly bool _showStatus;

        public AddSetPage()
            : this(true)
        {
        }

        public AddSetPage(bool showStatus)
        {
            _showStatus = showStatus;
            _repository = new SetRepository();
            InitializeComponent();
            BuildUi();
            LoadCurrentUser();
        }

        private void BuildUi()
        {
            // Form properties
            Text = "Add New Set";
            Size = new Size(720, 520);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

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

            lblTitle = new Label
            {
                AutoSize = false,
                Height = 34,
                Padding = new Padding(0, 0, 0, 6),
                Text = "Add Set",
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
                Text = "Fill out the set details, then click Create Set.",
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
                AutoScroll = true,
                Padding = new Padding(0, 0, 10, 0)
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
            formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
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

                if (c is ComboBox cb)
                {
                    cb.Font = new Font("Segoe UI", 9.5F);
                    cb.Height = 28;
                    cb.IntegralHeight = false;
                }

                if (c is DateTimePicker dp)
                {
                    dp.Font = new Font("Segoe UI", 9.5F);
                    dp.Height = 28;
                }

                if (c is CheckBox chk)
                {
                    chk.AutoSize = true;
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

            lblCreatedBy = new Label { Text = "Created By" };
            txtCreatedBy = new TextBox
            {
                ReadOnly = true,
                BackColor = SystemColors.Control,
                BorderStyle = BorderStyle.FixedSingle
            };

            lblDispatchDate = new Label { Text = "Dispatch Date" };
            chkSetDispatchDate = new CheckBox
            {
                Text = "Set Dispatch Date",
                Checked = false,
                AutoSize = true
            };
            chkSetDispatchDate.CheckedChanged += ChkSetDispatchDate_CheckedChanged;

            dtpDispatchDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MM/dd/yyyy",
                Enabled = false
            };

            var dispatchRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(0, 0, 0, 2)
            };
            chkSetDispatchDate.Margin = new Padding(0, 4, 12, 0);
            dtpDispatchDate.Margin = new Padding(0);
            dispatchRow.Controls.Add(chkSetDispatchDate);
            dispatchRow.Controls.Add(dtpDispatchDate);

            lblStatus = new Label { Text = "Status" };
            cboStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboStatus.Items.AddRange(new object[] { "Pending", "Dispatched" });
            cboStatus.SelectedIndex = 0;

            lblRemarks = new Label { Text = "Remarks" };
            txtRemarks = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };

            // Independent sales distributor (dbo.Distributor) — optional, defaults to none.
            // A dept-level distributor request added to this Set propagates here automatically.
            lblDistributor = new Label { Text = "Distributor" };
            cboDistributor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            LoadDistributors();

            AddRow(lblCreatedBy, txtCreatedBy);
            AddRow(lblDispatchDate, dispatchRow);
            if (_showStatus)
            {
                AddRow(lblStatus, cboStatus);
            }
            AddRow(lblRemarks, txtRemarks);
            AddRow(lblDistributor, cboDistributor);

            bodyPanel.Controls.Add(formGrid);
            cardPanel.Controls.Add(bodyPanel);
            cardPanel.Controls.Add(lblInfo);
            cardPanel.Controls.Add(headerSpacer);
            cardPanel.Controls.Add(lblTitle);

            btnSave = new Button
            {
                Text = "Create Set",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 42),
                Padding = new Padding(18, 0, 18, 0),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                DialogResult = DialogResult.None
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 42),
                Padding = new Padding(18, 0, 18, 0),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(60, 60, 60),
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 210);
            btnCancel.FlatAppearance.BorderSize = 1;

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

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private void ChkSetDispatchDate_CheckedChanged(object sender, EventArgs e)
        {
            dtpDispatchDate.Enabled = chkSetDispatchDate.Checked;
        }

        private void LoadCurrentUser()
        {
            // Auto-fill the current logged-in user
            txtCreatedBy.Text = $"{AppSession.CurrentUserName} (ID: {AppSession.CurrentUserId})";
        }

        private sealed class DistributorOption
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private void LoadDistributors()
        {
            var options = new System.Collections.Generic.List<DistributorOption>
            {
                new DistributorOption { Id = 0, Name = "(None)" }
            };
            try
            {
                var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;
                if (!string.IsNullOrWhiteSpace(cs))
                {
                    using (var con = new System.Data.SqlClient.SqlConnection(cs))
                    {
                        con.Open();
                        using (var cmd = new System.Data.SqlClient.SqlCommand(
                            "IF OBJECT_ID('dbo.Distributor', 'U') IS NOT NULL SELECT DistributorId, Name FROM dbo.Distributor WHERE IsActive = 1 ORDER BY SortOrder, Name", con))
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                                options.Add(new DistributorOption { Id = r.GetInt32(0), Name = r.GetString(1) });
                        }
                    }
                }
            }
            catch
            {
                // Distributor catalog unavailable — keep "(None)" only.
            }
            // NOTE: Items.Add (not DataSource) — this runs mid-BuildUi before the
            // combo is parented, so there is no BindingContext yet for data binding
            // and SelectedIndex = 0 would throw ArgumentOutOfRangeException.
            cboDistributor.Items.Clear();
            foreach (var option in options)
                cboDistributor.Items.Add(option);
            if (cboDistributor.Items.Count > 0)
                cboDistributor.SelectedIndex = 0;
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                // Disable button to prevent double-clicks
                btnSave.Enabled = false;
                btnSave.Text = "Creating...";
                
                Cursor = Cursors.WaitCursor;

                // Get dispatch date if checkbox is checked
                DateTime? dispatchDate = chkSetDispatchDate.Checked ? (DateTime?)dtpDispatchDate.Value : null;
                
                // Get selected status
                string status = _showStatus
                    ? (cboStatus?.SelectedItem?.ToString() ?? "Pending")
                    : "Pending";

                // Optional dept-level distributor (independent of Company/Dept/Branch)
                int? distributorId = null;
                if (cboDistributor?.SelectedItem is DistributorOption selectedDistributor
                    && selectedDistributor.Id > 0)
                    distributorId = selectedDistributor.Id;

                // Create the set
                int newSetId = await _repository.CreateSetAsync(
                    AppSession.CurrentUserId, 
                    string.IsNullOrWhiteSpace(txtRemarks.Text) ? null : txtRemarks.Text.Trim(),
                    dispatchDate,
                    status,
                    distributorId
                );

                Cursor = Cursors.Default;

                if (newSetId > 0)
                {
                    // Store the new SetId in Tag so ViewSetPage can access it
                    Tag = newSetId;
                    
                    MessageBox.Show(
                        $"Set created successfully!\n\nSet ID: {newSetId}\n" +
                        (_showStatus ? $"Status: {status}\n" : string.Empty) +
                        (dispatchDate.HasValue ? $"Dispatch Date: {dispatchDate.Value:yyyy-MM-dd}\n" : "") +
                        "\nYou can now add requests to this set.",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    MessageBox.Show(
                        "Failed to create set. Please try again.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    
                    btnSave.Enabled = true;
                    btnSave.Text = "Create Set";
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                btnSave.Enabled = true;
                btnSave.Text = "Create Set";

                MessageBox.Show(
                    $"Failed to create set:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
