using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ReaLTaiizor.Controls;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

using Panel    = System.Windows.Forms.Panel;
using Button   = System.Windows.Forms.Button;
using CheckBox = System.Windows.Forms.CheckBox;

namespace Yakult.Inventory.App.Pages.Export
{
    // ════════════════════════════════════════════════════════════════════════
    //  Export Cartridge Disposed/Sold — DefaultListPageTemplate design
    // ════════════════════════════════════════════════════════════════════════
    public class ExportCartridgeDisposedSold : UserControl
    {
        // ── data ────────────────────────────────────────────────────────────
        private readonly DisposedSoldReportRepository _repo;
        private List<DisposedSoldItemDto> _data = new List<DisposedSoldItemDto>();

        private readonly List<DataGridViewRow> _filteredRows = new List<DataGridViewRow>();
        private int _currentPage = 1;
        private int _pageSize    = 25;

        private bool _headerCheckState = false;

        // ── layout ──────────────────────────────────────────────────────────
        private DefaultListPageLayout _layout;

        // ── filter controls ─────────────────────────────────────────────────
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private ComboBox       _cboType;

        // ── grid & action controls ───────────────────────────────────────────
        private DataGridView _grid;
        private HopeButton   _btnExport;

        private Button   _btnFirstPage, _btnPrevPage, _btnNextPage, _btnLastPage;
        private Label    _lblPageInfo;
        private ComboBox _cmbPageSize;
        private Label    _lblCount;

        public ExportCartridgeDisposedSold()
        {
            Dock      = DockStyle.Fill;
            BackColor = Color.White;
            _repo     = new DisposedSoldReportRepository();
            BuildUiWithTemplate();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadAsync();
        }

        // ════════════════════════════════════════════════════════════════════
        //  UI — template build
        // ════════════════════════════════════════════════════════════════════

        private void BuildUiWithTemplate()
        {
            SuspendLayout();
            Controls.Clear();

            // ── 1. Layout skeleton ───────────────────────────────────────────
            _layout = DefaultListPageTemplate.Create(
                "Export Cartridge Disposed/Sold",
                "Search by Item, Model, Serial, Category, Recipient, Brand…",
                (s, e) => RebuildFilter(),
                () => { _ = LoadAsync(); });

            // hide Filter By / Sort By dropdowns from template
            if (_layout.FilterByComboBox != null) _layout.FilterByComboBox.Visible = false;
            if (_layout.SortByComboBox   != null) _layout.SortByComboBox.Visible   = false;
            foreach (Control c in _layout.HeaderPanel.Controls)
                if (c is Label lbl && (lbl.Text == "Filter By:" || lbl.Text == "Sort By:"))
                    lbl.Visible = false;

            // ── 2. Back button ───────────────────────────────────────────────
            var btnBack = new HopeButton
            {
                Text   = "← Back",
                Font   = UiTheme.Fonts.Button,
                Size   = new Size(100, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 8, 0)
            };
            UiFactory.ConfigurePillHopeButton(btnBack, UiTheme.Colors.TextMuted, Color.FromArgb(90, 100, 120));
            btnBack.Click += (s, e) =>
            {
                var p = Parent;
                if (p != null) { p.Controls.Clear(); p.Controls.Add(new ReportPickerPage()); }
            };
            DefaultListPageTemplate.AddButtons(_layout.ButtonLeftFlow, new[] { btnBack });

            // ── 3. Export button (right flow) ────────────────────────────────
            _btnExport = new HopeButton
            {
                Text    = "📄  Export Selected",
                Font    = UiTheme.Fonts.Button,
                Size    = new Size(200, UiTheme.Sizes.PillButtonHeight),
                Margin  = new Padding(0, 6, 8, 0),
                Enabled = false
            };
            UiFactory.ConfigurePillHopeButton(_btnExport, UiTheme.Colors.Primary, UiTheme.Colors.PrimaryHover);
            _btnExport.Click += OnExportClick;

            var rightFlow = _layout.RefreshButton?.Parent as FlowLayoutPanel;
            if (rightFlow != null)
            {
                rightFlow.Controls.Add(_btnExport);
                rightFlow.Controls.SetChildIndex(_btnExport, 0);
            }

            if (_layout.RefreshButton != null)
            {
                _layout.RefreshButton.BackColor = Color.FromArgb(240, 242, 245);
                _layout.RefreshButton.ForeColor = UiTheme.Colors.TextDark;
            }

            // ── 4. Filter bar (From / To / Type) ────────────────────────────
            var filterHost = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 46,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding   = new Padding(12, 8, 12, 6)
            };
            filterHost.Paint += (s, ev) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240)))
                {
                    ev.Graphics.DrawLine(pen, 0, filterHost.Height - 1, filterHost.Width, filterHost.Height - 1);
                }
            };

            var filterFlow = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                AutoSize      = false,
                BackColor     = Color.Transparent
            };

            Label FLbl(string t) => new Label
            {
                Text      = t,
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = UiTheme.Colors.TextMuted,
                Margin    = new Padding(0, 6, 4, 0)
            };

            _dtpFrom = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Width  = 110,
                Value  = new DateTime(DateTime.Today.Year, 1, 1),
                Margin = new Padding(0, 2, 10, 0)
            };
            _dtpFrom.ValueChanged += (s, e) => _ = LoadAsync();

            _dtpTo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Width  = 110,
                Value  = DateTime.Today,
                Margin = new Padding(0, 2, 10, 0)
            };
            _dtpTo.ValueChanged += (s, e) => _ = LoadAsync();

            _cboType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width         = 110,
                Font          = new Font("Segoe UI", 8.5F),
                Margin        = new Padding(0, 2, 0, 0)
            };
            _cboType.Items.AddRange(new object[] { "All", "DISPOSE", "SELL" });
            _cboType.SelectedIndex = 0;
            _cboType.SelectedIndexChanged += (s, e) => _ = LoadAsync();

            filterFlow.Controls.Add(FLbl("From:"));   filterFlow.Controls.Add(_dtpFrom);
            filterFlow.Controls.Add(FLbl("To:"));     filterFlow.Controls.Add(_dtpTo);
            filterFlow.Controls.Add(FLbl("Type:"));   filterFlow.Controls.Add(_cboType);

            filterHost.Controls.Add(filterFlow);

            // ── 5. Count label ───────────────────────────────────────────────
            _lblCount = new Label
            {
                Dock      = DockStyle.Bottom,
                Height    = 28,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = UiTheme.Colors.TextMuted,
                TextAlign = ContentAlignment.MiddleRight,
                Padding   = new Padding(0, 0, 12, 0),
                Text      = "0 item(s)"
            };

            // ── 6. Grid ─────────────────────────────────────────────────────
            _grid = new DataGridView
            {
                Dock              = DockStyle.Fill,
                BackgroundColor   = Color.White,
                BorderStyle       = BorderStyle.None,
                CellBorderStyle   = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor         = Color.FromArgb(226, 232, 240),
                RowHeadersVisible = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                ReadOnly          = false,
                SelectionMode     = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 40,
                RowTemplate         = { Height = 56 },
                Font                = new Font("Arial", 9F),
                MultiSelect         = true
            };
            ApplyGridStyle(_grid);
            _grid.CellValueChanged += OnCellValueChanged;
            _grid.CurrentCellDirtyStateChanged += (s, ev) =>
            {
                if (_grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.CellFormatting += OnCellFormatting;

            DefaultListPageTemplate.AttachVendorsPillCellPainting(_grid, col =>
                col?.Name == "chkSelect");

            // Header checkbox on ✓ column
            _grid.CellPainting += (s, ev) =>
            {
                if (ev.RowIndex != -1) return;
                if (!_grid.Columns.Contains("chkSelect")) return;
                if (ev.ColumnIndex != _grid.Columns["chkSelect"].Index) return;

                var state = _headerCheckState
                    ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                    : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
                var pt = new Point(
                    ev.CellBounds.X + (ev.CellBounds.Width  - 13) / 2,
                    ev.CellBounds.Y + (ev.CellBounds.Height - 13) / 2);
                CheckBoxRenderer.DrawCheckBox(ev.Graphics, pt, state);
            };
            _grid.ColumnHeaderMouseClick += (s, ev) =>
            {
                if (!_grid.Columns.Contains("chkSelect")) return;
                if (ev.ColumnIndex != _grid.Columns["chkSelect"].Index) return;

                _headerCheckState = !_headerCheckState;
                _grid.CellValueChanged -= OnCellValueChanged;
                foreach (DataGridViewRow row in _grid.Rows)
                    if (!row.IsNewRow && row.Visible)
                        row.Cells["chkSelect"].Value = _headerCheckState;
                _grid.Refresh();
                _grid.CellValueChanged += OnCellValueChanged;
                UpdatePaginationUi();
            };

            // ── 7. Wire grid card ─────────────────────────────────────────────
            _layout.GridCard.Controls.Clear();
            _layout.GridCard.Controls.Add(_grid);
            _layout.GridCard.Controls.Add(_lblCount);
            _layout.GridCard.Controls.Add(filterHost);

            // ── 8. Pagination ────────────────────────────────────────────────
            BuildPaginationControls();

            // ── 9. Compose ───────────────────────────────────────────────────
            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_layout.HeaderPanel);

            ResumeLayout(true);
        }

        private static void ApplyGridStyle(DataGridView g)
        {
            var h = g.ColumnHeadersDefaultCellStyle;
            h.BackColor          = UiTheme.Colors.GridHeaderBack;
            h.ForeColor          = Color.White;
            h.Font               = UiTheme.Fonts.GridHeader;
            h.SelectionBackColor = UiTheme.Colors.GridHeaderBack;
            h.SelectionForeColor = Color.White;
            h.Padding            = new Padding(10, 6, 10, 6);
            g.DefaultCellStyle.SelectionBackColor = UiTheme.Colors.GridSelectionBack;
            g.DefaultCellStyle.SelectionForeColor = UiTheme.Colors.GridSelectionFore;
            g.DefaultCellStyle.Padding            = new Padding(8, 6, 8, 6);
            g.AlternatingRowsDefaultCellStyle.BackColor = UiTheme.Colors.GridAltRowBack;
            g.EnableHeadersVisualStyles = false;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Pagination
        // ════════════════════════════════════════════════════════════════════

        private void BuildPaginationControls()
        {
            if (_layout?.PaginationPanel == null) return;

            var footer = _layout.PaginationPanel;
            footer.Controls.Clear();

            var container = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1,
                BackColor   = Color.Transparent,
                Margin      = new Padding(0),
                Padding     = new Padding(0)
            };
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            container.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var navFlow = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                AutoSize      = false,
                BackColor     = Color.Transparent,
                Margin        = new Padding(0),
                Padding       = new Padding(0)
            };

            _btnFirstPage = new Button { Text = "<<", Width = 45, Height = 25, Margin = new Padding(0, 0, 6, 0) };
            _btnPrevPage  = new Button { Text = "<",  Width = 45, Height = 25, Margin = new Padding(0, 0, 10, 0) };
            _lblPageInfo  = new Label
            {
                AutoSize  = false,
                Width     = 310,
                Height    = 25,
                Font      = new Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.Colors.TextMuted,
                Margin    = new Padding(0, 2, 10, 0)
            };
            _btnNextPage = new Button { Text = ">",  Width = 45, Height = 25, Margin = new Padding(0, 0, 6, 0) };
            _btnLastPage = new Button { Text = ">>", Width = 45, Height = 25 };

            _btnFirstPage.Click += (s, e) => { _currentPage = 1; ApplyPage(); };
            _btnPrevPage.Click  += (s, e) => { _currentPage = Math.Max(1, _currentPage - 1); ApplyPage(); };
            _btnNextPage.Click  += (s, e) => { _currentPage++; ApplyPage(); };
            _btnLastPage.Click  += (s, e) => { _currentPage = TotalPages(); ApplyPage(); };

            navFlow.Controls.Add(_btnFirstPage);
            navFlow.Controls.Add(_btnPrevPage);
            navFlow.Controls.Add(_lblPageInfo);
            navFlow.Controls.Add(_btnNextPage);
            navFlow.Controls.Add(_btnLastPage);

            var rightFlow = new FlowLayoutPanel
            {
                AutoSize      = true,
                AutoSizeMode  = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                BackColor     = Color.Transparent,
                Margin        = new Padding(0),
                Padding       = new Padding(0)
            };
            rightFlow.Controls.Add(new Label
            {
                AutoSize  = true,
                Text      = "Rows:",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = UiTheme.Colors.TextMuted,
                Margin    = new Padding(0, 5, 6, 0)
            });

            _cmbPageSize = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width         = 80,
                Font          = new Font("Segoe UI", 9F),
                Margin        = new Padding(0, 1, 0, 0)
            };
            _cmbPageSize.Items.AddRange(new object[] { "10", "25", "50", "100" });
            _cmbPageSize.SelectedItem = _pageSize.ToString();
            _cmbPageSize.SelectedIndexChanged += (s, e) =>
            {
                if (_cmbPageSize.SelectedItem == null) return;
                if (int.TryParse(_cmbPageSize.SelectedItem.ToString(), out var size) && size > 0)
                {
                    _pageSize    = size;
                    _currentPage = 1;
                    ApplyPage();
                }
            };
            rightFlow.Controls.Add(_cmbPageSize);

            container.Controls.Add(navFlow, 0, 0);
            container.Controls.Add(rightFlow, 1, 0);
            footer.Controls.Add(container);
        }

        private void UpdatePaginationUi()
        {
            if (_lblPageInfo == null) return;

            int total = _filteredRows.Count;
            int pages = TotalPages();
            _currentPage = Math.Max(1, Math.Min(_currentPage, Math.Max(1, pages)));

            int selAll = 0;
            foreach (DataGridViewRow row in _grid.Rows)
                if (!row.IsNewRow && IsChecked(row)) selAll++;

            int disposed = 0, sold = 0;
            foreach (DataGridViewRow row in _filteredRows)
            {
                if (row.IsNewRow) continue;
                if (row.Tag is DisposedSoldItemDto dto)
                {
                    if (dto.IsDisposed) disposed++;
                    else if (dto.IsSold) sold++;
                }
            }

            _lblPageInfo.Text = $"Page {_currentPage} of {Math.Max(1, pages)}  ({total} item(s))";
            _lblCount.Text    = $"{total} item(s)  ·  {disposed} Disposed  ·  {sold} Sold  |  {selAll} selected";

            if (_btnFirstPage != null) _btnFirstPage.Enabled = _btnPrevPage.Enabled = _currentPage > 1;
            if (_btnNextPage  != null) _btnNextPage.Enabled  = _btnLastPage.Enabled = _currentPage < pages;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Data
        // ════════════════════════════════════════════════════════════════════

        private async Task LoadAsync()
        {
            if (_btnExport != null) _btnExport.Enabled = false;
            if (_lblCount  != null) _lblCount.Text = "Loading…";

            try
            {
                // Mirror DisposedSoldReportPage: pass date range + type filter
                DateTime? from = _dtpFrom?.Value.Date;
                DateTime? to   = _dtpTo?.Value.Date;
                string typeFilter = _cboType?.SelectedItem?.ToString();
                if (typeFilter == "All") typeFilter = null;

                _data = await Task.Run(() => _repo.GetReport(from, to, typeFilter));
                PopulateGrid();
            }
            catch (Exception ex)
            {
                if (_lblCount != null) _lblCount.Text = $"Error: {ex.Message}";
            }
        }

        private void PopulateGrid()
        {
            _grid.Columns.Clear();
            _grid.Rows.Clear();

            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name         = "chkSelect",
                HeaderText   = "✓",
                Width        = 36,
                ReadOnly     = false,
                FalseValue   = false,
                TrueValue    = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            });

            // Columns matching DisposedSoldReportPage / vw_DisposedSoldItems
            AddTextCol("DecidedAt",           "Date",          100);
            AddTextCol("DecisionTypeName",    "Type",           70);
            AddTextCol("ItemName",            "Item Name",      180);
            AddTextCol("ItemModelNumber",     "Model",          120);
            AddTextCol("SerialNumber",        "Serial #",       100);
            AddTextCol("CategoryName",        "Category",       100);
            AddTextCol("ConditionName",       "Condition",       90);
            AddTextCol("CartridgeBrand",      "Brand",          100);
            AddTextCol("CartridgeModelNumber","Cartridge Model", 130);
            AddTextCol("Quantity",            "Qty",             50, rightAlign: true);
            AddTextCol("RecipientName",       "Recipient",       140);
            AddTextCol("SaleAmount",          "Sale Amount",      90, rightAlign: true);
            AddTextCol("DisposalCompanyName", "Disposal Co.",    130);
            AddTextCol("VendorName",          "Vendor",          110);
            AddTextCol("DecidedByName",       "Decided By",      110);
            AddTextCol("Remarks",             "Remarks",         150);
            AddTextCol("_DecisionId",         "_DecisionId",       0);

            _grid.Columns["_DecisionId"].Visible = false;

            foreach (var item in _data)
            {
                int idx = _grid.Rows.Add();
                var r   = _grid.Rows[idx];
                r.Tag   = item;

                r.Cells["chkSelect"].Value           = false;
                r.Cells["DecidedAt"].Value           = item.DecidedAt.ToString("MM/dd/yyyy");
                r.Cells["DecisionTypeName"].Value    = item.DecisionTypeName ?? "";
                r.Cells["ItemName"].Value            = item.ItemName         ?? "";
                r.Cells["ItemModelNumber"].Value     = item.ItemModelNumber  ?? "";
                r.Cells["SerialNumber"].Value        = item.SerialNumber     ?? "";
                r.Cells["CategoryName"].Value        = item.CategoryName     ?? "";
                r.Cells["ConditionName"].Value       = item.ConditionName    ?? "";
                r.Cells["CartridgeBrand"].Value      = item.CartridgeBrand   ?? "";
                r.Cells["CartridgeModelNumber"].Value= item.CartridgeModelNumber ?? "";
                r.Cells["Quantity"].Value            = item.Quantity.ToString();
                r.Cells["RecipientName"].Value       = item.RecipientName    ?? "";
                r.Cells["SaleAmount"].Value          = item.SaleAmount?.ToString("N2") ?? "";
                r.Cells["DisposalCompanyName"].Value = item.DisposalCompanyName ?? "";
                r.Cells["VendorName"].Value          = item.VendorName         ?? "";
                r.Cells["DecidedByName"].Value       = item.DecidedByName      ?? "";
                r.Cells["Remarks"].Value             = item.Remarks            ?? "";
                r.Cells["_DecisionId"].Value         = item.DecisionId.ToString();
            }

            RebuildFilter();
            if (_btnExport != null) _btnExport.Enabled = true;
        }

        private void AddTextCol(string name, string header, int width, bool rightAlign = false)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name       = name,
                HeaderText = header,
                Width      = width,
                ReadOnly   = true,
                SortMode   = DataGridViewColumnSortMode.Automatic
            };
            if (rightAlign)
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns.Add(col);
        }

        // ════════════════════════════════════════════════════════════════════
        //  Filtering & pagination
        // ════════════════════════════════════════════════════════════════════

        private void RebuildFilter()
        {
            string q = _layout?.SearchBox?.Text.Trim().ToLowerInvariant() ?? "";
            _filteredRows.Clear();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                bool match = string.IsNullOrEmpty(q)
                    || CellContains(row, "ItemName",            q)
                    || CellContains(row, "ItemModelNumber",     q)
                    || CellContains(row, "SerialNumber",        q)
                    || CellContains(row, "CategoryName",        q)
                    || CellContains(row, "CartridgeBrand",      q)
                    || CellContains(row, "CartridgeModelNumber",q)
                    || CellContains(row, "RecipientName",       q)
                    || CellContains(row, "DisposalCompanyName", q)
                    || CellContains(row, "VendorName",          q)
                    || CellContains(row, "DecidedByName",       q)
                    || CellContains(row, "DecisionTypeName",    q)
                    || CellContains(row, "Remarks",             q);
                if (match) _filteredRows.Add(row);
            }
            _currentPage = 1;
            ApplyPage();
        }

        private void ApplyPage()
        {
            foreach (DataGridViewRow row in _grid.Rows)
                if (!row.IsNewRow) row.Visible = false;

            int total = _filteredRows.Count;
            int pages = TotalPages();
            _currentPage = Math.Max(1, Math.Min(_currentPage, Math.Max(1, pages)));

            int start = (_currentPage - 1) * _pageSize;
            int end   = Math.Min(start + _pageSize, total);
            for (int i = start; i < end; i++)
                _filteredRows[i].Visible = true;

            UpdatePaginationUi();
            _layout?.PaginationPanel?.PerformLayout();
        }

        private int TotalPages()
        {
            int total = _filteredRows.Count;
            return total == 0 ? 1 : (int)Math.Ceiling(total / (double)_pageSize);
        }

        // ════════════════════════════════════════════════════════════════════
        //  Events
        // ════════════════════════════════════════════════════════════════════

        private void OnCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_grid.Columns.Contains("chkSelect") && e.ColumnIndex == _grid.Columns["chkSelect"].Index)
                UpdatePaginationUi();
        }

        private void OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (!_grid.Columns.Contains("DecisionTypeName")) return;
            if (e.ColumnIndex != _grid.Columns["DecisionTypeName"].Index) return;

            string val = e.Value?.ToString() ?? "";
            switch (val.ToUpperInvariant())
            {
                case "DISPOSE":
                    e.CellStyle.ForeColor = Color.FromArgb(153, 27, 27);
                    e.CellStyle.BackColor = Color.FromArgb(254, 226, 226);
                    break;
                case "SELL":
                    e.CellStyle.ForeColor = Color.FromArgb(21, 128, 61);
                    e.CellStyle.BackColor = Color.FromArgb(220, 252, 231);
                    break;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Export
        // ════════════════════════════════════════════════════════════════════

        private void OnExportClick(object sender, EventArgs e)
        {
            var selected = new List<DisposedSoldItemDto>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                if (IsChecked(row) && row.Tag is DisposedSoldItemDto item)
                    selected.Add(item);
            }

            if (selected.Count == 0)
            {
                MessageBox.Show("Please select at least one item to export.",
                    "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                using (var dlg = new ExportCartridgeDisposedSoldPreviewDialog(selected))
                    dlg.ShowDialog(FindForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error preparing export:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════════════════

        private static bool IsChecked(DataGridViewRow row)
            => row.Cells["chkSelect"].Value is bool b && b;

        private static bool CellContains(DataGridViewRow row, string col, string q)
            => row.Cells[col].Value?.ToString().ToLowerInvariant().Contains(q) == true;
    }
}