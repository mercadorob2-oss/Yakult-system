using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.EmailManagement.ViewModels
{
    /// <summary>
    /// Backs the WPF Email Logs page. Loads the full log set once, then does all
    /// filtering (search, status, template, date range, per-column), summary-card
    /// counting, sorting and pagination on the client — mirroring the
    /// UserAccountManagement page pattern.
    /// </summary>
    public class EmailLogsViewModel : ViewModelBase
    {
        public const string AllTemplates = "All Templates";
        private const int PageSize = 25;

        private readonly EmailRepository _repo = new EmailRepository();

        private List<SystemEmailLogDto> _all = new List<SystemEmailLogDto>();
        private List<SystemEmailLogDto> _filtered = new List<SystemEmailLogDto>();

        private readonly Dictionary<string, HashSet<string>> _columnFilters
            = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private string _sortColumn;
        private bool _sortAscending = true;

        private int _currentPage = 1;
        private int _totalPages = 1;

        private bool _isLoading;
        private string _searchText = string.Empty;
        private string _statusFilter = "All";
        private string _templateFilter = AllTemplates;
        private DateTime? _fromDate;
        private DateTime? _toDate;

        private int _totalCount, _sentCount, _failedCount, _skippedCount;
        private string _pageInfo = "Page 1 of 1  (0 logs)";
        private bool _canGoPrev, _canGoNext;

        public EmailLogsViewModel()
        {
            SelectStatusCommand = new RelayCommand<string>(s => StatusFilter = string.IsNullOrEmpty(s) ? "All" : s);
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            ResetFiltersCommand = new RelayCommand(ResetFilters);
            FirstPageCommand = new RelayCommand(() => { if (_currentPage != 1) { _currentPage = 1; RebuildPagedRows(); } });
            PrevPageCommand = new RelayCommand(() => { if (_currentPage > 1) { _currentPage--; RebuildPagedRows(); } });
            NextPageCommand = new RelayCommand(() => { if (_currentPage < _totalPages) { _currentPage++; RebuildPagedRows(); } });
            LastPageCommand = new RelayCommand(() => { if (_currentPage != _totalPages) { _currentPage = _totalPages; RebuildPagedRows(); } });
        }

        // ── Bound collections ────────────────────────────────────────────────
        public ObservableCollection<SystemEmailLogDto> PagedRows { get; } = new ObservableCollection<SystemEmailLogDto>();
        public ObservableCollection<string> TemplateOptions { get; } = new ObservableCollection<string> { AllTemplates };

        // ── Commands ─────────────────────────────────────────────────────────
        public RelayCommand<string> SelectStatusCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ResetFiltersCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        // ── Summary-card counts ──────────────────────────────────────────────
        public int TotalCount { get => _totalCount; set => SetField(ref _totalCount, value); }
        public int SentCount { get => _sentCount; set => SetField(ref _sentCount, value); }
        public int FailedCount { get => _failedCount; set => SetField(ref _failedCount, value); }
        public int SkippedCount { get => _skippedCount; set => SetField(ref _skippedCount, value); }

        // ── Summary-card active-state (drives the highlight brush) ────────────
        public bool IsAllCardActive => string.Equals(_statusFilter, "All", StringComparison.OrdinalIgnoreCase);
        public bool IsSentCardActive => string.Equals(_statusFilter, "Sent", StringComparison.OrdinalIgnoreCase);
        public bool IsFailedCardActive => string.Equals(_statusFilter, "Failed", StringComparison.OrdinalIgnoreCase);
        public bool IsSkippedCardActive => string.Equals(_statusFilter, "Skipped", StringComparison.OrdinalIgnoreCase);

        // ── Filters ──────────────────────────────────────────────────────────
        public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) { _currentPage = 1; ApplyFilter(); } }
        }

        /// <summary>"All" / "Sent" / "Failed" / "Skipped" — shared by the summary cards and the Status combo.</summary>
        public string StatusFilter
        {
            get => _statusFilter;
            set
            {
                if (!SetField(ref _statusFilter, value)) return;
                OnPropertyChanged(nameof(IsAllCardActive));
                OnPropertyChanged(nameof(IsSentCardActive));
                OnPropertyChanged(nameof(IsFailedCardActive));
                OnPropertyChanged(nameof(IsSkippedCardActive));
                _currentPage = 1;
                ApplyFilter();
            }
        }

        public string TemplateFilter
        {
            get => _templateFilter;
            set { if (SetField(ref _templateFilter, value)) { _currentPage = 1; ApplyFilter(); } }
        }

        public DateTime? FromDate
        {
            get => _fromDate;
            set { if (SetField(ref _fromDate, value)) { _currentPage = 1; ApplyFilter(); } }
        }

        public DateTime? ToDate
        {
            get => _toDate;
            set { if (SetField(ref _toDate, value)) { _currentPage = 1; ApplyFilter(); } }
        }

        // ── Pagination ───────────────────────────────────────────────────────
        public string PageInfo { get => _pageInfo; set => SetField(ref _pageInfo, value); }
        public bool CanGoPrev { get => _canGoPrev; set => SetField(ref _canGoPrev, value); }
        public bool CanGoNext { get => _canGoNext; set => SetField(ref _canGoNext, value); }

        // ── Load ─────────────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                _columnFilters.Clear();
                _sortColumn = null;
                _sortAscending = true;

                _all = await _repo.GetAllEmailLogsAsync() ?? new List<SystemEmailLogDto>();

                var templates = _all.Select(l => l.TemplateKey)
                                    .Where(t => !string.IsNullOrWhiteSpace(t))
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                                    .ToList();
                TemplateOptions.Clear();
                TemplateOptions.Add(AllTemplates);
                foreach (var t in templates) TemplateOptions.Add(t);
                if (!TemplateOptions.Contains(_templateFilter)) TemplateFilter = AllTemplates;

                _currentPage = 1;
                ApplyFilter();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ResetFilters()
        {
            _searchText = string.Empty; OnPropertyChanged(nameof(SearchText));
            _templateFilter = AllTemplates; OnPropertyChanged(nameof(TemplateFilter));
            _fromDate = null; OnPropertyChanged(nameof(FromDate));
            _toDate = null; OnPropertyChanged(nameof(ToDate));
            _columnFilters.Clear();
            _sortColumn = null;
            _sortAscending = true;
            _currentPage = 1;
            StatusFilter = "All"; // triggers ApplyFilter
            ApplyFilter();
        }

        // ── Filtering ────────────────────────────────────────────────────────
        public void ApplyFilter()
        {
            var search = (_searchText ?? string.Empty).Trim();
            bool hasSearch = search.Length > 0;

            // "Base" set = everything except the status filter. The summary cards
            // count over this so they show how many of each status match the
            // other active filters.
            var baseSet = _all.Where(log =>
            {
                if (hasSearch)
                {
                    bool m =
                        Contains(log.TemplateKey, search) ||
                        Contains(log.Recipients, search) ||
                        Contains(log.Subject, search) ||
                        Contains(log.Status, search) ||
                        Contains(log.SentByUserName, search);
                    if (!m) return false;
                }

                if (!string.Equals(_templateFilter, AllTemplates, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(log.TemplateKey ?? string.Empty, _templateFilter, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (_fromDate.HasValue && log.SentDate.Date < _fromDate.Value.Date) return false;
                if (_toDate.HasValue && log.SentDate.Date > _toDate.Value.Date) return false;

                return true;
            }).ToList();

            TotalCount = baseSet.Count;
            SentCount = baseSet.Count(l => IsStatus(l, "Sent"));
            FailedCount = baseSet.Count(l => IsStatus(l, "Failed"));
            SkippedCount = baseSet.Count(l => IsStatus(l, "Skipped"));

            IEnumerable<SystemEmailLogDto> q = baseSet;
            if (!string.Equals(_statusFilter, "All", StringComparison.OrdinalIgnoreCase))
                q = q.Where(l => IsStatus(l, _statusFilter));

            foreach (var kv in _columnFilters)
            {
                string col = kv.Key;
                var allowed = kv.Value;
                q = q.Where(l => allowed.Contains(GetColumnValue(l, col) ?? string.Empty));
            }

            if (!string.IsNullOrEmpty(_sortColumn))
            {
                q = _sortAscending
                    ? q.OrderBy(l => SortKey(l, _sortColumn), Comparer<object>.Default)
                    : q.OrderByDescending(l => SortKey(l, _sortColumn), Comparer<object>.Default);
            }

            _filtered = q.ToList();
            RebuildPagedRows();
        }

        private void RebuildPagedRows()
        {
            _totalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1) _currentPage = 1;

            PagedRows.Clear();
            foreach (var r in _filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(r);

            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
            PageInfo = $"Page {_currentPage} of {_totalPages}  ({_filtered.Count} log{(_filtered.Count == 1 ? "" : "s")})";
        }

        // ── Per-column filter / sort support (header buttons) ─────────────────
        public void SetSort(string column, bool ascending)
        {
            _sortColumn = column;
            _sortAscending = ascending;
            _currentPage = 1;
            ApplyFilter();
        }

        public HashSet<string> GetColumnFilter(string column)
            => _columnFilters.TryGetValue(column, out var f) ? f : null;

        public void SetColumnFilter(string column, HashSet<string> values)
        {
            if (values == null || values.Count == 0) _columnFilters.Remove(column);
            else _columnFilters[column] = values;
            _currentPage = 1;
            ApplyFilter();
        }

        public void ClearColumnFilter(string column)
        {
            _columnFilters.Remove(column);
            _currentPage = 1;
            ApplyFilter();
        }

        public List<string> GetUniqueValuesForColumn(string column)
        {
            return _all.Select(l => GetColumnValue(l, column))
                       .Where(v => !string.IsNullOrEmpty(v))
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                       .ToList();
        }

        private static string GetColumnValue(SystemEmailLogDto l, string column)
        {
            switch (column)
            {
                case "ID": return l.LogId.ToString();
                case "Date": return l.SentDate.ToString("yyyy-MM-dd HH:mm:ss");
                case "Template": return l.TemplateKey ?? string.Empty;
                case "Recipients": return l.Recipients ?? string.Empty;
                case "Subject": return l.Subject ?? string.Empty;
                case "Status": return l.Status ?? string.Empty;
                case "Sent By": return l.SentByUserName ?? string.Empty;
                default: return null;
            }
        }

        private static object SortKey(SystemEmailLogDto l, string column)
        {
            switch (column)
            {
                case "ID": return l.LogId;
                case "Date": return l.SentDate;
                default: return GetColumnValue(l, column) ?? string.Empty;
            }
        }

        private static bool IsStatus(SystemEmailLogDto l, string status)
            => string.Equals(l.Status, status, StringComparison.OrdinalIgnoreCase);

        private static bool Contains(string haystack, string needle)
            => haystack != null && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
