using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.Search.ViewModels
{
    public class DetailFieldViewModel
    {
        public string Label { get; }
        public string Value { get; }
        public DetailFieldViewModel(string label, string value) { Label = label; Value = value; }
    }

    public class FilterOptionViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string Name { get; }
        internal Action OnSelectionChanged { private get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                Notify(nameof(IsSelected));
                OnSelectionChanged?.Invoke();
            }
        }

        public FilterOptionViewModel(string name) { Name = name; }
    }

    public class PageSizeOptionViewModel
    {
        public int    Value { get; }
        public string Label { get; }
        public PageSizeOptionViewModel(int v) { Value = v; Label = v == 0 ? "All" : v.ToString(); }
    }

    public class SearchViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private readonly System.Windows.Threading.Dispatcher _dispatcher =
            System.Windows.Threading.Dispatcher.CurrentDispatcher;

        // ── Callback wired by SearchView → MainForm ───────────────────────────
        public Action<ItemDto, string, string> NavigateAction { get; set; }

        // ── Search text ───────────────────────────────────────────────────────
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                Notify(nameof(SearchText));
            }
        }

        // ── UI state flags ────────────────────────────────────────────────────
        private bool _isInitial = true;
        public bool IsInitial
        {
            get => _isInitial;
            private set
            {
                _isInitial = value;
                Notify(nameof(IsInitial));
            }
        }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; private set { _isLoading = value; Notify(nameof(IsLoading)); } }

        private bool _isResults;
        public bool IsResults { get => _isResults; private set { _isResults = value; Notify(nameof(IsResults)); } }

        private bool _isNoResults;
        public bool IsNoResults { get => _isNoResults; private set { _isNoResults = value; Notify(nameof(IsNoResults)); } }

        private string _noResultsMessage = "";
        public string NoResultsMessage { get => _noResultsMessage; private set { _noResultsMessage = value; Notify(nameof(NoResultsMessage)); } }

        // Set when the results currently on screen came from the "Did you mean...?" fuzzy fallback
        // (SearchRepository.FuzzySearchFallbackAsync) rather than an exact tokenized match — lets the
        // results header show a banner explaining why, instead of presenting typo-corrected guesses
        // as if they were literal hits.
        private bool _isFuzzyResults;
        public bool IsFuzzyResults { get => _isFuzzyResults; private set { _isFuzzyResults = value; Notify(nameof(IsFuzzyResults)); } }

        private string _fuzzyNoticeText = "";
        public string FuzzyNoticeText { get => _fuzzyNoticeText; private set { _fuzzyNoticeText = value; Notify(nameof(FuzzyNoticeText)); } }

        private bool _showLogo = true;
        public bool ShowLogo { get => _showLogo; private set { _showLogo = value; Notify(nameof(ShowLogo)); } }

        private bool _showBack;
        public bool ShowBack
        {
            get => _showBack;
            private set
            {
                _showBack = value;
                Notify(nameof(ShowBack));
            }
        }

        // ── Suggestions (AutoCompleteBox dropdown, populated by SearchView's Populating handler) ──
        public ObservableCollection<SearchSuggestionDto> Suggestions { get; } = new ObservableCollection<SearchSuggestionDto>();

        // ── Filters ───────────────────────────────────────────────────────────
        public ObservableCollection<FilterOptionViewModel> FilterOptions { get; }
            = new ObservableCollection<FilterOptionViewModel>();

        private bool _filterDropdownOpen;
        public bool FilterDropdownOpen
        {
            get => _filterDropdownOpen;
            set { _filterDropdownOpen = value; Notify(nameof(FilterDropdownOpen)); }
        }

        private bool _hasActiveFilters;
        public bool HasActiveFilters
        {
            get => _hasActiveFilters;
            private set { _hasActiveFilters = value; Notify(nameof(HasActiveFilters)); }
        }

        private int _activeFilterCount;
        public int ActiveFilterCount
        {
            get => _activeFilterCount;
            private set { _activeFilterCount = value; Notify(nameof(ActiveFilterCount)); }
        }

        private bool _suppressFilterUpdate;

        // ── Include Archived ─────────────────────────────────────────────────
        private bool _includeArchived;
        public bool IncludeArchived
        {
            get => _includeArchived;
            set
            {
                if (_includeArchived == value) return;
                _includeArchived = value;
                Notify(nameof(IncludeArchived));
                if (!string.IsNullOrWhiteSpace(_currentQuery))
                    ExecuteSearch(_currentQuery);
            }
        }

        // ── Results ───────────────────────────────────────────────────────────
        private List<SearchCardViewModel> _allCards      = new List<SearchCardViewModel>();
        private List<SearchCardViewModel> _filteredCards = new List<SearchCardViewModel>();

        public ObservableCollection<SearchCardViewModel> PageCards { get; } = new ObservableCollection<SearchCardViewModel>();

        private SearchCardViewModel _selectedCard;
        public SearchCardViewModel SelectedCard
        {
            get => _selectedCard;
            set
            {
                if (_selectedCard == value) return;
                if (_selectedCard != null) _selectedCard.IsSelected = false;
                _selectedCard = value;
                if (_selectedCard != null) _selectedCard.IsSelected = true;
                Notify(nameof(SelectedCard));
                Notify(nameof(HasSelectedCard));
                Notify(nameof(NoSelectedCard));
                UpdateDetailFields();
            }
        }
        public bool HasSelectedCard => _selectedCard != null;
        public bool NoSelectedCard  => _selectedCard == null;

        // ── Detail panel ──────────────────────────────────────────────────────
        public ObservableCollection<DetailFieldViewModel> DetailFields { get; } = new ObservableCollection<DetailFieldViewModel>();

        private string _detailDestinationLabel = "";
        public string DetailDestinationLabel { get => _detailDestinationLabel; private set { _detailDestinationLabel = value; Notify(nameof(DetailDestinationLabel)); } }

        private string _detailItemName = "";
        public string DetailItemName { get => _detailItemName; private set { _detailItemName = value; Notify(nameof(DetailItemName)); } }

        private string _detailGroupNote = "";
        public string DetailGroupNote { get => _detailGroupNote; private set { _detailGroupNote = value; Notify(nameof(DetailGroupNote)); } }

        private bool _detailHasGroupNote;
        public bool DetailHasGroupNote { get => _detailHasGroupNote; private set { _detailHasGroupNote = value; Notify(nameof(DetailHasGroupNote)); } }

        private string _detailNavigateLabel = "Go to Inventory";
        public string DetailNavigateLabel { get => _detailNavigateLabel; private set { _detailNavigateLabel = value; Notify(nameof(DetailNavigateLabel)); } }

        // ── Page size ─────────────────────────────────────────────────────────
        public List<PageSizeOptionViewModel> PageSizeOptions { get; } = new List<PageSizeOptionViewModel>
        {
            new PageSizeOptionViewModel(12),
            new PageSizeOptionViewModel(24),
            new PageSizeOptionViewModel(48),
            new PageSizeOptionViewModel(96),
            new PageSizeOptionViewModel(0),
        };

        private PageSizeOptionViewModel _selectedPageSize;
        public PageSizeOptionViewModel SelectedPageSize
        {
            get => _selectedPageSize;
            set
            {
                if (_selectedPageSize == value) return;
                _selectedPageSize = value;
                Notify(nameof(SelectedPageSize));
                if (_allCards.Count > 0) ApplyFiltersAndPaginate(resetPage: true);
            }
        }

        private int EffectiveCardsPerPage
        {
            get
            {
                int v = _selectedPageSize?.Value ?? 12;
                return v == 0 ? Math.Max(1, _filteredCards.Count) : v;
            }
        }

        // ── Pagination ────────────────────────────────────────────────────────
        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            private set { _currentPage = value; Notify(nameof(CurrentPage)); Notify(nameof(PageLabel)); NotifyPagination(); }
        }

        private int _totalPages = 1;
        public int TotalPages
        {
            get => _totalPages;
            private set { _totalPages = value; Notify(nameof(TotalPages)); Notify(nameof(PageLabel)); NotifyPagination(); }
        }

        public string PageLabel => $"{_currentPage} / {_totalPages}";
        public bool   ShowPagination => _totalPages > 1;
        public bool   CanGoPrev      => _currentPage > 1;
        public bool   CanGoNext      => _currentPage < _totalPages;

        private string _jumpToPage = "";
        public string JumpToPage
        {
            get => _jumpToPage;
            set { _jumpToPage = value; Notify(nameof(JumpToPage)); }
        }

        private string _resultsCountLabel = "";
        public string ResultsCountLabel
        {
            get => _resultsCountLabel;
            private set { _resultsCountLabel = value; Notify(nameof(ResultsCountLabel)); }
        }

        private string _currentQuery = "";

        // ── Commands ──────────────────────────────────────────────────────────
        public ICommand ExecuteSearchCommand      { get; }
        public ICommand BackCommand               { get; }
        public ICommand PrevPageCommand           { get; }
        public ICommand NextPageCommand           { get; }
        public ICommand SelectCardCommand         { get; }
        public ICommand NavigateToCardCommand     { get; }
        public ICommand ToggleFilterDropdownCommand { get; }
        public ICommand ToggleFilterOptionCommand   { get; }
        public ICommand ClearFiltersCommand         { get; }
        public ICommand RemoveFilterCommand         { get; }
        public ICommand GoToPageCommand             { get; }

        private CancellationTokenSource _searchCts;

        public SearchViewModel()
        {
            _selectedPageSize = PageSizeOptions[0]; // default 12

            ExecuteSearchCommand = new RelayCommand(() =>
            {
                if (!string.IsNullOrWhiteSpace(_searchText))
                    ExecuteSearch(_searchText);
            });

            BackCommand = new RelayCommand(ResetView);

            PrevPageCommand = new RelayCommand(
                () => { _currentPage--; RenderPage(); NotifyPagination(); },
                () => CanGoPrev);

            NextPageCommand = new RelayCommand(
                () => { _currentPage++; RenderPage(); NotifyPagination(); },
                () => CanGoNext);

            SelectCardCommand = new RelayCommand<SearchCardViewModel>(card =>
            {
                if (card != null) SelectedCard = card;
            });

            NavigateToCardCommand = new RelayCommand(() =>
            {
                if (_selectedCard != null)
                    NavigateAction?.Invoke(_selectedCard.Item, _selectedCard.Destination, _selectedCard.DisplayTitle);
            });

            ToggleFilterDropdownCommand = new RelayCommand(() =>
                FilterDropdownOpen = !FilterDropdownOpen);

            ToggleFilterOptionCommand = new RelayCommand<string>(name =>
            {
                var opt = FilterOptions.FirstOrDefault(f => f.Name == name);
                if (opt != null) opt.IsSelected = !opt.IsSelected;
            });

            ClearFiltersCommand = new RelayCommand(() =>
            {
                _suppressFilterUpdate = true;
                foreach (var opt in FilterOptions) opt.IsSelected = false;
                _suppressFilterUpdate = false;
                ApplyFiltersAndPaginate(resetPage: true);
            });

            RemoveFilterCommand = new RelayCommand<string>(name =>
            {
                var opt = FilterOptions.FirstOrDefault(f => f.Name == name);
                if (opt != null) opt.IsSelected = false;
            });

            GoToPageCommand = new RelayCommand(ExecuteGoToPage);
        }

        /// <summary>Called by SearchView when the user picks a row from the AutoCompleteBox dropdown.</summary>
        public void SelectSuggestion(SearchSuggestionDto suggestion)
        {
            if (suggestion == null || string.IsNullOrWhiteSpace(suggestion.MatchText)) return;
            ExecuteSearch(suggestion.MatchText);
        }

        // ── Execute search ────────────────────────────────────────────────────
        public void ExecuteSearch(string query)
        {
            query = query?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(query)) { ResetView(); return; }

            // Code-behind runs searches straight off AutoCompleteBox.Text (see SearchView.xaml.cs),
            // bypassing the Text→SearchText binding entirely. Without this, SearchText/_currentQuery
            // could go stale — e.g. typing a *new* query into the header search box and hitting Enter
            // would run the new search correctly, but SearchText would still hold whatever was last
            // written through here, so Back would reopen the initial page showing the old query text
            // instead of a blank box. Setting it here, on every path into a search, keeps it authoritative.
            _searchText = query;
            Notify(nameof(SearchText));

            Suggestions.Clear();
            _currentQuery = query;
            ShowLogo    = false;
            ShowBack    = true;
            IsInitial   = false;
            IsLoading   = true;
            IsResults   = false;
            IsNoResults = false;
            IsFuzzyResults = false;
            FuzzyNoticeText = "";
            _allCards.Clear();
            _filteredCards.Clear();
            PageCards.Clear();
            SelectedCard = null;

            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    var repo = new SearchRepository();
                    var itemsTask = repo.SearchItemsAsync(query, 500);
                    var directTask = repo.SearchDirectMatchesAsync(query, _includeArchived, 100);
                    await Task.WhenAll(itemsTask, directTask);
                    if (token.IsCancellationRequested) return;

                    var items = itemsTask.Result;
                    items.AddRange(directTask.Result);

                    string queryLower = query.ToLowerInvariant();
                    var grouped = new Dictionary<string, List<ItemDto>>();

                    foreach (var item in items)
                    {
                        foreach (var dest in item.ValidDestinations)
                        {
                            string key;
                            if (!string.IsNullOrWhiteSpace(item.SerialNumber) && item.SerialNumber.ToLowerInvariant().Contains(queryLower))
                                key = $"SERIAL:{item.SerialNumber.ToUpper()}|{dest}";
                            else if (!string.IsNullOrWhiteSpace(item.ModelNumber) && item.ModelNumber.ToLowerInvariant().Contains(queryLower))
                                key = $"MODEL:{item.ModelNumber.ToUpper()}|{dest}";
                            else if (!string.IsNullOrWhiteSpace(item.MatchedSubTypeReferenceCode))
                                key = $"REFCODE:{item.MatchedSubTypeReferenceCode.ToUpper()}|{dest}";
                            else
                                key = $"NAME:{(item.Name ?? "").ToUpper()}|{dest}";

                            if (!grouped.ContainsKey(key)) grouped[key] = new List<ItemDto>();
                            grouped[key].Add(item);
                        }
                    }

                    var cards = grouped
                        .Select(kvp =>
                        {
                            var dest = kvp.Key.Substring(kvp.Key.IndexOf('|') + 1);
                            var rep = kvp.Value[0];
                            int badgeCount = rep.DestinationCounts.TryGetValue(dest, out var tc) ? tc : kvp.Value.Count;
                            return new SearchCardViewModel(rep, dest, query, badgeCount);
                        })
                        .Where(c => c.CountBadgeValue > 0)
                        .ToList();

                    // Strict fallback: only ever runs when the exact tokenized search above found
                    // nothing at all, and only relaxes code-shaped tokens (SetCode/Serial/Model) within
                    // an edit distance of 2 — see FuzzySearchFallbackAsync for the full guardrails.
                    bool isFuzzy = false;
                    if (cards.Count == 0)
                    {
                        var fuzzyMatches = await repo.FuzzySearchFallbackAsync(query, 8, token);
                        if (token.IsCancellationRequested) return;

                        if (fuzzyMatches.Count > 0)
                        {
                            isFuzzy = true;
                            cards = fuzzyMatches
                                .Select(m => new SearchCardViewModel(m, m.ValidDestinations.First(), query, 1))
                                .ToList();
                        }
                    }

                    // Collect destinations present in results (for filter options)
                    var destinations = cards.Select(c => c.Destination).Distinct().OrderBy(d => d).ToList();

                    _dispatcher.Invoke(() =>
                    {
                        if (token.IsCancellationRequested) return;

                        _allCards = cards;
                        IsLoading = false;
                        IsFuzzyResults = isFuzzy;
                        FuzzyNoticeText = isFuzzy
                            ? $"No exact matches for \"{query}\" — showing the closest results instead."
                            : "";

                        // Rebuild filter options, preserving existing selections
                        var prevSelected = FilterOptions.Where(f => f.IsSelected).Select(f => f.Name).ToHashSet();
                        _suppressFilterUpdate = true;
                        FilterOptions.Clear();
                        foreach (var dest in destinations)
                        {
                            var opt = new FilterOptionViewModel(dest);
                            opt.OnSelectionChanged = OnFilterSelectionChanged;
                            opt.IsSelected = prevSelected.Contains(dest);
                            FilterOptions.Add(opt);
                        }
                        _suppressFilterUpdate = false;

                        ApplyFiltersAndPaginate(resetPage: true);
                    });
                }
                catch (Exception ex)
                {
                    _dispatcher.Invoke(() =>
                    {
                        IsLoading        = false;
                        IsNoResults      = true;
                        NoResultsMessage = $"Search failed: {ex.Message}";
                    });
                }
            });
        }

        // ── Filter + paginate ─────────────────────────────────────────────────
        private void OnFilterSelectionChanged()
        {
            if (!_suppressFilterUpdate)
                ApplyFiltersAndPaginate(resetPage: true);
        }

        private void ApplyFiltersAndPaginate(bool resetPage)
        {
            var activeSet = FilterOptions.Where(f => f.IsSelected).Select(f => f.Name).ToHashSet();
            HasActiveFilters  = activeSet.Count > 0;
            ActiveFilterCount = activeSet.Count;

            _filteredCards = activeSet.Count == 0
                ? _allCards.ToList()
                : _allCards.Where(c => activeSet.Contains(c.Destination)).ToList();

            if (resetPage) _currentPage = 1;

            int cpp = EffectiveCardsPerPage;
            _totalPages = Math.Max(1, (int)Math.Ceiling(_filteredCards.Count / (double)cpp));
            _currentPage = Math.Max(1, Math.Min(_currentPage, _totalPages));
            NotifyPagination();

            // Update results count label
            ResultsCountLabel = activeSet.Count == 0
                ? $"{_allCards.Count} result{(_allCards.Count == 1 ? "" : "s")}"
                : $"{_filteredCards.Count} of {_allCards.Count} result{(_allCards.Count == 1 ? "" : "s")}";

            if (_filteredCards.Count == 0 && _allCards.Count > 0)
            {
                IsResults        = false;
                IsNoResults      = true;
                NoResultsMessage = "No results match the selected filters. Try adjusting your filters.";
            }
            else if (_filteredCards.Count > 0)
            {
                IsResults   = true;
                IsNoResults = false;
                RenderPage();
            }
            else
            {
                IsNoResults      = true;
                NoResultsMessage = $"No results found for \"{_currentQuery}\"";
            }
        }

        private void RenderPage()
        {
            PageCards.Clear();
            int cpp   = EffectiveCardsPerPage;
            int start = (_currentPage - 1) * cpp;
            int count = Math.Min(cpp, _filteredCards.Count - start);
            for (int i = 0; i < count; i++)
                PageCards.Add(_filteredCards[start + i]);
        }

        private void ExecuteGoToPage()
        {
            if (int.TryParse(_jumpToPage?.Trim(), out int page) && page >= 1 && page <= _totalPages)
            {
                _currentPage = page;
                RenderPage();
                NotifyPagination();
            }
            JumpToPage = "";
        }

        private void NotifyPagination()
        {
            Notify(nameof(CurrentPage));
            Notify(nameof(TotalPages));
            Notify(nameof(PageLabel));
            Notify(nameof(ShowPagination));
            Notify(nameof(CanGoPrev));
            Notify(nameof(CanGoNext));
        }

        // ── Reset ─────────────────────────────────────────────────────────────
        public void ResetView()
        {
            _searchCts?.Cancel();
            _searchText = "";
            Notify(nameof(SearchText));
            Suggestions.Clear();
            ShowLogo    = true;
            ShowBack    = false;
            IsInitial   = true;
            IsLoading   = false;
            IsResults   = false;
            IsNoResults = false;
            IsFuzzyResults = false;
            FuzzyNoticeText = "";
            _allCards.Clear();
            _filteredCards.Clear();
            PageCards.Clear();
            SelectedCard = null;
            _currentPage  = 1;
            _totalPages   = 1;
            FilterDropdownOpen = false;
            _suppressFilterUpdate = true;
            foreach (var opt in FilterOptions) opt.IsSelected = false;
            _suppressFilterUpdate = false;
            HasActiveFilters  = false;
            ActiveFilterCount = 0;
            ResultsCountLabel = "";
            JumpToPage        = "";
            NotifyPagination();
        }

        // ── Detail panel population ───────────────────────────────────────────
        private void UpdateDetailFields()
        {
            DetailFields.Clear();
            if (_selectedCard == null)
            {
                DetailDestinationLabel = "";
                DetailItemName         = "";
                DetailGroupNote        = "";
                DetailHasGroupNote     = false;
                DetailNavigateLabel    = "";
                return;
            }

            var item = _selectedCard.Item;
            var dest = _selectedCard.Destination;

            DetailDestinationLabel = dest.ToUpperInvariant();
            DetailItemName         = item.Name ?? "(Unnamed)";
            DetailNavigateLabel    = $"Go to {dest}  →";

            if (item.IsFuzzyMatch)
            {
                DetailGroupNote    = item.DirectMatchSubtitle; // "Closest match for \"...\" (N characters different)"
                DetailHasGroupNote = true;
            }
            else if (_selectedCard.GroupCount > 1)
            {
                DetailGroupNote    = $"Showing 1 of {_selectedCard.GroupCount} items in this group";
                DetailHasGroupNote = true;
            }
            else
            {
                DetailGroupNote    = "";
                DetailHasGroupNote = false;
            }

            var rows = BuildDetailRows(item, dest);
            foreach (var (label, value) in rows)
                DetailFields.Add(new DetailFieldViewModel(label, value));
        }

        private static List<(string, string)> BuildDetailRows(ItemDto item, string destination)
        {
            var rows = new List<(string, string)>();

            if (item.IsDirectMatch)
            {
                string codeLabel = destination == "Request" ? "Request"
                    : destination == "Invoice" ? "Invoice Code"
                    : destination == "Set" ? "Set Code"
                    : "Name"; // fuzzy fallback hits on Items/Renewal/Warranty carry a Name, not a code
                rows.Add((codeLabel, item.Name ?? "N/A"));
                if (!string.IsNullOrWhiteSpace(item.DirectMatchStatus)) rows.Add(("Status", item.DirectMatchStatus));
                // Fuzzy matches repurpose DirectMatchSubtitle for the "closest match" explanation,
                // which is already surfaced in the detail panel's banner (see UpdateDetailFields) —
                // showing it again here as "Created By"/"Requested By" would be actively misleading.
                if (!item.IsFuzzyMatch && !string.IsNullOrWhiteSpace(item.DirectMatchSubtitle))
                    rows.Add((destination == "Request" ? "Requested By" : "Created By", item.DirectMatchSubtitle));
                rows.Add(("Archived", item.IsArchived ? "Yes" : "No"));
                return rows;
            }

            switch (destination)
            {
                case "Warranty":
                    rows.Add(("Name",    item.Name ?? "N/A"));
                    rows.Add(("Serial",  item.SerialNumber ?? "N/A"));
                    rows.Add(("Model",   item.ModelNumber ?? "N/A"));
                    rows.Add(("Type",    item.ItemType ?? "Hardware"));
                    if (item.WarrantyYears > 0)           rows.Add(("Warranty Years", item.WarrantyYears.ToString()));
                    if (item.WarrantyStartDate.HasValue)  rows.Add(("Warranty Start", item.WarrantyStartDate.Value.ToString("MMM dd, yyyy")));
                    if (item.WarrantyEndDate.HasValue)    rows.Add(("Warranty End",   item.WarrantyEndDate.Value.ToString("MMM dd, yyyy")));
                    if (item.StartDate.HasValue)           rows.Add(("Start Date",     item.StartDate.Value.ToString("MMM dd, yyyy")));
                    if (item.EndDate.HasValue)             rows.Add(("End Date",       item.EndDate.Value.ToString("MMM dd, yyyy")));
                    if (!string.IsNullOrWhiteSpace(item.VendorName)) rows.Add(("Vendor", item.VendorName));
                    break;

                case "Repaired Items":
                    rows.Add(("Name",   item.Name ?? "N/A"));
                    rows.Add(("Serial", item.SerialNumber ?? "N/A"));
                    rows.Add(("Model",  item.ModelNumber ?? "N/A"));
                    if (!string.IsNullOrWhiteSpace(item.Category))     rows.Add(("Category",  item.Category));
                    if (item.ConditionId > 0 && !string.IsNullOrWhiteSpace(item.ConditionName)) rows.Add(("Condition", item.ConditionName));
                    rows.Add(("Stock On Hand", item.StockOnHand.ToString()));
                    rows.Add(("Active",        item.Active ? "Yes" : "No"));
                    if (!string.IsNullOrWhiteSpace(item.Remarks)) rows.Add(("Remarks", item.Remarks));
                    rows.Add(("Type", item.ItemType ?? "Hardware"));
                    break;

                case "Fixed Assets":
                    rows.Add(("Name",   item.Name ?? "N/A"));
                    rows.Add(("Serial", item.SerialNumber ?? "N/A"));
                    rows.Add(("Model",  item.ModelNumber ?? "N/A"));
                    if (!string.IsNullOrWhiteSpace(item.Category)) rows.Add(("Category", item.Category));
                    rows.Add(("Type",          item.ItemType ?? "Hardware"));
                    rows.Add(("Tracked Asset", item.IsTrackedAsset ? "Yes" : "No"));
                    rows.Add(("Date Created",  item.DateCreated.ToString("MMM dd, yyyy")));
                    if (!string.IsNullOrWhiteSpace(item.CreatedByName)) rows.Add(("Created By", item.CreatedByName));
                    if (item.Amount > 0) rows.Add(("Amount", item.Amount.ToString("C")));
                    if (!string.IsNullOrWhiteSpace(item.VendorName)) rows.Add(("Vendor", item.VendorName));
                    break;

                case "Archive":
                    rows.Add(("Name",     item.Name ?? "N/A"));
                    rows.Add(("Serial",   item.SerialNumber ?? "N/A"));
                    rows.Add(("Model",    item.ModelNumber ?? "N/A"));
                    if (!string.IsNullOrWhiteSpace(item.Category)) rows.Add(("Category", item.Category));
                    rows.Add(("Archived", item.IsArchived ? "Yes" : "No"));
                    if (!string.IsNullOrWhiteSpace(item.Remarks)) rows.Add(("Remarks", item.Remarks));
                    if (item.ConditionId > 0 && !string.IsNullOrWhiteSpace(item.ConditionName)) rows.Add(("Condition", item.ConditionName));
                    rows.Add(("Type", item.ItemType ?? "Hardware"));
                    break;

                case "Items":
                    rows.Add(("Name",         item.Name ?? "N/A"));
                    rows.Add(("Serial",        item.SerialNumber ?? "N/A"));
                    rows.Add(("Model",         item.ModelNumber ?? "N/A"));
                    if (!string.IsNullOrWhiteSpace(item.Description)) rows.Add(("Description",   item.Description));
                    if (!string.IsNullOrWhiteSpace(item.Category))    rows.Add(("Category",      item.Category));
                    rows.Add(("Type",          item.ItemType ?? "Hardware"));
                    if (!string.IsNullOrWhiteSpace(item.UnitOfMeasure)) rows.Add(("Unit of Measure", item.UnitOfMeasure));
                    rows.Add(("Stock On Hand", item.StockOnHand.ToString()));
                    rows.Add(("Active",        item.Active ? "Yes" : "No"));
                    rows.Add(("Date Created",  item.DateCreated.ToString("MMM dd, yyyy")));
                    if (!string.IsNullOrWhiteSpace(item.CreatedByName)) rows.Add(("Created By", item.CreatedByName));
                    if (item.ConditionId > 0 && !string.IsNullOrWhiteSpace(item.ConditionName)) rows.Add(("Condition", item.ConditionName));
                    if (!string.IsNullOrWhiteSpace(item.VendorName)) rows.Add(("Vendor",        item.VendorName));
                    if (item.WarrantyYears > 0) rows.Add(("Warranty Years", item.WarrantyYears.ToString()));
                    if (!string.IsNullOrWhiteSpace(item.Remarks)) rows.Add(("Remarks", item.Remarks));
                    break;

                case "Set":
                    rows.Add(("Name",   item.Name ?? "N/A"));
                    rows.Add(("Serial", item.SerialNumber ?? "N/A"));
                    rows.Add(("Model",  item.ModelNumber ?? "N/A"));
                    if (!string.IsNullOrWhiteSpace(item.Category)) rows.Add(("Category", item.Category));
                    rows.Add(("Type", item.ItemType ?? "Hardware"));
                    rows.Add(("Stock On Hand", item.StockOnHand.ToString()));
                    break;

                case "Request":
                    rows.Add(("Name",   item.Name ?? "N/A"));
                    rows.Add(("Serial", item.SerialNumber ?? "N/A"));
                    rows.Add(("Model",  item.ModelNumber ?? "N/A"));
                    if (!string.IsNullOrWhiteSpace(item.Category)) rows.Add(("Category", item.Category));
                    rows.Add(("Type", item.ItemType ?? "Hardware"));
                    rows.Add(("Stock On Hand", item.StockOnHand.ToString()));
                    break;

                case "Invoice":
                    rows.Add(("Name",   item.Name ?? "N/A"));
                    rows.Add(("Serial", item.SerialNumber ?? "N/A"));
                    rows.Add(("Model",  item.ModelNumber ?? "N/A"));
                    if (!string.IsNullOrWhiteSpace(item.Category)) rows.Add(("Category", item.Category));
                    rows.Add(("Type", item.ItemType ?? "Hardware"));
                    if (item.Amount > 0) rows.Add(("Unit Price", item.Amount.ToString("C")));
                    if (!string.IsNullOrWhiteSpace(item.VendorName)) rows.Add(("Vendor", item.VendorName));
                    break;

                case "Renewal":
                case "Renewals (Grouped)":
                    rows.Add(("Name",   item.Name ?? "N/A"));
                    rows.Add(("Serial", item.SerialNumber ?? "N/A"));
                    rows.Add(("Model",  item.ModelNumber ?? "N/A"));
                    if (!string.IsNullOrWhiteSpace(item.Category)) rows.Add(("Category", item.Category));
                    rows.Add(("Type", item.ItemType ?? "Hardware"));
                    if (item.EndDate.HasValue) rows.Add(("End Date", item.EndDate.Value.ToString("MMM dd, yyyy")));
                    if (!string.IsNullOrWhiteSpace(item.VendorName)) rows.Add(("Vendor", item.VendorName));
                    break;

                default: // Inventory
                    rows.Add(("Name",   item.Name ?? "N/A"));
                    rows.Add(("Serial", item.SerialNumber ?? "N/A"));
                    rows.Add(("Model",  item.ModelNumber ?? "N/A"));
                    if (!string.IsNullOrWhiteSpace(item.Category)) rows.Add(("Category", item.Category));
                    rows.Add(("Type",              item.ItemType ?? "Hardware"));
                    rows.Add(("Stock On Hand",     item.StockOnHand.ToString()));
                    rows.Add(("Affects Inventory", item.AffectsInventory ? "Yes" : "No"));
                    if (!string.IsNullOrWhiteSpace(item.AcquisitionType)) rows.Add(("Acquisition Type", item.AcquisitionType));
                    if (!string.IsNullOrWhiteSpace(item.UnitOfMeasure))   rows.Add(("Unit of Measure",  item.UnitOfMeasure));
                    if (item.ConditionId > 0 && !string.IsNullOrWhiteSpace(item.ConditionName)) rows.Add(("Condition", item.ConditionName));
                    if (!string.IsNullOrWhiteSpace(item.VendorName)) rows.Add(("Vendor",         item.VendorName));
                    if (item.WarrantyYears > 0)                      rows.Add(("Warranty Years", item.WarrantyYears.ToString()));
                    break;
            }

            return rows;
        }
    }
}
