using System;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Consumable
{
    /// <summary>
    /// "Add Ink / Toner / Print Head Model" — the on-the-spot dialog behind the "+ Add Model"
    /// entry at the bottom of a consumable Model dropdown.
    ///
    /// Consumable models are scoped by category (dbo.ConsumableModel.Category), so the same model
    /// number can exist once per family; the category is fixed by the caller rather than chosen
    /// here, because it always follows the item's own category.
    ///
    /// NOTE: BatchAddItemDialog and EditItemDialog still build this form inline from their own
    /// private copies. This is the reusable version; converting them is left out of this change.
    /// </summary>
    public sealed class QuickAddConsumableModelDialog : Form
    {
        private readonly TextBox _modelNumber;
        private readonly CheckBox _isRequestable;
        private readonly Button _save;
        private readonly string _category;

        /// <summary>Id of the model created, once the dialog closes with OK.</summary>
        public int? NewConsumableModelId { get; private set; }

        /// <summary>Model number of the model created, once the dialog closes with OK.</summary>
        public string NewModelNumber { get; private set; }

        /// <param name="category">Ink, Toner or Print Head — the family the new model belongs to.</param>
        /// <param name="initialModelNumber">Pre-fills the Model Number box.</param>
        public QuickAddConsumableModelDialog(string category, string initialModelNumber = null)
        {
            _category = string.IsNullOrWhiteSpace(category) ? "Ink" : category;

            Text = "Add " + _category + " Model";

            _save = new Button { Text = "Save", DialogResult = DialogResult.None };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            _save.Click += Save_Click;

            var fields = Shared.QuickAddDialogLayout.BuildShell(this, _save, cancel);

            _modelNumber = new TextBox();
            if (!string.IsNullOrWhiteSpace(initialModelNumber))
                _modelNumber.Text = initialModelNumber.Trim();
            Shared.QuickAddDialogLayout.AddRow(fields, "Model Number *", _modelNumber);

            _isRequestable = new CheckBox { Text = "Is Requestable", Checked = true };
            Shared.QuickAddDialogLayout.AddFullWidth(fields,
                Shared.QuickAddDialogLayout.CheckBoxRow(_isRequestable), topMargin: 8);
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

                var repo = new ConsumableModelRepository();

                // UQ_ConsumableModel_ModelNumber_Category — unique per (model, category), so the
                // same number can legitimately exist under a different family.
                var existing = await repo.FindByModelNumberAsync(modelNumber, _category);
                if (existing != null)
                {
                    MessageBox.Show(this, $"Model '{modelNumber}' already exists for {_category}.", "Duplicate",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _save.Enabled = true;
                    _save.Text = "Save";
                    return;
                }

                var model = new ConsumableModelDto
                {
                    ModelNumber = modelNumber,
                    Category = _category,
                    IsRequestable = _isRequestable.Checked,
                    IsActive = true,
                    CreatedBy = AppSession.CurrentUserId,
                    CreatedAt = DateTime.Now
                };

                NewConsumableModelId = await repo.CreateAsync(model);
                NewModelNumber = model.ModelNumber;

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                Core.Logger.LogError("QuickAddConsumableModelDialog: save failed", ex);
                MessageBox.Show(this, "Error: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _save.Enabled = true;
                _save.Text = "Save";
            }
        }
    }
}
