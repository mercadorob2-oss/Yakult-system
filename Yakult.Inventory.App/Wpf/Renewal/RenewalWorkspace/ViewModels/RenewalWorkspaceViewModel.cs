using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Renewal.RenewalDetail.ViewModels;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalWorkspace.ViewModels
{
    /// <summary>
    /// Payload for the "Renew Items" flow — the View constructs and owns RenewItemsWindow
    /// (it needs to be the WPF Owner), the ViewModel only supplies the arguments.
    /// </summary>
    public sealed class RenewItemsRequest
    {
        public int SetId { get; set; }
        public string SetCode { get; set; }
        public string Title { get; set; }
        public List<SetItemRenewalDto> Items { get; set; }
        public List<ItemCatalogDto> Catalog { get; set; }
        public decimal BaseSubtotal { get; set; }
        public decimal BaseVatPct { get; set; }
        public decimal BaseWhtPct { get; set; }
        public decimal BaseDiscountPct { get; set; }
        public decimal OriginalTotal { get; set; }
    }

    /// <summary>
    /// WPF replacement for the WinForms ViewRenewalDetailPage. Preserves the same backend
    /// calls/behavior; only the presentation layer changed. See
    /// Repositories/RenewalRepository.cs for the ad-hoc SQL helpers that used to live as
    /// private methods on the WinForms page.
    /// </summary>
    public class RenewalWorkspaceViewModel : ViewModelBase
    {
        private readonly RenewalRepository _repository = new RenewalRepository();
        private readonly VendorRepository _vendorRepository = new VendorRepository();

        private readonly int _setId;
        private string _setCode;
        private int _itemId;
        private RenewalDetailDto _renewalDetail;
        private bool _isFirstTimeRenewal;
        private List<RenewalChainDto> _renewalChain;
        private List<SetItemRenewalDto> _allSetItems;
        private List<VendorDto> _vendors;
        private bool _isVendorSyncing;

        /// <summary>Preserved from the WinForms page — invoked after a first-time renewal is created from the Warranty page.</summary>
        public Action OnRenewalCreated { get; set; }

        public event EventHandler CloseRequested;
        public event EventHandler<int> NavigateToSetRequested;
        public event Action<RenewItemsRequest> RequestRenewItems;
        public event Action<int> RequestAttachReceipt;
        public event Action<int> RequestViewChainReceipts;
        public event Action<string> RequestInfo;
        public event Action<string> RequestWarning;
        public event Action<string> RequestError;

        /// <summary>(title, message) -> user's Yes/No answer. Set by the View.</summary>
        public Func<string, string, bool> ConfirmYesNo { get; set; }

        /// <summary>(promptText, caption, defaultValue) -> entered text, or null if cancelled. Set by the View.</summary>
        public Func<string, string, string, string> RequestPrompt { get; set; }

        public RenewalWorkspaceViewModel(int setId)
        {
            _setId = setId;
            _itemId = 0;
        }

        public RenewalWorkspaceViewModel(int itemId, bool isItemIdConstructor)
        {
            _setId = 0;
            _itemId = itemId;
        }

        public bool HasSet => _setId > 0;

        // ── Loading ───────────────────────────────────────────────────────────────
        private bool _isLoading = true;
        public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }

        private string _errorMessage;
        public string ErrorMessage { get => _errorMessage; set { SetField(ref _errorMessage, value); OnPropertyChanged(nameof(HasError)); } }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        // ── Header banner ─────────────────────────────────────────────────────────
        private string _headerTitle = "—";
        public string HeaderTitle { get => _headerTitle; set => SetField(ref _headerTitle, value); }

        private string _headerSubtitle = "—";
        public string HeaderSubtitle { get => _headerSubtitle; set => SetField(ref _headerSubtitle, value); }

        private string _expiryStatusText = "—";
        public string ExpiryStatusText { get => _expiryStatusText; set => SetField(ref _expiryStatusText, value); }

        private string _expiryStatusColor = "#8896A7";
        public string ExpiryStatusColor { get => _expiryStatusColor; set => SetField(ref _expiryStatusColor, value); }

        private string _expiryWarningText = "—";
        public string ExpiryWarningText { get => _expiryWarningText; set => SetField(ref _expiryWarningText, value); }

        private string _expiryWarningColor = "#8896A7";
        public string ExpiryWarningColor { get => _expiryWarningColor; set => SetField(ref _expiryWarningColor, value); }

        private bool _pausedNoteVisible;
        public bool PausedNoteVisible { get => _pausedNoteVisible; set => SetField(ref _pausedNoteVisible, value); }

        private string _daysLeftText = "N/A";
        public string DaysLeftText { get => _daysLeftText; set => SetField(ref _daysLeftText, value); }

        private string _daysLeftColor = "#8896A7";
        public string DaysLeftColor { get => _daysLeftColor; set => SetField(ref _daysLeftColor, value); }

        // ── Editable document header (Document #, Reference #, Document Date) ──────
        private string _documentNumberEdit = "";
        public string DocumentNumberEdit { get => _documentNumberEdit; set => SetField(ref _documentNumberEdit, value); }

        private string _referenceNumberEdit = "";
        public string ReferenceNumberEdit { get => _referenceNumberEdit; set => SetField(ref _referenceNumberEdit, value); }

        private DateTime _documentDateEdit = DateTime.Today;
        public DateTime DocumentDateEdit { get => _documentDateEdit; set => SetField(ref _documentDateEdit, value); }

        // ── Item information ─────────────────────────────────────────────────────
        private string _itemName = "";
        public string ItemName { get => _itemName; set => SetField(ref _itemName, value); }

        private string _description = "";
        public string Description { get => _description; set => SetField(ref _description, value); }

        private string _itemType = "";
        public string ItemType { get => _itemType; set => SetField(ref _itemType, value); }

        private string _categoryName = "";
        public string CategoryName { get => _categoryName; set => SetField(ref _categoryName, value); }

        private string _serialNumber = "";
        public string SerialNumber { get => _serialNumber; set => SetField(ref _serialNumber, value); }

        private string _modelNumber = "";
        public string ModelNumber { get => _modelNumber; set => SetField(ref _modelNumber, value); }

        private string _licenseNumber = "";
        public string LicenseNumber { get => _licenseNumber; set => SetField(ref _licenseNumber, value); }

        private string _partNumber = "";
        public string PartNumber { get => _partNumber; set => SetField(ref _partNumber, value); }

        // ── Invoice details (Set-level — shown instead of single-item info when HasSet) ──
        private string _invoiceSetType = "—";
        public string InvoiceSetType { get => _invoiceSetType; set => SetField(ref _invoiceSetType, value); }

        private string _invoiceCompanyName = "—";
        public string InvoiceCompanyName { get => _invoiceCompanyName; set => SetField(ref _invoiceCompanyName, value); }

        private string _invoiceVendorName = "—";
        public string InvoiceVendorName { get => _invoiceVendorName; set => SetField(ref _invoiceVendorName, value); }

        private string _invoiceStartDateDisplay = "—";
        public string InvoiceStartDateDisplay { get => _invoiceStartDateDisplay; set => SetField(ref _invoiceStartDateDisplay, value); }

        private string _invoiceEndDateDisplay = "—";
        public string InvoiceEndDateDisplay { get => _invoiceEndDateDisplay; set => SetField(ref _invoiceEndDateDisplay, value); }

        private string _invoiceItemSummary = "—";
        public string InvoiceItemSummary { get => _invoiceItemSummary; set => SetField(ref _invoiceItemSummary, value); }

        private string _invoiceSubTypeSummary = "—";
        public string InvoiceSubTypeSummary { get => _invoiceSubTypeSummary; set => SetField(ref _invoiceSubTypeSummary, value); }

        private string _amountDisplay = "0.00";
        public string AmountDisplay { get => _amountDisplay; set => SetField(ref _amountDisplay, value); }

        // ── Renewal info (Start/End Date now genuinely editable + saveable) ────────
        private string _renewalStatusText = "None";
        public string RenewalStatusText { get => _renewalStatusText; set => SetField(ref _renewalStatusText, value); }

        private DateTime _startDateEdit = DateTime.Today;
        public DateTime StartDateEdit { get => _startDateEdit; set => SetField(ref _startDateEdit, value); }

        private DateTime _endDateEdit = DateTime.Today;
        public DateTime EndDateEdit { get => _endDateEdit; set => SetField(ref _endDateEdit, value); }

        // ── Vendor info ──────────────────────────────────────────────────────────
        public ObservableCollection<VendorDto> Vendors { get; } = new ObservableCollection<VendorDto>();

        private VendorDto _selectedVendor;
        public VendorDto SelectedVendor
        {
            get => _selectedVendor;
            set
            {
                SetField(ref _selectedVendor, value);
                if (_isVendorSyncing) return;
                if (value == null || value.VendorId <= 0)
                {
                    VendorAddress = "";
                    VendorTIN = "";
                    return;
                }
                VendorAddress = value.Address ?? "";
                VendorTIN = value.TIN ?? "";
            }
        }

        private string _vendorAddress = "";
        public string VendorAddress { get => _vendorAddress; set => SetField(ref _vendorAddress, value); }

        private string _vendorTIN = "";
        public string VendorTIN { get => _vendorTIN; set => SetField(ref _vendorTIN, value); }

        private bool _vendorEditable = true;
        public bool VendorEditable { get => _vendorEditable; set => SetField(ref _vendorEditable, value); }

        // ── Site info ────────────────────────────────────────────────────────────
        private string _siteDisplay = "N/A";
        public string SiteDisplay { get => _siteDisplay; set => SetField(ref _siteDisplay, value); }

        // ── Financial info ───────────────────────────────────────────────────────
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

        private string _financialStripText = "";
        public string FinancialStripText { get => _financialStripText; set => SetField(ref _financialStripText, value); }

        // ── Archive info (display-only; actual archiving happens via the footer button) ─
        private bool _isArchived;
        public bool IsArchived { get => _isArchived; set => SetField(ref _isArchived, value); }

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

        // ── Metadata ─────────────────────────────────────────────────────────────
        private string _createdByText = "Created By: Unknown";
        public string CreatedByText { get => _createdByText; set => SetField(ref _createdByText, value); }

        private string _createdAtText = "Created: —";
        public string CreatedAtText { get => _createdAtText; set => SetField(ref _createdAtText, value); }

        private bool _archiveVisible;
        public bool ArchiveVisible { get => _archiveVisible; set => SetField(ref _archiveVisible, value); }

        // ── Chain strip ──────────────────────────────────────────────────────────
        public ObservableCollection<RenewalChainNodeViewModel> ChainNodes { get; } = new ObservableCollection<RenewalChainNodeViewModel>();

        // ── Invoice Items tab ────────────────────────────────────────────────────
        public ObservableCollection<InvoiceItemRowViewModel> InvoiceItems { get; } = new ObservableCollection<InvoiceItemRowViewModel>();

        private string _itemCountsText = "Loading items...";
        public string ItemCountsText { get => _itemCountsText; set => SetField(ref _itemCountsText, value); }

        // Sub-Type Group financial cards — same feature as RenewalDetailWindow's ("View Details")
        // Invoice Items tab, so this window can stand alone without needing to also open that one.
        public ObservableCollection<SubTypeGroupCardViewModel> GroupCards { get; } = new ObservableCollection<SubTypeGroupCardViewModel>();

        private bool _hasGroupCards;
        public bool HasGroupCards { get => _hasGroupCards; set => SetField(ref _hasGroupCards, value); }

        /// <summary>The same invoice items as <see cref="InvoiceItems"/>, bucketed by Sub-Type Group.</summary>
        public ObservableCollection<InvoiceItemGroupViewModel> InvoiceItemGroups { get; }
            = new ObservableCollection<InvoiceItemGroupViewModel>();

        // ── View toggle (Table / Grouped) ────────────────────────────────────────
        private bool _isGroupedView;
        public bool IsGroupedView
        {
            get => _isGroupedView;
            set
            {
                if (SetField(ref _isGroupedView, value))
                {
                    OnPropertyChanged(nameof(IsTableView));
                    OnPropertyChanged(nameof(ViewModeIcon));
                    OnPropertyChanged(nameof(ViewModeText));
                }
            }
        }

        public bool IsTableView => !IsGroupedView;

        // Label/icon describe the view you'd switch TO, not the one you're in.
        public string ViewModeIcon => IsGroupedView ? "▤" : "▥";
        public string ViewModeText => IsGroupedView ? "Table View" : "Grouped View";

        // ── Renewal History tab ──────────────────────────────────────────────────
        public ObservableCollection<RenewalHistoryRowViewModel> HistoryItems { get; } = new ObservableCollection<RenewalHistoryRowViewModel>();

        private bool _hasNoHistory;
        public bool HasNoHistory { get => _hasNoHistory; set => SetField(ref _hasNoHistory, value); }

        // ── Commands ─────────────────────────────────────────────────────────────
        public ICommand SaveHeaderCommand { get; private set; }
        public ICommand SaveDatesCommand { get; private set; }
        public ICommand SaveVendorCommand { get; private set; }
        public ICommand SaveRenewalNotesCommand { get; private set; }
        public ICommand RenewItemsCommand { get; private set; }
        public ICommand AttachReceiptCommand { get; private set; }
        public ICommand ViewChainReceiptsCommand { get; private set; }
        public ICommand ArchiveCommand { get; private set; }
        public ICommand NavigateToNodeCommand { get; private set; }
        public ICommand ToggleViewCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }

        public void WireCommands()
        {
            SaveHeaderCommand   = new RelayCommand(async () => await SaveHeaderAsync());
            SaveDatesCommand    = new RelayCommand(async () => await SaveDatesAsync());
            SaveVendorCommand   = new RelayCommand(async () => await SaveVendorAsync());
            SaveRenewalNotesCommand = new RelayCommand(async () => await SaveRenewalNotesAsync());
            RenewItemsCommand   = new RelayCommand(RenewItems);
            AttachReceiptCommand = new RelayCommand(() => RequestAttachReceipt?.Invoke(_setId));
            ViewChainReceiptsCommand = new RelayCommand(() => RequestViewChainReceipts?.Invoke(_setId));
            ArchiveCommand      = new RelayCommand(async () => await ArchiveAsync());
            NavigateToNodeCommand = new RelayCommand<int>(id => { if (id != _setId) NavigateToSetRequested?.Invoke(this, id); });
            ToggleViewCommand = new RelayCommand(() => IsGroupedView = !IsGroupedView);
            CloseCommand        = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
        }

        // ── Load ─────────────────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsLoading    = true;
            ErrorMessage = null;

            try
            {
                if (_itemId == 0 && _setId > 0)
                {
                    _itemId = await Task.Run(() => _repository.GetItemIdFromSet(_setId));
                    if (_itemId == 0)
                    {
                        RequestError?.Invoke("No items found in this set.");
                        CloseRequested?.Invoke(this, EventArgs.Empty);
                        return;
                    }
                }
                else if (_itemId == 0)
                {
                    RequestError?.Invoke("Invalid item reference.");
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    return;
                }

                _renewalDetail = await Task.Run(() => _repository.GetRenewalDetailsByItemId(_itemId));

                if (_renewalDetail == null)
                {
                    _isFirstTimeRenewal = true;
                    _renewalDetail = await Task.Run(() => _repository.LoadItemDataWithoutRenewal(_itemId));
                    if (_renewalDetail == null)
                    {
                        RequestError?.Invoke("Item details not found.");
                        CloseRequested?.Invoke(this, EventArgs.Empty);
                        return;
                    }
                }
                else
                {
                    _isFirstTimeRenewal = false;
                }

                if (_isFirstTimeRenewal) ApplyFirstTimeInvoiceBaselineAmount();
                else ApplySubsequentRenewalBaselineAmount();

                ApplyAuthoritativeDates();

                await LoadVendorsAsync();

                _renewalDetail.PartNumber = await Task.Run(() => _repository.LoadPartNumberFromRenewals(_itemId));

                if (_setId > 0)
                {
                    await LoadSetItemsAsync();
                    await LoadSetHeaderAsync();
                    await LoadChainAsync();
                }

                PopulateFields();

                // For a Set-backed session, Set-level history (GetRenewalHistoryBySetId) is
                // independent of _isFirstTimeRenewal — that flag only reflects whether
                // GetRenewalDetailsByItemId found a row for the arbitrarily-picked "primary" item
                // (_itemId from GetItemIdFromSet), which can return null even when other items in
                // the Set do have renewal history. Always attempt the Set-level query when HasSet.
                if (_setId > 0 || !_isFirstTimeRenewal)
                    await LoadRenewalHistoryAsync();
                else
                {
                    HistoryItems.Clear();
                    HasNoHistory = true;
                }
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

        private void ApplyFirstTimeInvoiceBaselineAmount()
        {
            if (_renewalDetail == null) return;

            var invoice = _repository.GetLatestInvoiceFinancialsByItemId(_itemId);
            if (invoice == null)
            {
                if (_renewalDetail.Amount > 0m)
                {
                    _renewalDetail.Subtotal = _renewalDetail.Amount;
                    _renewalDetail.VatAmount = 0m;
                    _renewalDetail.WhtAmount = 0m;
                    _renewalDetail.DiscountAmount = 0m;
                    _renewalDetail.TotalAmountDue = _renewalDetail.Amount;
                }
                return;
            }

            _renewalDetail.Subtotal = invoice.Subtotal;
            _renewalDetail.VatAmount = invoice.VatAmount;
            _renewalDetail.WhtAmount = invoice.WhtAmount;
            _renewalDetail.DiscountAmount = invoice.DiscountAmount;
            _renewalDetail.TotalAmountDue = invoice.TotalAmountDue;
            _renewalDetail.Amount = invoice.TotalAmountDue;
        }

        private void ApplySubsequentRenewalBaselineAmount()
        {
            if (_renewalDetail == null) return;

            var lastRenewalAmount = _repository.GetLatestRenewalAmountByItemId(_itemId);
            var invoice = _repository.GetLatestInvoiceFinancialsByItemId(_itemId);

            if (invoice != null)
            {
                _renewalDetail.Subtotal = invoice.Subtotal;
                _renewalDetail.VatAmount = invoice.VatAmount;
                _renewalDetail.WhtAmount = invoice.WhtAmount;
                _renewalDetail.DiscountAmount = invoice.DiscountAmount;
                _renewalDetail.TotalAmountDue = invoice.TotalAmountDue;
                _renewalDetail.Amount = invoice.TotalAmountDue;
            }

            if (lastRenewalAmount.HasValue && lastRenewalAmount.Value > 0m)
            {
                _renewalDetail.TotalAmountDue = lastRenewalAmount.Value;
                _renewalDetail.Amount = lastRenewalAmount.Value;
            }
            else if (invoice == null && _renewalDetail.Amount > 0m && _renewalDetail.TotalAmountDue <= 0m)
            {
                _renewalDetail.Subtotal = _renewalDetail.Amount;
                _renewalDetail.VatAmount = 0m;
                _renewalDetail.WhtAmount = 0m;
                _renewalDetail.DiscountAmount = 0m;
                _renewalDetail.TotalAmountDue = _renewalDetail.Amount;
            }
        }

        private void ApplyAuthoritativeDates()
        {
            if (_renewalDetail == null) return;

            var setDates = _repository.GetSetDateRangeIfSoftwareOrService(_setId);
            if (setDates == null) return;

            _renewalDetail.StartDate = setDates.StartDate;
            _renewalDetail.EndDate = setDates.EndDate;

            if (!_renewalDetail.EndDate.HasValue)
            {
                _renewalDetail.DaysUntilExpiry = null;
                _renewalDetail.ExpiryStatus = "No Expiry Date";
                return;
            }

            int daysUntilExpiry = (int)(_renewalDetail.EndDate.Value.Date - DateTime.Today).TotalDays;
            _renewalDetail.DaysUntilExpiry = daysUntilExpiry;

            if (daysUntilExpiry < 0) _renewalDetail.ExpiryStatus = "Expired";
            else if (daysUntilExpiry <= 30) _renewalDetail.ExpiryStatus = "Expiring Soon";
            else if (daysUntilExpiry <= 90) _renewalDetail.ExpiryStatus = "Warning";
            else _renewalDetail.ExpiryStatus = "Active";
        }

        private async Task LoadVendorsAsync()
        {
            if (_vendors == null)
            {
                var all = await _vendorRepository.GetAllVendorsAsync();
                _vendors = new List<VendorDto>
                {
                    new VendorDto { VendorId = 0, VendorName = "-- Select Vendor --", IsActive = true, CreatedDate = DateTime.Now }
                };
                _vendors.AddRange(all.Where(v => v != null && v.IsActive));
            }

            _isVendorSyncing = true;
            try
            {
                Vendors.Clear();
                foreach (var v in _vendors) Vendors.Add(v);

                int currentVendorId = _renewalDetail?.VendorId ?? 0;
                VendorDto match = null;
                if (currentVendorId > 0)
                    match = _vendors.FirstOrDefault(v => v.VendorId == currentVendorId);
                else if (!string.IsNullOrWhiteSpace(_renewalDetail?.VendorName))
                    match = _vendors.FirstOrDefault(v => v.VendorId > 0 &&
                        string.Equals(v.VendorName?.Trim(), _renewalDetail.VendorName.Trim(), StringComparison.OrdinalIgnoreCase));

                SelectedVendor = match ?? _vendors.FirstOrDefault(v => v.VendorId == 0);
                VendorAddress = _renewalDetail?.VendorAddress ?? "";
                VendorTIN = _renewalDetail?.VendorTIN ?? "";
            }
            finally
            {
                _isVendorSyncing = false;
            }
        }

        private async Task LoadSetItemsAsync()
        {
            var items = await Task.Run(() => _repository.GetSetItemsForRenewal(_setId));
            _allSetItems = items;

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
                    ReferenceCode = item.ReferenceCode,
                    ParentTagGroupId = item.ParentTagGroupId,
                    ParentTag     = item.ParentTag
                });
            }

            // Grouped View: the same rows bucketed by Sub-Type Group. Keyed on
            // SubType + ReferenceCode (the canonical model), with untagged lines collected
            // into a trailing "Ungrouped Items" section so nothing is hidden.
            InvoiceItemGroups.Clear();
            var buckets = InvoiceItems
                .GroupBy(r => string.IsNullOrWhiteSpace(r.SubType)
                                  ? string.Empty
                                  : r.SubType + "|" + (r.ReferenceCode ?? string.Empty))
                .OrderBy(g => g.Key == string.Empty ? 1 : 0)   // ungrouped last
                .ThenBy(g => g.Key);

            foreach (var bucket in buckets)
            {
                var first = bucket.First();
                var groupVm = new InvoiceItemGroupViewModel
                {
                    GroupKey    = bucket.Key == string.Empty ? null : bucket.Key,
                    GroupHeader = bucket.Key == string.Empty
                        ? "Ungrouped Items"
                        : string.IsNullOrWhiteSpace(first.ReferenceCode)
                            ? first.SubType.ToUpperInvariant()
                            : first.SubType.ToUpperInvariant() + "   |   " + first.ReferenceCode
                };

                foreach (var row in bucket)
                    groupVm.Items.Add(row);

                // Nested one level deeper: this Sub-Type bucket's own items, further bucketed
                // by Parent Tag — an independent grouping, so items with no Parent Tag land in
                // a Label=null section the view collapses rather than a second "Ungrouped".
                var parentTagBuckets = bucket
                    .GroupBy(r => string.IsNullOrWhiteSpace(r.ParentTag) ? string.Empty : r.ParentTag)
                    .OrderBy(g => g.Key == string.Empty ? 1 : 0)
                    .ThenBy(g => g.Key);

                foreach (var ptBucket in parentTagBuckets)
                {
                    var ptVm = new ParentTagSubGroupViewModel
                    {
                        Label = ptBucket.Key == string.Empty ? null : ptBucket.Key
                    };
                    foreach (var row in ptBucket)
                        ptVm.Items.Add(row);
                    groupVm.ParentTagGroups.Add(ptVm);
                }

                InvoiceItemGroups.Add(groupVm);
            }

            ItemCountsText =
                $"Total: {items.Count}   |   " +
                $"Active: {items.Count(x => string.IsNullOrEmpty(x.RenewalStatus))}   |   " +
                $"Renewed: {items.Count(x => x.RenewalStatus == "Renewed")}   |   " +
                $"Archived: {items.Count(x => x.RenewalStatus == "Archived")}";

            // Sub-Type breakdown, e.g. "Contract(1), Subscription(2), License(3), Services(4)" —
            // catalog order, so it reads consistently with the Sub-Type dropdowns elsewhere.
            var subTypeCounts = items
                .Where(x => !string.IsNullOrWhiteSpace(x.SubType))
                .GroupBy(x => x.SubType)
                .ToDictionary(g => g.Key, g => g.Count());
            var subTypeParts = ItemSubTypeCatalog.ValidSubTypes
                .Where(subTypeCounts.ContainsKey)
                .Select(s => $"{s}({subTypeCounts[s]})")
                .ToList();
            InvoiceSubTypeSummary = subTypeParts.Count > 0 ? string.Join(", ", subTypeParts) : "None";

            var groups = await Task.Run(() => _repository.GetSubTypeGroupSummaries(_setId));
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
        }

        private void OnGroupCardSaveSucceeded(string message) => RequestInfo?.Invoke(message);
        private void OnGroupCardSaveFailed(string message) => RequestError?.Invoke(message);

        private async Task LoadSetHeaderAsync()
        {
            if (_setId <= 0) return;
            var header = await Task.Run(() => _repository.GetSetHeader(_setId));
            if (header == null) return;

            DocumentNumberEdit = header.DocumentNumber ?? "";
            ReferenceNumberEdit = header.ReferenceNumber ?? "";
            DocumentDateEdit = header.DocumentDate?.Date ?? DateTime.Today;

            _setCode = header.SetCode;
            HeaderTitle = $"{header.SetCode ?? "—"}  ·  {header.DocumentNumber ?? "(no doc #)"}";
            string refDisplay = string.IsNullOrWhiteSpace(header.ReferenceNumber) ? "—" : header.ReferenceNumber;
            HeaderSubtitle = $"Ref: {refDisplay}  |  Date: {header.DocumentDate:MM/dd/yyyy}  |  By: {header.CreatedByUsername}";

            var setDetail = await Task.Run(() => _repository.GetRenewalSetById(_setId));
            if (setDetail != null)
            {
                InvoiceSetType     = setDetail.SetType ?? "—";
                InvoiceCompanyName = setDetail.CompanyName ?? "—";
                InvoiceVendorName  = setDetail.VendorName ?? "—";
                InvoiceStartDateDisplay = setDetail.StartDate?.ToString("MMM d, yyyy") ?? "Not set";
                InvoiceEndDateDisplay   = setDetail.EndDate?.ToString("MMM d, yyyy") ?? "Not set";
                InvoiceItemSummary = $"{setDetail.TotalActiveItems} Active  |  {setDetail.RenewedItemsCount} Renewed  |  {setDetail.ArchivedItemsCount} Archived";
            }
        }

        private async Task LoadChainAsync()
        {
            if (_setId == 0) return;

            _renewalChain = await Task.Run(() => _repository.GetRenewalChain(_setId));

            ChainNodes.Clear();
            for (int i = 0; i < _renewalChain.Count; i++)
            {
                var c = _renewalChain[i];
                ChainNodes.Add(new RenewalChainNodeViewModel
                {
                    SetId          = c.SetId,
                    SetCode        = c.SetCode,
                    DocumentNumber = c.DocumentNumber,
                    RenewalNumber  = c.RenewalNumber,
                    StartDate      = c.StartDate,
                    EndDate        = c.EndDate,
                    IsCurrent      = c.SetId == _setId,
                    IsLast         = i == _renewalChain.Count - 1
                });
            }
        }

        private async Task LoadRenewalHistoryAsync()
        {
            bool isSetLevel = _setId > 0;
            var history = isSetLevel
                ? await Task.Run(() => _repository.GetRenewalHistoryBySetId(_setId))
                : await Task.Run(() => _repository.GetRenewalHistoryByItemId(_itemId));

            HistoryItems.Clear();
            var sorted = history.OrderByDescending(h => h.RenewedDate ?? h.CreatedAt).ToList();

            // The raw RenewalStatus column records how a row came to exist ("Active" = the item's
            // first tracked record, "Renewed" = created by a later renewal action) — it does NOT
            // mean "this period is presently in effect". Per item, the entry with the highest
            // RenewalCount that isn't archived is the one actually covering the item right now;
            // everything else has been superseded even if its own stored status says "Active".
            var latestNonArchivedCountByItem = sorted
                .Where(h => !h.IsArchived)
                .GroupBy(h => h.ItemId)
                .ToDictionary(g => g.Key, g => g.Max(h => h.RenewalCount));

            foreach (var h in sorted)
            {
                bool isCurrent = !h.IsArchived
                    && latestNonArchivedCountByItem.TryGetValue(h.ItemId, out var maxCount)
                    && h.RenewalCount == maxCount;

                HistoryItems.Add(new RenewalHistoryRowViewModel
                {
                    RenewalStatus      = h.RenewalStatus,
                    RenewedDate        = h.RenewedDate,
                    RenewalCount       = h.RenewalCount,
                    NewStartDate       = h.NewStartDate,
                    NewEndDate         = h.NewEndDate,
                    RenewalYears       = h.RenewalYears,
                    RenewalNotes       = h.RenewalNotes,
                    RenewalAmount      = h.RenewalAmount,
                    CreatedByUsername  = h.CreatedByUsername,
                    CreatedAt          = h.CreatedAt,
                    IsArchived         = h.IsArchived,
                    ItemName           = h.ItemName,
                    ItemCode           = h.ItemCode,
                    IsCurrent          = isCurrent
                });
            }
            HasNoHistory = HistoryItems.Count == 0;

            // Renewal Notes is entered once in the "Renew Items" dialog's NOTES box and applied
            // identically to every item renewed together in that same action — a Renewal-level
            // concept, not a per-item one, even though it's physically stored on each item's
            // dbo.Renewals row. Surface the current (non-archived, highest-RenewalCount) note from
            // whichever item has one, rather than repeating it as a column on every item row.
            CurrentRenewalNotes = sorted
                .Where(h => !h.IsArchived
                    && latestNonArchivedCountByItem.TryGetValue(h.ItemId, out var maxCount)
                    && h.RenewalCount == maxCount
                    && !string.IsNullOrWhiteSpace(h.RenewalNotes))
                .Select(h => h.RenewalNotes)
                .FirstOrDefault();
        }

        private void PopulateFields()
        {
            if (_renewalDetail == null) return;

            if (_setId == 0)
            {
                HeaderTitle    = _renewalDetail.ItemName ?? "N/A";
                HeaderSubtitle = _renewalDetail.ItemType ?? "N/A";
            }

            ItemName      = _renewalDetail.ItemName ?? "";
            Description   = _renewalDetail.Description ?? "";
            ItemType      = _renewalDetail.ItemType ?? "";
            CategoryName  = _renewalDetail.CategoryName ?? "";
            SerialNumber  = _renewalDetail.SerialNumber ?? "";
            ModelNumber   = _renewalDetail.ModelNumber ?? "";
            LicenseNumber = _renewalDetail.LicenseNumber ?? "";
            PartNumber    = _renewalDetail.PartNumber ?? "";
            AmountDisplay = _renewalDetail.TotalAmountDue.ToString("N2");

            RenewalStatusText = _renewalDetail.CurrentRenewalStatus ?? "None";
            if (_renewalDetail.StartDate.HasValue) StartDateEdit = _renewalDetail.StartDate.Value;
            if (_renewalDetail.EndDate.HasValue)   EndDateEdit   = _renewalDetail.EndDate.Value;

            RecomputeDaysLeft();
            RecomputeExpiryStatus();

            bool isArchived = !_renewalDetail.Active;
            VendorEditable = !isArchived;
            SiteDisplay = _renewalDetail.SiteDisplay ?? "N/A";

            Subtotal       = _renewalDetail.Subtotal;
            VatAmount      = _renewalDetail.VatAmount;
            WhtAmount      = _renewalDetail.WhtAmount;
            DiscountAmount = _renewalDetail.DiscountAmount;
            TotalAmountDue = _renewalDetail.TotalAmountDue;

            FinancialStripText =
                $"Subtotal: {Subtotal:N2}  |  VAT Amount: {VatAmount:N2}  |  " +
                $"WHT Amount: {WhtAmount:N2}  |  Discount: {DiscountAmount:N2}  |  " +
                $"Total Amount: {TotalAmountDue:N2}";

            IsArchived = isArchived;
            CreatedByText = $"Created By: {_renewalDetail.CreatedByUsername ?? "Unknown"}";
            CreatedAtText = $"Created: {_renewalDetail.DateCreated:yyyy-MM-dd}";
            ArchiveVisible = _renewalDetail.Active;
        }

        private void RecomputeDaysLeft()
        {
            if (_renewalDetail.DaysUntilExpiry.HasValue)
            {
                int d = _renewalDetail.DaysUntilExpiry.Value;
                DaysLeftText = d.ToString();
                DaysLeftColor = d < 0 ? "#E03C31" : d <= 30 ? "#E08A00" : d <= 90 ? "#DAA520" : "#1E9E5E";
            }
            else
            {
                DaysLeftText = "N/A";
                DaysLeftColor = "#8896A7";
            }
        }

        private void RecomputeExpiryStatus()
        {
            if (_renewalDetail == null) return;

            bool hasBeenRenewed = _renewalChain != null
                && _renewalChain.Count > 1
                && _renewalChain.Last().SetId != _setId;

            if (hasBeenRenewed)
            {
                DateTime frozenDate = DateTime.Today;
                int idx = _renewalChain.FindIndex(c => c.SetId == _setId);
                if (idx >= 0 && idx + 1 < _renewalChain.Count && _renewalChain[idx + 1].StartDate.HasValue)
                    frozenDate = _renewalChain[idx + 1].StartDate.Value.Date;
                DateTime? ownEndDate = idx >= 0 ? _renewalChain[idx].EndDate : _renewalDetail.EndDate;
                int daysLeftForRenewed = ownEndDate.HasValue ? (int)(ownEndDate.Value.Date - frozenDate).TotalDays : 0;

                string baseText = daysLeftForRenewed < 0
                    ? $"EXPIRED ({Math.Abs(daysLeftForRenewed)} days ago)"
                    : daysLeftForRenewed <= 30 ? $"EXPIRING IN {daysLeftForRenewed} DAYS"
                    : daysLeftForRenewed <= 90 ? $"Warning: {daysLeftForRenewed} days remaining"
                    : $"Active ({daysLeftForRenewed} days remaining)";

                ExpiryStatusText  = baseText + "  (Renewed)";
                ExpiryStatusColor = "#1A6DD0";
                PausedNoteVisible = true;
                ExpiryWarningText  = "This set has been renewed";
                ExpiryWarningColor = "#1E9E5E";
                return;
            }

            PausedNoteVisible = false;

            if (!_renewalDetail.DaysUntilExpiry.HasValue)
            {
                ExpiryStatusText   = "No End Date Set";
                ExpiryStatusColor  = "#8896A7";
                ExpiryWarningText  = "No End Date Set";
                ExpiryWarningColor = "#8896A7";
                return;
            }

            int daysLeft = _renewalDetail.DaysUntilExpiry.Value;

            if (daysLeft < 0)      { ExpiryStatusText = $"EXPIRED ({Math.Abs(daysLeft)} days ago)"; ExpiryStatusColor = "#E03C31"; }
            else if (daysLeft <= 30) { ExpiryStatusText = $"EXPIRING IN {daysLeft} DAYS"; ExpiryStatusColor = "#E08A00"; }
            else if (daysLeft <= 90) { ExpiryStatusText = $"Warning: {daysLeft} days remaining"; ExpiryStatusColor = "#DAA520"; }
            else                     { ExpiryStatusText = $"Active ({daysLeft} days remaining)"; ExpiryStatusColor = "#1E9E5E"; }

            if (daysLeft < 0)        { ExpiryWarningText = "LICENSE EXPIRED"; ExpiryWarningColor = "#E03C31"; }
            else if (daysLeft < 30)  { ExpiryWarningText = "Less than 1 month before expiry"; ExpiryWarningColor = "#E03C31"; }
            else if (daysLeft < 60)  { ExpiryWarningText = "1-2 months before expiry"; ExpiryWarningColor = "#E08A00"; }
            else if (daysLeft < 90)  { ExpiryWarningText = "2-3 months before expiry"; ExpiryWarningColor = "#DAA520"; }
            else if (daysLeft < 120) { ExpiryWarningText = "3-4 months before expiry"; ExpiryWarningColor = "#D2691E"; }
            else                     { ExpiryWarningText = "More than 4 months before expiry"; ExpiryWarningColor = "#1E9E5E"; }
        }

        // ── Save header (Document #, Reference #, Document Date) ────────────────────
        private async Task SaveHeaderAsync()
        {
            if (_setId <= 0) return;
            try
            {
                string docNum = (DocumentNumberEdit ?? "").Trim();
                string refNum = (ReferenceNumberEdit ?? "").Trim();
                DateTime docDate = DocumentDateEdit.Date;

                await Task.Run(() => _repository.UpdateSetHeader(
                    _setId,
                    string.IsNullOrEmpty(docNum) ? null : docNum,
                    string.IsNullOrEmpty(refNum) ? null : refNum,
                    docDate));

                await LoadSetHeaderAsync();
                RequestInfo?.Invoke("Invoice header saved.");
            }
            catch (Exception ex)
            {
                RequestError?.Invoke($"Failed to save header:\n{ex.Message}");
            }
        }

        // ── Save Start/End Date (new capability — was display-only in the original) ──
        private async Task SaveDatesAsync()
        {
            try
            {
                if (EndDateEdit.Date < StartDateEdit.Date)
                {
                    RequestWarning?.Invoke("End Date cannot be before Start Date.");
                    return;
                }

                await Task.Run(() => _repository.UpdateItemDates(_itemId, StartDateEdit.Date, EndDateEdit.Date));

                _renewalDetail.StartDate = StartDateEdit.Date;
                _renewalDetail.EndDate = EndDateEdit.Date;
                int daysUntilExpiry = (int)(EndDateEdit.Date - DateTime.Today).TotalDays;
                _renewalDetail.DaysUntilExpiry = daysUntilExpiry;
                _renewalDetail.ExpiryStatus = daysUntilExpiry < 0 ? "Expired"
                    : daysUntilExpiry <= 30 ? "Expiring Soon"
                    : daysUntilExpiry <= 90 ? "Warning" : "Active";

                RecomputeDaysLeft();
                RecomputeExpiryStatus();

                RequestInfo?.Invoke("Renewal dates updated.");
            }
            catch (Exception ex)
            {
                RequestError?.Invoke($"Failed to save dates:\n{ex.Message}");
            }
        }

        // ── Save vendor ──────────────────────────────────────────────────────────
        private async Task SaveVendorAsync()
        {
            try
            {
                if (SelectedVendor == null || SelectedVendor.VendorId <= 0)
                {
                    RequestWarning?.Invoke("Please select a Vendor.");
                    return;
                }

                await _vendorRepository.UpdateItemVendorAsync(_itemId, SelectedVendor.VendorId);

                var vendorToUpdate = new VendorDto
                {
                    VendorId    = SelectedVendor.VendorId,
                    VendorName  = SelectedVendor.VendorName ?? string.Empty,
                    Address     = string.IsNullOrWhiteSpace(VendorAddress) ? null : VendorAddress.Trim(),
                    TIN         = string.IsNullOrWhiteSpace(VendorTIN) ? null : VendorTIN.Trim(),
                    IsActive    = SelectedVendor.IsActive,
                    CreatedDate = SelectedVendor.CreatedDate
                };
                await _vendorRepository.UpdateVendorAsync(vendorToUpdate);

                _renewalDetail.VendorId = SelectedVendor.VendorId;
                _renewalDetail.VendorName = vendorToUpdate.VendorName;
                _renewalDetail.VendorAddress = vendorToUpdate.Address;
                _renewalDetail.VendorTIN = vendorToUpdate.TIN;

                RequestInfo?.Invoke("Vendor information updated successfully.");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke($"Failed to save vendor information:\n\n{ex.Message}");
            }
        }

        // ── Renewal Notes ────────────────────────────────────────────────────────
        private async Task SaveRenewalNotesAsync()
        {
            try
            {
                int modifiedBy = SessionContext.CurrentUserId > 0 ? SessionContext.CurrentUserId : 1;

                int rowsUpdated = _setId > 0
                    ? await Task.Run(() => _repository.UpdateCurrentRenewalNotesForSet(_setId, CurrentRenewalNotes, modifiedBy))
                    : await Task.Run(() => _repository.UpdateCurrentRenewalNotesForItem(_itemId, CurrentRenewalNotes, modifiedBy));

                if (rowsUpdated == 0)
                {
                    RequestWarning?.Invoke("Renewal notes can only be saved after this item has been renewed at least once — there's no renewal record to attach them to yet.");
                    return;
                }

                RequestInfo?.Invoke("Renewal notes saved.");
                await LoadRenewalHistoryAsync();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke($"Failed to save renewal notes:\n\n{ex.Message}");
            }
        }

        // ── Renew Items ──────────────────────────────────────────────────────────
        private void RenewItems()
        {
            var renewable = (_allSetItems ?? new List<SetItemRenewalDto>()).Where(x => x.CanRenew).ToList();
            if (renewable.Count == 0)
            {
                RequestInfo?.Invoke("No active items available to renew.");
                return;
            }

            List<ItemCatalogDto> catalog;
            try { catalog = _repository.GetRenewableItemCatalog(); }
            catch (Exception ex)
            {
                RequestError?.Invoke($"Failed to load item catalog:\n{ex.Message}");
                return;
            }

            decimal baseSubtotal = (_renewalDetail?.Subtotal > 0 ? _renewalDetail.Subtotal : _renewalDetail?.TotalAmountDue) ?? 0m;
            decimal baseVatPct   = (_renewalDetail?.Subtotal > 0m) ? Math.Min(100m, Math.Max(0m, decimal.Round((_renewalDetail.VatAmount / _renewalDetail.Subtotal) * 100m, 2))) : 0m;
            decimal baseWhtPct   = (_renewalDetail?.Subtotal > 0m) ? Math.Min(100m, Math.Max(0m, decimal.Round((_renewalDetail.WhtAmount / _renewalDetail.Subtotal) * 100m, 2))) : 0m;
            decimal baseDiscountPct = (_renewalDetail?.Subtotal > 0m) ? Math.Min(100m, Math.Max(0m, decimal.Round(((_renewalDetail?.DiscountAmount ?? 0m) / _renewalDetail.Subtotal) * 100m, 2))) : 0m;
            decimal originalTotal = _renewalDetail?.TotalAmountDue ?? 0m;

            string title = renewable.Count == 1
                ? $"Renew Item — {renewable[0].Description}"
                : $"Renew Items ({renewable.Count})";

            RequestRenewItems?.Invoke(new RenewItemsRequest
            {
                SetId = _setId,
                SetCode = _setCode,
                Title = title,
                Items = renewable,
                Catalog = catalog,
                BaseSubtotal = baseSubtotal,
                BaseVatPct = baseVatPct,
                BaseWhtPct = baseWhtPct,
                BaseDiscountPct = baseDiscountPct,
                OriginalTotal = originalTotal
            });
        }

        /// <summary>Called by the View after RenewItemsWindow closes successfully.</summary>
        public async Task ReloadAfterRenewItemsAsync()
        {
            await LoadSetItemsAsync();
            await LoadChainAsync();
            await LoadRenewalHistoryAsync();
        }

        // ── Archive ──────────────────────────────────────────────────────────────
        private async Task ArchiveAsync()
        {
            try
            {
                if (_renewalDetail == null || !_renewalDetail.Active)
                {
                    RequestInfo?.Invoke("This renewal is already archived.");
                    return;
                }

                var history = await Task.Run(() => _repository.GetRenewalHistoryByItemId(_itemId));
                var latestRenewal = history.OrderByDescending(h => h.CreatedAt).FirstOrDefault();

                if (latestRenewal == null)
                {
                    await Task.Run(() => _repository.CreateRenewal(
                        itemId: _itemId,
                        renewalStatus: "On Hold",
                        newStartDate: _renewalDetail.StartDate ?? DateTime.Now,
                        newEndDate: _renewalDetail.EndDate ?? DateTime.Now.AddYears(1),
                        renewalYears: 0,
                        renewalAmount: _renewalDetail.TotalAmountDue,
                        renewalNotes: "Initial renewal record created for archiving",
                        createdBy: SessionContext.CurrentUserId > 0 ? SessionContext.CurrentUserId : 1,
                        setIdOverride: _setId));

                    history = await Task.Run(() => _repository.GetRenewalHistoryByItemId(_itemId));
                    latestRenewal = history.OrderByDescending(h => h.CreatedAt).FirstOrDefault();
                }

                bool confirmed = ConfirmYesNo?.Invoke(
                    "Confirm Archive",
                    $"Archive this renewal and item?\n\n" +
                    $"Item: {_renewalDetail.ItemName}\n" +
                    $"Type: {_renewalDetail.ItemType}\n\n" +
                    $"This will:\n" +
                    $"- Archive the renewal record\n" +
                    $"- Archive the item\n" +
                    $"- Mark the item as inactive\n\n" +
                    $"This action creates archive entries but can be restored later.\n\n" +
                    $"Continue?") ?? false;

                if (!confirmed) return;

                string archiveReason = RequestPrompt?.Invoke(
                    "Please enter the reason for archiving:", "Archive Reason", "Manual archive from Renewal Detail page");

                if (string.IsNullOrWhiteSpace(archiveReason))
                {
                    RequestWarning?.Invoke("Archive reason is required.");
                    return;
                }

                await Task.Run(() => _repository.ArchiveRenewal(
                    latestRenewal.RenewalId, _itemId, archiveReason, SessionContext.CurrentUserName ?? "Unknown"));

                RequestInfo?.Invoke("Renewal and item archived successfully.");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke($"Failed to archive renewal:\n\n{ex.Message}");
            }
        }
    }
}
