using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Dialogs;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages.Receipt;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Invoice
{
    /// <summary>
    /// WPF replacement for the legacy WinForms ViewInvoiceDetailPage — same namespace,
    /// class name, and constructor signature, with WinForms-compatible ShowDialog()
    /// overloads (same pattern as SoftwareServiceSetDialog/RecordSetInvoiceDialog), so
    /// every existing call site (ReceiptSetViewerDialog, ViewInvoicePage, InvoicePageView)
    /// keeps working unchanged.
    /// </summary>
    public partial class ViewInvoiceDetailPage : Window, IDisposable
    {
        private readonly InvoiceRepository _invoiceRepository;
        private readonly ReceiptSetRepository _receiptSetRepository;
        private readonly int _setId;
        private DataRow _invoiceData;
        private DataTable _companiesData;
        private DataTable _distributorsData;
        private DataTable _siteCompaniesData;
        private DataTable _branchesData;
        private DataTable _departmentsData;
        private DataTable _employeesData;
        private bool _isEditMode;
        private bool _invoiceItemsDirty;
        private DataTable _itemsTable;

        private bool _isInitializingSiteBuilder;

        private DateTime? _originalHeaderStartDate;
        private DateTime? _originalHeaderEndDate;

        private int? _linkedReceiptSetId;
        private string _setCode = "";

        // True only when DgvItems is bound to real SetItem rows (GetInvoiceItemsAsync /
        // GetSetItemsAsync) rather than the Request-based fallback (GetRequestItemsAsInvoiceItemsAsync)
        // — Request-based "invoice in place" Sets have no real SetItemId to tag, so the
        // per-item Sub-Type column is hidden for them.
        private bool _itemsAreRealSetItems;

        private DataGridColumn _subTypeColumn;
        private DataGridColumn _selectedColumn;

        public ViewInvoiceDetailPage(int setId)
        {
            InitializeComponent();
            WindowState = WindowState.Maximized;
            BuildExtraGroupStyles();

            string connectionString = DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                MessageBox.Show(this, "Connection string not configured.", "Config Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _invoiceRepository = new InvoiceRepository(connectionString);
            _receiptSetRepository = new ReceiptSetRepository();
            _setId = setId;

            Loaded += async (s, e) =>
            {
                await LoadDataAsync();
                SetEditMode(false);
            };
        }

        // ── WinForms-compatible ShowDialog overloads ─────────────────────────
        public new WinForms.DialogResult ShowDialog()
        {
            bool? result = base.ShowDialog();
            return result == true ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }

        public WinForms.DialogResult ShowDialog(WinForms.IWin32Window owner)
        {
            if (owner != null)
                new System.Windows.Interop.WindowInteropHelper(this).Owner = owner.Handle;
            return ShowDialog();
        }

        public void Dispose() { }

        private WinForms.IWin32Window GetWin32Owner()
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            return new Yakult.Inventory.App.WPF.Shared.Win32WindowWrapper(hwnd);
        }

        // ── Receipt linking ───────────────────────────────────────────────────

        private void BtnLinkReceiptSet_Click(object sender, RoutedEventArgs e) => LinkReceiptSet();

        private void BtnAttachReceipt_Click(object sender, RoutedEventArgs e) => AttachReceiptDirectly();

        private void AttachReceiptDirectly()
        {
            try
            {
                // Distributor (dbo.Distributor) and Supplier/Vendor (dbo.Vendor) are different
                // entities -- the Receipt's Supplier field must not be prefilled from the
                // Invoice's Distributor, since the Distributor name won't generally exist in
                // dbo.Vendor and would fail the "must match an existing vendor" validation.
                Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForSet(this, _setId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to open receipt set viewer: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RefreshLinkedReceiptSetState();
            }
        }

        private void LinkReceiptSet()
        {
            try
            {
                RefreshLinkedReceiptSetState();

                using (var dialog = new AttachReceiptSetDialog(_setId))
                {
                    if (dialog.ShowDialog(GetWin32Owner()) != WinForms.DialogResult.OK || !dialog.SelectedReceiptSetId.HasValue)
                        return;

                    var selectedReceiptSetId = dialog.SelectedReceiptSetId.Value;
                    if (selectedReceiptSetId <= 0)
                        return;

                    if (_linkedReceiptSetId.HasValue && _linkedReceiptSetId.Value > 0 && _linkedReceiptSetId.Value != selectedReceiptSetId)
                    {
                        var confirm = MessageBox.Show(
                            this,
                            "This invoice already has a linked receipt set.\n\nReplace it with the selected receipt set?",
                            "Link Receipt Set",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (confirm != MessageBoxResult.Yes)
                            return;

                        int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                        _receiptSetRepository.UnlinkReceiptSet(_linkedReceiptSetId.Value, _setId, userId);
                    }

                    {
                        int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                        _receiptSetRepository.AttachReceiptSetToSet(selectedReceiptSetId, _setId, userId);
                    }
                }

                RefreshLinkedReceiptSetState();

                MessageBox.Show(
                    this,
                    "Receipt set linked successfully.",
                    "Receipt Set",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to link receipt set: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshLinkedReceiptSetState()
        {
            _linkedReceiptSetId = null;

            try
            {
                _linkedReceiptSetId = _receiptSetRepository.GetLinkedReceiptSetId(_setId);
            }
            catch
            {
                _linkedReceiptSetId = null;
            }
        }

        private void OpenLinkedReceiptSetViewer()
        {
            RefreshLinkedReceiptSetState();

            if (!_linkedReceiptSetId.HasValue || _linkedReceiptSetId.Value <= 0)
            {
                MessageBox.Show(this, "No receipt set is linked to this invoice yet.", "Receipt Set",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var dto = _receiptSetRepository.GetByReceiptSetId(_linkedReceiptSetId.Value)
                    ?? new ReceiptSetDto { ReceiptSetId = _linkedReceiptSetId.Value };
                dto.SetId = _setId;
                dto.SetCode = _setCode;

                Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForReceipt(this, dto, readOnly: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to open receipt set viewer: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Load ──────────────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                _invoiceData = await _invoiceRepository.GetInvoiceByIdAsync(_setId);

                if (_invoiceData == null)
                {
                    MessageBox.Show(this, "Invoice not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                _companiesData = await _invoiceRepository.GetAllCompaniesAsync();
                BindLookupWithNone(CmbCompany, _companiesData, "ComId", "CompanyName");

                _distributorsData = await _invoiceRepository.GetAllDistributorsAsync();
                BindLookupWithNone(CmbDistributor, _distributorsData, "DistributorId", "DistributorName");

                _siteCompaniesData = await _invoiceRepository.GetAllCompaniesAsync();
                _branchesData = await _invoiceRepository.GetAllBranchesAsync();
                _departmentsData = await _invoiceRepository.GetAllDepartmentsAsync();

                BindLookupWithNone(CmbSiteCompany, _siteCompaniesData, "ComId", "CompanyName");
                BindLookupWithNone(CmbSiteBranch, _branchesData, "BranchId", "BranchName");
                BindLookupWithNone(CmbSiteDepartment, _departmentsData, "DeptId", "DepartmentName");

                _employeesData = await _invoiceRepository.GetAllEmployeesAsync();
                BindLookupWithNone(CmbRequestedBy, _employeesData, "EmpId", "Name");

                PopulateFields();
                RefreshLinkedReceiptSetState();

                await LoadInvoiceItemsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error loading invoice details: {ex.Message}\n\n{ex.StackTrace}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static DataTable CreateLookupWithNone(DataTable source, string idColumn, string displayColumn)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var table = source.Copy();

            if (!table.Columns.Contains(idColumn) || !table.Columns.Contains(displayColumn))
                return table;

            var noneRow = table.NewRow();
            noneRow[idColumn] = DBNull.Value;
            noneRow[displayColumn] = "(None)";
            table.Rows.InsertAt(noneRow, 0);

            return table;
        }

        private void BindLookupWithNone(ComboBox combo, DataTable data, string idColumn, string displayColumn)
        {
            var bound = CreateLookupWithNone(data, idColumn, displayColumn);
            combo.ItemsSource = bound.DefaultView;
        }

        private void SiteBuilder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializingSiteBuilder)
                return;

            UpdateSiteFromBuilder();
        }

        private void SiteBuilder_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isInitializingSiteBuilder)
                return;

            UpdateSiteFromBuilder();
        }

        private static string GetSelectedTextOrNull(ComboBox combo)
        {
            var text = combo?.Text?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private void UpdateSiteFromBuilder()
        {
            var parts = new List<string>();

            var company = GetSelectedTextOrNull(CmbSiteCompany);
            var branch = GetSelectedTextOrNull(CmbSiteBranch);
            var dept = GetSelectedTextOrNull(CmbSiteDepartment);

            if (!string.IsNullOrWhiteSpace(company)) parts.Add(company.Trim());
            if (!string.IsNullOrWhiteSpace(branch)) parts.Add(branch.Trim());
            if (!string.IsNullOrWhiteSpace(dept)) parts.Add(dept.Trim());

            TxtSite.Text = parts.Count > 0 ? string.Join(" - ", parts) : string.Empty;
        }

        private void PopulateFields()
        {
            var prepBy = (_invoiceData["CreatedByName"] == DBNull.Value || _invoiceData["CreatedByName"] == null)
                         ? "-" : _invoiceData["CreatedByName"].ToString();
            LblCreatedBy.Text = "Created By: " + prepBy;

            _setCode = _invoiceData["SetCode"]?.ToString() ?? "-";
            TxtSetCode.Text = _setCode;

            if (_invoiceData["InvoiceDate"] != DBNull.Value)
                DtpDate.SelectedDate = Convert.ToDateTime(_invoiceData["InvoiceDate"]);

            if (_invoiceData["ComId"] != DBNull.Value)
                CmbCompany.SelectedValue = Convert.ToInt32(_invoiceData["ComId"]);

            if (_invoiceData.Table.Columns.Contains("DistributorId") && _invoiceData["DistributorId"] != DBNull.Value)
                CmbDistributor.SelectedValue = Convert.ToInt32(_invoiceData["DistributorId"]);
            else
                CmbDistributor.SelectedIndex = 0;

            TxtDocumentNumber.Text = _invoiceData["DocumentNumber"]?.ToString() ?? "";

            string status = _invoiceData["Status"]?.ToString() ?? "";
            if (status.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                CmbStatus.SelectedIndex = 0;
            else if (status.Equals("No", StringComparison.OrdinalIgnoreCase))
                CmbStatus.SelectedIndex = 1;
            else
                CmbStatus.SelectedIndex = -1;

            TxtReferenceNumber.Text = _invoiceData["ReferenceNumber"]?.ToString() ?? "";
            TxtSite.Text = _invoiceData["Site"]?.ToString() ?? "";

            // Employee-level detail — the stored invoice requester (dbo.[Set].InvoiceRequesterEmpId),
            // falling back to the originating Request's employee. "(None)" when neither exists.
            if (_invoiceData.Table.Columns.Contains("RequestedByEmpId") && _invoiceData["RequestedByEmpId"] != DBNull.Value)
                CmbRequestedBy.SelectedValue = Convert.ToInt32(_invoiceData["RequestedByEmpId"]);
            else
                CmbRequestedBy.SelectedIndex = 0;
            // Older invoices had "[Software/Service] " auto-prefixed onto Remarks by
            // ServiceSetRepository — strip it so it doesn't look like user input.
            string remarks = _invoiceData["Remarks"]?.ToString() ?? "";
            const string legacyPrefix = "[Software/Service] ";
            if (remarks.StartsWith(legacyPrefix, StringComparison.Ordinal))
                remarks = remarks.Substring(legacyPrefix.Length);
            TxtRemarks.Text = remarks;

            _isInitializingSiteBuilder = true;
            try
            {
                if (_invoiceData.Table.Columns.Contains("SiteCompanyId") && _invoiceData["SiteCompanyId"] != DBNull.Value)
                    CmbSiteCompany.SelectedValue = Convert.ToInt32(_invoiceData["SiteCompanyId"]);
                else
                    CmbSiteCompany.SelectedIndex = 0;

                if (_invoiceData.Table.Columns.Contains("CurrentBranchId") && _invoiceData["CurrentBranchId"] != DBNull.Value)
                    CmbSiteBranch.SelectedValue = Convert.ToInt32(_invoiceData["CurrentBranchId"]);
                else
                    CmbSiteBranch.SelectedIndex = 0;

                if (_invoiceData.Table.Columns.Contains("CurrentDepartmentId") && _invoiceData["CurrentDepartmentId"] != DBNull.Value)
                    CmbSiteDepartment.SelectedValue = Convert.ToInt32(_invoiceData["CurrentDepartmentId"]);
                else
                    CmbSiteDepartment.SelectedIndex = 0;
            }
            finally
            {
                _isInitializingSiteBuilder = false;
            }

            if (string.IsNullOrWhiteSpace(TxtSite.Text))
                UpdateSiteFromBuilder();

            if (_invoiceData.Table.Columns.Contains("StartDate") && _invoiceData["StartDate"] != DBNull.Value)
            {
                _originalHeaderStartDate = Convert.ToDateTime(_invoiceData["StartDate"]);
                DtpStartDate.SelectedDate = _originalHeaderStartDate.Value;
            }
            else
            {
                _originalHeaderStartDate = null;
                DtpStartDate.SelectedDate = DateTime.Today;
            }

            if (_invoiceData.Table.Columns.Contains("EndDate") && _invoiceData["EndDate"] != DBNull.Value)
            {
                _originalHeaderEndDate = Convert.ToDateTime(_invoiceData["EndDate"]);
                DtpEndDate.SelectedDate = _originalHeaderEndDate.Value;
            }
            else
            {
                _originalHeaderEndDate = null;
                DtpEndDate.SelectedDate = DateTime.Today.AddYears(1);
            }

            TxtSubtotal.Text = _invoiceData["Subtotal"] != DBNull.Value ?
                Convert.ToDecimal(_invoiceData["Subtotal"]).ToString("N2") : "0.00";

            // Derive percentage fields from the stored dollar amounts so they reflect
            // what was actually saved rather than hardcoded defaults. Inverse of
            // TryCalculateFinancialsFromPercentages().
            decimal loadedSubtotal = _invoiceData["Subtotal"] != DBNull.Value ? Convert.ToDecimal(_invoiceData["Subtotal"]) : 0m;
            decimal loadedVat = _invoiceData["VatAmount"] != DBNull.Value ? Convert.ToDecimal(_invoiceData["VatAmount"]) : 0m;
            decimal loadedWht = _invoiceData["WhtAmount"] != DBNull.Value ? Convert.ToDecimal(_invoiceData["WhtAmount"]) : 0m;
            decimal loadedDiscount = _invoiceData["DiscountAmount"] != DBNull.Value ? Convert.ToDecimal(_invoiceData["DiscountAmount"]) : 0m;
            decimal loadedTotal = _invoiceData["TotalAmountDue"] != DBNull.Value ? Convert.ToDecimal(_invoiceData["TotalAmountDue"]) : 0m;

            decimal netAfterDiscount = loadedTotal - loadedVat + loadedWht;

            decimal vatPct = (netAfterDiscount > 0) ? Math.Round(loadedVat / netAfterDiscount * 100m, 2) : 0m;
            decimal whtPct = (netAfterDiscount > 0) ? Math.Round(loadedWht / netAfterDiscount * 100m, 2) : 0m;
            decimal discountPct = (loadedSubtotal > 0) ? Math.Round(loadedDiscount / loadedSubtotal * 100m, 2) : 0m;

            TxtVatAmount.Text = vatPct.ToString("N2");
            TxtWhtAmount.Text = whtPct.ToString("N2");
            TxtDiscountAmount.Text = discountPct.ToString("N2");

            TxtTotalAmountDue.Text = _invoiceData["TotalAmountDue"] != DBNull.Value ?
                Convert.ToDecimal(_invoiceData["TotalAmountDue"]).ToString("N2") : "0.00";

            int? daysRemainingNullable = null;
            if (_invoiceData.Table.Columns.Contains("EndDate") && _invoiceData["EndDate"] != DBNull.Value)
            {
                DateTime endDate = Convert.ToDateTime(_invoiceData["EndDate"]);
                daysRemainingNullable = (int)(endDate.Date - DateTime.Today).TotalDays;
            }

            int daysRemaining = daysRemainingNullable ?? 0;

            if (TxtDaysLeft != null)
                TxtDaysLeft.Text = daysRemainingNullable.HasValue ? daysRemaining.ToString() : "N/A";

            if (LblExpiryWarning != null)
            {
                string warningMessage;
                Brush warningBrush;

                if (!daysRemainingNullable.HasValue) { warningMessage = "End date not set"; warningBrush = Brushes.Gray; }
                else if (daysRemaining < 0) { warningMessage = "LICENSE EXPIRED"; warningBrush = Brushes.Red; }
                else if (daysRemaining < 60) { warningMessage = "1 month before end date"; warningBrush = Brushes.Red; }
                else if (daysRemaining < 90) { warningMessage = "2 months before end date"; warningBrush = Brushes.Orange; }
                else if (daysRemaining < 120) { warningMessage = "3 months before end date"; warningBrush = Brushes.Goldenrod; }
                else { warningMessage = "More than 3 months before end date"; warningBrush = Brushes.Green; }

                LblExpiryWarning.Text = warningMessage;
                LblExpiryWarning.Foreground = warningBrush;
            }
        }

        // ── Invoice items grid ────────────────────────────────────────────────

        private async System.Threading.Tasks.Task LoadInvoiceItemsAsync()
        {
            try
            {
                DataTable itemsData = await _invoiceRepository.GetInvoiceItemsAsync(_setId);
                _itemsAreRealSetItems = itemsData.Rows.Count > 0;

                if (itemsData.Rows.Count == 0)
                {
                    itemsData = await GetSetItemsAsync(_setId);
                    _itemsAreRealSetItems = itemsData.Rows.Count > 0;
                }

                if (itemsData.Rows.Count == 0)
                {
                    itemsData = await _invoiceRepository.GetRequestItemsAsInvoiceItemsAsync(_setId);
                    _itemsAreRealSetItems = false;
                }

                if (_itemsTable != null)
                    _itemsTable.ColumnChanged -= ItemsData_ColumnChanged;

                // A plain UI flag, not invoice data — kept out of ItemsData_ColumnChanged's
                // dirty-tracking whitelist so checking a row never forces a Save/Cancel prompt.
                // Ordinal 0 puts it immediately left of SetItemId once auto-generated.
                var selectedCol = itemsData.Columns.Add("Selected", typeof(bool));
                selectedCol.SetOrdinal(0);
                foreach (DataRow r in itemsData.Rows)
                    r["Selected"] = false;

                _itemsTable = itemsData;
                _itemsTable.ColumnChanged += ItemsData_ColumnChanged;

                _subTypeColumn = null;
                _selectedColumn = null;

                var itemsView = CollectionViewSource.GetDefaultView(_itemsTable.DefaultView);
                itemsView.GroupDescriptions.Clear();
                // Outer -> inner, per the confirmed hierarchy: Sub-Type Group (existing) wraps
                // Parent Tag (new), which wraps Category (new).
                itemsView.GroupDescriptions.Add(new PropertyGroupDescription(null, new SubTypeGroupKeyConverter()));
                itemsView.GroupDescriptions.Add(new PropertyGroupDescription(null, new ParentTagGroupKeyConverter()));
                itemsView.GroupDescriptions.Add(new PropertyGroupDescription(null, new CategoryGroupKeyConverter()));
                DgvItems.ItemsSource = itemsView;

                TxtItemCount.Text = itemsData.Rows.Count.ToString();

                if (_isEditMode)
                    CalculateTotalAmountDue();

                ConfigureInvoiceItemsGridEditMode(_isEditMode);
                LoadGroupSummaryPanel();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error loading invoice items: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private sealed class SubTypeGroupSummary
        {
            public int GroupId { get; set; }
            public string SubType { get; set; }
            public string ReferenceCode { get; set; }
            public DateTime? BeginDate { get; set; }
            public DateTime? EndDate { get; set; }
            /// <summary>Computed SUM(SetItem.Amount) — only used as a fallback when
            /// SubtotalOverride is null (never overridden yet).</summary>
            public decimal Subtotal { get; set; }
            public decimal? SubtotalOverride { get; set; }
            public decimal? VatPercent { get; set; }
            public decimal? WhtPercent { get; set; }
            public decimal? DiscountPercent { get; set; }
        }

        private List<SubTypeGroupSummary> _subTypeGroups = new List<SubTypeGroupSummary>();

        /// <summary>Holds the live controls for one rendered group card. Each card has its
        /// own independent Subtotal/VAT%/WHT%/Discount% inputs and recalculates its own
        /// dollar VAT/WHT/Discount/Total live as the user types — see
        /// RecalculateHeaderFinancials for how (and when, via the "Calculate" button) a
        /// card's own computed dollar amounts feed into the invoice header's totals.</summary>
        private sealed class GroupCardControls
        {
            public SubTypeGroupSummary Group { get; set; }
            public TextBox SubtotalBox { get; set; }
            public TextBox VatBox { get; set; }
            public TextBox WhtBox { get; set; }
            public TextBox DiscountBox { get; set; }
            public TextBlock TotalText { get; set; }
            public Button RemoveButton { get; set; }
            public decimal LastComputedVat { get; set; }
            public decimal LastComputedWht { get; set; }
            public decimal LastComputedDiscount { get; set; }
            public decimal LastComputedTotal { get; set; }
        }

        private List<GroupCardControls> _groupCards = new List<GroupCardControls>();

        /// <summary>Loads one entry per Sub-Type Group (Contract/Subscription/License/
        /// Services) attached to this invoice — reference #, date range, and subtotal —
        /// sourced from dbo.vw_SetItemSubTypeGroups (a group is just several dbo.SetItem
        /// rows sharing the same SubType + ReferenceCode, not a separate entity) — then
        /// renders their summary cards.</summary>
        private void LoadGroupSummaryPanel()
        {
            var groups = new List<SubTypeGroupSummary>();
            DatabaseConfig.EnsureConfigured();
            const string sql = @"
                SELECT GroupId, SubType, ReferenceCode, BeginDate, EndDate, Subtotal,
                       SubtotalOverride, VatPercent, WhtPercent, DiscountPercent
                FROM dbo.vw_SetItemSubTypeGroups
                WHERE SetId = @SetId";
            using (var connection = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@SetId", _setId);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            groups.Add(new SubTypeGroupSummary
                            {
                                GroupId = Convert.ToInt32(reader["GroupId"]),
                                SubType = reader["SubType"].ToString(),
                                ReferenceCode = reader["ReferenceCode"] == DBNull.Value ? null : reader["ReferenceCode"].ToString(),
                                BeginDate = reader["BeginDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["BeginDate"]),
                                EndDate = reader["EndDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["EndDate"]),
                                Subtotal = Convert.ToDecimal(reader["Subtotal"]),
                                SubtotalOverride = reader["SubtotalOverride"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader["SubtotalOverride"]),
                                VatPercent = reader["VatPercent"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader["VatPercent"]),
                                WhtPercent = reader["WhtPercent"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader["WhtPercent"]),
                                DiscountPercent = reader["DiscountPercent"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader["DiscountPercent"])
                            });
                        }
                    }
                }
            }

            _subTypeGroups = groups;
            RenderGroupSummaryCards();
            LoadParentTagGroups();
        }

        /// <summary>One entry per Parent Tag group attached to this invoice — free-text label,
        /// item count, and rolled-up subtotal — sourced from dbo.vw_SetItemParentTagGroups.
        /// Unlike Sub-Type groups, there is no summary-card panel: the rollup is shown directly
        /// in the group's banner header in the grid (see BuildParentTagGroupStyle), and there
        /// are no per-group financial overrides to edit.</summary>
        private sealed class ParentTagGroupSummary
        {
            public int ParentTagGroupId { get; set; }
            public string Label { get; set; }
            public int ItemCount { get; set; }
            public decimal Subtotal { get; set; }
        }

        private List<ParentTagGroupSummary> _parentTagGroups = new List<ParentTagGroupSummary>();

        private void LoadParentTagGroups()
        {
            var groups = new List<ParentTagGroupSummary>();
            const string sql = @"
                SELECT ParentTagGroupId, Label, ItemCount, Subtotal
                FROM dbo.vw_SetItemParentTagGroups
                WHERE SetId = @SetId";
            using (var connection = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@SetId", _setId);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            groups.Add(new ParentTagGroupSummary
                            {
                                ParentTagGroupId = Convert.ToInt32(reader["ParentTagGroupId"]),
                                Label = reader["Label"].ToString(),
                                ItemCount = Convert.ToInt32(reader["ItemCount"]),
                                Subtotal = Convert.ToDecimal(reader["Subtotal"])
                            });
                        }
                    }
                }
            }

            _parentTagGroups = groups;
        }

        private static readonly SolidColorBrush GroupCardAccentBrush = new SolidColorBrush(Color.FromRgb(0x1B, 0x6E, 0xC2));
        private static readonly SolidColorBrush GroupCardBorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xE2, 0xE8));
        private static readonly SolidColorBrush GroupCardSeparatorBrush = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));
        private static readonly SolidColorBrush GroupCardMutedBrush = new SolidColorBrush(Color.FromRgb(0x70, 0x78, 0x80));

        /// <summary>Redraws the group summary cards from the already-loaded
        /// <see cref="_subTypeGroups"/> list — no DB round trip. Each card gets its own
        /// independent Subtotal/VAT%/WHT%/Discount% inputs, pre-filled from the invoice
        /// header's current percentages as a starting default only — editing a card's own
        /// fields afterwards never affects the header or any other card (only Subtotal
        /// flows back up into the header's Subtotal field, per the existing design).</summary>
        private void RenderGroupSummaryCards()
        {
            GroupSummaryPanel.Children.Clear();
            _groupCards.Clear();
            GroupSummaryPanel.Visibility = _subTypeGroups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            decimal.TryParse(TxtVatAmount.Text, out decimal defaultVatPercent);
            decimal.TryParse(TxtWhtAmount.Text, out decimal defaultWhtPercent);
            decimal.TryParse(TxtDiscountAmount.Text, out decimal defaultDiscountPercent);

            foreach (var group in _subTypeGroups)
            {
                var card = new Border
                {
                    BorderBrush = GroupCardBorderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(14, 12, 14, 12),
                    Margin = new Thickness(0, 0, 10, 10),
                    Background = Brushes.White,
                    MinWidth = 260
                };

                var stack = new StackPanel();

                stack.Children.Add(new TextBlock
                {
                    Text = group.SubType,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = GroupCardAccentBrush
                });

                var infoGrid = new Grid { Margin = new Thickness(0, 4, 0, 8) };
                infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                for (int i = 0; i < 3; i++)
                    infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                void AddInfoRow(int row, string label, string value)
                {
                    var labelBlock = new TextBlock
                    {
                        Text = label,
                        FontSize = 11,
                        Foreground = GroupCardMutedBrush,
                        Margin = new Thickness(0, 1, 10, 1)
                    };
                    Grid.SetRow(labelBlock, row);
                    Grid.SetColumn(labelBlock, 0);
                    infoGrid.Children.Add(labelBlock);

                    var valueBlock = new TextBlock
                    {
                        Text = value,
                        FontSize = 11,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 1, 0, 1)
                    };
                    Grid.SetRow(valueBlock, row);
                    Grid.SetColumn(valueBlock, 1);
                    infoGrid.Children.Add(valueBlock);
                }

                AddInfoRow(0, ItemSubTypeCatalog.GetReferenceCodeLabel(group.SubType), group.ReferenceCode ?? "(none)");
                AddInfoRow(1, "Start Date", group.BeginDate?.ToString("yyyy-MM-dd") ?? "Not set");
                AddInfoRow(2, "End Date", group.EndDate?.ToString("yyyy-MM-dd") ?? "Not set");

                stack.Children.Add(infoGrid);

                var fieldsGrid = new Grid();
                fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                for (int i = 0; i < 4; i++)
                    fieldsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                TextBox AddFieldRow(int row, string label, string value)
                {
                    var labelBlock = new TextBlock
                    {
                        Text = label,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 3, 10, 3)
                    };
                    Grid.SetRow(labelBlock, row);
                    Grid.SetColumn(labelBlock, 0);
                    fieldsGrid.Children.Add(labelBlock);

                    var box = new TextBox
                    {
                        Text = value,
                        Width = 90,
                        TextAlignment = TextAlignment.Right,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 3, 0, 3),
                        IsReadOnly = !_isEditMode
                    };
                    Grid.SetRow(box, row);
                    Grid.SetColumn(box, 1);
                    fieldsGrid.Children.Add(box);
                    return box;
                }

                var subtotalBox = AddFieldRow(0, "Subtotal", (group.SubtotalOverride ?? group.Subtotal).ToString("N2"));
                var vatBox = AddFieldRow(1, "VAT (%)", (group.VatPercent ?? defaultVatPercent).ToString("N2"));
                var whtBox = AddFieldRow(2, "WHT (%)", (group.WhtPercent ?? defaultWhtPercent).ToString("N2"));
                var discountBox = AddFieldRow(3, "Discount (%)", (group.DiscountPercent ?? defaultDiscountPercent).ToString("N2"));

                stack.Children.Add(fieldsGrid);

                stack.Children.Add(new Border
                {
                    Height = 1,
                    Background = GroupCardSeparatorBrush,
                    Margin = new Thickness(0, 10, 0, 8)
                });

                var totalRow = new Grid();
                totalRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                totalRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var totalLabel = new TextBlock { Text = "Total", FontWeight = FontWeights.Bold, FontSize = 14 };
                var totalText = new TextBlock
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Foreground = GroupCardAccentBrush
                };
                Grid.SetColumn(totalLabel, 0);
                Grid.SetColumn(totalText, 1);
                totalRow.Children.Add(totalLabel);
                totalRow.Children.Add(totalText);
                stack.Children.Add(totalRow);

                var btnRemoveGroup = new Button
                {
                    Content = "Remove from Invoice",
                    Margin = new Thickness(0, 10, 0, 0),
                    Padding = new Thickness(8, 4, 8, 4),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x2A, 0x2A)),
                    Visibility = _isEditMode ? Visibility.Visible : Visibility.Collapsed
                };
                btnRemoveGroup.Click += (s, e) => RemoveGroupFromInvoice_Click(group);
                stack.Children.Add(btnRemoveGroup);

                card.Child = stack;
                GroupSummaryPanel.Children.Add(card);

                var controls = new GroupCardControls
                {
                    Group = group,
                    SubtotalBox = subtotalBox,
                    VatBox = vatBox,
                    WhtBox = whtBox,
                    DiscountBox = discountBox,
                    TotalText = totalText,
                    RemoveButton = btnRemoveGroup
                };
                _groupCards.Add(controls);

                // Each group card recalculates its own Total live, as the user types — and
                // the header's Subtotal (sum of every card's Total) follows along live too.
                // The header's VAT/WHT/Discount%/Total Amount Due still only update when the
                // user clicks "Calculate".
                subtotalBox.TextChanged += (s, e) => { RecalculateGroupCardTotal(controls); UpdateHeaderSubtotalFromGroups(); };
                vatBox.TextChanged += (s, e) => { RecalculateGroupCardTotal(controls); UpdateHeaderSubtotalFromGroups(); };
                whtBox.TextChanged += (s, e) => { RecalculateGroupCardTotal(controls); UpdateHeaderSubtotalFromGroups(); };
                discountBox.TextChanged += (s, e) => { RecalculateGroupCardTotal(controls); UpdateHeaderSubtotalFromGroups(); };

                RecalculateGroupCardTotal(controls);
            }

            // Deliberately does NOT call RecalculateHeaderFinancials()/UpdateHeaderSubtotalFromGroups()
            // here: the header's Subtotal/VAT/WHT/Discount/Total Amount Due were just loaded from
            // the database by PopulateFields and must be left exactly as persisted until the user
            // actually edits a group card or clicks "Calculate". Recomputing here would silently
            // overwrite the saved Subtotal with the sum of the freshly-rebuilt cards' own Subtotal
            // boxes (which always reflect the current dbo.SetItem.Amount truth, not any override the
            // user previously typed and saved) — exactly the "resets to zero on reload" bug.
        }

        /// <summary>Recomputes a single group card's Total from its own Subtotal/VAT%/WHT%/
        /// Discount% inputs — fully independent of the invoice header and every other card.</summary>
        private void RecalculateGroupCardTotal(GroupCardControls card)
        {
            decimal.TryParse(card.SubtotalBox.Text, out decimal subtotal);
            decimal.TryParse(card.VatBox.Text, out decimal vatPercent);
            decimal.TryParse(card.WhtBox.Text, out decimal whtPercent);
            decimal.TryParse(card.DiscountBox.Text, out decimal discountPercent);

            // Same calculation chain as TryCalculateFinancialsFromPercentages, applied to
            // this group's own subtotal and its own percentages instead of the invoice-wide ones.
            decimal discount = subtotal * (discountPercent / 100m);
            decimal netAfterDiscount = subtotal - discount;
            decimal vat = netAfterDiscount * (vatPercent / 100m);
            decimal wht = netAfterDiscount * (whtPercent / 100m);
            decimal total = netAfterDiscount + vat - wht;

            card.LastComputedDiscount = discount;
            card.LastComputedVat = vat;
            card.LastComputedWht = wht;
            card.LastComputedTotal = total;
            card.TotalText.Text = total.ToString("N2");
        }

        /// <summary>Keeps the header's Subtotal live — the sum of every group card's own
        /// Total (each of which already nets out that group's own VAT/WHT/Discount). Called
        /// on every card keystroke, unlike the rest of the header's math, so the Subtotal
        /// never needs the "Calculate" button to stay current. No-op when there are no
        /// sub-type groups (Subtotal is then driven by the raw item grid instead, see
        /// RecalculateSubtotalFromGrid).</summary>
        private void UpdateHeaderSubtotalFromGroups()
        {
            if (_groupCards.Count == 0) return;

            decimal totalSum = 0m;
            foreach (var card in _groupCards)
                totalSum += card.LastComputedTotal;

            TxtSubtotal.Text = totalSum.ToString("N2");
        }

        /// <summary>The explicit trigger for the rest of the Financial Details panel's math
        /// — applies the header's OWN VAT/WHT/Discount % to the current Subtotal (which is
        /// already kept live by UpdateHeaderSubtotalFromGroups) to get the invoice-wide
        /// Total Amount Due. A group's own percentages only ever affect that group's own
        /// card/Total, never this calculation directly — this is a second, independent
        /// layer on top of the combined Subtotal, exactly like before sub-type groups
        /// existed.</summary>
        private void RecalculateHeaderFinancials()
        {
            UpdateHeaderSubtotalFromGroups();
            CalculateTotalAmountDue();
        }

        private void BtnCalculateFinancials_Click(object sender, RoutedEventArgs e) => RecalculateHeaderFinancials();

        /// <summary>The DataGrid's own internal ScrollViewer swallows the mouse wheel
        /// entirely, so hovering over the Invoice Items grid stops the outer page from
        /// scrolling at all. Only forward the wheel event to the page when the grid itself
        /// has nowhere further to scroll in that direction — otherwise let the grid scroll
        /// normally, as expected.</summary>
        private void DgvItems_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = FindVisualChild<ScrollViewer>((DependencyObject)sender);
            if (scrollViewer == null) return;

            bool scrollingUpAtTop = e.Delta > 0 && scrollViewer.VerticalOffset <= 0;
            bool scrollingDownAtBottom = e.Delta < 0 && scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight;

            if (scrollingUpAtTop || scrollingDownAtBottom)
            {
                e.Handled = true;
                var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                ((UIElement)((FrameworkElement)sender).Parent)?.RaiseEvent(args);
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        // ReferenceCode/ParentTag/Category aren't shown as their own columns — they're shown
        // once per group in the grid's group header bars instead of repeated on every row.
        private static readonly HashSet<string> HiddenItemColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "InvId", "ItemId", "ItemCode", "DaysUntilExpiry", "Amount", "ReferenceCode", "ParentTagGroupId", "ParentTag", "Category" };

        private static readonly Dictionary<string, string> HeaderRenames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ItemName"] = "Item/License Name",
            ["ItemType"] = "Type",
            ["LicenseKey"] = "License Key",
            ["PartNumber"] = "Part #",
            ["Quantity"] = "Qty",
            ["UnitPrice"] = "Amount",
            ["LineTotal"] = "Total",
            ["CreatedAt"] = "Created Date"
        };

        private static readonly HashSet<string> WrapColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "ItemName", "ModelNumber", "PartNumber" };

        private static readonly HashSet<string> DateColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "LineStartDate", "LineEndDate", "StartDate", "EndDate", "CreatedAt" };

        private static readonly HashSet<string> EditableColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Quantity", "UnitPrice", "UnitOfMeasure", "LineStartDate", "LineEndDate", "StartDate", "EndDate" };

        private static readonly Style WrapCellStyle = BuildWrapCellStyle();

        private static Style BuildWrapCellStyle()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(6, 2, 6, 2)));
            return style;
        }

        private sealed class DateTimeMultiLineConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value == null || value == DBNull.Value) return string.Empty;
                if (value is DateTime dt) return dt.ToString("yyyy-MM-dd") + "\n" + dt.ToString("HH:mm");
                if (DateTime.TryParse(value.ToString(), out var parsed))
                    return parsed.ToString("yyyy-MM-dd") + "\n" + parsed.ToString("HH:mm");
                return value.ToString();
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }

        /// <summary>Groups the items grid by Sub-Type + Reference Code — the same pairing
        /// dbo.vw_SetItemSubTypeGroups uses to define a group — so items visually cluster
        /// under one header bar per Sub-Type Group instead of listing flat. Items with no
        /// Sub-Type fall into a shared "Ungrouped" bucket.</summary>
        private sealed class SubTypeGroupKeyConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (!(value is DataRowView row))
                    return "Ungrouped";

                var table = row.Row.Table;
                string subType = table.Columns.Contains("SubType") && row["SubType"] != DBNull.Value
                    ? row["SubType"].ToString() : null;

                if (string.IsNullOrWhiteSpace(subType))
                    return "Ungrouped";

                string refCode = table.Columns.Contains("ReferenceCode") && row["ReferenceCode"] != DBNull.Value
                    ? row["ReferenceCode"].ToString() : null;

                return $"{subType} #{(string.IsNullOrWhiteSpace(refCode) ? "(none)" : refCode)}";
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }

        /// <summary>Sentinel returned by ParentTagGroupKeyConverter for items with no Parent
        /// Tag — also checked by the group-header Visibility converter in XAML to collapse the
        /// banner entirely for invoices that don't use Parent Tag at all.</summary>
        internal const string NoParentTagKey = "Ungrouped";

        /// <summary>Sentinel returned by CategoryGroupKeyConverter for items with no Category.</summary>
        internal const string NoCategoryKey = "Uncategorized";

        /// <summary>Groups the items grid by Parent Tag — a free-text label, independent of and
        /// nested inside Sub-Type Group. Items with no Parent Tag fall into a shared "Ungrouped"
        /// bucket, matching the SubType bucket's naming.</summary>
        private sealed class ParentTagGroupKeyConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (!(value is DataRowView row))
                    return NoParentTagKey;

                var table = row.Row.Table;
                string parentTag = table.Columns.Contains("ParentTag") && row["ParentTag"] != DBNull.Value
                    ? row["ParentTag"].ToString() : null;

                return string.IsNullOrWhiteSpace(parentTag) ? NoParentTagKey : parentTag;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }

        /// <summary>Groups the items grid by Category (dbo.ItemCategory.Name via
        /// dbo.Item.CategoryId), nested inside Parent Tag. Items with no Category fall into a
        /// shared "Uncategorized" bucket.</summary>
        private sealed class CategoryGroupKeyConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (!(value is DataRowView row))
                    return NoCategoryKey;

                var table = row.Row.Table;
                string category = table.Columns.Contains("Category") && row["Category"] != DBNull.Value
                    ? row["Category"].ToString() : null;

                return string.IsNullOrWhiteSpace(category) ? NoCategoryKey : category;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }

        /// <summary>Sums a numeric column (e.g. "Quantity" or "Amount") across a Parent Tag
        /// group's member rows, for the inline rollup shown directly in its banner header.
        /// Parent Tag is not the innermost grouping level (Category nests inside it), so
        /// CollectionViewGroup.Items at this level holds Category SUBGROUPS, not DataRowView
        /// rows directly — this recurses through any nested CollectionViewGroup children down
        /// to the leaf rows instead of only checking the immediate level.</summary>
        private sealed class GroupSumConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (!(value is System.Collections.IEnumerable items) || !(parameter is string columnName))
                    return 0m;

                decimal sum = 0m;
                AddSum(items, columnName, ref sum);
                return sum;
            }

            private static void AddSum(System.Collections.IEnumerable items, string columnName, ref decimal sum)
            {
                foreach (var item in items)
                {
                    if (item is DataRowView row)
                    {
                        if (row.Row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value)
                        {
                            try { sum += System.Convert.ToDecimal(row[columnName]); } catch { }
                        }
                    }
                    else if (item is CollectionViewGroup subGroup)
                    {
                        AddSum(subGroup.Items, columnName, ref sum);
                    }
                }
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }

        /// <summary>Collapses a group header's Border when its group key equals the sentinel
        /// passed as ConverterParameter (e.g. NoParentTagKey/NoCategoryKey) — so a plain invoice
        /// that uses neither Parent Tag nor Category doesn't show two extra empty-looking
        /// banner rows per Sub-Type bucket.</summary>
        private sealed class GroupKeyVisibilityConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var key = value as string;
                var sentinel = parameter as string;
                return string.Equals(key, sentinel, StringComparison.Ordinal) ? Visibility.Collapsed : Visibility.Visible;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }

        // ── Sub-Type Group header selection (for whole-group deletion via Delete Row) ──

        private static readonly SolidColorBrush GroupHeaderDefaultBrush = new SolidColorBrush(Color.FromRgb(0xEA, 0xF1, 0xFB));
        private static readonly SolidColorBrush GroupHeaderSelectedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xB2));

        private Border _selectedGroupHeaderBorder;
        private Brush _selectedGroupHeaderOriginalBrush;
        private SubTypeGroupSummary _selectedGroupForDeletion;
        private ParentTagGroupSummary _selectedParentTagGroupForDeletion;

        /// <summary>Restores whichever level's own banner color the selected header had before
        /// selection (captured in *_Click) — Sub-Type, Parent Tag, and Category headers each
        /// have a different default background, so a single hardcoded restore color would be
        /// wrong for at least two of the three levels.</summary>
        private void ClearGroupHeaderSelection()
        {
            if (_selectedGroupHeaderBorder != null)
                _selectedGroupHeaderBorder.Background = _selectedGroupHeaderOriginalBrush ?? GroupHeaderDefaultBrush;
            _selectedGroupHeaderBorder = null;
            _selectedGroupHeaderOriginalBrush = null;
            _selectedGroupForDeletion = null;
            _selectedParentTagGroupForDeletion = null;
        }

        /// <summary>Clicking a Sub-Type Group's full-width header bar in the Invoice Items
        /// grid selects that whole group (highlighted), so the Delete Row button can offer
        /// to remove the entire group instead of a single line. Clicking the same header
        /// again, or selecting a normal row (see DgvItems_SelectionChanged), clears it.</summary>
        private void GroupHeader_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (!_isEditMode) return;

            var border = (Border)sender;

            if (_selectedGroupHeaderBorder == border)
            {
                ClearGroupHeaderSelection();
                return;
            }

            ClearGroupHeaderSelection();

            // DataContext here is the CollectionViewGroup itself (not the plain string) —
            // its Name property holds the key produced by SubTypeGroupKeyConverter.
            var key = (border.DataContext as System.Windows.Data.CollectionViewGroup)?.Name as string;
            var group = _subTypeGroups.FirstOrDefault(g => $"{g.SubType} #{g.ReferenceCode ?? "(none)"}" == key);
            if (group == null) return;

            _selectedGroupHeaderOriginalBrush = border.Background;
            border.Background = GroupHeaderSelectedBrush;
            _selectedGroupHeaderBorder = border;
            _selectedGroupForDeletion = group;

            DgvItems.UnselectAll();
        }

        /// <summary>Clicking a Parent Tag group's banner header selects that whole group
        /// (highlighted), mirroring GroupHeader_Click above — so Delete Row can offer to
        /// un-tag the entire group instead of a single line. Built and wired up in
        /// BuildParentTagGroupStyle (the header is created in code, not XAML).</summary>
        private void ParentTagGroupHeader_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (!_isEditMode) return;

            var border = (Border)sender;

            if (_selectedGroupHeaderBorder == border)
            {
                ClearGroupHeaderSelection();
                return;
            }

            ClearGroupHeaderSelection();

            var key = (border.DataContext as System.Windows.Data.CollectionViewGroup)?.Name as string;
            var group = _parentTagGroups.FirstOrDefault(g => g.Label == key);
            if (group == null) return;

            _selectedGroupHeaderOriginalBrush = border.Background;
            border.Background = GroupHeaderSelectedBrush;
            _selectedGroupHeaderBorder = border;
            _selectedParentTagGroupForDeletion = group;

            DgvItems.UnselectAll();
        }

        private void DgvItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgvItems.SelectedItem != null)
                ClearGroupHeaderSelection();
        }

        // ── Parent Tag / Category GroupStyle (built in code, appended after the XAML-declared
        // Sub-Type GroupStyle so DataGrid.GroupStyle order is Sub-Type -> Parent Tag -> Category,
        // outer to inner, matching GroupDescriptions order in LoadInvoiceItemsAsync). Built in
        // code rather than XAML because the Parent Tag banner needs a live rolled-up Qty/Subtotal
        // computed from the group's own member rows (GroupSumConverter), which this file's other
        // dynamic UI (RenderGroupSummaryCards) already builds the same way. ──

        private static readonly SolidColorBrush ParentTagHeaderBrush = new SolidColorBrush(Color.FromRgb(0xFD, 0xF3, 0xD7));
        private static readonly SolidColorBrush ParentTagHeaderBorderBrush = new SolidColorBrush(Color.FromRgb(0xF0, 0xD8, 0x8A));
        private static readonly SolidColorBrush ParentTagHeaderTextBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x5A, 0x00));
        private static readonly SolidColorBrush CategoryHeaderBrush = new SolidColorBrush(Color.FromRgb(0xEA, 0xF1, 0xFB));
        private static readonly SolidColorBrush CategoryHeaderTextBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x6D, 0xA4));

        private void BuildExtraGroupStyles()
        {
            DgvItems.GroupStyle.Add(BuildParentTagGroupStyle());
            DgvItems.GroupStyle.Add(BuildCategoryGroupStyle());
        }

        /// <summary>The prominent banner level — label on the left, rolled-up Qty/Subtotal on
        /// the right, computed live from the group's own member rows via GroupSumConverter.
        /// Collapses entirely (via GroupKeyVisibilityConverter) when this invoice has no Parent
        /// Tags at all, so plain invoices show no extra empty banner.</summary>
        private GroupStyle BuildParentTagGroupStyle()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, ParentTagHeaderBrush);
            border.SetValue(Border.BorderBrushProperty, ParentTagHeaderBorderBrush);
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
            border.SetValue(Border.PaddingProperty, new Thickness(10, 8, 10, 8));
            border.SetValue(Border.CursorProperty, Cursors.Hand);
            border.SetBinding(UIElement.VisibilityProperty,
                new Binding("Name") { Converter = new GroupKeyVisibilityConverter(), ConverterParameter = NoParentTagKey });
            border.AddHandler(Border.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(ParentTagGroupHeader_Click));

            var dock = new FrameworkElementFactory(typeof(DockPanel));
            border.AppendChild(dock);

            // No Subtotal shown here — Parent Tag has no financial semantics of its own, and
            // an Amount rollup on the banner read as implying otherwise. Item count only.
            var qtyText = new FrameworkElementFactory(typeof(TextBlock));
            qtyText.SetValue(DockPanel.DockProperty, Dock.Right);
            qtyText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            qtyText.SetValue(TextBlock.ForegroundProperty, ParentTagHeaderTextBrush);
            qtyText.SetBinding(TextBlock.TextProperty,
                new Binding("Items") { Converter = new GroupSumConverter(), ConverterParameter = "Quantity", StringFormat = "Qty: {0:N0}" });
            dock.AppendChild(qtyText);

            var label = new FrameworkElementFactory(typeof(TextBlock));
            label.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            label.SetValue(TextBlock.FontSizeProperty, 13.0);
            label.SetValue(TextBlock.ForegroundProperty, ParentTagHeaderTextBrush);
            label.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            dock.AppendChild(label);

            var containerStyle = new Style(typeof(GroupItem));
            containerStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0)));

            return new GroupStyle
            {
                HeaderTemplate = new DataTemplate { VisualTree = border },
                ContainerStyle = containerStyle
            };
        }

        /// <summary>The lighter sub-level banner, nested inside Parent Tag — just a label, no
        /// rollup (matching how the existing Sub-Type header behaves). Collapses when this
        /// invoice's items have no Category at all.</summary>
        private GroupStyle BuildCategoryGroupStyle()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, CategoryHeaderBrush);
            border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0xDD, 0xE3, 0xEC)));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
            border.SetValue(Border.PaddingProperty, new Thickness(10, 6, 10, 6));
            border.SetBinding(UIElement.VisibilityProperty,
                new Binding("Name") { Converter = new GroupKeyVisibilityConverter(), ConverterParameter = NoCategoryKey });

            var label = new FrameworkElementFactory(typeof(TextBlock));
            label.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            label.SetValue(TextBlock.FontSizeProperty, 11.5);
            label.SetValue(TextBlock.ForegroundProperty, CategoryHeaderTextBrush);
            label.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            border.AppendChild(label);

            var containerStyle = new Style(typeof(GroupItem));
            containerStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0)));

            return new GroupStyle
            {
                HeaderTemplate = new DataTemplate { VisualTree = border },
                ContainerStyle = containerStyle
            };
        }

        /// <summary>The header checkbox on the Selected column — checks/unchecks every row's
        /// own Selected cell to match. Lives on the DataTable, so this is a plain data write,
        /// not a DataGrid selection change.</summary>
        private void ChkSelectAllRows_Click(object sender, RoutedEventArgs e)
        {
            if (_itemsTable == null) return;

            bool check = (sender as CheckBox)?.IsChecked == true;
            foreach (DataRow row in _itemsTable.Rows)
                row["Selected"] = check;
        }

        /// <summary>Shapes each auto-generated column: hides internal columns, renames
        /// headers, adds the Sub-Type combo, applies numeric/date formatting, and sets
        /// initial read-only state to match _isEditMode.</summary>
        private void DgvItems_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var name = e.PropertyName;

            if (HiddenItemColumns.Contains(name))
            {
                e.Cancel = true;
                return;
            }

            if (string.Equals(name, "Selected", StringComparison.OrdinalIgnoreCase))
            {
                var selectAll = new CheckBox
                {
                    ToolTip = "Select all rows",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                selectAll.Click += ChkSelectAllRows_Click;

                var centeredCheckBoxStyle = new Style(typeof(CheckBox));
                centeredCheckBoxStyle.Setters.Add(new Setter(HorizontalAlignmentProperty, HorizontalAlignment.Center));
                centeredCheckBoxStyle.Setters.Add(new Setter(VerticalAlignmentProperty, VerticalAlignment.Center));

                var checkboxCol = new DataGridCheckBoxColumn
                {
                    Header = selectAll,
                    Binding = new Binding(name) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                    IsReadOnly = !_isEditMode,
                    Width = 40,
                    ElementStyle = centeredCheckBoxStyle,
                    EditingElementStyle = centeredCheckBoxStyle
                };
                e.Column = checkboxCol;
                _selectedColumn = checkboxCol;
                return;
            }

            if (string.Equals(name, "SubType", StringComparison.OrdinalIgnoreCase))
            {
                if (!_itemsAreRealSetItems)
                {
                    e.Cancel = true;
                    return;
                }

                var comboItems = new List<string> { "" };
                comboItems.AddRange(ItemSubTypeCatalog.ValidSubTypes);

                var combo = new DataGridComboBoxColumn
                {
                    Header = "Sub-Type",
                    ItemsSource = comboItems,
                    SelectedItemBinding = new Binding(name) { Mode = BindingMode.TwoWay },
                    IsReadOnly = !_isEditMode
                };
                e.Column = combo;
                _subTypeColumn = combo;
                return;
            }

            if (HeaderRenames.TryGetValue(name, out var renamed))
                e.Column.Header = renamed;

            if (e.Column is DataGridTextColumn textCol && textCol.Binding is Binding binding)
            {
                if (string.Equals(name, "UnitPrice", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "LineTotal", StringComparison.OrdinalIgnoreCase))
                {
                    binding.StringFormat = "N2";
                }

                if (WrapColumns.Contains(name))
                {
                    textCol.ElementStyle = WrapCellStyle;
                    textCol.Width = new DataGridLength(160);
                }

                if (DateColumns.Contains(name))
                {
                    binding.Converter = new DateTimeMultiLineConverter();
                    textCol.ElementStyle = WrapCellStyle;
                    textCol.Width = new DataGridLength(120);
                }

                textCol.IsReadOnly = !(_isEditMode && EditableColumns.Contains(name));
            }
        }

        /// <summary>Re-applies read-only state to every generated column — called whenever
        /// SetEditMode toggles without reloading the grid data.</summary>
        private void ConfigureInvoiceItemsGridEditMode(bool isEdit)
        {
            foreach (var col in DgvItems.Columns)
            {
                if (ReferenceEquals(col, _subTypeColumn) || ReferenceEquals(col, _selectedColumn))
                    continue;

                string name = (col as DataGridBoundColumn)?.Binding is Binding b ? b.Path?.Path : null;
                col.IsReadOnly = !(isEdit && name != null && EditableColumns.Contains(name));
            }

            if (_subTypeColumn != null)
                _subTypeColumn.IsReadOnly = !(_itemsAreRealSetItems && isEdit);

            if (_selectedColumn != null)
                _selectedColumn.IsReadOnly = !isEdit;
        }

        /// <summary>Mirrors the WinForms CellValueChanged handler — reacts to edits on the
        /// underlying DataTable (shared by the DataGrid, so this fires regardless of which
        /// UI made the edit) instead of DataGrid-level cell events.</summary>
        private void ItemsData_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (!_isEditMode) return;

            var columnName = e.Column.ColumnName;
            if (string.Equals(columnName, "Quantity", StringComparison.OrdinalIgnoreCase)
                || string.Equals(columnName, "UnitPrice", StringComparison.OrdinalIgnoreCase)
                || string.Equals(columnName, "UnitOfMeasure", StringComparison.OrdinalIgnoreCase)
                || string.Equals(columnName, "LineStartDate", StringComparison.OrdinalIgnoreCase)
                || string.Equals(columnName, "LineEndDate", StringComparison.OrdinalIgnoreCase)
                || string.Equals(columnName, "StartDate", StringComparison.OrdinalIgnoreCase)
                || string.Equals(columnName, "EndDate", StringComparison.OrdinalIgnoreCase)
                || string.Equals(columnName, "SubType", StringComparison.OrdinalIgnoreCase))
            {
                _invoiceItemsDirty = true;
            }

            RecalculateInvoiceItemRowAndTotals(e.Row);
        }

        private void RecalculateInvoiceItemRowAndTotals(DataRow row)
        {
            if (row == null) return;
            var table = row.Table;

            string qtyCol = table.Columns.Contains("Quantity") ? "Quantity" : null;
            string unitPriceCol = table.Columns.Contains("UnitPrice") ? "UnitPrice" : null;
            string amountCol = table.Columns.Contains("Amount")
                ? "Amount"
                : (table.Columns.Contains("LineTotal") ? "LineTotal" : null);

            if (qtyCol != null && unitPriceCol != null && amountCol != null)
            {
                var qty = row[qtyCol] == DBNull.Value ? 0m : Convert.ToDecimal(row[qtyCol]);
                var unitPrice = row[unitPriceCol] == DBNull.Value ? 0m : Convert.ToDecimal(row[unitPriceCol]);
                row[amountCol] = qty * unitPrice;
            }

            RecalculateSubtotalFromGrid();
        }

        private void RecalculateSubtotalFromGrid()
        {
            if (!_isEditMode || _itemsTable == null)
                return;

            string amountCol = _itemsTable.Columns.Contains("Amount")
                ? "Amount"
                : (_itemsTable.Columns.Contains("LineTotal") ? "LineTotal" : null);

            if (string.IsNullOrWhiteSpace(amountCol))
                return;

            decimal subtotal = 0m;
            foreach (DataRow r in _itemsTable.Rows)
            {
                if (r.RowState == DataRowState.Deleted) continue;
                if (r[amountCol] == DBNull.Value) continue;
                if (decimal.TryParse(r[amountCol].ToString(), out var v))
                    subtotal += v;
            }

            TxtSubtotal.Text = subtotal.ToString("N2");
            CalculateTotalAmountDue();
        }

        /// <summary>Gets items from SetItem table (for software/service sets).</summary>
        private async System.Threading.Tasks.Task<DataTable> GetSetItemsAsync(int setId)
        {
            string connectionString = DatabaseConfig.ConnectionString;

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT
                        si.SetItemId,
                        si.ItemId,
                        si.ItemCode,
                        i.Name AS ItemName,
                        i.ModelNumber,
                        i.SerialNumber,
                        rn.PartNumber,
                        si.SetId,
                        s.SetCode,
                        si.Description,
                        si.ItemType,
                        ISNULL(i.StockOnHand, si.Quantity) AS Quantity,
                        si.UnitOfMeasure,
                        CASE
                            WHEN s.SetType IN ('Software/License', 'Service', 'Services')
                                 AND ISNULL(si.UnitPrice, 0) = 0
                                 AND ISNULL(i.Amount, 0) > 0 THEN i.Amount
                            ELSE ISNULL(si.UnitPrice, 0)
                        END AS UnitPrice,
                        CASE
                            WHEN s.SetType IN ('Software/License', 'Service', 'Services')
                                 AND ISNULL(si.Amount, 0) = 0
                                 AND (
                                        (ISNULL(si.UnitPrice, 0) = 0 AND ISNULL(i.Amount, 0) > 0)
                                        OR ISNULL(si.UnitPrice, 0) > 0
                                     )
                                 THEN ISNULL(si.Quantity, 0) * CASE
                                     WHEN ISNULL(si.UnitPrice, 0) = 0 AND ISNULL(i.Amount, 0) > 0 THEN i.Amount
                                     ELSE ISNULL(si.UnitPrice, 0)
                                 END
                            ELSE ISNULL(si.Amount, 0)
                        END AS Amount,
                        CASE
                            WHEN si.LineStartDate IS NULL THEN s.StartDate
                            WHEN si.CreatedAt IS NOT NULL AND ABS(DATEDIFF(SECOND, si.LineStartDate, si.CreatedAt)) <= 5 THEN s.StartDate
                            ELSE si.LineStartDate
                        END AS LineStartDate,
                        COALESCE(si.LineEndDate, s.EndDate) AS LineEndDate,
                        si.CreatedAt,
                        si.SubType,
                        si.ReferenceCode,
                        si.ParentTagGroupId,
                        ptg.Label AS ParentTag,
                        ic.Name AS Category
                    FROM dbo.SetItem si
                    INNER JOIN dbo.[Set] s ON si.SetId = s.SetId
                    LEFT JOIN dbo.Item i ON si.ItemId = i.ItemId
                    LEFT JOIN dbo.SetItemParentTagGroup ptg ON si.ParentTagGroupId = ptg.ParentTagGroupId
                    LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
                    OUTER APPLY (
                        SELECT TOP (1) rn2.PartNumber
                        FROM dbo.Renewals rn2
                        WHERE rn2.ItemId = i.ItemId AND rn2.IsArchived = 0
                        ORDER BY rn2.RenewalId DESC
                    ) rn
                    WHERE si.SetId = @SetId
                    ORDER BY si.ItemCode";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SetId", setId);
                    using (var adapter = new SqlDataAdapter(command))
                    {
                        var dataTable = new DataTable();
                        adapter.Fill(dataTable);
                        return dataTable;
                    }
                }
            }
        }

        // ── Edit mode ─────────────────────────────────────────────────────────

        private void SetEditMode(bool isEdit)
        {
            _isEditMode = isEdit;

            DtpDate.IsEnabled = isEdit;
            CmbCompany.IsEnabled = isEdit;
            CmbDistributor.IsEnabled = isEdit;
            TxtDocumentNumber.IsReadOnly = !isEdit;
            CmbStatus.IsEnabled = isEdit;
            TxtReferenceNumber.IsReadOnly = !isEdit;
            TxtSite.IsReadOnly = !isEdit;
            CmbSiteCompany.IsEnabled = isEdit;
            CmbSiteBranch.IsEnabled = isEdit;
            CmbSiteDepartment.IsEnabled = isEdit;
            DtpStartDate.IsEnabled = isEdit;
            DtpEndDate.IsEnabled = isEdit;
            ChkOverrideLineItemDates.IsEnabled = isEdit;
            TxtSubtotal.IsReadOnly = !isEdit;
            TxtVatAmount.IsReadOnly = !isEdit;
            TxtWhtAmount.IsReadOnly = !isEdit;
            TxtDiscountAmount.IsReadOnly = !isEdit;
            TxtTotalAmountDue.IsReadOnly = true;
            TxtRemarks.IsReadOnly = !isEdit;

            BtnEdit.Visibility = isEdit ? Visibility.Collapsed : Visibility.Visible;
            BtnBulkAddItems.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;
            BtnConvertToSubType.Visibility = isEdit && _itemsAreRealSetItems ? Visibility.Visible : Visibility.Collapsed;
            BtnConvertToParentTag.Visibility = isEdit && _itemsAreRealSetItems ? Visibility.Visible : Visibility.Collapsed;
            BtnDeleteRow.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;
            BtnSave.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;
            BtnCancel.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;
            BtnSaveFinancials.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;

            foreach (var card in _groupCards)
            {
                card.SubtotalBox.IsReadOnly = !isEdit;
                card.VatBox.IsReadOnly = !isEdit;
                card.WhtBox.IsReadOnly = !isEdit;
                card.DiscountBox.IsReadOnly = !isEdit;
                card.RemoveButton.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;
            }

            ConfigureInvoiceItemsGridEditMode(isEdit);
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e) => SetEditMode(true);

        private async void BtnBulkAddItems_Click(object sender, RoutedEventArgs e)
        {
            if (!_isEditMode)
                return;

            if (_invoiceItemsDirty)
            {
                MessageBox.Show(this, "Please Save or Cancel your line item edits before adding items.", "Pending Changes",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string connectionString = DatabaseConfig.ConnectionString;
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    MessageBox.Show(this, "Connection string not configured.", "Config Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (var dlg = new BatchAddInvoiceItemsDialog(connectionString, _setId))
                {
                    if (dlg.ShowDialog(GetWin32Owner()) != WinForms.DialogResult.OK)
                        return;

                    if (dlg.SelectedItems == null || dlg.SelectedItems.Count == 0)
                        return;

                    await _invoiceRepository.AddInvoiceItemsAsync(
                        _setId,
                        dlg.SelectedItems,
                        AppSession.CurrentUserId,
                        dlg.SubType,
                        dlg.ReferenceCode,
                        dlg.BeginDate,
                        dlg.EndDate);

                    await LoadInvoiceItemsAsync();
                    _invoiceItemsDirty = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to bulk add invoice items: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Opens the Sub-Type conversion picker — it loads and displays every item on
        /// this invoice itself (with its own checkbox column), so batches can be converted to
        /// one group after another in a single session without pre-selecting rows here first.
        /// Each batch commits immediately inside the dialog; this only needs to refresh the
        /// grid/group cards once the dialog closes.</summary>
        private async void BtnConvertToSubType_Click(object sender, RoutedEventArgs e)
        {
            if (!_isEditMode || !_itemsAreRealSetItems)
                return;

            if (_invoiceItemsDirty)
            {
                MessageBox.Show(this, "Please Save or Cancel your line item edits before converting items.", "Pending Changes",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string connectionString = DatabaseConfig.ConnectionString;

            bool anyChanges;
            using (var dlg = new ConvertToSubTypeGroupDialog(connectionString, _setId))
            {
                dlg.ShowDialog(GetWin32Owner());
                anyChanges = dlg.AnyChanges;
            }

            if (anyChanges)
            {
                await LoadInvoiceItemsAsync();
                _invoiceItemsDirty = false;
            }
        }

        /// <summary>Opens the Parent Tag conversion picker — same self-contained flow as
        /// BtnConvertToSubType_Click above, but for the independent, orthogonal Parent Tag
        /// grouping (free-text label, no financial semantics).</summary>
        private async void BtnConvertToParentTag_Click(object sender, RoutedEventArgs e)
        {
            if (!_isEditMode || !_itemsAreRealSetItems)
                return;

            if (_invoiceItemsDirty)
            {
                MessageBox.Show(this, "Please Save or Cancel your line item edits before converting items.", "Pending Changes",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string connectionString = DatabaseConfig.ConnectionString;

            bool anyChanges;
            using (var dlg = new ConvertToParentTagGroupDialog(connectionString, _setId))
            {
                dlg.ShowDialog(GetWin32Owner());
                anyChanges = dlg.AnyChanges;
            }

            if (anyChanges)
            {
                await LoadInvoiceItemsAsync();
                _invoiceItemsDirty = false;
            }
        }

        private async void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (!_isEditMode)
                return;

            if (_invoiceItemsDirty)
            {
                MessageBox.Show(this, "Please Save or Cancel your line item edits before deleting.", "Pending Changes",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // A Sub-Type Group header row was selected (see GroupHeader_Click) — offer to
            // remove the whole group instead of a single line.
            if (_selectedGroupForDeletion != null)
            {
                var group = _selectedGroupForDeletion;
                string groupLabel = $"{group.SubType} #{group.ReferenceCode ?? "(none)"}";

                using (var groupDlg = new DeleteInvoiceLineDialog(null, groupLabel))
                {
                    if (groupDlg.ShowDialog(GetWin32Owner()) != WinForms.DialogResult.OK)
                        return;
                    if (groupDlg.Choice != DeleteInvoiceLineDialog.DeleteChoice.DeleteGroup)
                        return;
                }

                try
                {
                    new InvoicePreparationRepository().RemoveGroupFromInvoice(group.GroupId, AppSession.CurrentUserId);
                    ClearGroupHeaderSelection();
                    await LoadInvoiceItemsAsync();
                    _invoiceItemsDirty = false;

                    MessageBox.Show(this, "The group has been removed from this invoice and is now on the Invoice Sub Groups page's Not Completed tab.",
                        "Group Removed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to remove the group: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            // A Parent Tag group banner was selected (see ParentTagGroupHeader_Click) — offer
            // to remove the whole tag instead of a single line. Unlike Sub-Type, this has no
            // financial semantics to unwind: items simply become untagged, still on this invoice.
            if (_selectedParentTagGroupForDeletion != null)
            {
                var parentTagGroup = _selectedParentTagGroupForDeletion;

                using (var groupDlg = new DeleteInvoiceLineDialog(null, parentTagGroup.Label))
                {
                    if (groupDlg.ShowDialog(GetWin32Owner()) != WinForms.DialogResult.OK)
                        return;
                    if (groupDlg.Choice != DeleteInvoiceLineDialog.DeleteChoice.DeleteGroup)
                        return;
                }

                try
                {
                    await _invoiceRepository.RemoveParentTagGroupFromInvoice(parentTagGroup.ParentTagGroupId, AppSession.CurrentUserId);
                    ClearGroupHeaderSelection();
                    await LoadInvoiceItemsAsync();
                    _invoiceItemsDirty = false;

                    MessageBox.Show(this, "The Parent Tag has been removed. Its items remain on this invoice, now untagged.",
                        "Parent Tag Removed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to remove the Parent Tag: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            if (!(DgvItems.SelectedItem is DataRowView selectedRowView))
            {
                MessageBox.Show(this, "Please select a row, or a Sub-Type/Parent Tag group header, to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedRow = selectedRowView.Row;

            int GetIntCell(string colName) =>
                selectedRow.Table.Columns.Contains(colName) && selectedRow[colName] != DBNull.Value ? Convert.ToInt32(selectedRow[colName]) : 0;
            string GetStringCell(string colName) =>
                selectedRow.Table.Columns.Contains(colName) && selectedRow[colName] != DBNull.Value ? Convert.ToString(selectedRow[colName]) : null;

            int setItemId = GetIntCell("SetItemId");

            if (setItemId <= 0)
            {
                MessageBox.Show(this, "Selected row does not contain a valid SetItemId.", "Delete Row",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string itemName = GetStringCell("ItemName") ?? GetStringCell("Description") ?? $"SetItemId {setItemId}";

            using (var dlg = new DeleteInvoiceLineDialog(itemName))
            {
                if (dlg.ShowDialog(GetWin32Owner()) != WinForms.DialogResult.OK)
                    return;
                if (dlg.Choice != DeleteInvoiceLineDialog.DeleteChoice.DeleteRowOnly)
                    return;

                bool deleted = await _invoiceRepository.DeleteInvoiceSetItemAsync(_setId, setItemId);
                if (!deleted)
                {
                    MessageBox.Show(this, "Failed to delete the selected row (it may have already been removed).", "Delete Row",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            await LoadInvoiceItemsAsync();
            CalculateTotalAmountDue();
        }

        /// <summary>Pulls a Sub-Type Group entirely off this invoice and re-lands it as a
        /// Draft group on the Invoice Sub Groups page's Not Completed tab, fully intact
        /// (same items/quantities/prices) — the reverse of generating an invoice from a
        /// group. Uses InvoicePreparationRepository.RemoveGroupFromInvoice, which deletes
        /// the group's dbo.SetItem rows from this invoice in the same transaction that
        /// recreates them as a dbo.InvoicePreparation/InvoicePreparationItem group.</summary>
        private async void RemoveGroupFromInvoice_Click(SubTypeGroupSummary group)
        {
            if (!_isEditMode) return;

            if (_invoiceItemsDirty)
            {
                MessageBox.Show(this, "Please Save or Cancel your line item edits before removing a group.", "Pending Changes",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(this,
                $"Remove the {group.SubType} #{group.ReferenceCode ?? "(none)"} group from this invoice?\n\n" +
                "Its items will be taken off this invoice and sent back to the Invoice Sub Groups page's " +
                "Not Completed tab, fully intact, so they can be re-invoiced later.",
                "Remove Sub-Type Group",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                var prepRepository = new InvoicePreparationRepository();
                prepRepository.RemoveGroupFromInvoice(group.GroupId, AppSession.CurrentUserId);

                await LoadInvoiceItemsAsync();
                _invoiceItemsDirty = false;

                MessageBox.Show(this, "The group has been removed from this invoice and is now on the Invoice Sub Groups page's Not Completed tab.",
                    "Group Removed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to remove the group: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Save / Cancel / Close ─────────────────────────────────────────────

        /// <summary>Persists every group card's current Subtotal/VAT%/WHT%/Discount% into
        /// dbo.SetItemSubTypeGroup's SubtotalOverride/VatPercent/WhtPercent/DiscountPercent
        /// columns, so they round-trip correctly on reload instead of resetting to the raw
        /// SUM(SetItem.Amount)/header defaults every time the page refreshes.</summary>
        private void SaveGroupCardOverrides()
        {
            if (_groupCards.Count == 0) return;

            const string sql = @"
                UPDATE dbo.SetItemSubTypeGroup
                SET SubtotalOverride = @SubtotalOverride,
                    VatPercent = @VatPercent,
                    WhtPercent = @WhtPercent,
                    DiscountPercent = @DiscountPercent
                WHERE GroupId = @GroupId";

            using (var connection = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                connection.Open();
                foreach (var card in _groupCards)
                {
                    decimal.TryParse(card.SubtotalBox.Text, out decimal subtotal);
                    decimal.TryParse(card.VatBox.Text, out decimal vat);
                    decimal.TryParse(card.WhtBox.Text, out decimal wht);
                    decimal.TryParse(card.DiscountBox.Text, out decimal discount);

                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@SubtotalOverride", subtotal);
                        command.Parameters.AddWithValue("@VatPercent", vat);
                        command.Parameters.AddWithValue("@WhtPercent", wht);
                        command.Parameters.AddWithValue("@DiscountPercent", discount);
                        command.Parameters.AddWithValue("@GroupId", card.Group.GroupId);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs())
                return;

            try
            {
                if (_isEditMode && _invoiceItemsDirty && _itemsTable != null && _itemsTable.Rows.Count > 0)
                {
                    var updates = new List<InvoiceRepository.InvoiceItemLineUpdate>();

                    foreach (DataRow r in _itemsTable.Rows)
                    {
                        if (r == null) continue;
                        if (!_itemsTable.Columns.Contains("SetItemId")) continue;

                        int setItemId = r["SetItemId"] == DBNull.Value ? 0 : Convert.ToInt32(r["SetItemId"]);
                        if (setItemId <= 0) continue;

                        decimal qty = 0m;
                        decimal unitPrice = 0m;
                        decimal amount = 0m;

                        if (_itemsTable.Columns.Contains("Quantity") && r["Quantity"] != DBNull.Value)
                            qty = Convert.ToDecimal(r["Quantity"]);
                        if (_itemsTable.Columns.Contains("UnitPrice") && r["UnitPrice"] != DBNull.Value)
                            unitPrice = Convert.ToDecimal(r["UnitPrice"]);

                        if (_itemsTable.Columns.Contains("Amount") && r["Amount"] != DBNull.Value)
                            amount = Convert.ToDecimal(r["Amount"]);
                        else
                            amount = qty * unitPrice;

                        DateTime? lineStart = null;
                        DateTime? lineEnd = null;
                        if (_itemsTable.Columns.Contains("LineStartDate") && r["LineStartDate"] != DBNull.Value)
                            lineStart = Convert.ToDateTime(r["LineStartDate"]);
                        if (_itemsTable.Columns.Contains("LineEndDate") && r["LineEndDate"] != DBNull.Value)
                            lineEnd = Convert.ToDateTime(r["LineEndDate"]);

                        updates.Add(new InvoiceRepository.InvoiceItemLineUpdate
                        {
                            SetItemId = setItemId,
                            Quantity = qty,
                            UnitPrice = unitPrice,
                            Amount = amount,
                            UnitOfMeasure = _itemsTable.Columns.Contains("UnitOfMeasure") ? (r["UnitOfMeasure"] == DBNull.Value ? null : r["UnitOfMeasure"].ToString()) : null,
                            LineStartDate = lineStart,
                            LineEndDate = lineEnd,
                            SubType = _itemsTable.Columns.Contains("SubType") && r["SubType"] != DBNull.Value && !string.IsNullOrWhiteSpace(r["SubType"].ToString())
                                ? r["SubType"].ToString() : null
                        });
                    }

                    await _invoiceRepository.UpdateInvoiceItemsAsync(_setId, updates);
                    _invoiceItemsDirty = false;
                }

                int? companyId = (CmbCompany.SelectedValue != null && CmbCompany.SelectedValue != DBNull.Value)
                    ? (int?)Convert.ToInt32(CmbCompany.SelectedValue) : null;

                int? distributorId = (CmbDistributor.SelectedValue != null && CmbDistributor.SelectedValue != DBNull.Value)
                    ? (int?)Convert.ToInt32(CmbDistributor.SelectedValue) : null;

                int? currentBranchId = (CmbSiteBranch.SelectedValue != null && CmbSiteBranch.SelectedValue != DBNull.Value)
                    ? (int?)Convert.ToInt32(CmbSiteBranch.SelectedValue) : null;

                int? currentDepartmentId = (CmbSiteDepartment.SelectedValue != null && CmbSiteDepartment.SelectedValue != DBNull.Value)
                    ? (int?)Convert.ToInt32(CmbSiteDepartment.SelectedValue) : null;

                int? invoiceRequesterEmpId = (CmbRequestedBy.SelectedValue != null && CmbRequestedBy.SelectedValue != DBNull.Value)
                    ? (int?)Convert.ToInt32(CmbRequestedBy.SelectedValue) : null;

                bool senderOrSiteChanged =
                    companyId != GetInvoiceDataInt("ComId") ||
                    distributorId != GetInvoiceDataInt("DistributorId") ||
                    currentBranchId != GetInvoiceDataInt("CurrentBranchId") ||
                    currentDepartmentId != GetInvoiceDataInt("CurrentDepartmentId");

                object oldSenderSiteSnapshot = new
                {
                    Company = GetInvoiceDataString("CompanyName"),
                    Distributor = GetInvoiceDataString("DistributorName"),
                    SiteBranch = GetInvoiceDataString("CurrentBranchName"),
                    SiteDepartment = GetInvoiceDataString("CurrentDepartmentName")
                };

                string status = CmbStatus.SelectedIndex == 0 ? "Yes" : CmbStatus.SelectedIndex == 1 ? "No" : null;

                DateTime startDate = (DtpStartDate.SelectedDate ?? DateTime.Today).Date.AddHours(8);
                DateTime endDate = (DtpEndDate.SelectedDate ?? DateTime.Today).Date.AddHours(8);

                if (!decimal.TryParse(TxtSubtotal.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var subtotalValue))
                {
                    MessageBox.Show(this, "Please enter a valid Subtotal.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtSubtotal.Focus();
                    return;
                }

                if (!TryCalculateFinancialsFromPercentages(
                        out decimal vatAmount, out decimal discountAmount, out decimal whtAmount, out decimal totalAmountDue, out string errorMessage))
                {
                    MessageBox.Show(this, errorMessage, "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool updated = await _invoiceRepository.UpdateInvoiceAsync(
                    _setId,
                    TxtDocumentNumber.Text.Trim(),
                    TxtReferenceNumber.Text.Trim(),
                    TxtSite.Text.Trim(),
                    currentBranchId,
                    currentDepartmentId,
                    startDate,
                    endDate,
                    subtotalValue,
                    vatAmount,
                    whtAmount,
                    discountAmount,
                    totalAmountDue,
                    companyId,
                    status,
                    TxtRemarks.Text.Trim(),
                    distributorId,
                    invoiceRequesterEmpId
                );

                if (updated)
                {
                    if (senderOrSiteChanged)
                    {
                        object newSenderSiteSnapshot = new
                        {
                            Company = GetLookupName(_companiesData, "ComId", "CompanyName", companyId),
                            Distributor = GetLookupName(_distributorsData, "DistributorId", "DistributorName", distributorId),
                            SiteBranch = GetLookupName(_branchesData, "BranchId", "BranchName", currentBranchId),
                            SiteDepartment = GetLookupName(_departmentsData, "DeptId", "DepartmentName", currentDepartmentId)
                        };

                        try
                        {
                            await new AuditRepository().LogAsync(new AuditEntry
                            {
                                Action = "SenderSiteChange",
                                EntityId = _setId,
                                EntityType = "Invoice",
                                UserId = AppSession.CurrentUserId,
                                UserName = AppSession.CurrentUserName,
                                Timestamp = DateTime.Now,
                                Notes = $"Invoice #{_setId} sender/site changed",
                                OldValues = JsonConvert.SerializeObject(oldSenderSiteSnapshot),
                                NewValues = JsonConvert.SerializeObject(newSenderSiteSnapshot)
                            });
                        }
                        catch (Exception auditEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ViewInvoiceDetailPage] Failed to write sender/site audit log: {auditEx.Message}");
                        }
                    }

                    if (ChkOverrideLineItemDates?.IsChecked == true)
                    {
                        await _invoiceRepository.UpdateInvoiceItemDatesForHeaderChangeAsync(
                            _setId,
                            _originalHeaderStartDate,
                            _originalHeaderEndDate,
                            startDate,
                            endDate);
                    }

                    SaveGroupCardOverrides();

                    MessageBox.Show(this, "Invoice updated successfully.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    await LoadDataAsync();
                    SetEditMode(false);
                }
                else
                {
                    MessageBox.Show(this, "Failed to update invoice.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error updating invoice: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int? GetInvoiceDataInt(string column)
        {
            if (_invoiceData == null || !_invoiceData.Table.Columns.Contains(column) || _invoiceData[column] == DBNull.Value)
                return null;
            return Convert.ToInt32(_invoiceData[column]);
        }

        private string GetInvoiceDataString(string column)
        {
            if (_invoiceData == null || !_invoiceData.Table.Columns.Contains(column) || _invoiceData[column] == DBNull.Value)
                return null;
            return _invoiceData[column].ToString();
        }

        private static string GetLookupName(DataTable table, string idColumn, string nameColumn, int? id)
        {
            if (table == null || id == null)
                return null;

            foreach (DataRow row in table.Rows)
            {
                if (row[idColumn] != DBNull.Value && Convert.ToInt32(row[idColumn]) == id.Value)
                    return row[nameColumn] == DBNull.Value ? null : row[nameColumn].ToString();
            }

            return null;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            PopulateFields();
            RenderGroupSummaryCards();
            SetEditMode(false);
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(TxtDocumentNumber.Text))
            {
                MessageBox.Show(this, "Please enter a Document Number.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtDocumentNumber.Focus();
                return false;
            }

            if (!decimal.TryParse(TxtSubtotal.Text, out _))
            {
                MessageBox.Show(this, "Please enter a valid Subtotal.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtSubtotal.Focus();
                return false;
            }

            if (!decimal.TryParse(TxtVatAmount.Text, out _))
            {
                MessageBox.Show(this, "Please enter a valid VAT Amount.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtVatAmount.Focus();
                return false;
            }

            if (!decimal.TryParse(TxtTotalAmountDue.Text, out _))
            {
                MessageBox.Show(this, "Please enter a valid Total Amount Due.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtTotalAmountDue.Focus();
                return false;
            }

            return true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ── Financial calculation helpers ─────────────────────────────────────

        // Header fields no longer recalculate automatically on every keystroke — the user
        // explicitly clicks "Calculate" (BtnCalculateFinancials_Click -> RecalculateHeaderFinancials)
        // when ready. These handlers are wired from the XAML TextChanged events but are
        // intentionally no-ops.
        private void TxtSubtotal_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtVatAmount_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtWhtAmount_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TxtDiscountAmount_TextChanged(object sender, TextChangedEventArgs e) { }

        private void CalculateTotalAmountDue()
        {
            if (TryCalculateFinancialsFromPercentages(
                    out decimal _, out decimal _, out decimal _, out decimal totalAmountDue, out string _))
            {
                TxtTotalAmountDue.Text = totalAmountDue.ToString("N2");
            }
        }

        // Calculation chain, matching the paper sales invoice layout
        // ("Amount: Net of VAT / Add: VAT / Less: Withholding Tax / Total Amount Due"):
        //
        //   Subtotal            -- net-of-VAT amount (VAT is NOT included in this figure)
        //     -> Discount       = Subtotal * discountRate           (discount applies to the net base)
        //     -> Net After Disc = Subtotal - Discount
        //     -> VAT Amount     = Net After Disc * vatRate          (VAT is added back in)
        //     -> WHT Amount     = Net After Disc * whtRate
        //     -> Total Due      = Net After Disc + VAT Amount - WHT Amount
        private bool TryCalculateFinancialsFromPercentages(
            out decimal vatAmount,
            out decimal discountAmount,
            out decimal whtAmount,
            out decimal totalAmountDue,
            out string errorMessage)
        {
            vatAmount = 0m;
            discountAmount = 0m;
            whtAmount = 0m;
            totalAmountDue = 0m;
            errorMessage = string.Empty;

            if (!decimal.TryParse(TxtSubtotal.Text, out decimal subtotal))
            {
                errorMessage = "Invalid Subtotal value.";
                return false;
            }

            if (!decimal.TryParse(TxtVatAmount.Text, out decimal vatPercent))
            {
                errorMessage = "Invalid VAT % value.";
                return false;
            }

            if (!decimal.TryParse(TxtDiscountAmount.Text, out decimal discountPercent))
                discountPercent = 0;

            if (!decimal.TryParse(TxtWhtAmount.Text, out decimal whtPercent))
                whtPercent = 0;

            decimal vatRate = vatPercent / 100m;
            decimal discountRate = discountPercent / 100m;
            decimal whtRate = whtPercent / 100m;

            discountAmount = subtotal * discountRate;
            decimal netAfterDiscount = subtotal - discountAmount;

            vatAmount = netAfterDiscount * vatRate;
            whtAmount = netAfterDiscount * whtRate;

            totalAmountDue = netAfterDiscount + vatAmount - whtAmount;

            return true;
        }

        private void LblFinancialExample_Click(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show(this,
                "Worked example, taken from a paper sales invoice:\n\n" +
                "Subtotal (Net of VAT)........... 6,008,928.57\n" +
                "  + VAT 12% ...................... 721,071.43\n" +
                "  = Total Sales (VAT Inclusive) ... 6,730,000.00\n\n" +
                "  − WHT 2% ....................... 120,178.57\n" +
                "  = Total Amount Due ............... 6,609,821.43\n\n" +
                "Enter VAT / WHT / Discount as plain percentages\n" +
                "(type 12 for 12%, not 0.12). Subtotal must already\n" +
                "be net of VAT — VAT is added on top, then WHT is\n" +
                "subtracted, to arrive at the Total Amount Due.",
                "How to fill in Financial Details",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
