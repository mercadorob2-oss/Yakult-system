using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Pages
{
    public partial class AddInventoryDialog : Form
    {
        private ComboBox cboItem, cboEntryType;
        private TextBox txtQuantity, txtDescription, txtSetId, txtReqId;
        private DateTimePicker dtpDatePosted;
        private Button btnSave, btnCancel;
        private Label lblItem, lblQuantity, lblEntryType, lblDescription, lblDatePosted, lblSetId, lblReqId;
        private string _connectionString;
        
        // \u2705 NEW: Use repositories
        private readonly InventoryRepository _inventoryRepo;
        private readonly InventoryService _inventoryService;

        public AddInventoryDialog()
        {
            InitializeComponent();
            
            _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            // \u2705 NEW: Initialize repositories
            _inventoryRepo = new InventoryRepository();
            _inventoryService = new InventoryService();

            BuildUi();
            LoadItems();
        }

        private void BuildUi()
        {
            Text = "Add Inventory Entry";
            Size = new Size(500, 480);  // \u2705 Increased height for new fields
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            int labelX = 20;
            int controlX = 140;
            int controlWidth = 320;
            int rowHeight = 50;
            int currentY = 20;

            // Item Selection
            lblItem = new Label
            {
                Text = "Item:",
                Location = new Point(labelX, currentY + 3),
                AutoSize = true
            };

            cboItem = new ComboBox
            {
                Location = new Point(controlX, currentY),
                Width = controlWidth,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            // \u2705 NEW: Auto-determine EntryType when item changes
            cboItem.SelectedIndexChanged += CboItem_SelectedIndexChanged;

            currentY += rowHeight;

            // Entry Type
            lblEntryType = new Label
            {
                Text = "Entry Type:",
                Location = new Point(labelX, currentY + 3),
                AutoSize = true
            };

            cboEntryType = new ComboBox
            {
                Location = new Point(controlX, currentY),
                Width = controlWidth,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            // Added non-stock movement option
            cboEntryType.Items.AddRange(new object[] { 
                EntryTypes.Positive, 
                EntryTypes.Negative, 
                EntryTypes.None 
            });
            cboEntryType.SelectedIndex = 0;

            currentY += rowHeight;

            // Quantity
            lblQuantity = new Label
            {
                Text = "Quantity:",
                Location = new Point(labelX, currentY + 3),
                AutoSize = true
            };

            txtQuantity = new TextBox
            {
                Location = new Point(controlX, currentY),
                Width = controlWidth
            };

            currentY += rowHeight;

            // \u2705 NEW: SetId field (optional)
            lblSetId = new Label
            {
                Text = "Set ID (optional):",
                Location = new Point(labelX, currentY + 3),
                AutoSize = true,
                ForeColor = Color.Gray
            };

            txtSetId = new TextBox
            {
                Location = new Point(controlX, currentY),
                Width = controlWidth,
            };

            currentY += rowHeight;

            // \u2705 NEW: ReqId field (optional)
            lblReqId = new Label
            {
                Text = "Request ID (optional):",
                Location = new Point(labelX, currentY + 3),
                AutoSize = true,
                ForeColor = Color.Gray
            };

            txtReqId = new TextBox
            {
                Location = new Point(controlX, currentY),
                Width = controlWidth,
            };

            currentY += rowHeight;

            // Description
            lblDescription = new Label
            {
                Text = "Description:",
                Location = new Point(labelX, currentY + 3),
                AutoSize = true
            };

            txtDescription = new TextBox
            {
                Location = new Point(controlX, currentY),
                Width = controlWidth,
                Height = 60,
                Multiline = true
            };

            currentY += 70;

            // Date Posted
            lblDatePosted = new Label
            {
                Text = "Date Posted:",
                Location = new Point(labelX, currentY + 3),
                AutoSize = true
            };

            dtpDatePosted = new DateTimePicker
            {
                Location = new Point(controlX, currentY),
                Width = controlWidth,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now
            };

            currentY += rowHeight;

            // Buttons
            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(controlX + controlWidth - 160, currentY),
                Width = 75,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };

            btnSave = new Button
            {
                Text = "Save",
                Location = new Point(controlX + controlWidth - 80, currentY),
                Width = 75,
                Height = 30
            };
            btnSave.Click += BtnSave_Click;

            Controls.AddRange(new Control[]
            {
                lblItem, cboItem,
                lblEntryType, cboEntryType,
                lblQuantity, txtQuantity,
                lblSetId, txtSetId,  // \u2705 NEW
                lblReqId, txtReqId,  // \u2705 NEW
                lblDescription, txtDescription,
                lblDatePosted, dtpDatePosted,
                btnSave, btnCancel
            });

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private void LoadItems()
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    string sql = "SELECT ItemId, Name, ItemType FROM dbo.Item WHERE Active = 1 ORDER BY Name";

                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var items = new System.Collections.Generic.List<ItemDto>();
                        while (reader.Read())
                        {
                            items.Add(new ItemDto
                            {
                                ItemId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                ItemType = reader.GetString(2)  // \u2705 NEW
                            });
                        }

                        cboItem.DataSource = items;
                        cboItem.DisplayMember = "Name";
                        cboItem.ValueMember = "ItemId";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load items: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // \u2705 NEW: Auto-determine EntryType based on ItemType
        private async void CboItem_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboItem.SelectedValue == null) return;

            try
            {
                int itemId = (int)cboItem.SelectedValue;
                
                // Determine correct EntryType
                string entryType = await _inventoryService.DetermineEntryTypeAsync(itemId);
                cboEntryType.SelectedItem = entryType;
                
                // Lock EntryType for non-inventory items
                if (entryType == EntryTypes.None)
                {
                    cboEntryType.Enabled = false;  // Lock to no stock movement
                    lblEntryType.ForeColor = Color.Gray;
                }
                else
                {
                    cboEntryType.Enabled = true;   // Allow user to choose Positive/Negative
                    lblEntryType.ForeColor = SystemColors.ControlText;
                }
            }
            catch (Exception ex)
            {
                // Silently handle - user can still manually select
                System.Diagnostics.Debug.WriteLine($"Failed to determine EntryType: {ex.Message}");
            }
        }

        // \u2705 UPDATED: Use repository pattern
        private async void BtnSave_Click(object sender, EventArgs e)
        {
            if (!ValidateInput())
                return;

            try
            {
                // Disable button to prevent double-click
                btnSave.Enabled = false;
                Cursor = Cursors.WaitCursor;

                var dto = new InventoryCreateDto
                {
                    Description = string.IsNullOrWhiteSpace(txtDescription.Text) 
                        ? null 
                        : txtDescription.Text.Trim(),
                    Quantity = int.Parse(txtQuantity.Text),
                    PostedBy = 1,  // TODO: Get from current logged-in user
                    ItemId = (int)cboItem.SelectedValue,
                    ReqId = string.IsNullOrEmpty(txtReqId.Text) 
                        ? null 
                        : (int?)int.Parse(txtReqId.Text),
                    SetId = string.IsNullOrEmpty(txtSetId.Text) 
                        ? null 
                        : (int?)int.Parse(txtSetId.Text)
                };

                // Validate before saving
                await _inventoryService.ValidateInventoryCreationAsync(dto);

                // Create inventory (SetId will be auto-determined if null)
                int invId = await _inventoryRepo.CreateInventoryAsync(dto);

                MessageBox.Show(
                    $"Inventory created successfully!\n\n" +
                    $"Inventory ID: {invId}\n" +
                    $"Set ID: {dto.SetId}\n" +
                    $"Entry Type: {cboEntryType.SelectedItem}",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (InventoryException ex)
            {
                MessageBox.Show(
                    $"Validation Error:\n\n{ex.Message}",
                    "Validation Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to add inventory entry:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnSave.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private bool ValidateInput()
        {
            if (cboItem.SelectedIndex == -1)
            {
                MessageBox.Show("Please select an item.", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cboItem.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtQuantity.Text) || 
                !int.TryParse(txtQuantity.Text, out int quantity) || 
                quantity <= 0)
            {
                MessageBox.Show("Please enter a valid positive quantity.", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtQuantity.Focus();
                return false;
            }

            if (cboEntryType.SelectedIndex == -1)
            {
                MessageBox.Show("Please select an entry type.", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cboEntryType.Focus();
                return false;
            }

            // \u2705 NEW: Validate SetId if provided
            if (!string.IsNullOrEmpty(txtSetId.Text))
            {
                if (!int.TryParse(txtSetId.Text, out int setId) || setId <= 0)
                {
                    MessageBox.Show("Set ID must be a valid positive number.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtSetId.Focus();
                    return false;
                }
            }

            // \u2705 NEW: Validate ReqId if provided
            if (!string.IsNullOrEmpty(txtReqId.Text))
            {
                if (!int.TryParse(txtReqId.Text, out int reqId) || reqId <= 0)
                {
                    MessageBox.Show("Request ID must be a valid positive number.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtReqId.Focus();
                    return false;
                }
            }

            return true;
        }

        private class ItemDto
        {
            public int ItemId { get; set; }
            public string Name { get; set; }
            public string ItemType { get; set; }  // \u2705 NEW
        }
    }
}

