using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages.Receipt;
using Yakult.Inventory.App.WPF.Set.RequisitionForm.Services;
using Yakult.Inventory.App.WPF.Set.RequisitionForm.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

using WinForms  = System.Windows.Forms;
using WinMsgBox = System.Windows.MessageBox;
using WinCursor = System.Windows.Input.Cursors;

namespace Yakult.Inventory.App.Pages.Set
{
    // ── Display row wrapper (adds ComputerName/IPAddress that SetDetailRequestDto lacks) ──
    internal sealed class SetRequestDisplayRow
    {
        public int    ReqId         { get; set; }
        public int    ItemId        { get; set; }
        public string ItemName      { get; set; }
        public string Category      { get; set; }
        public bool   IsTrackedAsset{ get; set; }
        public string FixedAsset    => IsTrackedAsset ? "Yes" : "No";
        public string ModelNumber   { get; set; }
        public string SerialNumber  { get; set; }
        public string ComputerName  { get; set; }
        public string IPAddress     { get; set; }
        public int    Quantity      { get; set; }
        public string Status        { get; set; }
        public string Description   { get; set; }
        public string Remarks       { get; set; }

        // True when this row came from dbo.SetItem (Renewal/Invoice-created Set, no
        // dbo.Request row behind it) rather than a real dbo.Request. ReqId on these rows is
        // actually a SetItemId, repurposed only for QR/PDF/Requisition numbering — NEVER
        // pass it to a Request-based mutation (see BtnRemoveRequest_Click's guard).
        public bool   IsSetItemRow  { get; set; }

        public static SetRequestDisplayRow From(SetDetailRequestDto dto) => new SetRequestDisplayRow
        {
            ReqId          = dto.ReqId,
            ItemId         = dto.ItemId,
            ItemName       = dto.ItemName,
            Category       = dto.Category,
            IsTrackedAsset = dto.IsTrackedAsset,
            ModelNumber    = dto.ModelNumber,
            SerialNumber   = dto.SerialNumber,
            Quantity       = dto.Quantity,
            Status         = dto.Status,
            Description    = dto.Description,
            Remarks        = StripCartridgeCondition(dto.Remarks),
            ComputerName   = string.Empty,
            IPAddress      = string.Empty
        };

        public static SetRequestDisplayRow FromSetItem(SetDetailRequestDto dto)
        {
            var row = From(dto);
            row.IsSetItemRow = true;
            return row;
        }

        // dbo.Request.Remarks stores a synthetic "With Cartridge"/"Without Cartridge" leading
        // tag for cartridge-only requests (consumed by Cartridge Management's pending queue) —
        // strip it here so this display-only grid never shows a "remark" the requester never
        // typed. Mirrors RequesterPortalService.StripCartridgeCondition and
        // RequisitionFormViewModel's own copy of the same logic (no shared helper exists yet).
        private static string StripCartridgeCondition(string remarks)
        {
            if (string.IsNullOrWhiteSpace(remarks)) return null;

            const string sep = " | ";
            foreach (var prefix in new[] { "With Cartridge", "Without Cartridge" })
            {
                if (remarks.StartsWith(prefix + sep, StringComparison.OrdinalIgnoreCase))
                    return remarks.Substring(prefix.Length + sep.Length);
                if (string.Equals(remarks.Trim(), prefix, StringComparison.OrdinalIgnoreCase))
                    return null;
            }

            return remarks;
        }
    }

    // ── Adapter to let WinForms dialogs accept a WPF Window as owner ──────────────────────
    internal sealed class WpfWin32Window : WinForms.IWin32Window
    {
        private readonly IntPtr _handle;
        public WpfWin32Window(Window window)
            => _handle = new WindowInteropHelper(window).Handle;
        public IntPtr Handle => _handle;
    }

    public partial class ViewSetDetailPage : Window, IDisposable
    {
        // ── DllImports used by inline WinForms dialogs ────────────────────────────────────
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nL, int nT, int nR, int nB, int nW, int nH);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION        = 0x2;

        // ── State ─────────────────────────────────────────────────────────────────────────
        private readonly SetRepository     _repository;
        private readonly RequestRepository _requestRepository;
        private readonly int               _setId;

        private int    _currentEmployeeId;
        private string _currentSetType;
        private bool   _isDeployed;
        private byte[] _currentQrImageData;
        private bool   _isDeptLevel;
        private bool   _isMaximized;

        // True when this Set's Employee/Dept assignment is sourced from dbo.Request (the
        // normal case). False for Sets with items but no Request rows at all (Renewal- or
        // Invoice-created Sets), where assignment instead reads/writes dbo.[Set] directly
        // (CurrentEmployeeId / ComId+CurrentDepartmentId+CurrentBranchId).
        private bool   _isRequestBasedSet;

        private readonly List<OrgSetItem> _orgCompanies = new List<OrgSetItem>();
        private readonly List<OrgSetItem> _orgDepts     = new List<OrgSetItem>();
        private readonly List<OrgSetItem> _orgBranches  = new List<OrgSetItem>();
        private readonly List<OrgSetItem> _orgDistributors = new List<OrgSetItem>();

        private List<SetRequestDisplayRow> _displayRows = new List<SetRequestDisplayRow>();

        // ── IDisposable (no-op for WinForms `using` callers) ─────────────────────────────
        public void Dispose() { }

        // ── WinForms ShowDialog compatibility ─────────────────────────────────────────────
        public new WinForms.DialogResult ShowDialog()
        {
            bool? r = base.ShowDialog();
            return r == true ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }

        public WinForms.DialogResult ShowDialog(WinForms.IWin32Window owner)
        {
            if (owner != null)
                new WindowInteropHelper(this).Owner = owner.Handle;
            return ShowDialog();
        }

        public ViewSetDetailPage(int setId)
        {
            _setId             = setId;
            _repository        = new SetRepository();
            _requestRepository = new RequestRepository();
            InitializeComponent();
            Loaded += (_, __) =>
            {
                SetRepository.SetDispatchStateChanged += OnSetDispatchStateChanged;
                _ = LoadSetDetailsAsync();
            };
            Unloaded += (_, __) => SetRepository.SetDispatchStateChanged -= OnSetDispatchStateChanged;
        }

        // ── Window chrome ─────────────────────────────────────────────────────────────────
        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && !_isMaximized) DragMove();
        }

        private void OnMinimizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Yakult.Inventory.App.Helpers.ModalMinimizeGuard.Minimize(this);
        }

        private double _restoreLeft, _restoreTop, _restoreWidth, _restoreHeight;

        private void OnMaximizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_isMaximized)
            {
                Left   = _restoreLeft;
                Top    = _restoreTop;
                Width  = _restoreWidth;
                Height = _restoreHeight;
                BtnMaximizeGlyph.Text = "□";
                _isMaximized = false;
            }
            else
            {
                _restoreLeft   = Left;
                _restoreTop    = Top;
                _restoreWidth  = Width;
                _restoreHeight = Height;

                var wa = SystemParameters.WorkArea;
                Left   = wa.Left;
                Top    = wa.Top;
                Width  = wa.Width;
                Height = wa.Height;
                BtnMaximizeGlyph.Text = "❐";
                _isMaximized = true;
            }
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ── Data loading ──────────────────────────────────────────────────────────────────
        // GetSetRequestsAsync orders by DateCreated DESC for grid display, but every request in a
        // batch gets the same DateCreated timestamp, so requests[0] is not stable — SQL Server ties
        // are broken arbitrarily. Company/Department/Branch for a Dept-Level Set are only ever fully
        // populated on ONE of the linked requests, so an unstable "first" pick makes those fields go
        // blank intermittently on this page and in the generated QR code/PDF.
        // Pick the request with the lowest ReqId (the earliest one added to the Set) instead, matching
        // the deterministic "top_req" convention SetRepository already uses for the Set list view.
        private static int GetRepresentativeReqId(List<SetDetailRequestDto> requests)
            => requests.OrderBy(r => r.ReqId).First().ReqId;

        /// <summary>
        /// Employee/Dept details for QR/PDF/Requisition generation: from the representative
        /// Request for normal Sets, or from dbo.[Set]'s own CurrentEmployeeId/org-unit
        /// columns for Sets with items but no Request rows (Renewal/Invoice-created).
        /// </summary>
        private Task<EmployeeDetailDto> GetEmployeeDetailForDocumentAsync(List<SetDetailRequestDto> requests)
            => requests.Count > 0
                ? _repository.GetEmployeeDetailsForRequestAsync(GetRepresentativeReqId(requests))
                : _repository.GetEmployeeDetailsForSetAsync(_setId);

        /// <summary>
        /// The item list to embed in QR/PDF/Requisition output: dbo.Request-sourced rows for
        /// normal Sets, or dbo.SetItem-sourced rows (via GetSetItemsAsRequestsAsync) for Sets
        /// with items but no Request rows (Renewal/Invoice-created) — so those documents list
        /// the real items instead of an empty item list.
        /// </summary>
        private async Task<List<SetDetailRequestDto>> GetRequestsForDocumentAsync(List<SetDetailRequestDto> requests)
            => requests.Count > 0 ? requests : await _repository.GetSetItemsAsRequestsAsync(_setId);

        private async Task LoadSetDetailsAsync()
        {
            try
            {
                Mouse.OverrideCursor = WinCursor.Wait;

                var setDto = await _repository.GetSetByIdAsync(_setId);
                if (setDto == null)
                {
                    Mouse.OverrideCursor = null;
                    WinMsgBox.Show("Set not found. It may have been deleted.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                // Header fields
                TxtSetCode.Text   = setDto.SetCode;
                TxtCreatedBy.Text = setDto.CreatedByName;
                DtpCreatedAt.SelectedDate = setDto.CreatedAt.Date;
                TxtDateRequested.Text = setDto.DateRequested?.ToString("MM/dd/yyyy") ?? "—";
                TxtRemarks.Text = setDto.Remarks ?? "";
                bool hasQrImage   = !string.IsNullOrWhiteSpace(setDto.QRImagePath) || (setDto.QRImageData != null && setDto.QRImageData.Length > 0);
                TxtQRImage.Text   = hasQrImage
                    ? (setDto.QRImageData != null && setDto.QRImageData.Length > 0 ? "(Stored in database)" : setDto.QRImagePath)
                    : "(Not generated)";
                _currentQrImageData = setDto.QRImageData;
                BtnViewQR.Visibility = (setDto.QRImageData != null && setDto.QRImageData.Length > 0)
                    ? Visibility.Visible : Visibility.Collapsed;
                TxtItemCount.Text = setDto.ItemCount.ToString();

                // Financial fields
                TxtSubtotal.Text       = setDto.Subtotal.ToString("N2");
                TxtVatAmount.Text      = setDto.VatAmount.ToString("N2");
                TxtDiscountAmount.Text = setDto.DiscountAmount.ToString("N2");
                TxtWhtAmount.Text      = setDto.WhtAmount.ToString("N2");
                TxtTotalAmountDue.Text = setDto.TotalAmountDue.ToString("N2");

                Title = $"Set Details - {setDto.SetCode}";
                TitleText.Text = $"📋 Set Details — {setDto.SetCode}";

                // Requests DataGrid — for Sets with items but no Request rows at all
                // (Renewal/Invoice-created), fall back to dbo.SetItem so the grid isn't left
                // empty just because this Set never went through the Request pipeline.
                var requests = await _repository.GetSetRequestsAsync(_setId);
                if (requests.Count == 0 && setDto.ItemCount > 0)
                {
                    var setItemRows = await _repository.GetSetItemsAsRequestsAsync(_setId);
                    _displayRows = setItemRows.Select(SetRequestDisplayRow.FromSetItem).ToList();
                }
                else
                {
                    _displayRows = requests.Select(SetRequestDisplayRow.From).ToList();
                }

                _currentSetType = setDto.SetType;
                bool isHardwareSet = string.Equals(setDto.SetType, "Hardware", StringComparison.OrdinalIgnoreCase);
                bool hasCpu = requests != null && requests.Any(r =>
                    r != null && string.Equals(r.Category, "CPU", StringComparison.OrdinalIgnoreCase));

                SetHardwareInfoVisibility(isHardwareSet && hasCpu);
                ConfigureCpuInfoColumns(isHardwareSet && hasCpu);

                TxtComputerName.Text = setDto.ComputerName ?? string.Empty;
                TxtIPAddress.Text    = setDto.IPAddress    ?? string.Empty;
                BtnSaveHardwareInfo.IsEnabled = isHardwareSet && hasCpu;

                ApplyCpuInfoToGrid(setDto.ComputerName, setDto.IPAddress);

                DgvRequests.ItemsSource = null;
                DgvRequests.ItemsSource = _displayRows;

                // A Set with items but no Requests was created outside the Request pipeline --
                // Renew Items and Invoice creation both do this (write SetItem directly).
                bool hasItemsWithNoRequests = requests.Count == 0 && setDto.ItemCount > 0;
                PnlNoRequestsNote.Visibility = hasItemsWithNoRequests
                    ? Visibility.Visible : Visibility.Collapsed;

                // Employee/Dept assignment source: dbo.Request for normal Sets, dbo.[Set]'s
                // own CurrentEmployeeId/CurrentDepartmentId/CurrentBranchId/ComId columns for
                // Sets with items but no Request rows (Renewal/Invoice). Only a truly empty
                // Set (no items either way) has nothing to assign.
                _isRequestBasedSet = requests.Count > 0;

                if (_isRequestBasedSet || hasItemsWithNoRequests)
                {
                    var employeeDetail = _isRequestBasedSet
                        ? await _repository.GetEmployeeDetailsForRequestAsync(GetRepresentativeReqId(requests))
                        : await _repository.GetEmployeeDetailsForSetAsync(_setId);

                    if (employeeDetail != null)
                    {
                        _currentEmployeeId     = employeeDetail.EmpId;
                        await LoadEmployeesAsync();

                        TxtEmployeeNumber.Text = employeeDetail.EmployeeNumber ?? (employeeDetail.EmpId <= 0 ? "—" : "N/A");
                        TxtCompany.Text        = employeeDetail.CompanyName    ?? "—";
                        TxtBranch.Text         = employeeDetail.BranchName     ?? "—";
                        TxtDepartment.Text     = employeeDetail.DepartmentName ?? "—";
                        TxtDistributor.Text    = setDto.DistributorName        ?? "—";

                        if (employeeDetail.EmpId <= 0)
                        {
                            await LoadOrgUnitsAsync();
                            SwitchToDeptMode();
                        }
                        else
                        {
                            SwitchToEmployeeMode();
                        }
                    }
                    else
                    {
                        TxtEmployeeNumber.Text = "N/A";
                        TxtCompany.Text = TxtBranch.Text = TxtDepartment.Text = "N/A";
                        TxtDistributor.Text = setDto.DistributorName ?? "—";
                        SwitchToEmployeeMode();
                    }
                }
                else
                {
                    TxtEmployeeNumber.Text = "N/A";
                    TxtCompany.Text = TxtBranch.Text = TxtDepartment.Text = "N/A";
                    TxtDistributor.Text = setDto.DistributorName ?? "—";
                }

                LoadReceivedByCombo(setDto.ReceivedById);

                // Deploy button state
                bool isCartridgeSet = string.Equals(setDto.SetType, "Cartridge", StringComparison.OrdinalIgnoreCase);
                bool isDispatched   = string.Equals(setDto.Status, "Dispatched", StringComparison.OrdinalIgnoreCase);
                bool hasQr          = !string.IsNullOrWhiteSpace(setDto.QRImagePath) || (setDto.QRImageData != null && setDto.QRImageData.Length > 0);

                BtnGenerateQR.IsEnabled = !isCartridgeSet;

                if (isDispatched)
                {
                    _isDeployed            = true;
                    BtnDeploy.Content      = "Deployed";
                    BtnDeploy.IsEnabled    = false;
                }
                else
                {
                    _isDeployed            = false;
                    BtnDeploy.Content      = "🚀 Deploy";
                    BtnDeploy.IsEnabled    = isCartridgeSet || hasQr;
                }

                // Record Invoice / View Invoice — only meaningful for a genuine
                // Request-based dispatch Set (SetItem-based invoices are created directly
                // via SoftwareServiceSetDialog, not through this flow).
                if (setDto.IsInvoice)
                {
                    BtnRecordInvoice.Visibility = Visibility.Collapsed;
                    BtnViewInvoice.Visibility   = Visibility.Visible;
                }
                else if (_isRequestBasedSet)
                {
                    BtnRecordInvoice.Visibility = Visibility.Visible;
                    BtnViewInvoice.Visibility   = Visibility.Collapsed;
                }
                else
                {
                    BtnRecordInvoice.Visibility = Visibility.Collapsed;
                    BtnViewInvoice.Visibility   = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load set details:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ── Hardware visibility ───────────────────────────────────────────────────────────
        private void SetHardwareInfoVisibility(bool visible)
        {
            HwSectionPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ConfigureCpuInfoColumns(bool visible)
        {
            var vis = visible ? Visibility.Visible : Visibility.Collapsed;
            ColComputerName.Visibility = vis;
            ColIPAddress.Visibility    = vis;
        }

        private void ApplyCpuInfoToGrid(string computerName, string ipAddress)
        {
            foreach (var row in _displayRows)
            {
                bool isCpu = string.Equals(row.Category, "CPU", StringComparison.OrdinalIgnoreCase);
                row.ComputerName = isCpu ? (computerName ?? string.Empty) : string.Empty;
                row.IPAddress    = isCpu ? (ipAddress    ?? string.Empty) : string.Empty;
            }
            DgvRequests.Items.Refresh();
        }

        // ── Employee / org combos ─────────────────────────────────────────────────────────
        private async Task LoadEmployeesAsync()
        {
            try
            {
                // Scoped to the Set's current Company/Branch/Department (matched by the display
                // names already shown in TxtCompany/TxtBranch/TxtDepartment) rather than the flat
                // "every employee in the system" list — was previously unfiltered.
                string NormalizeOrgName(string text) =>
                    string.IsNullOrWhiteSpace(text) || text == "—" || text == "N/A" ? null : text;

                var employees = await _requestRepository.GetEmployeesByOrgNamesAsync(
                    NormalizeOrgName(TxtCompany.Text), NormalizeOrgName(TxtBranch.Text), NormalizeOrgName(TxtDepartment.Text));
                var items = new List<EmployeeItem> { new EmployeeItem { EmpId = 0, DisplayText = "-- None --" } };
                items.AddRange(employees.Select(e => new EmployeeItem
                {
                    EmpId       = e.EmpId,
                    DisplayText = $"{e.Name} ({e.Position})"
                }));

                CmbEmployee.DisplayMemberPath = "DisplayText";
                CmbEmployee.SelectedValuePath = "EmpId";
                CmbEmployee.ItemsSource       = items;

                var selected = items.FirstOrDefault(i => i.EmpId == _currentEmployeeId) ?? items[0];
                CmbEmployee.SelectedItem = selected;
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load employees:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadReceivedByCombo(int? currentReceivedById)
        {
            try
            {
                var employees = _repository.GetActiveEmployeesForReceiver();
                var items = new List<ReceivedByItem> { new ReceivedByItem(0, "— Not Set —") };

                foreach (var emp in employees)
                {
                    string empNum  = !string.IsNullOrWhiteSpace(emp.EmployeeNumber) ? $" ({emp.EmployeeNumber})" : "";
                    string deptPart= !string.IsNullOrWhiteSpace(emp.DepartmentName) ? $" – {emp.DepartmentName}" : "";
                    items.Add(new ReceivedByItem(emp.EmpId, $"{emp.Name}{empNum}{deptPart}"));
                }

                CmbReceivedBy.ItemsSource = items;
                CmbReceivedBy.SelectedIndex = 0;

                if (currentReceivedById.HasValue && currentReceivedById.Value > 0)
                {
                    var match = items.FirstOrDefault(i => i.EmpId == currentReceivedById.Value);
                    if (match != null) CmbReceivedBy.SelectedItem = match;
                }
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load receiver list:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Task LoadOrgUnitsAsync()
        {
            _orgCompanies.Clear();
            _orgDepts.Clear();
            _orgBranches.Clear();

            try
            {
                var cs = DatabaseConfig.ConnectionString;
                if (string.IsNullOrWhiteSpace(cs)) return Task.CompletedTask;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();

                    var companies = new List<OrgSetItem>();
                    using (var cmd = new SqlCommand("SELECT ComId, Name FROM dbo.Company ORDER BY Name", con))
                    using (var rdr = cmd.ExecuteReader())
                        while (rdr.Read())
                        {
                            var item = new OrgSetItem { Id = rdr.GetInt32(0), Name = rdr.GetString(1) };
                            _orgCompanies.Add(item);
                            companies.Add(item);
                        }
                    CmbCompanyEdit.ItemsSource = companies;

                    var depts = new List<OrgSetItem> { new OrgSetItem { Id = 0, Name = "-- None --" } };
                    using (var cmd = new SqlCommand("SELECT DeptId, Name FROM dbo.Department ORDER BY Name", con))
                    using (var rdr = cmd.ExecuteReader())
                        while (rdr.Read())
                        {
                            var item = new OrgSetItem { Id = rdr.GetInt32(0), Name = rdr.GetString(1) };
                            _orgDepts.Add(item);
                            depts.Add(item);
                        }
                    CmbDeptEdit.ItemsSource = depts;

                    var branches = new List<OrgSetItem> { new OrgSetItem { Id = 0, Name = "-- None --" } };
                    using (var cmd = new SqlCommand("SELECT BranchId, Name FROM dbo.Branch ORDER BY Name", con))
                    using (var rdr = cmd.ExecuteReader())
                        while (rdr.Read())
                        {
                            var item = new OrgSetItem { Id = rdr.GetInt32(0), Name = rdr.GetString(1) };
                            _orgBranches.Add(item);
                            branches.Add(item);
                        }
                    CmbBranchEdit.ItemsSource = branches;

                    // Independent sales distributors (dbo.Distributor). Missing table
                    // on older DBs simply leaves the "(None)" default.
                    _orgDistributors.Clear();
                    var distributors = new List<OrgSetItem> { new OrgSetItem { Id = 0, Name = "(None)" } };
                    try
                    {
                        using (var cmd = new SqlCommand(
                            "IF OBJECT_ID('dbo.Distributor', 'U') IS NOT NULL SELECT DistributorId, Name FROM dbo.Distributor WHERE IsActive = 1 ORDER BY SortOrder, Name", con))
                        using (var rdr = cmd.ExecuteReader())
                            while (rdr.Read())
                            {
                                var item = new OrgSetItem { Id = rdr.GetInt32(0), Name = rdr.GetString(1) };
                                _orgDistributors.Add(item);
                                distributors.Add(item);
                            }
                    }
                    catch
                    {
                        // Distributor catalog unavailable — keep "(None)" only.
                    }
                    CmbDistributorEdit.ItemsSource = distributors;
                }

                SelectOrgCombo(CmbCompanyEdit, TxtCompany.Text);
                SelectOrgCombo(CmbDeptEdit,    TxtDepartment.Text);
                SelectOrgCombo(CmbBranchEdit,  TxtBranch.Text);
                SelectOrgCombo(CmbDistributorEdit, TxtDistributor.Text);
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load org units:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return Task.CompletedTask;
        }

        private void SelectOrgCombo(System.Windows.Controls.ComboBox cmb, string currentName)
        {
            if (string.IsNullOrWhiteSpace(currentName) || currentName == "—") return;
            foreach (var item in cmb.Items.OfType<OrgSetItem>())
            {
                if (string.Equals(item.Name, currentName, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedItem = item;
                    return;
                }
            }
        }

        // ── Mode switching (Employee vs Dept-level) ───────────────────────────────────────
        private void SwitchToDeptMode()
        {
            _isDeptLevel = true;
            CmbEmployee.Visibility      = Visibility.Collapsed;
            LblDeptLevelTag.Visibility  = Visibility.Visible;
            BtnSwitchMode.Content       = "⇄ Employee";

            CmbCompanyEdit.Visibility   = Visibility.Visible;
            TxtCompany.Visibility       = Visibility.Collapsed;
            CmbDeptEdit.Visibility      = Visibility.Visible;
            TxtDepartment.Visibility    = Visibility.Collapsed;
            CmbBranchEdit.Visibility    = Visibility.Visible;
            TxtBranch.Visibility        = Visibility.Collapsed;
            CmbDistributorEdit.Visibility = Visibility.Visible;
            TxtDistributor.Visibility     = Visibility.Collapsed;

            TxtEmployeeNumber.Text = "—";
        }

        private void SwitchToEmployeeMode()
        {
            _isDeptLevel = false;
            CmbEmployee.Visibility      = Visibility.Visible;
            LblDeptLevelTag.Visibility  = Visibility.Collapsed;
            BtnSwitchMode.Content       = "⇄ Dept.";

            CmbCompanyEdit.Visibility   = Visibility.Collapsed;
            TxtCompany.Visibility       = Visibility.Visible;
            CmbDeptEdit.Visibility      = Visibility.Collapsed;
            TxtDepartment.Visibility    = Visibility.Visible;
            CmbBranchEdit.Visibility    = Visibility.Collapsed;
            TxtBranch.Visibility        = Visibility.Visible;
            CmbDistributorEdit.Visibility = Visibility.Collapsed;
            TxtDistributor.Visibility     = Visibility.Visible;
        }

        private async void BtnSwitchMode_Click(object sender, RoutedEventArgs e)
        {
            if (_isDeptLevel)
            {
                // Re-scope the Employee dropdown to the Set's current Company/Branch/Department —
                // LoadEmployeesAsync was previously only ever called once, at page load.
                await LoadEmployeesAsync();
                SwitchToEmployeeMode();
            }
            else
            {
                if (_orgCompanies.Count == 0)
                    await LoadOrgUnitsAsync();
                SwitchToDeptMode();
            }
        }

        // ── Save / Transfer buttons ───────────────────────────────────────────────────────
        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isDeptLevel)
                {
                    var comItem = CmbCompanyEdit.SelectedItem as OrgSetItem;
                    var distItem = CmbDistributorEdit.SelectedItem as OrgSetItem;
                    int? saveDistributorId = (distItem != null && distItem.Id > 0) ? (int?)distItem.Id : null;

                    // Distributors are independent of Company/Dept/Branch:
                    // a distributor-only set needs no Company.
                    if ((comItem == null || comItem.Id <= 0) && !saveDistributorId.HasValue)
                    {
                        WinMsgBox.Show("Please select a Company or a Distributor.", "Validation Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var deptItem   = CmbDeptEdit.SelectedItem   as OrgSetItem;
                    var branchItem = CmbBranchEdit.SelectedItem as OrgSetItem;

                    int? saveDeptId   = (deptItem   != null && deptItem.Id   > 0) ? (int?)deptItem.Id   : null;
                    int? saveBranchId = (branchItem != null && branchItem.Id > 0) ? (int?)branchItem.Id : null;

                    var confirm = WinMsgBox.Show(
                        $"Transfer set to dept. level:\n\n{(comItem != null && comItem.Id > 0 ? comItem.Name : "(No Company)")}" +
                        (deptItem   != null && deptItem.Id   > 0 ? $"\n{deptItem.Name}"   : "") +
                        (branchItem != null && branchItem.Id > 0 ? $"\n{branchItem.Name}" : "") +
                        (saveDistributorId.HasValue ? $"\nDistributor: {distItem.Name}" : "") +
                        (_isRequestBasedSet
                            ? "\n\nThis will clear the employee on all requests in this set.\n\nContinue?"
                            : "\n\nContinue?"),
                        "Confirm Transfer", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (confirm != MessageBoxResult.Yes) return;

                    Mouse.OverrideCursor        = WinCursor.Wait;
                    BtnSaveTransfer.IsEnabled   = false;
                    BtnSaveTransfer.Content     = "Saving...";

                    if (comItem != null && comItem.Id > 0)
                    {
                        if (_isRequestBasedSet)
                            await _repository.TransferSetOwnershipToDeptAsync(_setId, comItem.Id, saveDeptId, saveBranchId);
                        else
                            await _repository.UpdateSetLevelOrgUnitAsync(_setId, comItem.Id, saveDeptId, saveBranchId, AppSession.CurrentUserId);
                    }

                    await _repository.UpdateSetDistributorAsync(_setId, saveDistributorId, AppSession.CurrentUserId);

                    _currentEmployeeId = 0;
                    WinMsgBox.Show("Set ownership transferred to dept. level.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadSetDetailsAsync();
                }
                else
                {
                    var selectedEmployee = CmbEmployee.SelectedItem as EmployeeItem;
                    if (selectedEmployee == null || selectedEmployee.EmpId <= 0)
                    {
                        WinMsgBox.Show("Please select a valid employee to transfer ownership to.",
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (selectedEmployee.EmpId == _currentEmployeeId)
                    {
                        WinMsgBox.Show("The selected employee is already the current owner.", "No Change",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var result = WinMsgBox.Show(
                        $"Transfer set ownership to:\n\n{selectedEmployee.DisplayText}\n\n" +
                        (_isRequestBasedSet
                            ? "This will update all requests in this set to the new employee.\n\nContinue?"
                            : "Continue?"),
                        "Confirm Transfer", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result != MessageBoxResult.Yes) return;

                    Mouse.OverrideCursor      = WinCursor.Wait;
                    BtnSaveTransfer.IsEnabled = false;
                    BtnSaveTransfer.Content   = "Saving...";

                    if (_isRequestBasedSet)
                        await _repository.TransferSetOwnershipAsync(_setId, selectedEmployee.EmpId);
                    else
                        await _repository.UpdateSetLevelEmployeeAsync(_setId, selectedEmployee.EmpId, AppSession.CurrentUserId);

                    _currentEmployeeId = selectedEmployee.EmpId;
                    WinMsgBox.Show("Set ownership transferred successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadSetDetailsAsync();
                }
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to transfer ownership:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor      = null;
                BtnSaveTransfer.IsEnabled = true;
                BtnSaveTransfer.Content   = "💾 Save Transfer";
            }
        }

        private void BtnSaveReceivedBy_Click(object sender, RoutedEventArgs e)
        {
            var chosen = CmbReceivedBy.SelectedItem as ReceivedByItem;
            if (chosen == null) return;

            try
            {
                int? newId  = chosen.EmpId > 0 ? (int?)chosen.EmpId : null;
                int  userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
                _repository.UpdateReceivedByForSet(_setId, newId, userId);
                WinMsgBox.Show("Received By saved.", "Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Could not save receiver:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Hardware info ─────────────────────────────────────────────────────────────────
        private async void BtnSaveHardwareInfo_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(_currentSetType, "Hardware", StringComparison.OrdinalIgnoreCase))
            {
                WinMsgBox.Show("Hardware details are only applicable to Hardware sets.", "Not Supported",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                BtnSaveHardwareInfo.IsEnabled = false;
                Mouse.OverrideCursor = WinCursor.Wait;

                string computerName = NormalizeHardwareValue(TxtComputerName.Text);
                string ipAddress    = NormalizeHardwareValue(TxtIPAddress.Text);

                await _repository.UpdateSetHardwareInfoAsync(_setId, computerName, ipAddress);
                ApplyCpuInfoToGrid(computerName, ipAddress);

                WinMsgBox.Show("Hardware information saved.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to save hardware info:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor          = null;
                BtnSaveHardwareInfo.IsEnabled = true;
            }
        }

        private static string NormalizeHardwareValue(string value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        // ── Created At (correction for mis-keyed encoding date) ───────────────────────────
        private async void BtnSaveCreatedAt_Click(object sender, RoutedEventArgs e)
        {
            if (!DtpCreatedAt.SelectedDate.HasValue)
            {
                WinMsgBox.Show("Please pick a date.", "No Date Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                BtnSaveCreatedAt.IsEnabled = false;
                Mouse.OverrideCursor = WinCursor.Wait;

                await _repository.UpdateSetCreatedAtAsync(_setId, DtpCreatedAt.SelectedDate.Value);

                WinMsgBox.Show("Created At date saved.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to save Created At date:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor      = null;
                BtnSaveCreatedAt.IsEnabled = true;
            }
        }

        // ── Remarks ────────────────────────────────────────────────────────────────────────
        private async void BtnSaveRemarks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnSaveRemarks.IsEnabled = false;
                Mouse.OverrideCursor = WinCursor.Wait;

                await _repository.UpdateSetRemarksAsync(_setId, TxtRemarks.Text);

                WinMsgBox.Show("Remarks saved.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to save Remarks:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor    = null;
                BtnSaveRemarks.IsEnabled = true;
            }
        }

        // ── QR / PDF / Report ─────────────────────────────────────────────────────────────
        private void BtnViewQR_Click(object sender, RoutedEventArgs e)
        {
            if (_currentQrImageData == null || _currentQrImageData.Length == 0)
            {
                WinMsgBox.Show("No QR code image is stored for this set.", "No QR Code",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var viewer = new Yakult.Inventory.App.Pages.ImageViewerDialog(_currentQrImageData, $"{TxtSetCode.Text} - QR Code");
                viewer.ShowDialog(new WpfWin32Window(this));
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to open QR code image:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnGenerateQR_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor       = WinCursor.Wait;
                BtnGenerateQR.IsEnabled    = false;
                BtnGenerateQR.Content      = "Generating...";

                var setDto = await _repository.GetSetByIdAsync(_setId);
                if (setDto == null)
                {
                    WinMsgBox.Show("Set not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var requests = await _repository.GetSetRequestsAsync(_setId);
                if (requests.Count == 0 && setDto.ItemCount == 0)
                {
                    WinMsgBox.Show(
                        "Cannot generate QR code for an empty set.\n\nPlease add at least one item first.",
                        "No Items", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var employeeDetail = await GetEmployeeDetailForDocumentAsync(requests);
                if (employeeDetail == null)
                {
                    WinMsgBox.Show(
                        "Failed to retrieve employee details for this set.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var documentItems = await GetRequestsForDocumentAsync(requests);

                string qrDataJson   = SetQRGenerator.GenerateQRDataString(setDto, documentItems, employeeDetail);
                var    qrImage      = SetQRGenerator.GenerateQRCodeImage(setDto, documentItems, employeeDetail);
                byte[] qrImageBytes = SetQRGenerator.GetQRCodeImageBytes(qrImage);

                await _repository.UpdateQRDataAsync(_setId, qrImageBytes, qrDataJson);
                await _requestRepository.UpdateHardwareRequestsToSubmittedAsync(_setId);

                WinMsgBox.Show(
                    $"QR Code generated successfully!\n\n" +
                    $"Employee: {employeeDetail.EmployeeName}\n" +
                    $"Company: {employeeDetail.CompanyName}\n" +
                    $"Department: {employeeDetail.DepartmentName}\n" +
                    $"Branch: {employeeDetail.BranchName}\n\n" +
                    "Hardware requests have been marked as 'Submitted'.",
                    "QR Generated", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadSetDetailsAsync();
            }
            catch (Exception ex)
            {
                WinMsgBox.Show(
                    $"Failed to generate QR code:\n\n{ex.Message}\n\nDetails: {ex.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor    = null;
                BtnGenerateQR.IsEnabled = true;
                BtnGenerateQR.Content   = "QR Code";
            }
        }

        private async void BtnGeneratePDF_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor        = WinCursor.Wait;
                BtnGeneratePDF.IsEnabled    = false;
                BtnGeneratePDF.Content      = "Generating...";

                var setDto = await _repository.GetSetByIdAsync(_setId);
                if (setDto == null)
                {
                    WinMsgBox.Show("Set not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(setDto.QRImagePath) && (setDto.QRImageData == null || setDto.QRImageData.Length == 0))
                {
                    WinMsgBox.Show(
                        "Please generate a QR code first before creating a PDF.",
                        "No QR Code", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var requests = await _repository.GetSetRequestsAsync(_setId);
                if (requests.Count == 0 && setDto.ItemCount == 0)
                {
                    WinMsgBox.Show(
                        "Cannot generate PDF for an empty set.",
                        "No Items", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var employeeDetail = await GetEmployeeDetailForDocumentAsync(requests);
                if (employeeDetail == null)
                {
                    WinMsgBox.Show("Failed to retrieve employee details for this set.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var documentItems = await GetRequestsForDocumentAsync(requests);

                string pdfPath = SetQRGenerator.GenerateDispatchPDF(setDto, documentItems, employeeDetail, setDto.QRImageData);
                await _requestRepository.UpdateHardwareRequestsToSubmittedAsync(_setId);

                WinMsgBox.Show(
                    $"Dispatch PDF generated successfully!\n\n" +
                    $"Employee: {employeeDetail.EmployeeName}\n" +
                    $"Company: {employeeDetail.CompanyName}\n" +
                    $"Department: {employeeDetail.DepartmentName}\n" +
                    $"Branch: {employeeDetail.BranchName}\n\n" +
                    $"Saved to: {pdfPath}\n\n" +
                    "Hardware requests have been marked as 'Submitted'.",
                    "PDF Generated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WinMsgBox.Show(
                    $"Failed to generate PDF:\n\n{ex.Message}\n\nDetails: {ex.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor     = null;
                BtnGeneratePDF.IsEnabled = true;
                BtnGeneratePDF.Content   = "📄 PDF";
            }
        }

        private void BtnGenerateReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_setId <= 0)
                {
                    WinMsgBox.Show("Invalid Set ID.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!ReportHelper.ReportExists("SetReport.rdl"))
                {
                    WinMsgBox.Show(
                        "Report file 'SetReport.rdl' not found in the Reports folder.",
                        "Report Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var parameters = new ReportHelper.ReportParameterBuilder()
                    .AddParameter("SetId", _setId)
                    .Build();

                string reportTitle = !string.IsNullOrEmpty(TxtSetCode.Text)
                    ? $"Dispatch Report - {TxtSetCode.Text}"
                    : "Dispatch Report";

                ReportHelper.ShowReportWithParameters("SetReport.rdl", reportTitle, parameters);
            }
            catch (FileNotFoundException ex)
            {
                WinMsgBox.Show($"Report file not found: {ex.Message}", "File Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Error generating report:\n\n{ex.Message}", "Report Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Financial calculations ────────────────────────────────────────────────────────
        //
        // Calculation chain, matching the paper sales invoice layout
        // ("Total Sale [VAT-inclusive] / Less VAT / Total Sale [net of VAT]") — kept in sync
        // with TryCalculateFinancialsFromPercentages() in ViewInvoiceDetailPage.cs:
        //
        //   Subtotal            -- gross amount, VAT already included
        //     -> VATable Amount = Subtotal / (1 + vatRate)          (strip VAT out of the gross)
        //     -> VAT Amount     = Subtotal - VATable Amount         (the VAT that was stripped out; NOT added back)
        //     -> Discount       = VATable Amount * discountRate     (discount applies to the net base)
        //     -> Net After Disc = VATable Amount - Discount
        //     -> WHT Amount     = Net After Disc * whtRate
        //     -> Total Due      = Net After Disc - WHT Amount
        //
        // VAT% and WHT% are both plain percentages (enter 12 for 12%) and are only divided by
        // 100 once each. Money values are only rounded at the final "N2" display step.
        private async void BtnCalculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!decimal.TryParse(TxtSubtotal.Text, out decimal subtotal))
                {
                    WinMsgBox.Show("Invalid Subtotal value.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!decimal.TryParse(TxtVatPercent.Text, out decimal vatPercent))
                {
                    WinMsgBox.Show("Invalid VAT % value.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!decimal.TryParse(TxtDiscountPercent.Text, out decimal discountPercent)) discountPercent = 0;
                if (!decimal.TryParse(TxtWhtPercent.Text,      out decimal whtPercent))      whtPercent      = 0;

                decimal vatRate      = vatPercent      / 100m;
                decimal discountRate = discountPercent / 100m;
                decimal whtRate      = whtPercent      / 100m;

                // Strip VAT out of the VAT-inclusive Subtotal to get the taxable (VATable) base.
                decimal vatableAmount = subtotal / (1 + vatRate);
                decimal vatAmount     = subtotal - vatableAmount;

                // Discount is taken off the VATable base, not the VAT-inclusive gross.
                decimal discountAmount   = vatableAmount * discountRate;
                decimal netAfterDiscount = vatableAmount - discountAmount;

                // WHT is computed on the post-discount, net-of-VAT base.
                decimal whtAmount = netAfterDiscount * whtRate;

                // VAT is not added back in — Total Amount Due is net of VAT and WHT.
                decimal totalAmountDue = netAfterDiscount - whtAmount;

                TxtVatAmount.Text      = vatAmount.ToString("N2");
                TxtDiscountAmount.Text = discountAmount.ToString("N2");
                TxtWhtAmount.Text      = whtAmount.ToString("N2");
                TxtTotalAmountDue.Text = totalAmountDue.ToString("N2");

                Mouse.OverrideCursor     = WinCursor.Wait;
                BtnCalculate.IsEnabled   = false;
                BtnCalculate.Content     = "Saving...";

                await _repository.UpdateFinancialCalculationsAsync(
                    _setId, subtotal, vatAmount, discountAmount, whtAmount, totalAmountDue);

                string breakdown = "Calculation Breakdown:\n\n" +
                    $"Subtotal (VAT Included): {subtotal:N2}\n" +
                    $"- VAT ({vatPercent}%): {vatAmount:N2}\n" +
                    $"= VATable Amount: {vatableAmount:N2}\n\n";

                if (discountAmount > 0)
                    breakdown +=
                        $"- Discount ({discountPercent}%): {discountAmount:N2}\n" +
                        $"= Net After Discount: {netAfterDiscount:N2}\n\n" +
                        $"- WHT ({whtPercent}% on discounted net): {whtAmount:N2}\n";
                else
                    breakdown += $"- WHT ({whtPercent}%): {whtAmount:N2}\n";

                breakdown += $"= Total Amount Due: {totalAmountDue:N2}\n\n✅ Financial calculations saved!";

                WinMsgBox.Show(breakdown, "Calculation Result",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Calculation error:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor   = null;
                BtnCalculate.IsEnabled = true;
                BtnCalculate.Content   = "🧮 Calculate";
            }
        }

        // ── Grid action buttons ───────────────────────────────────────────────────────────
        private async void BtnAddRequest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dialog = new SelectRequestForSetDialog(_setId, _repository))
                {
                    dialog.Owner = this;
                    if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                        await LoadSetDetailsAsync();
                }
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to add request:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnRemoveRequest_Click(object sender, RoutedEventArgs e)
        {
            var selected = DgvRequests.SelectedItem as SetRequestDisplayRow;
            if (selected == null)
            {
                WinMsgBox.Show("Please select a request to remove.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selected.IsSetItemRow)
            {
                WinMsgBox.Show(
                    "This item was added via Renewal or Invoice, not a Request, and can't be removed from here.",
                    "Not Removable", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = WinMsgBox.Show(
                $"Remove this request from the set?\n\n" +
                $"Request ID: {selected.ReqId}\n" +
                $"Item: {selected.ItemName}\n" +
                $"Quantity: {selected.Quantity}\n\n" +
                $"The request will NOT be deleted, only removed from this set.",
                "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                Mouse.OverrideCursor = WinCursor.Wait;
                await _repository.RemoveRequestFromSetAsync(selected.ReqId);
                WinMsgBox.Show("Request removed from set successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadSetDetailsAsync();
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to remove request:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ── Refresh ───────────────────────────────────────────────────────────────────────
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadSetDetailsAsync();
        }

        // ── Deploy ────────────────────────────────────────────────────────────────────────
        private async void BtnDeploy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = WinMsgBox.Show(
                    "Deploy this set now? This will mark it as dispatched and log the action.",
                    "Confirm Deploy", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                Mouse.OverrideCursor  = WinCursor.Wait;
                BtnDeploy.IsEnabled   = false;
                BtnDeploy.Content     = "Deploying...";

                await _repository.DispatchSetAsync(_setId);

                WinMsgBox.Show(
                    "Set deployed successfully.\n\nUse 'Send Notifications (Sets)' from the menu to send the deployment email.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                _isDeployed         = true;
                BtnDeploy.Content   = "Deployed";
                BtnDeploy.IsEnabled = false;
                await LoadSetDetailsAsync();
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to deploy set:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                BtnDeploy.IsEnabled = true;
                BtnDeploy.Content   = "🚀 Deploy";
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ── Record Invoice / View Invoice ──────────────────────────────────────────────────
        private async void BtnRecordInvoice_Click(object sender, RoutedEventArgs e)
        {
            // Stop the same physical item from getting a second, separate invoice record —
            // if any item on this dispatch Set already has an invoice on a different Set,
            // surface that one instead of opening the "create new invoice" dialog.
            var existing = await _repository.FindExistingInvoiceForSetItemsAsync(_setId);
            if (existing != null)
            {
                var docLabel = string.IsNullOrWhiteSpace(existing.DocumentNumber)
                    ? existing.SetCode
                    : $"{existing.SetCode} (Document #: {existing.DocumentNumber})";

                var response = WinMsgBox.Show(
                    $"One or more items in this set already have an invoice recorded on {docLabel}.\n\n" +
                    "A separate invoice can't be created for the same item.\n\nView that invoice now?",
                    "Invoice Already Recorded", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (response == MessageBoxResult.Yes)
                {
                    new Yakult.Inventory.App.Pages.Invoice.ViewInvoiceDetailPage(existing.SetId)
                        .ShowDialog(new WpfWin32Window(this));
                }

                return;
            }

            var dialog = new RecordSetInvoiceDialog(_setId);
            if (dialog.ShowDialog(new WpfWin32Window(this)) == WinForms.DialogResult.OK)
            {
                // The dialog only stamps the invoice identity + amounts. Site (company/dept/
                // branch), Sub-Type groups and Parent Tags are edited on the full invoice
                // screen — open it straight away so recording an invoice lands where the
                // current invoice features actually live.
                new Yakult.Inventory.App.Pages.Invoice.ViewInvoiceDetailPage(_setId)
                    .ShowDialog(new WpfWin32Window(this));
                _ = LoadSetDetailsAsync();
            }
        }

        private void BtnViewInvoice_Click(object sender, RoutedEventArgs e)
        {
            new Yakult.Inventory.App.Pages.Invoice.ViewInvoiceDetailPage(_setId)
                .ShowDialog(new WpfWin32Window(this));
        }

        // ── Dispatch state change event (from external set operations) ────────────────────
        private void OnSetDispatchStateChanged(int setId, bool isDeployed)
        {
            if (setId != _setId) return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnSetDispatchStateChanged(setId, isDeployed));
                return;
            }

            _isDeployed = isDeployed;
            if (isDeployed)
            {
                BtnDeploy.Content   = "Deployed";
                BtnDeploy.IsEnabled = false;
            }
            else
            {
                BtnDeploy.Content   = "🚀 Deploy";
                BtnDeploy.IsEnabled = true;
            }
        }

        // ── Upgrade Lifecycle (kept as WinForms inline dialog) ────────────────────────────
        private async void BtnUpgradeLifecycle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var setDto = await _repository.GetSetByIdAsync(_setId);
                if (setDto == null)
                {
                    WinMsgBox.Show("Set not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!string.Equals(setDto.SetType, "Hardware", StringComparison.OrdinalIgnoreCase))
                {
                    WinMsgBox.Show(
                        "Lifecycle upgrade is only applicable to Hardware sets.",
                        "Not Supported", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                using (var upgradeDialog = new System.Windows.Forms.Form())
                {
                    upgradeDialog.Text = "Upgrade Set Lifecycle";
                    upgradeDialog.Size = new System.Drawing.Size(920, 640);
                    upgradeDialog.MinimumSize = new System.Drawing.Size(920, 640);
                    upgradeDialog.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                    upgradeDialog.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                    upgradeDialog.MaximizeBox = false;
                    upgradeDialog.MinimizeBox = false;

                    var appBg      = System.Drawing.Color.FromArgb(245, 247, 250);
                    var cardBg     = System.Drawing.Color.White;
                    var surfaceBg  = System.Drawing.Color.FromArgb(250, 251, 252);
                    var border     = System.Drawing.Color.FromArgb(215, 223, 233);
                    var textColor  = System.Drawing.Color.FromArgb(18, 22, 28);
                    var muted      = System.Drawing.Color.FromArgb(90, 100, 110);
                    var accent     = System.Drawing.Color.FromArgb(45, 120, 210);

                    upgradeDialog.BackColor = appBg;

                    void ApplyRoundedRegion()
                    {
                        var handle = CreateRoundRectRgn(0, 0, upgradeDialog.Width + 1, upgradeDialog.Height + 1, 22, 22);
                        try { upgradeDialog.Region = System.Drawing.Region.FromHrgn(handle); }
                        finally { DeleteObject(handle); }
                    }
                    ApplyRoundedRegion();
                    upgradeDialog.SizeChanged += (s, ev) => ApplyRoundedRegion();

                    void EnableDrag(System.Windows.Forms.Control c)
                    {
                        if (c == null) return;
                        c.MouseDown += (s, ev) =>
                        {
                            if (ev.Button != System.Windows.Forms.MouseButtons.Left) return;
                            ReleaseCapture();
                            SendMessage(upgradeDialog.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                        };
                    }

                    var rootLayout = new System.Windows.Forms.TableLayoutPanel
                    {
                        Dock = System.Windows.Forms.DockStyle.Fill,
                        ColumnCount = 1,
                        RowCount = 3,
                        BackColor = cardBg,
                        Padding = new System.Windows.Forms.Padding(24, 18, 24, 18),
                        Margin = new System.Windows.Forms.Padding(0)
                    };
                    rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 92F));
                    rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
                    rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 78F));
                    upgradeDialog.Controls.Add(rootLayout);

                    // Header panel
                    var hdrPanel = new System.Windows.Forms.Panel { Dock = System.Windows.Forms.DockStyle.Fill, BackColor = cardBg };
                    rootLayout.Controls.Add(hdrPanel, 0, 0);
                    EnableDrag(hdrPanel);

                    var titleLbl = new System.Windows.Forms.Label
                    {
                        Text = "Upgrade Lifecycle",
                        AutoSize = true,
                        Location = new System.Drawing.Point(16, 18),
                        ForeColor = textColor,
                        Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold)
                    };
                    var subtitleLbl = new System.Windows.Forms.Label
                    {
                        Text = "You are about to start a new lifecycle for this set.",
                        AutoSize = true,
                        Location = new System.Drawing.Point(18, 50),
                        ForeColor = muted,
                        Font = new System.Drawing.Font("Segoe UI", 9.5F)
                    };
                    hdrPanel.Controls.AddRange(new System.Windows.Forms.Control[] { titleLbl, subtitleLbl });
                    EnableDrag(titleLbl);
                    EnableDrag(subtitleLbl);

                    var btnCloseX = new System.Windows.Forms.Button
                    {
                        Text = "✕",
                        Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold),
                        Size = new System.Drawing.Size(34, 34),
                        FlatStyle = System.Windows.Forms.FlatStyle.Flat,
                        BackColor = surfaceBg,
                        ForeColor = textColor,
                        Cursor = System.Windows.Forms.Cursors.Hand
                    };
                    btnCloseX.FlatAppearance.BorderColor = border;
                    btnCloseX.Click += (s, ev) => { upgradeDialog.DialogResult = System.Windows.Forms.DialogResult.Cancel; upgradeDialog.Close(); };
                    hdrPanel.Controls.Add(btnCloseX);
                    hdrPanel.Resize += (s, ev) => btnCloseX.Location = new System.Drawing.Point(hdrPanel.Width - btnCloseX.Width - 4, 22);

                    // Body panel
                    var bodyPnl = new System.Windows.Forms.Panel
                    {
                        Dock = System.Windows.Forms.DockStyle.Fill,
                        BackColor = System.Drawing.Color.Transparent,
                        AutoScroll = true
                    };
                    rootLayout.Controls.Add(bodyPnl, 0, 1);

                    var infoLbl = new System.Windows.Forms.Label
                    {
                        Text = "Upgrade functionality requires updating the set items.\n\n" +
                               "Please use the 'Upgrade Items' feature from the Set List page, or\n" +
                               "contact your system administrator to perform a lifecycle upgrade.",
                        Dock = System.Windows.Forms.DockStyle.Fill,
                        Font = new System.Drawing.Font("Segoe UI", 10F),
                        ForeColor = muted,
                        Padding = new System.Windows.Forms.Padding(16),
                        TextAlign = System.Drawing.ContentAlignment.MiddleCenter
                    };
                    bodyPnl.Controls.Add(infoLbl);

                    // Footer
                    var footerPnl = new System.Windows.Forms.Panel
                    {
                        Dock = System.Windows.Forms.DockStyle.Fill,
                        BackColor = surfaceBg
                    };
                    rootLayout.Controls.Add(footerPnl, 0, 2);

                    var btnOk = new System.Windows.Forms.Button
                    {
                        Text = "Close",
                        Size = new System.Drawing.Size(120, 36),
                        FlatStyle = System.Windows.Forms.FlatStyle.Flat,
                        BackColor = accent,
                        ForeColor = System.Drawing.Color.White,
                        Cursor = System.Windows.Forms.Cursors.Hand,
                        DialogResult = System.Windows.Forms.DialogResult.OK
                    };
                    btnOk.FlatAppearance.BorderSize = 0;
                    footerPnl.Controls.Add(btnOk);
                    footerPnl.Resize += (s, ev) =>
                    {
                        btnOk.Location = new System.Drawing.Point(
                            footerPnl.Width - btnOk.Width - 16,
                            (footerPnl.Height - btnOk.Height) / 2);
                    };

                    upgradeDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Error opening lifecycle upgrade:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Requisition Form ──────────────────────────────────────────────────────────────
        private async void BtnRequisitionForm_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = WinCursor.Wait;

                var setDto = await _repository.GetSetByIdAsync(_setId);
                if (setDto == null)
                {
                    WinMsgBox.Show("Set not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var requests = await _repository.GetSetRequestsAsync(_setId);
                if (requests.Count == 0 && setDto.ItemCount == 0)
                {
                    WinMsgBox.Show(
                        "Cannot generate a requisition form for an empty set.\n\nPlease add at least one item first.",
                        "No Items", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var employeeDetail = await GetEmployeeDetailForDocumentAsync(requests);
                var documentItems = await GetRequestsForDocumentAsync(requests);

                var vm = RequisitionFormViewModel.FromSet(setDto, documentItems, employeeDetail);

                // Noted By defaults to whoever approved the portal submission this set came
                // from (the manager or supervisor). Best effort: sets without an approved
                // authorization, or an approver who cannot be resolved, just start blank.
                try
                {
                    var approver = await new CartridgeAuthorizationRepository().GetApproverForSetAsync(_setId);
                    if (approver.HasValue)
                    {
                        vm.NotedByName     = approver.Value.Name;
                        vm.NotedByPosition = approver.Value.Position;
                    }
                }
                catch { }

                Mouse.OverrideCursor = null;
                RequisitionFormPrintService.ShowPrintDialog(vm, this);
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to open requisition form:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ── Document images ───────────────────────────────────────────────────────────────
        private void BtnDocumentImages_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dialog = new DocumentImagesDialog(_setId))
                {
                    dialog.ShowDialog(new WpfWin32Window(this));
                }
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Error opening document images:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
