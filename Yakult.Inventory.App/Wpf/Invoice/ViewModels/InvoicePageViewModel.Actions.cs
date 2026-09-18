using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Yakult.Inventory.App.WPF.Invoice.ViewModels
{
    public sealed partial class InvoicePageViewModel
    {
        // ── Loading ──────────────────────────────────────────────────────────
        public async void LoadInvoices() => await LoadInvoicesAsync();

        public async Task LoadInvoicesAsync()
        {
            if (!Yakult.Inventory.App.Core.DatabaseConfig.IsConfigured)
            {
                RequestWarning?.Invoke(
                    "Connection Not Configured",
                    "Database connection is not configured.\n\nPlease restart the application and complete the database setup.");
                return;
            }

            try
            {
                var dt = await _invoiceRepository.GetAllInvoicesAsync(
                    _setCodeFilter, _documentNumberFilter, _companyFilter,
                    _departmentFilter, _branchFilter, _employeeFilter,
                    _referenceCodeFilter, _parentTagFilter, _reqIdFilter);
                var rows = new List<InvoiceRow>();
                var today = DateTime.Today;

                foreach (DataRow r in dt.Rows)
                {
                    var row = new InvoiceRow
                    {
                        SetId = r["SetId"] == DBNull.Value ? 0 : Convert.ToInt32(r["SetId"]),
                        SetCode = r["SetCode"] == DBNull.Value ? null : r["SetCode"].ToString(),
                        DocumentNumber = r["DocumentNumber"] == DBNull.Value ? null : r["DocumentNumber"].ToString(),
                        ReferenceNumber = r["ReferenceNumber"] == DBNull.Value ? null : r["ReferenceNumber"].ToString(),
                        DocumentDate = r["InvoiceDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["InvoiceDate"]),
                        StartDate = r["StartDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["StartDate"]),
                        EndDate = r["EndDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["EndDate"]),
                        Company = r["CompanyName"] == DBNull.Value ? null : r["CompanyName"].ToString(),
                        Distributor = r["DistributorName"] == DBNull.Value ? null : r["DistributorName"].ToString(),
                        Status = r["Status"] == DBNull.Value ? null : r["Status"].ToString(),
                        Remarks = r.Table.Columns.Contains("Remarks") && r["Remarks"] != DBNull.Value ? r["Remarks"].ToString() : null,
                        CreatedAt = r["CreatedAt"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["CreatedAt"]),
                        CreatedByName = r["CreatedByName"] == DBNull.Value ? null : r["CreatedByName"].ToString(),
                        Subtotal = r["Subtotal"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Subtotal"]),
                        VatAmount = r["VatAmount"] == DBNull.Value ? 0m : Convert.ToDecimal(r["VatAmount"]),
                        WhtAmount = r["WhtAmount"] == DBNull.Value ? 0m : Convert.ToDecimal(r["WhtAmount"]),
                        DiscountAmount = r["DiscountAmount"] == DBNull.Value ? 0m : Convert.ToDecimal(r["DiscountAmount"]),
                        TotalAmountDue = r["TotalAmountDue"] == DBNull.Value ? 0m : Convert.ToDecimal(r["TotalAmountDue"]),
                    };

                    row.DaysLeft = row.EndDate.HasValue ? (int)(row.EndDate.Value.Date - today).TotalDays : (int?)null;
                    rows.Add(row);
                }

                var setIds = rows.Select(r => r.SetId).ToList();
                _itemNamesBySetId = await Task.Run(() => _repository.GetItemNamesBySetIds(setIds));
                _serialNumbersBySetId = await Task.Run(() => _repository.GetSerialNumbersBySetIds(setIds));
                _parentTagLabelsBySetId = await Task.Run(() => _repository.GetParentTagLabelsBySetIds(setIds));
                _invoiceItemSearchFieldsBySetId = await Task.Run(() => _invoiceRepository.GetInvoiceItemSearchFieldsBySetIds(setIds));
                var categoriesBySetId = await Task.Run(() => _repository.GetCategoriesBySetIds(setIds));
                var itemTypesBySetId = await Task.Run(() => _repository.GetItemTypesBySetIds(setIds));
                var subTypesBySetId = await Task.Run(() => _repository.GetSubTypesBySetIds(setIds));

                foreach (var row in rows)
                {
                    row.Categories = categoriesBySetId.TryGetValue(row.SetId, out var cats) ? cats : Array.Empty<string>();
                    row.ItemTypes = itemTypesBySetId.TryGetValue(row.SetId, out var types) ? types : Array.Empty<string>();

                    if (subTypesBySetId.TryGetValue(row.SetId, out var subTypeEntry))
                    {
                        row.SubTypes = subTypeEntry.SubTypes;
                        row.HasNonSubTypeItems = subTypeEntry.HasNonSubType;
                        row.SubTypeReferenceCodes = subTypeEntry.ReferenceCodes;
                    }
                    else
                    {
                        // No dbo.SetItem rows at all (e.g. a pure Hardware/Request set) — has no
                        // Sub-Type items either, so it counts as Non-Subtype (InvoiceRow's default).
                        row.SubTypes = Array.Empty<string>();
                        row.HasNonSubTypeItems = true;
                        row.SubTypeReferenceCodes = Array.Empty<string>();
                    }
                }

                _allRows = rows;
                LoadCategoryFilterOptions();
                LoadItemTypeFilterOptions();
                LoadDocumentYearOptions();
                ApplyFilters(resetPage: true);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load invoice reports: {ex.Message}");
            }
        }

        // ── Row actions (act on checked rows, spanning all filtered pages) ──────
        private List<InvoiceRow> CheckedRows() =>
            (_filteredRows ?? _allRows ?? new List<InvoiceRow>()).Where(r => r.Selected).ToList();

        private void OpenSelected()
        {
            var checkedRows = CheckedRows();
            if (checkedRows.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please check an invoice to open.");
                return;
            }
            if (checkedRows.Count > 1)
            {
                RequestWarning?.Invoke("Multiple Selected", "Please check only one invoice to open.");
                return;
            }

            RequestOpenInvoice?.Invoke(checkedRows[0]);
        }

        private void EditSelected()
        {
            var checkedRows = CheckedRows();
            if (checkedRows.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please check an invoice to edit.");
                return;
            }
            if (checkedRows.Count > 1)
            {
                RequestWarning?.Invoke("Multiple Selected", "Please check only one invoice to edit.");
                return;
            }

            RequestEditInvoice?.Invoke(checkedRows[0]);
        }

        private void DeleteSelected()
        {
            var checkedRows = CheckedRows();
            if (checkedRows.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please check at least one invoice to delete.");
                return;
            }

            RequestDeleteInvoices?.Invoke(checkedRows);
        }

        /// <summary>Called by the View after the Archive/Permanently-Delete dialog is confirmed.</summary>
        public void CommitDeleteInvoices(List<InvoiceRow> rows, bool permanentlyDelete, string reason)
        {
            if (rows == null || rows.Count == 0) return;

            if (permanentlyDelete)
            {
                bool doubleConfirmed = ConfirmYesNo?.Invoke(
                    "Final Confirmation",
                    rows.Count == 1
                        ? "FINAL CONFIRMATION\n\nThis invoice will be permanently deleted and cannot be recovered.\n\nAre you absolutely certain?"
                        : $"FINAL CONFIRMATION\n\n{rows.Count} invoice(s) will be permanently deleted and cannot be recovered.\n\nAre you absolutely certain?") ?? false;
                if (!doubleConfirmed) return;
            }

            try
            {
                if (permanentlyDelete)
                {
                    foreach (var row in rows)
                        _serviceSetRepository.DeleteServiceSet(row.SetId);
                }
                else
                {
                    _invoiceRepository.ArchiveInvoiceSets(rows.Select(r => r.SetId), reason);
                }

                LoadInvoices();
            }
            catch (Exception ex)
            {
                var action = permanentlyDelete ? "delete" : "archive";
                RequestError?.Invoke("Error", $"Failed to {action} invoice(s):\n\n{ex.Message}");
            }
        }

        // ── Pagination ───────────────────────────────────────────────────────
        public ObservableCollection<int> PageSizeOptions { get; }

        private int _pageSize;
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
        public int CurrentPage
        {
            get => _currentPage;
            private set => SetField(ref _currentPage, value);
        }

        private string _pageInfoText = "Page 0 of 0 (0 invoices)";
        public string PageInfoText
        {
            get => _pageInfoText;
            private set => SetField(ref _pageInfoText, value);
        }

        private int GetTotalPages()
        {
            var total = _filteredRows?.Count ?? 0;
            return total <= 0 ? 0 : (int)Math.Ceiling(total / (double)PageSize);
        }

        private void BindPage()
        {
            var src = _filteredRows ?? _allRows ?? new List<InvoiceRow>();
            var totalPages = GetTotalPages();

            if (src.Count == 0)
            {
                Rows.Clear();
                PageInfoText = "Page 0 of 0 (0 invoices)";
                return;
            }

            CurrentPage = totalPages > 0 ? Math.Max(1, Math.Min(CurrentPage, totalPages)) : 1;

            var pageItems = src.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            Rows.Clear();
            foreach (var item in pageItems) Rows.Add(item);

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({src.Count} invoices)";
        }
    }
}
