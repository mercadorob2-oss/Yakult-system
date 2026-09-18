using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Invoice.ViewModels
{
    /// <summary>
    /// Business logic for the Invoice License Review page. Loads the read-only
    /// dbo.vw_InvoiceNonLicensedItems review queue, lets the user stage "Licensed" /
    /// "NonLicensed" classification decisions entirely in memory (nothing is written to the
    /// database until Save Changes), then commits them via InvoiceLicenseReviewRepository —
    /// which only ever writes to dbo.Item.LicenseReviewStatus/ReviewedBy/ReviewedAt.
    /// </summary>
    public sealed class InvoiceLicenseReviewViewModel : ViewModelBase
    {
        private readonly InvoiceLicenseReviewRepository _repository = new InvoiceLicenseReviewRepository();
        private readonly SetRepository _setRepository = new SetRepository();

        private List<InvoiceLicenseReviewRow> _allRows = new List<InvoiceLicenseReviewRow>();
        private List<InvoiceLicenseReviewRow> _filteredRows = new List<InvoiceLicenseReviewRow>();
        private readonly Dictionary<int, InvoiceLicenseReviewRow> _rowsByItemId = new Dictionary<int, InvoiceLicenseReviewRow>();

        /// <summary>Staged (not-yet-saved) decisions, keyed by ItemId. Source of truth for
        /// HasPendingChanges / Save Changes / Undo Changes — row.PendingDecision mirrors this
        /// for display but this dictionary is what actually gets persisted.</summary>
        private readonly Dictionary<int, string> _pendingByItemId = new Dictionary<int, string>();

        public InvoiceLicenseReviewViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            MarkAsLicensedCommand = new RelayCommand<IList>(
                items => MarkRowsAsLicensed(items?.Cast<InvoiceLicenseReviewRow>()),
                items => items != null && items.Count > 0);
            MarkAsNonLicensedCommand = new RelayCommand<IList>(
                items => MarkRowsAsNonLicensed(items?.Cast<InvoiceLicenseReviewRow>()),
                items => items != null && items.Count > 0);
            SaveChangesCommand = new RelayCommand(async () => await SaveChangesAsync(), () => HasPendingChanges && !IsSaving);
            UndoChangesCommand = new RelayCommand(UndoAllChanges, () => HasPendingChanges);
            UndoSingleCommand = new RelayCommand<InvoiceLicenseReviewRow>(UndoPendingDecision, r => r != null && r.HasPendingDecision);
            ClearCategoryFilterCommand = new RelayCommand(ClearCategoryFilter);
            ClearItemTypeFilterCommand = new RelayCommand(ClearItemTypeFilter);

            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; BindPage(); });
            PrevPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, CurrentPage - 1); BindPage(); });
            NextPageCommand = new RelayCommand(() => { CurrentPage = CurrentPage + 1; BindPage(); });
            LastPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, GetTotalPages()); BindPage(); });
        }

        // ── Events (WinForms interop seam — wired by the View's code-behind) ────
        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestWarning;
        public event Action<string, string> RequestError;
        public Func<string, string, bool> ConfirmYesNo { get; set; }

        /// <summary>Raised when the user wants to look at the actual invoice for one row
        /// before classifying it — the View opens the read-only invoice detail dialog.</summary>
        public event Action<InvoiceLicenseReviewRow> RequestPreviewInvoice;

        // ── Commands ─────────────────────────────────────────────────────────
        public RelayCommand RefreshCommand { get; }
        public RelayCommand<IList> MarkAsLicensedCommand { get; }
        public RelayCommand<IList> MarkAsNonLicensedCommand { get; }
        public RelayCommand SaveChangesCommand { get; }
        public RelayCommand UndoChangesCommand { get; }
        public RelayCommand<InvoiceLicenseReviewRow> UndoSingleCommand { get; }
        public RelayCommand ClearCategoryFilterCommand { get; }
        public RelayCommand ClearItemTypeFilterCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        // ── Grid data ────────────────────────────────────────────────────────
        public ObservableCollection<InvoiceLicenseReviewRow> Rows { get; } = new ObservableCollection<InvoiceLicenseReviewRow>();

        /// <summary>Live view of rows currently staged as "Licensed" — bound to the right drop panel.</summary>
        public ObservableCollection<InvoiceLicenseReviewRow> PendingLicensedRows { get; } = new ObservableCollection<InvoiceLicenseReviewRow>();

        /// <summary>Live view of rows currently staged as "NonLicensed" — bound to the left drop panel.</summary>
        public ObservableCollection<InvoiceLicenseReviewRow> PendingNonLicensedRows { get; } = new ObservableCollection<InvoiceLicenseReviewRow>();

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { if (SetField(ref _isLoading, value)) RaiseBusyChanged(); }
        }

        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                if (SetField(ref _isSaving, value))
                {
                    SaveChangesCommand.RaiseCanExecuteChanged();
                    RaiseBusyChanged();
                }
            }
        }

        public bool IsBusy => IsLoading || IsSaving;
        public string BusyMessage => IsSaving ? "Saving classification changes…" : "Loading review queue…";

        private void RaiseBusyChanged()
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(BusyMessage));
        }

        private int _totalCount;
        public int TotalCount { get => _totalCount; private set => SetField(ref _totalCount, value); }

        // ── Pagination ───────────────────────────────────────────────────────
        public ObservableCollection<int> PageSizeOptions { get; } = new ObservableCollection<int> { 25, 50, 100, 200 };

        private int _pageSize = 50;
        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (!SetField(ref _pageSize, value)) return;
                CurrentPage = 1;
                BindPage();
            }
        }

        private int _currentPage = 1;
        public int CurrentPage { get => _currentPage; private set => SetField(ref _currentPage, value); }

        private string _pageInfoText = "Page 0 of 0 (0 items)";
        public string PageInfoText { get => _pageInfoText; private set => SetField(ref _pageInfoText, value); }

        private int GetTotalPages()
        {
            var total = _filteredRows?.Count ?? 0;
            return total <= 0 ? 0 : (int)Math.Ceiling(total / (double)PageSize);
        }

        private void BindPage()
        {
            var src = _filteredRows ?? new List<InvoiceLicenseReviewRow>();
            var totalPages = GetTotalPages();

            if (src.Count == 0)
            {
                Rows.Clear();
                PageInfoText = "Page 0 of 0 (0 items)";
                return;
            }

            CurrentPage = totalPages > 0 ? Math.Max(1, Math.Min(CurrentPage, totalPages)) : 1;

            var pageItems = src.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            Rows.Clear();
            foreach (var item in pageItems) Rows.Add(item);

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({src.Count} items)";
        }

        public bool HasPendingChanges => _pendingByItemId.Count > 0;

        public int PendingLicensedCount => PendingLicensedRows.Count;
        public int PendingNonLicensedCount => PendingNonLicensedRows.Count;

        // ── Search ───────────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilters(); }
        }

        // ── Category / Item Type multi-select filters (same pattern as InvoicePageViewModel) ──
        public ObservableCollection<CategoryFilterOption> CategoryFilterOptions { get; } = new ObservableCollection<CategoryFilterOption>();
        public ObservableCollection<CategoryFilterOption> ItemTypeFilterOptions { get; } = new ObservableCollection<CategoryFilterOption>();

        private string _categoryFilterSummary = "All Categories";
        public string CategoryFilterSummary { get => _categoryFilterSummary; private set => SetField(ref _categoryFilterSummary, value); }

        private string _itemTypeFilterSummary = "All Item Types";
        public string ItemTypeFilterSummary { get => _itemTypeFilterSummary; private set => SetField(ref _itemTypeFilterSummary, value); }

        private bool _suppressCategoryFilterChange;
        private bool _suppressItemTypeFilterChange;

        private const string UncategorizedLabel = "(Uncategorized)";

        // ── Loading ──────────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            if (HasPendingChanges)
            {
                bool proceed = ConfirmYesNo?.Invoke(
                    "Discard Unsaved Changes?",
                    "You have unsaved classification changes. Refreshing will discard them and reload from the database. Continue?") ?? false;
                if (!proceed) return;
            }

            IsLoading = true;
            try
            {
                var dtos = await _repository.GetPendingReviewItemsAsync();
                var rows = dtos.Select(MapRow).ToList();

                // Enrich each row with EVERY item type / category found on its invoice (not just
                // this row's own Hardware item) — the review view only returns unreviewed Hardware
                // lines, so without this a reviewer can't see whether the same invoice also
                // carries Software/License items or other hardware categories, which is exactly
                // the context needed to judge whether a Hardware line is a genuine licensable
                // exception. Same helpers the Invoice Reports page uses for its own filters.
                var setIds = rows.Select(r => r.SetId).Distinct().ToList();
                var itemTypesBySetId = await Task.Run(() => _setRepository.GetItemTypesBySetIds(setIds));
                var categoriesBySetId = await Task.Run(() => _setRepository.GetCategoriesBySetIds(setIds));
                foreach (var row in rows)
                {
                    row.ItemTypesOnInvoice = itemTypesBySetId.TryGetValue(row.SetId, out var types) ? types : Array.Empty<string>();
                    row.CategoriesOnInvoice = categoriesBySetId.TryGetValue(row.SetId, out var cats) ? cats : Array.Empty<string>();
                }

                _allRows = rows;
                _rowsByItemId.Clear();
                foreach (var row in rows) _rowsByItemId[row.ItemId] = row;

                _pendingByItemId.Clear();
                RebuildPendingCollections();
                RaisePendingChanged();

                LoadCategoryFilterOptions();
                LoadItemTypeFilterOptions();
                ApplyFilters();
            }
            catch (SqlException ex)
            {
                RequestError?.Invoke(
                    "Database Error",
                    $"Lost connection to the database, or a database error occurred while loading the review queue:\n\n{ex.Message}");
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load invoice items pending license review:\n\n{ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static InvoiceLicenseReviewRow MapRow(InvoiceLicenseReviewRepository.ReviewItemDto dto) => new InvoiceLicenseReviewRow
        {
            ItemId = dto.ItemId,
            SetId = dto.SetId,
            ReqId = dto.ReqId,
            InvoiceNumber = dto.InvoiceNumber,
            PONumber = dto.PONumber,
            InvoiceDate = dto.InvoiceDate,
            Status = dto.Status,
            Site = dto.Site,
            CompanyName = dto.CompanyName,
            InvoiceTotalAmount = dto.InvoiceTotalAmount,
            Department = dto.Department,
            Employee = dto.Employee,
            ItemCode = dto.ItemCode,
            ItemName = dto.ItemName,
            ItemDescription = dto.ItemDescription,
            ItemType = dto.ItemType,
            ModelNumber = dto.ModelNumber,
            SerialNumber = dto.SerialNumber,
            CategoryName = dto.CategoryName,
            LegacyCategoryText = dto.LegacyCategoryText,
            Quantity = dto.Quantity,
            Unit = dto.Unit,
            UnitPrice = dto.UnitPrice,
            LineTotal = dto.LineTotal,
            VendorName = dto.VendorName,
            ConditionName = dto.ConditionName,
            CreatedAt = dto.CreatedAt,
        };

        // ── Filtering ────────────────────────────────────────────────────────
        private void ApplyFilters()
        {
            IEnumerable<InvoiceLicenseReviewRow> filtered = _allRows;

            var terms = SearchTextHelper.SplitTerms(SearchText);
            if (terms.Length > 0)
            {
                filtered = filtered.Where(r => SearchTextHelper.MatchesAllTerms(terms, new[]
                {
                    r.InvoiceNumber, r.PONumber, r.ItemName, r.ItemDescription, r.ModelNumber,
                    r.SerialNumber, r.DisplayCategory, r.CompanyName, r.Department, r.Employee, r.VendorName
                }));
            }

            var selectedCategories = CategoryFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            if (selectedCategories.Count > 0)
            {
                filtered = filtered.Where(r =>
                    selectedCategories.Contains(string.IsNullOrWhiteSpace(r.DisplayCategory) ? UncategorizedLabel : r.DisplayCategory, StringComparer.OrdinalIgnoreCase));
            }

            // Filters on the FULL set of item types present on the row's invoice (Hardware,
            // Software/License, Services — whatever the invoice actually contains), not just
            // this row's own Hardware type, since every row is Hardware by definition here.
            var selectedItemTypes = ItemTypeFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            if (selectedItemTypes.Count > 0)
            {
                filtered = filtered.Where(r =>
                    r.ItemTypesOnInvoice != null &&
                    r.ItemTypesOnInvoice.Any(t => selectedItemTypes.Contains(t, StringComparer.OrdinalIgnoreCase)));
            }

            _filteredRows = filtered.ToList();
            TotalCount = _filteredRows.Count;

            CurrentPage = 1;
            BindPage();
        }

        private void LoadCategoryFilterOptions()
        {
            var previouslyChecked = CategoryFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();

            var allCategories = _allRows
                .Select(r => string.IsNullOrWhiteSpace(r.DisplayCategory) ? UncategorizedLabel : r.DisplayCategory)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _suppressCategoryFilterChange = true;
            try
            {
                CategoryFilterOptions.Clear();
                foreach (var name in allCategories)
                {
                    var option = new CategoryFilterOption(name) { IsChecked = previouslyChecked.Contains(name, StringComparer.OrdinalIgnoreCase) };
                    option.CheckedChanged += OnCategoryFilterOptionChanged;
                    CategoryFilterOptions.Add(option);
                }
            }
            finally { _suppressCategoryFilterChange = false; }

            UpdateCategoryFilterSummary();
        }

        private void OnCategoryFilterOptionChanged()
        {
            if (_suppressCategoryFilterChange) return;
            UpdateCategoryFilterSummary();
            ApplyFilters();
        }

        private void ClearCategoryFilter()
        {
            _suppressCategoryFilterChange = true;
            try { foreach (var o in CategoryFilterOptions) o.IsChecked = false; }
            finally { _suppressCategoryFilterChange = false; }

            UpdateCategoryFilterSummary();
            ApplyFilters();
        }

        private void UpdateCategoryFilterSummary()
        {
            var names = CategoryFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            CategoryFilterSummary = names.Count == 0 ? "All Categories" : names.Count == 1 ? names[0] : $"{names.Count} categories selected";
        }

        private void LoadItemTypeFilterOptions()
        {
            var previouslyChecked = ItemTypeFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();

            var allTypes = _allRows
                .SelectMany(r => r.ItemTypesOnInvoice ?? Array.Empty<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _suppressItemTypeFilterChange = true;
            try
            {
                ItemTypeFilterOptions.Clear();
                foreach (var name in allTypes)
                {
                    var option = new CategoryFilterOption(name) { IsChecked = previouslyChecked.Contains(name, StringComparer.OrdinalIgnoreCase) };
                    option.CheckedChanged += OnItemTypeFilterOptionChanged;
                    ItemTypeFilterOptions.Add(option);
                }
            }
            finally { _suppressItemTypeFilterChange = false; }

            UpdateItemTypeFilterSummary();
        }

        private void OnItemTypeFilterOptionChanged()
        {
            if (_suppressItemTypeFilterChange) return;
            UpdateItemTypeFilterSummary();
            ApplyFilters();
        }

        private void ClearItemTypeFilter()
        {
            _suppressItemTypeFilterChange = true;
            try { foreach (var o in ItemTypeFilterOptions) o.IsChecked = false; }
            finally { _suppressItemTypeFilterChange = false; }

            UpdateItemTypeFilterSummary();
            ApplyFilters();
        }

        private void UpdateItemTypeFilterSummary()
        {
            var names = ItemTypeFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            ItemTypeFilterSummary = names.Count == 0 ? "All Item Types" : names.Count == 1 ? names[0] : $"{names.Count} item types selected";
        }

        // ── Preview ──────────────────────────────────────────────────────────
        public void PreviewInvoice(InvoiceLicenseReviewRow row)
        {
            if (row == null) return;
            RequestPreviewInvoice?.Invoke(row);
        }

        // ── Classification staging (drag-and-drop AND context menu/buttons call the same
        //    code path, per the requirement that both interaction methods behave identically) ──
        public void MarkRowsAsLicensed(IEnumerable<InvoiceLicenseReviewRow> rows) => MarkRows(rows, "Licensed");
        public void MarkRowsAsNonLicensed(IEnumerable<InvoiceLicenseReviewRow> rows) => MarkRows(rows, "NonLicensed");

        private void MarkRows(IEnumerable<InvoiceLicenseReviewRow> rows, string status)
        {
            if (rows == null) return;
            var list = rows.Where(r => r != null).ToList();
            if (list.Count == 0) return;

            foreach (var row in list)
            {
                row.PendingDecision = status;
                _pendingByItemId[row.ItemId] = status;
            }

            RebuildPendingCollections();
            RaisePendingChanged();
        }

        /// <summary>Reverts a single staged classification decision. Public so the View's
        /// code-behind can call it directly from the drop panels' ✕ button Click handler,
        /// instead of relying on a Command/CommandParameter binding resolved via
        /// RelativeSource from inside a nested ItemsControl DataTemplate (unreliable in this
        /// WPF-hosted-in-WinForms/ElementHost app — see other Click-handler workarounds in
        /// InvoiceLicenseReviewView.xaml.cs for the same reasoning).</summary>
        public void UndoPendingDecision(InvoiceLicenseReviewRow row)
        {
            if (row == null || !row.HasPendingDecision) return;

            row.PendingDecision = null;
            _pendingByItemId.Remove(row.ItemId);

            RebuildPendingCollections();
            RaisePendingChanged();
        }

        private void UndoAllChanges()
        {
            if (!HasPendingChanges) return;

            foreach (var itemId in _pendingByItemId.Keys.ToList())
            {
                if (_rowsByItemId.TryGetValue(itemId, out var row))
                    row.PendingDecision = null;
            }

            _pendingByItemId.Clear();
            RebuildPendingCollections();
            RaisePendingChanged();
        }

        private void RebuildPendingCollections()
        {
            PendingLicensedRows.Clear();
            PendingNonLicensedRows.Clear();

            foreach (var kvp in _pendingByItemId)
            {
                if (!_rowsByItemId.TryGetValue(kvp.Key, out var row)) continue;
                if (kvp.Value == "Licensed") PendingLicensedRows.Add(row);
                else if (kvp.Value == "NonLicensed") PendingNonLicensedRows.Add(row);
            }

            OnPropertyChanged(nameof(PendingLicensedCount));
            OnPropertyChanged(nameof(PendingNonLicensedCount));
        }

        private void RaisePendingChanged()
        {
            OnPropertyChanged(nameof(HasPendingChanges));
            SaveChangesCommand.RaiseCanExecuteChanged();
            UndoChangesCommand.RaiseCanExecuteChanged();
        }

        // ── Save ─────────────────────────────────────────────────────────────
        private async Task SaveChangesAsync()
        {
            if (!HasPendingChanges) return;

            int licensedCount = _pendingByItemId.Count(kvp => kvp.Value == "Licensed");
            int nonLicensedCount = _pendingByItemId.Count(kvp => kvp.Value == "NonLicensed");

            bool confirmed = ConfirmYesNo?.Invoke(
                "Save Classification Changes?",
                $"This will mark {licensedCount} item(s) as Licensed and {nonLicensedCount} item(s) as Non-Licensed.\n\n" +
                "This updates dbo.Item directly (LicenseReviewStatus). No invoice, Set, or SetItem record is changed or deleted. Continue?") ?? false;
            if (!confirmed) return;

            IsSaving = true;
            try
            {
                var decisions = _pendingByItemId
                    .Select(kvp => new InvoiceLicenseReviewRepository.ReviewDecision { ItemId = kvp.Key, Status = kvp.Value })
                    .ToList();

                int applied = await _repository.SaveReviewDecisionsAsync(decisions, Session.AppSession.CurrentUserId);

                _pendingByItemId.Clear();
                RebuildPendingCollections();
                RaisePendingChanged();

                RequestInfo?.Invoke(
                    "Changes Saved",
                    applied == decisions.Count
                        ? $"{applied} item(s) were classified successfully. The review queue has been refreshed."
                        : $"{applied} of {decisions.Count} item(s) were classified. The rest had already been reviewed by someone else in the meantime and were skipped automatically.");

                await LoadAsync();
            }
            catch (SqlException ex)
            {
                RequestError?.Invoke(
                    "Save Failed",
                    $"Lost connection to the database, or a database error occurred while saving:\n\n{ex.Message}\n\n" +
                    "Your pending changes have been kept — nothing was lost. You can try Save Changes again.");
            }
            catch (Exception ex)
            {
                RequestError?.Invoke(
                    "Save Failed",
                    $"Failed to save classification changes:\n\n{ex.Message}\n\n" +
                    "Your pending changes have been kept — nothing was lost. You can try Save Changes again.");
            }
            finally
            {
                IsSaving = false;
            }
        }

        /// <summary>Called by the View before navigating away or closing — true if there is
        /// unsaved work the user should be warned about.</summary>
        public bool HasUnsavedWork => HasPendingChanges;
    }
}
