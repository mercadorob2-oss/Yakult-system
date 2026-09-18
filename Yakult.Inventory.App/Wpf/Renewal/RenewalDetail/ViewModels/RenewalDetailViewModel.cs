using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalDetail.ViewModels
{
    public class RenewalDetailViewModel : ViewModelBase
    {
        private readonly RenewalRepository _repository;
        private readonly int _setId;

        public event EventHandler<int> NavigateToSetRequested;
        public event EventHandler      CloseRequested;
        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestError;

        // ── Loading ───────────────────────────────────────────────────────────────
        private bool _isLoading = true;
        public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }

        private string _errorMessage;
        public string ErrorMessage { get => _errorMessage; set { SetField(ref _errorMessage, value); OnPropertyChanged(nameof(HasError)); } }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        // ── Header fields ─────────────────────────────────────────────────────────
        private string _setCode = "—";
        public string SetCode { get => _setCode; set => SetField(ref _setCode, value); }

        private string _documentNumber = "—";
        public string DocumentNumber { get => _documentNumber; set => SetField(ref _documentNumber, value); }

        private string _referenceNumber = "—";
        public string ReferenceNumber { get => _referenceNumber; set => SetField(ref _referenceNumber, value); }

        private string _companyName = "—";
        public string CompanyName { get => _companyName; set => SetField(ref _companyName, value); }

        private string _vendorName = "—";
        public string VendorName { get => _vendorName; set => SetField(ref _vendorName, value); }

        private string _setType = "—";
        public string SetType { get => _setType; set => SetField(ref _setType, value); }

        private string _statusBadge = "—";
        public string StatusBadge
        {
            get => _statusBadge;
            set { SetField(ref _statusBadge, value); OnPropertyChanged(nameof(StatusBadgeColor)); }
        }

        public string StatusBadgeColor
        {
            get
            {
                switch (_statusBadge)
                {
                    case "Active":            return "#1E9E5E";
                    case "Expiring Soon":     return "#E08A00";
                    case "Warning":           return "#F4A261";
                    case "Expired":           return "#E03C31";
                    case "Partially Renewed": return "#3A8EF6";
                    case "Fully Renewed":     return "#1A6DD0";
                    case "Archived":          return "#5A6A7E";
                    default:                  return "#8896A7";
                }
            }
        }

        // ── Dates ─────────────────────────────────────────────────────────────────
        private DateTime? _startDate;
        public DateTime? StartDate { get => _startDate; set { SetField(ref _startDate, value); OnPropertyChanged(nameof(StartDateDisplay)); } }

        private DateTime? _endDate;
        public DateTime? EndDate { get => _endDate; set { SetField(ref _endDate, value); OnPropertyChanged(nameof(EndDateDisplay)); } }

        public string StartDateDisplay => StartDate?.ToString("MMM d, yyyy") ?? "Not set";
        public string EndDateDisplay   => EndDate?.ToString("MMM d, yyyy")   ?? "Not set";

        private int? _daysRemaining;
        public int? DaysRemaining
        {
            get => _daysRemaining;
            set { SetField(ref _daysRemaining, value); OnPropertyChanged(nameof(DaysRemainingText)); OnPropertyChanged(nameof(DaysRemainingColor)); }
        }

        public string DaysRemainingText
        {
            get
            {
                if (!DaysRemaining.HasValue) return "No expiry";
                if (DaysRemaining <= 0)      return $"{Math.Abs(DaysRemaining.Value)}d overdue";
                if (DaysRemaining == 1)      return "Expires tomorrow";
                return $"{DaysRemaining} days left";
            }
        }

        public string DaysRemainingColor
        {
            get
            {
                if (!DaysRemaining.HasValue) return "#8896A7";
                if (DaysRemaining <= 0)      return "#E03C31";
                if (DaysRemaining <= 30)     return "#E08A00";
                if (DaysRemaining <= 90)     return "#F4A261";
                return "#1E9E5E";
            }
        }

        // ── Document header (editable fields shown as read-only) ─────────────────
        private DateTime? _documentDate;
        public DateTime? DocumentDate { get => _documentDate; set { SetField(ref _documentDate, value); OnPropertyChanged(nameof(DocumentDateDisplay)); } }
        public string DocumentDateDisplay => DocumentDate?.ToString("MMMM d, yyyy") ?? "—";

        private string _createdByUsername = "—";
        public string CreatedByUsername { get => _createdByUsername; set => SetField(ref _createdByUsername, value); }

        // ── Site ──────────────────────────────────────────────────────────────────
        private string _siteDisplay = "—";
        public string SiteDisplay { get => _siteDisplay; set => SetField(ref _siteDisplay, value); }

        // ── Financial ─────────────────────────────────────────────────────────────
        private decimal _subtotal;
        public decimal Subtotal { get => _subtotal; set { SetField(ref _subtotal, value); OnPropertyChanged(nameof(SubtotalDisplay)); } }
        public string SubtotalDisplay => $"₱{Subtotal:N2}";

        private decimal _vatAmount;
        public decimal VatAmount { get => _vatAmount; set { SetField(ref _vatAmount, value); OnPropertyChanged(nameof(VatAmountDisplay)); } }
        public string VatAmountDisplay => $"₱{VatAmount:N2}";

        private decimal _whtAmount;
        public decimal WhtAmount { get => _whtAmount; set { SetField(ref _whtAmount, value); OnPropertyChanged(nameof(WhtAmountDisplay)); } }
        public string WhtAmountDisplay => $"₱{WhtAmount:N2}";

        private decimal _discountAmount;
        public decimal DiscountAmount { get => _discountAmount; set { SetField(ref _discountAmount, value); OnPropertyChanged(nameof(DiscountAmountDisplay)); } }
        public string DiscountAmountDisplay => $"₱{DiscountAmount:N2}";

        private decimal _totalAmountDue;
        public decimal TotalAmountDue { get => _totalAmountDue; set { SetField(ref _totalAmountDue, value); OnPropertyChanged(nameof(TotalAmountDueDisplay)); } }
        public string TotalAmountDueDisplay => $"₱{TotalAmountDue:N2}";

        // ── Item counts ───────────────────────────────────────────────────────────
        private int _activeItems;
        public int ActiveItems { get => _activeItems; set => SetField(ref _activeItems, value); }

        private int _renewedItems;
        public int RenewedItems { get => _renewedItems; set => SetField(ref _renewedItems, value); }

        private int _archivedItems;
        public int ArchivedItems { get => _archivedItems; set => SetField(ref _archivedItems, value); }

        // ── Renewal summary bar ───────────────────────────────────────────────────
        private int _totalRenewalsInChain;
        public int TotalRenewalsInChain { get => _totalRenewalsInChain; set => SetField(ref _totalRenewalsInChain, value); }

        private int _currentVersion;
        public int CurrentVersion { get => _currentVersion; set => SetField(ref _currentVersion, value); }

        private string _lastRenewalDate = "—";
        public string LastRenewalDate { get => _lastRenewalDate; set => SetField(ref _lastRenewalDate, value); }

        // ── Chain navigation ──────────────────────────────────────────────────────
        private bool _hasPreviousSet;
        public bool HasPreviousSet { get => _hasPreviousSet; set => SetField(ref _hasPreviousSet, value); }

        private bool _hasNextSet;
        public bool HasNextSet { get => _hasNextSet; set => SetField(ref _hasNextSet, value); }

        private int? _previousSetId;
        private int? _nextSetId;

        // ── History empty state ───────────────────────────────────────────────────
        private bool _hasNoHistory;
        public bool HasNoHistory { get => _hasNoHistory; set => SetField(ref _hasNoHistory, value); }

        // ── Renewal Notes (display-only; set via the "Renew Items" dialog's NOTES box) ─
        private string _currentRenewalNotes;
        public string CurrentRenewalNotes
        {
            get => _currentRenewalNotes;
            set
            {
                if (SetField(ref _currentRenewalNotes, value))
                    OnPropertyChanged(nameof(CurrentRenewalNotesDisplay));
            }
        }
        public string CurrentRenewalNotesDisplay =>
            string.IsNullOrWhiteSpace(CurrentRenewalNotes) ? "No notes were added for this renewal." : CurrentRenewalNotes;

        // ── Collections ───────────────────────────────────────────────────────────
        public ObservableCollection<RenewalChainNodeViewModel> ChainNodes   { get; } = new ObservableCollection<RenewalChainNodeViewModel>();
        public ObservableCollection<InvoiceItemRowViewModel>   InvoiceItems { get; } = new ObservableCollection<InvoiceItemRowViewModel>();
        public ObservableCollection<RenewalHistoryRowViewModel> HistoryItems { get; } = new ObservableCollection<RenewalHistoryRowViewModel>();
        public ObservableCollection<SubTypeGroupCardViewModel> GroupCards { get; } = new ObservableCollection<SubTypeGroupCardViewModel>();

        private bool _hasGroupCards;
        public bool HasGroupCards { get => _hasGroupCards; set => SetField(ref _hasGroupCards, value); }

        // ── Commands ──────────────────────────────────────────────────────────────
        public ICommand NavigatePreviousCommand { get; }
        public ICommand NavigateNextCommand     { get; }
        public ICommand NavigateToNodeCommand   { get; }
        public ICommand CloseCommand            { get; }

        public RenewalDetailViewModel(int setId)
        {
            _setId      = setId;
            _repository = new RenewalRepository();

            NavigatePreviousCommand = new RelayCommand(
                () => NavigateToSetRequested?.Invoke(this, _previousSetId.Value),
                () => HasPreviousSet);

            NavigateNextCommand = new RelayCommand(
                () => NavigateToSetRequested?.Invoke(this, _nextSetId.Value),
                () => HasNextSet);

            NavigateToNodeCommand = new RelayCommand<int>(
                id => { if (id != _setId) NavigateToSetRequested?.Invoke(this, id); });

            CloseCommand = new RelayCommand(
                () => CloseRequested?.Invoke(this, EventArgs.Empty));
        }

        public async Task LoadAsync()
        {
            IsLoading    = true;
            ErrorMessage = null;

            try
            {
                // Load all data off the UI thread
                var header  = await Task.Run(() => _repository.GetSetHeader(_setId));
                var set     = await Task.Run(() => _repository.GetRenewalSetById(_setId));
                var items   = await Task.Run(() => _repository.GetSetItemsForRenewal(_setId));
                var chain   = await Task.Run(() => _repository.GetRenewalChain(_setId));
                var history = await Task.Run(() => _repository.GetRenewalHistoryBySetId(_setId));
                var groups  = await Task.Run(() => _repository.GetSubTypeGroupSummaries(_setId));

                // Apply to UI-bound properties (already on dispatcher via async/await WPF default)
                if (header != null)
                {
                    DocumentNumber   = header.DocumentNumber   ?? "—";
                    ReferenceNumber  = header.ReferenceNumber  ?? "—";
                    DocumentDate     = header.DocumentDate;
                    CreatedByUsername = header.CreatedByUsername ?? "—";
                }

                if (set != null)
                {
                    SetCode         = set.SetCode       ?? "—";
                    CompanyName     = set.CompanyName   ?? "—";
                    VendorName      = set.VendorName    ?? "—";
                    SetType         = set.SetType       ?? "—";
                    StatusBadge     = set.SetLevelStatus ?? set.ExpiryStatus ?? "—";
                    StartDate       = set.StartDate;
                    EndDate         = set.EndDate;
                    DaysRemaining   = set.DaysUntilExpiry;
                    SiteDisplay     = set.SiteDisplay;
                    Subtotal        = set.Subtotal;
                    VatAmount       = set.VatAmount;
                    WhtAmount       = set.WhtAmount;
                    DiscountAmount  = set.DiscountAmount;
                    TotalAmountDue  = set.TotalAmountDue;
                    ActiveItems     = set.TotalActiveItems;
                    RenewedItems    = set.RenewedItemsCount;
                    ArchivedItems   = set.ArchivedItemsCount;
                }

                // Chain nodes
                ChainNodes.Clear();
                int currentIndex = -1;
                for (int i = 0; i < chain.Count; i++)
                {
                    var c = chain[i];
                    var node = new RenewalChainNodeViewModel
                    {
                        SetId          = c.SetId,
                        SetCode        = c.SetCode,
                        DocumentNumber = c.DocumentNumber,
                        RenewalNumber  = c.RenewalNumber,
                        StartDate      = c.StartDate,
                        EndDate        = c.EndDate,
                        IsCurrent      = c.SetId == _setId,
                        IsLast         = i == chain.Count - 1
                    };
                    ChainNodes.Add(node);
                    if (node.IsCurrent) { currentIndex = i; CurrentVersion = c.RenewalNumber; }
                }

                TotalRenewalsInChain = chain.Count;
                HasPreviousSet = currentIndex > 0;
                HasNextSet     = currentIndex >= 0 && currentIndex < chain.Count - 1;
                _previousSetId = HasPreviousSet ? chain[currentIndex - 1].SetId : (int?)null;
                _nextSetId     = HasNextSet     ? chain[currentIndex + 1].SetId : (int?)null;

                // Per-item "currently in effect" renewal record, keyed by ItemId — the entry with
                // the highest non-archived RenewalCount (same selection rule the History tab below
                // uses to mark a row IsCurrent). Used only to derive CurrentRenewalNotes below —
                // Renewal Notes is entered once per renewal action and applies to every item
                // renewed together, so it's surfaced as a single Renewal-level field, not a
                // per-item column.
                var latestNonArchivedCountByItem = history
                    .Where(h => !h.IsArchived)
                    .GroupBy(h => h.ItemId)
                    .ToDictionary(g => g.Key, g => g.Max(h => h.RenewalCount));

                CurrentRenewalNotes = history
                    .Where(h => !h.IsArchived
                        && latestNonArchivedCountByItem.TryGetValue(h.ItemId, out var maxCount)
                        && h.RenewalCount == maxCount
                        && !string.IsNullOrWhiteSpace(h.RenewalNotes))
                    .Select(h => h.RenewalNotes)
                    .FirstOrDefault();

                // Invoice items
                InvoiceItems.Clear();
                foreach (var item in items)
                {
                    InvoiceItems.Add(new InvoiceItemRowViewModel
                    {
                        SetItemId     = item.SetItemId,
                        ItemCode      = item.ItemCode,
                        Description   = item.Description,
                        ItemName      = item.ItemName,
                        Quantity      = item.Quantity,
                        UnitOfMeasure = item.UnitOfMeasure,
                        UnitPrice     = item.UnitPrice,
                        Amount        = item.Amount,
                        LineStartDate = item.LineStartDate,
                        LineEndDate   = item.LineEndDate,
                        RenewalStatus = item.RenewalStatus,
                        GroupId       = item.GroupId,
                        SubType       = item.SubType,
                        ReferenceCode = item.ReferenceCode
                    });
                }

                // Sub-Type Group financial cards
                foreach (var card in GroupCards)
                {
                    card.SaveSucceeded -= OnGroupCardSaveSucceeded;
                    card.SaveFailed    -= OnGroupCardSaveFailed;
                }
                GroupCards.Clear();
                foreach (var g in groups)
                {
                    var card = new SubTypeGroupCardViewModel(g);
                    card.SaveSucceeded += OnGroupCardSaveSucceeded;
                    card.SaveFailed    += OnGroupCardSaveFailed;
                    GroupCards.Add(card);
                }
                HasGroupCards = GroupCards.Count > 0;

                // History (newest first)
                HistoryItems.Clear();
                var sorted = history.OrderByDescending(h => h.RenewedDate ?? h.CreatedAt).ToList();

                // Per item, the entry with the highest non-archived RenewalCount is the one
                // actually in effect right now — everything else has been superseded even though
                // its own stored RenewalStatus still literally says "Renewed"/"Active". Without
                // this, RenewalHistoryRowViewModel.IsCurrent defaults to false and every row
                // renders as "Superseded" (see RenewalWorkspaceViewModel.LoadRenewalHistoryAsync
                // for the same logic, used by the "Manage Items" window). Computed once above
                // (latestNonArchivedCountByItem) and reused here — order doesn't affect the result.
                for (int i = 0; i < sorted.Count; i++)
                {
                    var h = sorted[i];
                    bool isCurrent = !h.IsArchived
                        && latestNonArchivedCountByItem.TryGetValue(h.ItemId, out var maxCount)
                        && h.RenewalCount == maxCount;

                    HistoryItems.Add(new RenewalHistoryRowViewModel
                    {
                        RenewalStatus    = h.RenewalStatus,
                        RenewedDate      = h.RenewedDate,
                        RenewalCount     = h.RenewalCount,
                        NewStartDate     = h.NewStartDate,
                        NewEndDate       = h.NewEndDate,
                        RenewalYears     = h.RenewalYears,
                        RenewalNotes     = h.RenewalNotes,
                        RenewalAmount    = h.RenewalAmount,
                        CreatedByUsername = h.CreatedByUsername,
                        CreatedAt        = h.CreatedAt,
                        IsArchived       = h.IsArchived,
                        ItemName         = h.ItemName,
                        ItemCode         = h.ItemCode,
                        IsCurrent        = isCurrent
                    });
                }

                HasNoHistory = HistoryItems.Count == 0;

                if (sorted.Count > 0)
                    LastRenewalDate = (sorted[0].RenewedDate ?? sorted[0].CreatedAt).ToString("MMMM d, yyyy");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load renewal details: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnGroupCardSaveSucceeded(string message) => RequestInfo?.Invoke("Saved", message);
        private void OnGroupCardSaveFailed(string message) => RequestError?.Invoke("Save Failed", message);
    }
}
