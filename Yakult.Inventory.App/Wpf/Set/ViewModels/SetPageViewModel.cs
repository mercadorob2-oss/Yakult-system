using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Set.ViewModels
{
    /// <summary>
    /// Business logic for the Manage Sets page, ported from Pages\Set\ViewSetPage.cs. Preserves
    /// search/summary-card filtering, Filter By ordering, Sort By dropdown, manual pagination
    /// (fixed page size of 50, matching the original), and the Add/Open/Archive/Delete/Generate
    /// Report workflows exactly as they existed in WinForms — including the original's two
    /// different selection scopes (see notes on SelectAllState vs GenerateReport below).
    /// </summary>
    public sealed partial class SetPageViewModel : ViewModelBase
    {
        private readonly SetRepository _repository = new SetRepository();

        private List<SetRow> _allRows = new List<SetRow>();
        private List<SetRow> _filteredRows = new List<SetRow>();
        private List<SetRow> _summaryUniverse = new List<SetRow>();
        private Dictionary<int, List<string>> _itemNamesBySetId = new Dictionary<int, List<string>>();
        private Dictionary<int, List<string>> _serialNumbersBySetId = new Dictionary<int, List<string>>();
        private Dictionary<int, List<string>> _categoriesBySetId = new Dictionary<int, List<string>>();
        private Dictionary<int, (DateTime? Min, DateTime? Max)> _purchaseDateRangeBySetId = new Dictionary<int, (DateTime? Min, DateTime? Max)>();

        // Only true when this VM was opened from Request & Set Management — controls both the
        // "Show Consumable Items" checkbox's visibility and its default-checked state. Other
        // launch points for this same shared page (MainForm's own View Sets menu items) never
        // set this, so they see every set/category exactly as before.
        private readonly bool _consumableFilterDefault;
        public bool IsConsumableFilterAvailable => _consumableFilterDefault;

        public SetPageViewModel(bool restrictToConsumablesByDefault = false)
        {
            _consumableFilterDefault = restrictToConsumablesByDefault;
            _showConsumableItemsOnly = restrictToConsumablesByDefault;

            FilterByOptions = new ObservableCollection<string> { "Default", "Most Recently Added", "Oldest Added" };
            SortByOptions = new ObservableCollection<ListSortOption>();
            Rows = new ObservableCollection<SetRow>();

            _selectedFilterBy = "Default";
            PageSize = 50;

            RefreshCommand = new RelayCommand(LoadSets);
            AddCommand = new RelayCommand(() => RequestAddSet?.Invoke());
            OpenSelectedCommand = new RelayCommand(OpenSelected, () => SelectedRow != null);
            BulkArchiveCommand = new RelayCommand(BulkArchiveChecked, () => Rows.Any(r => r.Selected));
            BulkDeleteCommand = new RelayCommand(() => DeleteRowsAsync(Rows.Where(r => r.Selected).ToList()), () => Rows.Any(r => r.Selected));
            GenerateReportCommand = new RelayCommand(GenerateReport);
            BulkAddFilesCommand = new RelayCommand(() => RequestBulkAddFiles?.Invoke());

            TotalCardCommand = new RelayCommand(() => ToggleCard(SummaryFilter.All));
            ExpiredCardCommand = new RelayCommand(() => ToggleCard(SummaryFilter.Expired));
            WithQrCardCommand = new RelayCommand(() => ToggleCard(SummaryFilter.WithQr));
            HardwareCardCommand = new RelayCommand(() => ToggleCard(SummaryFilter.Hardware));
            SoftwareLicenseCardCommand = new RelayCommand(() => ToggleCard(SummaryFilter.SoftwareLicense));
            ServicesCardCommand = new RelayCommand(() => ToggleCard(SummaryFilter.Services));

            CategoryFilterOptions = new ObservableCollection<CategoryFilterOption>();
            ClearCategoryFilterCommand = new RelayCommand(ClearCategoryFilter);
            ResetFiltersCommand = new RelayCommand(ResetFilters);

            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; BindPage(); });
            PrevPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, CurrentPage - 1); BindPage(); });
            NextPageCommand = new RelayCommand(() => { CurrentPage = CurrentPage + 1; BindPage(); });
            LastPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, GetTotalPages()); BindPage(); });

            _setFilterDebounceTimer.Tick += (s, e) => { _setFilterDebounceTimer.Stop(); LoadSets(); };
            _searchDebounceTimer.Tick += (s, e) => { _searchDebounceTimer.Stop(); ApplyFilters(); };
        }

        // ── Events (WinForms interop seam — wired by the View's code-behind) ────
        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestWarning;
        public event Action<string, string> RequestError;
        public Func<string, string, bool> ConfirmYesNo { get; set; }

        /// <summary>View opens AddSetPage; on OK, opens the detail page for the new set and reloads.</summary>
        public event Action RequestAddSet;

        /// <summary>View opens ViewSetDetailPage (already a WPF Window) for this SetId, then reloads.</summary>
        public event Action<int> RequestOpenSetDetail;

        /// <summary>View opens the extracted ArchiveSetDialog for these rows (works for 1 or many).</summary>
        public event Action<List<SetRow>> RequestArchiveRows;

        /// <summary>View opens BulkAddFilesWindow, then reloads once it closes.</summary>
        public event Action RequestBulkAddFiles;

        // ── Row selection (grid highlight, used by the toolbar "Open" button) ──
        private SetRow _selectedRow;
        public SetRow SelectedRow
        {
            get => _selectedRow;
            set => SetField(ref _selectedRow, value);
        }

        // ── Commands ─────────────────────────────────────────────────────────
        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand OpenSelectedCommand { get; }
        public RelayCommand BulkArchiveCommand { get; }
        public RelayCommand BulkDeleteCommand { get; }
        public RelayCommand GenerateReportCommand { get; }
        public RelayCommand BulkAddFilesCommand { get; }
        public RelayCommand TotalCardCommand { get; }
        public RelayCommand ExpiredCardCommand { get; }
        public RelayCommand WithQrCardCommand { get; }
        public RelayCommand HardwareCardCommand { get; }
        public RelayCommand SoftwareLicenseCardCommand { get; }
        public RelayCommand ServicesCardCommand { get; }
        public RelayCommand ClearCategoryFilterCommand { get; }
        public RelayCommand ResetFiltersCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        // ── Grid data (current page only) ───────────────────────────────────
        public ObservableCollection<SetRow> Rows { get; }

        /// <summary>All loaded Sets (not just the current page) — used by BulkAddFilesWindow's
        /// Set picker so it doesn't need to re-query the DB.</summary>
        public IReadOnlyList<SetRow> AllRows => _allRows;

        /// <summary>Header "select all" checkbox — toggles Selected for the CURRENT PAGE's rows
        /// only (matches the original, which iterated `dgvSets.Rows`, not the full filtered set).
        /// NOTE: Generate Report intentionally has a DIFFERENT scope — it reads Selected across
        /// the entire filtered set (`_filteredRows`, all pages), matching the original's use of
        /// `_filteredSets ?? _allSets` rather than the grid's bound rows. Do not unify these.</summary>
        private bool? _selectAllState = false;
        public bool? SelectAllState
        {
            get => _selectAllState;
            set
            {
                _selectAllState = value;
                OnPropertyChanged();
                if (value != true && value != false) return;

                foreach (var row in Rows) row.Selected = value.Value;
            }
        }

        // ── "N Selected" badge — page-scoped, matches BulkArchive/BulkDelete/SelectAllState
        //    (NOT GenerateReport's deliberately-different cross-page scope, see note above) ──
        private int _selectedCount;
        public int SelectedCount { get => _selectedCount; private set => SetField(ref _selectedCount, value); }
        public bool HasSelection => SelectedCount > 0;

        public List<SetRow> GetSelectedRows() => Rows.Where(r => r.Selected).ToList();
        public void ClearSelection() { foreach (var r in Rows) r.Selected = false; }

        private void OpenSelected()
        {
            var row = SelectedRow;
            if (row == null)
            {
                RequestInfo?.Invoke("No Selection", "Please select a set first.");
                return;
            }

            RequestOpenSetDetail?.Invoke(row.SetId);
        }

        private void BulkArchiveChecked()
        {
            var checkedRows = Rows.Where(r => r.Selected).ToList();
            if (checkedRows.Count == 0)
            {
                RequestInfo?.Invoke("No Selection", "Please check at least one set.");
                return;
            }

            RequestArchiveRows?.Invoke(checkedRows);
        }

        private void GenerateReport()
        {
            var list = _filteredRows ?? _allRows;
            var selectedIds = list?.Where(r => r.Selected).Select(r => r.SetId).ToList();
            RequestGenerateReport?.Invoke(selectedIds != null && selectedIds.Count > 0 ? selectedIds : null);
        }

        /// <summary>Raised with checked SetIds spanning ALL filtered pages (or null when none checked).</summary>
        public event Action<List<int>> RequestGenerateReport;
    }
}
