using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.RequestManagement.ViewModels
{
    /// <summary>
    /// One request line inside a mixed submission: what was asked for, what has been issued,
    /// what is in stock, and the quantity the user is about to issue now.
    /// </summary>
    public class MixedLineViewModel : ViewModelBase
    {
        private int    _issueQty;
        private int    _issueBrandNewQty;
        private int    _issueRefilledQty;
        private string _remarks;
        private MixedCartridgeLineInfo _cartridge;

        public MixedLineViewModel(MixedRequestLineDto dto, MixedGroupViewModel group)
        {
            Dto   = dto;
            Group = group;
        }

        public MixedRequestLineDto Dto   { get; }
        public MixedGroupViewModel Group { get; }

        public int      ReqId       => Dto.ReqId;
        public string   ItemName    => Dto.ItemName;
        public string   Category    => string.IsNullOrWhiteSpace(Dto.Category) ? "-" : Dto.Category;
        public int      Quantity    => Dto.Quantity;
        public int      IssuedQty   => Dto.IssuedQty;
        public int      PendingQty  => Math.Max(0, Dto.Quantity - Dto.IssuedQty);
        public int      StockOnHand => Dto.StockOnHand;
        public DateTime DateCreated => Dto.DateCreated;
        public bool     IsCartridge => string.Equals(Dto.Category, "Cartridge", StringComparison.OrdinalIgnoreCase);

        // ── Cartridge exchange (cartridge lines only) ────────────────────────────
        // A cartridge line is issued like the Cartridge Exchange: Brand New / Refilled quantities
        // against the model's stock, with the requester's empties collected in return.

        /// <summary>Model, stock by condition and declared empties; loaded when the submission is selected.</summary>
        public MixedCartridgeLineInfo Cartridge
        {
            get => _cartridge;
            set
            {
                if (!SetField(ref _cartridge, value)) return;
                _issueBrandNewQty = 0;
                _issueRefilledQty = 0;
                _issueQty         = 0;
                OnPropertyChanged(nameof(IsExchangeLine));
                OnPropertyChanged(nameof(IsPlainLine));
                OnPropertyChanged(nameof(MaxIssue));
                OnPropertyChanged(nameof(StockInfo));
                OnPropertyChanged(nameof(StockInfoBrush));
                RaiseIssueChanged();
            }
        }

        /// <summary>Issued as a cartridge exchange (Brand New / Refilled + returned empties).</summary>
        public bool IsExchangeLine => IsCartridge && _cartridge?.IsExchangeLine != false;
        public bool IsPlainLine    => !IsExchangeLine;

        public int AvailableBrandNew => _cartridge?.AvailableBrandNew ?? 0;
        public int AvailableRefilled => _cartridge?.AvailableRefilled ?? 0;

        public int MaxBrandNew => Math.Max(0, Math.Min(PendingQty - _issueRefilledQty, AvailableBrandNew));
        public int MaxRefilled => Math.Max(0, Math.Min(PendingQty - _issueBrandNewQty, AvailableRefilled));

        public int IssueBrandNewQty
        {
            get => _issueBrandNewQty;
            set
            {
                if (SetField(ref _issueBrandNewQty, Math.Max(0, Math.Min(value, MaxBrandNew))))
                    SyncExchangeTotal();
            }
        }

        public int IssueRefilledQty
        {
            get => _issueRefilledQty;
            set
            {
                if (SetField(ref _issueRefilledQty, Math.Max(0, Math.Min(value, MaxRefilled))))
                    SyncExchangeTotal();
            }
        }

        private void SyncExchangeTotal()
        {
            _issueQty = _issueBrandNewQty + _issueRefilledQty;
            RaiseIssueChanged();
        }

        private void RaiseIssueChanged()
        {
            OnPropertyChanged(nameof(IssueQty));
            OnPropertyChanged(nameof(IssueBrandNewQty));
            OnPropertyChanged(nameof(IssueRefilledQty));
            OnPropertyChanged(nameof(MaxBrandNew));
            OnPropertyChanged(nameof(MaxRefilled));
            OnPropertyChanged(nameof(IssueSummary));
            OnPropertyChanged(nameof(EmptiesInfo));
            RaiseStatusChanged();
        }

        /// <summary>
        /// The empties to collect for the quantity being issued, allocated the same way
        /// CartridgeManagementRepository.IssueMixedCartridgeLine records them: declared Good
        /// first, then declared Damaged, then any undeclared unit.
        /// </summary>
        public string EmptiesInfo
        {
            get
            {
                if (_cartridge == null || !IsExchangeLine) return string.Empty;
                var c = _cartridge;
                int goodLeft    = Math.Max(0, c.DeclaredGood - c.ReturnedGood);
                int damagedLeft = Math.Max(0, c.DeclaredDamaged - c.ReturnedDamaged);

                string declared = c.DeclaredGood + c.DeclaredDamaged == 0
                    ? "No empties declared by the requester"
                    : $"Declared empties: {c.DeclaredGood} Good, {c.DeclaredDamaged} Damaged";
                if (c.ReturnedGood + c.ReturnedDamaged > 0)
                    declared += $" (already returned: {c.ReturnedGood} Good, {c.ReturnedDamaged} Damaged)";

                if (_issueQty <= 0) return declared;

                int good    = Math.Min(_issueQty, goodLeft);
                int damaged = Math.Min(_issueQty - good, damagedLeft);
                int plain   = _issueQty - good - damaged;
                string collect = $"Collect {_issueQty} empty cartridge(s): {good + plain} Good, {damaged} Damaged";
                if (plain > 0) collect += $" ({plain} not declared)";
                return declared + "\n" + collect;
            }
        }

        /// <summary>Most that can be issued now: what is still pending, capped by stock on hand.</summary>
        public int MaxIssue => IsExchangeLine
            ? Math.Max(0, Math.Min(PendingQty, AvailableBrandNew + AvailableRefilled))
            : Math.Max(0, Math.Min(PendingQty, StockOnHand));

        /// <summary>
        /// Quantity to issue now. On a cartridge exchange line it is Brand New + Refilled and is
        /// set through those two; assigning 0 here clears both (used by Clear).
        /// </summary>
        public int IssueQty
        {
            get => _issueQty;
            set
            {
                if (IsExchangeLine)
                {
                    if (value <= 0 && _issueQty != 0)
                    {
                        _issueBrandNewQty = 0;
                        _issueRefilledQty = 0;
                        SyncExchangeTotal();
                    }
                    return;
                }

                int clamped = Math.Max(0, Math.Min(value, MaxIssue));
                if (SetField(ref _issueQty, clamped))
                {
                    OnPropertyChanged(nameof(IssueSummary));
                    RaiseStatusChanged();
                }
            }
        }

        public string Remarks
        {
            get => _remarks;
            set => SetField(ref _remarks, value);
        }

        public string StockInfo
        {
            get
            {
                if (PendingQty == 0) return "Fully issued";
                if (IsExchangeLine)
                {
                    if (_cartridge == null) return "Loading cartridge stock...";
                    if (!_cartridge.CartridgeModelId.HasValue)
                        return $"Model '{_cartridge.ModelNumber}' is not in Cartridge Models; cannot issue (stays pending)";
                    if (AvailableBrandNew + AvailableRefilled <= 0)
                        return $"{_cartridge.ModelNumber}: no stock. Stays pending until stock arrives";
                    return $"{_cartridge.ModelNumber} in stock: Brand New {AvailableBrandNew}, Refilled {AvailableRefilled}";
                }
                return StockOnHand <= 0 ? "No stock. Stays pending until stock arrives"
                     : StockOnHand < PendingQty ? $"In stock: {StockOnHand} (less than pending)"
                     : $"In stock: {StockOnHand}";
            }
        }

        public Brush StockInfoBrush
            => PendingQty > 0 && MaxIssue <= 0 && !(IsExchangeLine && _cartridge == null)
                ? Brushes.Firebrick
                : new SolidColorBrush(Color.FromRgb(0x5A, 0x6A, 0x7E));

        public string IssueSummary
            => IssueQty <= 0 ? string.Empty
             : IsExchangeLine ? $"Issuing {IssueQty} of {PendingQty} pending ({IssueBrandNewQty} Brand New, {IssueRefilledQty} Refilled)"
             : $"Issuing {IssueQty} of {PendingQty} pending";

        /// <summary>Status this line will have after Fulfill, counting what is being issued now.</summary>
        public string FulfillmentStatus
        {
            get
            {
                int issuedAfter = Dto.IssuedQty + IssueQty;
                return issuedAfter >= Dto.Quantity ? "Fulfilled"
                     : issuedAfter > 0 ? "Partially Fulfilled"
                     : "Unfulfilled";
            }
        }

        public Brush FulfillmentStatusBrush => MixedGroupViewModel.StatusBrush(FulfillmentStatus);

        /// <summary>Status right now, before this fulfill (left list).</summary>
        public string CurrentStatus
            => Dto.IssuedQty >= Dto.Quantity ? "Fulfilled"
             : Dto.IssuedQty > 0 ? "Partially Fulfilled"
             : "Unfulfilled";

        public Brush CurrentStatusBrush => MixedGroupViewModel.StatusBrush(CurrentStatus);

        private void RaiseStatusChanged()
        {
            OnPropertyChanged(nameof(FulfillmentStatus));
            OnPropertyChanged(nameof(FulfillmentStatusBrush));
            Group?.RaiseResultChanged();
        }
    }

    /// <summary>One submission (all its lines) shown as a block in the left list.</summary>
    public class MixedGroupViewModel : ViewModelBase
    {
        private bool  _isSelected;
        private Brush _dateBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x8E, 0xF6));

        public MixedGroupViewModel(Guid sessionId, IList<MixedRequestLineDto> lines)
        {
            SessionId = sessionId;
            var first = lines.First();

            Requester          = string.IsNullOrWhiteSpace(first.EmployeeName) ? "Dept Level" : first.EmployeeName;
            Company            = first.CompanyName ?? "Unknown Company";
            Branch             = first.BranchName ?? "Unknown Branch";
            Department         = first.DepartmentName ?? "Unknown Department";
            DistributionMethod = first.DistributionMethod;
            ReceivedBy         = first.ReceivedByName;
            DateCreated        = lines.Min(l => l.DateCreated);

            // Each line's Remarks is "<tag> | <requester's remark>", where the tag is the category
            // (Ink / Toner / Printhead) or the cartridge condition. Show the requester's own text.
            AdditionalRemarks = lines.Select(l => RequesterRemark(l.Remarks))
                                     .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r)) ?? string.Empty;

            Lines = lines.OrderBy(l => l.ReqId).Select(l => new MixedLineViewModel(l, this)).ToList();
        }

        /// <summary>
        /// The requester's part of a portal line's Remarks: the text after " | ", or nothing when
        /// the Remarks is only the category / cartridge-condition tag.
        /// </summary>
        private static string RequesterRemark(string remarks)
        {
            if (string.IsNullOrWhiteSpace(remarks)) return null;
            int bar = remarks.IndexOf(" | ", StringComparison.Ordinal);
            if (bar >= 0) return remarks.Substring(bar + 3).Trim();

            string t = remarks.Trim();
            bool isTagOnly = ConsumableCategories.IsConsumable(t)
                || t.Equals("With Cartridge", StringComparison.OrdinalIgnoreCase)
                || t.Equals("Without Cartridge", StringComparison.OrdinalIgnoreCase);
            return isTagOnly ? null : t;
        }

        public Guid   SessionId { get; }
        public string SessionKey => "G" + SessionId.ToString("N");

        public string   Requester          { get; }
        public string   Company            { get; }
        public string   Branch             { get; }
        public string   Department         { get; }
        public string   DistributionMethod { get; }
        public string   ReceivedBy         { get; }
        public string   AdditionalRemarks  { get; }
        public DateTime DateCreated        { get; }

        public List<MixedLineViewModel> Lines { get; }

        public string HeaderText   => $"{Requester} — {Company} → {Branch} → {Department}";
        public string ReqIds       => string.Join(", ", Lines.Select(l => l.ReqId));
        public int    TotalPending => Lines.Sum(l => l.PendingQty);
        public int    TotalQty     => Lines.Sum(l => l.Quantity);
        public string LineCountBadge => Lines.Count == 1 ? "1 line" : $"{Lines.Count} lines";

        /// <summary>
        /// Status the whole submission will have after Fulfill (Cartridge Exchange's
        /// "Fulfillment Status"): Unfulfilled when nothing is issued, Fulfilled when every line
        /// is fully issued, Partially Fulfilled otherwise.
        /// </summary>
        public string ResultStatus
        {
            get
            {
                int issuedAfter = Lines.Sum(l => l.IssuedQty + l.IssueQty);
                return issuedAfter <= 0 ? "Unfulfilled"
                     : Lines.All(l => l.IssuedQty + l.IssueQty >= l.Quantity) ? "Fulfilled"
                     : "Partially Fulfilled";
            }
        }

        public Brush ResultStatusBrush => StatusBrush(ResultStatus);

        public void RaiseResultChanged()
        {
            OnPropertyChanged(nameof(ResultStatus));
            OnPropertyChanged(nameof(ResultStatusBrush));
        }

        public static Brush StatusBrush(string status)
            => status == "Fulfilled"           ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
             : status == "Partially Fulfilled" ? new SolidColorBrush(Color.FromRgb(230, 126, 34))
             : new SolidColorBrush(Color.FromRgb(192, 57, 43));

        public Brush HeaderBrush      { get; } = new SolidColorBrush(Color.FromRgb(0xFF, 0xD9, 0xB8));
        public Brush HeaderForeground { get; } = new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x4A));

        public Brush DateBrush
        {
            get => _dateBrush;
            set => SetField(ref _dateBrush, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }
    }

    /// <summary>
    /// Mixed Request Exchange: the Cartridge Exchange counterpart for approved portal submissions
    /// that mix cartridge and non-cartridge items. Those submissions belong to Request and Set
    /// Management (dbo.Request.WorkflowType), so the Cartridge Exchange queue never lists them.
    /// Layout mirrors Cartridge Exchange: submissions on the left, the selected one on the right.
    /// Each line is fulfilled through RequestRepository.FulfillRequest (issued qty, audit trail,
    /// requester notification), the same path the Partially Fulfilled / Unfulfilled Requests pages use.
    /// Cartridge lines go through RequestRepository.FulfillCartridgeLine instead: Brand New /
    /// Refilled units and returned empties, exactly like the Cartridge Exchange.
    /// </summary>
    public class MixedRequestExchangeViewModel : ViewModelBase, IDisposable
    {
        private const int PageSize = 5;

        private readonly RequestRepository _repo = new RequestRepository();
        private readonly CartridgeManagementRepository _cartridgeRepo = new CartridgeManagementRepository();

        private List<MixedGroupViewModel> _allGroups = new List<MixedGroupViewModel>();

        private string    _searchText = "";
        private DateTime? _dateFrom;
        private DateTime? _dateTo;
        private string    _selectedQuickRange = "All";
        private string    _selectedCategoryFilter = "All";
        private bool      _sortAscending = true;
        private bool      _isLoading;
        private bool      _suppressRefresh;
        private int       _currentPage = 1;
        private int       _totalPages  = 1;
        private string    _pageInfo    = "No pending requests";
        private MixedGroupViewModel _selectedGroup;

        public MixedRequestExchangeViewModel()
        {
            RefreshCommand      = new RelayCommand(async () => await LoadAsync(), () => !_isLoading);
            ClearFilterCommand  = new RelayCommand(ClearFilters);
            ToggleSortCommand   = new RelayCommand(() => { _sortAscending = !_sortAscending; OnPropertyChanged(nameof(SortIndicatorText)); OnPropertyChanged(nameof(SortIndicatorColor)); ApplyPagedView(); });
            SelectGroupCommand  = new RelayCommand<MixedGroupViewModel>(SelectGroup);
            FulfillCommand      = new RelayCommand(OnFulfill, () => HasSelection);
            ClearCommand        = new RelayCommand(OnClear);

            FirstPageCommand = new RelayCommand(() => GoToPage(1),                () => CanGoFirst);
            PrevPageCommand  = new RelayCommand(() => GoToPage(_currentPage - 1), () => CanGoPrev);
            NextPageCommand  = new RelayCommand(() => GoToPage(_currentPage + 1), () => CanGoNext);
            LastPageCommand  = new RelayCommand(() => GoToPage(_totalPages),      () => CanGoLast);

            CategoryFilterOptions.Add("All");
        }

        // ── Left panel ───────────────────────────────────────────────────────────
        public ObservableCollection<MixedGroupViewModel> PagedGroups { get; } = new ObservableCollection<MixedGroupViewModel>();

        public ObservableCollection<string> CategoryFilterOptions { get; } = new ObservableCollection<string>();

        public string[] QuickRanges { get; } =
        {
            "All", "Last 7 days", "Last 30 days", "Last 90 days", "This month", "This year"
        };

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value ?? "")) Refilter(); }
        }

        public DateTime? DateFrom
        {
            get => _dateFrom;
            set { if (SetField(ref _dateFrom, value)) Refilter(); }
        }

        public DateTime? DateTo
        {
            get => _dateTo;
            set { if (SetField(ref _dateTo, value)) Refilter(); }
        }

        public string SelectedQuickRange
        {
            get => _selectedQuickRange;
            set
            {
                if (value == null) return;
                if (SetField(ref _selectedQuickRange, value)) ApplyQuickRange();
            }
        }

        public string SelectedCategoryFilter
        {
            get => _selectedCategoryFilter;
            set
            {
                if (value == null) return;   // ComboBox clears its selection while options are rebuilt
                if (SetField(ref _selectedCategoryFilter, value)) Refilter();
            }
        }

        public string SortIndicatorText  => _sortAscending ? "↑ Asc — Oldest First (Priority Order)" : "↓ Desc — Newest First";
        public string SortIndicatorColor => _sortAscending ? "#1E9E5E" : "#E07020";

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetField(ref _isLoading, value);
        }

        public string PageInfo
        {
            get => _pageInfo;
            private set => SetField(ref _pageInfo, value);
        }

        public bool CanGoFirst => _currentPage > 1;
        public bool CanGoPrev  => _currentPage > 1;
        public bool CanGoNext  => _currentPage < _totalPages;
        public bool CanGoLast  => _currentPage < _totalPages;

        public ICommand RefreshCommand     { get; }
        public ICommand ClearFilterCommand { get; }
        public ICommand ToggleSortCommand  { get; }
        public ICommand SelectGroupCommand { get; }
        public ICommand FirstPageCommand   { get; }
        public ICommand PrevPageCommand    { get; }
        public ICommand NextPageCommand    { get; }
        public ICommand LastPageCommand    { get; }

        // ── Right panel ──────────────────────────────────────────────────────────
        public ICommand FulfillCommand { get; }
        public ICommand ClearCommand   { get; }

        public MixedGroupViewModel SelectedGroup
        {
            get => _selectedGroup;
            private set
            {
                if (SetField(ref _selectedGroup, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool HasSelection => _selectedGroup != null;

        // ── Loading ──────────────────────────────────────────────────────────────
        public async Task InitializeAsync() => await LoadAsync();

        private async Task LoadAsync()
        {
            if (_isLoading) return;
            IsLoading = true;

            try
            {
                var lines = await Task.Run(() => _repo.GetApprovedMixedRequestLines())
                            ?? new List<MixedRequestLineDto>();

                string keepKey = _selectedGroup?.SessionKey;

                // Only submissions made entirely of known consumable categories are offered here,
                // the same rule the portal uses before it ever groups a submission into a Set.
                _allGroups = lines
                    .GroupBy(l => l.SubmissionSessionId)
                    .Select(g => new MixedGroupViewModel(g.Key, g.ToList()))
                    .Where(g => g.Lines.All(l => ConsumableCategories.IsKnown(l.Dto.Category)))
                    .OrderBy(g => g.DateCreated)
                    .ToList();

                for (int i = 0; i < _allGroups.Count; i++)
                    _allGroups[i].DateBrush = new SolidColorBrush(i == 0
                        ? Color.FromRgb(0x1E, 0x9E, 0x5E)    // oldest = green
                        : Color.FromRgb(0x3A, 0x8E, 0xF6));  // newer = blue

                RebuildCategoryOptions();

                // Keep the same submission selected after a refresh, if it is still pending.
                SelectedGroup = null;
                var again = keepKey == null ? null : _allGroups.FirstOrDefault(g => g.SessionKey == keepKey);
                if (again != null) SelectGroup(again);

                _currentPage = 1;
                ApplyPagedView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading mixed requests:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void RebuildCategoryOptions()
        {
            string keep = _selectedCategoryFilter;
            var cats = _allGroups.SelectMany(g => g.Lines).Select(l => l.Category)
                                 .Where(c => c != "-").Distinct(StringComparer.OrdinalIgnoreCase)
                                 .OrderBy(c => c).ToList();

            _suppressRefresh = true;
            try
            {
                CategoryFilterOptions.Clear();
                CategoryFilterOptions.Add("All");
                foreach (var c in cats) CategoryFilterOptions.Add(c);
                SelectedCategoryFilter = CategoryFilterOptions.Contains(keep) ? keep : "All";
            }
            finally { _suppressRefresh = false; }
        }

        // ── Filtering / paging ───────────────────────────────────────────────────
        private void Refilter()
        {
            if (_suppressRefresh) return;
            _currentPage = 1;
            ApplyPagedView();
        }

        private void ApplyQuickRange()
        {
            DateTime today = DateTime.Today;
            DateTime? from = null, to = null;

            switch (_selectedQuickRange)
            {
                case "Last 7 days":  from = today.AddDays(-7);  to = today; break;
                case "Last 30 days": from = today.AddDays(-30); to = today; break;
                case "Last 90 days": from = today.AddDays(-90); to = today; break;
                case "This month":   from = new DateTime(today.Year, today.Month, 1); to = today; break;
                case "This year":    from = new DateTime(today.Year, 1, 1); to = today; break;
            }

            _suppressRefresh = true;
            try { DateFrom = from; DateTo = to; }
            finally { _suppressRefresh = false; }

            Refilter();
        }

        private void ClearFilters()
        {
            _suppressRefresh = true;
            try
            {
                SearchText = "";
                DateFrom = null;
                DateTo = null;
                SelectedQuickRange = "All";
                SelectedCategoryFilter = "All";
            }
            finally { _suppressRefresh = false; }

            Refilter();
        }

        private bool Matches(MixedGroupViewModel g)
        {
            if (_dateFrom.HasValue && g.DateCreated.Date < _dateFrom.Value.Date) return false;
            if (_dateTo.HasValue   && g.DateCreated.Date > _dateTo.Value.Date)   return false;

            if (_selectedCategoryFilter != "All" &&
                !g.Lines.Any(l => string.Equals(l.Category, _selectedCategoryFilter, StringComparison.OrdinalIgnoreCase)))
                return false;

            string s = _searchText.Trim();
            if (s.Length == 0) return true;

            bool Has(string v) => !string.IsNullOrEmpty(v) && v.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
            return Has(g.Requester) || Has(g.Company) || Has(g.Branch) || Has(g.Department)
                || Has(g.ReqIds) || g.Lines.Any(l => Has(l.ItemName) || Has(l.Category));
        }

        private void ApplyPagedView()
        {
            var filtered = _allGroups.Where(Matches);
            var ordered = (_sortAscending ? filtered.OrderBy(g => g.DateCreated) : filtered.OrderByDescending(g => g.DateCreated)).ToList();

            int total = ordered.Count;
            _totalPages = Math.Max(1, (int)Math.Ceiling((double)total / PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1) _currentPage = 1;

            PagedGroups.Clear();
            foreach (var g in ordered.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedGroups.Add(g);

            PageInfo = total == 0 ? "No pending requests" : $"Page {_currentPage} of {_totalPages}  ({total} groups)";

            OnPropertyChanged(nameof(CanGoFirst));
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanGoLast));
            CommandManager.InvalidateRequerySuggested();
        }

        private void GoToPage(int page)
        {
            _currentPage = Math.Max(1, Math.Min(page, _totalPages));
            ApplyPagedView();
        }

        // ── Selection ────────────────────────────────────────────────────────────
        private void SelectGroup(MixedGroupViewModel group)
        {
            if (group == null || ReferenceEquals(group, _selectedGroup)) return;

            if (_selectedGroup != null) _selectedGroup.IsSelected = false;
            group.IsSelected = true;
            SelectedGroup = group;

            LoadCartridgeInfo(group);
        }

        /// <summary>
        /// Loads model, Brand New / Refilled stock and declared empties for the cartridge lines of
        /// the selected submission (fresh on every selection so stock is current).
        /// </summary>
        private async void LoadCartridgeInfo(MixedGroupViewModel group)
        {
            foreach (var line in group.Lines.Where(l => l.IsCartridge))
            {
                line.Cartridge = null;
                try
                {
                    line.Cartridge = await Task.Run(() => _cartridgeRepo.GetMixedCartridgeLineInfo(line.ReqId));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not load cartridge stock for Req #{line.ReqId}:\n\n{ex.Message}",
                        "Cartridge Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void OnClear()
        {
            if (_selectedGroup != null)
            {
                foreach (var l in _selectedGroup.Lines) { l.IssueQty = 0; l.Remarks = null; }
                _selectedGroup.IsSelected = false;
            }
            SelectedGroup = null;
        }

        // ── Fulfillment ──────────────────────────────────────────────────────────
        private async void OnFulfill()
        {
            var group = _selectedGroup;
            if (group == null) return;

            if (group.Lines.Any(l => l.IsCartridge && l.Cartridge == null && l.PendingQty > 0))
            {
                MessageBox.Show("Cartridge stock is still loading (or failed to load). Wait a moment, or re-select the request.",
                    "Cartridge Stock", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Like the Cartridge Exchange, Fulfill always records the outcome: whatever is issued
            // now (possibly nothing, when there is no stock) decides whether the submission ends
            // up Fulfilled, Partially Fulfilled or Unfulfilled. Lines not fully issued stay
            // pending in the submission's Set (Unfulfilled / Partially Fulfilled Requests).
            var toIssue = group.Lines.Where(l => l.IssueQty > 0).ToList();
            var unissued = group.Lines.Where(l => l.PendingQty > 0 && l.IssueQty == 0).ToList();
            string outcome = group.ResultStatus;

            string lineList = string.Join("\n", group.Lines.Where(l => l.PendingQty > 0).Select(l =>
            {
                string line = $"• Req #{l.ReqId}  {l.ItemName} ({l.Category}):  {l.IssueQty} / {l.PendingQty} pcs  ->  {l.FulfillmentStatus}";
                if (l.IsExchangeLine && l.IssueQty > 0)
                    line += $"\n      {l.IssueBrandNewQty} Brand New, {l.IssueRefilledQty} Refilled; collect {l.IssueQty} empty cartridge(s)";
                return line;
            }));

            string whereNext =
                outcome == "Fulfilled"           ? "Every line is fully issued."
              : outcome == "Partially Fulfilled" ? "The rest stays pending on Partially Fulfilled Requests."
              :                                    "Nothing is issued (no stock). It stays pending on Unfulfilled Requests.";

            var confirm = MessageBox.Show(
                $"Fulfill request for {group.Requester}?\n\n{lineList}\n\nResult: {outcome}. {whereNext}",
                "Confirm Fulfillment", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
            var done = new List<int>();
            bool succeeded = false;

            // The set every line of this submission was grouped into (its printable requisition
            // form is built from the set), read before the list refreshes.
            int? setId = group.Lines.Select(l => l.Dto.SetId).FirstOrDefault(s => s.HasValue);
            Guid sessionId = group.SessionId;

            try
            {
                foreach (var l in toIssue)
                {
                    string remarks = string.IsNullOrWhiteSpace(l.Remarks) ? null : l.Remarks.Trim();
                    if (l.IsExchangeLine)
                    {
                        int bn = l.IssueBrandNewQty, rf = l.IssueRefilledQty;
                        await Task.Run(() => _repo.FulfillCartridgeLine(l.ReqId, bn, rf, userId, remarks));
                    }
                    else
                    {
                        await Task.Run(() => _repo.FulfillRequest(l.ReqId, l.IssueQty, userId, remarks));
                    }
                    done.Add(l.ReqId);
                }

                succeeded = true;
            }
            catch (Exception ex)
            {
                string saved = done.Count == 0 ? "No lines were saved."
                    : "Already saved: Req # " + string.Join(", ", done) + ".";
                MessageBox.Show($"Error fulfilling request:\n\n{ex.Message}\n\n{saved}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // A self-service submission is not in a Set yet. Group the whole submission into one
            // now that IT has fulfilled it: this is what moves it onto Request & Set Management
            // (same steps the portal uses for IT-assisted submissions). Lines that were not fully
            // issued, even when nothing was issued at all, stay pending inside the Set and appear
            // on Unfulfilled / Partially Fulfilled Requests.
            bool grouped = setId.HasValue;
            if (succeeded && !setId.HasValue)
            {
                try
                {
                    var setRepo = new SetRepository();
                    int newSetId = await setRepo.CreateSetAsync(userId, remarks: "Auto-grouped from PORTAL submission");
                    foreach (var l in group.Lines)
                        await setRepo.AddRequestToSetAsync(l.ReqId, newSetId);
                    setId = newSetId;
                    grouped = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "The fulfillment was saved, but the request could not be grouped into a Set:\n\n" + ex.Message,
                        "Set Not Created", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            if (succeeded && grouped)
            {
                // Lines that got nothing: tell the requester, as the Cartridge Exchange does.
                // (Issued lines were already notified by FulfillRequest / FulfillCartridgeLine.)
                foreach (var l in unissued)
                {
                    int reqId = l.ReqId;
                    await Task.Run(() => _repo.NotifyUnfulfilled(reqId, userId));
                }

                string next =
                    outcome == "Fulfilled"           ? string.Empty
                  : outcome == "Partially Fulfilled" ? "\n\nThe remaining quantity is on Partially Fulfilled Requests."
                  :                                    "\n\nThe request is on Unfulfilled Requests until stock arrives.";
                MessageBox.Show($"Fulfillment recorded. Status: {outcome}.{next}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            SelectedGroup = null;
            await LoadAsync();

            // Offer the printable requisition form, as the Cartridge Exchange offers its transmittal.
            if (succeeded)
                FulfillmentCompleted?.Invoke(setId, sessionId);
        }

        /// <summary>
        /// Raised after a successful fulfill, with the set the submission belongs to (if any)
        /// and the submission's session id (used to look up who approved it).
        /// </summary>
        public event Action<int?, Guid> FulfillmentCompleted;

        public void Dispose() { }
    }
}
