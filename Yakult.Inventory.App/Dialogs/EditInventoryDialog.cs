using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages.Inventory;


namespace Yakult.Inventory.App.Pages
{
    public partial class EditInventoryDialog : Form
    {
        private ComboBox cboItem, cboEntryType;
        private TextBox txtQuantity, txtDescription;
        private DateTimePicker dtpDatePosted;
        private Button btnSave, btnCancel;
        private Label lblItem, lblQuantity, lblEntryType, lblDescription, lblDatePosted, lblInvId;
        private string _connectionString;
        private InventoryViewDto _inventoryEntry;

        public EditInventoryDialog(InventoryViewDto inventoryEntry)
        {
            _inventoryEntry = inventoryEntry;
            
            InitializeComponent();
            _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            BuildUi();
            LoadItems();
            PopulateFields();
        }

        private void BuildUi()
        {
            Text = "Edit Inventory Entry";
            Size = new Size(500, 410);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            int labelX = 20;
            int controlX = 140;
            int controlWidth = 320;
            int rowHeight = 50;
            int currentY = 20;

            // Inventory ID (Read-only)
            lblInvId = new Label
            {
                Text = $"Inventory ID: {_inventoryEntry.InvId}",
                Location = new Point(labelX, currentY),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };

            currentY += 35;

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
            cboEntryType.Items.AddRange(new object[] { "Positive", "Negative" });

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
                Format = DateTimePickerFormat.Short
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
                Text = "Update",
                Location = new Point(controlX + controlWidth - 80, currentY),
                Width = 75,
                Height = 30
            };
            btnSave.Click += BtnSave_Click;

            Controls.AddRange(new Control[]
            {
                lblInvId,
                lblItem, cboItem,
                lblEntryType, cboEntryType,
                lblQuantity, txtQuantity,
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
                    string sql = "SELECT ItemId, Name FROM dbo.Item ORDER BY Name";

                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var items = new System.Collections.Generic.List<ItemDto>();
                        while (reader.Read())
                        {
                            items.Add(new ItemDto
                            {
                                ItemId = reader.GetInt32(0),
                                Name = reader.GetString(1)
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

        private void PopulateFields()
        {
            try
            {
                // Get the ItemId for the current inventory entry
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    string sql = "SELECT ItemId FROM dbo.Inventory WHERE InvId = @InvId";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@InvId", _inventoryEntry.InvId);
                        var itemId = cmd.ExecuteScalar();

                        if (itemId != null)
                        {
                            cboItem.SelectedValue = itemId;
                        }
                    }
                }

                // Set other fields
                cboEntryType.SelectedItem = _inventoryEntry.EntryType;
                txtQuantity.Text = _inventoryEntry.Quantity.ToString();
                txtDescription.Text = _inventoryEntry.Description ?? "";
                dtpDatePosted.Value = _inventoryEntry.DatePosted;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to populate fields: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (!ValidateInput())
                return;

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    string sql = @"
                        UPDATE dbo.Inventory 
                        SET 
                            ItemId = @ItemId,
                            Quantity = @Quantity,
                            EntryType = @EntryType,
                            Description = @Description,
                            DatePosted = @DatePosted
                        WHERE InvId = @InvId";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@InvId", _inventoryEntry.InvId);
                        cmd.Parameters.AddWithValue("@ItemId", cboItem.SelectedValue);
                        cmd.Parameters.AddWithValue("@Quantity", int.Parse(txtQuantity.Text));
                        cmd.Parameters.AddWithValue("@EntryType", cboEntryType.SelectedItem.ToString());
                        cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(txtDescription.Text) 
                            ? (object)DBNull.Value 
                            : txtDescription.Text.Trim());
                        cmd.Parameters.AddWithValue("@DatePosted", dtpDatePosted.Value);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            DialogResult = DialogResult.OK;
                            Close();
                        }
                        else
                        {
                            MessageBox.Show("Failed to update inventory entry. Entry may not exist.", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update inventory entry: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            if (string.IsNullOrWhiteSpace(txtQuantity.Text) || !int.TryParse(txtQuantity.Text, out int quantity) || quantity <= 0)
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

            return true;
        }

        private class ItemDto
        {
            public int ItemId { get; set; }
            public string Name { get; set; }
        }
    }
}

