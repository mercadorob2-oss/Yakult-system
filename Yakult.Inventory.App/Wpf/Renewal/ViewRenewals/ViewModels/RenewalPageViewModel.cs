using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Renewal.ViewRenewals.ViewModels
{
    /// <summary>
    /// Business logic for the Renewals page, ported from Pages\Renewal\ViewRenewalPage.cs
    /// (specifically its live BuildUiWithTemplate()/ApplyFilters() path — the legacy BuildUi(),
    /// BuildDeveloperTools(), GridRenewals_CellPainting, RenewSet, and the nested
    /// InvoiceSetPickerDialog were all dead/unreachable and were not ported).
    ///
    /// Selection scope note: the checkbox "select all" AND the PDF-export/Generate-Report actions
    /// all operate across the ENTIRE filtered set (all pages), matching the original's use of
    /// `_filteredRenewals ?? _allRenewals` rather than the grid's currently-bound rows. This is the
    /// opposite scope from the Requests/Sets pages — do not "fix" this to be current-page-only.
    /// Export (CSV) is unconditional — it always dumps the complete unfiltered `_allRenewals`,
    /// ignoring both filters and checkbox selection, exactly like the original.
    /// </summary>
    public sealed partial class RenewalPageViewModel : ViewModelBase
    {
        private readonly RenewalRepository _repository = new RenewalRepository();
        private readonly SetRepository _setRepository = new SetRepository();

        private List<RenewalRow> _allRows = new List<RenewalRow>();
        private List<RenewalRow> _filteredRows = new List<RenewalRow>();
        private Dictionary<int, List<string>> _itemNamesBySetId = new Dictionary<int, List<string>>();

        private readonly Dictionary<string, Tuple<DateTime?, DateTime?>> _dateFilters
            = new Dictionary<string, Tuple<DateTime?, DateTime?>>();

        // ── Set-linked Advanced Filters (Set Code, Document #, Company, Department, Branch,
        // Employee, Reference Code, Parent Tag, Req ID) require a SQL reload — see
        // RenewalPageViewModel.Filters.cs for the property declarations and QueueSetFilterReload().
        // Same 300ms debounce pattern as ItemsPageViewModel.
        private readonly DispatcherTimer _setFilterDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };

        public RenewalPageViewModel()
        {
            StatusOptions = new ObservableCollection<string> { "All", "Expired", "Expiring Soon", "Warning", "Active", "No Expiry Date" };
            TypeOptions = new ObservableCollection<string> { "All", "Software/License", "Services" };
            CategoryFilterOptions = new ObservableCollection<CategoryFilterOption>();
            ItemTypeFilterOptions = new ObservableCollection<CategoryFilterOption>();
            SortByOptions = new ObservableCollection<ListSortOption>();
            Rows = new ObservableCollection<RenewalRow>();

            _selectedStatus = "All";
            _selectedType = "All";
            PageSize = 10;

            RefreshCommand = new RelayCommand(() => { _dateFilters.Clear(); LoadRenewals(); });
            ViewDetailsCommand = new RelayCommand(() => ViewDetails(SelectedRow), () => SelectedRow != null);
            ManageItemsCommand = new RelayCommand(() => ManageItems(SelectedRow), () => SelectedRow != null);
            ExportCommand = new RelayCommand(ExportCsv);
            PdfCommand = new RelayCommand(ExportPdf, () => (_filteredRows ?? _allRows).Any(r => r.Selected));
            LinkExistingCommand = new RelayCommand(() => RequestLinkExisting?.Invoke(SelectedRow), () => SelectedRow != null);
            GenerateReportCommand = new RelayCommand(GenerateReport);
            ClearCategoryFilterCommand = new RelayCommand(ClearCategoryFilter);
            ClearItemTypeFilterCommand = new RelayCommand(ClearItemTypeFilter);
            ClearSubTypeFilterCommand = new RelayCommand(ClearSubTypeFilter);
            ResetFiltersCommand = new RelayCommand(ResetFilters);
            InitDocumentDateFilterRows();

            TotalCardCommand = new RelayCommand(() => SelectedStatus = "All");
            ExpiredCardCommand = new RelayCommand(() => ToggleStatusCard("Expired"));
            ExpiringCardCommand = new RelayCommand(() => ToggleStatusCard("Expiring Soon"));
            WarningCardCommand = new RelayCommand(() => ToggleStatusCard("Warning"));
            ActiveCardCommand = new RelayCommand(() => ToggleStatusCard("Active"));

            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; BindPage(); });
            PrevPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, CurrentPage - 1); BindPage(); });
            NextPageCommand = new RelayCommand(() => { CurrentPage = CurrentPage + 1; BindPage(); });
            LastPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, GetTotalPages()); BindPage(); });

            _setFilterDebounceTimer.Tick += async (s, e) => { _setFilterDebounceTimer.Stop(); await LoadRenewalsAsync(); };
            _searchDebounceTimer.Tick += (s, e) => { _searchDebounceTimer.Stop(); ApplyFilters(); };
        }

        // ── Events (WinForms/WPF-window interop seam — wired by the View's code-behind) ──
        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestWarning;
        public event Action<string, string> RequestError;
        public Func<string, string, bool> ConfirmYesNo { get; set; }
        public Func<string, string> RequestSaveFilePath { get; set; }

        /// <summary>View opens RenewalDetailWindow(setId) and always reloads afterward.</summary>
        public event Action<int> RequestViewRenewalDetail;

        /// <summary>View opens ViewRenewalDetailPage(setId) (the WinForms page with the
        /// "Renew Items" bulk-renewal action) and always reloads afterward.</summary>
        public event Action<int> RequestManageRenewalItems;

        /// <summary>View fetches available sets, opens LinkRenewalWindow, shows result, reloads.</summary>
        public event Action<RenewalRow> RequestLinkExisting;

        /// <summary>Raised with checked SetIds (all filtered pages, or null when none checked) and
        /// the current status filter text (null when "All") to launch the RDLC report.</summary>
        public event Action<List<int>, string> RequestGenerateReport;

        // ── Row selection (grid highlight, used by View Details / Link Renewal Set) ──
        private RenewalRow _selectedRow;
        public RenewalRow SelectedRow
        {
            get => _selectedRow;
            set => SetField(ref _selectedRow, value);
        }

        // ── Commands ─────────────────────────────────────────────────────────
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ViewDetailsCommand { get; }
        public RelayCommand ManageItemsCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand PdfCommand { get; }
        public RelayCommand LinkExistingCommand { get; }
        public RelayCommand GenerateReportCommand { get; }
        public RelayCommand ClearCategoryFilterCommand { get; }
        public RelayCommand ResetFiltersCommand { get; }
        public RelayCommand TotalCardCommand { get; }
        public RelayCommand ExpiredCardCommand { get; }
        public RelayCommand ExpiringCardCommand { get; }
        public RelayCommand WarningCardCommand { get; }
        public RelayCommand ActiveCardCommand { get; }
        public RelayCommand ClearItemTypeFilterCommand { get; }
        public RelayCommand ClearSubTypeFilterCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        // ── Grid data (current page only) ───────────────────────────────────
        public ObservableCollection<RenewalRow> Rows { get; }

        /// <summary>Header "select all" checkbox — toggles Selected across the ENTIRE filtered set
        /// (all pages), matching the original's CheckStateChanged handler which iterated
        /// `_filteredRenewals ?? _allRenewals`, not just the grid's bound rows.</summary>
        private bool? _selectAllState = false;
        public bool? SelectAllState
        {
            get => _selectAllState;
            set
            {
                _selectAllState = value;
                OnPropertyChanged();
                if (value != true && value != false) return;

                foreach (var row in (_filteredRows ?? _allRows)) row.Selected = value.Value;
            }
        }

        // ── "N Selected" badge — cross-page, matches SelectAllState/PdfCommand/GenerateReport
        //    (see class-level Selection scope note above; this is the opposite of
        //    Requests/Sets, which are deliberately page-scoped) ──
        private int _selectedCount;
        public int SelectedCount { get => _selectedCount; private set => SetField(ref _selectedCount, value); }
        public bool HasSelection => SelectedCount > 0;

        public List<RenewalRow> GetSelectedRows() => (_filteredRows ?? _allRows).Where(r => r.Selected).ToList();
        public void ClearSelection() { foreach (var r in (_filteredRows ?? _allRows)) r.Selected = false; }

        private void GenerateReport()
        {
            var list = _filteredRows ?? _allRows;
            var selectedIds = list?.Where(r => r.Selected).Select(r => r.SetId).ToList();
            string filter = SelectedStatus == "All" ? null : SelectedStatus;
            RequestGenerateReport?.Invoke(selectedIds != null && selectedIds.Count > 0 ? selectedIds : null, filter);
        }

        /// <summary>Data-access passthrough for the View's Link Renewal Set flow (kept as a plain
        /// method, not an event, since it's pure data fetch with no dialog/UI concerns).</summary>
        public List<InvoiceSetPickerDto> GetAvailableSetsForLinking(int originalSetId)
            => _repository.GetAvailableInvoiceSetsForLinking(originalSetId);

        private void ViewDetails(RenewalRow row)
        {
            if (row == null)
            {
                RequestInfo?.Invoke("No Selection", "Please select a renewal first.");
                return;
            }

            try
            {
                int targetSetId = _repository.GetLatestSetIdInChain(row.SetId);
                if (targetSetId <= 0) targetSetId = row.SetId;
                RequestViewRenewalDetail?.Invoke(targetSetId);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to open renewal details: {ex.Message}\n\n{ex.StackTrace}");
            }
        }

        private void ManageItems(RenewalRow row)
        {
            if (row == null)
            {
                RequestInfo?.Invoke("No Selection", "Please select a renewal first.");
                return;
            }

            try
            {
                int targetSetId = _repository.GetLatestSetIdInChain(row.SetId);
                if (targetSetId <= 0) targetSetId = row.SetId;
                RequestManageRenewalItems?.Invoke(targetSetId);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to open renewal items: {ex.Message}\n\n{ex.StackTrace}");
            }
        }
    }
}
