using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Renewal.RenewItems.ViewModels
{
    /// <summary>
    /// WPF replacement for the WinForms RenewalDialog (Pages/Renewal/ViewRenewalDetailPage.cs).
    /// Lets the user renew one or more SetItems into a new Set, each row independently
    /// editable (Description/Quantity/UnitPrice/Start/End Date), with an optional
    /// per-row replacement Item picked from the renewable-item catalog.
    /// </summary>
    public class RenewItemsViewModel : ViewModelBase
    {
        private static readonly ItemCatalogDto KeepOriginalSentinel =
            new ItemCatalogDto { ItemId = 0, Name = "— Keep Original / No Replacement —" };

        private readonly RenewalRepository _repository = new RenewalRepository();
        private readonly int _originalSetId;
        private readonly List<ItemCatalogDto> _allCatalog;
        private readonly Dictionary<int, ItemCatalogDto> _replacementMap = new Dictionary<int, ItemCatalogDto>();
        private readonly decimal _originalTotal;
        private bool _isConfirming;

        /// <summary>SET-#### the Original Total figures were pulled from — null if the caller
        /// didn't have one to pass (e.g. the standalone single-item renewal path).</summary>
        public string OriginalSetCode { get; }

        public string OriginalSetCodeNote =>
            string.IsNullOrWhiteSpace(OriginalSetCode) ? "" : $"  (from {OriginalSetCode})";

        private string _newSetCodePreviewText = "";
        /// <summary>Best-effort "this will become SET-####" hint for the footer — see
        /// RenewalRepository.PreviewNextSetCode. Loaded asynchronously since it's a DB round trip.</summary>
        public string NewSetCodePreviewText
        {
            get => _newSetCodePreviewText;
            set => SetField(ref _newSetCodePreviewText, value);
        }

        public event Action<bool> CloseRequested;
        public event Action<string, string> RequestWarning;
        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestError;

        /// <summary>View opens ReceiptSetViewerWindow(setId) directly (pure WPF, no WinForms
        /// bridge needed) so the user can attach the new physical renewal document right away.</summary>
        public event Action<int> RequestAttachReceipt;

        /// <summary>View opens BatchAddItemDialog for when the item to add doesn't exist in the
        /// catalog yet at all. On success the view calls back into RefreshCatalogAfterNewItem().</summary>
        public event Action RequestAddNewCatalogItem;

        /// <summary>View shows a Yes/No prompt and returns the user's choice.</summary>
        public Func<string, string, bool> ConfirmYesNo { get; set; }

        public string Title { get; }

        public ObservableCollection<RenewItemRow> Rows { get; } = new ObservableCollection<RenewItemRow>();

        // ── New Invoice Set Details ─────────────────────────────────────────
        public string NewDocumentNumber { get; set; }
        public string NewReferenceNumber { get; set; }
        public DateTime NewDocumentDate { get; set; } = DateTime.Today;

        // ── Replacement Item picker ─────────────────────────────────────────
        public ObservableCollection<ItemCatalogDto> FilteredCatalog { get; } = new ObservableCollection<ItemCatalogDto>();

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyCatalogFilter(); }
        }

        private ItemCatalogDto _selectedCatalogItem;
        public ItemCatalogDto SelectedCatalogItem
        {
            get => _selectedCatalogItem;
            set => SetField(ref _selectedCatalogItem, value);
        }

        private RenewItemRow _selectedRow;
        public RenewItemRow SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (SetField(ref _selectedRow, value))
                {
                    OnPropertyChanged(nameof(AssigningLabel));
                    // Restore the catalog selection for this row if it already has a replacement,
                    // otherwise show "Keep Original" as selected rather than nothing.
                    if (value != null && _replacementMap.TryGetValue(value.SetItemId, out var current))
                        SelectedCatalogItem = _allCatalog.FirstOrDefault(c => c.ItemId == current.ItemId);
                    else
                        SelectedCatalogItem = value != null ? KeepOriginalSentinel : null;
                }
            }
        }

        public string AssigningLabel => SelectedRow == null
            ? "← Select a row above to assign a replacement item"
            : $"Assigning replacement for:  {SelectedRow.ItemCode}  —  {SelectedRow.Description}";

        // ── "+ Add Item" popup — a separate search/selection state from the replacement
        // picker above so opening it never disturbs whatever the user has selected there. ──
        private bool _isAddItemPopupOpen;
        public bool IsAddItemPopupOpen
        {
            get => _isAddItemPopupOpen;
            set => SetField(ref _isAddItemPopupOpen, value);
        }

        public ObservableCollection<ItemCatalogDto> AddItemFilteredCatalog { get; } = new ObservableCollection<ItemCatalogDto>();

        private string _addItemSearchText = string.Empty;
        public string AddItemSearchText
        {
            get => _addItemSearchText;
            set { if (SetField(ref _addItemSearchText, value)) ApplyAddItemCatalogFilter(); }
        }

        private ItemCatalogDto _selectedAddCatalogItem;
        public ItemCatalogDto SelectedAddCatalogItem
        {
            get => _selectedAddCatalogItem;
            set => SetField(ref _selectedAddCatalogItem, value);
        }

        // ── Renewal Settings ─────────────────────────────────────────────────
        public List<string> StatusOptions { get; } = new List<string> { "None", "On Hold", "Renewed" };
        public string SelectedStatus { get; set; } = "Renewed";

        private DateTime _newStartDate = DateTime.Today;
        public DateTime NewStartDate
        {
            get => _newStartDate;
            set { if (SetField(ref _newStartDate, value)) NewEndDate = _newStartDate.AddYears(RenewalYears); }
        }

        private int _renewalYears = 1;
        public int RenewalYears
        {
            get => _renewalYears;
            set { if (SetField(ref _renewalYears, value)) NewEndDate = NewStartDate.AddYears(_renewalYears); }
        }

        // Directly selectable — auto-filled from Start Date + Years as a convenience default,
        // but the user can freely override it afterward via the DatePicker.
        private DateTime _newEndDate = DateTime.Today.AddYears(1);
        public DateTime NewEndDate
        {
            get => _newEndDate;
            set => SetField(ref _newEndDate, value);
        }

        public ICommand ApplyToCheckedCommand { get; }

        // ── Sub-Type Group (applies to checked rows) ────────────────────────
        public List<string> SubTypeOptions { get; } =
            new List<string> { "(None)" }.Concat(ItemSubTypeCatalog.ValidSubTypes).ToList();

        private string _groupSubType = "(None)";
        public string GroupSubType
        {
            get => _groupSubType;
            set
            {
                if (SetField(ref _groupSubType, value))
                {
                    OnPropertyChanged(nameof(IsGroupPanelEnabled));
                    OnPropertyChanged(nameof(GroupReferenceCodeLabel));
                    OnPropertyChanged(nameof(IsGroupDetailEditable));
                    SelectedExistingGroup = null;
                    RefreshExistingGroupOptions();
                }
            }
        }

        public bool IsGroupPanelEnabled => !string.IsNullOrEmpty(GroupSubType) && GroupSubType != "(None)";

        public string GroupReferenceCodeLabel =>
            ItemSubTypeCatalog.GetReferenceCodeLabel(IsGroupPanelEnabled ? GroupSubType : null);

        public ObservableCollection<RowGroupOption> ExistingRowGroups { get; } = new ObservableCollection<RowGroupOption>();

        private RowGroupOption _selectedExistingGroup;
        public RowGroupOption SelectedExistingGroup
        {
            get => _selectedExistingGroup;
            set
            {
                if (SetField(ref _selectedExistingGroup, value))
                {
                    if (value != null && !value.IsNewGroup)
                    {
                        GroupReferenceCode = value.ReferenceCode;
                        GroupBeginDate = value.BeginDate;
                        GroupEndDate = value.EndDate;
                    }
                    OnPropertyChanged(nameof(IsGroupDetailEditable));
                }
            }
        }

        public bool IsGroupDetailEditable =>
            IsGroupPanelEnabled && (SelectedExistingGroup == null || SelectedExistingGroup.IsNewGroup);

        private string _groupReferenceCode;
        public string GroupReferenceCode
        {
            get => _groupReferenceCode;
            set => SetField(ref _groupReferenceCode, value);
        }

        private DateTime? _groupBeginDate = DateTime.Today;
        public DateTime? GroupBeginDate
        {
            get => _groupBeginDate;
            set => SetField(ref _groupBeginDate, value);
        }

        private DateTime? _groupEndDate = DateTime.Today.AddYears(1);
        public DateTime? GroupEndDate
        {
            get => _groupEndDate;
            set => SetField(ref _groupEndDate, value);
        }

        public ICommand ApplyGroupToCheckedCommand { get; }

        /// <summary>Rebuilds the "existing group" picker from distinct SubType+ReferenceCode
        /// combinations already present on the in-memory Rows (carried forward from the
        /// original items, or previously assigned via this panel). There is no persisted
        /// dbo.SetItemSubTypeGroup to query yet — the new renewal Set doesn't exist until
        /// Confirm — so the row collection itself is the source of truth here.</summary>
        private void RefreshExistingGroupOptions()
        {
            ExistingRowGroups.Clear();
            ExistingRowGroups.Add(new RowGroupOption { IsNewGroup = true });

            if (!IsGroupPanelEnabled) return;

            var distinct = Rows
                .Where(r => r.SubType == GroupSubType)
                .Select(r => new { r.ReferenceCode, r.GroupBeginDate, r.GroupEndDate })
                .Distinct()
                .ToList();

            foreach (var g in distinct)
            {
                ExistingRowGroups.Add(new RowGroupOption
                {
                    IsNewGroup = false,
                    SubType = GroupSubType,
                    ReferenceCode = g.ReferenceCode,
                    BeginDate = g.GroupBeginDate,
                    EndDate = g.GroupEndDate
                });
            }
        }

        // ── Parent Tag Group (applies to checked rows, same button as Sub-Type Group) ────────
        // Free-text, per-invoice label (e.g. "HX Cluster with 40% Storage Buffer") -- no fixed
        // catalog, no coverage dates, independent of Sub-Type grouping (a row can carry both).
        // Same "staged in-memory until Confirm" pattern as the Sub-Type panel above: the new
        // renewal Set doesn't exist yet, so there is no dbo.SetItemParentTagGroup row to
        // read/write against until ConfirmAsync actually creates the Set.
        private string _parentTagInput = string.Empty;
        public string ParentTagInput
        {
            get => _parentTagInput;
            set => SetField(ref _parentTagInput, value);
        }

        public ObservableCollection<ParentTagRowGroupOption> ExistingParentTagRowGroups { get; } = new ObservableCollection<ParentTagRowGroupOption>();

        private ParentTagRowGroupOption _selectedExistingParentTagGroup;
        public ParentTagRowGroupOption SelectedExistingParentTagGroup
        {
            get => _selectedExistingParentTagGroup;
            set
            {
                if (SetField(ref _selectedExistingParentTagGroup, value))
                {
                    if (value != null && !value.IsNewGroup)
                        ParentTagInput = value.Label;
                    OnPropertyChanged(nameof(IsParentTagEditable));
                }
            }
        }

        public bool IsParentTagEditable => SelectedExistingParentTagGroup == null || SelectedExistingParentTagGroup.IsNewGroup;

        /// <summary>Rebuilds the "existing group" picker from distinct Parent Tag labels
        /// already present on the in-memory Rows, mirroring RefreshExistingGroupOptions.</summary>
        private void RefreshExistingParentTagGroupOptions()
        {
            ExistingParentTagRowGroups.Clear();
            ExistingParentTagRowGroups.Add(new ParentTagRowGroupOption { IsNewGroup = true });

            var distinct = Rows
                .Where(r => !string.IsNullOrWhiteSpace(r.ParentTag))
                .Select(r => r.ParentTag)
                .Distinct()
                .ToList();

            foreach (var label in distinct)
                ExistingParentTagRowGroups.Add(new ParentTagRowGroupOption { IsNewGroup = false, Label = label });
        }

        // ── Sub-Type Group financial summary cards ──────────────────────────
        // Live, in-memory equivalent of ViewInvoiceDetailPage's group cards — since the new
        // renewal Set doesn't exist until Confirm, there is no dbo.SetItemSubTypeGroup row to
        // read/save against. Each card's Subtotal always tracks the live sum of its member
        // rows' Amount; VAT%/WHT%/Discount% are freely editable and preserved across rebuilds.
        public ObservableCollection<RenewItemGroupSummaryCardViewModel> GroupSummaryCards { get; } =
            new ObservableCollection<RenewItemGroupSummaryCardViewModel>();

        private bool _hasGroupSummaryCards;
        public bool HasGroupSummaryCards
        {
            get => _hasGroupSummaryCards;
            set => SetField(ref _hasGroupSummaryCards, value);
        }

        /// <summary>Rebuilds GroupSummaryCards from the checked rows' current SubType/
        /// ReferenceCode/date assignments and Amounts. Existing cards for a group that still
        /// exists are reused in place (Subtotal refreshed, VAT/WHT/Discount% preserved) rather
        /// than recreated, so editing one row's Qty doesn't wipe out percentages the user
        /// already typed into an unrelated group's card.</summary>
        private void RefreshGroupSummaryCards()
        {
            var groups = Rows
                .Where(r => r.Selected && !string.IsNullOrWhiteSpace(r.SubType))
                .GroupBy(r => (r.SubType, r.ReferenceCode, r.GroupBeginDate, r.GroupEndDate))
                .ToList();

            var existingByKey = GroupSummaryCards.ToDictionary(c => (c.SubType, c.ReferenceCode, c.BeginDate, c.EndDate));

            foreach (var card in GroupSummaryCards)
                card.TotalChanged -= OnGroupCardTotalChanged;

            var newCards = new List<RenewItemGroupSummaryCardViewModel>();
            foreach (var g in groups)
            {
                decimal subtotal = g.Sum(r => r.Amount);

                if (existingByKey.TryGetValue(g.Key, out var existing))
                {
                    // Keep OriginalSubtotal current, but never clobber a Subtotal the user has
                    // deliberately typed in (a new renewal price) — that's what Revert is for.
                    existing.OriginalSubtotal = subtotal;
                    newCards.Add(existing);
                }
                else
                {
                    newCards.Add(new RenewItemGroupSummaryCardViewModel(
                        g.Key.SubType, g.Key.ReferenceCode, g.Key.GroupBeginDate, g.Key.GroupEndDate,
                        subtotal, VatPercent, WhtPercent, DiscountPercent));
                }
            }

            GroupSummaryCards.Clear();
            foreach (var card in newCards)
            {
                card.TotalChanged += OnGroupCardTotalChanged;
                GroupSummaryCards.Add(card);
            }

            HasGroupSummaryCards = GroupSummaryCards.Count > 0;
            UpdateSubtotalFromGroups();
        }

        private void OnGroupCardTotalChanged() => UpdateSubtotalFromGroups();

        /// <summary>Header Subtotal = sum of every group card's own Total, plus the raw Amount
        /// of any checked row that isn't in a Sub-Type Group at all. No-op when there are no
        /// group cards — Subtotal then stays whatever the user typed directly.</summary>
        private void UpdateSubtotalFromGroups()
        {
            if (GroupSummaryCards.Count == 0) return;

            decimal ungroupedTotal = Rows
                .Where(r => r.Selected && string.IsNullOrWhiteSpace(r.SubType))
                .Sum(r => r.Amount);

            Subtotal = GroupSummaryCards.Sum(c => c.Total) + ungroupedTotal;
        }

        /// <summary>Wires a row so checking/unchecking it, editing its SubType/ReferenceCode/
        /// group dates, or its Amount (Qty/UnitPrice) keeps GroupSummaryCards and the header
        /// Subtotal live.</summary>
        private void AttachRowWatcher(RenewItemRow row)
        {
            row.PropertyChanged += RowPropertyChangedForGroups;
        }

        private void RowPropertyChangedForGroups(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(RenewItemRow.Selected):
                case nameof(RenewItemRow.SubType):
                case nameof(RenewItemRow.ReferenceCode):
                case nameof(RenewItemRow.GroupBeginDate):
                case nameof(RenewItemRow.GroupEndDate):
                case nameof(RenewItemRow.Amount):
                    RefreshGroupSummaryCards();
                    break;
            }
        }

        /// <summary>Applies both Sub-Type Group and Parent Tag Group to the checked rows in one
        /// step — the two are independent axes (a row can carry both, either, or neither), but
        /// the user works them from a single merged panel/button rather than two.</summary>
        private void ApplyGroupToChecked()
        {
            var checkedRows = Rows.Where(r => r.Selected).ToList();
            if (checkedRows.Count == 0)
            {
                RequestInfo?.Invoke("No Rows Checked", "Check at least one row above before applying a group.");
                return;
            }

            bool grouped = IsGroupPanelEnabled;
            string refCode = string.IsNullOrWhiteSpace(GroupReferenceCode) ? null : GroupReferenceCode.Trim();
            string parentTag = string.IsNullOrWhiteSpace(ParentTagInput) ? null : ParentTagInput.Trim();

            foreach (var row in checkedRows)
            {
                row.SubType = grouped ? GroupSubType : null;
                row.ReferenceCode = grouped ? refCode : null;
                row.GroupBeginDate = grouped ? GroupBeginDate : null;
                row.GroupEndDate = grouped ? GroupEndDate : null;

                row.ParentTag = parentTag;
            }

            RefreshExistingGroupOptions();
            RefreshExistingParentTagGroupOptions();
        }

        // ── Financial Calculator ─────────────────────────────────────────────
        public decimal OriginalTotal => _originalTotal;

        private decimal _subtotal;
        public decimal Subtotal
        {
            get => _subtotal;
            set
            {
                if (SetField(ref _subtotal, value))
                {
                    OnPropertyChanged(nameof(DiscountAmount));
                    OnPropertyChanged(nameof(NetAfterDiscount));
                    OnPropertyChanged(nameof(VatAmount));
                    OnPropertyChanged(nameof(WhtAmount));
                    OnPropertyChanged(nameof(TotalAmountDue));
                    OnPropertyChanged(nameof(ComparisonText));
                }
            }
        }

        private decimal _vatPercent;
        public decimal VatPercent
        {
            get => _vatPercent;
            set
            {
                if (SetField(ref _vatPercent, value))
                {
                    OnPropertyChanged(nameof(VatAmount));
                    OnPropertyChanged(nameof(TotalAmountDue));
                    OnPropertyChanged(nameof(ComparisonText));
                }
            }
        }

        private decimal _whtPercent;
        public decimal WhtPercent
        {
            get => _whtPercent;
            set
            {
                if (SetField(ref _whtPercent, value))
                {
                    OnPropertyChanged(nameof(WhtAmount));
                    OnPropertyChanged(nameof(TotalAmountDue));
                    OnPropertyChanged(nameof(ComparisonText));
                }
            }
        }

        private decimal _discountPercent;
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (SetField(ref _discountPercent, value))
                {
                    OnPropertyChanged(nameof(DiscountAmount));
                    OnPropertyChanged(nameof(NetAfterDiscount));
                    OnPropertyChanged(nameof(VatAmount));
                    OnPropertyChanged(nameof(WhtAmount));
                    OnPropertyChanged(nameof(TotalAmountDue));
                    OnPropertyChanged(nameof(ComparisonText));
                }
            }
        }

        // Matches ViewInvoiceDetailPage.TryCalculateFinancialsFromPercentages exactly: Subtotal
        // is net-of-VAT; Discount is a percentage of Subtotal taken off first; VAT and WHT are
        // then both computed on the post-discount base; VAT is added back in and WHT subtracted.
        public decimal DiscountAmount    => Subtotal * (DiscountPercent / 100m);
        public decimal NetAfterDiscount  => Subtotal - DiscountAmount;
        public decimal VatAmount         => NetAfterDiscount * (VatPercent / 100m);
        public decimal WhtAmount         => NetAfterDiscount * (WhtPercent / 100m);
        public decimal TotalAmountDue    => NetAfterDiscount + VatAmount - WhtAmount;

        public string ComparisonText
        {
            get
            {
                decimal diff = TotalAmountDue - _originalTotal;
                if (Math.Abs(diff) < 0.01m) return "Same as original amount";
                return diff > 0
                    ? $"Increase by {diff:N2} from original"
                    : $"Decrease by {Math.Abs(diff):N2} from original";
            }
        }

        // ── Notes ────────────────────────────────────────────────────────────
        public string Notes { get; set; }

        // ── Commands ─────────────────────────────────────────────────────────
        public ICommand AssignReplacementCommand { get; }
        public ICommand ClearReplacementCommand { get; }
        public ICommand OpenAddItemPopupCommand { get; }
        public ICommand CancelAddItemPopupCommand { get; }
        public ICommand ConfirmAddItemCommand { get; }
        public ICommand OpenBatchAddItemCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public RenewItemsViewModel(
            int originalSetId,
            string title,
            List<SetItemRenewalDto> items,
            List<ItemCatalogDto> catalog,
            decimal baseSubtotal, decimal baseVatPct, decimal baseWhtPct,
            decimal baseDiscountPct, decimal originalTotal,
            string originalSetCode = null)
        {
            _originalSetId = originalSetId;
            Title = title;
            _allCatalog = catalog ?? new List<ItemCatalogDto>();
            _originalTotal = originalTotal;
            OriginalSetCode = originalSetCode;

            foreach (var item in items ?? new List<SetItemRenewalDto>())
            {
                var row = new RenewItemRow(item);
                AttachRowWatcher(row);
                Rows.Add(row);
            }

            _subtotal = baseSubtotal;
            _vatPercent = baseVatPct;
            _whtPercent = baseWhtPct;
            _discountPercent = baseDiscountPct;

            ApplyCatalogFilter();

            ApplyToCheckedCommand = new RelayCommand(ApplyToChecked);
            ApplyGroupToCheckedCommand = new RelayCommand(ApplyGroupToChecked);
            RefreshExistingGroupOptions();
            RefreshExistingParentTagGroupOptions();
            RefreshGroupSummaryCards();
            AssignReplacementCommand = new RelayCommand(AssignReplacement);
            ClearReplacementCommand = new RelayCommand(ClearReplacement);
            OpenAddItemPopupCommand = new RelayCommand(OpenAddItemPopup);
            CancelAddItemPopupCommand = new RelayCommand(() => IsAddItemPopupOpen = false);
            ConfirmAddItemCommand = new RelayCommand(ConfirmAddItem);
            OpenBatchAddItemCommand = new RelayCommand(() => RequestAddNewCatalogItem?.Invoke());
            ConfirmCommand = new RelayCommand(() => _ = ConfirmAsync());
            CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));

            _ = LoadNewSetCodePreviewAsync();
        }

        /// <summary>Best-effort footer hint — fire-and-forget since it's just informational and
        /// the window is fully usable before it resolves.</summary>
        private async Task LoadNewSetCodePreviewAsync()
        {
            try
            {
                string code = await Task.Run(() => _repository.PreviewNextSetCode());
                NewSetCodePreviewText = $"If this Set is renewed, it will be {code}.";
            }
            catch
            {
                // Purely informational — say nothing rather than surface an error dialog for it.
            }
        }

        private void ApplyCatalogFilter()
        {
            var q = (SearchText ?? string.Empty).Trim().ToLower();

            FilteredCatalog.Clear();
            // Always offer "Keep Original" first, regardless of search text, so it's never
            // filtered out of reach.
            FilteredCatalog.Add(KeepOriginalSentinel);

            // Don't dump the entire catalog into the list until the user actually searches —
            // with nothing typed yet, "Keep Original" is the only sensible default to show.
            if (string.IsNullOrEmpty(q)) return;

            var filtered = _allCatalog.Where(x =>
                (x.Name ?? "").ToLower().Contains(q) ||
                (x.ItemCode ?? "").ToLower().Contains(q));
            foreach (var c in filtered)
                FilteredCatalog.Add(c);
        }

        private void ApplyAddItemCatalogFilter()
        {
            var q = (AddItemSearchText ?? string.Empty).Trim().ToLower();
            var filtered = string.IsNullOrEmpty(q)
                ? _allCatalog
                : _allCatalog.Where(x =>
                    (x.Name ?? "").ToLower().Contains(q) ||
                    (x.ItemCode ?? "").ToLower().Contains(q));

            AddItemFilteredCatalog.Clear();
            // No "Keep Original" sentinel here — every entry is a real item that can be added.
            foreach (var c in filtered)
                AddItemFilteredCatalog.Add(c);
        }

        /// <summary>Called by the view after BatchAddItemDialog closes successfully, so a
        /// just-created item is immediately searchable/selectable without reopening this window.</summary>
        public void RefreshCatalogAfterNewItem()
        {
            _allCatalog.Clear();
            _allCatalog.AddRange(_repository.GetRenewableItemCatalog());
            ApplyCatalogFilter();
            ApplyAddItemCatalogFilter();
            IsAddItemPopupOpen = true;
        }

        private void ApplyToChecked()
        {
            DateTime newStart = NewStartDate.Date;
            DateTime newEnd = NewEndDate.Date;
            foreach (var row in Rows.Where(r => r.Selected))
            {
                row.StartDate = newStart;
                row.EndDate = newEnd;
            }
        }

        private void AssignReplacement()
        {
            if (SelectedRow == null)
            {
                RequestInfo?.Invoke("No Row Selected", "Select a row first.");
                return;
            }
            if (SelectedCatalogItem == null)
            {
                RequestInfo?.Invoke("No Item Selected", "Select a replacement item from the list first.");
                return;
            }

            if (SelectedCatalogItem.ItemId == 0)
            {
                ClearReplacement();
                return;
            }

            var cat = SelectedCatalogItem;
            _replacementMap[SelectedRow.SetItemId] = cat;
            SelectedRow.ItemCode = cat.ItemCode ?? "";
            SelectedRow.Description = cat.Name ?? "";
            SelectedRow.UnitOfMeasure = cat.UnitOfMeasure ?? "";
            SelectedRow.UnitPrice = cat.UnitPrice;
            SelectedRow.ReplacementDisplay = $"{cat.ItemCode}  —  {cat.Name}";
        }

        private void ClearReplacement()
        {
            if (SelectedRow == null) return;
            _replacementMap.Remove(SelectedRow.SetItemId);
            SelectedRow.ItemCode = SelectedRow.OriginalItemCode ?? "";
            SelectedRow.Description = SelectedRow.OriginalDescription ?? "";
            SelectedRow.UnitOfMeasure = SelectedRow.OriginalUnitOfMeasure ?? "";
            SelectedRow.UnitPrice = SelectedRow.OriginalUnitPrice;
            SelectedRow.ReplacementDisplay = "— Original —";
            SelectedCatalogItem = KeepOriginalSentinel;
        }

        private void OpenAddItemPopup()
        {
            AddItemSearchText = string.Empty;
            SelectedAddCatalogItem = null;
            ApplyAddItemCatalogFilter();
            IsAddItemPopupOpen = true;
        }

        private void ConfirmAddItem()
        {
            if (SelectedAddCatalogItem == null)
            {
                RequestInfo?.Invoke("No Item Selected", "Search for and select the item to add first.");
                return;
            }

            var row = new RenewItemRow(SelectedAddCatalogItem, NewStartDate, NewEndDate);
            AttachRowWatcher(row);
            Rows.Add(row);
            SelectedRow = row;
            IsAddItemPopupOpen = false;
        }

        private async Task ConfirmAsync()
        {
            if (_isConfirming) return;

            var checkedItems = new List<SetItemRenewalDto>();
            var uncheckedExpired = new List<SetItemRenewalDto>();

            foreach (var row in Rows)
            {
                var dto = new SetItemRenewalDto
                {
                    SetItemId = row.SetItemId,
                    ItemId = row.OriginalItemId,
                    ItemCode = row.ItemCode,
                    Description = row.Description,
                    Quantity = row.Quantity,
                    UnitOfMeasure = row.UnitOfMeasure,
                    UnitPrice = row.UnitPrice,
                    Amount = row.Amount,
                    LineStartDate = row.StartDate,
                    LineEndDate = row.EndDate,
                    SubType = row.SubType,
                    ReferenceCode = row.ReferenceCode,
                    BeginDate = row.GroupBeginDate,
                    EndDate = row.GroupEndDate,
                    ParentTag = row.ParentTag
                };

                if (row.Selected)
                    checkedItems.Add(dto);
                else if (row.OriginalLineEndDate.HasValue && row.OriginalLineEndDate.Value.Date < DateTime.Today)
                    uncheckedExpired.Add(dto);
            }

            if (checkedItems.Count == 0 && uncheckedExpired.Count == 0)
            {
                RequestWarning?.Invoke("Nothing to Do", "No items selected and no expired items to archive.");
                return;
            }

            // A checked item whose End Date is still in the past almost always means its row
            // never actually received the new dates — e.g. it was checked after "Apply to
            // Checked" already ran, or its date cells were never edited. Confirming as-is creates
            // a renewal record that reads as "just renewed" but is already expired, which is
            // confusing after the fact — catch it here instead.
            var staleCheckedItems = checkedItems.Where(x => x.LineEndDate.HasValue && x.LineEndDate.Value.Date < DateTime.Today).ToList();
            if (staleCheckedItems.Count > 0)
            {
                string list = string.Join("\n", staleCheckedItems.Select(x => $"  • {x.Description} (ends {x.LineEndDate:MM/dd/yyyy})"));
                bool proceed = ConfirmYesNo?.Invoke(
                    "End Date Still in the Past",
                    $"{staleCheckedItems.Count} checked item(s) still have an End Date before today, so this renewal " +
                    $"would be created already expired:\n\n{list}\n\n" +
                    "This usually means the row's dates were never updated to the new period. " +
                    "Continue anyway?") ?? false;
                if (!proceed) return;
            }

            var itemReplacements = new Dictionary<int, ItemCatalogDto>(_replacementMap);
            string renewalNotes = (Notes ?? string.Empty).Trim();
            int userId = AppSession.CurrentUserId;

            _isConfirming = true;
            try
            {
                // The new Set's own header Start/End Date spans whatever range its checked line
                // items cover, since each row can now have its own independent dates.
                DateTime headerStart = checkedItems.Min(x => x.LineStartDate ?? DateTime.Today);
                DateTime headerEnd = checkedItems.Max(x => x.LineEndDate ?? DateTime.Today.AddYears(1));

                int newSetId = await Task.Run(() => _repository.CreateRenewalSet(
                    _originalSetId,
                    userId,
                    NewDocumentNumber,
                    NewReferenceNumber,
                    NewDocumentDate,
                    headerStart,
                    headerEnd,
                    Subtotal,
                    VatAmount,
                    WhtAmount,
                    DiscountAmount,
                    TotalAmountDue));

                int successCount = 0;
                var errors = new List<string>();

                foreach (var item in checkedItems)
                {
                    try
                    {
                        DateTime lineStart = item.LineStartDate ?? DateTime.Today;
                        DateTime lineEnd = item.LineEndDate ?? lineStart.AddYears(1);

                        // Rows added via "Add Item" have no predecessor SetItem to renew — they
                        // go straight into the new Set as a fresh line instead.
                        if (item.SetItemId <= 0)
                        {
                            await Task.Run(() => _repository.AddNewSetItem(
                                newSetId,
                                item.ItemId,
                                item.ItemCode,
                                item.Description,
                                item.Quantity,
                                item.UnitOfMeasure,
                                item.UnitPrice,
                                lineStart,
                                lineEnd,
                                userId,
                                item.SubType,
                                item.ReferenceCode,
                                item.BeginDate,
                                item.EndDate,
                                item.ParentTag));
                            successCount++;
                            continue;
                        }

                        itemReplacements.TryGetValue(item.SetItemId, out var replacement);
                        int years = Math.Max(1, (int)Math.Round((lineEnd - lineStart).Days / 365.25, MidpointRounding.AwayFromZero));

                        await Task.Run(() => _repository.RenewSingleItem(
                            item.SetItemId,
                            item.Description,
                            lineStart,
                            lineEnd,
                            userId,
                            replacement?.ItemId,
                            replacement?.ItemCode ?? item.ItemCode,
                            replacement?.UnitPrice ?? item.UnitPrice,
                            item.Quantity,
                            replacement?.UnitOfMeasure ?? item.UnitOfMeasure,
                            years,
                            item.UnitPrice * item.Quantity,
                            renewalNotes,
                            targetSetId: newSetId,
                            subType: item.SubType,
                            referenceCode: item.ReferenceCode,
                            groupBeginDate: item.BeginDate,
                            groupEndDate: item.EndDate,
                            parentTag: item.ParentTag));
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{item.Description}: {ex.Message}");
                    }
                }

                foreach (var item in uncheckedExpired)
                {
                    try
                    {
                        await Task.Run(() => _repository.ArchiveSetItem(item.SetItemId, userId, "Left unchecked during renewal — already past its end date"));
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{item.Description} (auto-archive): {ex.Message}");
                    }
                }

                // Persist each Sub-Type Group Summary card's Subtotal/VAT/WHT/Discount onto the
                // new Set's own SetItemSubTypeGroup rows — otherwise these only ever fed the
                // renewal's own header totals above and View Details would keep showing 0.00.
                foreach (var card in GroupSummaryCards)
                {
                    try
                    {
                        await Task.Run(() => _repository.SaveGroupCardFinancials(
                            newSetId, card.SubType, card.ReferenceCode, card.BeginDate, card.EndDate,
                            card.Subtotal, card.VatPercent, card.WhtPercent, card.DiscountPercent, userId));
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Sub-Type Group {card.SubType}: {ex.Message}");
                    }
                }

                string msg = $"{successCount} of {checkedItems.Count} item(s) renewed successfully.";
                if (uncheckedExpired.Count > 0)
                    msg += $"\n{uncheckedExpired.Count} expired, unchecked item(s) were auto-archived.";
                if (errors.Count > 0)
                    msg += $"\n\nErrors ({errors.Count}):\n" + string.Join("\n", errors);

                RequestInfo?.Invoke("Renewal Complete", msg);

                // Offer to attach the new physical renewal paperwork (SI/DR/PO scan) to the new
                // Set right away. This creates a brand-new ReceiptSet row linked only to
                // newSetId — the original Set's scanned documents are untouched (ReceiptSet/
                // ReceiptSetLink is a many-to-many join, not a single slot per Set).
                bool attach = ConfirmYesNo?.Invoke(
                    "Attach Scanned Documents",
                    "Would you like to attach the scanned renewal document(s) (SI/DR/PO) for the new Set now?") ?? false;
                if (attach)
                    RequestAttachReceipt?.Invoke(newSetId);

                CloseRequested?.Invoke(true);
            }
            catch (Exception ex)
            {
                // Keep the window open on failure (unlike the old behavior where the caller only
                // found out after the dialog had already closed) so the user can retry without
                // re-entering everything.
                RequestError?.Invoke("Renewal Failed", ex.Message);
            }
            finally
            {
                _isConfirming = false;
            }
        }
    }

    /// <summary>One entry in the "Existing Group" picker of the Sub-Type Group panel — either
    /// the "(New Group)" sentinel or a distinct group already present among the current Rows.</summary>
    public class RowGroupOption
    {
        public bool IsNewGroup { get; set; }
        public string SubType { get; set; }
        public string ReferenceCode { get; set; }
        public DateTime? BeginDate { get; set; }
        public DateTime? EndDate { get; set; }

        public override string ToString() =>
            IsNewGroup ? "(New Group)" : $"{SubType} #{ReferenceCode ?? "(none)"}";
    }

    public class ParentTagRowGroupOption
    {
        public bool IsNewGroup { get; set; }
        public string Label { get; set; }

        public override string ToString() => IsNewGroup ? "(New Group)" : Label;
    }
}
