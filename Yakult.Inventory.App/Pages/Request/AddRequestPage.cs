﻿using Yakult.Inventory.App.Session;
using System;
using System.Windows.Forms;
using System.Drawing;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages.Employee;


namespace Yakult.Inventory.App.Pages.Request
{
    public partial class AddRequestPage : UserControl
    {
        private Panel headerPanel, bodyPanel, footerPanel;
        private TextBox txtQuantity, txtDescription, txtRemarks, txtCreatedBy;
        private DateTimePicker dtDateRequested, dtCreated;
        private ComboBox cmbEmployee, cmbCategory, cmbItem, cmbStatus;
        private Button btnSave, btnCancel, btnAddEmployee;
        private Label titleLabel;

        public event Action<RequestDto> Submitted;

        public AddRequestPage()
        {
            InitializeComponent();
            BuildUi();
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
                Text = "Add Request",
                Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
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

            // Body - Request fields (LEFT COLUMN)
            var lblEmployee = new Label { AutoSize = true, Text = "Employee *", Left = 0, Top = 8 };
            cmbEmployee = new ComboBox
            {
                Left = 0,
                Top = 28,
                Width = 230,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            btnAddEmployee = new Button
            {
                Text = "+ Add",
                Left = 240,
                Top = 28,
                Width = 60,
                Height = cmbEmployee.Height
            };
            btnAddEmployee.Click += BtnAddEmployee_Click;

            var lblCategory = new Label { AutoSize = true, Text = "Category *", Left = 0, Top = 60 };
            cmbCategory = new ComboBox
            {
                Left = 0,
                Top = 80,
                Width = 300,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbCategory.SelectedIndexChanged += CmbCategory_SelectedIndexChanged;

            var lblItem = new Label { AutoSize = true, Text = "Item *", Left = 0, Top = 112 };
            cmbItem = new ComboBox
            {
                Left = 0,
                Top = 132,
                Width = 300,
                DropDownStyle = ComboBoxStyle.DropDownList,
                DrawMode = DrawMode.OwnerDrawFixed
            };
            // Custom drawing to show grayed out items
            cmbItem.DrawItem += CmbItem_DrawItem;

            var lblQuantity = new Label { AutoSize = true, Text = "Quantity *", Left = 0, Top = 164 };
            txtQuantity = new TextBox { Left = 0, Top = 184, Width = 300 };

            var lblDateRequested = new Label { AutoSize = true, Text = "Date Requested *", Left = 0, Top = 216 };
            dtDateRequested = new DateTimePicker { Left = 0, Top = 236, Width = 300, Value = DateTime.Now };

            var lblDescription = new Label { AutoSize = true, Text = "Description", Left = 0, Top = 268 };
            txtDescription = new TextBox { Left = 0, Top = 288, Width = 300, Multiline = true, Height = 60 };

            var lblRemarks = new Label { AutoSize = true, Text = "Remarks", Left = 0, Top = 358 };
            txtRemarks = new TextBox { Left = 0, Top = 378, Width = 300, Multiline = true, Height = 60 };

            var lblStatus = new Label { AutoSize = true, Text = "Status *", Left = 0, Top = 448 };
            cmbStatus = new ComboBox
            {
                Left = 0,
                Top = 468,
                Width = 300,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbStatus.Items.AddRange(new object[] { "Under Review", "On Hold" });
            cmbStatus.SelectedIndex = 0; // Default to "Under Review"

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

            bodyPanel.Controls.AddRange(new Control[] {
                lblEmployee, cmbEmployee, btnAddEmployee, lblCategory, cmbCategory, lblItem, cmbItem, lblQuantity, txtQuantity,
                lblDateRequested, dtDateRequested, lblDescription, txtDescription,
                lblRemarks, txtRemarks, lblStatus, cmbStatus,
                lblCreated, dtCreated, lblBy, txtCreatedBy
            });

            // Load dropdown data
            LoadEmployees();
            LoadCategories();
            LoadItems();

            // Wire events
            btnSave.Click += (s, e) => SaveRequest();
            btnCancel.Click += (s, e) =>
            {
                var result = MessageBox.Show("Are you sure you want to cancel?", "Cancel",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    ClearForm();
                }
            };

            // Prevent selecting disabled items
            cmbItem.SelectionChangeCommitted += CmbItem_SelectionChangeCommitted;

            // Responsive layout
            bodyPanel.Resize += (s, e) =>
            {
                dtCreated.Left = bodyPanel.ClientSize.Width - dtCreated.Width - 12 - 20;
                txtCreatedBy.Left = bodyPanel.ClientSize.Width - txtCreatedBy.Width - 12 - 20;
                lblCreated.Left = dtCreated.Left;
                lblBy.Left = txtCreatedBy.Left;
            };
        }

        private void CmbItem_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var item = cmbItem.Items[e.Index] as ItemItem;
            if (item == null) return;

            e.DrawBackground();

            // Determine if item should be grayed out (disabled)
            bool isDisabled = item.Id.HasValue && item.StockOnHand == 0;
            Color textColor = isDisabled ? Color.Gray : e.ForeColor;

            using (var brush = new SolidBrush(textColor))
            {
                e.Graphics.DrawString(item.Name, e.Font, brush, e.Bounds);
            }

            e.DrawFocusRectangle();
        }

        private void CmbItem_SelectionChangeCommitted(object sender, EventArgs e)
        {
            // Prevent selection of items with 0 stock
            if (cmbItem.SelectedItem is ItemItem item && item.Id.HasValue && item.StockOnHand == 0)
            {
                MessageBox.Show("This item is out of stock and cannot be selected.", "Out of Stock",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbItem.SelectedIndex = 0; // Reset to "-- Select Item --"
            }
        }

        private void LoadEmployees()
        {
            cmbEmployee.Items.Clear();
            cmbEmployee.Items.Add(new EmployeeItem { Id = null, Name = "-- Select Employee --" });

            try
            {
                var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new System.Data.SqlClient.SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT EmpId, Name FROM dbo.Employee ORDER BY Name", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbEmployee.Items.Add(new EmployeeItem
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }

                if (cmbEmployee.Items.Count > 0)
                    cmbEmployee.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load employees: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadCategories()
        {
            cmbCategory.Items.Clear();
            cmbCategory.Items.Add(new CategoryItem { Id = null, Name = "-- All Categories --" });

            try
            {
                var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new System.Data.SqlClient.SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT CategoryId, Name FROM dbo.ItemCategory WHERE Active = 1 ORDER BY Name", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbCategory.Items.Add(new CategoryItem
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }

                if (cmbCategory.Items.Count > 0)
                    cmbCategory.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load categories: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CmbCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            // When category changes, reload items filtered by category
            LoadItems();
        }

        private void LoadItems()
        {
            cmbItem.Items.Clear();
            cmbItem.Items.Add(new ItemItem { Id = null, Name = "-- Select Item --", StockOnHand = 0 });

            try
            {
                var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs)) return;

                // Get selected category
                var selectedCategory = cmbCategory.SelectedItem as CategoryItem;
                bool filterByCategory = selectedCategory != null && selectedCategory.Id.HasValue;

                using (var con = new System.Data.SqlClient.SqlConnection(cs))
                {
                    con.Open();
                    
                    // Build SQL query with optional category filter
                    string sql = filterByCategory
                        ? "SELECT ItemId, Name, StockOnHand FROM dbo.Item WHERE Active = 1 AND CategoryId = @CategoryId ORDER BY Name"
                        : "SELECT ItemId, Name, StockOnHand FROM dbo.Item WHERE Active = 1 ORDER BY Name";

                    using (var cmd = new System.Data.SqlClient.SqlCommand(sql, con))
                    {
                        if (filterByCategory)
                        {
                            cmd.Parameters.AddWithValue("@CategoryId", selectedCategory.Id.Value);
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                cmbItem.Items.Add(new ItemItem
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    StockOnHand = reader.GetInt32(2)
                                });
                            }
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

        private void SaveRequest()
        {
            // Validation - Employee
            if (!(cmbEmployee.SelectedItem is EmployeeItem empItem) || !empItem.Id.HasValue)
            {
                MessageBox.Show("Please select an Employee.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbEmployee.Focus();
                return;
            }

            // Validation - Category
            if (!(cmbCategory.SelectedItem is CategoryItem catItem) || !catItem.Id.HasValue)
            {
                MessageBox.Show("Please select a Category.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbCategory.Focus();
                return;
            }

            // Validation - Item
            if (!(cmbItem.SelectedItem is ItemItem itemItem) || !itemItem.Id.HasValue)
            {
                MessageBox.Show("Please select an Item.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbItem.Focus();
                return;
            }

            // Validation - Quantity
            if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity <= 0)
            {
                MessageBox.Show("Please enter a valid Quantity (must be greater than 0).", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtQuantity.Focus();
                return;
            }
            if (quantity > 3)
            {
                MessageBox.Show("Maximum quantity per request is 3.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtQuantity.Focus();
                return;
            }

            // EntryType for requests is ALWAYS "Negative" because requests subtract from stock
            string entryType = "Negative";
            string status = cmbStatus.SelectedItem?.ToString() ?? "Under Review";

            // Check if stock is insufficient (for status/warning only)
            int remainingStock = itemItem.StockOnHand - quantity;
            if (remainingStock < 0)
            {
                var result = MessageBox.Show(
                    $"\u26A0\uFE0F INSUFFICIENT STOCK!\n\n" +
                    $"Current Stock: {itemItem.StockOnHand}\n" +
                    $"Requested: {quantity}\n" +
                    $"Shortage: {Math.Abs(remainingStock)}\n\n" +
                    $"This request will be placed ON HOLD.\n\n" +
                    $"Do you want to continue?",
                    "Insufficient Stock",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                {
                    return;
                }

                // Automatically set status to "On Hold"
                status = "On Hold";
            }

            var request = new RequestDto
            {
                EmpId = empItem.Id.Value,
                EmployeeName = empItem.Name,
                ItemId = itemItem.Id.Value,
                ItemName = itemItem.Name,
                Quantity = quantity,
                // Do not strip time — database requires full timestamp
                DateRequested = dtDateRequested.Value,
                Description = txtDescription.Text.Trim(),
                Remarks = txtRemarks.Text.Trim(),
                Status = status,
                EntryType = entryType, // "+1" or "-1"
                DateCreated = dtCreated.Value,
                CreatedByUserId = AppSession.CurrentUserId,
                CreatedByName = AppSession.CurrentUserName
            };

            Submitted?.Invoke(request);
        }

        private void ClearForm()
        {
            cmbEmployee.SelectedIndex = 0;
            cmbCategory.SelectedIndex = 0;
            cmbItem.SelectedIndex = 0;
            txtQuantity.Clear();
            dtDateRequested.Value = DateTime.Now;
            txtDescription.Clear();
            txtRemarks.Clear();
            cmbStatus.SelectedIndex = 0;
            dtCreated.Value = DateTime.Now;
        }

        // Helper classes for ComboBox items
        private class EmployeeItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private class CategoryItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private class ItemItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public int StockOnHand { get; set; }
            public override string ToString() => Name;
        }

        private void BtnAddEmployee_Click(object sender, EventArgs e)
        {
            using (var dialog = new QuickAddEmployeeDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    // Reload employees
                    LoadEmployees();

                    // Select the newly added employee
                    if (dialog.NewEmployeeId.HasValue)
                    {
                        for (int i = 0; i < cmbEmployee.Items.Count; i++)
                        {
                            if (cmbEmployee.Items[i] is EmployeeItem item && 
                                item.Id == dialog.NewEmployeeId.Value)
                            {
                                cmbEmployee.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}

