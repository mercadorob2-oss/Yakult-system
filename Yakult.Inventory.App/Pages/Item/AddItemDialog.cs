using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Pages.Category;
using Yakult.Inventory.App.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Data.SqlClient;
using Yakult.Inventory.App.Core;


namespace Yakult.Inventory.App.Pages.Item
{
    public partial class AddItemDialog : Form
    {
        private ItemRepository _repository;
        private string _connectionString;

        // Controls
        private Label lblName, lblDescription, lblCategory, lblSerialNumber, lblModelNumber;
        private Label lblUnitOfMeasure, lblStockOnHand, lblActive, lblIsBorrowable;
        private Label lblCartridgeModel; // CARTRIDGE MODEL
        private Label lblConsumableModel; // CONSUMABLE MODEL (Ink/Toner/Print Head)
        private TextBox txtName, txtDescription, txtSerialNumber, txtModelNumber;
        private ComboBox cmbCategory, cmbUnitOfMeasure;
        private ComboBox cmbCartridgeModel; // CARTRIDGE MODEL
        private ComboBox cmbConsumableModel; // CONSUMABLE MODEL (Ink/Toner/Print Head)
        private TextBox txtStockOnHand;
        private CheckBox chkActive;
        private CheckBox chkIsBorrowable;
        private Button btnSave, btnCancel, btnAddCategory;

        public AddItemDialog()
        {
            _repository = new ItemRepository();
            _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            InitializeComponent();
            InitializeCustomControls();
            LoadCategories();
            LoadCartridgeModels();
            LoadConsumableModels();
        }

        private void InitializeCustomControls()
        {
            // Configure form
            Text = "Add Item";
            Size = new Size(550, 550);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Initialize labels
            lblName = new Label { Text = "Item Name: *", Location = new Point(20, 20), AutoSize = true };
            lblDescription = new Label { Text = "Description:", Location = new Point(20, 55), AutoSize = true };
            lblCategory = new Label { Text = "Category: *", Location = new Point(20, 130), AutoSize = true };
            lblSerialNumber = new Label { Text = "Serial Number:", Location = new Point(20, 165), AutoSize = true };
            lblModelNumber = new Label { Text = "Model Number: *", Location = new Point(20, 200), AutoSize = true };
            lblUnitOfMeasure = new Label { Text = "Unit of Measure: *", Location = new Point(20, 235), AutoSize = true };
            lblStockOnHand = new Label { Text = "Quantity:", Location = new Point(20, 270), AutoSize = true };
            lblActive = new Label { Text = "Active:", Location = new Point(20, 340), AutoSize = true };
            lblIsBorrowable = new Label { Text = "Borrowable:", Location = new Point(20, 370), AutoSize = true };
            lblCartridgeModel = new Label { Text = "Cartridge Model: *", Location = new Point(20, 305), AutoSize = true, Visible = false };
            lblConsumableModel = new Label { Text = "Model:", Location = new Point(20, 305), AutoSize = true, Visible = false };

            // Initialize textboxes
            txtName = new TextBox { Location = new Point(150, 17), Width = 350 };
            txtDescription = new TextBox { Location = new Point(150, 52), Width = 350, Height = 60, Multiline = true };
            txtSerialNumber = new TextBox { Location = new Point(150, 162), Width = 350 };
            txtModelNumber = new TextBox { Location = new Point(150, 197), Width = 350 };
            txtStockOnHand = new TextBox 
            { 
                Location = new Point(150, 267), 
                Width = 100,
                Text = "1",
                ReadOnly = true,
                BackColor = SystemColors.Control,
                TabStop = false
            };

            cmbCartridgeModel = new ComboBox
            {
                Location = new Point(150, 302),
                Width = 350,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };

            cmbConsumableModel = new ComboBox
            {
                Location = new Point(150, 302),
                Width = 350,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };

            // Initialize comboboxes
            cmbCategory = new ComboBox 
            { 
                Location = new Point(150, 127), 
                Width = 285, 
                DropDownStyle = ComboBoxStyle.DropDownList 
            };

            btnAddCategory = new Button
            {
                Text = "+ Category",
                Location = new Point(445, 126),
                Width = 55,
                Height = 23,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.LightGreen,
                Cursor = Cursors.Hand
            };
            btnAddCategory.Click += BtnAddCategory_Click;

            cmbUnitOfMeasure = new ComboBox 
            { 
                Location = new Point(150, 232), 
                Width = 350, 
                DropDownStyle = ComboBoxStyle.DropDown 
            };
            cmbUnitOfMeasure.Items.AddRange(new object[] { "Unit", "Piece", "Cartridge", "Box", "Set" });

            // Initialize checkbox
            chkActive = new CheckBox
            {
                Location = new Point(150, 337),
                AutoSize = true,
                Checked = true,
                Text = "Item is Active"
            };

            chkIsBorrowable = new CheckBox
            {
                Location = new Point(150, 367),
                AutoSize = true,
                Checked = false,
                Text = "Available for borrowing"
            };

            // Initialize buttons
            btnSave = new Button { Text = "Save", Location = new Point(320, 485), Width = 90, Height = 30 };
            btnCancel = new Button { Text = "Cancel", Location = new Point(420, 485), Width = 90, Height = 30, DialogResult = DialogResult.Cancel };

            // Event handlers
            btnSave.Click += BtnSave_Click;
            cmbCategory.SelectedIndexChanged += CmbCategory_SelectedIndexChanged;

            // Add controls to form
            Controls.AddRange(new Control[] {
                lblName, txtName,
                lblDescription, txtDescription,
                lblCategory, cmbCategory, btnAddCategory,
                lblSerialNumber, txtSerialNumber,
                lblModelNumber, txtModelNumber,
                lblCartridgeModel, cmbCartridgeModel,
                lblConsumableModel, cmbConsumableModel,
                lblUnitOfMeasure, cmbUnitOfMeasure,
                lblStockOnHand, txtStockOnHand,
                lblActive, chkActive,
                lblIsBorrowable, chkIsBorrowable,
                btnSave, btnCancel
            });

            CancelButton = btnCancel;
        }

        private void LoadCategories()
        {
            try
            {
                cmbCategory.Items.Clear();

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    string query = @"
                        SELECT CategoryId, Name 
                        FROM dbo.ItemCategory 
                        WHERE Active = 1 
                        ORDER BY Name";

                    using (var cmd = new SqlCommand(query, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbCategory.Items.Add(new CategoryItem
                            {
                                CategoryId = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }

                if (cmbCategory.Items.Count == 0)
                {
                    MessageBox.Show("No categories found. Please add a category first.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load categories: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CmbCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedCategory = cmbCategory.SelectedItem as CategoryItem;
            bool isCartridge = selectedCategory != null &&
                string.Equals(selectedCategory.Name, "Cartridge", StringComparison.OrdinalIgnoreCase);

            lblCartridgeModel.Visible = isCartridge;
            cmbCartridgeModel.Visible = isCartridge;

            if (isCartridge && cmbCartridgeModel.Items.Count == 0)
            {
                LoadCartridgeModels();
            }

            bool isConsumable = CanonicalConsumableCategory(selectedCategory?.Name) != null;
            lblConsumableModel.Visible = isConsumable;
            cmbConsumableModel.Visible = isConsumable;

            if (isConsumable)
            {
                // Category changed — reload so the dropdown reflects the newly selected category's models
                LoadConsumableModels();
            }
        }

        // Categories in scope for the ConsumableModel grouping — matches the fuzzy
        // (case/space-insensitive) filter used in RequestRepository.FulfillmentTrackedRequestsCte.
        private static string CanonicalConsumableCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return null;
            var normalized = categoryName.Replace(" ", "").ToLowerInvariant();
            if (normalized.Contains("ink")) return "Ink";
            if (normalized.Contains("toner")) return "Toner";
            if (normalized.Contains("printhead")) return "Print Head";
            return null;
        }

        private async void LoadConsumableModels()
        {
            try
            {
                cmbConsumableModel.Items.Clear();

                cmbConsumableModel.Items.Add(new ConsumableModelItem
                {
                    ConsumableModelId = 0,
                    ModelNumber = "(Select model)"
                });

                string category = CanonicalConsumableCategory((cmbCategory.SelectedItem as CategoryItem)?.Name);

                var repo = new ConsumableModelRepository();
                var models = await repo.GetAllActiveModelsAsync(category);

                foreach (var model in models)
                {
                    cmbConsumableModel.Items.Add(new ConsumableModelItem
                    {
                        ConsumableModelId = model.ConsumableModelId,
                        ModelNumber = model.ModelNumber
                    });
                }

                if (cmbConsumableModel.SelectedIndex < 0 && cmbConsumableModel.Items.Count > 0)
                    cmbConsumableModel.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load consumable models: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void LoadCartridgeModels()
        {
            try
            {
                cmbCartridgeModel.Items.Clear();

                cmbCartridgeModel.Items.Add(new CartridgeModelItem
                {
                    CartridgeModelId = 0,
                    ModelNumber = "(Select cartridge model)",
                    VendorName = null,
                    VendorId = null
                });

                var cartridgeModelRepo = new CartridgeModelRepository();
                var models = await cartridgeModelRepo.GetAllActiveModelsAsync();

                foreach (var model in models)
                {
                    cmbCartridgeModel.Items.Add(new CartridgeModelItem
                    {
                        CartridgeModelId = model.CartridgeModelId,
                        ModelNumber = model.ModelNumber,
                        VendorName = model.VendorName,
                        VendorId = model.VendorId
                    });
                }

                if (cmbCartridgeModel.SelectedIndex < 0 && cmbCartridgeModel.Items.Count > 0)
                    cmbCartridgeModel.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load cartridge models: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAddCategory_Click(object sender, EventArgs e)
        {
            var dialog = new QuickAddCategory();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                // Reload categories
                LoadCategories();

                // Select the newly added category
                for (int i = 0; i < cmbCategory.Items.Count; i++)
                {
                    if (cmbCategory.Items[i] is CategoryItem item && item.Name == dialog.NewCategoryName)
                    {
                        cmbCategory.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Please enter an Item Name.", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
                return;
            }

            // ModelNumber is now nullable (required only for IsPhysicalAsset items).
            // This dialog has no ItemType selector, so we skip the hard validation here.
            // The DB CHECK constraint covers IsPhysicalAsset = 1 cases at the server level.

            if (string.IsNullOrWhiteSpace(cmbUnitOfMeasure.Text))
            {
                MessageBox.Show("Please enter a Unit of Measure.", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbUnitOfMeasure.Focus();
                return;
            }

            if (cmbCategory.SelectedItem == null)
            {
                MessageBox.Show("Please select a Category.", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbCategory.Focus();
                return;
            }

            // CARTRIDGE MODEL VALIDATION: CartridgeModelId is REQUIRED when Category = 'Cartridge'
            var validationCategory = cmbCategory.SelectedItem as CategoryItem;
            if (validationCategory != null &&
                string.Equals(validationCategory.Name, "Cartridge", StringComparison.OrdinalIgnoreCase))
            {
                var selectedModel = cmbCartridgeModel.SelectedItem as CartridgeModelItem;
                if (selectedModel == null || selectedModel.CartridgeModelId <= 0)
                {
                    MessageBox.Show("Cartridge Model is required for cartridge items.\n\n" +
                        "Please select a valid Cartridge Model from the dropdown.",
                        "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cmbCartridgeModel.Focus();
                    return;
                }
            }

            try
            {
                btnSave.Enabled = false;
                btnCancel.Enabled = false;
                Cursor = Cursors.WaitCursor;

                var selectedCategory = cmbCategory.SelectedItem as CategoryItem;

                // Resolve CartridgeModelId for Cartridge items
                bool isCartridge = string.Equals(selectedCategory.Name, "Cartridge", StringComparison.OrdinalIgnoreCase);
                int? cartridgeModelId = null;
                if (isCartridge)
                {
                    var cartridgeModelItem = cmbCartridgeModel.SelectedItem as CartridgeModelItem;
                    if (cartridgeModelItem != null && cartridgeModelItem.CartridgeModelId > 0)
                        cartridgeModelId = cartridgeModelItem.CartridgeModelId;
                }

                int? consumableModelId = null;
                if (CanonicalConsumableCategory(selectedCategory.Name) != null)
                {
                    var consumableModelItem = cmbConsumableModel.SelectedItem as ConsumableModelItem;
                    if (consumableModelItem != null && consumableModelItem.ConsumableModelId > 0)
                        consumableModelId = consumableModelItem.ConsumableModelId;
                }

                var item = new ItemDto
                {
                    Name = txtName.Text.Trim(),
                    Description = string.IsNullOrWhiteSpace(txtDescription.Text) ? null : txtDescription.Text.Trim(),
                    CategoryId = selectedCategory.CategoryId,
                    Category = selectedCategory.Name,
                    SerialNumber = string.IsNullOrWhiteSpace(txtSerialNumber.Text) ? null : txtSerialNumber.Text.Trim(),
                    ModelNumber = txtModelNumber.Text.Trim(),
                    CartridgeModelId = cartridgeModelId,
                    ConsumableModelId = consumableModelId,
                    Active = chkActive.Checked,
                    IsBorrowable = chkIsBorrowable.Checked,
                    UnitOfMeasure = cmbUnitOfMeasure.Text.Trim(),
                    StockOnHand = 1,
                    DateCreated = DateTime.Now,
                    CreatedByUserId = AppSession.CurrentUserId,
                    CreatedByName = AppSession.CurrentUserName
                };

                int itemId;
                using (Yakult.Inventory.App.Services.InventoryActivityNotifier.Source("Add Item"))
                    itemId = _repository.AddItem(item);

                if (itemId > 0)
                {
                    MessageBox.Show("Item added successfully!", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    MessageBox.Show("Failed to add item. No ID was returned.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding item: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSave.Enabled = true;
                btnCancel.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        // Helper class for ComboBox items
        private class CategoryItem
        {
            public int CategoryId { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private class CartridgeModelItem
        {
            public int CartridgeModelId { get; set; }
            public string ModelNumber { get; set; }
            public string VendorName { get; set; }
            public int? VendorId { get; set; }

            public override string ToString() => string.IsNullOrWhiteSpace(VendorName)
                ? ModelNumber
                : $"{ModelNumber} - {VendorName}";
        }

        private class ConsumableModelItem
        {
            public int ConsumableModelId { get; set; }
            public string ModelNumber { get; set; }

            public override string ToString() => ModelNumber;
        }
    }
}

