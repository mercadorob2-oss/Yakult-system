using Yakult.Inventory.App.Session;
using System;
using System.Drawing; // <-- ADDED (for Font)
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages.Category;
using Yakult.Inventory.App.Repositories;


namespace Yakult.Inventory.App.Pages.Item
{
    public partial class AddItemPage : UserControl
    {
        private Panel headerPanel, bodyPanel, footerPanel;
        // --- MODIFIED: Added new textboxes ---
        private TextBox txtName, txtStockOnHand, txtDescription, txtCreatedBy, txtSerialNumber, txtModelNumber;
        private ComboBox cmbCategory; // Changed from TextBox to ComboBox
        private ComboBox cmbVendor;
        private ComboBox txtUnitOfMeasure;
        private DateTimePicker dtCreated;
        private CheckBox chkActive;
        private Button btnSave, btnCancel, btnAddCategory; // Added btnAddCategory
        private Label titleLabel;
        private bool _vendorsLoaded;

        public event Action<ItemDto> Submitted;

        public AddItemPage()
        {
            // InitializeComponent(); // This is often a call in the .Designer.cs file, make sure it's handled.
            BuildUi();
            LoadCategories(); // Load categories on startup
            Load += async (_, __) => await EnsureVendorsLoadedAsync();
        }

        public void PrefillSerial(string serialNumber)
        {
            if (txtSerialNumber == null) return;
            txtSerialNumber.Text = string.IsNullOrWhiteSpace(serialNumber) ? string.Empty : serialNumber.Trim();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            AutoScroll = false;

            // Create panels
            bodyPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };
            footerPanel = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(12, 8, 12, 8) };
            headerPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(12, 8, 12, 0) };

            Controls.Add(bodyPanel);
            Controls.Add(footerPanel);
            Controls.Add(headerPanel);

            // Header
            titleLabel = new Label
            {
                AutoSize = true,
                Text = "Add Item",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold), // <-- MODIFIED (System.Drawing.Font)
                Left = 0,
                Top = 0
            };
            headerPanel.Controls.Add(titleLabel);

            // Footer buttons
            btnSave = new Button { Text = "Save", Top = 8, Width = 80 };
            btnCancel = new Button { Text = "Cancel", Top = 8, Width = 80 };
            footerPanel.Controls.AddRange(new Control[] { btnSave, btnCancel });
            footerPanel.Resize += (s, e) =>
            {
                btnCancel.Left = footerPanel.ClientSize.Width - btnCancel.Width - 12;
                btnSave.Left = btnCancel.Left - btnSave.Width - 8;
            };

            // Body - Item fields (LEFT COLUMN)
            var lblName = new Label { AutoSize = true, Text = "Item Name *", Left = 0, Top = 8 };
            txtName = new TextBox { Left = 0, Top = 28, Width = 300 };

            var lblDescription = new Label { AutoSize = true, Text = "Description", Left = 0, Top = 60 };
            txtDescription = new TextBox { Left = 0, Top = 80, Width = 300, Multiline = true, Height = 60 };

            // --- ADDED Category ComboBox + Add Button ---
            var lblCategory = new Label { AutoSize = true, Text = "Category *", Left = 0, Top = 150 };
            cmbCategory = new ComboBox
            {
                Left = 0,
                Top = 170,
                Width = 225,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            btnAddCategory = new Button
            {
                Text = "+ Add Category",
                Left = 235,
                Top = 169,
                Width = 65,
                Height = 23,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.LightGreen,
                Cursor = Cursors.Hand
            };
            btnAddCategory.Click += BtnAddCategory_Click;

            // --- ADDED SerialNumber ---
            var lblSerialNumber = new Label { AutoSize = true, Text = "Serial Number", Left = 0, Top = 202 };
            txtSerialNumber = new TextBox { Left = 0, Top = 222, Width = 300 };

            // --- NEW: Model Number ---
            var lblModelNumber = new Label { AutoSize = true, Text = "Model Number *", Left = 0, Top = 254 };
            txtModelNumber = new TextBox { Left = 0, Top = 274, Width = 300 };

            var lblUnitOfMeasure = new Label { AutoSize = true, Text = "Unit of Measure *", Left = 0, Top = 306 };
            txtUnitOfMeasure = new ComboBox
            {
                Left = 0,
                Top = 326,
                Width = 300,
                DropDownStyle = ComboBoxStyle.DropDown  // Allow typing custom values
            };
            txtUnitOfMeasure.Items.AddRange(new object[] { "Unit", "Piece", "Cartridge", "Box", "Set" });

            var lblStockOnHand = new Label { AutoSize = true, Text = "Quantity *", Left = 0, Top = 358 };
            txtStockOnHand = new TextBox
            {
                Left = 0,
                Top = 378,
                Width = 300,
                Text = "1",
                ReadOnly = true,
                BackColor = System.Drawing.SystemColors.Control, // Gray background to show it's read-only
                TabStop = false  // Skip this field when user presses Tab
            };

            var lblActive = new Label { AutoSize = true, Text = "Active", Left = 0, Top = 410 };
            chkActive = new CheckBox { Left = 0, Top = 430, Checked = true, Text = "Item is Active" };

            // RIGHT COLUMN - Date and User info
            var lblCreated = new Label { AutoSize = true, Text = "Date Created", Left = 450, Top = 8 };
            dtCreated = new DateTimePicker { Left = 450, Top = 28, Width = 180, Value = DateTime.Now };

            var lblBy = new Label { AutoSize = true, Text = "Created By", Left = 450, Top = 60 };
            txtCreatedBy = new TextBox
            {
                Left = 450,
                Top = 80,
                Width = 180,
                ReadOnly = true,
                TabStop = false,
                BackColor = System.Drawing.SystemColors.Window,
                Text = AppSession.CurrentUserName ?? string.Empty
            };

            var lblVendor = new Label { AutoSize = true, Text = "Vendor", Left = 450, Top = 112 };
            cmbVendor = new ComboBox
            {
                Left = 450,
                Top = 132,
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbVendor.Items.Add(new VendorItem { VendorId = 0, VendorName = "(None)" });
            cmbVendor.SelectedIndex = 0;

            // --- MODIFIED: Added new controls ---
            bodyPanel.Controls.AddRange(new Control[] {
                lblName, txtName, lblDescription, txtDescription,
                lblCategory, cmbCategory, btnAddCategory, // <-- MODIFIED
                lblSerialNumber, txtSerialNumber,
                lblModelNumber, txtModelNumber,
                lblUnitOfMeasure, txtUnitOfMeasure, lblStockOnHand, txtStockOnHand,
                lblActive, chkActive,
                lblCreated, dtCreated, lblBy, txtCreatedBy,
                lblVendor, cmbVendor
            });

            // Wire events
            btnSave.Click += (s, e) => SaveItem();
            btnCancel.Click += (s, e) =>
            {
                var result = MessageBox.Show("Are you sure you want to cancel?", "Cancel",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    ClearForm();
                }
            };

            // Responsive layout
            bodyPanel.Resize += (s, e) =>
            {
                dtCreated.Left = bodyPanel.ClientSize.Width - dtCreated.Width - 12 - 20;
                txtCreatedBy.Left = bodyPanel.ClientSize.Width - txtCreatedBy.Width - 12 - 20;
                cmbVendor.Left = bodyPanel.ClientSize.Width - cmbVendor.Width - 12 - 20;
                lblCreated.Left = dtCreated.Left;
                lblBy.Left = txtCreatedBy.Left;
                lblVendor.Left = cmbVendor.Left;
            };
        }

        private void SaveItem()
        {
            // Validation
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Please enter an Item Name.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
                return;
            }

            // Model Number is optional for Services; only enforce for Hardware items.
            // AddItemPage defaults to Hardware, so we still validate it here.
            // If this page is ever updated with an ItemType selector, move this guard there.
            if (string.IsNullOrWhiteSpace(txtModelNumber.Text))
            {
                MessageBox.Show("Please enter a Model Number.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtModelNumber.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtUnitOfMeasure.Text))
            {
                MessageBox.Show("Please enter a Unit of Measure (e.g., 'piece', 'box', 'cartridge').", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtUnitOfMeasure.Focus();
                return;
            }

            // --- ADDED: Category validation ---
            if (cmbCategory.SelectedItem == null)
            {
                MessageBox.Show("Please select a Category.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbCategory.Focus();
                return;
            }

            //if (!int.TryParse(txtStockOnHand.Text, out int stockOnHand) || stockOnHand < 0)
            //{
            //    MessageBox.Show("Please enter a valid Stock on Hand (must be 0 or greater).", "Validation",
            //        MessageBoxButtons.OK, MessageBoxIcon.Warning);
            //    txtStockOnHand.Focus();
            //    return;
            //}

            // --- ADDED: SerialNumber validation ---
            //string serialNumber = txtSerialNumber.Text.Trim();
            //if (!string.IsNullOrWhiteSpace(serialNumber) && stockOnHand != 1)
            //{
            //    MessageBox.Show("Stock on Hand must be 1 when a Serial Number is provided.", "Validation",
            //        MessageBoxButtons.OK, MessageBoxIcon.Warning);
            //    txtStockOnHand.Focus();
            //    return;
            //}

            // Get selected category
            var selectedCategory = cmbCategory.SelectedItem as CategoryItem;
            var selectedVendor = cmbVendor.SelectedItem as VendorItem;
            
            // --- MODIFIED: Added new properties ---
            var item = new ItemDto
            {
                Name = txtName.Text.Trim(),
                Description = txtDescription.Text.Trim(),
                CategoryId = selectedCategory.CategoryId, // Use CategoryId if migrated
                Category = selectedCategory?.Name, // Keep Category name for compatibility
                SerialNumber = string.IsNullOrWhiteSpace(txtSerialNumber.Text) ? null : txtSerialNumber.Text.Trim(), // <-- ADDED (sends NULL if empty)
                ModelNumber = txtModelNumber.Text.Trim(),
                Active = chkActive.Checked,
                UnitOfMeasure = txtUnitOfMeasure.Text.Trim(),
                StockOnHand = 1,
                DateCreated = dtCreated.Value,
                CreatedByUserId = AppSession.CurrentUserId,
                CreatedByName = AppSession.CurrentUserName,
                VendorId = (selectedVendor != null && selectedVendor.VendorId > 0) ? (int?)selectedVendor.VendorId : null,
                VendorName = (selectedVendor != null && selectedVendor.VendorId > 0) ? selectedVendor.VendorName : null
            };

            Submitted?.Invoke(item);
        }

        private void ClearForm()
        {
            txtName.Clear();
            txtDescription.Clear();
            cmbCategory.SelectedIndex = -1; // Clear selection
            txtSerialNumber.Clear();
            txtModelNumber.Clear();
            txtUnitOfMeasure.SelectedIndex = -1; // For ComboBox, use SelectedIndex = -1
            txtUnitOfMeasure.Text = "";
            txtStockOnHand.Text = "1";
            chkActive.Checked = true;
            dtCreated.Value = DateTime.Now;
            if (cmbVendor != null)
                cmbVendor.SelectedIndex = cmbVendor.Items.Count > 0 ? 0 : -1;
        }

        private void LoadCategories()
        {
            try
            {
                cmbCategory.Items.Clear();
                
                var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs))
                {
                    MessageBox.Show("Connection string not configured.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using (var con = new System.Data.SqlClient.SqlConnection(cs))
                {
                    con.Open();
                    
                    // Try to load from ItemCategory table first (if migration done)
                    string query = @"
                        SELECT CategoryId, Name 
                        FROM dbo.ItemCategory 
                        WHERE Active = 1 
                        ORDER BY Name";
                    
                    using (var cmd = new System.Data.SqlClient.SqlCommand(query, con))
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
                // If ItemCategory table doesn't exist, show helpful message
                MessageBox.Show($"Failed to load categories: {ex.Message}\n\nPlease run the migration scripts.", "Error",
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

        private async System.Threading.Tasks.Task EnsureVendorsLoadedAsync()
        {
            if (_vendorsLoaded || cmbVendor == null || IsDisposed)
                return;

            try
            {
                _vendorsLoaded = true;
                cmbVendor.Items.Clear();
                cmbVendor.Items.Add(new VendorItem
                {
                    VendorId = 0,
                    VendorName = "(None)"
                });

                var repo = new VendorRepository();
                var vendors = await repo.GetAllVendorsAsync();
                if (vendors != null)
                {
                    foreach (var vendor in vendors)
                    {
                        if (vendor == null || !vendor.IsActive || vendor.IsArchived || string.IsNullOrWhiteSpace(vendor.VendorName))
                            continue;

                        cmbVendor.Items.Add(new VendorItem
                        {
                            VendorId = vendor.VendorId,
                            VendorName = vendor.VendorName.Trim()
                        });
                    }
                }

                cmbVendor.SelectedIndex = cmbVendor.Items.Count > 0 ? 0 : -1;
            }
            catch (Exception ex)
            {
                _vendorsLoaded = false;
                cmbVendor.Items.Clear();
                cmbVendor.Items.Add(new VendorItem
                {
                    VendorId = 0,
                    VendorName = "(None)"
                });
                cmbVendor.SelectedIndex = 0;

                MessageBox.Show($"Failed to load vendors: {ex.Message}", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Helper class for ComboBox items
        private class CategoryItem
        {
            public int CategoryId { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private class VendorItem
        {
            public int VendorId { get; set; }
            public string VendorName { get; set; }
            public override string ToString() => VendorName;
        }
    }

    
    
}
