using System;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    /// <summary>
    /// "Add Cartridge Model" — the small on-the-spot dialog behind the "+ Add" button beside a
    /// Cartridge Model picker, so a model that does not exist yet can be created without leaving
    /// the page.
    ///
    /// NOTE: BatchAddItemDialog and EditItemDialog each still build this form inline with their
    /// own private copy of the same code. This class is the reusable version; those two should be
    /// converted to it, but that is deliberately left out of this change so working dialogs are
    /// not disturbed.
    /// </summary>
    public sealed class QuickAddCartridgeModelDialog : Form
    {
        private readonly TextBox _modelNumber;
        private readonly CheckBox _isRequestable;
        private readonly CheckBox _isRefillable;
        private readonly Button _save;

        /// <summary>Id of the model created, once the dialog closes with OK.</summary>
        public int? NewCartridgeModelId { get; private set; }

        /// <summary>Model number of the model created, once the dialog closes with OK.</summary>
        public string NewModelNumber { get; private set; }

        /// <param name="initialModelNumber">
        /// Pre-fills the Model Number box — callers pass whatever the user already typed so the
        /// value does not have to be entered twice.
        /// </param>
        public QuickAddCartridgeModelDialog(string initialModelNumber = null)
        {
            Text = "Add Cartridge Model";

            _save = new Button { Text = "Save", DialogResult = DialogResult.None };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            _save.Click += Save_Click;

            // Auto-sizing shell — the form measures itself from these rows, so nothing clips
            // when the display is scaled above 100%.
            var fields = Shared.QuickAddDialogLayout.BuildShell(this, _save, cancel);

            _modelNumber = new TextBox();
            if (!string.IsNullOrWhiteSpace(initialModelNumber))
                _modelNumber.Text = initialModelNumber.Trim();
            Shared.QuickAddDialogLayout.AddRow(fields, "Model Number *", _modelNumber);

            _isRequestable = new CheckBox { Text = "Is Requestable", Checked = true };
            _isRefillable = new CheckBox { Text = "Is Refillable", Checked = true };
            Shared.QuickAddDialogLayout.AddFullWidth(fields,
                Shared.QuickAddDialogLayout.CheckBoxRow(_isRequestable, _isRefillable), topMargin: 8);
        }

        private async void Save_Click(object sender, EventArgs e)
        {
            string modelNumber = (_modelNumber.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(modelNumber))
            {
                MessageBox.Show(this, "Please enter a model number.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _save.Enabled = false;
                _save.Text = "Saving...";

                var repo = new CartridgeModelRepository();

                // ModelNumber is uniquely indexed, so check first and report it plainly rather
                // than surfacing a constraint violation.
                var existing = await repo.FindByModelNumberAsync(modelNumber);
                if (existing != null)
                {
                    MessageBox.Show(this, $"Model '{modelNumber}' already exists.", "Duplicate",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _save.Enabled = true;
                    _save.Text = "Save";
                    return;
                }

                var model = new CartridgeModelDto
                {
                    ModelNumber = modelNumber,
                    IsRequestable = _isRequestable.Checked,
                    IsRefillable = _isRefillable.Checked,
                    IsActive = true,
                    CreatedBy = AppSession.CurrentUserId,
                    CreatedAt = DateTime.Now
                };

                NewCartridgeModelId = await repo.CreateAsync(model);
                NewModelNumber = model.ModelNumber;

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                Core.Logger.LogError("QuickAddCartridgeModelDialog: save failed", ex);
                MessageBox.Show(this, "Error: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _save.Enabled = true;
                _save.Text = "Save";
            }
        }
    }
}
