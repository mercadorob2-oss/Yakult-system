using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Pages.Invoice
{
    internal sealed class AddInvoiceItemDialog : Form
    {
        private readonly string _connectionString;
        private readonly int _setId;

        private TextBox _txtSearch;
        private ComboBox _cmbCategory;
        private DataGridView _grid;
        private NumericUpDown _numQty;
        private TextBox _txtDescription;
        private Button _btnOk;
        private Button _btnCancel;

        private DataTable _itemsTable;
        private DataView _itemsView;

        public int SelectedItemId { get; private set; }
        public int Quantity { get; private set; } = 1;
        public string LineDescription { get; private set; }

        public AddInvoiceItemDialog(string connectionString, int setId)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string is required.", nameof(connectionString));
            if (setId <= 0)
                throw new ArgumentOutOfRangeException(nameof(setId));

            _connectionString = connectionString;
            _setId = setId;

            BuildUi();
            Load += async (s, e) => await LoadItemsAsync();
        }

        private void BuildUi()
        {
            Text = "Add Invoice Item";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(900, 560);
            MinimizeBox = false;
            MaximizeBox = true;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(16)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var info = new Label
            {
                AutoSize = false,
                Height = 36,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Select an item to add. Only Item Type = Services or Software/License is allowed.",
                BackColor = Color.FromArgb(230, 240, 255),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8, 0, 8, 0)
            };
            root.Controls.Add(info, 0, 0);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoGenerateColumns = false
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ItemType", HeaderText = "Type", Width = 120 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ItemName", HeaderText = "Item Name", Width = 260 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ModelNumber", HeaderText = "Model", Width = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SerialNumber", HeaderText = "Serial", Width = 160 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UnitOfMeasure", HeaderText = "UOM", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Amount", HeaderText = "Unit Price", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _grid.SelectionChanged += (s, e) => OnSelectionChanged();

            root.Controls.Add(_grid, 0, 1);

            var bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 8,
                AutoSize = true
            };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

            bottom.Controls.Add(new Label { Text = "Search:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 6, 8, 0) }, 0, 0);
            _txtSearch = new TextBox { Dock = DockStyle.Top };
            _txtSearch.TextChanged += (s, e) => ApplyFilter();
            bottom.Controls.Add(_txtSearch, 1, 0);

            bottom.Controls.Add(new Label { Text = "Category:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 6, 8, 0) }, 2, 0);
            _cmbCategory = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbCategory.SelectedIndexChanged += (s, e) => ApplyFilter();
            bottom.Controls.Add(_cmbCategory, 3, 0);

            bottom.Controls.Add(new Label { Text = "Qty:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 6, 8, 0) }, 4, 0);
            _numQty = new NumericUpDown { Minimum = 1, Maximum = 999999, Value = 1, Width = 100 };
            bottom.Controls.Add(_numQty, 5, 0);

            bottom.Controls.Add(new Label { Text = "Description:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 6, 8, 0) }, 0, 1);
            bottom.SetColumnSpan(bottom.GetControlFromPosition(0, 1), 1);
            _txtDescription = new TextBox { Dock = DockStyle.Top };
            bottom.Controls.Add(_txtDescription, 1, 1);
            bottom.SetColumnSpan(_txtDescription, 7);

            root.Controls.Add(bottom, 0, 2);

            var buttonBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(0, 12, 0, 0)
            };

            _btnOk = new Button { Text = "Add", Width = 100, Height = 36, Enabled = false };
            _btnCancel = new Button { Text = "Cancel", Width = 100, Height = 36, DialogResult = DialogResult.Cancel };
            _btnOk.Click += (s, e) => OnOk();

            buttonBar.Controls.Add(_btnOk);
            buttonBar.Controls.Add(_btnCancel);
            root.Controls.Add(buttonBar, 0, 3);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        private async Task LoadItemsAsync()
        {
            try
            {
                DatabaseConfig.EnsureConfigured();

                using (var con = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(@"
SELECT
    i.ItemId,
    i.ItemType,
    i.Name AS ItemName,
    i.ModelNumber,
    i.SerialNumber,
    i.UnitOfMeasure,
    i.Category,
    i.Amount
FROM dbo.Item i
LEFT JOIN dbo.ArchiveStatus arch
    ON arch.EntityType = 'Item'
   AND arch.EntityId = i.ItemId
   AND arch.IsArchived = 1
WHERE i.Active = 1
  AND arch.EntityId IS NULL
  AND NOT EXISTS (
      SELECT 1 FROM dbo.SetItem si
      WHERE si.SetId = @SetId
        AND si.ItemId = i.ItemId
  )
  AND NOT EXISTS (
      -- Exclude items already grouped in ANY non-archived Set (including Invoice sets)
      SELECT 1
      FROM dbo.SetItem siAny
      INNER JOIN dbo.[Set] sAny ON sAny.SetId = siAny.SetId
      LEFT JOIN dbo.ArchiveStatus archSet
          ON archSet.EntityType = 'Set'
         AND archSet.EntityId = sAny.SetId
         AND archSet.IsArchived = 1
      WHERE siAny.ItemId = i.ItemId
        AND archSet.EntityId IS NULL
  )
ORDER BY i.ItemType, i.Name, i.ModelNumber, i.SerialNumber;", con))
                {
                    cmd.Parameters.AddWithValue("@SetId", _setId);
                    await con.OpenAsync();
                    using (var adapter = new SqlDataAdapter(cmd))
                    {
                        _itemsTable = new DataTable();
                        adapter.Fill(_itemsTable);
                    }
                }

                _itemsView = new DataView(_itemsTable);
                _grid.DataSource = _itemsView;

                LoadCategoryOptions();
                ApplyFilter();
                if (_grid.Rows.Count > 0)
                {
                    _grid.ClearSelection();
                    _grid.Rows[0].Selected = true;
                }
                OnSelectionChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load eligible items.\n\n{ex.Message}", "Add Invoice Item",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private void ApplyFilter()
        {
            if (_itemsView == null)
                return;

            var q = (_txtSearch?.Text ?? string.Empty).Trim();
            var category = _cmbCategory?.SelectedItem as string;
            bool hasCategory = !string.IsNullOrWhiteSpace(category) && !string.Equals(category, "(All)", StringComparison.OrdinalIgnoreCase);

            // DataView.RowFilter uses SQL-like expressions; escape single quotes.
            var filters = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qEscaped = q.Replace("'", "''");
                filters.Add($"(ItemName LIKE '%{qEscaped}%' OR ModelNumber LIKE '%{qEscaped}%' OR SerialNumber LIKE '%{qEscaped}%')");
            }

            if (hasCategory)
            {
                var cEscaped = category.Replace("'", "''");
                filters.Add($"Category = '{cEscaped}'");
            }

            _itemsView.RowFilter = filters.Count == 0 ? string.Empty : string.Join(" AND ", filters);
        }

        private void LoadCategoryOptions()
        {
            if (_cmbCategory == null || _itemsTable == null)
                return;

            _cmbCategory.BeginUpdate();
            try
            {
                _cmbCategory.Items.Clear();
                _cmbCategory.Items.Add("(All)");

                if (_itemsTable.Columns.Contains("Category"))
                {
                    var categories = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (DataRow r in _itemsTable.Rows)
                    {
                        if (r == null) continue;
                        var c = r["Category"] == DBNull.Value ? null : r["Category"].ToString();
                        if (string.IsNullOrWhiteSpace(c)) continue;
                        categories.Add(c.Trim());
                    }

                    foreach (var c in categories.OrderBy(x => x))
                        _cmbCategory.Items.Add(c);
                }

                _cmbCategory.SelectedIndex = 0;
            }
            finally
            {
                _cmbCategory.EndUpdate();
            }
        }

        private void OnSelectionChanged()
        {
            var row = GetSelectedRow();
            if (row == null)
            {
                _btnOk.Enabled = false;
                return;
            }

            _btnOk.Enabled = true;

            if (string.IsNullOrWhiteSpace(_txtDescription.Text))
            {
                var name = row["ItemName"] == DBNull.Value ? string.Empty : row["ItemName"].ToString();
                _txtDescription.Text = name;
            }
        }

        private DataRowView GetSelectedRow()
        {
            if (_grid?.CurrentRow?.DataBoundItem is DataRowView drv)
                return drv;
            if (_grid?.SelectedRows != null && _grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].DataBoundItem is DataRowView drv2)
                return drv2;
            return null;
        }

        private void OnOk()
        {
            var row = GetSelectedRow();
            if (row == null)
            {
                MessageBox.Show("Please select an item first.", "Add Invoice Item",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (row["ItemId"] == DBNull.Value)
            {
                MessageBox.Show("Selected item is invalid.", "Add Invoice Item",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedItemId = Convert.ToInt32(row["ItemId"]);
            Quantity = (int)_numQty.Value;
            LineDescription = string.IsNullOrWhiteSpace(_txtDescription.Text) ? null : _txtDescription.Text.Trim();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
