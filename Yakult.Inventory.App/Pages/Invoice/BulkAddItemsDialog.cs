using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Pages.Invoice
{
    // Entry point: ViewItemsPage "Bulk Add to Invoice" button. Takes a caller-selected
    // set of ItemIds, lets the user set Qty/Description per row, and returns the
    // finished line list -- the caller still has to resolve which Invoice (Set) to
    // attach them to (see PickOrCreateInvoiceDialog).
    internal sealed class BulkAddItemsDialog : Form
    {
        private readonly string _connectionString;
        private readonly List<int> _prefillItemIds;

        private DataGridView _grid;
        private Button _btnOk;
        private Button _btnCancel;
        private Label _lblSelectedCount;

        private List<InvoiceItemRow> _rows;

        public List<(int ItemId, int Quantity, string Description)> SelectedItems { get; private set; }

        public BulkAddItemsDialog(IEnumerable<int> prefillItemIds)
            : this(DatabaseConfig.ConnectionString, prefillItemIds)
        {
        }

        public BulkAddItemsDialog(string connectionString, IEnumerable<int> prefillItemIds)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string is required.", nameof(connectionString));

            _connectionString = connectionString;
            _prefillItemIds = (prefillItemIds ?? Enumerable.Empty<int>()).Distinct().ToList();

            BuildUi();
            Load += async (s, e) => await LoadItemsAsync();
        }

        private sealed class InvoiceItemRow
        {
            public bool Selected { get; set; } = true;
            public int ItemId { get; set; }
            public string ItemType { get; set; }
            public string ItemName { get; set; }
            public string ModelNumber { get; set; }
            public string SerialNumber { get; set; }
            public string UnitOfMeasure { get; set; }
            public decimal Amount { get; set; }
            public int Quantity { get; set; } = 1;
            public string Description { get; set; }
        }

        private void BuildUi()
        {
            Text = "Bulk Add Items to Invoice";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1000, 560);
            MinimizeBox = false;
            MaximizeBox = true;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(16)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var info = new Label
            {
                AutoSize = false,
                Height = 36,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Set the quantity/description for each item, then choose or create the Invoice on the next step.",
                BackColor = Color.FromArgb(230, 240, 255),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8, 0, 8, 0)
            };
            root.Controls.Add(info, 0, 0);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoGenerateColumns = false,
                EditMode = DataGridViewEditMode.EditOnEnter
            };
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Selected", HeaderText = "Add", Width = 50 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ItemType", HeaderText = "Type", Width = 110, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ItemName", HeaderText = "Item Name", Width = 220, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ModelNumber", HeaderText = "Model", Width = 110, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SerialNumber", HeaderText = "Serial", Width = 120, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UnitOfMeasure", HeaderText = "UOM", Width = 70, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Amount", HeaderText = "Unit Price", Width = 100, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Quantity", HeaderText = "Qty", Width = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Description", HeaderText = "Description", Width = 200 });

            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.CellValueChanged += (s, e) => UpdateSelectedCount();

            root.Controls.Add(_grid, 0, 1);

            var buttonBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(0, 12, 0, 0)
            };

            _btnOk = new Button { Text = "Next >", Width = 120, Height = 36 };
            _btnCancel = new Button { Text = "Cancel", Width = 100, Height = 36, DialogResult = DialogResult.Cancel };
            _btnOk.Click += (s, e) => OnOk();

            buttonBar.Controls.Add(_btnOk);
            buttonBar.Controls.Add(_btnCancel);

            _lblSelectedCount = new Label { AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 12, 0, 0), Text = "0 item(s) selected" };
            buttonBar.Controls.Add(_lblSelectedCount);

            root.Controls.Add(buttonBar, 0, 2);

            CancelButton = _btnCancel;
        }

        private async Task LoadItemsAsync()
        {
            try
            {
                if (_prefillItemIds.Count == 0)
                {
                    MessageBox.Show("No items were selected.", "Bulk Add Items to Invoice",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.Cancel;
                    Close();
                    return;
                }

                DatabaseConfig.EnsureConfigured();

                var table = new DataTable();
                using (var con = new SqlConnection(_connectionString))
                {
                    var idParams = _prefillItemIds.Select((id, idx) => $"@Id{idx}").ToList();
                    var sql = $@"
SELECT
    i.ItemId,
    i.ItemType,
    i.Name AS ItemName,
    i.ModelNumber,
    i.SerialNumber,
    i.UnitOfMeasure,
    i.Amount
FROM dbo.Item i
LEFT JOIN dbo.ArchiveStatus arch
    ON arch.EntityType = 'Item'
   AND arch.EntityId = i.ItemId
   AND arch.IsArchived = 1
WHERE i.ItemId IN ({string.Join(",", idParams)})
  AND i.Active = 1
  AND arch.EntityId IS NULL
  AND NOT EXISTS (
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
ORDER BY i.ItemType, i.Name, i.ModelNumber, i.SerialNumber;";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        for (int i = 0; i < _prefillItemIds.Count; i++)
                            cmd.Parameters.AddWithValue(idParams[i], _prefillItemIds[i]);

                        await con.OpenAsync();
                        using (var adapter = new SqlDataAdapter(cmd))
                            adapter.Fill(table);
                    }
                }

                _rows = table.Rows.Cast<DataRow>().Select(r => new InvoiceItemRow
                {
                    ItemId = Convert.ToInt32(r["ItemId"]),
                    ItemType = r["ItemType"] == DBNull.Value ? null : r["ItemType"].ToString(),
                    ItemName = r["ItemName"] == DBNull.Value ? null : r["ItemName"].ToString(),
                    ModelNumber = r["ModelNumber"] == DBNull.Value ? null : r["ModelNumber"].ToString(),
                    SerialNumber = r["SerialNumber"] == DBNull.Value ? null : r["SerialNumber"].ToString(),
                    UnitOfMeasure = r["UnitOfMeasure"] == DBNull.Value ? null : r["UnitOfMeasure"].ToString(),
                    Amount = r["Amount"] == DBNull.Value ? 0 : Convert.ToDecimal(r["Amount"]),
                    Description = r["ItemName"] == DBNull.Value ? null : r["ItemName"].ToString()
                }).ToList();

                int skippedCount = _prefillItemIds.Count - _rows.Count;

                _grid.DataSource = new BindingList<InvoiceItemRow>(_rows);
                UpdateSelectedCount();

                if (skippedCount > 0)
                {
                    MessageBox.Show(
                        $"{skippedCount} of the selected item(s) can't be added to an Invoice (only Services / Software-License items " +
                        "that aren't already part of another Set are eligible) and were skipped.",
                        "Bulk Add Items to Invoice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                if (_rows.Count == 0)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load selected items.\n\n{ex.Message}", "Bulk Add Items to Invoice",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private void UpdateSelectedCount()
        {
            var count = _rows?.Count(r => r.Selected) ?? 0;
            _lblSelectedCount.Text = $"{count} item(s) selected";
        }

        private void OnOk()
        {
            _grid.EndEdit();

            var selected = _rows?.Where(r => r.Selected).ToList() ?? new List<InvoiceItemRow>();
            if (selected.Count == 0)
            {
                MessageBox.Show("Please check at least one item to add.", "Bulk Add Items to Invoice",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var invalid = selected.Where(r => r.Quantity <= 0).ToList();
            if (invalid.Count > 0)
            {
                MessageBox.Show($"Quantity must be greater than 0 for: {string.Join(", ", invalid.Select(r => r.ItemName))}",
                    "Bulk Add Items to Invoice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedItems = selected
                .Select(r => (r.ItemId, r.Quantity, string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim()))
                .ToList();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
