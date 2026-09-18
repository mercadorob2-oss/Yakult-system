using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Renewal.LinkRenewal.ViewModels
{
    public class LinkRenewalViewModel : ViewModelBase
    {
        private readonly RenewalRepository        _repo = new RenewalRepository();
        private readonly List<InvoiceSetPickerDto> _allCandidateDtos;

        private List<CandidateSetViewModel> _filteredSortedList = new List<CandidateSetViewModel>();

        private const int PageSize = 15;

        // ── Expiring set (left panel) ─────────────────────────────────────────

        public RenewalDto ExpiringSet { get; }

        public ObservableCollection<SetItemSummaryDto> ExpiringItems { get; }
            = new ObservableCollection<SetItemSummaryDto>();

        // ── Candidate sets ────────────────────────────────────────────────────

        public ObservableCollection<CandidateSetViewModel> AllCandidates { get; }
            = new ObservableCollection<CandidateSetViewModel>();

        public ObservableCollection<CandidateSetViewModel> PagedCandidates { get; }
            = new ObservableCollection<CandidateSetViewModel>();

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilterAndPage(); }
        }

        // ── Pagination ────────────────────────────────────────────────────────

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            private set => SetField(ref _currentPage, value);
        }

        private int _totalPages = 1;
        public int TotalPages
        {
            get => _totalPages;
            private set => SetField(ref _totalPages, value);
        }

        private int _totalFilteredCount;
        public int TotalFilteredCount
        {
            get => _totalFilteredCount;
            private set => SetField(ref _totalFilteredCount, value);
        }

        public bool HasPrevPage => _currentPage > 1;
        public bool HasNextPage => _currentPage < _totalPages;
        public bool IsPageEmpty => PagedCandidates.Count == 0;

        public string PageInfo => TotalPages > 0 ? $"Page {_currentPage} of {_totalPages}" : "No results";

        public ICommand PrevPageCommand { get; }
        public ICommand NextPageCommand { get; }

        // ── Selection ─────────────────────────────────────────────────────────

        private CandidateSetViewModel _selectedCandidate;
        public CandidateSetViewModel SelectedCandidate
        {
            get => _selectedCandidate;
            set
            {
                if (!SetField(ref _selectedCandidate, value)) return;
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(NoSelection));
                OnPropertyChanged(nameof(SelectedIsExpired));
                BuildComparisonRows();
            }
        }

        public bool HasSelection    => _selectedCandidate != null;
        public bool NoSelection     => _selectedCandidate == null;
        public bool SelectedIsExpired => _selectedCandidate?.IsExpired == true;

        // ── Item comparison ───────────────────────────────────────────────────

        public ObservableCollection<SetItemComparisonRow> ComparisonRows { get; }
            = new ObservableCollection<SetItemComparisonRow>();

        private int _matchedCount;
        public int MatchedCount { get => _matchedCount; private set => SetField(ref _matchedCount, value); }

        private int _newCount;
        public int NewCount { get => _newCount; private set => SetField(ref _newCount, value); }

        private int _missingCount;
        public int MissingCount { get => _missingCount; private set => SetField(ref _missingCount, value); }

        // ── State ─────────────────────────────────────────────────────────────

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }

        private bool _isConfirming;
        public bool IsConfirming { get => _isConfirming; set => SetField(ref _isConfirming, value); }

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

        private bool _isError;
        public bool IsError { get => _isError; set => SetField(ref _isError, value); }

        private bool _hasStatus;
        public bool HasStatus { get => _hasStatus; set => SetField(ref _hasStatus, value); }

        // ── Commands ──────────────────────────────────────────────────────────

        public ICommand LinkCommand          { get; }
        public ICommand ConfirmLinkCommand   { get; }
        public ICommand CancelConfirmCommand { get; }
        public ICommand CancelCommand        { get; }

        // ── Result ────────────────────────────────────────────────────────────

        public int?   ResultSetId   { get; private set; }
        public string ResultSetCode { get; private set; }

        public event Action<bool> CloseRequested;

        // ── Constructor ───────────────────────────────────────────────────────

        public LinkRenewalViewModel(RenewalDto original, List<InvoiceSetPickerDto> candidates)
        {
            ExpiringSet       = original ?? throw new ArgumentNullException(nameof(original));
            _allCandidateDtos = candidates ?? new List<InvoiceSetPickerDto>();

            LinkCommand          = new RelayCommand(() => IsConfirming = true, () => _selectedCandidate != null);
            ConfirmLinkCommand   = new RelayCommand(() => _ = ExecuteLinkAsync());
            CancelConfirmCommand = new RelayCommand(() => IsConfirming = false);
            CancelCommand        = new RelayCommand(() => CloseRequested?.Invoke(false));

            PrevPageCommand = new RelayCommand(GoPrevPage, () => HasPrevPage);
            NextPageCommand = new RelayCommand(GoNextPage, () => HasNextPage);

            foreach (var dto in _allCandidateDtos)
                AllCandidates.Add(new CandidateSetViewModel(dto));

            ApplyFilterAndPage();
        }

        // ── Async load ────────────────────────────────────────────────────────

        public async Task LoadAsync()
        {
            IsLoading = true;
            ClearStatus();
            try
            {
                var expiringItems = await Task.Run(() => _repo.GetSetItemSummaries(ExpiringSet.SetId));
                foreach (var item in expiringItems)
                    ExpiringItems.Add(item);

                var candidateIds = AllCandidates.Select(c => c.SetId).ToList();
                if (candidateIds.Any())
                {
                    var allCandidateItems = await Task.Run(
                        () => _repo.GetCandidateItemSummaries(candidateIds));

                    var itemsBySet = allCandidateItems
                        .GroupBy(x => x.SetId)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    foreach (var candidate in AllCandidates)
                    {
                        if (itemsBySet.TryGetValue(candidate.SetId, out var items))
                            candidate.Items = items;
                        candidate.ComputeMatch(expiringItems);
                    }

                    // Re-sort now that match % is populated
                    ApplyFilterAndPage();
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load data: {ex.Message}", isError: true);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Filter + sort + pagination ────────────────────────────────────────

        private void ApplyFilterAndPage()
        {
            var q = _searchText?.Trim().ToLowerInvariant() ?? "";

            _filteredSortedList = AllCandidates
                .Where(c => string.IsNullOrEmpty(q) || c.AllSearchableText.Contains(q))
                .OrderByDescending(c => c.MatchPercent)
                .ThenBy(c => c.IsExpired)          // active sets float above expired at same match %
                .ThenByDescending(c => c.SetId)
                .ToList();

            TotalFilteredCount = _filteredSortedList.Count;
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)_filteredSortedList.Count / PageSize));

            // Reset to page 1 whenever filter changes
            _currentPage = 1;
            OnPropertyChanged(nameof(CurrentPage));

            RepopulatePage();
        }

        private void RepopulatePage()
        {
            PagedCandidates.Clear();
            foreach (var item in _filteredSortedList.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedCandidates.Add(item);

            OnPropertyChanged(nameof(HasPrevPage));
            OnPropertyChanged(nameof(HasNextPage));
            OnPropertyChanged(nameof(IsPageEmpty));
            OnPropertyChanged(nameof(PageInfo));
        }

        private void GoPrevPage()
        {
            if (_currentPage <= 1) return;
            _currentPage--;
            OnPropertyChanged(nameof(CurrentPage));
            RepopulatePage();
        }

        private void GoNextPage()
        {
            if (_currentPage >= _totalPages) return;
            _currentPage++;
            OnPropertyChanged(nameof(CurrentPage));
            RepopulatePage();
        }

        // ── Comparison rows ───────────────────────────────────────────────────

        private void BuildComparisonRows()
        {
            ComparisonRows.Clear();
            MatchedCount = NewCount = MissingCount = 0;

            if (_selectedCandidate == null) return;

            var expiringCodes = new HashSet<string>(
                ExpiringItems.Select(x => (x.ItemCode ?? "").Trim()),
                StringComparer.OrdinalIgnoreCase);

            var candidateCodes = new HashSet<string>(
                _selectedCandidate.Items.Select(x => (x.ItemCode ?? "").Trim()),
                StringComparer.OrdinalIgnoreCase);

            foreach (var item in ExpiringItems.Where(i => candidateCodes.Contains((i.ItemCode ?? "").Trim())))
                ComparisonRows.Add(new SetItemComparisonRow { ItemCode = item.ItemCode, Description = item.Description, Status = "Matched" });

            foreach (var item in ExpiringItems.Where(i => !candidateCodes.Contains((i.ItemCode ?? "").Trim())))
                ComparisonRows.Add(new SetItemComparisonRow { ItemCode = item.ItemCode, Description = item.Description, Status = "Missing" });

            foreach (var item in _selectedCandidate.Items.Where(i => !expiringCodes.Contains((i.ItemCode ?? "").Trim())))
                ComparisonRows.Add(new SetItemComparisonRow { ItemCode = item.ItemCode, Description = item.Description, Status = "New" });

            MatchedCount = ComparisonRows.Count(r => r.Status == "Matched");
            NewCount     = ComparisonRows.Count(r => r.Status == "New");
            MissingCount = ComparisonRows.Count(r => r.Status == "Missing");
        }

        // ── Link execution ────────────────────────────────────────────────────

        private async Task ExecuteLinkAsync()
        {
            if (_selectedCandidate == null) return;

            IsConfirming = false;
            IsLoading    = true;
            SetStatus($"Linking {_selectedCandidate.SetCode}…");

            try
            {
                int    renewalSetId   = _selectedCandidate.SetId;
                string renewalSetCode = _selectedCandidate.SetCode;

                await Task.Run(() => _repo.LinkExistingInvoiceSet(ExpiringSet.SetId, renewalSetId));

                ResultSetId   = renewalSetId;
                ResultSetCode = renewalSetCode;
                CloseRequested?.Invoke(true);
            }
            catch (Exception ex)
            {
                SetStatus($"Linking failed: {ex.Message}", isError: true);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetStatus(string message, bool isError = false)
        {
            StatusMessage = message;
            IsError       = isError;
            HasStatus     = !string.IsNullOrEmpty(message);
        }

        private void ClearStatus()
        {
            StatusMessage = string.Empty;
            IsError       = false;
            HasStatus     = false;
        }
    }
}
