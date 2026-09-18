using System;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.Vendor
{
    public partial class AddVendorDialog : Form
    {
        private Label lblVendorName, lblAddress, lblTIN, lblActive, lblIsRefiller, lblIsDisposer, lblIsBuyer, lblDateCreated;
        private TextBox txtVendorName, txtAddress, txtTIN;
        private DateTimePicker dtCreated;
        private CheckBox chkActive, chkIsRefiller, chkIsDisposer, chkIsBuyer;
        private Button btnSave, btnCancel;
        private VendorRepository _repository;

        public AddVendorDialog()
        {
            _repository = new VendorRepository();
            InitializeComponent();
            BuildUi();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // Form properties
            Text = "Add New Vendor";
            Size = new Size(720, 760);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            ResumeLayout(false);
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

            var titleLabel = new Label
            {
                AutoSize = false,
                Height = 34,
                Padding = new Padding(0, 0, 0, 6),
                Text = "Add Vendor",
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
                Text = "Enter vendor details, then click Save.",
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

            var bodyPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                Padding = new Padding(0)
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
                    l.Dock = DockStyle.Fill;
                    l.Height = 32;
                    l.TextAlign = ContentAlignment.MiddleLeft;
                    l.ForeColor = Color.FromArgb(60, 60, 60);
                    l.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
                    l.Margin = new Padding(0, 6, 12, 6);
                }
            }

            void StyleField(Control c)
            {
                c.Margin = new Padding(0, 6, 0, 6);

                if (c is CheckBox cb)
                {
                    cb.AutoSize = true;
                    cb.Anchor = AnchorStyles.Left | AnchorStyles.Top;
                    return;
                }

                c.Dock = DockStyle.Fill;
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

            lblVendorName = new Label { Text = "Vendor Name *" };
            lblAddress = new Label { Text = "Address" };
            lblTIN = new Label { Text = "TIN" };
            lblDateCreated = new Label { Text = "Date Created" };
            lblActive = new Label { Text = "Active" };
            lblIsRefiller = new Label { Text = "Is Refiller" };
            lblIsDisposer = new Label { Text = "Is Disposer" };
            lblIsBuyer    = new Label { Text = "Is Buyer" };

            txtVendorName = new TextBox();
            txtAddress = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };
            txtTIN = new TextBox();
            dtCreated = new DateTimePicker { Value = DateTime.Now, Format = DateTimePickerFormat.Custom, CustomFormat = "MM/dd/yyyy" };
            chkActive     = new CheckBox { Checked = true,  AutoSize = true };
            chkIsRefiller = new CheckBox { Checked = false, AutoSize = true };
            chkIsDisposer = new CheckBox { Checked = false, AutoSize = true };
            chkIsBuyer    = new CheckBox { Checked = false, AutoSize = true };

            AddRow(lblVendorName, txtVendorName);
            AddRow(lblAddress, txtAddress);
            AddRow(lblTIN, txtTIN);
            AddRow(lblDateCreated, dtCreated);
            AddRow(lblActive, chkActive);
            AddRow(lblIsRefiller, chkIsRefiller);
            AddRow(lblIsDisposer, chkIsDisposer);
            AddRow(lblIsBuyer,    chkIsBuyer);

            bodyPanel.Controls.Add(formGrid);
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
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 42),
                Padding = new Padding(18, 0, 18, 0),
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
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

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                // Validation
                if (string.IsNullOrWhiteSpace(txtVendorName.Text))
                {
                    MessageBox.Show("Vendor Name is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtVendorName.Focus();
                    return;
                }

                // Disable button to prevent double-click
                btnSave.Enabled = false;
                btnSave.Text = "Saving...";
                Cursor = Cursors.WaitCursor;

                var vendor = new VendorDto
                {
                    VendorName = txtVendorName.Text.Trim(),
                    Address = string.IsNullOrWhiteSpace(txtAddress.Text) ? null : txtAddress.Text.Trim(),
                    TIN = string.IsNullOrWhiteSpace(txtTIN.Text) ? null : txtTIN.Text.Trim(),
                    IsActive   = chkActive.Checked,
                    IsRefiller = chkIsRefiller.Checked,
                    IsDisposer = chkIsDisposer.Checked,
                    IsBuyer    = chkIsBuyer.Checked,
                    CreatedDate = dtCreated.Value
                };

                int newVendorId = await _repository.AddVendorAsync(vendor);

                Cursor = Cursors.Default;

                if (newVendorId > 0)
                {
                    MessageBox.Show(
                        $"Vendor '{vendor.VendorName}' added successfully!\n\nVendor ID: {newVendorId}",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    MessageBox.Show("Failed to add vendor. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnSave.Enabled = true;
                    btnSave.Text = "Save";
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                btnSave.Enabled = true;
                btnSave.Text = "Save";

                MessageBox.Show(
                    $"Failed to add vendor:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
