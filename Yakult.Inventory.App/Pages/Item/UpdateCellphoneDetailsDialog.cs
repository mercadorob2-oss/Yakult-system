using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Item
{
    // Editable grid for the CellPhone category, opened from the "Update Cellphone Details"
    // action button on ViewItemsPage. Mirrors the SSMS "Edit Top 200 Rows" workflow the
    // Item team was previously doing by hand in SSMS.
    public class UpdateCellphoneDetailsDialog : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int MK_LBUTTON = 0x0001;

        private readonly DataGridView _grid;
        private readonly Button _btnSave;
        private readonly Button _btnCancel;
        private readonly Button _btnExclude;
        private readonly Button _btnInclude;
        private readonly Button _btnBulkPaste;
        private readonly Label _lblStatus;
        private readonly ItemRepository _repo = new ItemRepository();
        private BindingList<ItemDto> _rows;
        // Items removed via "Exclude" are kept here (not deleted) so "Include" can restore them.
        private readonly List<ItemDto> _excludedItems = new List<ItemDto>();
        private bool _headerChecked;

        // Tracks a potential drag-to-select starting inside the active cell's editing TextBox —
        // a plain click there should still place the text caret like a normal TextBox.
        private bool _editorMouseDown;
        private Point _editorMouseDownPoint;

        // Max lengths mirror dbo.Item's column definitions so paste/typed values can't
        // silently exceed what SQL will accept (Name is also NOT NULL there).
        private static readonly Dictionary<string, int> _maxLengths = new Dictionary<string, int>
        {
            ["Name"] = 200,
            ["Description"] = 400,
            ["ModelNumber"] = 500,
            ["SerialNumber"] = 255,
            ["CellPhoneNumber"] = 50,
            ["IMEI1"] = 50,
            ["IMEI2"] = 50,
        };

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MINIMIZE = 0xF020;

        // This dialog is shown modally with the main window as its Owner, which disables the
        // owner for the duration. Windows' default SC_MINIMIZE handling for an owned window
        // whose owner is disabled cascades the minimize up to the owner too — so clicking
        // Minimize on this dialog was minimizing the entire app instead of just this window.
        // Handling it ourselves (and never calling base for this message) keeps it scoped to
        // this dialog only.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SYSCOMMAND && (m.WParam.ToInt32() & 0xFFF0) == SC_MINIMIZE)
            {
                WindowState = FormWindowState.Minimized;
                return;
            }

            base.WndProc(ref m);
        }

        public UpdateCellphoneDetailsDialog()
        {
            Text = "Update Cellphone Details";
            Size = new Size(1500, 850);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1000, 500);
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

            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "Selected",
                HeaderText = "",
                DataPropertyName = "Selected",
                Width = 40,
                Resizable = DataGridViewTriState.False,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            });
            // Checkbox columns need the edit committed on the same click, otherwise the check
            // state doesn't visually update until the cell loses focus.
            _grid.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "Selected")
                {
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ItemId",
                HeaderText = "Item ID",
                DataPropertyName = "ItemId",
                ReadOnly = true,
                Width = 80,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            });
            // Remaining columns fill whatever width is left in the dialog instead of leaving
            // blank space to the right — FillWeight mirrors the original fixed-width ratios.
            // Everything except the checkbox and ItemId is editable, including from an Excel paste.
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Name", DataPropertyName = "Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 220 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description", DataPropertyName = "Description", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 280 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ModelNumber", HeaderText = "Model Number", DataPropertyName = "ModelNumber", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SerialNumber", HeaderText = "Serial Number", DataPropertyName = "SerialNumber", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CellPhoneNumber", HeaderText = "Cellphone Number", DataPropertyName = "CellPhoneNumber", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 160 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "IMEI1", HeaderText = "IMEI 1", DataPropertyName = "IMEI1", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 160 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "IMEI2", HeaderText = "IMEI 2", DataPropertyName = "IMEI2", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 160 });

            // Tint every editable column so it's visually obvious which fields can be changed.
            foreach (var columnName in _maxLengths.Keys)
                _grid.Columns[columnName].DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 235);

            SetupSelectAllHeaderCheckbox();
            SetupValidation();
            SetupExcelPaste();

            var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(16, 12, 16, 12) };

            _lblStatus = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.Colors.TextMuted,
                Padding = new Padding(0, 8, 0, 0)
            };

            _btnCancel = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Height = 32,
                Dock = DockStyle.Right
            };

            _btnSave = new Button
            {
                Text = "Save Changes",
                Width = 130,
                Height = 32,
                Dock = DockStyle.Right,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnSave.Click += BtnSave_Click;

            _btnExclude = new Button
            {
                Text = "Exclude Checked",
                Width = 140,
                Height = 32,
                Dock = DockStyle.Left,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnExclude.Click += BtnExclude_Click;

            _btnInclude = new Button
            {
                Text = "Include Excluded",
                Width = 140,
                Height = 32,
                Dock = DockStyle.Left,
                Margin = new Padding(0, 0, 8, 0),
                Enabled = false
            };
            _btnInclude.Click += BtnInclude_Click;

            _btnBulkPaste = new Button
            {
                Text = "Bulk Paste IMEI + Phone",
                Width = 175,
                Height = 32,
                Dock = DockStyle.Left,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnBulkPaste.Click += BtnBulkPaste_Click;

            buttonPanel.Controls.Add(_lblStatus);
            buttonPanel.Controls.Add(_btnSave);
            buttonPanel.Controls.Add(_btnCancel);
            buttonPanel.Controls.Add(_btnExclude);
            buttonPanel.Controls.Add(_btnInclude);
            buttonPanel.Controls.Add(_btnBulkPaste);

            Controls.Add(_grid);
            Controls.Add(buttonPanel);
            CancelButton = _btnCancel;

            Load += (s, e) => LoadRows();
        }

        // DataGridView has no built-in header checkbox, so the "select all" toggle is hand-painted
        // onto the Selected column's header cell and driven off a click on that header.
        private void SetupSelectAllHeaderCheckbox()
        {
            _grid.CellPainting += (s, e) =>
            {
                if (e.RowIndex != -1 || _grid.Columns[e.ColumnIndex].Name != "Selected")
                    return;

                e.PaintBackground(e.ClipBounds, true);
                var checkBoxState = _headerChecked
                    ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                    : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
                var glyphSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, checkBoxState);
                var glyphLocation = new Point(
                    e.CellBounds.Left + (e.CellBounds.Width - glyphSize.Width) / 2,
                    e.CellBounds.Top + (e.CellBounds.Height - glyphSize.Height) / 2);
                CheckBoxRenderer.DrawCheckBox(e.Graphics, glyphLocation, checkBoxState);
                e.Handled = true;
            };

            _grid.CellMouseClick += (s, e) =>
            {
                if (e.RowIndex != -1 || _grid.Columns[e.ColumnIndex].Name != "Selected" || _rows == null)
                    return;

                _headerChecked = !_headerChecked;
                foreach (var item in _rows)
                    item.Selected = _headerChecked;

                _grid.Refresh();
            };
        }

        // Flags an invalid keyed-in value with an inline error icon, but never blocks leaving
        // the cell — getting trapped in edit mode by a validation failure would violate the
        // "never get stuck" spreadsheet requirement. Hard enforcement happens at Save time.
        private void SetupValidation()
        {
            _grid.CellValidating += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

                var column = _grid.Columns[e.ColumnIndex];
                if (!_maxLengths.ContainsKey(column.Name)) return;

                var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                cell.ErrorText = TryValidateValue(column.Name, e.FormattedValue?.ToString(), out string error)
                    ? string.Empty
                    : error;
            };
        }

        private static bool TryValidateValue(string columnName, string value, out string error)
        {
            error = null;

            if (columnName == "Name" && string.IsNullOrWhiteSpace(value))
            {
                error = "Name is required and cannot be blank.";
                return false;
            }

            if (_maxLengths.TryGetValue(columnName, out int maxLen) && value != null && value.Length > maxLen)
            {
                error = $"{columnName} cannot exceed {maxLen} characters.";
                return false;
            }

            return true;
        }

        // Enables copying a rectangular range of cells from Excel and pasting it starting at
        // whichever cell is active, exactly like pasting a range within Excel itself. Also makes
        // mouse drag-selection work even while a cell's TextBox editor has focus, and lets the
        // editor's own default Ctrl+V (which would otherwise paste raw text at the caret) get
        // pre-empted by our range-aware paste.
        private void SetupExcelPaste()
        {
            var pasteMenu = new ContextMenuStrip();
            var pasteItem = new ToolStripMenuItem("Paste", null, (s, e) => PasteFromClipboard()) { ShortcutKeyDisplayString = "Ctrl+V" };
            pasteMenu.Items.Add(pasteItem);
            _grid.ContextMenuStrip = pasteMenu;

            // Right-click doesn't move CurrentCell on its own, so the paste would land on
            // whatever cell was last active instead of the one under the cursor.
            _grid.CellMouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    _grid.EndEdit();
                    _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                }
            };

            _grid.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.V)
                {
                    PasteFromClipboard();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Delete && !e.Control && !e.Alt)
                {
                    // Only fires when the grid itself (not an in-progress cell editor) has
                    // focus, so this can't be confused with Delete-forward-a-character while
                    // typing — that's handled natively by the editing TextBox instead.
                    DeleteSelectedCells();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            // The cell editor is a separate child TextBox control — Ctrl+V and mouse drags
            // that start on it never reach the DataGridView's own handlers above, so both
            // have to be intercepted directly on the editing control as well.
            _grid.EditingControlShowing += (s, e) =>
            {
                if (!(e.Control is TextBox tb)) return;

                tb.KeyDown -= EditingControl_KeyDown;
                tb.KeyDown += EditingControl_KeyDown;
                tb.MouseDown -= EditingControl_MouseDown;
                tb.MouseDown += EditingControl_MouseDown;
                tb.MouseMove -= EditingControl_MouseMove;
                tb.MouseMove += EditingControl_MouseMove;
                tb.MouseUp -= EditingControl_MouseUp;
                tb.MouseUp += EditingControl_MouseUp;
            };
        }

        // A TextBox's built-in Ctrl+V would paste the raw clipboard text at the caret instead
        // of running our range-aware paste — pre-empt it before it reaches the native control.
        private void EditingControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Control || e.KeyCode != Keys.V) return;

            e.Handled = true;
            e.SuppressKeyPress = true;
            PasteFromClipboard();
        }

        private void EditingControl_MouseDown(object sender, MouseEventArgs e)
        {
            _editorMouseDown = e.Button == MouseButtons.Left;
            _editorMouseDownPoint = e.Location;
        }

        private void EditingControl_MouseUp(object sender, MouseEventArgs e)
        {
            _editorMouseDown = false;
        }

        // A plain click inside the active cell's TextBox should still place the caret like any
        // TextBox. Only once the mouse moves far enough to look like an intentional drag do we
        // hand the gesture off to the grid so it can drive its own cell-range selection instead
        // of the TextBox selecting its own text.
        private void EditingControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_editorMouseDown || e.Button != MouseButtons.Left) return;

            int dx = Math.Abs(e.X - _editorMouseDownPoint.X);
            int dy = Math.Abs(e.Y - _editorMouseDownPoint.Y);
            if (dx < SystemInformation.DragSize.Width / 2 && dy < SystemInformation.DragSize.Height / 2)
                return;

            _editorMouseDown = false; // this press has been handed off — don't forward it again

            var control = (Control)sender;
            var screenPoint = control.PointToScreen(_editorMouseDownPoint);
            var gridPoint = _grid.PointToClient(screenPoint);

            // Standard "hand a drag off to the parent" trick: release the TextBox's implicit
            // mouse capture, then replay the button-down on the grid at the same screen
            // position. The grid's own drag-select logic takes it from there — the physical
            // button is still held, so Windows routes the rest of the drag to whichever
            // control now holds capture (the grid, once it processes this message).
            ReleaseCapture();
            SendMessage(_grid.Handle, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON,
                (IntPtr)((gridPoint.Y << 16) | (gridPoint.X & 0xFFFF)));
        }

        private void PasteFromClipboard()
        {
            if (_rows == null || !Clipboard.ContainsText())
                return;

            string text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text)) return;

            // Excel's clipboard format is a rectangular tab-delimited matrix: tabs separate
            // columns, CRLF separates rows, with a trailing row terminator — drop the resulting
            // empty last line before mapping.
            var lines = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

            // Exit edit mode BEFORE reading the anchor cell — the requirement is that paste
            // always maps from the selected DataGrid cell, never from wherever the text caret
            // happened to be inside an in-progress edit.
            _grid.EndEdit();

            // When a rectangular range was drag-selected to mark the paste destination, Excel
            // (and the user's intent here) anchors on the TOP-LEFT of that highlighted block.
            // DataGridView's CurrentCell, however, is wherever the drag ENDED — the last cell
            // highlighted, not the first — so anchoring on CurrentCell pastes from the wrong
            // corner of a multi-cell selection. Use the actual top-left of SelectedCells instead,
            // falling back to CurrentCell for a plain single-cell click.
            int startRow;
            int anchorDisplayIndex;
            if (_grid.SelectedCells.Count > 0)
            {
                var selected = _grid.SelectedCells.Cast<DataGridViewCell>().ToList();
                startRow = selected.Min(cell => cell.RowIndex);
                anchorDisplayIndex = selected.Min(cell => _grid.Columns[cell.ColumnIndex].DisplayIndex);
            }
            else if (_grid.CurrentCell != null)
            {
                startRow = _grid.CurrentCell.RowIndex;
                anchorDisplayIndex = _grid.Columns[_grid.CurrentCell.ColumnIndex].DisplayIndex;
            }
            else
            {
                return;
            }

            // Map clipboard columns onto consecutive EDITABLE grid columns only — ItemId and the
            // Selected checkbox are never destinations and never consume a slot in that sequence,
            // regardless of where they happen to sit relative to the anchor. Ordered by DisplayIndex
            // (visual left-to-right order) so this stays correct even if columns are ever reordered.
            var editableColumns = _grid.Columns.Cast<DataGridViewColumn>()
                .Where(col => _maxLengths.ContainsKey(col.Name))
                .OrderBy(col => col.DisplayIndex)
                .ToList();

            // If the anchor itself is a non-editable column (e.g. ItemId), start from the next
            // editable column to its right instead of silently doing nothing.
            int startEditableIndex = editableColumns.FindIndex(col => col.DisplayIndex >= anchorDisplayIndex);
            if (startEditableIndex < 0) return; // anchor is right of every editable column

            int pastedCells = 0;
            var errors = new List<string>();

            for (int r = 0; r < lines.Length; r++)
            {
                int targetRow = startRow + r;
                if (targetRow >= _grid.Rows.Count) break; // exceeds available rows — ignore the rest

                var values = lines[r].Split('\t');
                for (int c = 0; c < values.Length; c++)
                {
                    int editableIndex = startEditableIndex + c;
                    if (editableIndex >= editableColumns.Count) break; // exceeds available editable columns — ignore the rest

                    var column = editableColumns[editableIndex];
                    var value = values[c].Trim();
                    if (!TryValidateValue(column.Name, value, out string error))
                    {
                        errors.Add($"Row {targetRow + 1}, {column.HeaderText}: {error}");
                        continue;
                    }

                    _grid.Rows[targetRow].Cells[column.Index].Value = value;
                    pastedCells++;
                }
            }

            _lblStatus.Text = $"Pasted {pastedCells} cell(s) from clipboard.";

            if (errors.Count > 0)
            {
                var message = new StringBuilder("Some pasted values were skipped because they failed validation:\n\n");
                foreach (var error in errors.Take(20))
                    message.AppendLine(error);
                if (errors.Count > 20)
                    message.AppendLine($"...and {errors.Count - 20} more.");

                MessageBox.Show(message.ToString(), "Paste Validation Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Excel's Delete key clears the contents of every selected cell (not just the active
        // one) without needing to enter edit mode first — this mirrors that for a highlighted
        // multi-cell range.
        private void DeleteSelectedCells()
        {
            if (_rows == null || _grid.SelectedCells.Count == 0) return;

            _grid.EndEdit();

            int clearedCells = 0;
            foreach (DataGridViewCell cell in _grid.SelectedCells)
            {
                var column = _grid.Columns[cell.ColumnIndex];
                if (!_maxLengths.ContainsKey(column.Name))
                    continue; // ItemId / Selected checkbox aren't clearable

                cell.Value = string.Empty;
                clearedCells++;
            }

            if (clearedCells > 0)
                _lblStatus.Text = $"Cleared {clearedCells} cell(s).";
        }

        private void LoadRows()
        {
            try
            {
                var items = _repo.GetCellPhoneItems();
                _rows = new BindingList<ItemDto>(items);
                _grid.DataSource = _rows;
                _excludedItems.Clear();
                _btnInclude.Enabled = false;
                _headerChecked = false;
                _lblStatus.Text = $"{_rows.Count} cellphone item(s) loaded.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load cellphone items: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Excluding an item only removes it from THIS grid/save batch — it does not delete or
        // modify the underlying dbo.Item row. "Include" brings it back for the current session.
        private void BtnExclude_Click(object sender, EventArgs e)
        {
            if (_rows == null || _rows.Count == 0)
                return;

            _grid.EndEdit();

            var toExclude = new List<ItemDto>();
            foreach (var item in _rows)
            {
                if (item.Selected)
                    toExclude.Add(item);
            }

            if (toExclude.Count == 0)
            {
                MessageBox.Show("Check at least one row to exclude.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var item in toExclude)
            {
                item.Selected = false;
                _rows.Remove(item);
                _excludedItems.Add(item);
            }

            _btnInclude.Enabled = _excludedItems.Count > 0;
            _lblStatus.Text = $"{_rows.Count} item(s) shown, {_excludedItems.Count} excluded.";
        }

        private void BtnInclude_Click(object sender, EventArgs e)
        {
            if (_excludedItems.Count == 0)
                return;

            using (var dlg = new IncludeExcludedCellphonesDialog(_excludedItems))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                foreach (var item in dlg.IncludedItems)
                {
                    _excludedItems.Remove(item);
                    _rows.Add(item);
                }
            }

            _btnInclude.Enabled = _excludedItems.Count > 0;
            _lblStatus.Text = $"{_rows.Count} item(s) shown, {_excludedItems.Count} excluded.";
        }

        private static string GetColumnValue(ItemDto item, string columnName)
        {
            switch (columnName)
            {
                case "Name": return item.Name;
                case "Description": return item.Description;
                case "ModelNumber": return item.ModelNumber;
                case "SerialNumber": return item.SerialNumber;
                case "CellPhoneNumber": return item.CellPhoneNumber;
                case "IMEI1": return item.IMEI1;
                case "IMEI2": return item.IMEI2;
                default: return null;
            }
        }

        private void BtnBulkPaste_Click(object sender, EventArgs e)
        {
            if (_rows == null || _rows.Count == 0)
            {
                MessageBox.Show("No active cellphone items are loaded.", "Bulk Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _grid.EndEdit();
            using (var dlg = new BulkCellphoneNumberUpdateDialog())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                foreach (var update in dlg.AppliedUpdates)
                {
                    var item = _rows.FirstOrDefault(i => string.Equals(
                        i.IMEI1 == null ? null : i.IMEI1.Trim(),
                        update.IMEI1,
                        StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                        item.CellPhoneNumber = update.CellPhoneNumber;
                }

                _grid.Refresh();
                _lblStatus.Text = $"Bulk updated {dlg.AppliedUpdates.Count} cellphone number(s).";
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (_rows == null || _rows.Count == 0)
                return;

            _grid.EndEdit();

            var validationErrors = new List<string>();
            for (int i = 0; i < _rows.Count; i++)
            {
                var item = _rows[i];
                foreach (var kvp in _maxLengths)
                {
                    var value = GetColumnValue(item, kvp.Key);
                    if (!TryValidateValue(kvp.Key, value, out string error))
                        validationErrors.Add($"Row {i + 1} ({item.Name}): {error}");
                }
            }

            if (validationErrors.Count > 0)
            {
                var message = "Cannot save — fix the following before continuing:\n\n" +
                              string.Join("\n", validationErrors.Take(20));
                MessageBox.Show(message, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                _btnSave.Enabled = false;
                var items = new List<ItemDto>(_rows);
                _repo.UpdateCellPhoneDetails(items, AppSession.CurrentUserId);
                _lblStatus.Text = $"Saved {items.Count} item(s) at {DateTime.Now:t}.";
                MessageBox.Show("Cellphone details saved successfully.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save changes: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnSave.Enabled = true;
            }
        }
    }
}
