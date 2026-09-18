using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Item
{
    /// <summary>
    /// Bulk version of the Edit Item dialog: the same editable fields, but one row per selected
    /// item in a grid. Deliberately a WinForms DataGridView so it behaves exactly like the
    /// "Update Cellphone Details" grid the Item team already uses — arrow keys move between
    /// cells, F2/typing starts an edit, Tab moves across.
    ///
    /// The Cellphone columns (Cellphone Number / IMEI 1 / IMEI 2) are only editable on rows whose
    /// Category is CellPhone; on every other row they render read-only and greyed, matching the
    /// single-item dialog where that section is hidden entirely for non-cellphone categories.
    /// </summary>
    public class BulkEditItemsDialog : Form
    {
        private readonly ItemRepository _repo = new ItemRepository();
        private readonly List<int> _itemIds;

        private readonly DataGridView _grid;
        private readonly Label _status;
        private readonly Button _btnSave;
        private readonly Button _btnClose;

        private BindingList<ItemDto> _rows;

        /// <summary>True when Save completed, so the caller knows to reload.</summary>
        public bool SavedChanges { get; private set; }

        private static bool IsCellPhone(ItemDto row) =>
            !string.IsNullOrWhiteSpace(row?.Category) &&
            row.Category.Replace(" ", "").Equals("CellPhone", StringComparison.OrdinalIgnoreCase);

        public BulkEditItemsDialog(IEnumerable<int> itemIds)
        {
            _itemIds = (itemIds ?? Enumerable.Empty<int>()).Distinct().ToList();

            Text = "Bulk Edit Items";
            Size = new Size(1600, 850);
            MinimumSize = new Size(1000, 500);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9.5F);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
            };
            UiFactory.StyleGrid(_grid);
            BuildColumns();

            // Grey out and lock the cellphone columns on non-cellphone rows.
            _grid.CellFormatting += Grid_CellFormatting;
            _grid.CellBeginEdit += Grid_CellBeginEdit;

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(12, 10, 12, 10) };

            _btnClose = new Button
            {
                Text = "Close",
                Dock = DockStyle.Right,
                Width = 100,
                DialogResult = DialogResult.Cancel
            };
            _btnClose.Click += (s, e) => Close();

            _btnSave = new Button
            {
                Text = "Save Changes",
                Dock = DockStyle.Right,
                Width = 130,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnSave.Click += BtnSave_Click;

            _status = new Label
            {
                Dock = DockStyle.Left,
                AutoSize = false,
                Width = 700,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            bottom.Controls.Add(_status);
            bottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
            bottom.Controls.Add(_btnClose);
            bottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
            bottom.Controls.Add(_btnSave);

            Controls.Add(_grid);
            Controls.Add(bottom);

            Load += (s, e) => LoadRows();
        }

        // ── Columns ──────────────────────────────────────────────────────────
        // Mirrors the Edit Item dialog's field set. Read-only identity columns first, then the
        // editable ones grouped the same way the single-item dialog groups them.
        private void BuildColumns()
        {
            void Text(string name, string header, int width, bool readOnly = false)
            {
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = name,
                    HeaderText = header,
                    DataPropertyName = name,
                    Width = width,
                    ReadOnly = readOnly,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
            }

            // Identity — read-only context so the user knows which row they are on.
            Text("ItemId",        "ID",            60,  readOnly: true);
            Text("Category",      "Category",      130, readOnly: true);

            // ITEM INFORMATION
            Text("Name",          "Item Name",     200);
            Text("Description",   "Description",   240);
            Text("ItemType",      "Item Type",     120);
            Text("SubType",       "Sub-Type",      120);
            Text("SerialNumber",  "Serial Number", 140);
            Text("ModelNumber",   "Model Number",  140);
            Text("UnitOfMeasure", "UoM",           90);
            Text("StockOnHand",   "Stock",         70);
            Text("Amount",        "Amount",        100);

            // CELLPHONE — editable only when Category is CellPhone (see Grid_CellBeginEdit).
            Text("CellPhoneNumber", "Cellphone Number", 150);
            Text("IMEI1",           "IMEI 1",           150);
            Text("IMEI2",           "IMEI 2",           150);

            // CONDITION & VENDOR
            Text("ConditionName", "Condition",     130, readOnly: true);
            Text("VendorName",    "Vendor",        150, readOnly: true);
            Text("StartDate",     "Start Date",    110);
            Text("EndDate",       "End Date",      110);
            Text("Remarks",       "Remarks",       200);

            // LICENSE & WARRANTY
            Text("LicenseNumber",     "License Number", 150);
            Text("PartNumber",        "Part #",         130);
            Text("WarrantyYears",     "Warranty (Yrs)", 100);
            Text("WarrantyStartDate", "Warranty Start", 120);
            Text("WarrantyEndDate",   "Warranty End",   120);
            Text("DatePurchased",     "Date Purchased", 120);
            Text("AcquisitionType",   "Acquisition",    130);
        }

        private bool IsCellPhoneColumn(int columnIndex)
        {
            string n = _grid.Columns[columnIndex].Name;
            return n == "CellPhoneNumber" || n == "IMEI1" || n == "IMEI2";
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _rows == null || e.RowIndex >= _rows.Count) return;
            if (!IsCellPhoneColumn(e.ColumnIndex)) return;

            if (!IsCellPhone(_rows[e.RowIndex]))
            {
                e.CellStyle.BackColor = Color.FromArgb(245, 246, 248);
                e.CellStyle.ForeColor = Color.FromArgb(170, 178, 189);
            }
        }

        // Blocking the edit here (rather than setting ReadOnly per cell) keeps arrow-key movement
        // through those cells working — the user can still land on them, just not type.
        private void Grid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.RowIndex < 0 || _rows == null || e.RowIndex >= _rows.Count) return;
            if (IsCellPhoneColumn(e.ColumnIndex) && !IsCellPhone(_rows[e.RowIndex]))
                e.Cancel = true;
        }

        private void LoadRows()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var items = _repo.GetItemsForBulkEdit(_itemIds);
                _rows = new BindingList<ItemDto>(items);
                _grid.DataSource = _rows;

                int phones = items.Count(IsCellPhone);
                _status.Text = phones > 0
                    ? $"{items.Count} item(s) loaded — {phones} cellphone item(s) can take Cellphone/IMEI values."
                    : $"{items.Count} item(s) loaded — none are in the CellPhone category, so those columns are locked.";
            }
            catch (Exception ex)
            {
                Core.Logger.LogError("BulkEditItemsDialog: failed to load items", ex);
                MessageBox.Show(this, "Could not load the selected items.\n\n" + ex.Message,
                    "Bulk Edit Items", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (_rows == null || _rows.Count == 0) return;

            // Commit whatever cell is mid-edit so its value is included.
            _grid.EndEdit();
            if (_grid.BindingContext != null)
                _grid.BindingContext[_grid.DataSource].EndCurrentEdit();

            var blank = _rows.Where(r => string.IsNullOrWhiteSpace(r.Name)).ToList();
            if (blank.Count > 0)
            {
                MessageBox.Show(this,
                    $"{blank.Count} row(s) have an empty Item Name. Item Name is required.",
                    "Bulk Edit Items", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                _btnSave.Enabled = false;

                int affected = _repo.BulkUpdateItems(_rows.ToList(), AppSession.CurrentUserId);
                SavedChanges = true;

                MessageBox.Show(this, $"{affected} item(s) updated.",
                    "Bulk Edit Items", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                Core.Logger.LogError("BulkEditItemsDialog: save failed", ex);
                MessageBox.Show(this,
                    "Could not save the changes. No item was modified.\n\n" + ex.Message,
                    "Bulk Edit Items", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnSave.Enabled = true;
                Cursor = Cursors.Default;
            }
        }
    }
}
