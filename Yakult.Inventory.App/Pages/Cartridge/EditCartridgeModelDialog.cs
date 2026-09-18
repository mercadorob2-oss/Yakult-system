using System;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    public class EditCartridgeModelDialog : Form
    {
        private readonly int _cartridgeModelId;
        private CartridgeModelDto _model;

        private TextBox txtModelNumber;
        private CheckBox chkRequestable;
        private CheckBox chkRefillable;
        private CheckBox chkActive;
        private Button btnSave;
        private Button btnCancel;

        public EditCartridgeModelDialog(int cartridgeModelId)
        {
            _cartridgeModelId = cartridgeModelId;
            InitializeDialog();
            LoadModelDataAsync();
        }

        private void InitializeDialog()
        {
            Text = "Edit Cartridge Model";
            Size = new Size(500, 380);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(20)
            };
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Model Number
            var lblModelNumber = new Label { Text = "Model Number *", Dock = DockStyle.Fill };
            txtModelNumber = new TextBox { Dock = DockStyle.Fill };
            mainPanel.Controls.Add(lblModelNumber, 0, 0);
            mainPanel.Controls.Add(txtModelNumber, 1, 0);

            // Is Requestable
            chkRequestable = new CheckBox { Text = "Is Requestable", Dock = DockStyle.Fill };
            mainPanel.Controls.Add(new Label(), 0, 1);
            mainPanel.Controls.Add(chkRequestable, 1, 1);

            // Is Refillable
            chkRefillable = new CheckBox { Text = "Is Refillable", Dock = DockStyle.Fill };
            mainPanel.Controls.Add(new Label(), 0, 2);
            mainPanel.Controls.Add(chkRefillable, 1, 2);

            // Is Active
            chkActive = new CheckBox { Text = "Is Active", Dock = DockStyle.Fill };
            mainPanel.Controls.Add(new Label(), 0, 3);
            mainPanel.Controls.Add(chkActive, 1, 3);

            // Buttons
            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true
            };

            btnSave = new Button
            {
                Text = "Save",
                Width = 80,
                DialogResult = DialogResult.None
            };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Width = 80,
                DialogResult = DialogResult.Cancel
            };

            buttonPanel.Controls.Add(btnSave);
            buttonPanel.Controls.Add(btnCancel);
            mainPanel.Controls.Add(buttonPanel, 0, 4);
            mainPanel.SetColumnSpan(buttonPanel, 2);

            Controls.Add(mainPanel);

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private async void LoadModelDataAsync()
        {
            try
            {
                var repository = new CartridgeModelRepository();
                _model = await repository.GetByIdAsync(_cartridgeModelId);

                if (_model == null)
                {
                    MessageBox.Show("Cartridge model not found.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.Cancel;
                    Close();
                    return;
                }

                // Populate form fields
                txtModelNumber.Text = _model.ModelNumber;
                chkRequestable.Checked = _model.IsRequestable;
                chkRefillable.Checked = _model.IsRefillable;
                chkActive.Checked = _model.IsActive;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading cartridge model: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtModelNumber.Text))
            {
                MessageBox.Show("Please enter a model number.", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                btnSave.Enabled = false;
                btnSave.Text = "Saving...";

                var repository = new CartridgeModelRepository();

                // Check for duplicate model number (excluding current model)
                var existing = await repository.FindByModelNumberAsync(txtModelNumber.Text.Trim());
                if (existing != null && existing.CartridgeModelId != _cartridgeModelId)
                {
                    MessageBox.Show($"Model number '{txtModelNumber.Text.Trim()}' already exists.",
                        "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    btnSave.Enabled = true;
                    btnSave.Text = "Save";
                    return;
                }

                // Update the model
                _model.ModelNumber = txtModelNumber.Text.Trim();
                _model.IsRequestable = chkRequestable.Checked;
                _model.IsRefillable = chkRefillable.Checked;
                _model.IsActive = chkActive.Checked;

                await repository.UpdateAsync(_model);

                MessageBox.Show("Cartridge model updated successfully!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSave.Enabled = true;
                btnSave.Text = "Save";
            }
        }

    }
}
