using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class PartiallyFulfilledViewModel : ViewModelBase, IDisposable
    {
        private const int PageSize = 10;

        private readonly UnfulfilledCartridgeExchangeRepository _repository;
        private readonly CartridgeManagementRepository          _cartridgeRepo;

        private List<PfSessionGroupViewModel> _allSessions      = new List<PfSessionGroupViewModel>();
        private List<PfSessionGroupViewModel> _filteredSessions = new List<PfSessionGroupViewModel>();

        private string _searchText  = string.Empty;
        private string _filterBy    = "All Fields";
        private string _sortBy      = "Set # (A→Z)";
        private bool   _isLoading;
        private int    _currentPage = 1;
        private int    _totalPages  = 1;
        private string _pageInfoText = string.Empty;
        private int    _sessionCount;
        private int    _totalIssued;
        private int    _totalPending;
        private PfExchangeRowViewModel _selectedRow;
        private string _remarksText = "Click a row to view remarks";

        // ── Observable collection bound to the ItemsControl ──────────────────────
        public ObservableCollection<PfSessionGroupViewModel> PagedSessions { get; }
            = new ObservableCollection<PfSessionGroupViewModel>();

        // ── Properties ───────────────────────────────────────────────────────────
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilter(); }
        }

        public string FilterBy
        {
            get => _filterBy;
            set { if (SetField(ref _filterBy, value)) ApplyFilter(); }
        }

        public string SortBy
        {
            get => _sortBy;
            set { if (SetField(ref _sortBy, value)) ApplyFilter(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetField(ref _isLoading, value);
        }

        public int CurrentPage
        {
            get => _currentPage;
            set => SetField(ref _currentPage, value);
        }

        public int TotalPages
        {
            get => _totalPages;
            set => SetField(ref _totalPages, value);
        }

        public string PageInfoText
        {
            get => _pageInfoText;
            set => SetField(ref _pageInfoText, value);
        }

        public int SessionCount
        {
            get => _sessionCount;
            set => SetField(ref _sessionCount, value);
        }

        public int TotalIssued
        {
            get => _totalIssued;
            set => SetField(ref _totalIssued, value);
        }

        public int TotalPending
        {
            get => _totalPending;
            set => SetField(ref _totalPending, value);
        }

        public PfExchangeRowViewModel SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (SetField(ref _selectedRow, value))
                {
                    OnPropertyChanged(nameof(CanFulfill));
                    UpdateRemarks();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string RemarksText
        {
            get => _remarksText;
            set => SetField(ref _remarksText, value);
        }

        public bool CanFulfill => _selectedRow?.IsPending == true;

        public bool CanGoFirst => _currentPage > 1;
        public bool CanGoPrev  => _currentPage > 1;
        public bool CanGoNext  => _currentPage < _totalPages;
        public bool CanGoLast  => _currentPage < _totalPages;

        // ── Commands ─────────────────────────────────────────────────────────────
        public ICommand RefreshCommand        { get; }
        public ICommand FulfillSelectedCommand { get; }
        public ICommand FirstPageCommand      { get; }
        public ICommand PrevPageCommand       { get; }
        public ICommand NextPageCommand       { get; }
        public ICommand LastPageCommand       { get; }

        // ── Event raised so code-behind can show the WPF dialog ──────────────────
        public event Action<List<PfExchangeRowViewModel>> FulfillRequested;

        // ── Constructor ──────────────────────────────────────────────────────────
        public PartiallyFulfilledViewModel()
        {
            _repository    = new UnfulfilledCartridgeExchangeRepository();
            _cartridgeRepo = new CartridgeManagementRepository();

            RefreshCommand         = new RelayCommand(LoadData);
            FulfillSelectedCommand = new RelayCommand(OnFulfillSelected, () => CanFulfill);
            FirstPageCommand       = new RelayCommand(() => NavigateTo(1),             () => CanGoFirst);
            PrevPageCommand        = new RelayCommand(() => NavigateTo(_currentPage - 1), () => CanGoPrev);
            NextPageCommand        = new RelayCommand(() => NavigateTo(_currentPage + 1), () => CanGoNext);
            LastPageCommand        = new RelayCommand(() => NavigateTo(_totalPages),   () => CanGoLast);
        }

        // ── Data loading ─────────────────────────────────────────────────────────
        public void LoadData()
        {
            try
            {
                IsLoading = true;
                SelectedRow = null;

                var rows = _repository.GetPartiallyFulfilledExchangesFullSet()
                           ?? new List<UnfulfilledCartridgeExchangeDto>();

                _allSessions = BuildSessionViewModels(GroupIntoSessions(rows));
                UpdateStats();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading data:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Grouping (mirrors WinForms logic exactly) ─────────────────────────────
        private static List<SessionData> GroupIntoSessions(List<UnfulfilledCartridgeExchangeDto> rows)
        {
            var result     = new List<SessionData>();
            var setMap     = new Dictionary<int, SessionData>();
            var sessionMap = new Dictionary<Guid, SessionData>();

            foreach (var row in rows)
            {
                if (row.SetId.HasValue && row.SetId.Value > 0)
                {
                    if (!setMap.TryGetValue(row.SetId.Value, out var grp))
                    {
                        grp = new SessionData { SetId = row.SetId, SessionId = row.SubmissionSessionId };
                        setMap[row.SetId.Value] = grp;
                        result.Add(grp);
                    }
                    grp.Rows.Add(row);
                }
                else if (row.SubmissionSessionId.HasValue)
                {
                    if (!sessionMap.TryGetValue(row.SubmissionSessionId.Value, out var grp))
                    {
                        grp = new SessionData { SessionId = row.SubmissionSessionId };
                        sessionMap[row.SubmissionSessionId.Value] = grp;
                        result.Add(grp);
                    }
                    grp.Rows.Add(row);
                }
                else
                {
                    var grp = new SessionData();
                    grp.Rows.Add(row);
                    result.Add(grp);
                }
            }

            return result;
        }

        private static List<PfSessionGroupViewModel> BuildSessionViewModels(List<SessionData> sessions)
        {
            return sessions.Select(s =>
            {
                var first = s.Rows.FirstOrDefault();
                return new PfSessionGroupViewModel(
                    s.SetId,
                    first?.RequesterName,
                    first?.BranchName,
                    first?.DepartmentName,
                    s.Rows);
            }).ToList();
        }

        // ── Stats ────────────────────────────────────────────────────────────────
        private void UpdateStats()
        {
            SessionCount = _allSessions.Count;
            TotalIssued  = _allSessions.Sum(s => s.TotalIssued);
            TotalPending = _allSessions.Sum(s => s.TotalPending);
        }

        // ── Filtering & sorting ───────────────────────────────────────────────────
        private void ApplyFilter()
        {
            string search = _searchText?.Trim() ?? string.Empty;

            _filteredSessions = string.IsNullOrWhiteSpace(search)
                ? _allSessions.ToList()
                : _allSessions.Where(s => SessionMatchesSearch(s, search, _filterBy)).ToList();

            _filteredSessions = _sortBy == "Set # (Z→A)"
                ? _filteredSessions.OrderByDescending(s => s.SetId ?? int.MaxValue).ToList()
                : _filteredSessions.OrderBy(s => s.SetId ?? int.MaxValue).ToList();

            _currentPage = 1;
            RebuildPage();
        }

        private static bool SessionMatchesSearch(PfSessionGroupViewModel s, string search, string filterBy)
            => s.Rows.Any(r => RowMatchesSearch(r, search, filterBy));

        private static bool RowMatchesSearch(PfExchangeRowViewModel r, string search, string filterBy)
        {
            bool Has(string v) => !string.IsNullOrEmpty(v)
                               && v.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            switch (filterBy)
            {
                case "Requester":  return Has(r.Dto.RequesterName);
                case "Branch":     return Has(r.Dto.BranchName);
                case "Department": return Has(r.Dto.DepartmentName);
                case "Model":      return Has(r.CartridgeModel);
                case "Req #":      return Has(r.ReqId.ToString());
                default:
                    return Has(r.Dto.RequesterName) || Has(r.Dto.BranchName)
                        || Has(r.Dto.DepartmentName) || Has(r.CartridgeModel)
                        || Has(r.ReqId.ToString());
            }
        }

        // ── Pagination ────────────────────────────────────────────────────────────
        private void NavigateTo(int page)
        {
            _currentPage = page;
            RebuildPage();
        }

        private void RebuildPage()
        {
            int total = _filteredSessions.Count;
            int pages = Math.Max(1, (int)Math.Ceiling((double)total / PageSize));
            _currentPage = Math.Max(1, Math.Min(_currentPage, pages));

            TotalPages   = pages;
            CurrentPage  = _currentPage;
            PageInfoText = total == 0
                ? "No results"
                : $"Page {_currentPage} of {pages}  ({total} sets)";

            PagedSessions.Clear();
            foreach (var s in _filteredSessions.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedSessions.Add(s);

            OnPropertyChanged(nameof(CanGoFirst));
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanGoLast));
        }

        // ── Fulfill ───────────────────────────────────────────────────────────────
        private void OnFulfillSelected()
        {
            if (_selectedRow == null || !_selectedRow.IsPending) return;

            // Find session containing the selected row
            var session = _filteredSessions
                .FirstOrDefault(s => s.Rows.Any(r => r == _selectedRow));

            var pendingRows = session?.Rows.Where(r => r.IsPending).ToList()
                           ?? new List<PfExchangeRowViewModel> { _selectedRow };

            FulfillRequested?.Invoke(pendingRows);
        }

        public List<FulfillRowStateViewModel> BuildFulfillStates(List<PfExchangeRowViewModel> pendingRows)
        {
            var states = new List<FulfillRowStateViewModel>();
            foreach (var row in pendingRows)
            {
                int brandNew = 0, refilled = 0;
                try
                {
                    int? modelId = _cartridgeRepo.GetCartridgeModelIdByModelNumber(row.CartridgeModel);
                    brandNew = _cartridgeRepo.GetAvailableIssuableStockByCondition(modelId, "Brand New");
                    refilled = _cartridgeRepo.GetAvailableIssuableStockByCondition(modelId, "Refilled");
                }
                catch { }
                states.Add(new FulfillRowStateViewModel(row.Dto, brandNew, refilled));
            }
            return states;
        }

        public void CommitFulfillment(List<FulfillRowStateViewModel> states)
        {
            int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
            foreach (var st in states)
            {
                if (st.TotalToIssue <= 0) continue;
                string remarks = string.IsNullOrWhiteSpace(st.Remarks)
                    ? CartridgeExchangeRemarks.GenerateRemarks(
                          st.IssuedAfter, st.Dto.ReturnedEmptyQty, st.Dto.CartridgeModel)
                    : st.Remarks.Trim();
                _repository.FulfillExchange(st.Dto.UnfulfilledId, st.TotalToIssue, userId, remarks);
            }
        }

        // ── Remarks ───────────────────────────────────────────────────────────────
        private void UpdateRemarks()
        {
            if (_selectedRow == null)
            {
                RemarksText = "Click a row to view remarks";
                return;
            }

            string text = string.Empty;
            if (!string.IsNullOrWhiteSpace(_selectedRow.Remarks))
                text = $"Remarks: {_selectedRow.Remarks}";
            if (_selectedRow.Status == "Fulfilled" && !string.IsNullOrWhiteSpace(_selectedRow.FulfilledRemarks))
            {
                if (!string.IsNullOrWhiteSpace(text)) text += "   |   ";
                text += $"Fulfilled Remarks: {_selectedRow.FulfilledRemarks}";
            }

            RemarksText = string.IsNullOrWhiteSpace(text) ? "No remarks" : text;
        }

        public void Dispose() { }

        // ── Private helper ────────────────────────────────────────────────────────
        private sealed class SessionData
        {
            public int?  SetId     { get; set; }
            public Guid? SessionId { get; set; }
            public List<UnfulfilledCartridgeExchangeDto> Rows { get; }
                = new List<UnfulfilledCartridgeExchangeDto>();
        }
    }
}
