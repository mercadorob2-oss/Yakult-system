using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Software
{
    public partial class SoftwareItemPickerDialog : Window, IDisposable
    {
        private readonly string _connectionString;
        private readonly HashSet<int> _excludedItemIds;
        private readonly bool _isForInvoiceSet;
        private readonly List<int> _preSelectedItemIds;
        private readonly DispatcherTimer _searchTimer;
        private List<ItemRowViewModel> _allRows    = new List<ItemRowViewModel>();
        private List<ItemRowViewModel> _currentRows = new List<ItemRowViewModel>();
        private int _currentPage = 1;
        private int _pageSize    = 25;

        // Tracks checked rows by ItemId across searches, since LoadItems() rebuilds
        // _allRows from scratch on every search/refresh and would otherwise drop
        // selections made under a previous search term.
        private readonly Dictionary<int, ItemRowViewModel> _selectedCache = new Dictionary<int, ItemRowViewModel>();

        public List<ItemDto> SelectedItems { get; private set; } = new List<ItemDto>();

        // ── Constructors ─────────────────────────────────────────────────────
        public SoftwareItemPickerDialog(IEnumerable<int> excludedItemIds = null, bool isForInvoiceSet = false, IEnumerable<int> preSelectedItemIds = null)
        {
            _connectionString = DatabaseConfig.ConnectionString;
            _isForInvoiceSet = isForInvoiceSet;

            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string not configured.");

            _excludedItemIds = excludedItemIds != null
                ? new HashSet<int>(excludedItemIds)
                : new HashSet<int>();
            _preSelectedItemIds = preSelectedItemIds?.ToList() ?? new List<int>();

            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); LoadItems(); };

            InitializeComponent();
            Loaded += (s, e) =>
            {
                CmbPageSize.SelectedIndex = 0; // 25
                LoadItems();
                ApplyPreSelection();
            };
        }

        /// <summary>Pre-checks any rows whose ItemId was passed in via preSelectedItemIds
        /// — used by the "bulk add Sub-Type Groups" flow so a group's items arrive already
        /// checked for review. Runs once, right after the first LoadItems(); subsequent
        /// searches keep them checked via the existing _selectedCache restore logic.</summary>
        private void ApplyPreSelection()
        {
            if (_preSelectedItemIds.Count == 0) return;

            foreach (var itemId in _preSelectedItemIds)
            {
                var row = _allRows.FirstOrDefault(r => r.ItemId == itemId);
                if (row == null) continue;
                row.Selected = true;
                _selectedCache[itemId] = row;
            }

            ApplyPagination();
        }

        // ── WinForms-compatible ShowDialog overloads ─────────────────────────
        public new WinForms.DialogResult ShowDialog()
        {
            bool? result = base.ShowDialog();
            return result == true ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }

        public WinForms.DialogResult ShowDialog(WinForms.IWin32Window owner)
        {
            if (owner != null)
                new System.Windows.Interop.WindowInteropHelper(this).Owner = owner.Handle;
            return ShowDialog();
        }

        // ── IDisposable ───────────────────────────────────────────────────────
        public void Dispose()
        {
            _searchTimer?.Stop();
        }

        // ── Header chrome ─────────────────────────────────────────────────────
        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void OnMinimizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Maximized)
            {
                var wa = SystemParameters.WorkArea;
                MaxWidth  = wa.Width;
                MaxHeight = wa.Height;
                Left = wa.Left;
                Top  = wa.Top;
                BtnMaximizeGlyph.Text = "❐";
            }
            else
            {
                MaxWidth  = double.PositiveInfinity;
                MaxHeight = double.PositiveInfinity;
                BtnMaximizeGlyph.Text = "□";
            }
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            DialogResult = false;
        }

        // ── Filter event handlers ─────────────────────────────────────────────
        private void TxtFilter_TextChanged(object sender,
            System.Windows.Controls.TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            _searchTimer.Stop();
            LoadItems();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _searchTimer.Stop();
            TxtSearchName.Clear();
            TxtSearchCategory.Clear();
            TxtSearchSerial.Clear();
            ChkShowInactive.IsChecked = false;
            LoadItems();
        }

        private void ChkShowInactive_Changed(object sender, RoutedEventArgs e)
        {
            _searchTimer.Stop();
            LoadItems();
        }

        // ── Selected-items review ────────────────────────────────────────────
        private void BdrSelectedCount_Click(object sender, MouseButtonEventArgs e)
        {
            var summaries = _selectedCache.Values
                .OrderBy(r => r.Name)
                .Select(r => new SelectedItemSummary
                {
                    ItemId       = r.ItemId,
                    Name         = r.Name,
                    Category     = r.Category,
                    SerialNumber = r.SerialNumber
                })
                .ToList();

            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var owner = new Win32WindowWrapper(hwnd);

            using (var reviewDialog = new SelectedItemsReviewDialog(summaries))
            {
                reviewDialog.ShowDialog(owner);

                foreach (var removedId in reviewDialog.RemovedItemIds)
                {
                    if (_selectedCache.TryGetValue(removedId, out var row))
                        row.Selected = false;
                }
            }

            UpdateSelectionHint();
        }

        // ── Select-All header checkbox ────────────────────────────────────────
        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            var chk = sender as System.Windows.Controls.CheckBox;
            if (chk == null) return;
            bool check = chk.IsChecked == true;
            foreach (var row in _currentRows)
                row.Selected = check;
            UpdateSelectionHint();
        }

        // ── Data loading ──────────────────────────────────────────────────────
        private void LoadItems()
        {
            var rows = new List<ItemRowViewModel>();

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    var whereParts = new List<string> { "1 = 1" };

                    if (ChkShowInactive.IsChecked != true)
                        whereParts.Add("i.Active = 1");

                    // Each search box matches across every displayed column (ID, Name, Description,
                    // Category, Serial No., Model No., Unit, Stock), not just its own labeled field.
                    string AllColumnsMatch(string paramName) => $@"(
                        CAST(i.ItemId AS NVARCHAR(20)) LIKE {paramName} OR
                        i.Name           LIKE {paramName} OR
                        i.Description    LIKE {paramName} OR
                        i.Category       LIKE {paramName} OR
                        i.SerialNumber   LIKE {paramName} OR
                        i.ModelNumber    LIKE {paramName} OR
                        i.UnitOfMeasure  LIKE {paramName} OR
                        CAST(i.StockOnHand AS NVARCHAR(20)) LIKE {paramName}
                    )";

                    if (!string.IsNullOrWhiteSpace(TxtSearchName.Text))
                        whereParts.Add(AllColumnsMatch("@Name"));

                    if (!string.IsNullOrWhiteSpace(TxtSearchCategory.Text))
                        whereParts.Add(AllColumnsMatch("@Category"));

                    if (!string.IsNullOrWhiteSpace(TxtSearchSerial.Text))
                        whereParts.Add(AllColumnsMatch("@Serial"));

                    string sql = $@"
SELECT
    i.ItemId,
    i.Name,
    i.Description,
    i.Category,
    i.SerialNumber,
    i.ModelNumber,
    i.CategoryId,
    i.UnitOfMeasure,
    i.StockOnHand,
    i.DateCreated,
    i.CreatedBy,
    u.Name AS CreatedByName,
    i.StartDate,
    i.EndDate
FROM dbo.Item i
LEFT JOIN dbo.[User] u ON i.CreatedBy = u.UserId
WHERE {string.Join(" AND ", whereParts)}";

                    if (_isForInvoiceSet)
                    {
                        sql += @"
    AND i.ItemId NOT IN (
        SELECT DISTINCT r.ItemId
        FROM dbo.Request r
        INNER JOIN dbo.[Set] s ON r.ReqId = s.ReqId
        WHERE s.IsInvoice = 1
            AND s.Status IS NOT NULL
            AND r.ItemId IS NOT NULL
    )
    AND i.ItemId NOT IN (
        SELECT DISTINCT si.ItemId
        FROM dbo.SetItem si
        INNER JOIN dbo.[Set] s ON si.SetId = s.SetId
        WHERE s.IsInvoice = 1
            AND s.ReqId IS NULL
            AND s.Status IS NOT NULL
            AND si.ItemId IS NOT NULL
    )";
                    }

                    sql += " ORDER BY i.Name";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        if (!string.IsNullOrWhiteSpace(TxtSearchName.Text))
                            cmd.Parameters.AddWithValue("@Name", $"%{TxtSearchName.Text.Trim()}%");

                        if (!string.IsNullOrWhiteSpace(TxtSearchCategory.Text))
                            cmd.Parameters.AddWithValue("@Category", $"%{TxtSearchCategory.Text.Trim()}%");

                        if (!string.IsNullOrWhiteSpace(TxtSearchSerial.Text))
                            cmd.Parameters.AddWithValue("@Serial", $"%{TxtSearchSerial.Text.Trim()}%");

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                rows.Add(new ItemRowViewModel
                                {
                                    ItemId      = reader.GetInt32(reader.GetOrdinal("ItemId")),
                                    Name        = reader.GetString(reader.GetOrdinal("Name")),
                                    Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                                        ? null : reader.GetString(reader.GetOrdinal("Description")),
                                    Category    = reader.IsDBNull(reader.GetOrdinal("Category"))
                                        ? null : reader.GetString(reader.GetOrdinal("Category")),
                                    SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber"))
                                        ? null : reader.GetString(reader.GetOrdinal("SerialNumber")),
                                    ModelNumber  = reader.IsDBNull(reader.GetOrdinal("ModelNumber"))
                                        ? null : reader.GetString(reader.GetOrdinal("ModelNumber")),
                                    CategoryId   = reader.IsDBNull(reader.GetOrdinal("CategoryId"))
                                        ? 0 : reader.GetInt32(reader.GetOrdinal("CategoryId")),
                                    UnitOfMeasure = reader.IsDBNull(reader.GetOrdinal("UnitOfMeasure"))
                                        ? null : reader.GetString(reader.GetOrdinal("UnitOfMeasure")),
                                    StockOnHand  = reader.IsDBNull(reader.GetOrdinal("StockOnHand"))
                                        ? 0 : reader.GetInt32(reader.GetOrdinal("StockOnHand")),
                                    DateCreated  = reader.IsDBNull(reader.GetOrdinal("DateCreated"))
                                        ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                                    CreatedByUserId = reader.IsDBNull(reader.GetOrdinal("CreatedBy"))
                                        ? 0 : reader.GetInt32(reader.GetOrdinal("CreatedBy")),
                                    CreatedByName = reader.IsDBNull(reader.GetOrdinal("CreatedByName"))
                                        ? null : reader.GetString(reader.GetOrdinal("CreatedByName")),
                                    StartDate    = reader.IsDBNull(reader.GetOrdinal("StartDate"))
                                        ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("StartDate")),
                                    EndDate      = reader.IsDBNull(reader.GetOrdinal("EndDate"))
                                        ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("EndDate"))
                                });
                            }
                        }
                    }
                }

                if (_excludedItemIds.Count > 0)
                    rows = rows.Where(r => !_excludedItemIds.Contains(r.ItemId)).ToList();

                foreach (var row in rows)
                {
                    // Restore checkbox state from a previous search before wiring the handler,
                    // so re-applying it doesn't count as a fresh user selection.
                    row.Selected = _selectedCache.ContainsKey(row.ItemId);

                    // Only wire the handler once per row instance to avoid duplicate firing.
                    if (!row.HandlerWired)
                    {
                        row.HandlerWired = true;
                        row.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName != nameof(ItemRowViewModel.Selected)) return;

                            if (row.Selected)
                                _selectedCache[row.ItemId] = row;
                            else
                                _selectedCache.Remove(row.ItemId);

                            UpdateSelectionHint();
                        };
                    }
                }

                _allRows = rows;
                _currentPage = 1;
                ApplyPagination();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load items: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSelectionHint()
        {
            int count = _selectedCache.Count;
            TxtSelectionHint.Text = count > 0
                ? $"{count} item{(count == 1 ? "" : "s")} selected"
                : "Check items to select, then click OK";

            TxtSelectedCount.Text = $"{count} selected";
            BdrSelectedCount.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Pagination ────────────────────────────────────────────────────────
        private void ApplyPagination()
        {
            int total     = _allRows.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)_pageSize));
            if (_currentPage > totalPages) _currentPage = totalPages;

            _currentRows = _allRows
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            ItemsGrid.ItemsSource = _currentRows;
            TxtPageInfo.Text = $"Page {_currentPage} of {totalPages}  ({total} items)";
            BtnPrevPage.IsEnabled = _currentPage > 1;
            BtnNextPage.IsEnabled = _currentPage < totalPages;
            UpdateSelectionHint();
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1) { _currentPage--; ApplyPagination(); }
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = Math.Max(1, (int)Math.Ceiling(_allRows.Count / (double)_pageSize));
            if (_currentPage < totalPages) { _currentPage++; ApplyPagination(); }
        }

        private void CmbPageSize_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbPageSize.SelectedItem is System.Windows.Controls.ComboBoxItem item &&
                int.TryParse(item.Content?.ToString(), out int size))
            {
                _pageSize    = size;
                _currentPage = 1;
                ApplyPagination();
            }
        }

        // ── OK / Cancel ───────────────────────────────────────────────────────
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            // Collect from the cross-search cache so selections made under earlier
            // search terms (not just the current page/results) are included
            var selected = _selectedCache.Values.ToList();

            // Fallback: if no checkboxes ticked, use highlighted rows on current page
            if (selected.Count == 0)
            {
                selected = ItemsGrid.SelectedItems
                    .Cast<ItemRowViewModel>()
                    .ToList();
            }

            SelectedItems = selected.Select(r => new ItemDto
            {
                ItemId          = r.ItemId,
                Name            = r.Name,
                Description     = r.Description,
                Active          = true,
                Category        = r.Category,
                SerialNumber    = r.SerialNumber,
                ModelNumber     = r.ModelNumber,
                CategoryId      = r.CategoryId,
                UnitOfMeasure   = r.UnitOfMeasure,
                StockOnHand     = r.StockOnHand,
                StartDate       = r.StartDate,
                EndDate         = r.EndDate,
                Amount          = 0m,
                DateCreated     = r.DateCreated,
                CreatedByUserId = r.CreatedByUserId,
                CreatedByName   = r.CreatedByName
            }).ToList();

            DialogResult = SelectedItems.Count > 0;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // ── Row view-model ─────────────────────────────────────────────────────
        private class ItemRowViewModel : INotifyPropertyChanged
        {
            private bool _selected;

            public event PropertyChangedEventHandler PropertyChanged;
            private void Notify(string name) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

            public bool Selected
            {
                get => _selected;
                set { _selected = value; Notify(nameof(Selected)); }
            }

            public int       ItemId          { get; set; }
            public string    Name            { get; set; }
            public string    Description     { get; set; }
            public string    Category        { get; set; }
            public string    SerialNumber    { get; set; }
            public string    ModelNumber     { get; set; }
            public int       CategoryId      { get; set; }
            public string    UnitOfMeasure   { get; set; }
            public int       StockOnHand     { get; set; }
            public DateTime  DateCreated     { get; set; }
            public int       CreatedByUserId { get; set; }
            public string    CreatedByName   { get; set; }
            public DateTime? StartDate       { get; set; }
            public DateTime? EndDate         { get; set; }

            // The row instance is reused (e.g. re-listed after a later search), so guard
            // against wiring the PropertyChanged handler more than once.
            public bool HandlerWired { get; set; }
        }

        private class Win32WindowWrapper : WinForms.IWin32Window
        {
            private readonly IntPtr _handle;
            public Win32WindowWrapper(IntPtr handle) { _handle = handle; }
            public IntPtr Handle => _handle;
        }
    }
}
