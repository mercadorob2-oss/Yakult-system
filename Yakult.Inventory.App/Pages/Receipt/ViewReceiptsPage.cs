using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReaLTaiizor.Controls;
using ReaLTaiizor.Enum.Poison;
using ReaLTaiizor.Util;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Receipt
{
    public class ViewReceiptsPage : UserControl
    {
        private System.Windows.Forms.Panel _headerPanel;
        private System.Windows.Forms.Panel _buttonBarPanel;
        private System.Windows.Forms.Panel _subtitlePanel;
        private System.Windows.Forms.Panel _summaryPanel;
        private System.Windows.Forms.Panel _legendPanel;
        private ReaLTaiizor.Controls.Panel _bodyPanel;
        private MaterialCard _gridCard;
        private MaterialCard _cardTotal;
        private MaterialCard _cardComplete;
        private MaterialCard _cardPending;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Label _lblTotalCount;
        private Label _lblCompleteCount;
        private Label _lblPendingCount;
        private System.Windows.Forms.Button _btnAddReceipt;
        private HopeButton _btnOpenViewer;
        private HopeButton _btnGoToManageSets;
        private HopeButton _btnRefresh;
        private PoisonDataGridView _gridReceipts;
        private HopeTextBox _txtSearch;
        private ComboBox cmbFilterBy;
        private ComboBox cmbSortBy;
        private ComboBox _cboFilterStatus;
        private Label _lblFilterStatus;
        private System.Windows.Forms.Button _chipStatusAll;
        private System.Windows.Forms.Button _chipStatusComplete;
        private System.Windows.Forms.Button _chipStatusPending;
        private ComboBox _cboSupplierFilter;
        private Label _lblSupplierFilter;
        private DateTimePicker _dtpCreatedFrom;
        private DateTimePicker _dtpCreatedTo;
        private Label _lblCreatedRange;
        private Label _lblLegend;
        private System.Windows.Forms.Panel _paginationPanel;
        private System.Windows.Forms.Button _btnFirstPage;
        private System.Windows.Forms.Button _btnPrevPage;
        private System.Windows.Forms.Button _btnNextPage;
        private System.Windows.Forms.Button _btnLastPage;
        private Label _lblPageInfo;
        private Label _lblShowingInfo;
        private ComboBox _cboPageSize;
        private HopeButton _btnClearFilters;

        private System.Windows.Forms.Panel _emptyStatePanel;
        private Label _lblEmptyTitle;
        private Label _lblEmptySubtitle;
        private HopeButton _btnEmptyAction;
        private EmptyStateActionMode _emptyStateActionMode = EmptyStateActionMode.AddReceipt;

        private ContextMenuStrip _rowMenu;

        private System.Windows.Forms.Panel _loadingPanel;
        private ProgressBar _loadingProgress;
        private Label _lblLoading;

        private System.Windows.Forms.Timer _stateSaveTimer;
        private bool _restoringState;
        private string _restoreSortKey = string.Empty;
        private System.Windows.Forms.SortOrder _restoreSortOrder = System.Windows.Forms.SortOrder.None;
        private bool _isFilteringUi;
        private string _restoreSupplier = string.Empty;

        private enum EmptyStateActionMode
        {
            AddReceipt,
            ClearFilters
        }

        private readonly ReceiptSetRepository _repository;
        private List<ReceiptSetDto> _allReceipts;
        private List<ReceiptSetDto> _filteredReceipts;
        private int _currentPage = 1;
        private int _pageSize = 10;
        private System.Windows.Forms.Timer _debounceTimer;

        // Sorting state
        private DataGridViewColumn _sortColumn;
        private System.Windows.Forms.SortOrder _sortOrder = System.Windows.Forms.SortOrder.None;
        private bool _sortByDropdownInitialized = false;
        private bool _suppressApplySearchFilter = false;

        // Checkbox selection (for bulk actions like PDF export)
        private readonly HashSet<int> _checkedReceiptSetIds = new HashSet<int>();
        private bool _suppressCheckboxSync;

        public event Action GoToManageSetsRequested;

        public ViewReceiptsPage()
        {
            _repository = new ReceiptSetRepository();
            _debounceTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                ApplySearchFilter();
            };
            InitializeStatePersistence();
            BuildUi();
            ApplyTheme();
            RestoreUiState();
            LoadReceiptSets();
        }

        private void InitializeStatePersistence()
        {
            _stateSaveTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _stateSaveTimer.Tick += (s, e) =>
            {
                _stateSaveTimer.Stop();
                SaveUiState();
            };
        }

        private void RestoreUiState()
        {
            _restoringState = true;
            _suppressApplySearchFilter = true;
            try
            {
                var settings = Yakult.Inventory.App.Properties.Settings.Default;

                if (_cboFilterStatus != null && settings.ViewReceipts_StatusIndex >= 0 && settings.ViewReceipts_StatusIndex < _cboFilterStatus.Items.Count)
                    _cboFilterStatus.SelectedIndex = settings.ViewReceipts_StatusIndex;
                UpdateStatusChips();

                if (settings.ViewReceipts_PageSize > 0)
                    _pageSize = settings.ViewReceipts_PageSize;

                if (_txtSearch != null)
                    _txtSearch.Text = settings.ViewReceipts_SearchText ?? string.Empty;

                if (cmbFilterBy != null && settings.ViewReceipts_FilterByIndex >= 0 && settings.ViewReceipts_FilterByIndex < cmbFilterBy.Items.Count)
                    cmbFilterBy.SelectedIndex = settings.ViewReceipts_FilterByIndex;

                _restoreSupplier = (settings.ViewReceipts_Supplier ?? string.Empty).Trim();

                if (_dtpCreatedFrom != null)
                {
                    _dtpCreatedFrom.Value = settings.ViewReceipts_CreatedFrom == default(DateTime) ? DateTime.Today : settings.ViewReceipts_CreatedFrom;
                    _dtpCreatedFrom.Checked = settings.ViewReceipts_CreatedFromEnabled;
                }

                if (_dtpCreatedTo != null)
                {
                    _dtpCreatedTo.Value = settings.ViewReceipts_CreatedTo == default(DateTime) ? DateTime.Today : settings.ViewReceipts_CreatedTo;
                    _dtpCreatedTo.Checked = settings.ViewReceipts_CreatedToEnabled;
                }

                if (_cboPageSize != null)
                {
                    _cboPageSize.SelectedItem = _pageSize.ToString();
                    if (_cboPageSize.SelectedItem == null) _cboPageSize.SelectedIndex = 0;
                    if (_cboPageSize.SelectedItem != null && int.TryParse(_cboPageSize.SelectedItem.ToString(), out int rows) && rows > 0)
                        _pageSize = rows;
                }

                _restoreSortKey = settings.ViewReceipts_SortKey ?? string.Empty;
                var order = settings.ViewReceipts_SortOrder;
                _restoreSortOrder = order == (int)System.Windows.Forms.SortOrder.Ascending
                    ? System.Windows.Forms.SortOrder.Ascending
                    : order == (int)System.Windows.Forms.SortOrder.Descending
                        ? System.Windows.Forms.SortOrder.Descending
                        : System.Windows.Forms.SortOrder.None;

                // Better default: Created At (DESC)
                if (string.IsNullOrWhiteSpace(_restoreSortKey) || _restoreSortOrder == System.Windows.Forms.SortOrder.None)
                {
                    _restoreSortKey = "CreatedAt";
                    _restoreSortOrder = System.Windows.Forms.SortOrder.Descending;
                }
            }
            catch
            {
                // ignore restore errors
            }
            finally
            {
                _suppressApplySearchFilter = false;
                _restoringState = false;
                UpdateClearFiltersButtonVisibility();
            }
        }

        private void ScheduleSaveState()
        {
            if (_restoringState)
                return;

            _stateSaveTimer?.Stop();
            _stateSaveTimer?.Start();
        }

        private void SaveUiState()
        {
            if (_restoringState)
                return;

            try
            {
                var settings = Yakult.Inventory.App.Properties.Settings.Default;

                if (_cboFilterStatus != null) settings.ViewReceipts_StatusIndex = _cboFilterStatus.SelectedIndex;
                settings.ViewReceipts_PageSize = _pageSize;

                settings.ViewReceipts_SearchText = _txtSearch?.Text ?? string.Empty;
                settings.ViewReceipts_FilterByIndex = cmbFilterBy?.SelectedIndex ?? 0;
                settings.ViewReceipts_Supplier = _cboSupplierFilter?.SelectedItem?.ToString() ?? string.Empty;

                settings.ViewReceipts_CreatedFromEnabled = _dtpCreatedFrom?.Checked ?? false;
                settings.ViewReceipts_CreatedFrom = _dtpCreatedFrom?.Value ?? default(DateTime);
                settings.ViewReceipts_CreatedToEnabled = _dtpCreatedTo?.Checked ?? false;
                settings.ViewReceipts_CreatedTo = _dtpCreatedTo?.Value ?? default(DateTime);

                settings.ViewReceipts_SortKey = GetCurrentSortKey() ?? string.Empty;
                settings.ViewReceipts_SortOrder = (int)_sortOrder;

                settings.Save();
            }
            catch
            {
                // ignore save errors (config not writable)
            }
        }

        private string GetCurrentSortKey()
        {
            if (_sortColumn == null || _sortOrder == System.Windows.Forms.SortOrder.None)
                return string.Empty;

            return !string.IsNullOrWhiteSpace(_sortColumn.DataPropertyName)
                ? _sortColumn.DataPropertyName
                : _sortColumn.Name;
        }

        private void SetLoading(bool isLoading, string message)
        {
            if (_loadingPanel == null)
                return;

            if (isLoading)
            {
                _lblLoading.Text = string.IsNullOrWhiteSpace(message) ? "Loading…" : message;
                _loadingPanel.Visible = true;
                _loadingPanel.BringToFront();
                _loadingProgress.Style = ProgressBarStyle.Marquee;
                UseWaitCursor = true;

                if (_btnRefresh != null) _btnRefresh.Enabled = false;
                if (_btnAddReceipt != null) _btnAddReceipt.Enabled = false;
                if (_btnOpenViewer != null) _btnOpenViewer.Enabled = false;
                if (_btnGoToManageSets != null) _btnGoToManageSets.Enabled = false;
                if (_btnClearFilters != null) _btnClearFilters.Enabled = false;
                if (_cboFilterStatus != null) _cboFilterStatus.Enabled = false;
                if (_chipStatusAll != null) _chipStatusAll.Enabled = false;
                if (_chipStatusComplete != null) _chipStatusComplete.Enabled = false;
                if (_chipStatusPending != null) _chipStatusPending.Enabled = false;
                if (_cboSupplierFilter != null) _cboSupplierFilter.Enabled = false;
                if (_dtpCreatedFrom != null) _dtpCreatedFrom.Enabled = false;
                if (_dtpCreatedTo != null) _dtpCreatedTo.Enabled = false;
                if (_cboPageSize != null) _cboPageSize.Enabled = false;
                if (_txtSearch != null) _txtSearch.Enabled = false;
                if (cmbFilterBy != null) cmbFilterBy.Enabled = false;
                if (cmbSortBy != null) cmbSortBy.Enabled = false;
            }
            else
            {
                _loadingPanel.Visible = false;
                _loadingProgress.Style = ProgressBarStyle.Blocks;
                UseWaitCursor = false;

                if (_btnRefresh != null) _btnRefresh.Enabled = true;
                if (_btnAddReceipt != null) _btnAddReceipt.Enabled = true;
                if (_btnOpenViewer != null) _btnOpenViewer.Enabled = true;
                if (_btnGoToManageSets != null) _btnGoToManageSets.Enabled = true;
                if (_btnClearFilters != null) _btnClearFilters.Enabled = true;
                if (_cboFilterStatus != null) _cboFilterStatus.Enabled = true;
                if (_chipStatusAll != null) _chipStatusAll.Enabled = true;
                if (_chipStatusComplete != null) _chipStatusComplete.Enabled = true;
                if (_chipStatusPending != null) _chipStatusPending.Enabled = true;
                if (_cboSupplierFilter != null) _cboSupplierFilter.Enabled = true;
                if (_dtpCreatedFrom != null) _dtpCreatedFrom.Enabled = true;
                if (_dtpCreatedTo != null) _dtpCreatedTo.Enabled = true;
                if (_cboPageSize != null) _cboPageSize.Enabled = true;
                if (_txtSearch != null) _txtSearch.Enabled = true;
                if (cmbFilterBy != null) cmbFilterBy.Enabled = true;
                if (cmbSortBy != null) cmbSortBy.Enabled = true;
            }
        }

        private void TriggerDebouncedFilter()
        {
            if (_suppressApplySearchFilter)
                return;

            _debounceTimer?.Stop();
            _debounceTimer?.Start();
        }

        private void MakePill(Control control)
        {
            if (control == null || control.Width <= 0 || control.Height <= 0)
                return;

            int radius = control.Height;
            using (var path = new GraphicsPath())
            {
                path.AddArc(0, 0, radius, radius, 90, 180);
                path.AddArc(control.Width - radius, 0, radius, radius, 270, 180);
                path.CloseAllFigures();
                control.Region = new Region(path);
            }
        }

        private void ConfigurePillHopeButton(HopeButton button, Color baseColor, Color hoverColor)
        {
            if (button == null)
                return;

            button.ButtonType = HopeButtonType.Primary;
            button.PrimaryColor = baseColor;
            button.DefaultColor = baseColor;
            button.BorderColor = baseColor;
            button.TextColor = Color.White;
            button.HoverTextColor = Color.White;
            button.Cursor = Cursors.Hand;

            button.MouseEnter += (s, e) =>
            {
                button.PrimaryColor = hoverColor;
                button.DefaultColor = hoverColor;
                button.BorderColor = hoverColor;
                button.Invalidate();
            };

            button.MouseLeave += (s, e) =>
            {
                button.PrimaryColor = baseColor;
                button.DefaultColor = baseColor;
                button.BorderColor = baseColor;
                button.Invalidate();
            };

            button.Resize += (s, e) => MakePill(button);
            MakePill(button);
        }

        private void ConfigureOutlineHopeButton(HopeButton button, Color borderColor, Color hoverBackColor)
        {
            if (button == null)
                return;

            button.ButtonType = HopeButtonType.Primary;
            button.PrimaryColor = Color.White;
            button.DefaultColor = Color.White;
            button.BorderColor = borderColor;
            button.TextColor = borderColor;
            button.HoverTextColor = borderColor;
            button.Cursor = Cursors.Hand;

            button.Paint += (s, e) =>
            {
                var b = s as Control;
                if (b == null || b.Width <= 1 || b.Height <= 1)
                    return;

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(borderColor, 2.2f))
                using (var path = new GraphicsPath())
                {
                    pen.Alignment = PenAlignment.Inset;

                    // Inset so the border doesn't get clipped
                    var rect = new Rectangle(1, 1, b.Width - 3, b.Height - 3);
                    int radius = Math.Max(8, rect.Height);
                    int d = radius;

                    // Left arc
                    path.AddArc(rect.X, rect.Y, d, d, 90, 180);
                    // Bottom line
                    path.AddLine(rect.X + (d / 2), rect.Bottom, rect.Right - (d / 2), rect.Bottom);
                    // Right arc
                    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180);
                    // Top line
                    path.AddLine(rect.Right - (d / 2), rect.Y, rect.X + (d / 2), rect.Y);
                    path.CloseFigure();

                    e.Graphics.DrawPath(pen, path);
                }
            };

            button.MouseEnter += (s, e) =>
            {
                button.PrimaryColor = hoverBackColor;
                button.DefaultColor = hoverBackColor;
                button.BorderColor = borderColor;
                button.TextColor = borderColor;
                button.Invalidate();
            };

            button.MouseLeave += (s, e) =>
            {
                button.PrimaryColor = Color.White;
                button.DefaultColor = Color.White;
                button.BorderColor = borderColor;
                button.TextColor = borderColor;
                button.Invalidate();
            };

            button.Resize += (s, e) => MakePill(button);
            MakePill(button);
        }

        private void ConfigureGhostHopeButton(HopeButton button, Color foreColor, Color hoverBackColor)
        {
            if (button == null)
                return;

            button.ButtonType = HopeButtonType.Primary;
            button.PrimaryColor = Color.White;
            button.DefaultColor = Color.White;
            button.BorderColor = Color.White;
            button.TextColor = foreColor;
            button.HoverTextColor = foreColor;
            button.Cursor = Cursors.Hand;

            button.MouseEnter += (s, e) =>
            {
                button.PrimaryColor = hoverBackColor;
                button.DefaultColor = hoverBackColor;
                button.BorderColor = hoverBackColor;
                button.TextColor = foreColor;
                button.Invalidate();
            };

            button.MouseLeave += (s, e) =>
            {
                button.PrimaryColor = Color.White;
                button.DefaultColor = Color.White;
                button.BorderColor = Color.White;
                button.TextColor = foreColor;
                button.Invalidate();
            };

            button.Resize += (s, e) => MakePill(button);
            MakePill(button);
        }

        private sealed class ChipPaintState
        {
            public bool Active { get; set; }
            public bool Hover { get; set; }
        }

        private sealed class PrimaryPillPaintState
        {
            public bool Hover { get; set; }
        }

        private void ConfigurePrimaryPillButton(System.Windows.Forms.Button button)
        {
            if (button == null)
                return;

            var accent = Color.FromArgb(52, 152, 219);
            var hover = Color.FromArgb(41, 128, 185);

            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            // Keep the control background neutral so rounded corners are visible against the parent.
            button.FlatAppearance.MouseOverBackColor = Color.White;
            button.FlatAppearance.MouseDownBackColor = Color.White;
            button.UseVisualStyleBackColor = false;
            button.BackColor = Color.White;
            button.ForeColor = Color.White;
            button.Cursor = Cursors.Hand;
            button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Padding = new Padding(14, 0, 14, 0);
            button.TabStop = false;
            button.Tag = new PrimaryPillPaintState();

            button.MouseEnter += (s, e) =>
            {
                if (!(button.Tag is PrimaryPillPaintState st)) return;
                st.Hover = true;
                button.Invalidate();
            };
            button.MouseLeave += (s, e) =>
            {
                if (!(button.Tag is PrimaryPillPaintState st)) return;
                st.Hover = false;
                button.Invalidate();
            };

            button.Paint += (s, e) =>
            {
                if (!(button.Tag is PrimaryPillPaintState st))
                    return;

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = button.ClientRectangle;
                rect.Inflate(-1, -1);
                if (rect.Width <= 2 || rect.Height <= 2)
                    return;

                int d = Math.Max(14, rect.Height);
                using (var path = new GraphicsPath())
                {
                    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();

                    var back = !button.Enabled
                        ? Color.FromArgb(200, 210, 210, 210)
                        : st.Hover ? hover : accent;

                    using (var b = new SolidBrush(back))
                        e.Graphics.FillPath(b, path);

                    TextRenderer.DrawText(
                        e.Graphics,
                        button.Text,
                        button.Font,
                        button.ClientRectangle,
                        button.Enabled ? Color.White : Color.FromArgb(240, 255, 255, 255),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            };
        }

        private void ConfigureStatusChip(System.Windows.Forms.Button button)
        {
            if (button == null)
                return;

            var accent = Color.FromArgb(52, 152, 219);
            var hover = Color.FromArgb(232, 244, 253);

            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0; // custom paint
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            button.BackColor = Color.White;
            button.ForeColor = accent;
            button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Padding = new Padding(12, 0, 12, 0);
            button.TabStop = false;
            button.Tag = new ChipPaintState();

            button.MouseEnter += (s, e) =>
            {
                if (!(button.Tag is ChipPaintState st)) return;
                st.Hover = true;
                button.Invalidate();
            };
            button.MouseLeave += (s, e) =>
            {
                if (!(button.Tag is ChipPaintState st)) return;
                st.Hover = false;
                button.Invalidate();
            };

            button.Paint += (s, e) =>
            {
                if (!(button.Tag is ChipPaintState st))
                    return;

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = button.ClientRectangle;
                rect.Inflate(-1, -1);
                if (rect.Width <= 2 || rect.Height <= 2)
                    return;

                int d = Math.Max(12, rect.Height); // diameter for corner arcs (pill-like)
                using (var path = new GraphicsPath())
                {
                    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();

                    Color back;
                    Color text;
                    Color border;

                    if (!button.Enabled)
                    {
                        back = Color.White;
                        text = Color.FromArgb(150, accent);
                        border = Color.FromArgb(180, 220, 220, 220);
                    }
                    else if (st.Active)
                    {
                        back = accent;
                        text = Color.White;
                        border = accent;
                    }
                    else if (st.Hover)
                    {
                        back = hover;
                        text = accent;
                        border = accent;
                    }
                    else
                    {
                        back = Color.White;
                        text = accent;
                        border = accent;
                    }

                    using (var b = new SolidBrush(back))
                        e.Graphics.FillPath(b, path);

                    using (var pen = new Pen(border, 1.2f))
                        e.Graphics.DrawPath(pen, path);

                    TextRenderer.DrawText(
                        e.Graphics,
                        button.Text,
                        button.Font,
                        button.ClientRectangle,
                        text,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            };
        }

        private void UpdateStatusChips()
        {
            int selected = _cboFilterStatus?.SelectedIndex ?? 0;

            ApplyStatusChipStyle(_chipStatusAll, selected == 0);
            ApplyStatusChipStyle(_chipStatusComplete, selected == 1);
            ApplyStatusChipStyle(_chipStatusPending, selected == 2);
        }

        private static void ApplyStatusChipStyle(System.Windows.Forms.Button button, bool isActive)
        {
            if (button == null)
                return;

            if (button.Tag is ChipPaintState st)
            {
                st.Active = isActive;
                button.Invalidate();
            }
        }

        private static bool HasAttachment(byte[] bytes, string path)
        {
            return (bytes != null && bytes.Length > 0) || !string.IsNullOrWhiteSpace(path);
        }

        private static bool IsReceiptComplete(ReceiptSetDto dto)
        {
            if (dto == null)
                return false;

            bool hasSi = HasAttachment(dto.SiImage, dto.SiImagePath);
            bool hasDr = HasAttachment(dto.DrImage, dto.DrImagePath);
            bool hasPo = HasAttachment(dto.PoImage, dto.PoImagePath);

            return hasSi && hasDr && hasPo;
        }

        private static string GetReceiptStatusText(ReceiptSetDto dto)
        {
            return IsReceiptComplete(dto) ? "Complete" : "Pending";
        }

        private MaterialCard CreateSummaryCard(string title, Color valueColor, out Label valueLabel)
        {
            var card = new MaterialCard
            {
                Size = new Size(210, 70),
                BackColor = Color.White,
                Padding = new Padding(12, 10, 12, 10),
                Margin = new Padding(0, 0, 10, 0)
            };

            var lblTitle = new Label
            {
                AutoSize = true,
                Text = title,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(90, 90, 90),
                Location = new Point(12, 10)
            };

            valueLabel = new Label
            {
                AutoSize = true,
                Text = "0",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = valueColor,
                Location = new Point(12, 32)
            };

            card.Controls.Add(lblTitle);
            card.Controls.Add(valueLabel);
            return card;
        }

        private void EnsureGridHeaderVisible()
        {
            if (_gridReceipts == null)
                return;

            _gridReceipts.ColumnHeadersVisible = true;
            _gridReceipts.EnableHeadersVisualStyles = false;
            _gridReceipts.ColumnHeadersHeight = 40;
            _gridReceipts.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _gridReceipts.Invalidate();
        }

        private void UpdatePagination()
        {
            if (_gridReceipts == null)
                return;

            var data = _filteredReceipts ?? new List<ReceiptSetDto>();
            if (data.Count == 0)
            {
                ShowEmptyState();
                _gridReceipts.DataSource = new List<ReceiptSetDto>();
                if (_lblPageInfo != null)
                    _lblPageInfo.Text = "Page 0 of 0";
                if (_lblShowingInfo != null)
                    _lblShowingInfo.Text = "Showing 0 of 0";
                if (_btnFirstPage != null) _btnFirstPage.Enabled = false;
                if (_btnPrevPage != null) _btnPrevPage.Enabled = false;
                if (_btnNextPage != null) _btnNextPage.Enabled = false;
                if (_btnLastPage != null) _btnLastPage.Enabled = false;
                EnsureGridHeaderVisible();
                return;
            }

            HideEmptyState();

            int totalPages = (int)Math.Ceiling((double)data.Count / _pageSize);
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1) _currentPage = 1;

            var pagedData = data
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            _gridReceipts.DataSource = pagedData;
            EnableSortingGlyphs();
            _gridReceipts.ClearSelection();

            // Initialize Sort By dropdown after first load (when columns are available)
            EnsureSortByDropdownInitialized(forceReset: false);

            if (_lblPageInfo != null)
                _lblPageInfo.Text = $"Page {_currentPage} of {totalPages}";

            if (_lblShowingInfo != null)
            {
                int start = ((_currentPage - 1) * _pageSize) + 1;
                int end = start + pagedData.Count - 1;
                if (pagedData.Count == 0)
                {
                    start = 0;
                    end = 0;
                }
                _lblShowingInfo.Text = $"Showing {start}-{end} of {data.Count}";
            }

            if (_btnFirstPage != null) _btnFirstPage.Enabled = _currentPage > 1;
            if (_btnPrevPage != null) _btnPrevPage.Enabled = _currentPage > 1;
            if (_btnNextPage != null) _btnNextPage.Enabled = _currentPage < totalPages;
            if (_btnLastPage != null) _btnLastPage.Enabled = _currentPage < totalPages;

            EnsureGridHeaderVisible();
        }

        private List<ReceiptSetDto> GetSelectedRowDtos()
        {
            if (_gridReceipts == null || _gridReceipts.SelectedRows == null)
                return new List<ReceiptSetDto>();

            var list = new List<(int Index, ReceiptSetDto Dto)>();
            foreach (DataGridViewRow row in _gridReceipts.SelectedRows)
            {
                if (row?.DataBoundItem is ReceiptSetDto dto)
                {
                    list.Add((row.Index, dto));
                }
            }

            // Stable order (top-to-bottom in current view)
            return list.OrderBy(t => t.Index).Select(t => t.Dto).ToList();
        }

        private List<int> GetMarkedReceiptSetIds()
        {
            if (_checkedReceiptSetIds.Count > 0)
                return _checkedReceiptSetIds.OrderBy(x => x).ToList();

            return GetSelectedRowDtos()
                .Select(d => d.ReceiptSetId)
                .Distinct()
                .ToList();
        }

        private List<ReceiptSetDto> ResolveReceiptSetDtosForIds(List<int> receiptSetIds)
        {
            if (receiptSetIds == null || receiptSetIds.Count == 0)
                return new List<ReceiptSetDto>();

            var map = new Dictionary<int, ReceiptSetDto>();
            if (_allReceipts != null)
            {
                foreach (var dto in _allReceipts)
                {
                    if (dto == null)
                        continue;

                    if (!map.ContainsKey(dto.ReceiptSetId))
                        map[dto.ReceiptSetId] = dto;
                }
            }

            var list = new List<ReceiptSetDto>(receiptSetIds.Count);
            foreach (var id in receiptSetIds)
            {
                if (map.TryGetValue(id, out var dto))
                {
                    list.Add(dto);
                }
                else
                {
                    // Fallback: attempt to pull from current grid binding if available.
                    if (_gridReceipts != null && _gridReceipts.Rows != null)
                    {
                        foreach (DataGridViewRow row in _gridReceipts.Rows)
                        {
                            if (row?.DataBoundItem is ReceiptSetDto gridDto && gridDto.ReceiptSetId == id)
                            {
                                list.Add(gridDto);
                                break;
                            }
                        }
                    }
                }
            }

            return list.Where(x => x != null).ToList();
        }

        private void EnsureSortByDropdownInitialized(bool forceReset)
        {
            if (!forceReset && _sortByDropdownInitialized)
                return;

            if (cmbSortBy == null || _gridReceipts == null || _gridReceipts.Columns == null || _gridReceipts.Columns.Count == 0)
                return;

            var defaultSortKey = "CreatedAt";

            DefaultListPageTemplate.SetupSortByDropdown(
                cmbSortBy,
                _gridReceipts,
                (columnKey, direction) =>
                {
                    var col = _gridReceipts.Columns.Cast<DataGridViewColumn>()
                        .FirstOrDefault(c => c.DataPropertyName == columnKey || c.Name == columnKey);

                    if (col == null)
                        return;

                    _sortColumn = col;
                    _sortOrder = direction;

                    var propertyName = !string.IsNullOrWhiteSpace(col.DataPropertyName)
                        ? col.DataPropertyName
                        : col.Name;

                    if (cmbFilterBy != null && cmbFilterBy.SelectedIndex != 0)
                        cmbFilterBy.SelectedIndex = 0;

                    var sortDir = direction == System.Windows.Forms.SortOrder.Ascending
                        ? System.ComponentModel.ListSortDirection.Ascending
                        : System.ComponentModel.ListSortDirection.Descending;

                    SortDataSource(propertyName, sortDir);
                    UpdatePagination();

                    foreach (DataGridViewColumn c in _gridReceipts.Columns)
                        c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;

                    col.HeaderCell.SortGlyphDirection = direction;
                    _gridReceipts.Invalidate();

                    UpdateClearFiltersButtonVisibility();
                    ScheduleSaveState();
                },
                defaultColumnKey: defaultSortKey,
                defaultDirection: System.Windows.Forms.SortOrder.Descending
            );

            _sortByDropdownInitialized = true;
        }

        private void BtnFirstPage_Click(object sender, EventArgs e)
        {
            _currentPage = 1;
            UpdatePagination();
        }

        private void BtnPrevPage_Click(object sender, EventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                UpdatePagination();
            }
        }

        private void BtnNextPage_Click(object sender, EventArgs e)
        {
            if (_filteredReceipts == null)
                return;

            int totalPages = (int)Math.Ceiling((double)_filteredReceipts.Count / _pageSize);
            if (_currentPage < totalPages)
            {
                _currentPage++;
                UpdatePagination();
            }
        }

        private void BtnLastPage_Click(object sender, EventArgs e)
        {
            if (_filteredReceipts == null)
                return;

            int totalPages = (int)Math.Ceiling((double)_filteredReceipts.Count / _pageSize);
            _currentPage = totalPages;
            UpdatePagination();
        }

        private static Control CreateLegendChip(string text, Color backColor, Color foreColor)
        {
            var chip = new System.Windows.Forms.Panel
            {
                AutoSize = true,
                BackColor = backColor,
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0, 0, 8, 0)
            };

            var lbl = new Label
            {
                AutoSize = true,
                Text = text,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = foreColor,
                BackColor = Color.Transparent
            };

            chip.Controls.Add(lbl);
            chip.Resize += (s, e) =>
            {
                if (chip.Width <= 0 || chip.Height <= 0)
                    return;

                int radius = chip.Height;
                using (var path = new GraphicsPath())
                {
                    path.AddArc(0, 0, radius, radius, 90, 180);
                    path.AddArc(chip.Width - radius, 0, radius, radius, 270, 180);
                    path.CloseAllFigures();
                    chip.Region = new Region(path);
                }
            };

            // Apply shape initially
            chip.PerformLayout();
            chip.Width = chip.PreferredSize.Width;
            chip.Height = Math.Max(chip.Height, chip.PreferredSize.Height);
            chip.Invalidate();
            return chip;
        }

        private void ApplyRoundedButtonStyle(System.Windows.Forms.Button button, bool primary = false)
        {
            if (button == null)
                return;

            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;

            Color backColor;
            Color foreColor;
            Color borderColor;

            if (primary)
            {
                backColor = Color.FromArgb(52, 152, 219);
                foreColor = Color.White;
                borderColor = Color.FromArgb(41, 128, 185);
            }
            else
            {
                backColor = Color.White;
                foreColor = Color.FromArgb(41, 128, 185);
                borderColor = Color.FromArgb(41, 128, 185);
            }

            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.FlatAppearance.BorderColor = borderColor;

            button.Resize += (s, e) =>
            {
                var rect = button.ClientRectangle;
                rect.Inflate(-1, -1);
                int radius = 8;

                using (var path = new GraphicsPath())
                {
                    path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                    path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
                    path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
                    path.CloseFigure();
                    button.Region = new Region(path);
                }
            };
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            // Header panel
            _headerPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(12, 10, 12, 10)
            };

            _titleLabel = new Label
            {
                AutoSize = true,
                Text = "Receipt Sets",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Location = new Point(15, 10)
            };

            _txtSearch = new HopeTextBox
            {
                BackColor = Color.White,
                BaseColor = Color.White,
                BorderColorA = Color.FromArgb(220, 220, 220),
                BorderColorB = Color.FromArgb(220, 220, 220),
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(60, 60, 60),
                Hint = "Search by Set Code, Supplier, SI/DR/PO...",
                Location = new Point(18, 54),
                Size = new Size(320, 32),
                MaxLength = 32767,
                Multiline = false,
                UseSystemPasswordChar = false
            };
            _txtSearch.TextChanged += (s, e) => TriggerDebouncedFilter();
            _txtSearch.TextChanged += (s, e) => UpdateClearFiltersButtonVisibility();
            _txtSearch.TextChanged += (s, e) => ScheduleSaveState();

            var lblFilterBy = new Label
            {
                Text = "Filter By:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(60, 60, 60),
                Margin = new Padding(14, 8, 6, 0)
            };

            cmbFilterBy = new ComboBox
            {
                Width = 160,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 4, 8, 0)
            };
            cmbFilterBy.Items.AddRange(new object[] { "Default", "Most Recently Added", "Oldest Added" });
            cmbFilterBy.SelectedIndex = 0;
            cmbFilterBy.SelectedIndexChanged += CmbFilterBy_SelectedIndexChanged;
            cmbFilterBy.SelectedIndexChanged += (s, e) => UpdateClearFiltersButtonVisibility();
            cmbFilterBy.SelectedIndexChanged += (s, e) => ScheduleSaveState();

            var lblSortBy = new Label
            {
                Text = "Sort By:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(60, 60, 60),
                Margin = new Padding(10, 8, 6, 0)
            };

            cmbSortBy = new ComboBox
            {
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 4, 8, 0)
            };
            cmbSortBy.SelectedIndexChanged += (s, e) => UpdateClearFiltersButtonVisibility();
            cmbSortBy.SelectedIndexChanged += (s, e) => ScheduleSaveState();

            _lblSupplierFilter = new Label
            {
                AutoSize = true,
                Text = "Supplier:",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(60, 60, 60),
                Margin = new Padding(10, 8, 6, 0)
            };

            _cboSupplierFilter = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 220,
                Margin = new Padding(0, 4, 8, 0)
            };
            _cboSupplierFilter.Items.Add("All Suppliers");
            _cboSupplierFilter.SelectedIndex = 0;
            _cboSupplierFilter.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressApplySearchFilter) return;
                ApplySearchFilter();
                UpdateClearFiltersButtonVisibility();
                ScheduleSaveState();
            };

            _btnClearFilters = new HopeButton
            {
                Text = "Clear",
                Size = new Size(90, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ButtonType = HopeButtonType.Primary,
                Margin = new Padding(6, 4, 0, 0)
            };
            _btnClearFilters.Click += (s, e) => ClearAllFilters();
            ConfigureOutlineHopeButton(_btnClearFilters, Color.FromArgb(130, 130, 130), Color.FromArgb(245, 247, 250));

            var headerRow = new FlowLayoutPanel
            {
                Location = new Point(18, 54),
                Height = 38,
                Width = Math.Max(0, _headerPanel.Width - 36),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _headerPanel.SizeChanged += (s, e) =>
            {
                if (headerRow != null)
                    headerRow.Width = Math.Max(0, _headerPanel.Width - 36);
            };

            _txtSearch.Margin = new Padding(0, 2, 8, 0);
            headerRow.Controls.Add(_txtSearch);
            headerRow.Controls.Add(lblFilterBy);
            headerRow.Controls.Add(cmbFilterBy);
            headerRow.Controls.Add(lblSortBy);
            headerRow.Controls.Add(cmbSortBy);
            headerRow.Controls.Add(_lblSupplierFilter);
            headerRow.Controls.Add(_cboSupplierFilter);
            headerRow.Controls.Add(_btnClearFilters);

            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(headerRow);

            // Button bar panel (same header background)
            _buttonBarPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 94,
                Padding = new Padding(12, 0, 12, 10),
                BackColor = Color.FromArgb(245, 247, 250)
            };

            var filterCard = new MaterialCard
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(14, 12, 14, 12)
            };

            var filterRowHost = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            filterRowHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            filterRowHost.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filterRowHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var filterRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            _lblFilterStatus = new Label
            {
                AutoSize = true,
                Text = "Status",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60),
                Margin = new Padding(0, 14, 6, 0)
            };

            _cboFilterStatus = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 160,
                Margin = new Padding(0, 10, 14, 0)
            };
            _cboFilterStatus.Items.AddRange(new object[]
            {
                "All",
                "Complete",
                "Pending"
            });
            _cboFilterStatus.SelectedIndex = 0;
            _cboFilterStatus.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressApplySearchFilter) return;
                ApplySearchFilter();
                UpdateClearFiltersButtonVisibility();
                UpdateStatusChips();
                ScheduleSaveState();
            };

            // Status chips (replace dropdown in UI; dropdown remains for persistence/state)
            _chipStatusAll = new System.Windows.Forms.Button
            {
                Text = "All",
                Size = new Size(70, 34),
                Margin = new Padding(0, 10, 8, 0)
            };
            _chipStatusAll.Click += (s, e) => { if (_cboFilterStatus != null) _cboFilterStatus.SelectedIndex = 0; };
            ConfigureStatusChip(_chipStatusAll);

            _chipStatusComplete = new System.Windows.Forms.Button
            {
                Text = "Complete",
                Size = new Size(96, 34),
                Margin = new Padding(0, 10, 8, 0)
            };
            _chipStatusComplete.Click += (s, e) => { if (_cboFilterStatus != null) _cboFilterStatus.SelectedIndex = 1; };
            ConfigureStatusChip(_chipStatusComplete);

            _chipStatusPending = new System.Windows.Forms.Button
            {
                Text = "Pending",
                Size = new Size(90, 34),
                Margin = new Padding(0, 10, 14, 0)
            };
            _chipStatusPending.Click += (s, e) => { if (_cboFilterStatus != null) _cboFilterStatus.SelectedIndex = 2; };
            ConfigureStatusChip(_chipStatusPending);

            _lblCreatedRange = new Label
            {
                AutoSize = true,
                Text = "Created",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60),
                Margin = new Padding(0, 14, 6, 0)
            };

            _dtpCreatedFrom = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                ShowCheckBox = true,
                Checked = false,
                Width = 140,
                Margin = new Padding(0, 10, 8, 0)
            };
            _dtpCreatedFrom.ValueChanged += (s, e) =>
            {
                if (_suppressApplySearchFilter) return;
                ApplySearchFilter();
                UpdateClearFiltersButtonVisibility();
                ScheduleSaveState();
            };

            _dtpCreatedTo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                ShowCheckBox = true,
                Checked = false,
                Width = 140,
                Margin = new Padding(0, 10, 14, 0)
            };
            _dtpCreatedTo.ValueChanged += (s, e) =>
            {
                if (_suppressApplySearchFilter) return;
                ApplySearchFilter();
                UpdateClearFiltersButtonVisibility();
                ScheduleSaveState();
            };

            _btnAddReceipt = new System.Windows.Forms.Button
            {
                Text = "+  Add Receipt",
                Size = new Size(190, 46),
                Margin = new Padding(0, 6, 12, 0)
            };
            _btnAddReceipt.Click += BtnAddReceipt_Click;
            ConfigurePrimaryPillButton(_btnAddReceipt);

            _btnOpenViewer = new HopeButton
            {
                Text = "👁  Open Receipt Viewer",
                Size = new Size(260, 46),
                Font = new Font("Segoe UI Emoji", 10.5F, FontStyle.Bold),
                ButtonType = HopeButtonType.Primary,
                Margin = new Padding(0, 6, 12, 0)
            };
            _btnOpenViewer.Click += BtnOpenViewer_Click;
            ConfigureOutlineHopeButton(_btnOpenViewer, Color.FromArgb(52, 152, 219), Color.FromArgb(232, 244, 253));

            _btnGoToManageSets = new HopeButton
            {
                Text = "⚙  Go to Manage Sets",
                Size = new Size(200, 46),
                Font = new Font("Segoe UI Emoji", 10.5F, FontStyle.Bold),
                ButtonType = HopeButtonType.Primary,
                Margin = new Padding(0, 6, 12, 0)
            };
            _btnGoToManageSets.Click += (s, e) => GoToManageSetsRequested?.Invoke();
            ConfigureGhostHopeButton(_btnGoToManageSets, Color.FromArgb(52, 152, 219), Color.FromArgb(245, 247, 250));

            _btnRefresh = new HopeButton
            {
                Text = "⟳",
                Size = new Size(58, 58),
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ButtonType = HopeButtonType.Primary,
                Margin = new Padding(0, 0, 0, 0)
            };
            _btnRefresh.Click += (s, e) => LoadReceiptSets();
            ConfigurePillHopeButton(_btnRefresh, Color.FromArgb(52, 152, 219), Color.FromArgb(41, 128, 185));

            var rightActions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            rightActions.Controls.Add(_btnOpenViewer);
            rightActions.Controls.Add(new Label { Width = 12, Height = 1 }); // Add spacing between buttons
            rightActions.Controls.Add(_btnGoToManageSets);
            rightActions.Controls.Add(new Label { Width = 12, Height = 1 }); // Add spacing between buttons
            rightActions.Controls.Add(_btnRefresh);

            // Place primary actions before filters.
            filterRow.Controls.Add(_btnAddReceipt);
            filterRow.Controls.Add(new Label { Width = 12, Height = 1, Margin = new Padding(0, 0, 0, 0) });

            filterRow.Controls.Add(_lblFilterStatus);
            filterRow.Controls.Add(_chipStatusAll);
            filterRow.Controls.Add(_chipStatusComplete);
            filterRow.Controls.Add(_chipStatusPending);
            filterRow.Controls.Add(_lblCreatedRange);
            filterRow.Controls.Add(_dtpCreatedFrom);
            filterRow.Controls.Add(_dtpCreatedTo);

            filterRowHost.Controls.Add(filterRow, 0, 0);
            filterRowHost.Controls.Add(rightActions, 1, 0);
            filterCard.Controls.Add(filterRowHost);
            _buttonBarPanel.Controls.Add(filterCard);

            UpdateStatusChips();

            // Body panel for grid (ReaLTaiizor Panel for smoother edges)
            _bodyPanel = new ReaLTaiizor.Controls.Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 10, 12, 12),
                BackColor = Color.White,
                EdgeColor = Color.FromArgb(224, 224, 224),
                SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality
            };

            _summaryPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = Color.White
            };

            var summaryFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            _cardTotal = CreateSummaryCard("Total", Color.FromArgb(52, 152, 219), out _lblTotalCount);
            _cardComplete = CreateSummaryCard("Complete", Color.FromArgb(46, 204, 113), out _lblCompleteCount);
            _cardPending = CreateSummaryCard("Pending", Color.FromArgb(231, 76, 60), out _lblPendingCount);

            // Summary cards act as quick status filters
            void MakeSummaryCardClickable(MaterialCard card, int filterIndex)
            {
                if (card == null)
                    return;

                card.Cursor = Cursors.Hand;
                card.Click += (s, e) =>
                {
                    if (_cboFilterStatus != null)
                    {
                        // Toggle: clicking the active filter goes back to "All"
                        _cboFilterStatus.SelectedIndex = (_cboFilterStatus.SelectedIndex == filterIndex) ? 0 : filterIndex;
                    }
                };

                foreach (Control child in card.Controls)
                {
                    child.Cursor = Cursors.Hand;
                    child.Click += (s, e) =>
                    {
                        if (_cboFilterStatus != null)
                        {
                            _cboFilterStatus.SelectedIndex = (_cboFilterStatus.SelectedIndex == filterIndex) ? 0 : filterIndex;
                        }
                    };
                }
            }

            MakeSummaryCardClickable(_cardTotal, 0);    // All
            MakeSummaryCardClickable(_cardComplete, 1); // Complete
            MakeSummaryCardClickable(_cardPending, 2);  // Pending

            summaryFlow.Controls.Add(_cardTotal);
            summaryFlow.Controls.Add(_cardComplete);
            summaryFlow.Controls.Add(_cardPending);
            _summaryPanel.Controls.Add(summaryFlow);

            _legendPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.White,
                Padding = new Padding(6, 4, 6, 4)
            };

            var legendRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            _lblLegend = new Label
            {
                AutoSize = true,
                Text = "Legend:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(90, 90, 90),
                Margin = new Padding(0, 6, 8, 0)
            };

            legendRow.Controls.Add(_lblLegend);
            legendRow.Controls.Add(CreateLegendChip("✓ Complete", Color.FromArgb(235, 248, 240), Color.FromArgb(39, 174, 96)));
            legendRow.Controls.Add(CreateLegendChip("! Pending", Color.FromArgb(255, 238, 220), Color.FromArgb(192, 57, 43)));
            legendRow.Controls.Add(CreateLegendChip("⋮ Actions", Color.FromArgb(232, 244, 253), Color.FromArgb(41, 128, 185)));
            _legendPanel.Controls.Add(legendRow);

            _paginationPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.White,
                Padding = new Padding(12, 5, 12, 5)
            };

            var paginationLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            paginationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            paginationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            paginationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var navFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _btnFirstPage = new System.Windows.Forms.Button
            {
                Text = "<<",
                Width = 45,
                Height = 25,
                Margin = new Padding(0, 0, 6, 0)
            };
            _btnFirstPage.Click += BtnFirstPage_Click;

            _btnPrevPage = new System.Windows.Forms.Button
            {
                Text = "<",
                Width = 45,
                Height = 25,
                Margin = new Padding(0, 0, 6, 0)
            };
            _btnPrevPage.Click += BtnPrevPage_Click;

            _lblPageInfo = new Label
            {
                AutoSize = false,
                Width = 340,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 10, 0)
            };

            _btnNextPage = new System.Windows.Forms.Button
            {
                Text = ">",
                Width = 45,
                Height = 25,
                Margin = new Padding(0, 0, 6, 0)
            };
            _btnNextPage.Click += BtnNextPage_Click;

            _btnLastPage = new System.Windows.Forms.Button
            {
                Text = ">>",
                Width = 45,
                Height = 25,
                Margin = new Padding(0, 0, 0, 0)
            };
            _btnLastPage.Click += BtnLastPage_Click;

            navFlow.Controls.Add(_btnFirstPage);
            navFlow.Controls.Add(_btnPrevPage);
            navFlow.Controls.Add(_lblPageInfo);
            navFlow.Controls.Add(_btnNextPage);
            navFlow.Controls.Add(_btnLastPage);

            _lblShowingInfo = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(90, 90, 90),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(12, 2, 12, 0)
            };

            var pageSizeFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            var lblRows = new Label
            {
                AutoSize = true,
                Text = "Rows:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(90, 90, 90),
                Margin = new Padding(0, 5, 6, 0)
            };

            _cboPageSize = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 70,
                Margin = new Padding(0, 1, 0, 0)
            };
            _cboPageSize.Items.AddRange(new object[] { "10", "25", "50", "100" });
            _cboPageSize.SelectedIndex = 0;
            _cboPageSize.SelectedIndexChanged += (s, e) =>
            {
                if (_cboPageSize?.SelectedItem == null)
                    return;

                if (int.TryParse(_cboPageSize.SelectedItem.ToString(), out int size) && size > 0)
                {
                    _pageSize = size;
                    _currentPage = 1;
                    UpdatePagination();
                    ScheduleSaveState();
                }
            };

            pageSizeFlow.Controls.Add(lblRows);
            pageSizeFlow.Controls.Add(_cboPageSize);

            paginationLayout.Controls.Add(navFlow, 0, 0);
            paginationLayout.Controls.Add(_lblShowingInfo, 1, 0);
            paginationLayout.Controls.Add(pageSizeFlow, 2, 0);
            _paginationPanel.Controls.Add(paginationLayout);

            _gridCard = new MaterialCard
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            _gridReceipts = new PoisonDataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeColumns = true,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                GridColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.Single,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                EnableHeadersVisualStyles = false,
                ColumnHeadersVisible = true,
                RowHeadersVisible = false,
                RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                ShowCellToolTips = true
            };
            _gridReceipts.HandleCreated += (s, e) => EnsureGridHeaderVisible();
            _gridReceipts.DataBindingComplete += (s, e) =>
            {
                EnsureGridHeaderVisible();
                SyncCheckboxesFromCheckedIds();
                if (_gridReceipts.Rows.Count > 0)
                    _gridReceipts.ClearSelection();
            };

            // Style the column headers (match updates page)
            _gridReceipts.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 152, 219);
            _gridReceipts.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _gridReceipts.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _gridReceipts.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            _gridReceipts.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            _gridReceipts.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 4, 0, 4);
            _gridReceipts.ColumnHeadersHeight = 40;
            _gridReceipts.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // Style the rows for pill effect
            _gridReceipts.DefaultCellStyle.BackColor = Color.FromArgb(240, 246, 252);
            _gridReceipts.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            _gridReceipts.DefaultCellStyle.ForeColor = Color.FromArgb(40, 40, 40);
            _gridReceipts.DefaultCellStyle.SelectionBackColor = Color.FromArgb(41, 128, 185);
            _gridReceipts.DefaultCellStyle.SelectionForeColor = Color.White;
            _gridReceipts.DefaultCellStyle.Padding = new Padding(8, 6, 8, 6);
            _gridReceipts.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(232, 240, 248);
            _gridReceipts.RowTemplate.Height = 56;
            _gridReceipts.RowTemplate.Resizable = DataGridViewTriState.False;

            _gridReceipts.CellDoubleClick += GridReceipts_CellDoubleClick;
            _gridReceipts.CellClick += GridReceipts_CellClick;
            _gridReceipts.CellContentClick += GridReceipts_CellContentClick;
            _gridReceipts.CellFormatting += GridReceipts_CellFormatting;
            _gridReceipts.CellPainting += GridReceipts_CellPainting;
            _gridReceipts.KeyDown += GridReceipts_KeyDown;
            _gridReceipts.CellMouseDown += GridReceipts_CellMouseDown;
            _gridReceipts.CurrentCellDirtyStateChanged += GridReceipts_CurrentCellDirtyStateChanged;
            _gridReceipts.CellValueChanged += GridReceipts_CellValueChanged;
            _gridReceipts.ColumnHeaderMouseClick += GridReceipts_ColumnHeaderMouseClick;
            _gridReceipts.EditMode = DataGridViewEditMode.EditOnEnter;

            _gridReceipts.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "Select",
                HeaderText = "",
                Width = 42,
                ReadOnly = false,
                Frozen = true,
                Resizable = DataGridViewTriState.False,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            var actionIconFont = new Font("Segoe UI Symbol", 12F, FontStyle.Bold);
            var actionButtonCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Font = actionIconFont
            };

            _gridReceipts.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "Actions",
                HeaderText = "Actions",
                Text = "⋮",
                UseColumnTextForButtonValue = true,
                DefaultCellStyle = actionButtonCellStyle,
                Width = 80,
                Frozen = true,
                ReadOnly = true
            });

            var deleteButtonCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(192, 57, 43),
                SelectionForeColor = Color.FromArgb(192, 57, 43)
            };

            _gridReceipts.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "DeleteRow",
                HeaderText = "",
                Text = "🗑 Delete",
                UseColumnTextForButtonValue = true,
                FlatStyle = FlatStyle.Flat,
                DefaultCellStyle = deleteButtonCellStyle,
                Width = 90,
                Frozen = true,
                ReadOnly = true
            });

            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SetCode",
                HeaderText = "Linked Sets",
                DataPropertyName = "SetCode",
                Width = 150,
                ReadOnly = true
            });

            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Supplier",
                HeaderText = "Supplier",
                DataPropertyName = "Supplier",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 100,
                MinimumWidth = 240,
                ReadOnly = true
            });

            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SiNumber",
                HeaderText = "SI #",
                DataPropertyName = "SiNumber",
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter },
                Width = 120,
                ReadOnly = true
            });

            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DrNumber",
                HeaderText = "DR #",
                DataPropertyName = "DrNumber",
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter },
                Width = 120,
                ReadOnly = true
            });

            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PoNumber",
                HeaderText = "PO #",
                DataPropertyName = "PoNumber",
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter },
                Width = 120,
                ReadOnly = true
            });

            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Status",
                HeaderText = "Status",
                Width = 90,
                ReadOnly = true
            });

            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Renewed",
                HeaderText = "Renewed",
                Width = 110,
                ReadOnly = true
            });

            _gridReceipts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CreatedAt",
                HeaderText = "Created At",
                DataPropertyName = "CreatedAt",
                Width = 160,
                ReadOnly = true
            });

            BuildRowContextMenu();

            _emptyStatePanel = BuildEmptyStatePanel();
            _loadingPanel = BuildLoadingPanel();

            // Re-assert header settings after adding columns (Poison can override these)
            EnsureGridHeaderVisible();

            // Important: docking is applied in reverse z-order. Add grid first, then bottom pager,
            // then legend, so the top sections stack correctly.
            _gridCard.Controls.Add(_gridReceipts);
            _gridCard.Controls.Add(_paginationPanel);
            _gridCard.Controls.Add(_legendPanel);
            _gridCard.Controls.Add(_emptyStatePanel);
            _gridCard.Controls.Add(_loadingPanel);
            _bodyPanel.Controls.Add(_gridCard);
            _gridReceipts.Refresh();

            // Add panels to control (order matters for docking)
            Controls.Add(_bodyPanel);
            Controls.Add(_summaryPanel);
            Controls.Add(_buttonBarPanel);
            Controls.Add(_headerPanel);

            UpdateClearFiltersButtonVisibility();
        }

        private void BtnOpenViewer_Click(object sender, EventArgs e)
        {
            var dto = GetFocusedRowDto();
            if (dto == null)
            {
                MessageBox.Show(this, "Select a receipt row first.", "Open Receipt Viewer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            OpenReceiptSetViewer(dto, readOnly: false);

            // Reload in case the user saved changes
            ReloadReceiptSetsPreserveState(dto.ReceiptSetId);
        }

        private void BtnAddReceipt_Click(object sender, EventArgs e)
        {
            Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForNewReceipt(FindForm());

            ReloadReceiptSetsPreserveState(null);
        }

        private void GridReceipts_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (e.ColumnIndex >= 0 && e.ColumnIndex < _gridReceipts.Columns.Count)
            {
                var col = _gridReceipts.Columns[e.ColumnIndex];
                if (col != null && (string.Equals(col.Name, "Actions", StringComparison.OrdinalIgnoreCase) || string.Equals(col.Name, "Select", StringComparison.OrdinalIgnoreCase)))
                    return;
            }

            var dto = _gridReceipts.Rows[e.RowIndex].DataBoundItem as ReceiptSetDto;
            if (dto == null) return;

            OpenReceiptSetViewer(dto, readOnly: false);

            // Reload in case the user saved changes
            ReloadReceiptSetsPreserveState(dto.ReceiptSetId);
        }

        private void GridReceipts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            if (_gridReceipts == null)
                return;

            var col = _gridReceipts.Columns[e.ColumnIndex];
            if (col == null)
                return;

            if (string.Equals(col.Name, "Actions", StringComparison.OrdinalIgnoreCase) || string.Equals(col.Name, "Select", StringComparison.OrdinalIgnoreCase))
                return;

            // Allow multi-select without popping viewers.
            var mods = Control.ModifierKeys;
            if ((mods & (Keys.Control | Keys.Shift | Keys.Alt)) != Keys.None)
                return;

            if (_gridReceipts.SelectedRows != null && _gridReceipts.SelectedRows.Count > 1)
                return;

            var dto = _gridReceipts.Rows[e.RowIndex].DataBoundItem as ReceiptSetDto;
            if (dto == null)
                return;

            ReceiptSetViewerDialog.ReceiptSlot? slot = null;
            if (string.Equals(col.Name, "SiNumber", StringComparison.OrdinalIgnoreCase))
                slot = ReceiptSetViewerDialog.ReceiptSlot.SI;
            else if (string.Equals(col.Name, "DrNumber", StringComparison.OrdinalIgnoreCase))
                slot = ReceiptSetViewerDialog.ReceiptSlot.DR;
            else if (string.Equals(col.Name, "PoNumber", StringComparison.OrdinalIgnoreCase))
                slot = ReceiptSetViewerDialog.ReceiptSlot.PO;

            OpenReceiptSetViewer(dto, readOnly: true, initialSlot: slot);
        }

        private void GridReceipts_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_gridReceipts == null)
                return;

            if (_gridReceipts.IsCurrentCellDirty)
            {
                _gridReceipts.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void GridReceipts_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_suppressCheckboxSync)
                return;

            if (_gridReceipts == null || e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var col = _gridReceipts.Columns[e.ColumnIndex];
            if (col == null || !string.Equals(col.Name, "Select", StringComparison.OrdinalIgnoreCase))
                return;

            var row = _gridReceipts.Rows[e.RowIndex];
            var dto = row?.DataBoundItem as ReceiptSetDto;
            if (dto == null)
                return;

            bool isChecked = false;
            try { isChecked = Convert.ToBoolean(row.Cells[e.ColumnIndex].Value); } catch { }

            if (isChecked)
                _checkedReceiptSetIds.Add(dto.ReceiptSetId);
            else
                _checkedReceiptSetIds.Remove(dto.ReceiptSetId);
        }

        private void ToggleAllVisibleChecks()
        {
            if (_gridReceipts == null || _gridReceipts.Rows == null || _gridReceipts.Rows.Count == 0)
                return;

            bool allChecked = true;
            foreach (DataGridViewRow row in _gridReceipts.Rows)
            {
                if (!(row?.DataBoundItem is ReceiptSetDto dto))
                    continue;

                if (!_checkedReceiptSetIds.Contains(dto.ReceiptSetId))
                {
                    allChecked = false;
                    break;
                }
            }

            bool newValue = !allChecked;
            _suppressCheckboxSync = true;
            try
            {
                foreach (DataGridViewRow row in _gridReceipts.Rows)
                {
                    if (!(row?.DataBoundItem is ReceiptSetDto dto))
                        continue;

                    if (newValue)
                        _checkedReceiptSetIds.Add(dto.ReceiptSetId);
                    else
                        _checkedReceiptSetIds.Remove(dto.ReceiptSetId);

                    if (row.Cells["Select"] is DataGridViewCheckBoxCell cell)
                        cell.Value = newValue;
                }
            }
            finally
            {
                _suppressCheckboxSync = false;
            }
        }

        private void SyncCheckboxesFromCheckedIds()
        {
            if (_gridReceipts == null || _gridReceipts.Rows == null || _gridReceipts.Rows.Count == 0)
                return;

            _suppressCheckboxSync = true;
            try
            {
                foreach (DataGridViewRow row in _gridReceipts.Rows)
                {
                    if (!(row?.DataBoundItem is ReceiptSetDto dto))
                        continue;

                    if (row.Cells["Select"] is DataGridViewCheckBoxCell cell)
                        cell.Value = _checkedReceiptSetIds.Contains(dto.ReceiptSetId);
                }
            }
            finally
            {
                _suppressCheckboxSync = false;
            }
        }

        private void GridReceipts_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;
            var dto = GetFocusedRowDto();
            if (dto != null)
                OpenReceiptSetViewer(dto, readOnly: true);
        }

        private void GridReceipts_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            if (_gridReceipts == null || _rowMenu == null)
                return;

            if (e.RowIndex < 0)
                return;

            try
            {
                bool rowAlreadySelected = _gridReceipts.Rows[e.RowIndex].Selected;
                if (!rowAlreadySelected)
                {
                    _gridReceipts.ClearSelection();
                    _gridReceipts.Rows[e.RowIndex].Selected = true;
                }
                if (e.ColumnIndex >= 0 && e.ColumnIndex < _gridReceipts.Columns.Count)
                {
                    _gridReceipts.CurrentCell = _gridReceipts.Rows[e.RowIndex].Cells[e.ColumnIndex];
                }
            }
            catch
            {
                // ignore
            }

            _rowMenu.Show(Cursor.Position);
        }

        private void ClearAllFilters()
        {
            _suppressApplySearchFilter = true;
            try
            {
                _debounceTimer?.Stop();

                if (_txtSearch != null)
                    _txtSearch.Text = string.Empty;

                if (_cboFilterStatus != null)
                    _cboFilterStatus.SelectedIndex = 0;

                if (_cboSupplierFilter != null)
                    _cboSupplierFilter.SelectedIndex = 0;

                if (_dtpCreatedFrom != null)
                {
                    _dtpCreatedFrom.Checked = false;
                    _dtpCreatedFrom.Value = DateTime.Today;
                }

                if (_dtpCreatedTo != null)
                {
                    _dtpCreatedTo.Checked = false;
                    _dtpCreatedTo.Value = DateTime.Today;
                }

                if (cmbFilterBy != null)
                    cmbFilterBy.SelectedIndex = 0;

                _sortColumn = null;
                _sortOrder = System.Windows.Forms.SortOrder.None;

                if (_gridReceipts != null && _gridReceipts.Columns != null)
                {
                    foreach (DataGridViewColumn col in _gridReceipts.Columns)
                        col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                }

                EnsureSortByDropdownInitialized(forceReset: true);
                UpdateStatusChips();
            }
            finally
            {
                _suppressApplySearchFilter = false;
            }

            ApplySearchFilter();
            UpdateClearFiltersButtonVisibility();
            ScheduleSaveState();
        }

        private void UpdateClearFiltersButtonVisibility()
        {
            if (_btnClearFilters == null)
                return;

            bool hasSearch = !string.IsNullOrWhiteSpace(_txtSearch?.Text);
            bool hasStatus = _cboFilterStatus != null && _cboFilterStatus.SelectedIndex > 0;
            bool hasSupplier = _cboSupplierFilter != null && _cboSupplierFilter.SelectedIndex > 0;
            bool hasCreatedRange = (_dtpCreatedFrom != null && _dtpCreatedFrom.Checked) || (_dtpCreatedTo != null && _dtpCreatedTo.Checked);
            bool hasFilterBy = cmbFilterBy != null && cmbFilterBy.SelectedIndex > 0;
            bool hasSort = _sortColumn != null && _sortOrder != System.Windows.Forms.SortOrder.None;

            _btnClearFilters.Visible = hasSearch || hasStatus || hasSupplier || hasCreatedRange || hasFilterBy || hasSort;
        }

        private void BuildRowContextMenu()
        {
            _rowMenu = new ContextMenuStrip { ShowItemToolTips = true };

            var miView = new ToolStripMenuItem("View");
            miView.Click += (s, e) =>
            {
                var dto = GetFocusedRowDto();
                if (dto == null) return;
                OpenReceiptSetViewer(dto, readOnly: true);
            };

            var miOpenTab = new ToolStripMenuItem("Open Tab");
            var miOpenSi = new ToolStripMenuItem("SI");
            miOpenSi.Click += (s, e) =>
            {
                var dto = GetFocusedRowDto();
                if (dto == null) return;
                OpenReceiptSetViewer(dto, readOnly: true, initialSlot: ReceiptSetViewerDialog.ReceiptSlot.SI);
            };
            var miOpenDr = new ToolStripMenuItem("DR");
            miOpenDr.Click += (s, e) =>
            {
                var dto = GetFocusedRowDto();
                if (dto == null) return;
                OpenReceiptSetViewer(dto, readOnly: true, initialSlot: ReceiptSetViewerDialog.ReceiptSlot.DR);
            };
            var miOpenPo = new ToolStripMenuItem("PO");
            miOpenPo.Click += (s, e) =>
            {
                var dto = GetFocusedRowDto();
                if (dto == null) return;
                OpenReceiptSetViewer(dto, readOnly: true, initialSlot: ReceiptSetViewerDialog.ReceiptSlot.PO);
            };
            miOpenTab.DropDownItems.Add(miOpenSi);
            miOpenTab.DropDownItems.Add(miOpenDr);
            miOpenTab.DropDownItems.Add(miOpenPo);

            var miEdit = new ToolStripMenuItem("Edit");
            miEdit.Click += (s, e) =>
            {
                var dto = GetFocusedRowDto();
                if (dto == null) return;
                OpenReceiptSetViewer(dto, readOnly: false);
                ReloadReceiptSetsPreserveState(dto.ReceiptSetId);
            };

            var miDelete = new ToolStripMenuItem("Delete");
            miDelete.Click += (s, e) =>
            {
                var dto = GetFocusedRowDto();
                if (dto == null) return;
                DeleteReceiptSet(dto);
            };

            var miUnlink = new ToolStripMenuItem("Unlink from Set(s)…");
            miUnlink.Click += (s, e) =>
            {
                var dto = GetFocusedRowDto();
                if (dto == null) return;
                UnlinkReceiptSetFromAllSets(dto);
            };

            var miViewSelected = new ToolStripMenuItem("View Selected");
            miViewSelected.Click += (s, e) => ViewSelectedReceiptSets();

            var miOpenIncomplete = new ToolStripMenuItem("Open Incomplete…");
            miOpenIncomplete.Click += (s, e) => OpenIncompleteSelectedReceiptSets();

            var miExportSelected = new ToolStripMenuItem("Export Selected (CSV)…");
            miExportSelected.Click += (s, e) => BulkExportSelected();

            var miExportPdfCombined = new ToolStripMenuItem("Export Selected (PDF)…");
            miExportPdfCombined.Click += (s, e) => ExportSelectedReceiptSetsPdfCombined();

            var miExportPdfPerReceipt = new ToolStripMenuItem("Export Selected (PDF per receipt)…");
            miExportPdfPerReceipt.Click += (s, e) => ExportSelectedReceiptSetsPdfPerReceipt();

            var miDeleteSelected = new ToolStripMenuItem("Delete Selected…");
            miDeleteSelected.Click += (s, e) => DeleteSelectedReceiptSets();

            var miCopySetCode = new ToolStripMenuItem("Copy Set Code");
            miCopySetCode.Click += (s, e) =>
            {
                var dto = GetFocusedRowDto();
                if (dto == null) return;
                Clipboard.SetText((dto.SetCode ?? string.Empty).Trim());
            };

            var miCopySupplier = new ToolStripMenuItem("Copy Supplier");
            miCopySupplier.Click += (s, e) =>
            {
                var dto = GetFocusedRowDto();
                if (dto == null) return;
                Clipboard.SetText((dto.Supplier ?? string.Empty).Trim());
            };

            var miCopySi = new ToolStripMenuItem("Copy SI #");
            miCopySi.Click += (s, e) =>
            {
                var dto = GetFocusedRowDto();
                if (dto == null) return;
                Clipboard.SetText((dto.SiNumber ?? string.Empty).Trim());
            };

            var miCopyDr = new ToolStripMenuItem("Copy DR #");
            miCopyDr.Click += (s, e) =>
            {
                var dto = GetFocusedRowDto();
                if (dto == null) return;
                Clipboard.SetText((dto.DrNumber ?? string.Empty).Trim());
            };

            var miCopyPo = new ToolStripMenuItem("Copy PO #");
            miCopyPo.Click += (s, e) =>
            {
                var dto = GetFocusedRowDto();
                if (dto == null) return;
                Clipboard.SetText((dto.PoNumber ?? string.Empty).Trim());
            };

            _rowMenu.Items.Add(miView);
            _rowMenu.Items.Add(miOpenTab);
            _rowMenu.Items.Add(miEdit);
            _rowMenu.Items.Add(new ToolStripSeparator());
            _rowMenu.Items.Add(miDelete);
            _rowMenu.Items.Add(miUnlink);
            _rowMenu.Items.Add(new ToolStripSeparator());
            _rowMenu.Items.Add(miViewSelected);
            _rowMenu.Items.Add(miOpenIncomplete);
            _rowMenu.Items.Add(miExportSelected);
            _rowMenu.Items.Add(miExportPdfCombined);
            _rowMenu.Items.Add(miExportPdfPerReceipt);
            _rowMenu.Items.Add(miDeleteSelected);
            _rowMenu.Items.Add(new ToolStripSeparator());
            _rowMenu.Items.Add(miCopySetCode);
            _rowMenu.Items.Add(miCopySupplier);
            _rowMenu.Items.Add(new ToolStripSeparator());
            _rowMenu.Items.Add(miCopySi);
            _rowMenu.Items.Add(miCopyDr);
            _rowMenu.Items.Add(miCopyPo);

            _rowMenu.Opening += (s, e) =>
            {
                var dto = GetFocusedRowDto();
                bool hasRow = dto != null;
                var markedIds = GetMarkedReceiptSetIds();
                int markedCount = markedIds.Count;

                string lockReason = string.Empty;
                bool canDeleteRow = hasRow && !IsLockedForDelete(dto, out lockReason);

                miView.Enabled = hasRow;
                miEdit.Enabled = hasRow;
                miDelete.Enabled = canDeleteRow;
                miDelete.Text = canDeleteRow ? "Delete" : "Delete (locked)";
                miDelete.ToolTipText = canDeleteRow ? string.Empty : (!string.IsNullOrWhiteSpace(lockReason) ? lockReason : "Delete is disabled for linked/locked receipt sets.");
                bool isLinked = hasRow && !string.IsNullOrWhiteSpace(dto?.SetCode) && !string.Equals((dto?.SetCode ?? "").Trim(), "(unlinked)", StringComparison.OrdinalIgnoreCase);
                miUnlink.Enabled = isLinked;
                miOpenTab.Enabled = hasRow;
                miViewSelected.Enabled = markedCount > 1;
                miExportSelected.Enabled = markedCount > 0;
                miExportPdfCombined.Enabled = markedCount > 0;
                miExportPdfPerReceipt.Enabled = markedCount > 0;
                miDeleteSelected.Enabled = markedCount > 1 && ResolveReceiptSetDtosForIds(markedIds).All(s2 => !IsLockedForDelete(s2, out _));
                miOpenIncomplete.Enabled = markedCount > 0 && ResolveReceiptSetDtosForIds(markedIds).Any(d => !IsReceiptComplete(d));
                miCopySetCode.Enabled = hasRow && !string.IsNullOrWhiteSpace(dto?.SetCode);
                miCopySupplier.Enabled = hasRow && !string.IsNullOrWhiteSpace(dto?.Supplier);
                miCopySi.Enabled = hasRow && !string.IsNullOrWhiteSpace(dto?.SiNumber);
                miCopyDr.Enabled = hasRow && !string.IsNullOrWhiteSpace(dto?.DrNumber);
                miCopyPo.Enabled = hasRow && !string.IsNullOrWhiteSpace(dto?.PoNumber);
            };
        }

        private ReceiptSetDto GetFocusedRowDto()
        {
            if (_gridReceipts == null)
                return null;

            var row = _gridReceipts.CurrentRow;
            if (row == null)
                return null;

            return row.DataBoundItem as ReceiptSetDto;
        }

        private void OpenReceiptSetViewer(ReceiptSetDto dto, bool readOnly, ReceiptSetViewerDialog.ReceiptSlot? initialSlot = null)
        {
            if (dto == null)
                return;

            Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForReceipt(FindForm(), dto, readOnly, initialSlot);
        }

        private static bool IsLockedForDelete(ReceiptSetDto dto, out string reason)
        {
            reason = string.Empty;
            if (dto == null)
            {
                reason = "No receipt set selected.";
                return true;
            }

            var linked = (dto.SetCode ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(linked) && !string.Equals(linked, "(unlinked)", StringComparison.OrdinalIgnoreCase))
            {
                reason =
                    "This receipt set is linked to one or more sets and is locked for deletion.\n\n" +
                    "Linked set code(s): " + linked + "\n\n" +
                    "Open the receipt set, unlink it first, then try deleting again.";
                return true;
            }

            return false;
        }

        private void DeleteReceiptSet(ReceiptSetDto dto)
        {
            if (dto == null)
                return;

            if (IsLockedForDelete(dto, out var lockReason))
            {
                MessageBox.Show(this, lockReason, "Delete Disabled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var details = BuildDeleteDetails(dto);
            var confirm = MessageBox.Show(this,
                "Delete this receipt set?\n\n" + details,
                "Delete Receipt Set",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                _repository.Delete(dto.ReceiptSetId);
                ReloadReceiptSetsPreserveState(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Failed to delete receipt set.\n\n" + ex.Message,
                    "Receipt Sets",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void UnlinkReceiptSetFromAllSets(ReceiptSetDto dto)
        {
            if (dto == null)
                return;

            var linked = _repository.GetLinkedSets(dto.ReceiptSetId);
            if (linked == null || linked.Count == 0)
            {
                MessageBox.Show(this, "This receipt set is not linked to any set.", "Unlink Receipt Set",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var codes = string.Join(", ", linked.Select(l => l.SetCode));
            var confirm = MessageBox.Show(this,
                "Unlink this receipt set from the following set(s)?\n\n" + codes +
                "\n\nThe receipt set will remain saved as an unlinked receipt.",
                "Unlink Receipt Set",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                foreach (var set in linked)
                {
                    _repository.UnlinkReceiptSet(dto.ReceiptSetId, set.SetId, userId);
                }

                ReloadReceiptSetsPreserveState(dto.ReceiptSetId);

                MessageBox.Show(this, "Receipt set has been unlinked.", "Unlink Receipt Set",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Failed to unlink receipt set.\n\n" + ex.Message,
                    "Receipt Sets",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static string BuildDeleteDetails(ReceiptSetDto dto)
        {
            if (dto == null)
                return string.Empty;

            string supplier = string.IsNullOrWhiteSpace(dto.Supplier) ? "(none)" : dto.Supplier.Trim();
            string setCodes = string.IsNullOrWhiteSpace(dto.SetCode) ? "(none)" : dto.SetCode.Trim();
            string si = string.IsNullOrWhiteSpace(dto.SiNumber) ? "(none)" : dto.SiNumber.Trim();
            string dr = string.IsNullOrWhiteSpace(dto.DrNumber) ? "(none)" : dto.DrNumber.Trim();
            string po = string.IsNullOrWhiteSpace(dto.PoNumber) ? "(none)" : dto.PoNumber.Trim();
            string createdAt = dto.CreatedAt != default(DateTime)
                ? dto.CreatedAt.ToString("MMM dd, yyyy hh:mm tt")
                : "(unknown)";

            return
                $"Linked Set Code(s): {setCodes}\n" +
                $"Supplier: {supplier}\n" +
                $"SI #: {si}\n" +
                $"DR #: {dr}\n" +
                $"PO #: {po}\n" +
                $"Created At: {createdAt}";
        }

        private System.Windows.Forms.Panel BuildEmptyStatePanel()
        {
            var p = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Visible = false
            };

            var inner = new System.Windows.Forms.Panel
            {
                Width = 560,
                Height = 220,
                BackColor = Color.White
            };

            _lblEmptyTitle = new Label
            {
                AutoSize = true,
                Text = "No receipt sets",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Location = new Point(0, 0)
            };

            _lblEmptySubtitle = new Label
            {
                AutoSize = true,
                Text = "There are no items to display.",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(90, 90, 90),
                Location = new Point(0, 44)
            };

            _btnEmptyAction = new HopeButton
            {
                Text = "Add Receipt",
                Size = new Size(160, 42),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ButtonType = HopeButtonType.Primary,
                Location = new Point(0, 98)
            };
            _btnEmptyAction.Click += EmptyStateAction_Click;
            ConfigurePillHopeButton(_btnEmptyAction, Color.FromArgb(52, 152, 219), Color.FromArgb(41, 128, 185));

            inner.Controls.Add(_lblEmptyTitle);
            inner.Controls.Add(_lblEmptySubtitle);
            inner.Controls.Add(_btnEmptyAction);

            p.Controls.Add(inner);
            p.Resize += (s, e) =>
            {
                inner.Left = Math.Max(0, (p.Width - inner.Width) / 2);
                inner.Top = Math.Max(0, (p.Height - inner.Height) / 2);
            };

            return p;
        }

        private System.Windows.Forms.Panel BuildLoadingPanel()
        {
            var p = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Visible = false
            };

            var inner = new System.Windows.Forms.Panel
            {
                Width = 420,
                Height = 140,
                BackColor = Color.White
            };

            _lblLoading = new Label
            {
                AutoSize = true,
                Text = "Loading…",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60),
                Location = new Point(0, 0)
            };

            _loadingProgress = new ProgressBar
            {
                Width = 360,
                Height = 18,
                Style = ProgressBarStyle.Marquee,
                Location = new Point(0, 52)
            };

            inner.Controls.Add(_lblLoading);
            inner.Controls.Add(_loadingProgress);

            p.Controls.Add(inner);
            p.Resize += (s, e) =>
            {
                inner.Left = Math.Max(0, (p.Width - inner.Width) / 2);
                inner.Top = Math.Max(0, (p.Height - inner.Height) / 2);
            };

            return p;
        }

        private void ShowEmptyState()
        {
            if (_emptyStatePanel == null)
                return;

            bool hasAnyReceipts = _allReceipts != null && _allReceipts.Count > 0;

            _emptyStatePanel.Visible = true;
            _emptyStatePanel.BringToFront();
            if (_loadingPanel != null) _loadingPanel.Visible = false;

            if (_gridReceipts != null) _gridReceipts.Visible = false;
            if (_paginationPanel != null) _paginationPanel.Visible = false;
            if (_legendPanel != null) _legendPanel.Visible = false;

            if (!hasAnyReceipts)
            {
                _emptyStateActionMode = EmptyStateActionMode.AddReceipt;
                if (_lblEmptyTitle != null) _lblEmptyTitle.Text = "No receipt sets yet";
                if (_lblEmptySubtitle != null) _lblEmptySubtitle.Text = "Start by adding supplier info and attaching SI/DR/PO.";
                if (_btnEmptyAction != null)
                {
                    _btnEmptyAction.Text = "Add Receipt";
                }
            }
            else
            {
                _emptyStateActionMode = EmptyStateActionMode.ClearFilters;
                if (_lblEmptyTitle != null) _lblEmptyTitle.Text = "No results found";
                if (_lblEmptySubtitle != null) _lblEmptySubtitle.Text = "Try adjusting your filters or clear them to see all receipts.";
                if (_btnEmptyAction != null)
                {
                    _btnEmptyAction.Text = "Clear Filters";
                }
            }
        }

        private void EmptyStateAction_Click(object sender, EventArgs e)
        {
            if (_emptyStateActionMode == EmptyStateActionMode.AddReceipt)
            {
                BtnAddReceipt_Click(sender, e);
            }
            else
            {
                ClearAllFilters();
            }
        }

        private void HideEmptyState()
        {
            if (_emptyStatePanel != null)
                _emptyStatePanel.Visible = false;

            if (_gridReceipts != null) _gridReceipts.Visible = true;
            if (_paginationPanel != null) _paginationPanel.Visible = true;
            if (_legendPanel != null) _legendPanel.Visible = true;
            if (_loadingPanel != null && UseWaitCursor == false) _loadingPanel.Visible = false;
        }

        private void ViewSelectedReceiptSets()
        {
            var selected = ResolveReceiptSetDtosForIds(GetMarkedReceiptSetIds());
            if (selected.Count == 0)
                return;

            foreach (var dto in selected)
            {
                OpenReceiptSetViewer(dto, readOnly: true);
            }
        }

        private void OpenIncompleteSelectedReceiptSets()
        {
            var selected = ResolveReceiptSetDtosForIds(GetMarkedReceiptSetIds());
            if (selected.Count == 0)
                return;

            var incomplete = selected.Where(d => !IsReceiptComplete(d)).ToList();
            if (incomplete.Count == 0)
            {
                MessageBox.Show(this, "All selected receipt sets are already complete.", "Receipt Sets",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(this,
                $"Open {incomplete.Count} incomplete receipt set(s) so you can attach missing SI/DR/PO?\n\n" +
                "Tip: Save each receipt set after attaching to mark it complete.",
                "Open Incomplete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (confirm != DialogResult.Yes)
                return;

            foreach (var dto in incomplete)
            {
                OpenReceiptSetViewer(dto, readOnly: false);
            }

            ReloadReceiptSetsPreserveState(null);
        }

        private void DeleteSelectedReceiptSets()
        {
            var selected = ResolveReceiptSetDtosForIds(GetMarkedReceiptSetIds());
            if (selected.Count == 0)
                return;

            if (!ConfirmBulkDelete(selected))
                return;

            int deleted = 0;
            int failed = 0;
            foreach (var dto in selected)
            {
                try
                {
                    _repository.Delete(dto.ReceiptSetId);
                    _checkedReceiptSetIds.Remove(dto.ReceiptSetId);
                    deleted++;
                }
                catch
                {
                    failed++;
                }
            }

            ReloadReceiptSetsPreserveState(null);

            if (failed > 0)
            {
                MessageBox.Show(this,
                    $"Deleted {deleted} receipt set(s). Failed to delete {failed}.",
                    "Delete Selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static string CsvEscape(string s)
        {
            if (s == null) return string.Empty;
            if (s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
            {
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            }
            return s;
        }

        private void BulkExportSelected()
        {
            var markedIds = GetMarkedReceiptSetIds();
            var selected = ResolveReceiptSetDtosForIds(markedIds);
            if (selected.Count == 0)
                return;

            using (var sfd = new SaveFileDialog())
            {
                sfd.Title = "Export Receipt Sets";
                sfd.Filter = "CSV file|*.csv";
                sfd.FileName = $"receipt_sets_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return;

                var sb = new StringBuilder();
                sb.AppendLine("ReceiptSetId,SetCode,Supplier,SiNumber,DrNumber,PoNumber,HasSI,HasDR,HasPO,Status,CreatedAt");

                foreach (var dto in selected)
                {
                    bool hasSi = HasAttachment(dto.SiImage, dto.SiImagePath);
                    bool hasDr = HasAttachment(dto.DrImage, dto.DrImagePath);
                    bool hasPo = HasAttachment(dto.PoImage, dto.PoImagePath);

                    sb.Append(dto.ReceiptSetId).Append(",");
                    sb.Append(CsvEscape(dto.SetCode)).Append(",");
                    sb.Append(CsvEscape(dto.Supplier)).Append(",");
                    sb.Append(CsvEscape(dto.SiNumber)).Append(",");
                    sb.Append(CsvEscape(dto.DrNumber)).Append(",");
                    sb.Append(CsvEscape(dto.PoNumber)).Append(",");
                    sb.Append(hasSi ? "1" : "0").Append(",");
                    sb.Append(hasDr ? "1" : "0").Append(",");
                    sb.Append(hasPo ? "1" : "0").Append(",");
                    sb.Append(CsvEscape(GetReceiptStatusText(dto))).Append(",");
                    sb.Append(CsvEscape(dto.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")));
                    sb.AppendLine();
                }

                System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            }
        }

        private async void ExportSelectedReceiptSetsPdfCombined()
        {
            var ids = GetMarkedReceiptSetIds();
            if (ids.Count == 0)
                return;

            using (var sfd = new SaveFileDialog())
            {
                sfd.Title = "Export Receipt Sets (PDF)";
                sfd.Filter = "PDF file|*.pdf";
                sfd.FileName = $"receipt_sets_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return;

                SetLoading(true, "Exporting PDF…");
                Application.DoEvents();

                try
                {
                    int pageCount = 0;
                    await Task.Run(() =>
                    {
                        var imageRepo = new ReceiptSetRepository();
                        ReceiptSetPdfGenerator.ImageProvider = (receiptSetId, docType) => imageRepo.GetImagesForReceiptSet(receiptSetId, docType);

                        var receipts = LoadReceiptSetsForPdf(ids);
                        pageCount = ReceiptSetPdfGenerator.GetPageCount(receipts);
                        ReceiptSetPdfGenerator.GenerateReceiptSetsPdf(receipts, sfd.FileName, title: "Receipt Sets");
                    });

                    MessageBox.Show(this, $"Saved {pageCount} page(s):\n{sfd.FileName}", "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to export PDF:\n" + ex.Message, "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    SetLoading(false, string.Empty);
                }
            }
        }

        private async void ExportSelectedReceiptSetsPdfPerReceipt()
        {
            var ids = GetMarkedReceiptSetIds();
            if (ids.Count == 0)
                return;

            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select a folder to export PDF files";
                if (fbd.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    return;

                SetLoading(true, "Exporting PDFs…");
                Application.DoEvents();

                try
                {
                    var folder = fbd.SelectedPath;
                    int exportedCount = 0;
                    await Task.Run(() =>
                    {
                        var imageRepo = new ReceiptSetRepository();
                        ReceiptSetPdfGenerator.ImageProvider = (receiptSetId, docType) => imageRepo.GetImagesForReceiptSet(receiptSetId, docType);

                        var receipts = LoadReceiptSetsForPdf(ids);
                        foreach (var dto in receipts)
                        {
                            var fileName = BuildReceiptPdfFileName(dto);
                            var fullPath = GetUniqueFilePath(Path.Combine(folder, fileName));
                            ReceiptSetPdfGenerator.GenerateReceiptSetPdf(dto, fullPath);
                            exportedCount++;
                        }
                    });

                    MessageBox.Show(this, $"Exported {exportedCount} PDF file(s) to:\n{fbd.SelectedPath}", "Export PDFs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to export PDFs:\n" + ex.Message, "Export PDFs", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    SetLoading(false, string.Empty);
                }
            }
        }

        private List<ReceiptSetDto> LoadReceiptSetsForPdf(List<int> receiptSetIds)
        {
            var list = new List<ReceiptSetDto>();
            if (receiptSetIds == null || receiptSetIds.Count == 0)
                return list;

            foreach (var id in receiptSetIds.Distinct())
            {
                try
                {
                    var dto = _repository.GetByReceiptSetId(id);
                    if (dto != null)
                        list.Add(dto);
                }
                catch
                {
                    // ignore individual failures; export what we can
                }
            }

            return list;
        }

        private static string BuildReceiptPdfFileName(ReceiptSetDto dto)
        {
            if (dto == null)
                return $"receipt_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            var firstSetCode = GetFirstSetCode(dto.SetCode);
            if (string.IsNullOrWhiteSpace(firstSetCode))
                firstSetCode = "unlinked";

            var supplier = Truncate(SanitizeFileNamePart(dto.Supplier), 28);
            var suffix = $"{dto.ReceiptSetId}_{dto.CreatedAt:yyyyMMdd_HHmm}";

            var baseName = $"{firstSetCode}_receipt_{suffix}";
            if (!string.IsNullOrWhiteSpace(supplier))
                baseName += $"_{supplier}";

            return baseName + ".pdf";
        }

        private static string GetFirstSetCode(string setCodes)
        {
            if (string.IsNullOrWhiteSpace(setCodes))
                return string.Empty;

            var first = setCodes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return (first ?? string.Empty).Trim();
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || max <= 0)
                return string.Empty;

            return s.Length <= max ? s : s.Substring(0, max);
        }

        private static string SanitizeFileNamePart(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;

            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(s.Where(ch => !invalid.Contains(ch)).ToArray());
            cleaned = cleaned.Trim();
            return cleaned;
        }

        private static string GetUniqueFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return path;

            var dir = Path.GetDirectoryName(path) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);

            for (int i = 2; i < 1000; i++)
            {
                var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
                if (!File.Exists(candidate))
                    return candidate;
            }

            return path;
        }

        private void GridReceipts_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var column = _gridReceipts.Columns[e.ColumnIndex];
            if (column == null) return;

            var dto = _gridReceipts.Rows[e.RowIndex].DataBoundItem as ReceiptSetDto;
            if (dto == null) return;

            if (string.Equals(column.Name, "Actions", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // Ensure the clicked row is focused so the context menu actions operate on it.
                    _gridReceipts.ClearSelection();
                    _gridReceipts.Rows[e.RowIndex].Selected = true;
                    _gridReceipts.CurrentCell = _gridReceipts.Rows[e.RowIndex].Cells[e.ColumnIndex];

                    var cellRect = _gridReceipts.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                    var menuPoint = new Point(cellRect.Left + (cellRect.Width / 2), cellRect.Bottom);
                    _rowMenu?.Show(_gridReceipts, menuPoint);
                }
                catch
                {
                    _rowMenu?.Show(Cursor.Position);
                }
            }
            else if (string.Equals(column.Name, "DeleteRow", StringComparison.OrdinalIgnoreCase))
            {
                _gridReceipts.ClearSelection();
                _gridReceipts.Rows[e.RowIndex].Selected = true;
                _gridReceipts.CurrentCell = _gridReceipts.Rows[e.RowIndex].Cells[e.ColumnIndex];

                DeleteReceiptSet(dto);
            }
        }

        private void GridReceipts_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var grid = sender as DataGridView;
            if (grid == null) return;

            var column = grid.Columns[e.ColumnIndex];
            if (column == null) return;

            var dto = grid.Rows[e.RowIndex].DataBoundItem as ReceiptSetDto;
            if (dto == null) return;

            if (column.Name == "Status")
            {
                e.Value = GetReceiptStatusText(dto);
                e.FormattingApplied = true;
            }
            else if (column.Name == "Renewed")
            {
                if (dto.TotalItems.HasValue && dto.TotalItems.Value > 0)
                {
                    int renewed = dto.RenewedItems ?? 0;
                    int total = dto.TotalItems.Value;
                    e.Value = renewed > 0 ? $"✓ {renewed}/{total}" : $"{renewed}/{total}";

                    var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    cell.ToolTipText = "Renewed items (latest renewal status): " + renewed + " of " + total;
                }
                else
                {
                    e.Value = "—";
                }

                e.FormattingApplied = true;
            }
            else if (column.Name == "SetCode")
            {
                string originalText = (dto.SetCode ?? string.Empty).Trim();
                var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                cell.ToolTipText = string.IsNullOrWhiteSpace(originalText)
                    ? "No linked sets"
                    : ("Linked sets: " + originalText);

                var parts = originalText.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length <= 1)
                {
                    e.Value = string.IsNullOrWhiteSpace(originalText) ? "(unlinked)" : originalText;
                    e.FormattingApplied = true;
                }
                else
                {
                    e.Value = $"{parts[0]} (+{parts.Length - 1})";
                    e.FormattingApplied = true;
                }
            }
            else if (column.Name == "SiNumber" || column.Name == "DrNumber" || column.Name == "PoNumber")
            {
                bool has;
                string label;
                string number;

                if (column.Name == "SiNumber")
                {
                    has = HasAttachment(dto.SiImage, dto.SiImagePath);
                    label = "SI";
                    number = dto.SiNumber;
                }
                else if (column.Name == "DrNumber")
                {
                    has = HasAttachment(dto.DrImage, dto.DrImagePath);
                    label = "DR";
                    number = dto.DrNumber;
                }
                else
                {
                    has = HasAttachment(dto.PoImage, dto.PoImagePath);
                    label = "PO";
                    number = dto.PoNumber;
                }

                var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                cell.ToolTipText = has
                    ? $"Click to open {label} tab"
                    : $"Click to open {label} tab (no image attached)";

                var clean = string.IsNullOrWhiteSpace(number) ? "—" : number.Trim();
                e.Value = clean;
                e.FormattingApplied = true;
            }
        }

        private void GridReceipts_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null) return;

            if (e.ColumnIndex < 0) return;

            var column = grid.Columns[e.ColumnIndex];
            if (column == null) return;

            // Paint header cells so headers don't disappear with custom painting
            if (e.RowIndex < 0)
            {
                e.Handled = true;

                // Fill background with white for border separation
                e.Graphics.FillRectangle(Brushes.White, e.CellBounds);

                // Fill header cell with header color (leaving 1px border)
                Rectangle r = new Rectangle(
                    e.CellBounds.X,
                    e.CellBounds.Y,
                    e.CellBounds.Width - 1,  // Leave 1px for right border
                    e.CellBounds.Height - 1  // Leave 1px for bottom border
                );

                using (var brush = new SolidBrush(Color.FromArgb(52, 152, 219)))
                {
                    e.Graphics.FillRectangle(brush, r);
                }

                // Draw white vertical border on the right edge
                using (var borderPen = new Pen(Color.White, 1))
                {
                    e.Graphics.DrawLine(borderPen,
                        e.CellBounds.Right - 1, e.CellBounds.Top,
                        e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                }

                // Draw white horizontal border on the bottom edge
                using (var borderPen = new Pen(Color.White, 1))
                {
                    e.Graphics.DrawLine(borderPen,
                        e.CellBounds.Left, e.CellBounds.Bottom - 1,
                        e.CellBounds.Right, e.CellBounds.Bottom - 1);
                }

                // Draw header text
                {
                    // Adjust text rectangle to make room for sort glyph
                    var textRect = e.CellBounds;
                    
                    // Draw Sort Glyph on ALL sortable columns
                    bool isSortable = column.SortMode == DataGridViewColumnSortMode.Programmatic;
                    
                    if (isSortable)
                    {
                        // Reduce text width to avoid overlap
                        textRect.Width -= 16;
                        
                        var glyphX = e.CellBounds.Right - 18;
                        var glyphY = e.CellBounds.Y + (e.CellBounds.Height - 8) / 2;
                        
                        // Determine arrow color - bright white for active sort, dimmed for inactive
                        Color arrowColor = column.HeaderCell.SortGlyphDirection != System.Windows.Forms.SortOrder.None 
                            ? Color.White 
                            : Color.FromArgb(180, 255, 255, 255); // Semi-transparent white
                        
                        // Draw arrow
                        using (var pen = new Pen(arrowColor, 2))
                        {
                            if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Ascending)
                            {
                                // Up arrow (active sort)
                                e.Graphics.DrawLine(pen, glyphX, glyphY + 6, glyphX + 5, glyphY);
                                e.Graphics.DrawLine(pen, glyphX + 5, glyphY, glyphX + 10, glyphY + 6);
                            }
                            else if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Descending)
                            {
                                // Down arrow (active sort)
                                e.Graphics.DrawLine(pen, glyphX, glyphY, glyphX + 5, glyphY + 6);
                                e.Graphics.DrawLine(pen, glyphX + 5, glyphY + 6, glyphX + 10, glyphY);
                            }
                            else
                            {
                                // Default: show both arrows (inactive state)
                                // Up arrow
                                e.Graphics.DrawLine(pen, glyphX + 2, glyphY + 2, glyphX + 5, glyphY - 1);
                                e.Graphics.DrawLine(pen, glyphX + 5, glyphY - 1, glyphX + 8, glyphY + 2);
                                // Down arrow
                                e.Graphics.DrawLine(pen, glyphX + 2, glyphY + 4, glyphX + 5, glyphY + 7);
                                e.Graphics.DrawLine(pen, glyphX + 5, glyphY + 7, glyphX + 8, glyphY + 4);
                            }
                        }
                    }

                    TextRenderer.DrawText(e.Graphics, column.HeaderText, e.CellStyle.Font ?? grid.Font, textRect, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                }

                return;
            }

            bool isSelected = grid.Rows[e.RowIndex].Selected;
            bool isFirstColumn = e.ColumnIndex == 0;
            bool isLastColumn = e.ColumnIndex == grid.Columns.Count - 1;
            bool isActionColumn = column.Name == "Actions";
            bool isStatusColumn = column.Name == "Status";
            bool isRenewalColumn = column.Name == "Renewed";
            bool isCenteredDataColumn = isActionColumn || isStatusColumn || isRenewalColumn || column.Name == "SiNumber" || column.Name == "DrNumber" || column.Name == "PoNumber";

            // Alternating pill colors
            Color pillColor = (e.RowIndex % 2 == 0)
                ? Color.FromArgb(227, 242, 253)  // Light blue
                : Color.FromArgb(232, 245, 233); // Light green

            if (isSelected)
            {
                pillColor = Color.FromArgb(41, 128, 185);
            }

            // Fill background with white for border separation
            e.Graphics.FillRectangle(Brushes.White, e.CellBounds);

            // Calculate cell bounds with padding for gap effect
            int topPadding = 6;
            int bottomPadding = 6;

            // Fill cell with color (leaving space for borders)
            Rectangle cellBounds = new Rectangle(
                e.CellBounds.X,
                e.CellBounds.Y + topPadding,
                e.CellBounds.Width - 1,  // Leave 1px for right border
                e.CellBounds.Height - topPadding - bottomPadding - 1  // Leave 1px for bottom border
            );

            using (var brush = new SolidBrush(pillColor))
            {
                e.Graphics.FillRectangle(brush, cellBounds);
            }

            Color textColor;
            if (isSelected)
            {
                textColor = Color.White;
            }
            else if (isActionColumn)
            {
                textColor = Color.FromArgb(41, 128, 185);
            }
            else if (isStatusColumn)
            {
                var statusText = (e.FormattedValue?.ToString() ?? string.Empty).Trim();
                if (string.Equals(statusText, "Complete", StringComparison.OrdinalIgnoreCase))
                    textColor = Color.FromArgb(39, 174, 96);
                else if (string.Equals(statusText, "Pending", StringComparison.OrdinalIgnoreCase))
                    textColor = Color.FromArgb(192, 57, 43);
                else
                    textColor = Color.FromArgb(40, 40, 40);
            }
            else if (isRenewalColumn)
            {
                var txt = (e.FormattedValue?.ToString() ?? string.Empty).Trim();
                textColor = txt.StartsWith("✓", StringComparison.OrdinalIgnoreCase)
                    ? Color.FromArgb(39, 174, 96)
                    : Color.FromArgb(90, 90, 90);
            }
            else
            {
                textColor = Color.FromArgb(40, 40, 40);
            }

            // Handle checkbox column
            if (column is DataGridViewCheckBoxColumn)
            {
                var checkBoxSize = 18;
                var checkBoxX = e.CellBounds.X + (e.CellBounds.Width - checkBoxSize) / 2;
                var checkBoxY = e.CellBounds.Y + (e.CellBounds.Height - checkBoxSize) / 2;
                var checkBoxRect = new Rectangle(checkBoxX, checkBoxY, checkBoxSize, checkBoxSize);

                bool isChecked = e.Value is bool b && b;

                var state = isChecked
                    ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                    : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;

                CheckBoxRenderer.DrawCheckBox(e.Graphics, checkBoxRect.Location, state);
            }
            // Paint cell content (use FormattedValue so unbound columns still render)
            else if (e.FormattedValue != null)
            {
                var textRect = new Rectangle(
                    e.CellBounds.X + (isCenteredDataColumn ? 0 : 10),
                    e.CellBounds.Y + topPadding,
                    e.CellBounds.Width - (isCenteredDataColumn ? 0 : 20),
                    e.CellBounds.Height - topPadding - bottomPadding
                );

                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
                flags |= isCenteredDataColumn ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left;

                TextRenderer.DrawText(e.Graphics, e.FormattedValue?.ToString() ?? string.Empty,
                    e.CellStyle.Font ?? grid.Font, textRect, textColor, flags);
            }

            // Draw white vertical border on the right edge of the cell
            using (var borderPen = new Pen(Color.White, 1))
            {
                e.Graphics.DrawLine(borderPen,
                    e.CellBounds.Right - 1, e.CellBounds.Top,
                    e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
            }

            // Draw white horizontal border on the bottom edge of the cell
            using (var borderPen = new Pen(Color.White, 1))
            {
                e.Graphics.DrawLine(borderPen,
                    e.CellBounds.Left, e.CellBounds.Bottom - 1,
                    e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }

            e.Handled = true;
        }

        private void RefreshSupplierFilterOptions()
        {
            if (_cboSupplierFilter == null)
                return;

            var desired = !string.IsNullOrWhiteSpace(_restoreSupplier)
                ? _restoreSupplier
                : (_cboSupplierFilter.SelectedItem?.ToString() ?? string.Empty);

            _suppressApplySearchFilter = true;
            try
            {
                _cboSupplierFilter.BeginUpdate();
                _cboSupplierFilter.Items.Clear();
                _cboSupplierFilter.Items.Add("All Suppliers");

                var suppliers = (_allReceipts ?? new List<ReceiptSetDto>())
                    .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Supplier))
                    .Select(r => r.Supplier.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var s in suppliers)
                    _cboSupplierFilter.Items.Add(s);

                int selectedIndex = 0;
                if (!string.IsNullOrWhiteSpace(desired))
                {
                    for (int i = 1; i < _cboSupplierFilter.Items.Count; i++)
                    {
                        if (string.Equals(_cboSupplierFilter.Items[i]?.ToString(), desired, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = i;
                            break;
                        }
                    }
                }

                _cboSupplierFilter.SelectedIndex = selectedIndex;
            }
            finally
            {
                _cboSupplierFilter.EndUpdate();
                _restoreSupplier = string.Empty;
                _suppressApplySearchFilter = false;
            }
        }

        private void LoadReceiptSets()
        {
            SetLoading(true, "Loading receipt sets…");
            Application.DoEvents();
            try
            {
                // Show only the latest (current) receipt per linked Set to prevent duplicate rows
                // when receipts are renewed. Older periods remain accessible in the viewer's
                // Previous/History tabs.
                var data = _repository.GetAllMetadataCurrentPerSet();
                _allReceipts = data?.ToList() ?? new List<ReceiptSetDto>();

                RefreshSupplierFilterOptions();
                ApplySearchFilter();
                UpdateClearFiltersButtonVisibility();
            }
            catch (Exception ex)
            {
                _allReceipts = new List<ReceiptSetDto>();
                _gridReceipts.DataSource = null;

                string details = ex.Message;
                string guidance = "";

                if (!string.IsNullOrWhiteSpace(details) && details.IndexOf("ReceiptSetLink", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    guidance =
                        "\n\n" +
                        "This feature requires the dbo.ReceiptSetLink table.\n" +
                        "Run the database migration script:\n" +
                        "Yakult.Inventory.App/Database_Migration_ReceiptSetLinkCoverage.sql";
                }

                MessageBox.Show(this,
                    "Failed to load receipt sets.\n\n" +
                    details +
                    guidance,
                    "Receipt Sets",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                SetLoading(false, null);
            }
        }

        private void ReloadReceiptSetsPreserveState(int? receiptSetIdToSelect)
        {
            var prevPage = _currentPage;
            var prevSortKey = _sortColumn?.DataPropertyName;
            var prevSortOrder = _sortOrder;

            LoadReceiptSets();

            if (!string.IsNullOrWhiteSpace(prevSortKey) && prevSortOrder != System.Windows.Forms.SortOrder.None)
            {
                var direction = prevSortOrder == System.Windows.Forms.SortOrder.Ascending
                    ? System.ComponentModel.ListSortDirection.Ascending
                    : System.ComponentModel.ListSortDirection.Descending;

                SortDataSource(prevSortKey, direction);
            }

            _currentPage = prevPage;
            UpdatePagination();

            if (!string.IsNullOrWhiteSpace(prevSortKey) && prevSortOrder != System.Windows.Forms.SortOrder.None && _gridReceipts != null)
            {
                var col = _gridReceipts.Columns
                    .Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => string.Equals(c.DataPropertyName, prevSortKey, StringComparison.OrdinalIgnoreCase));

                if (col != null)
                {
                    foreach (DataGridViewColumn c in _gridReceipts.Columns)
                        c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;

                    col.HeaderCell.SortGlyphDirection = prevSortOrder;
                    _sortColumn = col;
                    _sortOrder = prevSortOrder;

                    if (cmbSortBy != null)
                        DefaultListPageTemplate.SyncSortByDropdown(cmbSortBy, _sortColumn, _sortOrder);
                }
            }

            if (receiptSetIdToSelect.HasValue)
            {
                TrySelectReceiptSetRow(receiptSetIdToSelect.Value);
            }
        }

        private void TrySelectReceiptSetRow(int receiptSetId)
        {
            if (_gridReceipts == null || _gridReceipts.Rows == null)
                return;

            foreach (DataGridViewRow row in _gridReceipts.Rows)
            {
                if (row?.DataBoundItem is ReceiptSetDto dto && dto.ReceiptSetId == receiptSetId)
                {
                    row.Selected = true;
                    _gridReceipts.CurrentCell = row.Cells.Cast<DataGridViewCell>().FirstOrDefault(c => c.Visible) ?? row.Cells[0];
                    _gridReceipts.FirstDisplayedScrollingRowIndex = Math.Max(0, row.Index);
                    return;
                }
            }
        }

        private bool ConfirmBulkDelete(List<ReceiptSetDto> selected)
        {
            if (selected == null || selected.Count == 0)
                return false;

            var locked = selected
                .Where(s => IsLockedForDelete(s, out _))
                .ToList();

            if (locked.Count > 0)
            {
                var preview = string.Join(Environment.NewLine,
                    locked
                        .OrderByDescending(s => s.CreatedAt)
                        .Take(15)
                        .Select(s => $"{s.CreatedAt:yyyy-MM-dd HH:mm} | {(s.SetCode ?? "").Trim()} | {(s.Supplier ?? "").Trim()}"));

                MessageBox.Show(this,
                    "Delete is disabled for linked/locked receipt sets.\n\n" +
                    "Unlink these first, then try again:\n\n" +
                    preview,
                    "Delete Disabled",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            // For a single item, a detailed yes/no is enough.
            if (selected.Count == 1)
            {
                var dto = selected[0];
                var details = BuildDeleteDetails(dto);
                var confirm = MessageBox.Show(this,
                    "Delete this receipt set?\n\n" + details,
                    "Delete Receipt Set",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                return confirm == DialogResult.Yes;
            }

            // For multiple deletes, require typing DELETE.
            var previewLines = selected
                .OrderByDescending(s => s.CreatedAt)
                .Take(30)
                .Select(s =>
                {
                    var supplier = string.IsNullOrWhiteSpace(s.Supplier) ? "(none)" : s.Supplier.Trim();
                    var setCodes = string.IsNullOrWhiteSpace(s.SetCode) ? "(none)" : s.SetCode.Trim();
                    var createdAt = s.CreatedAt != default(DateTime) ? s.CreatedAt.ToString("yyyy-MM-dd HH:mm") : "(unknown)";
                    return $"{createdAt} | {setCodes} | {supplier}";
                })
                .ToList();

            var detailsText = string.Join(Environment.NewLine, previewLines);
            if (selected.Count > previewLines.Count)
            {
                detailsText += Environment.NewLine + $"… and {selected.Count - previewLines.Count} more";
            }

            using (var dialog = new System.Windows.Forms.Form())
            {
                dialog.Text = "Confirm Delete Selected";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Width = 720;
                dialog.Height = 520;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
                dialog.ShowInTaskbar = false;

                var lbl = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 60,
                    Text = $"You are about to delete {selected.Count} receipt set(s).\n\nType DELETE to confirm.",
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    Padding = new Padding(12, 12, 12, 0)
                };

                var details = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Font = new Font("Consolas", 9F, FontStyle.Regular),
                    Text = detailsText
                };

                var bottom = new System.Windows.Forms.Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 84,
                    Padding = new Padding(12, 10, 12, 10)
                };

                var input = new TextBox
                {
                    Width = 240,
                    Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
                    Font = new Font("Segoe UI", 10F, FontStyle.Regular)
                };

                var btnCancel = new System.Windows.Forms.Button
                {
                    Text = "Cancel",
                    Width = 100,
                    Anchor = AnchorStyles.Right | AnchorStyles.Bottom
                };
                btnCancel.Click += (s, e) => dialog.DialogResult = DialogResult.Cancel;

                var btnDelete = new System.Windows.Forms.Button
                {
                    Text = "Delete",
                    Width = 100,
                    Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                    Enabled = false
                };
                btnDelete.Click += (s, e) => dialog.DialogResult = DialogResult.OK;

                input.TextChanged += (s, e) =>
                {
                    btnDelete.Enabled = string.Equals(input.Text?.Trim(), "DELETE", StringComparison.OrdinalIgnoreCase);
                };

                var bottomLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 3,
                    RowCount = 1
                };
                bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

                var left = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    AutoSize = true
                };
                left.Controls.Add(new Label
                {
                    AutoSize = true,
                    Text = "Type:",
                    Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                    Margin = new Padding(0, 8, 8, 0)
                });
                left.Controls.Add(input);

                var right = new FlowLayoutPanel
                {
                    AutoSize = true,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Dock = DockStyle.Fill
                };
                right.Controls.Add(btnCancel);
                right.Controls.Add(new Label { Width = 10, Height = 1 });
                right.Controls.Add(btnDelete);

                bottomLayout.Controls.Add(left, 0, 0);
                bottomLayout.Controls.Add(new System.Windows.Forms.Panel { Width = 1, Height = 1 }, 1, 0);
                bottomLayout.Controls.Add(right, 2, 0);
                bottom.Controls.Add(bottomLayout);

                dialog.Controls.Add(details);
                dialog.Controls.Add(bottom);
                dialog.Controls.Add(lbl);

                dialog.AcceptButton = btnDelete;
                dialog.CancelButton = btnCancel;

                return dialog.ShowDialog(this) == DialogResult.OK;
            }
        }

        private void UpdateSummaryCards(IEnumerable<ReceiptSetDto> data)
        {
            if (_lblTotalCount == null || _lblCompleteCount == null || _lblPendingCount == null)
                return;

            var list = data?.ToList() ?? new List<ReceiptSetDto>();

            int total = list.Count;
            int complete = list.Count(dto => IsReceiptComplete(dto));
            int pending = total - complete;

            _lblTotalCount.Text = total.ToString();
            _lblCompleteCount.Text = complete.ToString();
            _lblPendingCount.Text = pending.ToString();
        }

        /// <summary>
        /// Applies the text in the HopeTextBox search field to the in-memory receipts list
        /// and binds the filtered result to the grid, while keeping the summary cards in sync.
        /// </summary>
        private void ApplySearchFilter()
        {
            var showFiltering = !_restoringState &&
                                !_isFilteringUi &&
                                _allReceipts != null &&
                                _allReceipts.Count > 200;

            if (showFiltering)
            {
                _isFilteringUi = true;
                SetLoading(true, "Filtering…");
                Application.DoEvents();
            }

            if (_allReceipts == null)
            {
                _filteredReceipts = new List<ReceiptSetDto>();
                UpdateSummaryCards(Enumerable.Empty<ReceiptSetDto>());
                _currentPage = 1;
                UpdatePagination();
                if (showFiltering)
                {
                    SetLoading(false, null);
                    _isFilteringUi = false;
                }
                return;
            }

            var filtered = _allReceipts.AsEnumerable();

            var term = _txtSearch?.Text;
            if (!string.IsNullOrWhiteSpace(term))
            {
                var lower = term.Trim().ToLowerInvariant();
                filtered = filtered.Where(r =>
                    (!string.IsNullOrWhiteSpace(r.SetCode) && r.SetCode.ToLowerInvariant().Contains(lower)) ||
                    (!string.IsNullOrWhiteSpace(r.Supplier) && r.Supplier.ToLowerInvariant().Contains(lower)) ||
                    (!string.IsNullOrWhiteSpace(r.SiNumber) && r.SiNumber.ToLowerInvariant().Contains(lower)) ||
                    (!string.IsNullOrWhiteSpace(r.DrNumber) && r.DrNumber.ToLowerInvariant().Contains(lower)) ||
                    (!string.IsNullOrWhiteSpace(r.PoNumber) && r.PoNumber.ToLowerInvariant().Contains(lower))
                );
            }

            var selectedStatus = _cboFilterStatus?.SelectedItem?.ToString();
            if (!string.IsNullOrWhiteSpace(selectedStatus) && selectedStatus != "All")
            {
                if (selectedStatus == "Complete")
                {
                    filtered = filtered.Where(r => IsReceiptComplete(r));
                }
                else if (selectedStatus == "Pending")
                {
                    filtered = filtered.Where(r => !IsReceiptComplete(r));
                }
            }

            var selectedSupplier = _cboSupplierFilter?.SelectedItem?.ToString();
            if (!string.IsNullOrWhiteSpace(selectedSupplier) &&
                !string.Equals(selectedSupplier, "All Suppliers", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(r =>
                    string.Equals((r.Supplier ?? string.Empty).Trim(), selectedSupplier.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (_dtpCreatedFrom != null && _dtpCreatedFrom.Checked)
            {
                var from = _dtpCreatedFrom.Value.Date;
                filtered = filtered.Where(r => r.CreatedAt != default(DateTime) && r.CreatedAt.Date >= from);
            }

            if (_dtpCreatedTo != null && _dtpCreatedTo.Checked)
            {
                var to = _dtpCreatedTo.Value.Date;
                filtered = filtered.Where(r => r.CreatedAt != default(DateTime) && r.CreatedAt.Date <= to);
            }

            var filteredList = filtered.ToList();
            _filteredReceipts = filteredList;
            UpdateSummaryCards(filteredList);
            _currentPage = 1;

            ApplyRestoredSortIfNeeded();
            ApplyCurrentSortToFilteredIfAny();
            UpdatePagination();
            ApplyCurrentSortGlyphToGrid();

            if (showFiltering)
            {
                SetLoading(false, null);
                _isFilteringUi = false;
            }
        }

        private void ApplyRestoredSortIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(_restoreSortKey) || _restoreSortOrder == System.Windows.Forms.SortOrder.None)
                return;

            var direction = _restoreSortOrder == System.Windows.Forms.SortOrder.Ascending
                ? System.ComponentModel.ListSortDirection.Ascending
                : System.ComponentModel.ListSortDirection.Descending;

            SortDataSource(_restoreSortKey, direction);

            _sortOrder = _restoreSortOrder;
            if (_gridReceipts != null && _gridReceipts.Columns != null)
            {
                var col = _gridReceipts.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(c =>
                        string.Equals(c.DataPropertyName, _restoreSortKey, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.Name, _restoreSortKey, StringComparison.OrdinalIgnoreCase));
                if (col != null)
                    _sortColumn = col;
            }

            _restoreSortKey = string.Empty;
            _restoreSortOrder = System.Windows.Forms.SortOrder.None;
        }

        private void ApplyCurrentSortToFilteredIfAny()
        {
            if (_sortColumn == null || _sortOrder == System.Windows.Forms.SortOrder.None)
                return;

            var key = !string.IsNullOrWhiteSpace(_sortColumn.DataPropertyName)
                ? _sortColumn.DataPropertyName
                : _sortColumn.Name;

            if (string.IsNullOrWhiteSpace(key))
                return;

            var direction = _sortOrder == System.Windows.Forms.SortOrder.Ascending
                ? System.ComponentModel.ListSortDirection.Ascending
                : System.ComponentModel.ListSortDirection.Descending;

            SortDataSource(key, direction);
        }

        private void ApplyCurrentSortGlyphToGrid()
        {
            if (_gridReceipts == null || _gridReceipts.Columns == null)
                return;

            foreach (DataGridViewColumn c in _gridReceipts.Columns)
                c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;

            if (_sortColumn != null && _sortOrder != System.Windows.Forms.SortOrder.None)
            {
                _sortColumn.HeaderCell.SortGlyphDirection = _sortOrder;
                if (cmbSortBy != null)
                    DefaultListPageTemplate.SyncSortByDropdown(cmbSortBy, _sortColumn, _sortOrder);
            }
        }

        private void ApplyTheme()
        {
            BackColor = Color.White;

            if (_headerPanel != null)
                _headerPanel.BackColor = Color.FromArgb(245, 247, 250);
            if (_buttonBarPanel != null)
                _buttonBarPanel.BackColor = Color.FromArgb(245, 247, 250);
            if (_titleLabel != null)
                _titleLabel.ForeColor = Color.FromArgb(40, 40, 40);

            if (_summaryPanel != null)
                _summaryPanel.BackColor = Color.White;
            if (_bodyPanel != null)
                _bodyPanel.BackColor = Color.White;
            if (_gridCard != null)
                _gridCard.BackColor = Color.White;

            if (_gridReceipts != null)
            {
                _gridReceipts.BackgroundColor = Color.White;
                _gridReceipts.GridColor = Color.White;
                _gridReceipts.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 152, 219);
                _gridReceipts.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                _gridReceipts.DefaultCellStyle.ForeColor = Color.FromArgb(40, 40, 40);
            }
        }

        #region Sorting Implementation

        // Common date column name candidates for created date
        private static readonly string[] CreatedDateCandidates = new[]
        {
            "CreatedOn",
            "DateCreated",
            "CreatedDate",
            "CreatedAt",
            "Date",
            "LastModified"
        };

        /// <summary>
        /// Resolves the created date column for this grid by checking which candidate column exists
        /// </summary>
        private string ResolveCreatedDateColumn()
        {
            if (_gridReceipts == null || _gridReceipts.Columns == null)
                return null;

            foreach (var candidate in CreatedDateCandidates)
            {
                var col = _gridReceipts.Columns
                    .Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => string.Equals(c.DataPropertyName, candidate, StringComparison.OrdinalIgnoreCase));

                if (col != null)
                    return col.DataPropertyName;
            }

            return null;
        }

        /// <summary>
        /// Enables column header sorting by configuring sort modes and wiring the column header click event
        /// </summary>
        private void EnableSortingGlyphs()
        {
            if (_gridReceipts == null)
                return;

            // Configure sort mode for each column
            foreach (DataGridViewColumn column in _gridReceipts.Columns)
            {
                if (column is DataGridViewCheckBoxColumn ||
                    column is DataGridViewButtonColumn ||
                    column is DataGridViewImageColumn ||
                    string.Equals(column.Name, "Renewed", StringComparison.OrdinalIgnoreCase))
                {
                    column.SortMode = DataGridViewColumnSortMode.NotSortable;
                }
                else
                {
                    column.SortMode = DataGridViewColumnSortMode.Programmatic;
                }
            }

            // Enable visual styles for sort glyphs
            _gridReceipts.EnableHeadersVisualStyles = false;

            // Wire up the column header click event (remove existing handler first to avoid duplicates)
            _gridReceipts.ColumnHeaderMouseClick -= GridReceipts_ColumnHeaderMouseClick;
            _gridReceipts.ColumnHeaderMouseClick += GridReceipts_ColumnHeaderMouseClick;
        }

        /// <summary>
        /// Handles column header clicks for sorting
        /// </summary>
        private void GridReceipts_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (_gridReceipts == null || e.ColumnIndex < 0 || e.ColumnIndex >= _gridReceipts.Columns.Count)
                return;

            var clickedColumn = _gridReceipts.Columns[e.ColumnIndex];

            if (string.Equals(clickedColumn.Name, "Select", StringComparison.OrdinalIgnoreCase))
            {
                ToggleAllVisibleChecks();
                return;
            }

            // Don't sort non-sortable columns
            if (clickedColumn.SortMode == DataGridViewColumnSortMode.NotSortable)
                return;

            // Determine new sort direction
            System.Windows.Forms.SortOrder newSortOrder;
            if (clickedColumn.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.None ||
                clickedColumn.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Descending)
            {
                newSortOrder = System.Windows.Forms.SortOrder.Ascending;
            }
            else
            {
                newSortOrder = System.Windows.Forms.SortOrder.Descending;
            }

            // Clear all glyphs
            foreach (DataGridViewColumn col in _gridReceipts.Columns)
            {
                col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            }

            // Apply sort
            string propertyName = clickedColumn.DataPropertyName;
            if (!string.IsNullOrEmpty(propertyName))
            {
                var direction = newSortOrder == System.Windows.Forms.SortOrder.Ascending
                    ? System.ComponentModel.ListSortDirection.Ascending
                    : System.ComponentModel.ListSortDirection.Descending;

                // Track current sort state so the Sort By dropdown can stay in sync
                _sortColumn = clickedColumn;
                _sortOrder = newSortOrder;

                SortDataSource(propertyName, direction);

                // Refresh display first (this rebinds data)
                UpdatePagination();

                // Set glyph AFTER pagination to persist it
                clickedColumn.HeaderCell.SortGlyphDirection = newSortOrder;

                // Sync the Sort By dropdown with the column header sort
                if (cmbSortBy != null && _sortColumn != null)
                {
                    DefaultListPageTemplate.SyncSortByDropdown(cmbSortBy, _sortColumn, _sortOrder);
                }

                UpdateClearFiltersButtonVisibility();
                ScheduleSaveState();
            }
        }

        /// <summary>
        /// Sorts the underlying data source and refreshes the grid
        /// </summary>
        private void SortDataSource(string propertyName, System.ComponentModel.ListSortDirection direction)
        {
            if (_filteredReceipts == null || _filteredReceipts.Count == 0)
                return;

            try
            {
                Func<ReceiptSetDto, DateTime> dateKey = null;
                Func<ReceiptSetDto, string> textKey = null;

                if (string.Equals(propertyName, "CreatedAt", StringComparison.OrdinalIgnoreCase))
                    dateKey = x => x.CreatedAt;
                else if (string.Equals(propertyName, "SetCode", StringComparison.OrdinalIgnoreCase))
                    textKey = x => x.SetCode ?? string.Empty;
                else if (string.Equals(propertyName, "Supplier", StringComparison.OrdinalIgnoreCase))
                    textKey = x => x.Supplier ?? string.Empty;
                else if (string.Equals(propertyName, "SiNumber", StringComparison.OrdinalIgnoreCase))
                    textKey = x => x.SiNumber ?? string.Empty;
                else if (string.Equals(propertyName, "DrNumber", StringComparison.OrdinalIgnoreCase))
                    textKey = x => x.DrNumber ?? string.Empty;
                else if (string.Equals(propertyName, "PoNumber", StringComparison.OrdinalIgnoreCase))
                    textKey = x => x.PoNumber ?? string.Empty;
                else
                    return;

                if (dateKey != null)
                {
                    _filteredReceipts = direction == System.ComponentModel.ListSortDirection.Ascending
                        ? _filteredReceipts.OrderBy(dateKey).ToList()
                        : _filteredReceipts.OrderByDescending(dateKey).ToList();
                }
                else if (textKey != null)
                {
                    _filteredReceipts = direction == System.ComponentModel.ListSortDirection.Ascending
                        ? _filteredReceipts.OrderBy(textKey, StringComparer.OrdinalIgnoreCase).ToList()
                        : _filteredReceipts.OrderByDescending(textKey, StringComparer.OrdinalIgnoreCase).ToList();
                }
            }
            catch
            {
                // If sorting fails, leave data as-is
            }
        }

        /// <summary>
        /// Handles Filter By dropdown selection changes
        /// </summary>
        private void CmbFilterBy_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressApplySearchFilter)
                return;

            if (cmbFilterBy == null || _filteredReceipts == null)
                return;

            string selectedFilter = cmbFilterBy.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedFilter))
                return;

            // Resolve the created date column
            string dateColumn = ResolveCreatedDateColumn();

            if (string.IsNullOrEmpty(dateColumn))
            {
                // No date column found - cannot apply date-based filter
                if (selectedFilter != "Default")
                {
                    MessageBox.Show(
                        "Cannot apply date-based filter: No date column found in the grid.",
                        "Filter Not Available",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    cmbFilterBy.SelectedIndex = 0; // Reset to Default
                }
                return;
            }

            // Clear all existing sort glyphs
            foreach (DataGridViewColumn col in _gridReceipts.Columns)
            {
                col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            }

            // Apply filter-based sorting
            System.ComponentModel.ListSortDirection? dir = null;
            if (selectedFilter == "Most Recently Added")
                dir = System.ComponentModel.ListSortDirection.Descending;
            else if (selectedFilter == "Oldest Added")
                dir = System.ComponentModel.ListSortDirection.Ascending;

            if (dir.HasValue)
            {
                // Sort by the resolved date column
                SortDataSource(dateColumn, dir.Value);

                // Set sort glyph on the date column
                var dateCol = _gridReceipts.Columns
                    .Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => string.Equals(c.DataPropertyName, dateColumn, StringComparison.OrdinalIgnoreCase));

                if (dateCol != null)
                {
                    dateCol.HeaderCell.SortGlyphDirection = dir.Value == System.ComponentModel.ListSortDirection.Ascending
                        ? System.Windows.Forms.SortOrder.Ascending
                        : System.Windows.Forms.SortOrder.Descending;
                }
            }

            // Refresh display
            UpdatePagination();
        }

        #endregion
    }
}
