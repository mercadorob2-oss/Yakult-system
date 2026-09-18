using System;
using System.Drawing;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Pages
{
    public class CreateBatchDialog : Form
    {
        private NumericUpDown nudRequiredQty;
        private TextBox txtRemarks;

        public int RequiredQty => (int)nudRequiredQty.Value;
        public string Remarks => txtRemarks.Text.Trim();

        public CreateBatchDialog(string vendorName, string cartridgeModel, int availableQty)
        {
            InitializeDialog(vendorName, cartridgeModel, availableQty);
        }

        private void InitializeDialog(string vendorName, string cartridgeModel, int availableQty)
        {
            Text = "Create Vendor Batch";
            Size = new Size(520, 500);
            MinimumSize = new Size(450, 400);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;

            // Content panel with scrolling
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(20, 20, 20, 10)
            };

            // Button panel (docked to bottom, always visible)
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                Padding = new Padding(20, 10, 20, 10)
            };

            int yPos = 10;

            // Title
            var lblTitle = new Label
            {
                Text = "Create Vendor Batch from Empties",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Location = new Point(0, yPos),
                AutoSize = true
            };
            contentPanel.Controls.Add(lblTitle);
            yPos += 35;

            // Info section
            var lblInfo = new Label
            {
                Text = "Batch Information",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(0, yPos),
                AutoSize = true
            };
            contentPanel.Controls.Add(lblInfo);
            yPos += 30;

            var lblVendor = new Label
            {
                Text = $"Vendor: {vendorName}",
                Location = new Point(0, yPos),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };
            contentPanel.Controls.Add(lblVendor);
            yPos += 25;

            var lblModel = new Label
            {
                Text = $"Model: {cartridgeModel}",
                Location = new Point(0, yPos),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };
            contentPanel.Controls.Add(lblModel);
            yPos += 25;

            var lblAvailable = new Label
            {
                Text = $"Available Empty Qty: {availableQty}",
                Location = new Point(0, yPos),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(230, 126, 34)
            };
            contentPanel.Controls.Add(lblAvailable);
            yPos += 40;

            // Required Quantity
            var lblRequiredQty = new Label
            {
                Text = "Quantity to Send to Vendor:",
                Location = new Point(0, yPos),
                AutoSize = true
            };
            contentPanel.Controls.Add(lblRequiredQty);
            yPos += 25;

            nudRequiredQty = new NumericUpDown
            {
                Location = new Point(0, yPos),
                Width = 200,
                Minimum = 1,
                Maximum = availableQty,
                Value = Math.Min(10, availableQty)
            };
            contentPanel.Controls.Add(nudRequiredQty);
            yPos += 40;

            // Remarks
            var lblRemarks = new Label
            {
                Text = "Remarks (optional):",
                Location = new Point(0, yPos),
                AutoSize = true
            };
            contentPanel.Controls.Add(lblRemarks);
            yPos += 25;

            txtRemarks = new TextBox
            {
                Location = new Point(0, yPos),
                Width = 440,
                Height = 60,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            contentPanel.Controls.Add(txtRemarks);
            yPos += 75;

            // Buttons (in button panel, always visible at bottom)
            var btnCreate = new Button
            {
                Text = "Create Batch",
                Location = new Point(0, 5),
                Width = 150,
                Height = 35,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                DialogResult = DialogResult.OK
            };
            btnCreate.FlatAppearance.BorderSize = 0;
            buttonPanel.Controls.Add(btnCreate);

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(160, 5),
                Width = 100,
                Height = 35,
                BackColor = Color.FromArgb(149, 165, 166),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            buttonPanel.Controls.Add(btnCancel);

            // Add panels to form (button panel first so it appears on top in Z-order)
            Controls.Add(contentPanel);
            Controls.Add(buttonPanel);

            AcceptButton = btnCreate;
            CancelButton = btnCancel;
        }
    }
}
