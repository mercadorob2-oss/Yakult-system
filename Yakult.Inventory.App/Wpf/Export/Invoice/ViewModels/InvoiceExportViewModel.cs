using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Export.Invoice.ViewModels
{
    /// <summary>Row-display wrapper — the original page bound raw DataTable/DataRow values
    /// directly to the grid; DataRow has no Selected property to bind a checkbox to, so this
    /// wrapper carries the formatted display strings plus a reference back to the source
    /// DataRow (needed by the export dialog, which takes List&lt;DataRow&gt;).</summary>
    public class InvoiceExportRow
    {
        public bool Selected { get; set; }
        public string SetCode { get; set; }
        public string DocumentNumber { get; set; }
        public string ReferenceNumber { get; set; }
        public string InvoiceDate { get; set; }
        public string Site { get; set; }
        public string CompanyName { get; set; }
        public string Status { get; set; }
        public string Subtotal { get; set; }
        public string VatAmount { get; set; }
        public string TotalAmountDue { get; set; }
        public DataRow Source { get; set; }
    }

    /// <summary>
    /// Business logic for the Export Invoice page, ported verbatim from
    /// Pages\Export\ExportInvoice.cs. No Add/Edit/Archive/Delete and no Filter-By/Sort-By on
    /// this page (hidden in the original) — just search, paging, and "Export Selected" which
    /// opens ExportInvoicePreviewDialog (which self-contains its own Save flow).
    /// Select-All only affects the CURRENT PAGE's rows (matches the original's header-click
    /// handler, which iterated only `row.Visible` rows) — unlike ExportRenewal/ExportSets,
    /// whose Select-All spans every filtered row across all pages.
    /// </summary>
    public sealed class InvoiceExportViewModel : ViewModelBase
    {
        private readonly InvoiceRepository _repo = new InvoiceRepository(Core.DatabaseConfig.ConnectionString);

        private List<InvoiceExportRow> _allRows = new List<InvoiceExportRow>();
        private List<InvoiceExportRow> _filteredRows = new List<InvoiceExportRow>();

        private string _sortProperty;
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;

        public InvoiceExportViewModel()
        {
            PageSizeOptions = new ObservableCollection<string> { "10", "25", "50", "100" };
            _selectedPageSize = "25";
            PagedRows = new ObservableCollection<InvoiceExportRow>();

            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            ExportCommand = new RelayCommand(async () => await ExportAsync());
            BackCommand = new RelayCommand(() => RequestBack?.Invoke());
            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; ApplyPage(); });
            PrevPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, CurrentPage - 1); ApplyPage(); });
            NextPageCommand = new RelayCommand(() => { CurrentPage++; ApplyPage(); });
            LastPageCommand = new RelayCommand(() => { CurrentPage = TotalPages(); ApplyPage(); });
        }

        public event Action RequestBack;
        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestError;
        public event Action<List<DataRow>, DataTable> RequestExport;

        public ObservableCollection<InvoiceExportRow> PagedRows { get; }
        public ObservableCollection<string> PageSizeOptions { get; }

        private string _selectedPageSize;
        public string SelectedPageSize
        {
            get => _selectedPageSize;
            set
            {
                if (SetField(ref _selectedPageSize, value) && int.TryParse(value, out var size) && size > 0)
                {
                    _pageSize = size;
                    CurrentPage = 1;
                    ApplyPage();
                }
            }
        }
        private int _pageSize = 25;

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) RebuildFilter(); }
        }

        private bool _canExport;
        public bool CanExport { get => _canExport; private set => SetField(ref _canExport, value); }

        private string _countText = "0 invoice(s)";
        public string CountText { get => _countText; private set => SetField(ref _countText, value); }

        private int _currentPage = 1;
        public int CurrentPage { get => _currentPage; set => SetField(ref _currentPage, value); }

        private string _pageInfoText = "Page 1 of 1 (0 invoice(s))";
        public string PageInfoText { get => _pageInfoText; private set => SetField(ref _pageInfoText, value); }

        private bool _canFirstPage, _canPrevPage, _canNextPage, _canLastPage;
        public bool CanFirstPage { get => _canFirstPage; private set => SetField(ref _canFirstPage, value); }
        public bool CanPrevPage { get => _canPrevPage; private set => SetField(ref _canPrevPage, value); }
        public bool CanNextPage { get => _canNextPage; private set => SetField(ref _canNextPage, value); }
        public bool CanLastPage { get => _canLastPage; private set => SetField(ref _canLastPage, value); }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand BackCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        public async Task LoadAsync()
        {
            CanExport = false;
            CountText = "Loading…";
            try
            {
                var data = await _repo.GetAllInvoicesAsync();
                PopulateRows(data);
            }
            catch (Exception ex)
            {
                CountText = $"Error: {ex.Message}";
            }
        }

        private static string Fmt(DataRow row, string col)
            => row[col] != DBNull.Value ? Convert.ToDecimal(row[col]).ToString("N2") : "0.00";

        private void PopulateRows(DataTable data)
        {
            _allRows = new List<InvoiceExportRow>();
            foreach (DataRow row in data.Rows)
            {
                var dt = row["InvoiceDate"];
                _allRows.Add(new InvoiceExportRow
                {
                    Selected = false,
                    SetCode = row["SetCode"]?.ToString() ?? "",
                    DocumentNumber = row["DocumentNumber"]?.ToString() ?? "",
                    ReferenceNumber = row["ReferenceNumber"]?.ToString() ?? "",
                    InvoiceDate = dt != DBNull.Value ? Convert.ToDateTime(dt).ToString("MM/dd/yyyy") : "",
                    Site = row["Site"]?.ToString() ?? "",
                    CompanyName = row["CompanyName"]?.ToString() ?? "",
                    Status = row["Status"]?.ToString() ?? "",
                    Subtotal = Fmt(row, "Subtotal"),
                    VatAmount = Fmt(row, "VatAmount"),
                    TotalAmountDue = Fmt(row, "TotalAmountDue"),
                    Source = row
                });
            }

            RebuildFilter();
            CanExport = true;
        }

        private void RebuildFilter()
        {
            string q = SearchText?.Trim().ToLowerInvariant() ?? "";
            _filteredRows = string.IsNullOrEmpty(q)
                ? _allRows.ToList()
                : _allRows.Where(r =>
                    (r.SetCode?.ToLowerInvariant().Contains(q) ?? false) ||
                    (r.DocumentNumber?.ToLowerInvariant().Contains(q) ?? false) ||
                    (r.CompanyName?.ToLowerInvariant().Contains(q) ?? false) ||
                    (r.Site?.ToLowerInvariant().Contains(q) ?? false)).ToList();

            if (_sortProperty != null) ApplySort();

            CurrentPage = 1;
            ApplyPage();
        }

        public ListSortDirection SortByColumn(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return _sortDirection;
            _sortDirection = _sortProperty == propertyName && _sortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
            _sortProperty = propertyName;
            ApplySort();
            ApplyPage();
            return _sortDirection;
        }

        private void ApplySort()
        {
            var prop = typeof(InvoiceExportRow).GetProperty(_sortProperty);
            if (prop == null) return;
            _filteredRows = _sortDirection == ListSortDirection.Ascending
                ? _filteredRows.OrderBy(x => prop.GetValue(x, null)).ToList()
                : _filteredRows.OrderByDescending(x => prop.GetValue(x, null)).ToList();
        }

        private int TotalPages()
        {
            int total = _filteredRows.Count;
            return total == 0 ? 1 : (int)Math.Ceiling(total / (double)_pageSize);
        }

        private void ApplyPage()
        {
            int total = _filteredRows.Count;
            int pages = TotalPages();
            CurrentPage = Math.Max(1, Math.Min(CurrentPage, pages));

            var paged = _filteredRows.Skip((CurrentPage - 1) * _pageSize).Take(_pageSize).ToList();
            PagedRows.Clear();
            foreach (var r in paged) PagedRows.Add(r);

            int selAll = _allRows.Count(r => r.Selected);
            PageInfoText = $"Page {CurrentPage} of {pages} ({total} invoice(s))";
            CountText = $"{total} invoice(s)   |   {selAll} selected";

            CanFirstPage = CanPrevPage = CurrentPage > 1;
            CanNextPage = CanLastPage = CurrentPage < pages;
        }

        /// <summary>Select-All affects only the current page — matches the original.</summary>
        public void NotifySelectionChanged() => ApplyPage();

        /// <summary>Called by the View when the "select all" header checkbox is toggled.</summary>
        public void SetPageSelection(bool state)
        {
            foreach (var r in PagedRows) r.Selected = state;
            ApplyPage();
        }

        private async Task ExportAsync()
        {
            var selectedHeaders = _allRows.Where(r => r.Selected).Select(r => r.Source).ToList();
            if (selectedHeaders.Count == 0)
            {
                RequestInfo?.Invoke("No Selection", "Please select at least one invoice to export.");
                return;
            }

            CanExport = false;
            CountText = "Loading details…";

            try
            {
                var allItems = new DataTable();
                bool schemaSet = false;

                foreach (var hdr in selectedHeaders)
                {
                    int setId = Convert.ToInt32(hdr["SetId"]);
                    var items = await _repo.GetInvoiceItemsAsync(setId);
                    if (!schemaSet && items.Columns.Count > 0)
                    {
                        foreach (DataColumn c in items.Columns)
                            allItems.Columns.Add(c.ColumnName, c.DataType);
                        schemaSet = true;
                    }
                    if (!schemaSet) continue;

                    foreach (DataRow r in items.Rows)
                    {
                        var nr = allItems.NewRow();
                        foreach (DataColumn c in items.Columns)
                            if (allItems.Columns.Contains(c.ColumnName))
                                nr[c.ColumnName] = r[c.ColumnName];
                        allItems.Rows.Add(nr);
                    }
                }

                RequestExport?.Invoke(selectedHeaders, allItems);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Error loading invoice data:\n{ex.Message}");
            }
            finally
            {
                CanExport = true;
                ApplyPage();
            }
        }
    }
}
