using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;

using WinForms  = System.Windows.Forms;
using WinMsgBox = System.Windows.MessageBox;

namespace Yakult.Inventory.App.Pages.Set
{
    public partial class SelectRequestForSetDialog : Window, IDisposable
    {
        private readonly int           _setId;
        private readonly SetRepository _repository;

        private List<RequestDto> _allRequests      = new List<RequestDto>();
        private List<RequestDto> _filteredRequests = new List<RequestDto>();

        // ── Sorting ───────────────────────────────────────────────────────────────────────
        private int  _sortColumnIndex = -1;
        private bool _sortAscending   = true;

        private static readonly string[] _colHeaders =
            { "Req ID", "Employee", "Item", "Qty", "Date", "Date Requested", "Status", "Description" };

        // ── Pagination ────────────────────────────────────────────────────────────────────
        private int        _currentPage = 1;
        private const int  _pageSize    = 15;

        // ── IDisposable (no-op, required for WinForms `using` callers) ───────────────────
        public void Dispose() { }

        // ── WinForms ShowDialog compatibility ─────────────────────────────────────────────
        public new WinForms.DialogResult ShowDialog()
        {
            bool? r = base.ShowDialog();
            return r == true ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }

        public WinForms.DialogResult ShowDialog(WinForms.IWin32Window owner)
        {
            if (owner != null)
                new System.Windows.Interop.WindowInteropHelper(this).Owner = owner.Handle;
            return ShowDialog();
        }

        public SelectRequestForSetDialog(int setId, SetRepository repository)
        {
            _setId      = setId;
            _repository = repository;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            DtpFrom.SelectedDate = DateTime.Today.AddMonths(-1);
            DtpTo.SelectedDate   = DateTime.Today;
            UpdateColumnHeaders();
            await LoadUnassignedRequestsAsync();
        }

        // ── Window chrome ─────────────────────────────────────────────────────────────────
        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled    = true;
            DialogResult = false;
        }

        // ── Filter events ─────────────────────────────────────────────────────────────────
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _currentPage = 1;
            ApplyFilters();
        }

        private void DateFilter_Changed(object sender, RoutedEventArgs e)
        {
            bool enabled = ChkDateFilter.IsChecked == true;
            DtpFrom.IsEnabled = enabled;
            DtpTo.IsEnabled   = enabled;
            _currentPage = 1;
            ApplyFilters();
        }

        // ── Data loading ──────────────────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task LoadUnassignedRequestsAsync()
        {
            try
            {
                _allRequests = await _repository.GetUnassignedRequestsAsync();
                ApplyFilters();

                if (_allRequests.Count == 0)
                    BtnAdd.IsEnabled = false;
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load unassigned requests:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Filtering + sorting + pagination ──────────────────────────────────────────────
        private void ApplyFilters()
        {
            if (_allRequests == null) return;

            string   search   = TxtSearch?.Text?.Trim().ToLowerInvariant() ?? "";
            bool     useDate  = ChkDateFilter?.IsChecked == true;
            DateTime fromDate = DtpFrom?.SelectedDate ?? DateTime.MinValue;
            DateTime toDate   = DtpTo?.SelectedDate   ?? DateTime.MaxValue;

            _filteredRequests = _allRequests.Where(r =>
            {
                if (useDate && (r.DateCreated.Date < fromDate.Date || r.DateCreated.Date > toDate.Date))
                    return false;
                if (!string.IsNullOrEmpty(search))
                {
                    bool matches =
                        (r.EmployeeName ?? "").ToLowerInvariant().Contains(search) ||
                        (r.CompanyName ?? "").ToLowerInvariant().Contains(search) ||
                        (r.DepartmentName ?? "").ToLowerInvariant().Contains(search) ||
                        (r.BranchName ?? "").ToLowerInvariant().Contains(search) ||
                        (r.DistributorName ?? "").ToLowerInvariant().Contains(search) ||
                        (r.ItemName     ?? "").ToLowerInvariant().Contains(search) ||
                        (r.Description  ?? "").ToLowerInvariant().Contains(search) ||
                        (r.Status       ?? "").ToLowerInvariant().Contains(search) ||
                        r.ReqId.ToString().Contains(search) ||
                        r.Quantity.ToString().Contains(search) ||
                        r.DateCreated.ToString("MM/dd/yyyy").Contains(search) ||
                        (r.DateRequested?.ToString("MM/dd/yyyy") ?? "").Contains(search);
                    if (!matches) return false;
                }
                return true;
            }).ToList();

            ApplySort();
            RefreshGrid();
        }

        private void ApplySort()
        {
            if (_sortColumnIndex < 0) return;

            switch (_sortColumnIndex)
            {
                case 0:
                    _filteredRequests = _sortAscending
                        ? _filteredRequests.OrderBy(r => r.ReqId).ToList()
                        : _filteredRequests.OrderByDescending(r => r.ReqId).ToList();
                    break;
                case 1:
                    _filteredRequests = _sortAscending
                        ? _filteredRequests.OrderBy(r => r.EmployeeName ?? "", StringComparer.OrdinalIgnoreCase).ToList()
                        : _filteredRequests.OrderByDescending(r => r.EmployeeName ?? "", StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                case 2:
                    _filteredRequests = _sortAscending
                        ? _filteredRequests.OrderBy(r => r.ItemName ?? "", StringComparer.OrdinalIgnoreCase).ToList()
                        : _filteredRequests.OrderByDescending(r => r.ItemName ?? "", StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                case 3:
                    _filteredRequests = _sortAscending
                        ? _filteredRequests.OrderBy(r => r.Quantity).ToList()
                        : _filteredRequests.OrderByDescending(r => r.Quantity).ToList();
                    break;
                case 4:
                    _filteredRequests = _sortAscending
                        ? _filteredRequests.OrderBy(r => r.DateCreated).ToList()
                        : _filteredRequests.OrderByDescending(r => r.DateCreated).ToList();
                    break;
                case 5:
                    _filteredRequests = _sortAscending
                        ? _filteredRequests.OrderBy(r => r.DateRequested).ToList()
                        : _filteredRequests.OrderByDescending(r => r.DateRequested).ToList();
                    break;
                case 6:
                    _filteredRequests = _sortAscending
                        ? _filteredRequests.OrderBy(r => r.Status ?? "", StringComparer.OrdinalIgnoreCase).ToList()
                        : _filteredRequests.OrderByDescending(r => r.Status ?? "", StringComparer.OrdinalIgnoreCase).ToList();
                    break;
                case 7:
                    _filteredRequests = _sortAscending
                        ? _filteredRequests.OrderBy(r => r.Description ?? "", StringComparer.OrdinalIgnoreCase).ToList()
                        : _filteredRequests.OrderByDescending(r => r.Description ?? "", StringComparer.OrdinalIgnoreCase).ToList();
                    break;
            }
        }

        private void UpdateColumnHeaders()
        {
            if (DgvRequests?.Columns == null) return;
            for (int i = 0; i < DgvRequests.Columns.Count && i < _colHeaders.Length; i++)
            {
                string glyph = (i == _sortColumnIndex)
                    ? (_sortAscending ? " ▲" : " ▼")
                    : " ⇅";
                DgvRequests.Columns[i].Header = _colHeaders[i] + glyph;
            }
        }

        private void RefreshGrid()
        {
            int total      = _filteredRequests.Count;
            int totalPages = (int)Math.Ceiling(total / (double)_pageSize);
            if (totalPages == 0) totalPages = 1;
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1)          _currentPage = 1;

            var page = _filteredRequests
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            DgvRequests.ItemsSource = null;
            DgvRequests.ItemsSource = page;

            // Status label
            if (_allRequests.Count == 0)
                LblTitle.Text = "No unassigned requests available. All requests are either in sets or need to be created first.";
            else if (total == 0)
                LblTitle.Text = "No requests match the current filter.";
            else
                LblTitle.Text = $"Select a request to add to this set:";

            // Page info
            LblPageInfo.Text = total == 0
                ? "No results"
                : $"Page {_currentPage} of {totalPages}  ({total} request{(total == 1 ? "" : "s")})";

            BtnFirstPage.IsEnabled = _currentPage > 1;
            BtnPrevPage.IsEnabled  = _currentPage > 1;
            BtnNextPage.IsEnabled  = _currentPage < totalPages;
            BtnLastPage.IsEnabled  = _currentPage < totalPages;
        }

        // ── Sorting event ─────────────────────────────────────────────────────────────────
        private void DgvRequests_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true; // take over; our list isn't an ICollectionView

            int colIndex = DgvRequests.Columns.IndexOf(e.Column);
            if (_sortColumnIndex == colIndex)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumnIndex = colIndex;
                _sortAscending   = true;
            }

            UpdateColumnHeaders();
            _currentPage = 1;
            ApplySort();
            RefreshGrid();
        }

        // ── Pagination buttons ────────────────────────────────────────────────────────────
        private void BtnFirstPage_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = 1;
            RefreshGrid();
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1) { _currentPage--; RefreshGrid(); }
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = (int)Math.Ceiling(_filteredRequests.Count / (double)_pageSize);
            if (_currentPage < totalPages) { _currentPage++; RefreshGrid(); }
        }

        private void BtnLastPage_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = (int)Math.Ceiling(_filteredRequests.Count / (double)_pageSize);
            if (totalPages < 1) totalPages = 1;
            _currentPage = totalPages;
            RefreshGrid();
        }

        // ── Button / grid events ──────────────────────────────────────────────────────────
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            _ = AddSelectedRequestAsync();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void DgvRequests_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            _ = AddSelectedRequestAsync();
        }

        private async System.Threading.Tasks.Task AddSelectedRequestAsync()
        {
            var selected = DgvRequests.SelectedItem as RequestDto;
            if (selected == null)
            {
                WinMsgBox.Show("Please select a request to add.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _repository.AddRequestToSetAsync(selected.ReqId, _setId);
                WinMsgBox.Show("Request added to set successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to add request to set:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
