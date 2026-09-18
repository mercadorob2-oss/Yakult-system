using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Invoice.ViewModels
{
    /// <summary>
    /// Business logic for the Invoice Reports page, ported from
    /// Pages\Invoice\ViewInvoiceReportPage.cs. Preserves search/company/date-range/quick-range
    /// filtering, expiry-status summary cards, column date-range popup filters, Sort By dropdown,
    /// manual pagination with a rows-per-page selector, and the Generate Report/Sheet/Open/Edit/
    /// Delete actions exactly as they existed in the WinForms page.
    /// </summary>
    public sealed partial class InvoicePageViewModel : ViewModelBase
    {
        private readonly SetRepository _repository = new SetRepository();
        private readonly ServiceSetRepository _serviceSetRepository = new ServiceSetRepository();
        private readonly InvoiceRepository _invoiceRepository =
            new InvoiceRepository(Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString);

        private List<InvoiceRow> _allRows = new List<InvoiceRow>();
        private List<InvoiceRow> _filteredRows = new List<InvoiceRow>();
        private Dictionary<int, List<string>> _itemNamesBySetId = new Dictionary<int, List<string>>();
        private Dictionary<int, List<string>> _serialNumbersBySetId = new Dictionary<int, List<string>>();
        private Dictionary<int, List<string>> _parentTagLabelsBySetId = new Dictionary<int, List<string>>();
        private Dictionary<int, InvoiceItemSearchFields> _invoiceItemSearchFieldsBySetId = new Dictionary<int, InvoiceItemSearchFields>();

        /// <summary>Popup date-range filters keyed by column name ("StartDate", "EndDate").</summary>
        private readonly Dictionary<string, Tuple<DateTime?, DateTime?>> _dateFilters
            = new Dictionary<string, Tuple<DateTime?, DateTime?>>();

        public InvoicePageViewModel()
        {
            FilterByOptions = new ObservableCollection<string> { "Default", "Most Recently Added", "Oldest Added" };
            CategoryFilterOptions = new ObservableCollection<CategoryFilterOption>();
            ItemTypeFilterOptions = new ObservableCollection<CategoryFilterOption>();
            QuickRangeOptions = new ObservableCollection<string> { "All", "Last 7 days", "Last 30 days", "Last 90 days", "This month", "This year" };
            PageSizeOptions = new ObservableCollection<int> { 10, 20, 50, 100 };
            SortByOptions = new ObservableCollection<ListSortOption>();
            Rows = new ObservableCollection<InvoiceRow>();

            _selectedFilterBy = "Default";
            _selectedQuickRange = "All";
            _pageSize = 20;

            RefreshCommand = new RelayCommand(LoadInvoices);
            GenerateReportCommand = new RelayCommand(GenerateReport);
            GenerateSheetCommand = new RelayCommand(GenerateSheet);
            OpenCommand = new RelayCommand(OpenSelected);
            EditCommand = new RelayCommand(EditSelected);
            DeleteCommand = new RelayCommand(DeleteSelected);
            ClearCategoryFilterCommand = new RelayCommand(ClearCategoryFilter);
            ClearItemTypeFilterCommand = new RelayCommand(ClearItemTypeFilter);
            ClearSubTypeFilterCommand = new RelayCommand(ClearSubTypeFilter);
            ResetFiltersCommand = new RelayCommand(ResetFilters);
            InitDocumentDateFilterRows();

            _setFilterDebounceTimer.Tick += (s, e) => { _setFilterDebounceTimer.Stop(); LoadInvoices(); };
            _searchDebounceTimer.Tick += (s, e) => { _searchDebounceTimer.Stop(); ApplyFilters(); };

            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; BindPage(); });
            PrevPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, CurrentPage - 1); BindPage(); });
            NextPageCommand = new RelayCommand(() => { CurrentPage = CurrentPage + 1; BindPage(); });
            LastPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, GetTotalPages()); BindPage(); });

            TotalCardCommand = new RelayCommand(() => ToggleCard(ExpiryFilterMode.All));
            ExpiredCardCommand = new RelayCommand(() => ToggleCard(ExpiryFilterMode.Expired));
            ExpiringCardCommand = new RelayCommand(() => ToggleCard(ExpiryFilterMode.Expiring90));
            ActiveCardCommand = new RelayCommand(() => ToggleCard(ExpiryFilterMode.Active));
            NoEndDateCardCommand = new RelayCommand(() => ToggleCard(ExpiryFilterMode.NoEndDate));
        }

        // ── Events (WinForms interop seam — wired by the View's code-behind) ────
        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestWarning;
        public event Action<string, string> RequestError;
        public event Action<InvoiceRow> RequestOpenInvoice;
        public event Action<InvoiceRow> RequestEditInvoice;

        /// <summary>Raised with the checked invoices (1 or more) so the View can show the
        /// Archive/Permanently-Delete confirmation dialog, then call CommitDeleteInvoices.</summary>
        public event Action<List<InvoiceRow>> RequestDeleteInvoices;

        /// <summary>Set by the View: shows a Yes/No confirm dialog and returns the user's choice.</summary>
        public Func<string, string, bool> ConfirmYesNo { get; set; }

        /// <summary>Raised with the checked-row SetCodes (or null when none checked) to launch the RDLC report.</summary>
        public event Action<List<string>> RequestGenerateReport;

        /// <summary>Raised with the checked-row SetCodes (or null when none checked) to launch the Excel-style sheet.</summary>
        public event Action<List<string>> RequestGenerateSheet;

        // ── Commands ─────────────────────────────────────────────────────────
        public RelayCommand RefreshCommand { get; }
        public RelayCommand GenerateReportCommand { get; }
        public RelayCommand GenerateSheetCommand { get; }
        public RelayCommand OpenCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand ClearCategoryFilterCommand { get; }
        public RelayCommand ClearItemTypeFilterCommand { get; }
        public RelayCommand ClearSubTypeFilterCommand { get; }
        public RelayCommand ResetFiltersCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        // ── Grid data (current page only) ───────────────────────────────────
        public ObservableCollection<InvoiceRow> Rows { get; }

        /// <summary>Header "select all" checkbox — toggling applies to every row in the
        /// current filtered set (not just the visible page), matching the WinForms behavior.</summary>
        private bool? _selectAllState = false;
        public bool? SelectAllState
        {
            get => _selectAllState;
            set
            {
                _selectAllState = value;
                OnPropertyChanged();
                if (value != true && value != false) return;

                var list = _filteredRows ?? _allRows;
                if (list == null) return;
                foreach (var r in list) r.Selected = value.Value;
            }
        }

        private void GenerateReport() => RequestGenerateReport?.Invoke(SelectedSetCodes());

        private void GenerateSheet() => RequestGenerateSheet?.Invoke(SelectedSetCodes());

        private List<string> SelectedSetCodes()
        {
            var list = (_filteredRows ?? _allRows) ?? new List<InvoiceRow>();
            var selectedRows = list.Where(r => r.Selected).ToList();
            // Null means "nothing checked" (callers fall back to the unfiltered report).
            // A non-null (possibly empty, if the checked rows have no resolvable SetCode)
            // list must still be honored as an explicit selection — otherwise checking a
            // row with no SetCode collapses to this returning null and the report silently
            // shows every invoice instead of the one the user picked.
            if (selectedRows.Count == 0) return null;
            return selectedRows.Select(r => r.SetCode).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        }
    }
}
