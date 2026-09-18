using Yakult.Inventory.App.Session;
using System;
using System.Windows.Forms;
using System.Drawing;
using System.Data.SqlClient;
using Yakult.Inventory.App.Core;


namespace Yakult.Inventory.App.Pages.Inventory
{
    public partial class AddInventoryDialog : Form
    {
        private ComboBox cmbItem, cmbEntryType;
        private TextBox txtQuantity, txtDescription, txtPostedBy;
        private DateTimePicker dtpDatePosted;
        private Button btnSave, btnCancel;
        private Label lblItem, lblEntryType, lblQuantity, lblDatePosted, lblDescription, lblPostedBy;
        private string _connectionString;

        public AddInventoryDialog()
        {
            _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            InitializeComponent();
            BuildUi();
            LoadItems();
            LoadCurrentUser();
        }

        private void BuildUi()
        {
            Text = "Add Inventory Entry";
            Width = 560;
            Height = 520;
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

            // Entry Type (auto-determined, read-only display)
            lblEntryType = new Label { Text = "Entry Type (Auto)", AutoSize = true };
            cmbEntryType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            cmbEntryType.Items.AddRange(new object[] { "Positive", "Negative", "Fixed Assets" });
            cmbEntryType.SelectedIndex = 0; // Will be updated based on ItemType

            // CRITICAL: Auto-update EntryType when Item or Quantity changes
            cmbItem.SelectedIndexChanged += UpdateEntryType;

            // Quantity
            lblQuantity = new Label { Text = "Quantity *", AutoSize = true };
            txtQuantity = new TextBox();
            txtQuantity.TextChanged += (s, e) => UpdateEntryType(s, e);

            // Date Posted
            lblDatePosted = new Label { Text = "Date Posted *", AutoSize = true };
            dtpDatePosted = new DateTimePicker { Value = DateTime.Now };

            // Posted By (read-only)
            lblPostedBy = new Label { Text = "Posted By", AutoSize = true };
            txtPostedBy = new TextBox { ReadOnly = true, BackColor = SystemColors.Control };

            // Description
            lblDescription = new Label { Text = "Description", AutoSize = true };
            txtDescription = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };

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

            void AddRow(Control label, Control control, bool multiline = false)
            {
                label.Anchor = AnchorStyles.Left;
                label.Margin = new Padding(0, 10, 10, 0);

                var host = new Panel { Dock = DockStyle.Fill, Height = multiline ? 110 : 34, Margin = new Padding(0, 8, 0, 0) };
                control.Dock = DockStyle.Fill;
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
            txtDescription.Height = 100;

            AddRow(lblItem, cmbItem);
            AddRow(lblEntryType, cmbEntryType);
            AddRow(lblQuantity, txtQuantity);
            AddRow(lblDatePosted, dtpDatePosted);
            AddRow(lblPostedBy, txtPostedBy);
            AddRow(lblDescription, txtDescription, multiline: true);

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
            cmbItem.Items.Add(new ItemItem { Id = null, Name = "-- Select Item --" });

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

                if (cmbItem.Items.Count > 0)
                    cmbItem.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load items: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadCurrentUser()
        {
            txtPostedBy.Text = $"{AppSession.CurrentUserName} (ID: {AppSession.CurrentUserId})";
        }

        /// <summary>
        /// CRITICAL: Auto-update EntryType based on selected ItemType and Quantity
        /// This ensures EntryType is always correctly determined, not manually selected
        /// </summary>
        private void UpdateEntryType(object sender, EventArgs e)
        {
            if (!(cmbItem.SelectedItem is ItemItem itemItem) || !itemItem.Id.HasValue)
            {
                return;
            }

            // Get quantity (default to 1 if not entered yet)
            int quantity = 1;
            if (!string.IsNullOrWhiteSpace(txtQuantity.Text))
            {
                int.TryParse(txtQuantity.Text, out quantity);
            }

            // Get inventory behavior from database
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    string query = "SELECT AffectsInventory FROM dbo.Item WHERE ItemId = @ItemId";
                    using (var cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", itemItem.Id.Value);
                        var result = cmd.ExecuteScalar();
                        bool affectsInventory = result != DBNull.Value && Convert.ToBoolean(result);

                        // CRITICAL: Use centralized method to determine EntryType
                        string entryType = affectsInventory
                            ? Helpers.InventoryHelper.DetermineEntryType(affectsInventory, quantity)
                            : "Fixed Assets";

                        // Update the combo box to display the auto-determined value
                        if (cmbEntryType.Items.Contains(entryType))
                        {
                            cmbEntryType.SelectedItem = entryType;
                        }
                    }
                }
            }
            catch
            {
                // Silently fail - validation will catch issues later
            }
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

            if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity <= 0)
            {
                MessageBox.Show("Please enter a valid Quantity (must be greater than 0).", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtQuantity.Focus();
                return;
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
                            // CRITICAL: Get inventory behavior to determine correct EntryType
                            bool affectsInventory = false;
                            string getItemInventoryBehaviorQuery = "SELECT AffectsInventory FROM dbo.Item WHERE ItemId = @ItemId";
                            using (var cmd = new SqlCommand(getItemInventoryBehaviorQuery, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", itemItem.Id.Value);
                                var result = cmd.ExecuteScalar();
                                affectsInventory = result != DBNull.Value && Convert.ToBoolean(result);
                            }

                            if (!affectsInventory)
                            {
                                MessageBox.Show(
                                    "The selected item does not affect inventory stock, so no inventory movement can be added.",
                                    "Validation",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                                transaction.Rollback();
                                return;
                            }

                            // CRITICAL: Use centralized method to determine EntryType
                            string entryType = Helpers.InventoryHelper.DetermineEntryType(affectsInventory, quantity);

                            // Insert inventory entry
                            string insertSql = @"
                                INSERT INTO dbo.Inventory
                                    (Description, EntryType, Quantity, DatePosted, PostedBy, ItemId)
                                VALUES
                                    (@Description, @EntryType, @Quantity, @DatePosted, @PostedBy, @ItemId)";

                            using (var cmd = new SqlCommand(insertSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Description",
                                    string.IsNullOrWhiteSpace(txtDescription.Text) ? (object)DBNull.Value : txtDescription.Text.Trim());
                                cmd.Parameters.AddWithValue("@EntryType", entryType);
                                cmd.Parameters.AddWithValue("@Quantity", quantity);
                                cmd.Parameters.AddWithValue("@DatePosted", dtpDatePosted.Value);
                                cmd.Parameters.AddWithValue("@PostedBy", AppSession.CurrentUserId);
                                cmd.Parameters.AddWithValue("@ItemId", itemItem.Id.Value);

                                cmd.ExecuteNonQuery();
                            }

                            // Update item stock based on entry type
                            string updateStockSql = @"
                                UPDATE dbo.Item
                                SET StockOnHand = StockOnHand + @Adjustment
                                WHERE ItemId = @ItemId";

                            using (var cmd = new SqlCommand(updateStockSql, con, transaction))
                            {
                                // Determine stock adjustment based on EntryType
                                int adjustment = entryType == "Positive" ? quantity :
                                               entryType == "Negative" ? -quantity :
                                               0; // Fixed Assets don't affect stock
                                cmd.Parameters.AddWithValue("@Adjustment", adjustment);
                                cmd.Parameters.AddWithValue("@ItemId", itemItem.Id.Value);

                                cmd.ExecuteNonQuery();
                            }

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
                MessageBox.Show($"Failed to save inventory entry: {ex.Message}", "Error",
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
