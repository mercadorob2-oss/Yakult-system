using Yakult.Inventory.App.Session;
using System;
using System.Windows.Forms;
using System.Drawing;
using System.Data.SqlClient;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;


namespace Yakult.Inventory.App.Pages.Inventory
{
    public partial class EditInventoryDialog : Form
    {
        private InventoryViewDto _entry;
        private ComboBox cmbItem, cmbEntryType;
        private TextBox txtQuantity, txtDescription, txtPostedBy, txtRequestId;
        private DateTimePicker dtpDatePosted;
        private Button btnSave, btnCancel;
        private Label lblItem, lblEntryType, lblQuantity, lblDatePosted, lblDescription, lblPostedBy, lblRequestId, lblIsArchived;
        private CheckBox chkIsArchived;
        private string _connectionString;
        private int _originalQuantity;
        private string _originalEntryType;
        private int _originalItemId;
        private bool _originalIsArchived;

        public EditInventoryDialog(InventoryViewDto entry)
        {
            _entry = entry;
            _originalQuantity = entry.Quantity;
            _originalEntryType = entry.EntryType;
            _originalIsArchived = entry.IsArchived;

            _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            InitializeComponent();
            BuildUi();
            LoadItems();
            LoadData();
        }

        private void BuildUi()
        {
            Text = "Edit Inventory Entry";
            Width = 560;
            Height = 620;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            BackColor = Color.FromArgb(245, 246, 250);
            Font = new Font("Segoe UI", 9F);

            Controls.Clear();

            // Item
            lblItem = new Label { Text = "Item *", AutoSize = true };
            cmbItem = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

            // Entry Type
            lblEntryType = new Label { Text = "Entry Type *", AutoSize = true };
            cmbEntryType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbEntryType.Items.AddRange(new object[] { "Positive", "Negative" });

            // Quantity
            lblQuantity = new Label { Text = "Quantity *", AutoSize = true };
            txtQuantity = new TextBox();

            // Date Posted
            lblDatePosted = new Label { Text = "Date Posted *", AutoSize = true };
            dtpDatePosted = new DateTimePicker();

            // Posted By (read-only)
            lblPostedBy = new Label { Text = "Posted By", AutoSize = true };
            txtPostedBy = new TextBox
            {
                ReadOnly = true,
                BackColor = SystemColors.Control
            };

            // Request ID (read-only)
            lblRequestId = new Label { Text = "Request ID", AutoSize = true };
            txtRequestId = new TextBox
            {
                ReadOnly = true,
                BackColor = SystemColors.Control
            };

            // Description
            lblDescription = new Label { Text = "Description", AutoSize = true };
            txtDescription = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };

            // Is Archived checkbox
            lblIsArchived = new Label { Text = "Is Archived", AutoSize = true };
            chkIsArchived = new CheckBox { AutoSize = true };

            // Buttons
            btnSave = new Button { Text = "Save", Width = 96, Height = 34 };
            btnCancel = new Button { Text = "Cancel", Width = 96, Height = 34, DialogResult = DialogResult.Cancel };

            btnSave.Click += BtnSave_Click;

            var accent = Color.FromArgb(0, 150, 136);
            var accentHover = Color.FromArgb(0, 170, 156);

            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.BackColor = accent;
            btnSave.ForeColor = Color.White;
            btnSave.UseVisualStyleBackColor = false;
            btnSave.MouseEnter += (s, e) => btnSave.BackColor = accentHover;
            btnSave.MouseLeave += (s, e) => btnSave.BackColor = accent;

            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.FlatAppearance.BorderColor = accent;
            btnCancel.BackColor = Color.White;
            btnCancel.ForeColor = accent;
            btnCancel.UseVisualStyleBackColor = false;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(14),
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var card = new ReaLTaiizor.Controls.MaterialCard
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14),
                BackColor = Color.White,
                Margin = new Padding(0)
            };

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(0, 0, SystemInformation.VerticalScrollBarWidth, 0)
            };

            var form = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(0)
            };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            void AddRow(Control label, Control control, bool multiline = false, bool isCheckbox = false)
            {
                label.Anchor = AnchorStyles.Left;
                label.Margin = new Padding(0, 10, 10, 0);

                var height = multiline ? 110 : (isCheckbox ? 24 : 34);
                var host = new Panel { Dock = DockStyle.Fill, Height = height, Margin = new Padding(0, 8, 0, 0) };

                if (isCheckbox)
                {
                    control.Dock = DockStyle.Left;
                }
                else
                {
                    control.Dock = DockStyle.Fill;
                }

                host.Controls.Add(control);

                var rowIndex = form.RowCount++;
                form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                form.Controls.Add(label, 0, rowIndex);
                form.Controls.Add(host, 1, rowIndex);
            }

            cmbItem.Height = 28;
            cmbEntryType.Width = 200;
            txtQuantity.Width = 150;
            dtpDatePosted.Width = 200;
            txtPostedBy.Width = 250;
            txtRequestId.Width = 150;
            txtDescription.Height = 100;

            AddRow(lblItem, cmbItem);
            AddRow(lblEntryType, cmbEntryType);
            AddRow(lblQuantity, txtQuantity);
            AddRow(lblDatePosted, dtpDatePosted);
            AddRow(lblPostedBy, txtPostedBy);
            AddRow(lblRequestId, txtRequestId);
            AddRow(lblDescription, txtDescription, multiline: true);
            AddRow(lblIsArchived, chkIsArchived, isCheckbox: true);

            scroll.Controls.Add(form);
            card.Controls.Add(scroll);

            var footer = new Panel { Dock = DockStyle.Fill, Height = 46, Margin = new Padding(0, 10, 0, 0) };
            var footerFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            footerFlow.Controls.Add(btnCancel);
            footerFlow.Controls.Add(btnSave);
            footer.Controls.Add(footerFlow);

            root.Controls.Add(card, 0, 0);
            root.Controls.Add(footer, 0, 1);
            Controls.Add(root);

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private void LoadItems()
        {
            cmbItem.Items.Clear();

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT ItemId, Name FROM dbo.Item WHERE Active = 1 ORDER BY Name", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbItem.Items.Add(new ItemItem
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load items: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadData()
        {
            // Get ItemId from database
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT ItemId FROM dbo.Inventory WHERE InvId = @InvId", con))
                    {
                        cmd.Parameters.AddWithValue("@InvId", _entry.InvId);
                        var result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            _originalItemId = Convert.ToInt32(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load item ID: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Set Item selection
            for (int i = 0; i < cmbItem.Items.Count; i++)
            {
                if (cmbItem.Items[i] is ItemItem item && item.Name == _entry.ItemName)
                {
                    cmbItem.SelectedIndex = i;
                    break;
                }
            }

            // Set Entry Type
            int entryTypeIndex = cmbEntryType.FindStringExact(_entry.EntryType);
            if (entryTypeIndex >= 0)
                cmbEntryType.SelectedIndex = entryTypeIndex;

            txtQuantity.Text = _entry.Quantity.ToString();
            dtpDatePosted.Value = _entry.DatePosted;
            txtPostedBy.Text = _entry.PostedByName;
            txtRequestId.Text = _entry.RequestId.HasValue ? _entry.RequestId.Value.ToString() : "N/A";
            txtDescription.Text = _entry.Description ?? string.Empty;
            chkIsArchived.Checked = _entry.IsArchived;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            // Validation
            if (!(cmbItem.SelectedItem is ItemItem itemItem) || !itemItem.Id.HasValue)
            {
                MessageBox.Show("Please select an Item.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbItem.Focus();
                return;
            }

            if (cmbEntryType.SelectedItem == null)
            {
                MessageBox.Show("Please select an Entry Type.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbEntryType.Focus();
                return;
            }

            if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity <= 0)
            {
                MessageBox.Show("Please enter a valid Quantity (must be greater than 0).", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtQuantity.Focus();
                return;
            }

            // Handle archive status changes BEFORE saving
            bool archiveStatusChanged = chkIsArchived.Checked != _originalIsArchived;

            if (archiveStatusChanged)
            {
                try
                {
                    using (var con = new SqlConnection(_connectionString))
                    {
                        con.Open();
                        using (var transaction = con.BeginTransaction())
                        {
                            try
                            {
                                if (chkIsArchived.Checked)
                                {
                                    // Archive the inventory entry - insert into ArchiveStatus table
                                    string insertArchiveSql = @"
                                        INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                        VALUES ('Inventory', @InvId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                                    using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@InvId", _entry.InvId);
                                        cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                        cmd.Parameters.AddWithValue("@ArchiveReason", "Archived from Edit Inventory Dialog");
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                                else
                                {
                                    // Unarchive the inventory entry - delete from ArchiveStatus table
                                    string deleteArchiveSql = @"
                                        DELETE FROM ArchiveStatus
                                        WHERE EntityType = 'Inventory' AND EntityId = @InvId";

                                    using (var cmd = new SqlCommand(deleteArchiveSql, con, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@InvId", _entry.InvId);
                                        cmd.ExecuteNonQuery();
                                    }
                                }

                                transaction.Commit();
                            }
                            catch
                            {
                                transaction.Rollback();
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to update archive status: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var transaction = con.BeginTransaction())
                    {
                        try
                        {
                            // Update inventory entry
                            string updateInventorySql = @"
                                UPDATE dbo.Inventory 
                                SET Description = @Description,
                                    EntryType = @EntryType,
                                    Quantity = @Quantity,
                                    DatePosted = @DatePosted,
                                    ItemId = @ItemId
                                WHERE InvId = @InvId";

                            using (var cmd = new SqlCommand(updateInventorySql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Description", 
                                    string.IsNullOrWhiteSpace(txtDescription.Text) ? (object)DBNull.Value : txtDescription.Text.Trim());
                                cmd.Parameters.AddWithValue("@EntryType", cmbEntryType.SelectedItem.ToString());
                                cmd.Parameters.AddWithValue("@Quantity", quantity);
                                cmd.Parameters.AddWithValue("@DatePosted", dtpDatePosted.Value);
                                cmd.Parameters.AddWithValue("@ItemId", itemItem.Id.Value);
                                cmd.Parameters.AddWithValue("@InvId", _entry.InvId);

                                cmd.ExecuteNonQuery();
                            }

                            // Restore original stock adjustment
                            string restoreStockSql = @"
                                UPDATE dbo.Item 
                                SET StockOnHand = StockOnHand + @Adjustment
                                WHERE ItemId = @ItemId";

                            // First, reverse the original adjustment
                            using (var cmd = new SqlCommand(restoreStockSql, con, transaction))
                            {
                                // Reverse original: if was Positive, subtract; if was Negative, add
                                int reverseAdjustment = _originalEntryType == "Positive" ? -_originalQuantity : _originalQuantity;
                                cmd.Parameters.AddWithValue("@Adjustment", reverseAdjustment);
                                cmd.Parameters.AddWithValue("@ItemId", _originalItemId);
                                cmd.ExecuteNonQuery();
                            }

                            // Then, apply the new adjustment
                            using (var cmd = new SqlCommand(restoreStockSql, con, transaction))
                            {
                                // Apply new: if Positive, add; if Negative, subtract
                                int newAdjustment = cmbEntryType.SelectedItem.ToString() == "Positive" ? quantity : -quantity;
                                cmd.Parameters.AddWithValue("@Adjustment", newAdjustment);
                                cmd.Parameters.AddWithValue("@ItemId", itemItem.Id.Value);
                                cmd.ExecuteNonQuery();
                            }

                            ItemAuditTrailWriter.TryLog(con, transaction, new ItemAuditTrailDto
                            {
                                ItemId = itemItem.Id.Value,
                                SerialNumber = null,
                                Action = "Inventory Entry Updated",
                                ActionTime = DateTime.Now,
                                Direction = cmbEntryType.SelectedItem.ToString() == "Positive" ? "IN" : "OUT",
                                Status = "Completed",
                                ReferenceType = "Item",
                                ReferenceId = itemItem.Id.Value,
                                Notes = $"Entry #{_entry.InvId} updated: {txtDescription.Text?.Trim() ?? string.Empty}",
                                CreatedBy = AppSession.CurrentUserName ?? "System"
                            });

                            transaction.Commit();
                            DialogResult = DialogResult.OK;
                            Close();
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            throw;
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

        private class ItemItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }
    }
}

