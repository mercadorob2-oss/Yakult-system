using System;
using System.Collections.Generic;
using System.Drawing;
using System.Data.SqlClient;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    /// <summary>
    /// Modal dialog for editing the refill vendor assignment on a batch.
    /// Vendor editing is only available for Active batches.
    /// </summary>
    public partial class EditBatchDialog : Form
    {
        private readonly VendorCartridgeBatchDto _batch;

        // Controls
        private Label lblBatchId, lblCartridgeModel, lblOriginalQty, lblStatus;
        private TextBox txtBatchId, txtCartridgeModel, txtOriginalQty, txtStatus;
        private Label lblVendor;
        private ComboBox cmbVendor;
        private Label lblHelperText;
        private Button btnSave, btnCancel;

        /// <summary>New vendor ID (only meaningful when VendorChanged is true)</summary>
        public int SelectedVendorId { get; private set; }

        /// <summary>True if the user changed the vendor</summary>
        public bool VendorChanged { get; private set; }

        /// <summary>True if the user clicked Save</summary>
        public bool SaveClicked { get; private set; }

        /// <summary>True if cartridges were transferred and the parent page should reload</summary>
        public bool NeedsRefresh { get; private set; }

        public EditBatchDialog(VendorCartridgeBatchDto batch)
        {
            _batch = batch ?? throw new ArgumentNullException(nameof(batch));
            SelectedVendorId = _batch.VendorId;
            InitializeCustomControls();
            LoadBatchData();
            LoadVendors();
        }

        private void InitializeCustomControls()
        {
            Text = "Edit Batch";
            Size = new Size(520, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(245, 246, 250);
            Font = new Font("Segoe UI", 9F);

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

            var cardPanel = new Panel
            {
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Padding = new Padding(32, 24, 32, 32),
                AutoScroll = true
            };
            rootLayout.Controls.Add(cardPanel, 0, 0);

            var formLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Padding = new Padding(0, 20, 0, 0)
            };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            cardPanel.Controls.Add(formLayout);

            var titleLabel = new Label
            {
                AutoSize = false,
                Height = 34,
                Padding = new Padding(0, 0, 0, 6),
                Text = "Edit Batch",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Dock = DockStyle.Top
            };
            cardPanel.Controls.Add(titleLabel);

            var divider = new Panel
            {
                BackColor = Color.FromArgb(235, 235, 235),
                Height = 1,
                Dock = DockStyle.Top
            };
            cardPanel.Controls.Add(divider);

            int rowIndex = 0;

            // Batch ID (read-only)
            AddFormField(formLayout, ref rowIndex, "Batch ID:", out lblBatchId, out txtBatchId, true);

            // Status (read-only)
            AddFormField(formLayout, ref rowIndex, "Status:", out lblStatus, out txtStatus, true);

            // Cartridge Model (read-only)
            AddFormField(formLayout, ref rowIndex, "Cartridge Model:", out lblCartridgeModel, out txtCartridgeModel, true);

            // Original Qty (read-only)
            AddFormField(formLayout, ref rowIndex, "Original Qty:", out lblOriginalQty, out txtOriginalQty, true);

            // --- Vendor (editable for Active batches only) ---
            bool vendorEditable = _batch.Status == "Active";

            formLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            lblVendor = new Label
            {
                Text = "Refill Vendor:" + (vendorEditable ? "" : "  (locked)"),
                AutoSize = false,
                Height = 20,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = vendorEditable ? Color.FromArgb(52, 73, 94) : Color.FromArgb(149, 165, 166),
                Padding = new Padding(0, 16, 0, 4)
            };
            formLayout.Controls.Add(lblVendor, 0, rowIndex++);

            cmbVendor = new ComboBox
            {
                Dock = DockStyle.Top,
                Height = 36,
                Font = new Font("Segoe UI", 10F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = vendorEditable
            };
            formLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            formLayout.Controls.Add(cmbVendor, 0, rowIndex++);

            // Helper text
            formLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            lblHelperText = new Label
            {
                Text = vendorEditable
                    ? "Select a different vendor to reassign this refill batch."
                    : "Vendor can only be changed for Active batches.",
                AutoSize = false,
                Height = 40,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(149, 165, 166),
                Padding = new Padding(0, 8, 0, 0)
            };
            formLayout.Controls.Add(lblHelperText, 0, rowIndex++);

            // Button panel
            var buttonPanel = new Panel
            {
                Height = 50,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(250, 250, 250),
                Padding = new Padding(24, 10, 24, 10)
            };
            rootLayout.Controls.Add(buttonPanel, 0, 1);

            // Transfer Cartridges button — only for Active batches
            if (_batch.Status == "Active")
            {
                var btnTransferCartridges = new Button
                {
                    Text      = "⇄ Transfer Cartridges",
                    Width     = 170,
                    Height    = 36,
                    BackColor = Color.FromArgb(0, 150, 136),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                    Cursor    = Cursors.Hand,
                    Location  = new Point(8, 7)
                };
                btnTransferCartridges.FlatAppearance.BorderSize = 0;
                btnTransferCartridges.Click += (s, e) =>
                {
                    using (var dlg = new TransferCartridgesDialog(_batch))
                    {
                        dlg.ShowDialog(this);
                        if (dlg.AnyTransferred)
                            NeedsRefresh = true;
                    }
                };
                buttonPanel.Controls.Add(btnTransferCartridges);
            }

            btnCancel = new Button
            {
                Text = "Cancel",
                Width = 100,
                Height = 36,
                BackColor = Color.FromArgb(189, 195, 199),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Location = new Point(buttonPanel.Width - 234, 7);
            btnCancel.Click += (s, e) => { SaveClicked = false; DialogResult = DialogResult.Cancel; Close(); };
            buttonPanel.Controls.Add(btnCancel);

            btnSave = new Button
            {
                Text = "Save Changes",
                Width = 120,
                Height = 36,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Location = new Point(buttonPanel.Width - 124, 7);
            btnSave.Click += BtnSave_Click;
            buttonPanel.Controls.Add(btnSave);
        }

        private void AddFormField(TableLayoutPanel layout, ref int rowIndex, string labelText, out Label label, out TextBox textBox, bool readOnly)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            label = new Label
            {
                Text = labelText,
                AutoSize = false,
                Height = 20,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Padding = new Padding(0, 16, 0, 4)
            };
            layout.Controls.Add(label, 0, rowIndex++);

            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            textBox = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 36,
                Font = new Font("Segoe UI", 10F),
                ReadOnly = readOnly,
                BackColor = readOnly ? Color.FromArgb(245, 247, 250) : Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            layout.Controls.Add(textBox, 0, rowIndex++);
        }

        private void LoadBatchData()
        {
            txtBatchId.Text = _batch.BatchId.ToString();
            txtStatus.Text = _batch.Status;
            txtCartridgeModel.Text = _batch.CartridgeModel;
            txtOriginalQty.Text = _batch.OriginalQty.ToString();
        }

        private void LoadVendors()
        {
            try
            {
                cmbVendor.Items.Clear();

                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();

                    const string query = @"
                        SELECT v.VendorID, v.VendorName
                        FROM dbo.Vendor v
                        LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Vendor' AND arc.EntityId = v.VendorID AND arc.IsArchived = 1
                        WHERE v.IsActive = 1 AND v.IsRefiller = 1 AND arc.ArchiveId IS NULL
                        ORDER BY v.VendorName";

                    using (var cmd = new SqlCommand(query, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbVendor.Items.Add(new VendorItem
                            {
                                VendorId = reader.GetInt32(0),
                                VendorName = reader.GetString(1)
                            });
                        }
                    }
                }

                if (cmbVendor.Items.Count > 0)
                {
                    int indexToSelect = 0;
                    for (int i = 0; i < cmbVendor.Items.Count; i++)
                    {
                        if (cmbVendor.Items[i] is VendorItem v && v.VendorId == _batch.VendorId)
                        {
                            indexToSelect = i;
                            break;
                        }
                    }
                    cmbVendor.SelectedIndex = indexToSelect;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load vendors: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            bool vendorEditable = _batch.Status == "Active";

            // Validate vendor
            if (vendorEditable && cmbVendor.SelectedItem == null)
            {
                MessageBox.Show("Please select a vendor.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Detect changes
            VendorChanged = false;

            if (vendorEditable && cmbVendor.SelectedItem is VendorItem selected)
            {
                if (selected.VendorId != _batch.VendorId)
                {
                    VendorChanged = true;
                    SelectedVendorId = selected.VendorId;
                }
            }

            if (!VendorChanged)
            {
                MessageBox.Show(
                    "No changes detected.",
                    "No Changes",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            SaveClicked = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private class VendorItem
        {
            public int VendorId { get; set; }
            public string VendorName { get; set; }
            public override string ToString() => VendorName;
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(520, 560);
            Name = "EditBatchDialog";
            ResumeLayout(false);
        }
    }
}
