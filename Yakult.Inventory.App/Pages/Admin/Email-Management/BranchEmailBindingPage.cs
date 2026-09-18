using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Pages.Admin.EmailManagement
{
    /// <summary>
    /// Assigns emails to each Company / Department / Branch combination.
    ///
    /// Two email levels:
    ///   • Department Email (parent)  — one per Company+Department, stored in
    ///     dbo.DepartmentEmail.  All branches of the same dept share it.
    ///   • Branch Email (child)       — one per Company+Department+Branch, stored in
    ///     dbo.DepartmentAccount.EmailAddressId.
    ///
    /// Effective email = Branch Email if set, otherwise Department Email.
    ///
    /// Click any cell in "Department Email ✎" or "Branch Email ✎" to edit inline.
    /// </summary>
    public class BranchEmailBindingPage : UserControl
    {
        // ── Layout ──────────────────────────────────────────────────────────
        private DefaultListPageLayout _layout;
        private DataGridView _dgv;
        private System.Windows.Forms.Button _btnFirstPage, _btnPrevPage, _btnNextPage, _btnLastPage;
        private Label _lblPageInfo;
        private Label _lblStatus;
        private System.Windows.Forms.Timer _statusTimer;

        // ── Data ────────────────────────────────────────────────────────────
        private List<DeptEmailRow> _allRows;
        private List<DeptEmailRow> _filteredRows;
        private readonly string _connectionString;
        private int _currentPage = 1;
        private const int PageSize = 15;

        // ── Inline edit state ────────────────────────────────────────────────
        private bool _isSaving;
        private string _editOriginalValue = string.Empty;
        private int _editingColumnIndex = -1;

        // Email autocomplete cache (shared for both columns)
        private List<string> _emailCache = new List<string>();
        private Dictionary<string, int> _emailIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Column indices
        private const int DeptEmailColIdx   = 3;   // parent — dbo.DepartmentEmail
        private const int BranchEmailColIdx = 4;   // child  — dbo.DepartmentAccount.EmailAddressId

        // ── Sort state ───────────────────────────────────────────────────────
        private string _sortColumnName = null;
        private bool _sortAscending = true;

        // ── Column value filters (Excel-style) ────────────────────────────
        private Dictionary<string, HashSet<string>> _columnFilters = new Dictionary<string, HashSet<string>>();

        // ── Constructor ─────────────────────────────────────────────────────
        public BranchEmailBindingPage()
        {
            _connectionString = DatabaseConfig.ConnectionString;
            BuildUI();
            _ = LoadDataAsync();
        }

        // ── UI Construction ──────────────────────────────────────────────────
        private void BuildUI()
        {
            SuspendLayout();
            Controls.Clear();
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            _layout = DefaultListPageTemplate.Create(
                "Branch Email Binding",
                "Search by Company, Department, Branch, or Email...",
                (s, e) => ApplyFilters(),
                () => _ = LoadDataAsync());

            if (_layout.FilterByComboBox != null)
            {
                _layout.FilterByComboBox.Items.Clear();
                _layout.FilterByComboBox.Items.AddRange(new object[]
                {
                    "All", "Has Branch Email", "No Branch Email", "No Email (Both Empty)"
                });
                _layout.FilterByComboBox.SelectedIndex = 0;
                _layout.FilterByComboBox.SelectedIndexChanged += (s, e) => ApplyFilters();
            }

            if (_layout.SummaryPanel != null)   _layout.SummaryPanel.Visible = false;
            if (_layout.ButtonBarPanel != null) _layout.ButtonBarPanel.Visible = false;

            // ── Status bar ───────────────────────────────────────────────────
            _lblStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(39, 174, 96),
                BackColor = Color.White,
                Padding = new Padding(18, 0, 0, 0),
                Text = string.Empty
            };
            _statusTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _statusTimer.Tick += (s, e) => { _statusTimer.Stop(); _lblStatus.Text = string.Empty; };

            // ── DataGridView ─────────────────────────────────────────────────
            _dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                RowTemplate = { Height = 36 }
            };
            UiFactory.StyleGrid(_dgv);
            _dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Company",
                HeaderText = "Company",
                DataPropertyName = "CompanyName",
                MinimumWidth = 110,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Department",
                HeaderText = "Department",
                DataPropertyName = "DepartmentName",
                MinimumWidth = 150,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Branch",
                HeaderText = "Branch",
                DataPropertyName = "BranchName",
                MinimumWidth = 140,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            // Parent / Department email — editable
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDeptEmail",
                HeaderText = "Department Email  ✎",
                DataPropertyName = "DepartmentEmail",
                MinimumWidth = 200,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = false,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    ForeColor = Color.FromArgb(22, 100, 170)
                }
            });
            // Child / Branch email — editable
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colBranchEmail",
                HeaderText = "Branch Email  ✎",
                DataPropertyName = "BranchEmail",
                MinimumWidth = 200,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = false,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    ForeColor = Color.FromArgb(22, 100, 170)
                }
            });

            _dgv.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (e.ColumnIndex == DeptEmailColIdx)
                    e.ToolTipText = "Department email — shared by all branches in this department";
                else if (e.ColumnIndex == BranchEmailColIdx)
                    e.ToolTipText = "Branch email — overrides the department email for this branch";
            };

            _dgv.CellBeginEdit          += DgvCellBeginEdit;
            _dgv.EditingControlShowing  += DgvEditingControlShowing;
            _dgv.CellEndEdit            += DgvCellEndEdit;
            _dgv.ColumnHeaderMouseClick += DgvColumnHeaderMouseClick;
            _dgv.CellPainting           += DgvCellPainting;

            if (_layout.GridCard != null)
            {
                _layout.GridCard.Controls.Clear();
                _layout.GridCard.Controls.Add(_dgv);
            }

            // ── Pagination ───────────────────────────────────────────────────
            _btnFirstPage = new System.Windows.Forms.Button { Text = "<<", Width = 45, Height = 25, Left = 0,   Top = 5 };
            _btnPrevPage  = new System.Windows.Forms.Button { Text = "<",  Width = 45, Height = 25, Left = 50,  Top = 5 };
            _lblPageInfo  = new Label { AutoSize = false, Width = 300, Left = 100, Top = 10, Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.MiddleLeft };
            _btnNextPage  = new System.Windows.Forms.Button { Text = ">",  Width = 45, Height = 25, Left = 410, Top = 5 };
            _btnLastPage  = new System.Windows.Forms.Button { Text = ">>", Width = 45, Height = 25, Left = 460, Top = 5 };

            _btnFirstPage.Click += (s, e) => { _currentPage = 1; UpdateDataGridView(); };
            _btnPrevPage.Click  += (s, e) => { if (_currentPage > 1) { _currentPage--; UpdateDataGridView(); } };
            _btnNextPage.Click  += (s, e) => { int t = TotalPages(); if (_currentPage < t) { _currentPage++; UpdateDataGridView(); } };
            _btnLastPage.Click  += (s, e) => { _currentPage = TotalPages(); UpdateDataGridView(); };

            if (_layout.PaginationPanel != null)
            {
                _layout.PaginationPanel.Controls.Clear();
                _layout.PaginationPanel.Controls.AddRange(new Control[]
                    { _btnFirstPage, _btnPrevPage, _lblPageInfo, _btnNextPage, _btnLastPage });
            }

            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.SummaryPanel);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_lblStatus);
            Controls.Add(_layout.HeaderPanel);

            ResumeLayout(false);
        }

        // ── Inline Editing ────────────────────────────────────────────────────

        private void DgvCellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.ColumnIndex != DeptEmailColIdx && e.ColumnIndex != BranchEmailColIdx)
            {
                e.Cancel = true;
                return;
            }

            _editingColumnIndex = e.ColumnIndex;
            string raw = _dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
            _editOriginalValue = raw.Equals("(None)", StringComparison.OrdinalIgnoreCase) ? string.Empty : raw;
        }

        private void DgvEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            int col = _dgv.CurrentCell?.ColumnIndex ?? -1;
            if (col != DeptEmailColIdx && col != BranchEmailColIdx)
                return;

            if (!(e.Control is TextBox tb)) return;

            tb.TextChanged -= EmailEditor_TextChanged;
            tb.AutoCompleteMode = AutoCompleteMode.None;

            var source = new AutoCompleteStringCollection();
            if (_emailCache.Count > 0)
                source.AddRange(_emailCache.ToArray());

            tb.AutoCompleteCustomSource = source;
            tb.AutoCompleteSource = AutoCompleteSource.CustomSource;
            tb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;

            if (tb.Text.Equals("(None)", StringComparison.OrdinalIgnoreCase))
                tb.Text = string.Empty;

            tb.TextChanged += EmailEditor_TextChanged;
        }

        private void EmailEditor_TextChanged(object sender, EventArgs e) { }

        private void DgvCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if ((e.ColumnIndex != DeptEmailColIdx && e.ColumnIndex != BranchEmailColIdx) || _isSaving)
                return;

            var row = _dgv.Rows[e.RowIndex].DataBoundItem as DeptEmailRow;
            if (row == null) return;

            string newValue = (_dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty).Trim();
            if (newValue.Equals("(None)", StringComparison.OrdinalIgnoreCase))
                newValue = string.Empty;

            if (string.Equals(newValue, _editOriginalValue, StringComparison.OrdinalIgnoreCase))
                return;

            if (e.ColumnIndex == DeptEmailColIdx)
                _ = CommitDeptEmailChangeAsync(e.RowIndex, row, newValue);
            else
                _ = CommitBranchEmailChangeAsync(e.RowIndex, row, newValue);
        }

        // ── Commit: Department Email (parent) ─────────────────────────────────
        private async Task CommitDeptEmailChangeAsync(int rowIndex, DeptEmailRow row, string newEmail)
        {
            _isSaving = true;
            try
            {
                // ── Remove ──────────────────────────────────────────────────
                if (string.IsNullOrWhiteSpace(newEmail))
                {
                    var confirm = MessageBox.Show(
                        $"Remove the department email for:\n\n" +
                        $"  {row.CompanyName}  /  {row.DepartmentName}\n\n" +
                        $"This affects all branches in this department.",
                        "Confirm Remove",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (confirm != DialogResult.Yes) { RevertCell(rowIndex, DeptEmailColIdx); return; }

                    await DeleteDeptEmailBindingAsync(row.CompanyName, row.DepartmentName);
                    PropagateParentEmailToAllRows(row.CompanyName, row.DepartmentName, null, "(None)");
                    ShowStatus($"Department email removed for \"{row.DepartmentName}\".", success: false);
                    FlashCell(rowIndex, DeptEmailColIdx, Color.FromArgb(255, 210, 210));
                    return;
                }

                if (!IsValidEmailFormat(newEmail)) { ShowValidationError(rowIndex, DeptEmailColIdx); return; }

                int emailId = await ResolveOrCreateEmailAsync(newEmail, rowIndex, DeptEmailColIdx);
                if (emailId < 0) return; // user cancelled

                // Warn that this updates all branches of the same dept
                var branches = _allRows.Count(r => r.CompanyName == row.CompanyName && r.DepartmentName == row.DepartmentName);
                if (branches > 1)
                {
                    var confirm = MessageBox.Show(
                        $"Setting this email will apply to all {branches} branches of:\n\n" +
                        $"  {row.CompanyName}  /  {row.DepartmentName}\n\n" +
                        $"Continue?",
                        "Confirm Department Email",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (confirm != DialogResult.Yes) { RevertCell(rowIndex, DeptEmailColIdx); return; }
                }

                await UpsertDeptEmailBindingAsync(row.CompanyName, row.DepartmentName, emailId);
                PropagateParentEmailToAllRows(row.CompanyName, row.DepartmentName, emailId, newEmail);
                ShowStatus($"Department email updated for \"{row.DepartmentName}\" ({branches} branch{(branches == 1 ? "" : "es")}).");
                FlashCell(rowIndex, DeptEmailColIdx, Color.FromArgb(195, 240, 210));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving department email:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                RevertCell(rowIndex, DeptEmailColIdx);
            }
            finally { _isSaving = false; }
        }

        // ── Commit: Branch Email (child) ──────────────────────────────────────
        private async Task CommitBranchEmailChangeAsync(int rowIndex, DeptEmailRow row, string newEmail)
        {
            _isSaving = true;
            try
            {
                // ── Remove ──────────────────────────────────────────────────
                if (string.IsNullOrWhiteSpace(newEmail))
                {
                    var confirm = MessageBox.Show(
                        $"Remove the branch email for:\n\n" +
                        $"  {row.CompanyName}  /  {row.DepartmentName}  /  {row.BranchName}",
                        "Confirm Remove",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (confirm != DialogResult.Yes) { RevertCell(rowIndex, BranchEmailColIdx); return; }

                    await SaveBranchEmailBindingAsync(row.DeptAccountId, null);
                    row.BranchEmailAddressId = null;
                    row.BranchEmail = "(None)";
                    _dgv.Rows[rowIndex].Cells[BranchEmailColIdx].Value = "(None)";
                    ShowStatus($"Branch email removed from \"{row.BranchName}\".", success: false);
                    FlashCell(rowIndex, BranchEmailColIdx, Color.FromArgb(255, 210, 210));
                    return;
                }

                if (!IsValidEmailFormat(newEmail)) { ShowValidationError(rowIndex, BranchEmailColIdx); return; }

                int emailId = await ResolveOrCreateEmailAsync(newEmail, rowIndex, BranchEmailColIdx);
                if (emailId < 0) return;

                await SaveBranchEmailBindingAsync(row.DeptAccountId, emailId);
                row.BranchEmailAddressId = emailId;
                row.BranchEmail = newEmail;
                ShowStatus($"Branch email updated for \"{row.BranchName}\".");
                FlashCell(rowIndex, BranchEmailColIdx, Color.FromArgb(195, 240, 210));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving branch email:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                RevertCell(rowIndex, BranchEmailColIdx);
            }
            finally { _isSaving = false; }
        }

        // ── Shared Helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the EmailId for the given address, inserting it into
        /// dbo.EmailAddress if it doesn't exist (with user confirmation).
        /// Returns -1 if the user cancelled.
        /// </summary>
        private async Task<int> ResolveOrCreateEmailAsync(string email, int rowIndex, int colIdx)
        {
            if (_emailIdMap.TryGetValue(email, out int existing))
                return existing;

            var confirm = MessageBox.Show(
                $"\"{email}\" does not exist in the email list.\n\nInsert it now?",
                "New Email Address",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
            {
                RevertCell(rowIndex, colIdx);
                return -1;
            }

            int newId = await InsertEmailAddressAsync(email);
            _emailIdMap[email] = newId;
            _emailCache.Add(email);
            return newId;
        }

        private void ShowValidationError(int rowIndex, int colIdx)
        {
            MessageBox.Show("Please enter a valid email address (e.g. name@domain.com).",
                "Invalid Email", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RevertCell(rowIndex, colIdx);
        }

        /// <summary>Updates all in-memory rows with the same Company+Department parent email.</summary>
        private void PropagateParentEmailToAllRows(string company, string department, int? emailId, string emailText)
        {
            foreach (var r in _allRows)
            {
                if (r.CompanyName != company || r.DepartmentName != department) continue;
                r.DeptEmailAddressId = emailId;
                r.DepartmentEmail = emailText;
            }
            _dgv.Invalidate();
        }

        private void RevertCell(int rowIndex, int colIdx)
        {
            if (rowIndex < 0 || rowIndex >= _dgv.Rows.Count) return;
            _dgv.Rows[rowIndex].Cells[colIdx].Value =
                string.IsNullOrEmpty(_editOriginalValue) ? "(None)" : _editOriginalValue;
        }

        private void FlashCell(int rowIndex, int colIdx, Color flashColor)
        {
            if (rowIndex < 0 || rowIndex >= _dgv.Rows.Count) return;
            var cell = _dgv.Rows[rowIndex].Cells[colIdx];
            cell.Style.BackColor = flashColor;
            var t = new System.Windows.Forms.Timer { Interval = 1600 };
            t.Tick += (s, e) =>
            {
                t.Stop(); t.Dispose();
                if (rowIndex < _dgv.Rows.Count)
                    _dgv.Rows[rowIndex].Cells[colIdx].Style.BackColor = Color.Empty;
            };
            t.Start();
        }

        private void ShowStatus(string message, bool success = true)
        {
            _lblStatus.Text = message;
            _lblStatus.ForeColor = success ? Color.FromArgb(39, 174, 96) : Color.FromArgb(192, 57, 43);
            _statusTimer.Stop();
            _statusTimer.Start();
        }

        private static bool IsValidEmailFormat(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            int at = email.IndexOf('@');
            if (at <= 0 || at == email.Length - 1) return false;
            int dot = email.LastIndexOf('.');
            return dot > at + 1 && dot < email.Length - 1;
        }

        // ── Column Header Sort ────────────────────────────────────────────────

        private void DgvColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = _dgv.Columns[e.ColumnIndex];
            if (col.SortMode != DataGridViewColumnSortMode.Programmatic) return;

            // Click in the filter ▼ icon zone (rightmost 18 px) → show filter popup
            if (e.X >= col.Width - 18)
            {
                ShowColumnFilterPopup(col);
                return;
            }

            // Click elsewhere → toggle sort
            if (_sortColumnName == col.Name)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumnName = col.Name;
                _sortAscending = true;
            }

            foreach (DataGridViewColumn c in _dgv.Columns)
                c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            col.HeaderCell.SortGlyphDirection = _sortAscending
                ? System.Windows.Forms.SortOrder.Ascending
                : System.Windows.Forms.SortOrder.Descending;

            _currentPage = 1;
            ApplySortToFiltered();
            UpdateDataGridView();
        }

        private void ShowColumnFilterPopup(DataGridViewColumn col)
        {
            if (_allRows == null) return;

            Func<DeptEmailRow, string> getter;
            switch (col.Name)
            {
                case "Company":    getter = r => r.CompanyName    ?? ""; break;
                case "Department": getter = r => r.DepartmentName ?? ""; break;
                case "Branch":     getter = r => r.BranchName     ?? ""; break;
                default: return;
            }

            var distinctValues = _allRows
                .Select(getter)
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _columnFilters.TryGetValue(col.Name, out var currentFilter);

            // Position popup below the column header cell
            var colRect   = _dgv.GetColumnDisplayRectangle(col.Index, false);
            var screenPt  = _dgv.PointToScreen(new Point(colRect.Left, _dgv.ColumnHeadersHeight));

            using (var popup = new ColumnFilterPopup(col.HeaderText, distinctValues, currentFilter))
            {
                popup.Location = screenPt;

                // Keep popup on screen
                var screen = Screen.FromPoint(screenPt).WorkingArea;
                if (popup.Right  > screen.Right)  popup.Left = Math.Max(screen.Left, screen.Right - popup.Width);
                if (popup.Bottom > screen.Bottom) popup.Top  = Math.Max(screen.Top,  screenPt.Y - popup.Height - _dgv.ColumnHeadersHeight);

                popup.ShowDialog(this.FindForm());

                if (popup.Action == ColumnFilterPopup.PopupAction.SortAscending)
                {
                    _sortColumnName = col.Name;
                    _sortAscending = true;
                    foreach (DataGridViewColumn c in _dgv.Columns)
                        c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                    col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Ascending;
                    _currentPage = 1;
                    ApplySortToFiltered();
                    UpdateDataGridView();
                }
                else if (popup.Action == ColumnFilterPopup.PopupAction.SortDescending)
                {
                    _sortColumnName = col.Name;
                    _sortAscending = false;
                    foreach (DataGridViewColumn c in _dgv.Columns)
                        c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                    col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Descending;
                    _currentPage = 1;
                    ApplySortToFiltered();
                    UpdateDataGridView();
                }
                else if (popup.Action == ColumnFilterPopup.PopupAction.Filter)
                {
                    if (popup.SelectedValues == null || popup.SelectedValues.Count == 0
                        || popup.SelectedValues.Count >= distinctValues.Count)
                        _columnFilters.Remove(col.Name);
                    else
                        _columnFilters[col.Name] = popup.SelectedValues;

                    _currentPage = 1;
                    ApplyFilters();
                }
            }
        }

        private void ApplySortToFiltered()
        {
            if (_filteredRows == null || _sortColumnName == null) return;

            if (_sortColumnName == "Company")
                _filteredRows = _sortAscending
                    ? _filteredRows.OrderBy(r => r.CompanyName,    StringComparer.OrdinalIgnoreCase).ToList()
                    : _filteredRows.OrderByDescending(r => r.CompanyName,    StringComparer.OrdinalIgnoreCase).ToList();
            else if (_sortColumnName == "Department")
                _filteredRows = _sortAscending
                    ? _filteredRows.OrderBy(r => r.DepartmentName, StringComparer.OrdinalIgnoreCase).ToList()
                    : _filteredRows.OrderByDescending(r => r.DepartmentName, StringComparer.OrdinalIgnoreCase).ToList();
            else if (_sortColumnName == "Branch")
                _filteredRows = _sortAscending
                    ? _filteredRows.OrderBy(r => r.BranchName,     StringComparer.OrdinalIgnoreCase).ToList()
                    : _filteredRows.OrderByDescending(r => r.BranchName,     StringComparer.OrdinalIgnoreCase).ToList();
        }

        // ── Header Painting ───────────────────────────────────────────────────

        private void DgvCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 || e.ColumnIndex < 0) return;

            var column = _dgv.Columns[e.ColumnIndex];
            if (column == null) return;

            e.Handled = true;

            e.Graphics.FillRectangle(Brushes.White, e.CellBounds);

            var headerRect = new Rectangle(
                e.CellBounds.X, e.CellBounds.Y,
                e.CellBounds.Width - 1, e.CellBounds.Height - 1);

            using (var brush = new SolidBrush(e.CellStyle.BackColor))
                e.Graphics.FillRectangle(brush, headerRect);

            using (var pen = new Pen(Color.White, 1))
            {
                e.Graphics.DrawLine(pen,
                    e.CellBounds.Right - 1, e.CellBounds.Top,
                    e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                e.Graphics.DrawLine(pen,
                    e.CellBounds.Left, e.CellBounds.Bottom - 1,
                    e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }

            using (var textBrush = new SolidBrush(Color.White))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var textRect = e.CellBounds;
                bool isSortable = column.SortMode == DataGridViewColumnSortMode.Programmatic;

                if (isSortable)
                {
                    // Reserve space for sort arrow (14 px) + filter icon (14 px) + gaps
                    textRect.Width -= 34;

                    // Sort arrow — shifted left to make room for filter ▼ icon
                    int glyphX = e.CellBounds.Right - 32;
                    int glyphY = e.CellBounds.Y + (e.CellBounds.Height - 8) / 2;

                    Color arrowColor = column.HeaderCell.SortGlyphDirection != System.Windows.Forms.SortOrder.None
                        ? Color.White
                        : Color.FromArgb(160, 255, 255, 255);

                    using (var arrowPen = new Pen(arrowColor, 2))
                    {
                        if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Ascending)
                        {
                            e.Graphics.DrawLine(arrowPen, glyphX,      glyphY + 6, glyphX + 5,  glyphY);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5,  glyphY,     glyphX + 10, glyphY + 6);
                        }
                        else if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Descending)
                        {
                            e.Graphics.DrawLine(arrowPen, glyphX,      glyphY,     glyphX + 5,  glyphY + 6);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5,  glyphY + 6, glyphX + 10, glyphY);
                        }
                        else
                        {
                            e.Graphics.DrawLine(arrowPen, glyphX + 2, glyphY + 2, glyphX + 5, glyphY - 1);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY - 1, glyphX + 8, glyphY + 2);
                            e.Graphics.DrawLine(arrowPen, glyphX + 2, glyphY + 4, glyphX + 5, glyphY + 7);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY + 7, glyphX + 8, glyphY + 4);
                        }
                    }

                    // Filter ▼ icon — rightmost 14 px; yellow when filter is active
                    bool hasActiveFilter = _columnFilters.ContainsKey(column.Name);
                    int  filterX   = e.CellBounds.Right - 14;
                    int  filterMidY = e.CellBounds.Y + e.CellBounds.Height / 2;
                    var  filterPts = new PointF[]
                    {
                        new PointF(filterX,     filterMidY - 4),
                        new PointF(filterX + 9, filterMidY - 4),
                        new PointF(filterX + 4, filterMidY + 3)
                    };
                    using (var filterBrush = new SolidBrush(hasActiveFilter
                        ? Color.FromArgb(255, 230, 80)           // yellow = active filter
                        : Color.FromArgb(140, 255, 255, 255)))   // dim = no filter
                    {
                        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        e.Graphics.FillPolygon(filterBrush, filterPts);
                        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
                    }
                }

                e.Graphics.DrawString(column.HeaderText, e.CellStyle.Font ?? _dgv.Font, textBrush, textRect, fmt);
            }
        }

        // ── Filter / Pagination ───────────────────────────────────────────────

        private void ApplyFilters()
        {
            if (_allRows == null)
            {
                _filteredRows = new List<DeptEmailRow>();
                UpdateDataGridView();
                return;
            }

            var search = _layout.SearchBox?.Text?.Trim().ToLower() ?? "";
            var filter = _layout.FilterByComboBox?.SelectedItem?.ToString() ?? "All";

            _filteredRows = _allRows.Where(r =>
            {
                if (!string.IsNullOrEmpty(search))
                {
                    bool hit = r.CompanyName.ToLower().Contains(search)
                        || r.DepartmentName.ToLower().Contains(search)
                        || r.BranchName.ToLower().Contains(search)
                        || (r.DepartmentEmail ?? "").ToLower().Contains(search)
                        || (r.BranchEmail ?? "").ToLower().Contains(search);
                    if (!hit) return false;
                }

                switch (filter)
                {
                    case "Has Branch Email":  return r.BranchEmailAddressId.HasValue;
                    case "No Branch Email":   return !r.BranchEmailAddressId.HasValue;
                    case "No Email (Both Empty)": return !r.BranchEmailAddressId.HasValue && !r.DeptEmailAddressId.HasValue;
                    default:                  return true;
                }
            }).ToList();

            // Apply per-column value filters (Excel-style)
            foreach (var kvp in _columnFilters)
            {
                var selected = kvp.Value;
                switch (kvp.Key)
                {
                    case "Company":
                        _filteredRows = _filteredRows.Where(r => selected.Contains(r.CompanyName ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                    case "Department":
                        _filteredRows = _filteredRows.Where(r => selected.Contains(r.DepartmentName ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                    case "Branch":
                        _filteredRows = _filteredRows.Where(r => selected.Contains(r.BranchName ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                }
            }

            ApplySortToFiltered();
            _currentPage = 1;
            UpdateDataGridView();
        }

        private void UpdateDataGridView()
        {
            if (_filteredRows == null) { _dgv.DataSource = null; return; }

            var paged = _filteredRows
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            _dgv.DataSource = paged;
            _dgv.Invalidate();

            int total = TotalPages();
            if (_lblPageInfo != null)
                _lblPageInfo.Text = total == 0
                    ? "Page 0 of 0 (0 rows)"
                    : $"Page {_currentPage} of {total} ({_filteredRows.Count} rows)";

            if (_btnFirstPage != null) _btnFirstPage.Enabled = _currentPage > 1;
            if (_btnPrevPage  != null) _btnPrevPage.Enabled  = _currentPage > 1;
            if (_btnNextPage  != null) _btnNextPage.Enabled  = _currentPage < total;
            if (_btnLastPage  != null) _btnLastPage.Enabled  = _currentPage < total;
        }

        private int TotalPages()
        {
            int count = _filteredRows?.Count ?? 0;
            return count == 0 ? 1 : (int)Math.Ceiling(count / (double)PageSize);
        }

        // ── Data Loading ──────────────────────────────────────────────────────

        private async Task LoadDataAsync()
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    // Load all rows: parent email from dbo.DepartmentEmail, child from dbo.DepartmentAccount
                    const string rowSql = @"
                        SELECT da.Id,
                               da.CompanyName,
                               da.DepartmentName,
                               da.BranchName,
                               de.EmailAddressId  AS DeptEmailAddressId,
                               dea.EmailAddress   AS DepartmentEmail,
                               da.EmailAddressId  AS BranchEmailAddressId,
                               bea.EmailAddress   AS BranchEmail
                        FROM  dbo.DepartmentAccount da
                        LEFT JOIN dbo.DepartmentEmail de
                               ON  de.CompanyName    = da.CompanyName
                               AND de.DepartmentName = da.DepartmentName
                        LEFT JOIN dbo.EmailAddress dea ON dea.EmailId = de.EmailAddressId
                        LEFT JOIN dbo.EmailAddress bea ON bea.EmailId = da.EmailAddressId
                        ORDER BY da.CompanyName, da.DepartmentName, da.BranchName";

                    _allRows = new List<DeptEmailRow>();
                    using (var cmd = new SqlCommand(rowSql, con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _allRows.Add(new DeptEmailRow
                            {
                                DeptAccountId        = reader.GetInt32(0),
                                CompanyName          = reader.GetString(1),
                                DepartmentName       = reader.GetString(2),
                                BranchName           = reader.GetString(3),
                                DeptEmailAddressId   = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                                DepartmentEmail      = reader.IsDBNull(5) ? "(None)" : reader.GetString(5),
                                BranchEmailAddressId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                                BranchEmail          = reader.IsDBNull(7) ? "(None)" : reader.GetString(7)
                            });
                        }
                    }

                    // Load email cache for autocomplete
                    const string emailSql = @"
                        SELECT EmailId, EmailAddress FROM dbo.EmailAddress
                        WHERE IsActive = 1 ORDER BY EmailAddress";

                    _emailCache = new List<string>();
                    _emailIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    using (var cmd = new SqlCommand(emailSql, con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int id = reader.GetInt32(0);
                            string addr = reader.GetString(1);
                            _emailCache.Add(addr);
                            _emailIdMap[addr] = id;
                        }
                    }
                }

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load data:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── DB Operations ──────────────────────────────────────────────────────

        /// <summary>INSERT or UPDATE dbo.DepartmentEmail for the given Company+Department.</summary>
        private async Task UpsertDeptEmailBindingAsync(string company, string department, int emailId)
        {
            const string sql = @"
                IF EXISTS (SELECT 1 FROM dbo.DepartmentEmail
                           WHERE CompanyName = @Company AND DepartmentName = @Dept)
                    UPDATE dbo.DepartmentEmail
                    SET    EmailAddressId = @EmailId
                    WHERE  CompanyName = @Company AND DepartmentName = @Dept
                ELSE
                    INSERT INTO dbo.DepartmentEmail (CompanyName, DepartmentName, EmailAddressId)
                    VALUES (@Company, @Dept, @EmailId)";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Company", company);
                    cmd.Parameters.AddWithValue("@Dept",    department);
                    cmd.Parameters.AddWithValue("@EmailId", emailId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>Removes the dbo.DepartmentEmail row for the given Company+Department.</summary>
        private async Task DeleteDeptEmailBindingAsync(string company, string department)
        {
            const string sql = @"
                DELETE FROM dbo.DepartmentEmail
                WHERE CompanyName = @Company AND DepartmentName = @Dept";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Company", company);
                    cmd.Parameters.AddWithValue("@Dept",    department);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>Sets or clears EmailAddressId on the DepartmentAccount row (branch email).</summary>
        private async Task SaveBranchEmailBindingAsync(int deptAccountId, int? emailId)
        {
            const string sql = @"
                UPDATE dbo.DepartmentAccount
                SET EmailAddressId = @EmailAddressId
                WHERE Id = @Id";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id", deptAccountId);
                    cmd.Parameters.Add(new SqlParameter("@EmailAddressId", SqlDbType.Int)
                    {
                        Value = emailId.HasValue ? (object)emailId.Value : DBNull.Value
                    });
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>Inserts a new email into dbo.EmailAddress and returns its new EmailId.</summary>
        private async Task<int> InsertEmailAddressAsync(string email)
        {
            const string sql = @"
                UPDATE dbo.EmailAddress SET IsActive = 1
                WHERE EmailAddress = @Email AND IsActive = 0;

                IF NOT EXISTS (SELECT 1 FROM dbo.EmailAddress WHERE EmailAddress = @Email)
                    INSERT INTO dbo.EmailAddress (EmailAddress, DisplayName, IsActive)
                    VALUES (@Email, @Email, 1);

                SELECT EmailId FROM dbo.EmailAddress WHERE EmailAddress = @Email;";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        // ── Inner DTO ─────────────────────────────────────────────────────────

        private class DeptEmailRow
        {
            public int    DeptAccountId        { get; set; }
            public string CompanyName          { get; set; }
            public string DepartmentName       { get; set; }
            public string BranchName           { get; set; }
            // Parent — stored in dbo.DepartmentEmail (shared per Company+Department)
            public int?   DeptEmailAddressId   { get; set; }
            public string DepartmentEmail      { get; set; }
            // Child — stored in dbo.DepartmentAccount.EmailAddressId (per branch)
            public int?   BranchEmailAddressId { get; set; }
            public string BranchEmail          { get; set; }
        }
    }
}
