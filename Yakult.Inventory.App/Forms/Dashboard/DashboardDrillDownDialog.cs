using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Forms.Dashboard
{
    /// <summary>
    /// Reusable modal dialog for displaying drill-down data
    /// Used by all dashboards for chart and summary card drill-down functionality
    /// </summary>
    public class DashboardDrillDownDialog : Form
    {
        private DataGridView _dataGridView;
        private Label _titleLabel;
        private Label _countLabel;
        private Button _closeButton;
        private Panel _headerPanel;

        public DashboardDrillDownDialog()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Form settings
            this.Text = "Summary Details";
            this.Size = new Size(1000, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(240, 242, 245);

            // Header panel
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.FromArgb(52, 152, 219),
                Padding = new Padding(20, 15, 20, 10)
            };

            // Title label
            _titleLabel = new Label
            {
                Text = "Summary Results",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            _headerPanel.Controls.Add(_titleLabel);

            // Count label
            _countLabel = new Label
            {
                Text = "0 records",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(230, 240, 255),
                AutoSize = true,
                Location = new Point(20, 45)
            };
            _headerPanel.Controls.Add(_countLabel);

            this.Controls.Add(_headerPanel);

            // DataGridView
            _dataGridView = new DataGridView
            {
                Location = new Point(20, 90),
                Size = new Size(940, 420),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 40,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                RowHeadersVisible = false,
                RowTemplate = { Height = 35 },
                GridColor = Color.FromArgb(220, 220, 220),
                EnableHeadersVisualStyles = false,
                ScrollBars = ScrollBars.Both
            };

            // DataGridView styling
            _dataGridView.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 73, 94);
            _dataGridView.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _dataGridView.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _dataGridView.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dataGridView.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            _dataGridView.DefaultCellStyle.SelectionBackColor = Color.FromArgb(200, 230, 255);
            _dataGridView.DefaultCellStyle.SelectionForeColor = Color.Black;
            _dataGridView.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);

            this.Controls.Add(_dataGridView);

            // Close button
            _closeButton = new Button
            {
                Text = "Close",
                Size = new Size(100, 35),
                Location = new Point(860, 520),
                BackColor = Color.FromArgb(52, 73, 94),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _closeButton.FlatAppearance.BorderSize = 0;
            _closeButton.Click += (s, e) => this.Close();

            this.Controls.Add(_closeButton);
        }

        /// <summary>
        /// Set the data to display in the dialog (DataTable approach - legacy)
        /// </summary>
        /// <param name="data">DataTable containing filtered results</param>
        /// <param name="title">Title to display in dialog header</param>
        public void SetData(DataTable data, string title)
        {
            _titleLabel.Text = title ?? "Summary Results";
            _countLabel.Text = $"{data.Rows.Count} record{(data.Rows.Count != 1 ? "s" : "")} found";

            // Bind data
            _dataGridView.DataSource = data;

            // Hide ID columns if present
            if (_dataGridView.Columns["ItemId"] != null)
            {
                _dataGridView.Columns["ItemId"].Visible = false;
            }
            if (_dataGridView.Columns["InvId"] != null)
            {
                _dataGridView.Columns["InvId"].Visible = false;
            }

            // Apply column-specific formatting
            foreach (DataGridViewColumn col in _dataGridView.Columns)
            {
                if (col.Name == "Stock On Hand" || col.Name == "Quantity")
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    col.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                }
                else if (col.Name == "Condition" || col.Name == "Entry Type")
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    col.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                }
                else if (col.Name == "Status")
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    col.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                }
                else if (col.Name == "Date Posted")
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
            }

            // Apply row-level formatting based on stock and condition
            foreach (DataGridViewRow row in _dataGridView.Rows)
            {
                if (row.Cells["Stock On Hand"] != null && row.Cells["Stock On Hand"].Value != null)
                {
                    int stock = Convert.ToInt32(row.Cells["Stock On Hand"].Value);
                    if (stock <= 0)
                    {
                        row.Cells["Stock On Hand"].Style.ForeColor = Color.FromArgb(231, 76, 60);
                    }
                    else if (stock < 5)
                    {
                        row.Cells["Stock On Hand"].Style.ForeColor = Color.FromArgb(241, 196, 15);
                    }
                    else
                    {
                        row.Cells["Stock On Hand"].Style.ForeColor = Color.FromArgb(46, 204, 113);
                    }
                }

                if (row.Cells["Condition"] != null && row.Cells["Condition"].Value != null)
                {
                    string condition = row.Cells["Condition"].Value.ToString();
                    if (condition == "Good")
                    {
                        row.Cells["Condition"].Style.ForeColor = Color.FromArgb(46, 204, 113);
                    }
                    else if (condition == "Damaged")
                    {
                        row.Cells["Condition"].Style.ForeColor = Color.FromArgb(231, 76, 60);
                    }
                }

                if (row.Cells["Status"] != null && row.Cells["Status"].Value != null)
                {
                    string status = row.Cells["Status"].Value.ToString();
                    if (status == "Active")
                    {
                        row.Cells["Status"].Style.ForeColor = Color.FromArgb(46, 204, 113);
                    }
                    else
                    {
                        row.Cells["Status"].Style.ForeColor = Color.FromArgb(231, 76, 60);
                    }
                }

                // Format Entry Type column for movement drill-downs
                if (row.Cells["Entry Type"] != null && row.Cells["Entry Type"].Value != null)
                {
                    string entryType = row.Cells["Entry Type"].Value.ToString();
                    if (string.Equals(entryType, "Positive", StringComparison.OrdinalIgnoreCase))
                    {
                        row.Cells["Entry Type"].Style.ForeColor = Color.FromArgb(46, 204, 113);
                    }
                    else if (string.Equals(entryType, "Negative", StringComparison.OrdinalIgnoreCase))
                    {
                        row.Cells["Entry Type"].Style.ForeColor = Color.FromArgb(231, 76, 60);
                    }
                    else
                    {
                        row.Cells["Entry Type"].Style.ForeColor = Color.FromArgb(52, 152, 219);
                    }
                }

                // Format Quantity column for movement drill-downs (color based on sign)
                if (row.Cells["Quantity"] != null && row.Cells["Quantity"].Value != null)
                {
                    int quantity = Convert.ToInt32(row.Cells["Quantity"].Value);
                    if (quantity > 0)
                    {
                        row.Cells["Quantity"].Style.ForeColor = Color.FromArgb(46, 204, 113);
                    }
                    else if (quantity < 0)
                    {
                        row.Cells["Quantity"].Style.ForeColor = Color.FromArgb(231, 76, 60);
                    }
                }
            }

            _dataGridView.ClearSelection();
        }

        /// <summary>
        /// Set the data to display in the dialog (DTO List approach - NEW)
        /// Dynamically generates columns based on DTO properties
        /// </summary>
        /// <param name="dtoList">List of DTOs (can be any type)</param>
        /// <param name="title">Title to display in dialog header</param>
        public void SetDataFromDto(object dtoList, string title)
        {
            _titleLabel.Text = title ?? "Summary Results";

            // Check if dtoList is IEnumerable
            if (!(dtoList is System.Collections.IEnumerable enumerable))
            {
                _countLabel.Text = "0 records found";
                return;
            }

            // Convert to list to get count
            var list = enumerable.Cast<object>().ToList();
            _countLabel.Text = $"{list.Count} record{(list.Count != 1 ? "s" : "")} found";

            if (list.Count == 0)
            {
                _dataGridView.DataSource = null;
                _dataGridView.Columns.Clear();
                return;
            }

            // CRITICAL: Use BindingList for proper DataGridView binding
            // This ensures the DataGridView can read properties via reflection
            var bindingListType = typeof(System.ComponentModel.BindingList<>).MakeGenericType(list[0].GetType());
            var bindingList = Activator.CreateInstance(bindingListType) as System.Collections.IList;

            foreach (var item in list)
            {
                bindingList.Add(item);
            }

            // Bind to DataGridView
            _dataGridView.DataSource = bindingList;

            // Auto-generate columns if not already done
            _dataGridView.AutoGenerateColumns = true;

            // Hide ID columns
            HideIdColumns();

            // Apply formatting
            ApplyDynamicFormatting();

            _dataGridView.ClearSelection();
        }

        /// <summary>
        /// Hide common ID columns that shouldn't be visible
        /// </summary>
        private void HideIdColumns()
        {
            var idColumns = new[] { "ItemId", "InvId", "ReqId", "EmpId", "SetId", "ComId", "BranchId", "DeptId", "VendorID", "Selected" };

            foreach (var colName in idColumns)
            {
                if (_dataGridView.Columns.Contains(colName))
                {
                    _dataGridView.Columns[colName].Visible = false;
                }
            }
        }

        /// <summary>
        /// Apply dynamic formatting based on column names
        /// </summary>
        private void ApplyDynamicFormatting()
        {
            foreach (DataGridViewColumn col in _dataGridView.Columns)
            {
                // Center and bold numeric/status columns
                if (col.Name.Contains("Stock") || col.Name.Contains("Quantity") ||
                    col.Name.Contains("Count") || col.Name == "StockOnHand")
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    col.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                }
                else if (col.Name.Contains("Status") || col.Name.Contains("Condition") ||
                         col.Name.Contains("EntryType") || col.Name == "Active")
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    col.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                }
                else if (col.Name.Contains("Date"))
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }

                // Set friendly column headers
                col.HeaderText = SplitCamelCase(col.Name);
            }

            // Apply row-level color coding
            ApplyRowColorCoding();
        }

        /// <summary>
        /// Convert camelCase/PascalCase to "Title Case With Spaces"
        /// </summary>
        private string SplitCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var result = System.Text.RegularExpressions.Regex.Replace(input, "([A-Z])", " $1").Trim();
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(result);
        }

        /// <summary>
        /// Apply color coding to rows based on data values
        /// </summary>
        private void ApplyRowColorCoding()
        {
            foreach (DataGridViewRow row in _dataGridView.Rows)
            {
                // Color code StockOnHand (Items dashboard)
                if (_dataGridView.Columns.Contains("StockOnHand") && row.Cells["StockOnHand"].Value != null)
                {
                    if (int.TryParse(row.Cells["StockOnHand"].Value.ToString(), out int stock))
                    {
                        if (stock <= 0)
                            row.Cells["StockOnHand"].Style.ForeColor = Color.FromArgb(231, 76, 60);
                        else if (stock < 5)
                            row.Cells["StockOnHand"].Style.ForeColor = Color.FromArgb(241, 196, 15);
                        else
                            row.Cells["StockOnHand"].Style.ForeColor = Color.FromArgb(46, 204, 113);
                    }
                }

                // Color code ConditionName (Items dashboard)
                if (_dataGridView.Columns.Contains("ConditionName") && row.Cells["ConditionName"].Value != null)
                {
                    string condition = row.Cells["ConditionName"].Value.ToString();
                    if (condition == "Good")
                        row.Cells["ConditionName"].Style.ForeColor = Color.FromArgb(46, 204, 113);
                    else if (condition == "Damaged")
                        row.Cells["ConditionName"].Style.ForeColor = Color.FromArgb(231, 76, 60);
                }

                // Color code Active status (Categories, Employees, Vendors dashboards)
                if (_dataGridView.Columns.Contains("Active") && row.Cells["Active"].Value != null)
                {
                    bool isActive = row.Cells["Active"].Value.ToString().ToLower() == "true" ||
                                   row.Cells["Active"].Value.ToString() == "Active";
                    row.Cells["Active"].Style.ForeColor = isActive ?
                        Color.FromArgb(46, 204, 113) : Color.FromArgb(231, 76, 60);
                }

                // Color code EntryType (Inventory Movements dashboard only)
                if (_dataGridView.Columns.Contains("EntryType") && row.Cells["EntryType"].Value != null)
                {
                    string entryType = row.Cells["EntryType"].Value.ToString();
                    if (entryType == "Positive")
                        row.Cells["EntryType"].Style.ForeColor = Color.FromArgb(46, 204, 113);
                    else if (entryType == "Negative")
                        row.Cells["EntryType"].Style.ForeColor = Color.FromArgb(231, 76, 60);
                    else
                        row.Cells["EntryType"].Style.ForeColor = Color.FromArgb(52, 152, 219);
                }

                // Color code Quantity (Inventory Movements dashboard only)
                if (_dataGridView.Columns.Contains("Quantity") && row.Cells["Quantity"].Value != null)
                {
                    if (int.TryParse(row.Cells["Quantity"].Value.ToString(), out int qty))
                    {
                        if (qty > 0)
                            row.Cells["Quantity"].Style.ForeColor = Color.FromArgb(46, 204, 113);
                        else if (qty < 0)
                            row.Cells["Quantity"].Style.ForeColor = Color.FromArgb(231, 76, 60);
                    }
                }
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // DashboardDrillDownDialog
            // 
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Name = "DashboardDrillDownDialog";
            this.Load += new System.EventHandler(this.DashboardDrillDownDialog_Load);
            this.ResumeLayout(false);

        }

        private void DashboardDrillDownDialog_Load(object sender, EventArgs e)
        {

        }
    }
}
