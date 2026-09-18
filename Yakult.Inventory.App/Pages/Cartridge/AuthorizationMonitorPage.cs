using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    /// <summary>
    /// Read-only monitor for IT staff to view all CartridgeAuthorization records.
    /// </summary>
    public class AuthorizationMonitorPage : UserControl
    {
        private DefaultListPageLayout _layout;
        private DataGridView _grid;

        private List<CartridgeAuthorizationModel> _allItems = new List<CartridgeAuthorizationModel>();
        private List<CartridgeAuthorizationModel> _filtered  = new List<CartridgeAuthorizationModel>();

        // Pagination
        private System.Windows.Forms.Button _btnFirst, _btnPrev, _btnNext, _btnLast;
        private Label _lblPageInfo;
        private int _currentPage = 1;
        private const int PageSize = 20;

        // Sorting
        private DataGridViewColumn _sortColumn;
        private SortOrder _sortOrder = SortOrder.None;
        private bool _sortByDropdownInitialized = false;

        // Status badge colours — each status uses a distinct hue
        private static readonly Color PendingBack  = Color.FromArgb(238, 242, 255); // indigo-50
        private static readonly Color PendingFore  = Color.FromArgb( 67,  56, 202); // indigo-700
        private static readonly Color ApprovedBack = Color.FromArgb(220, 252, 231); // green-100
        private static readonly Color ApprovedFore = Color.FromArgb( 21, 128,  61); // green-700
        private static readonly Color RejectedBack = Color.FromArgb(254, 226, 226); // red-100
        private static readonly Color RejectedFore = Color.FromArgb(185,  28,  28); // red-700
        private static readonly Color UsedBack     = Color.FromArgb(241, 245, 249); // slate-100
        private static readonly Color UsedFore     = Color.FromArgb( 71,  85, 105); // slate-600

        public AuthorizationMonitorPage()
        {
            Dock = DockStyle.Fill;
            BuildUi();
            _ = LoadDataAsync();
        }

        private void BuildUi()
        {
            SuspendLayout();
            Controls.Clear();
            BackColor = Color.White;

            _layout = DefaultListPageTemplate.Create(
                "Authorization Monitor",
                "Search by employee, department, branch, status...",
                (s, e) => ApplyFilter(),
                () => _ = LoadDataAsync());

            if (_layout.FilterByComboBox != null)
                _layout.FilterByComboBox.SelectedIndexChanged += (s, e) =>
                {
                    _sortColumn = null;
                    _sortOrder  = SortOrder.None;
                    ApplyFilter();
                };

            if (_layout.SummaryPanel != null) _layout.SummaryPanel.Visible = false;

            // Show pagination panel
            if (_layout.PaginationPanel != null) _layout.PaginationPanel.Visible = true;

            // ── Grid ─────────────────────────────────────────────────────────
            _grid = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                AutoGenerateColumns   = false,
                RowHeadersVisible     = false,
                BackgroundColor       = Color.White,
                BorderStyle           = BorderStyle.None,
            };

            _grid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "colId",         HeaderText = "ID",               FillWeight = 5,  MinimumWidth = 50,  ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colEmployee",   HeaderText = "Employee",          FillWeight = 16, MinimumWidth = 120, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colPosition",   HeaderText = "Position",          FillWeight = 12, MinimumWidth = 100, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colDepartment", HeaderText = "Department",        FillWeight = 12, MinimumWidth = 100, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colBranch",     HeaderText = "Branch",            FillWeight = 10, MinimumWidth = 90,  ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colCompany",    HeaderText = "Company",           FillWeight = 10, MinimumWidth = 90,  ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colModels",     HeaderText = "Requested Models",  FillWeight = 16, MinimumWidth = 120, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colStatus",     HeaderText = "Status",            FillWeight = 9,  MinimumWidth = 90,  ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colSignedBy",   HeaderText = "Signed By",         FillWeight = 12, MinimumWidth = 100, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colSignedDate", HeaderText = "Signed Date",       FillWeight = 10, MinimumWidth = 110, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colRequested",  HeaderText = "Requested",         FillWeight = 10, MinimumWidth = 110, ReadOnly = true },
            });

            foreach (DataGridViewColumn col in _grid.Columns)
                col.SortMode = DataGridViewColumnSortMode.Programmatic;

            UiFactory.StyleGrid(_grid);

            // Pill painter first — Status badge painter registered after so it draws on top
            DefaultListPageTemplate.AttachVendorsPillCellPainting(_grid, c =>
            {
                if (c == null) return false;
                return string.Equals(c.Name, "colId", StringComparison.OrdinalIgnoreCase);
            });

            _grid.CellPainting          += Grid_CellPainting;
            _grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;

            _layout.GridCard.Controls.Clear();
            _layout.GridCard.Controls.Add(_grid);

            // ── Pagination controls ───────────────────────────────────────────
            _btnFirst = new System.Windows.Forms.Button { Text = "<<", Width = 45, Height = 25, Left = 0,   Top = 5 };
            _btnPrev  = new System.Windows.Forms.Button { Text = "<",  Width = 45, Height = 25, Left = 50,  Top = 5 };
            _lblPageInfo = new Label { Width = 280, Height = 25, Left = 100, Top = 8, TextAlign = ContentAlignment.MiddleCenter };
            _btnNext  = new System.Windows.Forms.Button { Text = ">",  Width = 45, Height = 25, Left = 385, Top = 5 };
            _btnLast  = new System.Windows.Forms.Button { Text = ">>", Width = 45, Height = 25, Left = 435, Top = 5 };

            _btnFirst.Click += (s, e) => { _currentPage = 1; UpdatePagination(); };
            _btnPrev.Click  += (s, e) => { if (_currentPage > 1) { _currentPage--; UpdatePagination(); } };
            _btnNext.Click  += (s, e) =>
            {
                int total = (int)Math.Ceiling((double)_filtered.Count / PageSize);
                if (_currentPage < total) { _currentPage++; UpdatePagination(); }
            };
            _btnLast.Click  += (s, e) =>
            {
                _currentPage = Math.Max(1, (int)Math.Ceiling((double)_filtered.Count / PageSize));
                UpdatePagination();
            };

            if (_layout.PaginationPanel != null)
            {
                _layout.PaginationPanel.Controls.Clear();
                _layout.PaginationPanel.Controls.AddRange(new Control[]
                    { _btnFirst, _btnPrev, _lblPageInfo, _btnNext, _btnLast });
            }

            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.SummaryPanel);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_layout.HeaderPanel);

            ResumeLayout(true);
            DefaultListPageTemplate.SetupInitialPageFocus(_layout.SearchBox, _grid, _layout.RefreshButton);
        }

        // ── Data ─────────────────────────────────────────────────────────────

        private async Task LoadDataAsync()
        {
            try
            {
                var repo  = new CartridgeAuthorizationRepository();
                var items = await Task.Run(() => repo.GetAllAuthorizationsAsync());
                _allItems = items ?? new List<CartridgeAuthorizationModel>();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading authorizations:\n\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyFilter()
        {
            if (_layout?.SearchBox == null) return;

            string q = (_layout.SearchBox.Text ?? "").Trim();
            bool hasQuery = !string.IsNullOrWhiteSpace(q)
                         && !q.Equals("Search by employee, department, branch, status...",
                                      StringComparison.OrdinalIgnoreCase);

            IEnumerable<CartridgeAuthorizationModel> query = _allItems;

            if (hasQuery)
                query = query.Where(a =>
                    Contains(a.EmployeeName,     q) || Contains(a.EmployeePosition, q) ||
                    Contains(a.DepartmentName,   q) || Contains(a.BranchName,       q) ||
                    Contains(a.CompanyName,      q) || Contains(a.Status,           q) ||
                    Contains(a.SignedByName,     q) || Contains(a.RequestedModels,  q));

            _filtered = query.ToList();

            string filterBy = _layout?.FilterByComboBox?.SelectedItem?.ToString();
            if (_sortColumn != null)
            {
                var prop = typeof(CartridgeAuthorizationModel).GetProperty(_sortColumn.Name
                    .Replace("col", string.Empty));
                // fall back to column header name mapping
                _filtered = SortFiltered(_filtered, _sortColumn.Name, _sortOrder);
            }
            else
            {
                _filtered = filterBy == "Oldest Added"
                    ? _filtered.OrderBy(x => x.CreatedDate).ToList()
                    : _filtered.OrderByDescending(x => x.CreatedDate).ToList();
            }

            _currentPage = 1;
            UpdatePagination();

            if (!_sortByDropdownInitialized && _layout?.SortByComboBox != null && _grid.Columns.Count > 0)
            {
                DefaultListPageTemplate.SetupSortByDropdown(
                    _layout.SortByComboBox, _grid,
                    (columnKey, direction) =>
                    {
                        var col = _grid.Columns.Cast<DataGridViewColumn>()
                            .FirstOrDefault(c => c.Name == columnKey);
                        if (col != null)
                        {
                            _sortColumn = col;
                            _sortOrder  = direction;
                            if (_layout.FilterByComboBox?.SelectedIndex != 0)
                                _layout.FilterByComboBox.SelectedIndex = 0;
                            ApplyFilter();
                        }
                    },
                    defaultColumnKey: "colRequested",
                    defaultDirection: SortOrder.Descending);
                _sortByDropdownInitialized = true;
            }
        }

        private static bool Contains(string source, string q) =>
            source != null && source.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

        private static List<CartridgeAuthorizationModel> SortFiltered(
            List<CartridgeAuthorizationModel> list, string colName, SortOrder order)
        {
            Func<CartridgeAuthorizationModel, object> key;
            switch (colName)
            {
                case "colId":         key = x => (object)x.AuthorizationId; break;
                case "colEmployee":   key = x => x.EmployeeName;    break;
                case "colPosition":   key = x => x.EmployeePosition; break;
                case "colDepartment": key = x => x.DepartmentName;  break;
                case "colBranch":     key = x => x.BranchName;      break;
                case "colCompany":    key = x => x.CompanyName;     break;
                case "colModels":     key = x => x.RequestedModels; break;
                case "colStatus":     key = x => x.Status;          break;
                case "colSignedBy":   key = x => x.SignedByName;    break;
                case "colSignedDate": key = x => (object)x.SignedDate; break;
                case "colRequested":  key = x => (object)x.CreatedDate; break;
                default:              key = x => (object)x.CreatedDate; break;
            }
            return order == SortOrder.Ascending
                ? list.OrderBy(key).ToList()
                : list.OrderByDescending(key).ToList();
        }

        private void UpdatePagination()
        {
            int total      = _filtered.Count;
            int totalPages = total == 0 ? 1 : (int)Math.Ceiling((double)total / PageSize);
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1)          _currentPage = 1;

            var page = _filtered
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            BindGrid(page);

            if (_lblPageInfo != null)
                _lblPageInfo.Text = $"Page {_currentPage} of {totalPages}  ({total} records)";

            if (_btnFirst != null) _btnFirst.Enabled = _currentPage > 1;
            if (_btnPrev  != null) _btnPrev.Enabled  = _currentPage > 1;
            if (_btnNext  != null) _btnNext.Enabled  = _currentPage < totalPages;
            if (_btnLast  != null) _btnLast.Enabled  = _currentPage < totalPages;
        }

        private void BindGrid(List<CartridgeAuthorizationModel> page)
        {
            _grid.Rows.Clear();

            foreach (var a in page)
            {
                _grid.Rows.Add(
                    a.AuthorizationId,
                    a.EmployeeName     ?? $"EmpId {a.EmployeeId}",
                    a.EmployeePosition ?? "—",
                    a.DepartmentName   ?? $"DeptId {a.DepartmentId}",
                    a.BranchName       ?? "—",
                    a.CompanyName      ?? "—",
                    FormatRequestedModels(a.RequestedModels),
                    a.Status,
                    a.SignedByName     ?? "—",
                    a.SignedDate.HasValue ? a.SignedDate.Value.ToString("yyyy-MM-dd HH:mm") : "—",
                    a.CreatedDate.ToString("yyyy-MM-dd HH:mm")
                );
            }

            DefaultListPageTemplate.DisableDefaultRowHighlight(_grid, _layout.RefreshButton);
        }

        // ── Sorting ──────────────────────────────────────────────────────────

        private void Grid_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = _grid.Columns[e.ColumnIndex];
            if (col is DataGridViewCheckBoxColumn || col is DataGridViewButtonColumn) return;

            if (_sortColumn == col)
                _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            else
            {
                _sortColumn = col;
                _sortOrder  = SortOrder.Ascending;
            }

            if (_layout?.SortByComboBox != null)
                DefaultListPageTemplate.SyncSortByDropdown(_layout.SortByComboBox, _sortColumn, _sortOrder);

            ApplyFilter();
        }

        // ── Status badge painting ─────────────────────────────────────────────
        // Registered AFTER AttachVendorsPillCellPainting — draws pill badge on top
        // of the already-painted row background without repainting it, preserving
        // alternating row colours and selection highlights.

        private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "colStatus") return;
            if (e.Value == null || string.IsNullOrEmpty(e.Value.ToString())) return;

            string status = e.Value.ToString();
            GetStatusColors(status, out Color back, out Color fore);

            // AttachVendorsPillCellPainting already drew the row background AND the raw
            // cell text (e.g. "Approved"). Repaint the cell interior with the same row
            // background colour to erase that text before drawing the badge, while
            // preserving the white 1px separator borders it painted.
            bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
            Color rowBg = isSelected
                ? UiTheme.Colors.GridSelectionBack
                : (e.RowIndex % 2 == 0
                    ? Color.FromArgb(227, 242, 253)   // even row — light blue
                    : Color.FromArgb(232, 245, 233));  // odd row  — light green

            // Matches the interior rect used by AttachVendorsPillCellPainting
            // (6px top + bottom padding, 1px right border gap)
            var interior = new Rectangle(
                e.CellBounds.X,
                e.CellBounds.Y + 6,
                e.CellBounds.Width - 1,
                e.CellBounds.Height - 13);

            using (var bgBrush = new SolidBrush(rowBg))
                e.Graphics.FillRectangle(bgBrush, interior);

            // Draw pill badge centred in the cell
            int padH = 10, padV = 4;
            SizeF textSize = e.Graphics.MeasureString(status, e.CellStyle.Font ?? _grid.Font);
            int pillW = (int)textSize.Width  + padH * 2;
            int pillH = (int)textSize.Height + padV * 2;
            int pillX = e.CellBounds.X + (e.CellBounds.Width  - pillW) / 2;
            int pillY = e.CellBounds.Y + (e.CellBounds.Height - pillH) / 2;

            var pillRect = new Rectangle(pillX, pillY, pillW, pillH);
            int radius   = pillH / 2;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var gp = new GraphicsPath())
            {
                gp.AddArc(pillRect.X,               pillRect.Y,                radius, radius, 180, 90);
                gp.AddArc(pillRect.Right - radius,  pillRect.Y,                radius, radius, 270, 90);
                gp.AddArc(pillRect.Right - radius,  pillRect.Bottom - radius,  radius, radius,   0, 90);
                gp.AddArc(pillRect.X,               pillRect.Bottom - radius,  radius, radius,  90, 90);
                gp.CloseFigure();

                using (var brush = new SolidBrush(back))
                    e.Graphics.FillPath(brush, gp);
            }

            using (var sf    = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var font  = new Font(e.CellStyle.Font ?? _grid.Font, FontStyle.Bold))
            using (var brush = new SolidBrush(fore))
                e.Graphics.DrawString(status, font, brush, pillRect, sf);

            e.Handled = true;
        }

        /// <summary>
        /// Converts RequestedModels to a human-readable string.
        /// JSON array format: [{"model":"HP 85A","qty":1,...}] → "HP 85A (x1), Canon 337 (x2)"
        /// Plain text: returned as-is.
        /// </summary>
        private static string FormatRequestedModels(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "—";
            if (!raw.TrimStart().StartsWith("[")) return raw;

            try
            {
                var items = JsonSerializer.Deserialize<List<JsonElement>>(raw);
                if (items == null || items.Count == 0) return raw;

                var parts = items.Select(e =>
                {
                    string model = e.TryGetProperty("model", out var m) ? m.GetString() ?? "" : "";
                    int qty = e.TryGetProperty("qty", out var q) ? q.GetInt32() : 0;
                    return qty > 0 ? $"{model} (x{qty})" : model;
                }).Where(s => !string.IsNullOrWhiteSpace(s));

                return string.Join(", ", parts);
            }
            catch
            {
                return raw;
            }
        }

        private static void GetStatusColors(string status, out Color back, out Color fore)
        {
            switch (status)
            {
                case "Approved": back = ApprovedBack; fore = ApprovedFore; break;
                case "Pending":  back = PendingBack;  fore = PendingFore;  break;
                case "Rejected": back = RejectedBack; fore = RejectedFore; break;
                case "Used":     back = UsedBack;     fore = UsedFore;     break;
                default:         back = UsedBack;     fore = UsedFore;     break;
            }
        }
    }
}
