using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.ItemMovementAudit.ViewModels
{
    public sealed partial class ItemMovementAuditPageViewModel
    {
        public ObservableCollection<ItemMovementAuditDto> PagedMovements { get; }

        private int _currentPage = 1;
        public int CurrentPage { get => _currentPage; private set => SetField(ref _currentPage, value); }

        private string _pageInfoText = string.Empty;
        public string PageInfoText { get => _pageInfoText; private set => SetField(ref _pageInfoText, value); }

        private bool _canFirstPage, _canPrevPage, _canNextPage, _canLastPage;
        public bool CanFirstPage { get => _canFirstPage; private set => SetField(ref _canFirstPage, value); }
        public bool CanPrevPage { get => _canPrevPage; private set => SetField(ref _canPrevPage, value); }
        public bool CanNextPage { get => _canNextPage; private set => SetField(ref _canNextPage, value); }
        public bool CanLastPage { get => _canLastPage; private set => SetField(ref _canLastPage, value); }

        private int GetTotalPages()
        {
            var count = _filteredMovements?.Count ?? 0;
            return count == 0 ? 0 : (int)Math.Ceiling((double)count / PageSize);
        }

        private void UpdatePagination()
        {
            if (_filteredMovements == null || _filteredMovements.Count == 0)
            {
                PagedMovements.Clear();

                var rangeHint = DateTo.Date < DateTime.Today ? " Date range ends before today." : string.Empty;
                PageInfoText = $"Page 0 of 0 (0 movements) — No data for {DateFrom:yyyy-MM-dd} to {DateTo:yyyy-MM-dd}.{rangeHint} Try widening the date range." + GetTopRowsWarningSuffix();
                CanFirstPage = CanPrevPage = CanNextPage = CanLastPage = false;
                return;
            }

            int totalPages = GetTotalPages();
            if (CurrentPage > totalPages) CurrentPage = totalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var pagedData = _filteredMovements.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            PagedMovements.Clear();
            foreach (var m in pagedData) PagedMovements.Add(m);

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({_filteredMovements.Count} movements)" + GetTopRowsWarningSuffix();
            CanFirstPage = CurrentPage > 1;
            CanPrevPage = CurrentPage > 1;
            CanNextPage = CurrentPage < totalPages;
            CanLastPage = CurrentPage < totalPages;
        }

        private string GetCurrentSortKey() => _sortColumnKey;

        private void ApplyCurrentSortIfAny()
        {
            var key = _sortColumnKey;
            var dir = _sortDirection;

            if (string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(_restoreSortKey))
                key = _restoreSortKey;
            if (dir == null && _restoreSortDirection != null)
                dir = _restoreSortDirection;

            if (string.IsNullOrWhiteSpace(key) || dir == null)
                return;

            SortDataSource(key, dir.Value);
            _sortDirection = dir;
            _sortColumnKey = key;
        }

        private void ApplySortGlyphAndDropdown()
        {
            RebuildSortByOptionsRequested?.Invoke();
        }

        public ObservableCollection<ListSortOption> SortByOptions { get; }

        private ListSortOption _selectedSortBy;
        public ListSortOption SelectedSortBy
        {
            get => _selectedSortBy;
            set
            {
                _selectedSortBy = value;
                OnPropertyChanged();
                if (_suppressSortByChange || value == null) return;

                _sortDirection = value.Direction;
                SortDataSource(value.ColumnKey, value.Direction);
                _sortColumnKey = value.ColumnKey;
                UpdatePagination();
                ScheduleSaveState();
            }
        }

        public ListSortDirection? CurrentSortDirection => _sortDirection;

        public void SetSortByOptionsOnce(List<ListSortOption> options, string defaultColumnKey)
        {
            _suppressSortByChange = true;
            try
            {
                if (SortByOptions.Count == 0)
                {
                    foreach (var o in options) SortByOptions.Add(o);
                }

                // Look up the match inside SortByOptions (the ComboBox's actual ItemsSource), not
                // the freshly-built "options" list — ListSortOption has no value equality, so
                // assigning SelectedSortBy an instance that isn't physically a member of
                // SortByOptions leaves the ComboBox unable to find/highlight it (only worked on
                // the very first call, when SortByOptions was empty and got populated with these
                // exact same instances).
                ListSortOption selected = null;
                if (_sortColumnKey != null)
                    selected = SortByOptions.FirstOrDefault(o => o.ColumnKey == _sortColumnKey && o.Direction == (_sortDirection ?? ListSortDirection.Ascending));
                if (selected == null && !string.IsNullOrEmpty(defaultColumnKey))
                    selected = SortByOptions.FirstOrDefault(o => o.ColumnKey == defaultColumnKey && o.Direction == ListSortDirection.Ascending);

                if (selected != null) SelectedSortBy = selected;
            }
            finally
            {
                _suppressSortByChange = false;
            }
        }

        /// <summary>Called by the View's grid Sorting handler (column header click).</summary>
        public void SortByColumn(string columnKey, ListSortDirection direction)
        {
            if (string.IsNullOrEmpty(columnKey)) return;

            SortDataSource(columnKey, direction);
            _sortColumnKey = columnKey;
            _sortDirection = direction;
            UpdatePagination();

            _suppressSortByChange = true;
            try
            {
                var match = SortByOptions.FirstOrDefault(o => o.ColumnKey == columnKey && o.Direction == direction);
                if (match != null) SelectedSortBy = match;
            }
            finally
            {
                _suppressSortByChange = false;
            }

            ScheduleSaveState();
        }

        /// <summary>Mirrors CmbFilterBy_SelectedIndexChanged: Filter-By sorts by EventTime.</summary>
        private string _selectedFilterBy = "Default";
        public ObservableCollection<string> FilterByOptions { get; } = new ObservableCollection<string> { "Default", "Most Recently Added", "Oldest Added" };
        public string SelectedFilterBy
        {
            get => _selectedFilterBy;
            set
            {
                if (SetField(ref _selectedFilterBy, value))
                {
                    const string dateColumn = "EventTime";
                    ListSortDirection? dir = null;
                    if (value == "Most Recently Added") dir = ListSortDirection.Descending;
                    else if (value == "Oldest Added") dir = ListSortDirection.Ascending;

                    if (dir.HasValue)
                    {
                        SortDataSource(dateColumn, dir.Value);
                        _sortColumnKey = dateColumn;
                        _sortDirection = dir.Value;
                    }

                    UpdatePagination();

                    if (dir.HasValue)
                    {
                        _suppressSortByChange = true;
                        try
                        {
                            var match = SortByOptions.FirstOrDefault(o => o.ColumnKey == dateColumn && o.Direction == dir.Value);
                            if (match != null) SelectedSortBy = match;
                        }
                        finally
                        {
                            _suppressSortByChange = false;
                        }
                        ScheduleSaveState();
                    }
                }
            }
        }

        private void SortDataSource(string propertyName, ListSortDirection direction)
        {
            if (_filteredMovements == null || string.IsNullOrWhiteSpace(propertyName))
                return;

            try
            {
                var key = propertyName.Trim();
                IEnumerable<ItemMovementAuditDto> sorted;

                switch (key)
                {
                    case "EventTime":
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.EventTime)
                            : _filteredMovements.OrderByDescending(x => x.EventTime);
                        break;
                    case "Quantity":
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.Quantity)
                            : _filteredMovements.OrderByDescending(x => x.Quantity);
                        break;
                    case "ItemId":
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.ItemId ?? int.MaxValue)
                            : _filteredMovements.OrderByDescending(x => x.ItemId ?? int.MinValue);
                        break;
                    case "ReferenceId":
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.ReferenceId ?? int.MaxValue)
                            : _filteredMovements.OrderByDescending(x => x.ReferenceId ?? int.MinValue);
                        break;
                    case "Direction":
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.Direction, StringComparer.OrdinalIgnoreCase)
                            : _filteredMovements.OrderByDescending(x => x.Direction, StringComparer.OrdinalIgnoreCase);
                        break;
                    case "MovementType":
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.MovementType, StringComparer.OrdinalIgnoreCase)
                            : _filteredMovements.OrderByDescending(x => x.MovementType, StringComparer.OrdinalIgnoreCase);
                        break;
                    case "MovementCategory":
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.MovementCategory, StringComparer.OrdinalIgnoreCase)
                            : _filteredMovements.OrderByDescending(x => x.MovementCategory, StringComparer.OrdinalIgnoreCase);
                        break;
                    case "SerialNumber":
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.SerialNumber, StringComparer.OrdinalIgnoreCase)
                            : _filteredMovements.OrderByDescending(x => x.SerialNumber, StringComparer.OrdinalIgnoreCase);
                        break;
                    case "SetCode":
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.SetCode, StringComparer.OrdinalIgnoreCase)
                            : _filteredMovements.OrderByDescending(x => x.SetCode, StringComparer.OrdinalIgnoreCase);
                        break;
                    case "Source":
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.Source, StringComparer.OrdinalIgnoreCase)
                            : _filteredMovements.OrderByDescending(x => x.Source, StringComparer.OrdinalIgnoreCase);
                        break;
                    case "UserName":
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.UserName, StringComparer.OrdinalIgnoreCase)
                            : _filteredMovements.OrderByDescending(x => x.UserName, StringComparer.OrdinalIgnoreCase);
                        break;
                    case "EmployeeName":
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.EmployeeName, StringComparer.OrdinalIgnoreCase)
                            : _filteredMovements.OrderByDescending(x => x.EmployeeName, StringComparer.OrdinalIgnoreCase);
                        break;
                    case "BranchName":
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.BranchName, StringComparer.OrdinalIgnoreCase)
                            : _filteredMovements.OrderByDescending(x => x.BranchName, StringComparer.OrdinalIgnoreCase);
                        break;
                    case "DepartmentName":
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.DepartmentName, StringComparer.OrdinalIgnoreCase)
                            : _filteredMovements.OrderByDescending(x => x.DepartmentName, StringComparer.OrdinalIgnoreCase);
                        break;
                    case "ReferenceType":
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.ReferenceType, StringComparer.OrdinalIgnoreCase)
                            : _filteredMovements.OrderByDescending(x => x.ReferenceType, StringComparer.OrdinalIgnoreCase);
                        break;
                    default:
                        sorted = direction == ListSortDirection.Ascending
                            ? _filteredMovements.OrderBy(x => x.GetType().GetProperty(propertyName)?.GetValue(x, null))
                            : _filteredMovements.OrderByDescending(x => x.GetType().GetProperty(propertyName)?.GetValue(x, null));
                        break;
                }

                _filteredMovements = sorted.ToList();
            }
            catch
            {
                // Ignore sorting errors (matches original SortDataSource's silent catch).
            }
        }
    }
}
