using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Yakult.Inventory.App.WPF.Archive.Services;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Archive.ViewModels
{
    public class ArchivePageViewModel : ViewModelBase
    {
        private readonly string _connectionString;

        private List<ArchiveRecordDto> _allRecords      = new List<ArchiveRecordDto>();
        private List<ArchiveRecordDto> _filteredRecords = new List<ArchiveRecordDto>();

        public ObservableCollection<ArchiveRecordDto> PagedRecords { get; }
            = new ObservableCollection<ArchiveRecordDto>();

        private const int PageSize = 25;

        // ── Selection ─────────────────────────────────────────────────────────
        private ArchiveRecordDto _selectedRecord;
        public ArchiveRecordDto SelectedRecord
        {
            get => _selectedRecord;
            set { if (SetField(ref _selectedRecord, value)) OnPropertyChanged(nameof(HasSelection)); }
        }
        public bool HasSelection => _selectedRecord != null;

        // ── Bulk checkbox selection (independent of the single-row SelectedRecord above) ──
        public bool HasCheckedSelection => _filteredRecords.Any(r => r.IsSelected);
        public int  CheckedCount        => _filteredRecords.Count(r => r.IsSelected);

        public void SetRowSelected(int archiveId, bool selected)
        {
            var row = _filteredRecords.FirstOrDefault(r => r.ArchiveId == archiveId);
            if (row != null) row.IsSelected = selected;
            OnPropertyChanged(nameof(HasCheckedSelection));
            OnPropertyChanged(nameof(CheckedCount));
        }

        public void SetPageSelected(bool selected)
        {
            foreach (var row in PagedRecords)
                row.IsSelected = selected;
            OnPropertyChanged(nameof(HasCheckedSelection));
            OnPropertyChanged(nameof(CheckedCount));
        }

        public List<ArchiveRecordDto> GetCheckedRecords() => _filteredRecords.Where(r => r.IsSelected).ToList();

        // ── Search / Filters ──────────────────────────────────────────────────
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilters(); }
        }

        public List<string> EntityTypeOptions { get; } = new List<string>
        {
            "All Types", "Item", "Set", "Request", "Inventory",
            "Employee", "Department", "Branch", "Company", "Vendor",
            "EmptyCartridge", "CartridgeModel", "ItemCategory", "Condition", "Renewal"
        };

        // ── Multi-select "Type" filter (Advanced Filters) ────────────────────
        // One checkbox per real EntityTypeOptions value (excludes the "All Types" placeholder).
        // Nothing checked = no type restriction (show all).
        public ObservableCollection<EntityTypeCheckItem> EntityTypeCheckItems { get; }
            = new ObservableCollection<EntityTypeCheckItem>();

        private IEnumerable<string> CheckedTypes => EntityTypeCheckItems.Where(c => c.IsChecked).Select(c => c.Type);

        private string _typeFilterSummary = "All Types";
        public string TypeFilterSummary { get => _typeFilterSummary; private set => SetField(ref _typeFilterSummary, value); }

        private void UpdateTypeFilterSummary()
        {
            var names = CheckedTypes.ToList();
            TypeFilterSummary = names.Count == 0 ? "All Types"
                : names.Count == 1 ? names[0]
                : $"{names.Count} types selected";
        }

        public ICommand ClearTypeFilterCommand { get; private set; }

        // ── Multi-select "Category" filter (Advanced Filters) ────────────────
        // Populated dynamically from dbo.Item.Category values actually present among the
        // archived Item records loaded (mirrors ItemsPageViewModel.RebuildCategoryOptions).
        // Only Item-type rows carry a Category, so checking any category implicitly narrows
        // the grid to archived Items.
        public ObservableCollection<CategoryFilterOption> CategoryFilterOptions { get; }
            = new ObservableCollection<CategoryFilterOption>();

        private bool _suppressCategoryFilterChange;

        private string _categoryFilterSummary = "All Categories";
        public string CategoryFilterSummary { get => _categoryFilterSummary; private set => SetField(ref _categoryFilterSummary, value); }

        public ICommand ClearCategoryFilterCommand { get; private set; }

        private void UpdateCategoryFilterSummary()
        {
            var names = CategoryFilterOptions.Where(c => c.IsSelected).Select(c => c.Name).ToList();
            CategoryFilterSummary = names.Count == 0 ? "All Categories"
                : names.Count == 1 ? names[0]
                : $"{names.Count} categories selected";
        }

        private void OnCategoryFilterChanged()
        {
            UpdateCategoryFilterSummary();
            ApplyFilters();
        }

        private void ClearCategoryFilter()
        {
            _suppressCategoryFilterChange = true;
            try { foreach (var opt in CategoryFilterOptions) opt.IsSelected = false; }
            finally { _suppressCategoryFilterChange = false; }
            UpdateCategoryFilterSummary();
            ApplyFilters();
        }

        private void RebuildCategoryOptions()
        {
            var previouslySelected = new HashSet<string>(
                CategoryFilterOptions.Where(c => c.IsSelected).Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

            var categories = _allRecords
                .Where(r => r.EntityType == "Item")
                .Select(r => r.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            _suppressCategoryFilterChange = true;
            try
            {
                CategoryFilterOptions.Clear();
                foreach (var cat in categories)
                    CategoryFilterOptions.Add(new CategoryFilterOption(cat, () => _suppressCategoryFilterChange, OnCategoryFilterChanged)
                    { IsSelected = previouslySelected.Contains(cat) });
            }
            finally { _suppressCategoryFilterChange = false; }
            UpdateCategoryFilterSummary();
        }

        // ── Clickable summary cards (Total/Items/Sets/Requests/Other) ────────
        // Each card is a shortcut that sets the checklist to an exact single-type (or,
        // for "Other", every type except Item/Set/Request) selection; clicking an
        // already-active card clears back to "show all".
        public bool IsTotalCardActive    => !CheckedTypes.Any();
        public bool IsItemsCardActive    => IsExactSelection("Item");
        public bool IsSetsCardActive     => IsExactSelection("Set");
        public bool IsRequestsCardActive => IsExactSelection("Request");
        public bool IsOtherCardActive    => IsExactSelection(OtherTypes());

        private IEnumerable<string> OtherTypes() =>
            EntityTypeOptions.Where(t => t != "All Types" && t != "Item" && t != "Set" && t != "Request");

        private bool IsExactSelection(string singleType) => IsExactSelection(new[] { singleType });

        private bool IsExactSelection(IEnumerable<string> targetTypes)
        {
            var target = new HashSet<string>(targetTypes);
            var current = new HashSet<string>(CheckedTypes);
            return target.SetEquals(current);
        }

        public ICommand TotalCardCommand { get; private set; }
        public ICommand ItemsCardCommand { get; private set; }
        public ICommand SetsCardCommand { get; private set; }
        public ICommand RequestsCardCommand { get; private set; }
        public ICommand OtherCardCommand { get; private set; }

        private void ToggleEntityType(string type)
        {
            var targetTypes = type == "Other" ? OtherTypes() : new[] { type };

            SetCheckedTypes(IsExactSelection(targetTypes) ? Enumerable.Empty<string>() : targetTypes);
        }

        private void SetCheckedTypes(IEnumerable<string> types)
        {
            var target = new HashSet<string>(types);
            foreach (var item in EntityTypeCheckItems)
                item.IsChecked = target.Contains(item.Type);
        }

        private void NotifySummaryCardsChanged()
        {
            OnPropertyChanged(nameof(IsTotalCardActive));
            OnPropertyChanged(nameof(IsItemsCardActive));
            OnPropertyChanged(nameof(IsSetsCardActive));
            OnPropertyChanged(nameof(IsRequestsCardActive));
            OnPropertyChanged(nameof(IsOtherCardActive));
        }

        private DateTime? _dateFrom;
        public DateTime? DateFrom
        {
            get => _dateFrom;
            set { if (SetField(ref _dateFrom, value)) ApplyFilters(); }
        }

        private DateTime? _dateTo;
        public DateTime? DateTo
        {
            get => _dateTo;
            set { if (SetField(ref _dateTo, value)) ApplyFilters(); }
        }

        // ── Summary Stats ─────────────────────────────────────────────────────
        private int _totalCount;
        public int TotalCount    { get => _totalCount;    private set => SetField(ref _totalCount,    value); }

        private int _itemCount;
        public int ItemCount     { get => _itemCount;     private set => SetField(ref _itemCount,     value); }

        private int _setCount;
        public int SetCount      { get => _setCount;      private set => SetField(ref _setCount,      value); }

        private int _requestCount;
        public int RequestCount  { get => _requestCount;  private set => SetField(ref _requestCount,  value); }

        private int _otherCount;
        public int OtherCount    { get => _otherCount;    private set => SetField(ref _otherCount,    value); }

        // ── Loading / Status ──────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading    { get => _isLoading;     private set => SetField(ref _isLoading,     value); }

        private string _statusMessage = string.Empty;
        public string StatusMessage   { get => _statusMessage; private set => SetField(ref _statusMessage, value); }

        public bool HasError => !string.IsNullOrEmpty(_statusMessage);

        // ── Pagination ────────────────────────────────────────────────────────
        private int _currentPage = 1;
        private int _totalPages  = 0;

        private string _pageInfo = "Page 0 of 0";
        public string PageInfo   { get => _pageInfo;  private set => SetField(ref _pageInfo,  value); }

        public bool CanGoFirst => _currentPage > 1;
        public bool CanGoPrev  => _currentPage > 1;
        public bool CanGoNext  => _currentPage < _totalPages;
        public bool CanGoLast  => _currentPage < _totalPages;

        // ── Commands ──────────────────────────────────────────────────────────
        public ICommand RefreshCommand      { get; }
        public ICommand ViewDetailsCommand  { get; }
        public ICommand ExportCommand       { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand FirstPageCommand    { get; }
        public ICommand PrevPageCommand     { get; }
        public ICommand NextPageCommand     { get; }
        public ICommand LastPageCommand     { get; }

        // ── Events (code-behind subscription) ─────────────────────────────────
        public event Action<ArchiveRecordDto> RequestOpenDetails;
        public event Action                   RequestExportCsv;

        public ArchivePageViewModel(string connectionString)
        {
            _connectionString = connectionString;

            RefreshCommand      = new RelayCommand(() => _ = LoadDataAsync());
            ViewDetailsCommand  = new RelayCommand(() => RequestOpenDetails?.Invoke(_selectedRecord), () => HasSelection);
            ExportCommand       = new RelayCommand(() => RequestExportCsv?.Invoke());
            ClearFiltersCommand = new RelayCommand(ClearFilters);
            FirstPageCommand    = new RelayCommand(() => { _currentPage = 1;            RebuildPage(); }, () => CanGoFirst);
            PrevPageCommand     = new RelayCommand(() => { _currentPage--;              RebuildPage(); }, () => CanGoPrev);
            NextPageCommand     = new RelayCommand(() => { _currentPage++;              RebuildPage(); }, () => CanGoNext);
            LastPageCommand     = new RelayCommand(() => { _currentPage = _totalPages;  RebuildPage(); }, () => CanGoLast);

            TotalCardCommand    = new RelayCommand(() => SetCheckedTypes(Enumerable.Empty<string>()));
            ItemsCardCommand    = new RelayCommand(() => ToggleEntityType("Item"));
            SetsCardCommand     = new RelayCommand(() => ToggleEntityType("Set"));
            RequestsCardCommand = new RelayCommand(() => ToggleEntityType("Request"));
            OtherCardCommand    = new RelayCommand(() => ToggleEntityType("Other"));

            ClearTypeFilterCommand     = new RelayCommand(() => SetCheckedTypes(Enumerable.Empty<string>()));
            ClearCategoryFilterCommand = new RelayCommand(ClearCategoryFilter);

            foreach (var type in EntityTypeOptions.Where(t => t != "All Types"))
            {
                var item = new EntityTypeCheckItem(type);
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName != nameof(EntityTypeCheckItem.IsChecked)) return;
                    NotifySummaryCardsChanged();
                    UpdateTypeFilterSummary();
                    ApplyFilters();
                };
                EntityTypeCheckItems.Add(item);
            }
        }

        public async Task LoadDataAsync()
        {
            IsLoading     = true;
            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(HasError));

            try
            {
                var records = new List<ArchiveRecordDto>();

                const string sql = @"
                    SELECT
                        a.ArchiveId,
                        a.EntityType,
                        a.EntityId,
                        CASE a.EntityType
                            WHEN 'Item'           THEN ISNULL(i.Name,          'Item #'           + CAST(a.EntityId AS NVARCHAR(20)))
                            WHEN 'Set'            THEN ISNULL(s.SetCode,       'Set #'            + CAST(a.EntityId AS NVARCHAR(20)))
                            WHEN 'Request'        THEN 'Request #'             + CAST(a.EntityId AS NVARCHAR(20))
                            WHEN 'Inventory'      THEN 'Inventory #'           + CAST(a.EntityId AS NVARCHAR(20))
                            WHEN 'Department'     THEN ISNULL(dept.Name,       'Department #'     + CAST(a.EntityId AS NVARCHAR(20)))
                            WHEN 'Branch'         THEN ISNULL(br.Name,         'Branch #'         + CAST(a.EntityId AS NVARCHAR(20)))
                            WHEN 'Employee'       THEN ISNULL(emp.Name,        'Employee #'       + CAST(a.EntityId AS NVARCHAR(20)))
                            WHEN 'Company'        THEN ISNULL(comp.Name,       'Company #'        + CAST(a.EntityId AS NVARCHAR(20)))
                            WHEN 'Vendor'         THEN ISNULL(ven.VendorName,  'Vendor #'         + CAST(a.EntityId AS NVARCHAR(20)))
                            WHEN 'EmptyCartridge' THEN ISNULL(cm.ModelNumber,  'Cartridge #'      + CAST(a.EntityId AS NVARCHAR(20)))
                            WHEN 'CartridgeModel' THEN ISNULL(cmod.ModelNumber,'CartridgeModel #' + CAST(a.EntityId AS NVARCHAR(20)))
                            WHEN 'ItemCategory'   THEN ISNULL(ic.Name,         'Category #'       + CAST(a.EntityId AS NVARCHAR(20)))
                            WHEN 'Condition'      THEN ISNULL(cond.ConditionName,'Condition #'    + CAST(a.EntityId AS NVARCHAR(20)))
                            WHEN 'Renewal'        THEN 'Renewal #'             + CAST(a.EntityId AS NVARCHAR(20))
                            ELSE a.EntityType + ' #' + CAST(a.EntityId AS NVARCHAR(20))
                        END AS RecordName,
                        a.ArchivedAt,
                        a.ArchivedBy,
                        ISNULL(a.ArchiveReason, '') AS ArchiveReason,
                        i.Category AS ItemCategory
                    FROM dbo.ArchiveStatus a
                    LEFT JOIN dbo.Item              i    ON a.EntityType = 'Item'           AND a.EntityId = i.ItemId
                    LEFT JOIN dbo.[Set]             s    ON a.EntityType = 'Set'            AND a.EntityId = s.SetId
                    LEFT JOIN dbo.Department        dept ON a.EntityType = 'Department'     AND a.EntityId = dept.DeptId
                    LEFT JOIN dbo.Branch            br   ON a.EntityType = 'Branch'         AND a.EntityId = br.BranchId
                    LEFT JOIN dbo.Employee          emp  ON a.EntityType = 'Employee'       AND a.EntityId = emp.EmpId
                    LEFT JOIN dbo.Company           comp ON a.EntityType = 'Company'        AND a.EntityId = comp.ComId
                    LEFT JOIN dbo.Vendor            ven  ON a.EntityType = 'Vendor'         AND a.EntityId = ven.VendorID
                    LEFT JOIN dbo.EmptyCartridge    ec   ON a.EntityType = 'EmptyCartridge' AND a.EntityId = ec.EmptyCartridgeId
                    LEFT JOIN dbo.CartridgeModel    cm   ON ec.CartridgeModelId             = cm.CartridgeModelId
                    LEFT JOIN dbo.CartridgeModel    cmod ON a.EntityType = 'CartridgeModel' AND a.EntityId = cmod.CartridgeModelId
                    LEFT JOIN dbo.ItemCategory      ic   ON a.EntityType = 'ItemCategory'   AND a.EntityId = ic.CategoryId
                    LEFT JOIN dbo.Condition         cond ON a.EntityType = 'Condition'      AND a.EntityId = cond.ConditionId
                    WHERE a.IsArchived = 1
                    ORDER BY a.ArchivedAt DESC";

                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 })
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            records.Add(new ArchiveRecordDto
                            {
                                ArchiveId     = reader.GetInt32(0),
                                EntityType    = reader.GetString(1),
                                EntityId      = reader.GetInt32(2),
                                RecordName    = reader.IsDBNull(3) ? $"{reader.GetString(1)} #{reader.GetInt32(2)}" : reader.GetString(3),
                                ArchivedAt    = reader.GetDateTime(4),
                                ArchivedBy    = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                ArchiveReason = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                Category      = reader.IsDBNull(7) ? null : reader.GetString(7)
                            });
                        }
                    }
                }

                _allRecords = records;

                TotalCount   = records.Count;
                ItemCount    = records.Count(r => r.EntityType == "Item");
                SetCount     = records.Count(r => r.EntityType == "Set");
                RequestCount = records.Count(r => r.EntityType == "Request");
                OtherCount   = records.Count(r => r.EntityType != "Item" && r.EntityType != "Set" && r.EntityType != "Request");

                RebuildCategoryOptions();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                StatusMessage = "Error loading archive data: " + ex.Message;
                OnPropertyChanged(nameof(HasError));
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilters()
        {
            IEnumerable<ArchiveRecordDto> filtered = _allRecords;

            var checkedTypes = new HashSet<string>(CheckedTypes);
            if (checkedTypes.Count > 0)
                filtered = filtered.Where(r => checkedTypes.Contains(r.EntityType));

            var selectedCategories = CategoryFilterOptions.Where(c => c.IsSelected).Select(c => c.Name).ToList();
            if (selectedCategories.Count > 0)
                filtered = filtered.Where(r => r.Category != null && selectedCategories.Contains(r.Category, StringComparer.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string q = _searchText;
                filtered = filtered.Where(r =>
                    r.ArchivedBy.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.RecordName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.ArchiveReason.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (_dateFrom.HasValue)
                filtered = filtered.Where(r => r.ArchivedAt.Date >= _dateFrom.Value.Date);
            if (_dateTo.HasValue)
                filtered = filtered.Where(r => r.ArchivedAt.Date <= _dateTo.Value.Date);

            _filteredRecords = filtered.ToList();
            _currentPage = 1;
            _totalPages  = Math.Max(1, (_filteredRecords.Count + PageSize - 1) / PageSize);

            RebuildPage();
        }

        private void RebuildPage()
        {
            PagedRecords.Clear();
            foreach (var r in _filteredRecords.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRecords.Add(r);

            PageInfo = $"Page {_currentPage} of {_totalPages} ({_filteredRecords.Count} records)";
            OnPropertyChanged(nameof(CanGoFirst));
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanGoLast));
        }

        private void ClearFilters()
        {
            _searchText = string.Empty;
            _dateFrom   = null;
            _dateTo     = null;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(DateFrom));
            OnPropertyChanged(nameof(DateTo));
            SetCheckedTypes(Enumerable.Empty<string>());
            NotifySummaryCardsChanged();
            UpdateTypeFilterSummary();
            ClearCategoryFilter();
            ApplyFilters();
        }

        public List<ArchiveRecordDto> GetFilteredRecords() => _filteredRecords;

        /// <summary>
        /// Restores one archived record (flips IsArchived back to 0 and reactivates the
        /// underlying entity) via the shared cascade used by the single-record details dialog.
        /// </summary>
        public async Task RestoreAsync(ArchiveRecordDto record, string restoredBy)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        ArchiveRestoreService.RestoreEntityCascade(con, tx, record.EntityType, record.EntityId, restoredBy);
                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Permanently deletes one archived record and its underlying entity row. Only ever
        /// touches rows still marked IsArchived = 1 (enforced inside the service).
        /// </summary>
        public Task PermanentDeleteAsync(ArchiveRecordDto record)
            => ArchivePermanentDeleteService.DeleteAsync(_connectionString, record.EntityType, record.EntityId);
    }
}
