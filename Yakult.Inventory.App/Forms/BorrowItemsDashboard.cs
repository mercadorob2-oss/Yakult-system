using System;
using System.Collections.Generic;
using System.Reflection;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.BorrowItems;
using Yakult.Inventory.App.Pages.Employee;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Wpf.BorrowItems;

namespace Yakult.Inventory.App.Forms.BorrowItems
{
    public sealed class BorrowItemsDashboard : Form
    {
        private enum BorrowDisplayMode
        {
            Board,
            Open,
            History
        }

        private readonly BorrowItemsRepository _repo = new BorrowItemsRepository();
        private readonly List<BorrowEmployeeLookup> _employees = new List<BorrowEmployeeLookup>();
        private readonly List<BorrowEmployeeLookup> _employeesDb = new List<BorrowEmployeeLookup>();
        private readonly List<CompanyDto> _companiesDb = new List<CompanyDto>();
        private readonly List<BorrowLogRow> _open = new List<BorrowLogRow>();
        private readonly List<BorrowLogRow> _history = new List<BorrowLogRow>();
        private BorrowItemLookup _resolvedItem;
        private BorrowLogRow _resolvedOpen;
        private bool _busy;
        private bool _schema;
        private bool _borrowEmpLocked;
        private bool _suppressBorrowEmp;
        private TextBox txtSearch, txtBorrowSerial, txtReturnSerial;
        private ComboBox cboBorrowCompany, cboBorrowDept, cboBorrowEmp, cboReturnCompany, cboReturnDept, cboReturnEmp;
        private Label lblBorrowItem, lblBorrowDesc, lblReturnInfo, lblOpenCount, lblOldest, lblStatus;
        private Label lblEmptyOpen, lblEmptyHistory;
        private Label lblLastRefresh, lblLoading;
        private Label lblBorrowBackdateDate, lblBorrowBackdateTime;
        private ProgressBar pbLoading;
        private Button btnSearch, btnRefresh, btnClearSearch;
        private Button btnHelp;
        private Button btnBackToPortal;
        private Button btnThemeToggle, btnResolveBorrow, btnFindOpen, btnExport, btnReload;
        private Button btnDisplayBoard, btnDisplayOpen, btnDisplayHistory;
        private RadioButton rbBorrowEmpListed, rbBorrowEmpAddNew, rbReturnEmpListed, rbReturnEmpAddNew;
        private CheckBox chkBorrowBackdate;
        private Panel pnlBorrowEmpListed, pnlBorrowEmpAddNew, pnlReturnEmpListed, pnlReturnEmpAddNew;
        private Panel pnlBorrowBackdate;
        private Button btnBorrowAddEmp, btnReturnAddEmp;
        private Button btnBorrow, btnReturn, btnAddNewItem;
        private DateTimePicker dtpBorrowDate, dtpBorrowTime;
        private Panel _headerPanel, _contentPanel, _searchHost;
        private SectionCardPanel _borrowGroupBox, _returnGroupBox;
        private Label _headerTitleLabel, _headerSubLabel;
        private Yakult.Inventory.App.Wpf.BorrowItems.WpfBorrowHostControl _wpfRight;
        private System.Windows.Forms.Timer _timer;
        private Panel _tabsHost, _tabButtonBar, _tabContentHost;
        private Panel _boardHost, _openHost, _historyHost;
        private Panel _kpiOldestCard;
        private Yakult.Inventory.App.Wpf.BorrowItems.WpfBorrowBoardHostControl _boardHostControl;
        private Yakult.Inventory.App.Wpf.BorrowItems.WpfBorrowTransactionHostControl _transactionHost;

        private const int DefaultPageSize = 10;
        private int _pageSize = DefaultPageSize;
        private int _openPageIndex;
        private int _historyPageIndex;
        private int _openTotalCount;
        private int _historyTotalCount;
        private DateTime? _openOldestBorrowedAtUtc;
        private DateTime? _lastRefreshLocal;
        private string _lastSearch = string.Empty;
        private BorrowDisplayMode _displayMode = BorrowDisplayMode.Open;

        public BorrowItemsDashboard()
        {
            BuildUi();
            Load += async (_, __) => await InitAsync();
            BorrowUiTheme.ThemeChanged += HandleThemeChanged;
            FormClosed += (_, __) =>
            {
                if (_timer != null) _timer.Stop();
                BorrowUiTheme.ThemeChanged -= HandleThemeChanged;
            };
        }

        private void BuildUi()
        {
            Font = new Font("Segoe UI", 9.5F);
            Text = "Borrow Items";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1200, 760);
            BackColor = Color.FromArgb(242, 245, 249);
            KeyPreview = true;
            KeyDown += async (_, e) =>
            {
                if (e.KeyCode == Keys.F5)
                {
                    e.SuppressKeyPress = true;
                    await ReloadAsync();
                }

                if (e.Control && e.KeyCode == Keys.L && txtSearch != null)
                {
                    e.SuppressKeyPress = true;
                    txtSearch.Focus();
                    txtSearch.SelectAll();
                }
            };

            _headerPanel = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Color.FromArgb(40, 60, 86), Padding = new Padding(16, 8, 16, 8) };
            var headerGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 650F));
            var titleWrap = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            titleWrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            titleWrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _headerTitleLabel = new Label { Text = "Borrow Items", ForeColor = Color.White, Font = new Font("Segoe UI", 22, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 2) };
            _headerSubLabel = new Label { Text = "Quick logbook for borrowing and returning serialized items", ForeColor = Color.FromArgb(220, 230, 240), AutoSize = true };
            titleWrap.Controls.Add(_headerTitleLabel, 0, 0);
            titleWrap.Controls.Add(_headerSubLabel, 0, 1);

            Control headerLeft = titleWrap;
            var logo = TryLoadYakultNameLogo();
            if (logo != null)
            {
                var brandWrap = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
                brandWrap.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                brandWrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                var picBrand = new PictureBox
                {
                    Image = logo,
                    Size = new Size(220, 44),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0, 6, 14, 0)
                };
                brandWrap.Controls.Add(picBrand, 0, 0);
                brandWrap.Controls.Add(titleWrap, 1, 0);
                headerLeft = brandWrap;
            }

            var rightWrap = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = new Padding(0) };
            rightWrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightWrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // --- BEGIN: Borrow Items "See Guide" button ---------------------------------------
            // Opens Docs\BorrowItemsGuide.html (the enhancement guide) in the default browser.
            // Once the guide is no longer needed, comment out this whole block (and the
            // matching block further down marked "See Guide button") to remove it and get
            // the search bar back to its original width.
            const bool ShowSeeGuideButton = true;
            // --- END: Borrow Items "See Guide" button -------------------------------------------

            var searchGrid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 5, Margin = new Padding(0, 2, 0, 0), AutoSize = true };
            searchGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F)); // See Guide column
            searchGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            searchGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126F));
            searchGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108F));
            searchGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108F));
            searchGrid.RowCount = 1;
            searchGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));

            _searchHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, 0, 10, 0), Padding = new Padding(10, 6, 6, 6), BorderStyle = BorderStyle.FixedSingle };
            var searchInner = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
            searchInner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            searchInner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30F));

            txtSearch = new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Margin = new Padding(0), Font = new Font("Segoe UI", 10.5F) };
            txtSearch.HandleCreated += (_, __) => SetCueBanner(txtSearch, "Search serial / name / model...");
            txtSearch.KeyDown += async (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await ReloadAsync(); }
                if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; await ClearSearchAndReloadAsync(); }
            };
            txtSearch.TextChanged += (_, __) =>
            {
                if (btnClearSearch != null) btnClearSearch.Visible = (txtSearch.Text ?? string.Empty).Trim().Length > 0;
            };

            btnClearSearch = new Button
            {
                Text = "X",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(120, 130, 140),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(0),
                TabStop = false,
                Visible = false,
                Cursor = Cursors.Hand
            };
            btnClearSearch.FlatAppearance.BorderSize = 0;
            btnClearSearch.Click += async (_, __) => await ClearSearchAndReloadAsync();

            searchInner.Controls.Add(txtSearch, 0, 0);
            searchInner.Controls.Add(btnClearSearch, 1, 0);
            _searchHost.Controls.Add(searchInner);

            btnThemeToggle = NewBtn("Dark Mode", 126, Color.FromArgb(120, 90, 180));
            btnThemeToggle.Dock = DockStyle.Fill;
            btnThemeToggle.Margin = new Padding(0, 0, 10, 0);
            btnThemeToggle.Click += (_, __) => BorrowUiTheme.Toggle();

            btnSearch = NewBtn("Search", 108, Color.FromArgb(60, 120, 190));
            btnSearch.Dock = DockStyle.Fill;
            btnSearch.Margin = new Padding(0, 0, 10, 0);
            btnSearch.Height = 32;
            btnSearch.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnSearch.Click += async (_, __) => await ReloadAsync();

            btnRefresh = NewBtn("Refresh", 108, Color.FromArgb(70, 92, 118));
            btnRefresh.Dock = DockStyle.Fill;
            btnRefresh.Height = 32;
            btnRefresh.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnRefresh.Click += async (_, __) => await ReloadAsync();

            // --- BEGIN: Borrow Items "See Guide" button (button) -------------------------------
            // Comment out this "if" block to remove the button next to the search bar.
            if (ShowSeeGuideButton)
            {
                btnHelp = new Button
                {
                    Text = "See Guide",
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0, 0, 8, 0),
                    Height = 32,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(220, 230, 245),
                    ForeColor = Color.FromArgb(40, 60, 86),
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnHelp.FlatAppearance.BorderSize = 0;
                var helpTip = new ToolTip();
                helpTip.SetToolTip(btnHelp, "Open the Borrow Items enhancement guide in your browser");
                btnHelp.Click += (_, __) => OpenBorrowItemsGuide();
                searchGrid.Controls.Add(btnHelp, 0, 0);
            }
            // --- END: Borrow Items "See Guide" button (button) ----------------------------------

            searchGrid.Controls.Add(_searchHost, 1, 0);
            searchGrid.Controls.Add(btnThemeToggle, 2, 0);
            searchGrid.Controls.Add(btnSearch, 3, 0);
            searchGrid.Controls.Add(btnRefresh, 4, 0);

            var meta = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Padding(0, 2, 0, 0)
            };
            lblLastRefresh = new Label { AutoSize = true, ForeColor = Color.FromArgb(210, 220, 235), Text = "Last refreshed: --", Margin = new Padding(0, 2, 0, 0) };
            lblLoading = new Label { AutoSize = true, ForeColor = Color.FromArgb(210, 220, 235), Text = "Loading...", Margin = new Padding(0, 2, 8, 0), Visible = false };
            pbLoading = new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Width = 64, Height = 10, Margin = new Padding(0, 4, 8, 0), Visible = false };
            meta.Controls.Add(lblLastRefresh);
            meta.Controls.Add(lblLoading);
            meta.Controls.Add(pbLoading);

            rightWrap.Controls.Add(searchGrid, 0, 0);
            rightWrap.Controls.Add(meta, 0, 1);

            headerGrid.Controls.Add(headerLeft, 0, 0);
            headerGrid.Controls.Add(rightWrap, 1, 0);
            _headerPanel.Controls.Add(headerGrid);

            _contentPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            var contentGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            // Give the left (Borrow/Return) column more room so radio labels and combos don't get cut off.
            contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520F));
            contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _contentPanel.Controls.Add(contentGrid);

            var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            left.Padding = new Padding(0, 0, 8, 0);

            _wpfRight = new Yakult.Inventory.App.Wpf.BorrowItems.WpfBorrowHostControl();
            _wpfRight.ReturnSelectedClicked += async (s, e) => await ReturnSelectedAsync();
            _wpfRight.DeleteBorrowClicked += async (s, e) => await DeleteSelectedBorrowAsync();
            _wpfRight.ExportCsvClicked += (s, e) => ExportCsv();
            _wpfRight.PrevPageClicked += async (s, e) => await PrevPageAsync();
            _wpfRight.NextPageClicked += async (s, e) => await NextPageAsync();
            _wpfRight.RefreshClicked += async (s, e) => await ReloadAsync();
            _wpfRight.BackToPortalClicked += (s, e) => Close();
            _wpfRight.BorrowRowDoubleClicked += (s, e) =>
            {
                if (e != null) OpenBorrowDetails(e);
            };
            _wpfRight.BorrowRowSelected += (s, e) =>
            {
                _resolvedOpen = e;
                if (e != null)
                {
                    txtReturnSerial.Text = e.SerialNumber;
                    if (lblReturnInfo != null)
                        lblReturnInfo.Text = $"Borrowed by {e.BorrowedByEmpName} ({e.BorrowedByDeptName}) @ {e.BorrowedAtLocal}";
                    if (cboReturnCompany != null && cboReturnDept != null && cboReturnEmp != null && e.BorrowedByEmpId > 0 && e.BorrowedByDeptId > 0)
                    {
                        var comId = FindCompanyIdByEmpId(e.BorrowedByEmpId.GetValueOrDefault());
                        SelectCompanyDeptEmp(cboReturnCompany, cboReturnDept, cboReturnEmp, comId, e.BorrowedByDeptId.GetValueOrDefault(), e.BorrowedByEmpId.GetValueOrDefault());
                    }
                    if (_transactionHost != null)
                    {
                        var comId = FindCompanyIdByEmpId(e.BorrowedByEmpId.GetValueOrDefault());
                        _transactionHost.ScanText = e.SerialNumber;
                        _transactionHost.SetReturnInfo(e);
                        _transactionHost.SetMode(Yakult.Inventory.App.Wpf.BorrowItems.TransactionMode.Return);
                        _transactionHost.SelectCompany(comId);
                        _transactionHost.SelectDepartment(e.BorrowedByDeptId.GetValueOrDefault());
                        _transactionHost.SelectBranch(FindBranchIdByEmpId(e.BorrowedByEmpId.GetValueOrDefault()));
                        _transactionHost.SelectEmployee(e.BorrowedByEmpId.GetValueOrDefault());
                    }
                }
                UpdateButtons();
            };

            _boardHostControl = new Yakult.Inventory.App.Wpf.BorrowItems.WpfBorrowBoardHostControl();
            _boardHostControl.ReturnBorrowClicked += async (s, row) =>
            {
                if (row != null)
                {
                    _resolvedOpen = row;
                    txtReturnSerial.Text = row.SerialNumber;
                    lblReturnInfo.Text = $"Borrowed by {row.BorrowedByEmpName} ({row.BorrowedByDeptName}) @ {row.BorrowedAtLocal}";
                    SelectCompanyDeptEmp(cboReturnCompany, cboReturnDept, cboReturnEmp, FindCompanyIdByEmpId(row.BorrowedByEmpId.GetValueOrDefault()), row.BorrowedByDeptId.GetValueOrDefault(), row.BorrowedByEmpId.GetValueOrDefault());
                    if (_transactionHost != null)
                    {
                        var comId = FindCompanyIdByEmpId(row.BorrowedByEmpId.GetValueOrDefault());
                        _transactionHost.ScanText = row.SerialNumber;
                        _transactionHost.SetReturnInfo(row);
                        _transactionHost.SetMode(Yakult.Inventory.App.Wpf.BorrowItems.TransactionMode.Return);
                        _transactionHost.SelectCompany(comId);
                        _transactionHost.SelectDepartment(row.BorrowedByDeptId.GetValueOrDefault());
                        _transactionHost.SelectBranch(FindBranchIdByEmpId(row.BorrowedByEmpId.GetValueOrDefault()));
                        _transactionHost.SelectEmployee(row.BorrowedByEmpId.GetValueOrDefault());
                    }
                    var wasDepartmentOnly = !(row.BorrowedByEmpId.HasValue && row.BorrowedByEmpId.Value > 0);
                    await ReturnAsync(departmentOnly: wasDepartmentOnly, departmentId: row.BorrowedByDeptId);
                }
            };
            _boardHostControl.BorrowRowDoubleClicked += (s, row) =>
            {
                if (row != null) OpenBorrowDetails(row);
            };
            _boardHost = new Panel { Dock = DockStyle.Fill };
            _boardHost.Controls.Add(_boardHostControl);

            _tabsHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };
            _tabButtonBar = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(0, 0, 0, 8) };
            _tabContentHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };

            btnDisplayBoard = NewTabButton("Board");
            btnDisplayOpen = NewTabButton("Open");
            btnDisplayHistory = NewTabButton("History");

            btnDisplayBoard.Width = 110;
            btnDisplayOpen.Width = 110;
            btnDisplayHistory.Width = 120;

            btnDisplayBoard.Click += (_, __) => SetDisplayMode(BorrowDisplayMode.Board);
            btnDisplayOpen.Click += (_, __) => SetDisplayMode(BorrowDisplayMode.Open);
            btnDisplayHistory.Click += (_, __) => SetDisplayMode(BorrowDisplayMode.History);

            _tabButtonBar.Controls.Add(btnDisplayHistory);
            _tabButtonBar.Controls.Add(btnDisplayOpen);
            _tabButtonBar.Controls.Add(btnDisplayBoard);

            _tabsHost.Controls.Add(_tabContentHost);
            _tabsHost.Controls.Add(_tabButtonBar);

            // Old left panel is kept as a hidden data store for backward-compat methods.
            // The visible transaction UI is the new WPF host below.
            _transactionHost = new Yakult.Inventory.App.Wpf.BorrowItems.WpfBorrowTransactionHostControl();
            _transactionHost.Dock = DockStyle.Fill;

            // ── Scan / Resolve ────────────────────────────────────────────────────
            _transactionHost.ScanSubmitted += async (s, serial) =>
            {
                txtBorrowSerial.Text = serial;
                await ResolveBorrowAsync();
                if (_resolvedItem != null)
                {
                    _transactionHost.SetResolvedItem(_resolvedItem);
                    _transactionHost.SetMode(Yakult.Inventory.App.Wpf.BorrowItems.TransactionMode.Borrow);
                }
            };
            _transactionHost.ResolveClicked += async (s, e) =>
            {
                var serial = _transactionHost.ScanText;
                if (string.IsNullOrWhiteSpace(serial)) return;

                // Try return first
                if (_schema)
                {
                    txtReturnSerial.Text = serial;
                    await ResolveReturnAsync();
                    if (_resolvedOpen != null)
                    {
                        _transactionHost.SetReturnInfo(_resolvedOpen);
                        _transactionHost.SetMode(Yakult.Inventory.App.Wpf.BorrowItems.TransactionMode.Return);
                        return;
                    }
                }

                // Then borrow
                txtBorrowSerial.Text = serial;
                await ResolveBorrowAsync();
                if (_resolvedItem != null)
                {
                    _transactionHost.SetResolvedItem(_resolvedItem);
                    _transactionHost.SetMode(Yakult.Inventory.App.Wpf.BorrowItems.TransactionMode.Borrow);
                }
            };
            _transactionHost.ModelSearchRequested += async (s, modelText) =>
            {
                try
                {
                    var matches = await _repo.SearchItemsByModelAsync(modelText);
                    _transactionHost.ShowModelSuggestions(matches);
                }
                catch
                {
                    // Non-fatal: the model number field is a find-it helper only.
                    _transactionHost.ShowModelSuggestions(null);
                }
            };
            _transactionHost.AddNewItemClicked += async (s, e) => await AddNewAndBorrowAsync();
            _transactionHost.BrowseItemsClicked += async (s, e) => await BrowseAndResolveBorrowAsync();

            // ── Borrow cascade ──────────────────────────────────────────────────
            _transactionHost.BorrowCompanyChanged += (s, comId) =>
            {
                for (var i = 0; i < cboBorrowCompany.Items.Count; i++)
                {
                    var c = cboBorrowCompany.Items[i] as CompanyOpt;
                    if (c != null && c.Id == comId) { cboBorrowCompany.SelectedIndex = i; break; }
                }
            };
            _transactionHost.BorrowDepartmentChanged += (s, deptId) =>
            {
                for (var i = 0; i < cboBorrowDept.Items.Count; i++)
                {
                    var d = cboBorrowDept.Items[i] as Dept;
                    if (d != null && d.Id == deptId) { cboBorrowDept.SelectedIndex = i; break; }
                }
            };

            // ── Return cascade ──────────────────────────────────────────────────
            _transactionHost.ReturnCompanyChanged += (s, comId) =>
            {
                for (var i = 0; i < cboReturnCompany.Items.Count; i++)
                {
                    var c = cboReturnCompany.Items[i] as CompanyOpt;
                    if (c != null && c.Id == comId) { cboReturnCompany.SelectedIndex = i; break; }
                }
            };
            _transactionHost.ReturnDepartmentChanged += (s, deptId) =>
            {
                for (var i = 0; i < cboReturnDept.Items.Count; i++)
                {
                    var d = cboReturnDept.Items[i] as Dept;
                    if (d != null && d.Id == deptId) { cboReturnDept.SelectedIndex = i; break; }
                }
            };

            // ── Actions ─────────────────────────────────────────────────────────
            _transactionHost.BorrowClicked += async (s, e) =>
            {
                if (e != null)
                {
                    txtBorrowSerial.Text = e.SerialNumber;
                    chkBorrowBackdate.Checked = e.IsBackdate;
                    if (e.IsBackdate && e.BackdateLocal.HasValue)
                    {
                        var local = e.BackdateLocal.Value;
                        dtpBorrowDate.Value = local.Date;
                        dtpBorrowTime.Value = DateTime.Today.Add(local.TimeOfDay);
                    }
                    UpdateBorrowBackdateVisibility();
                    SyncBorrowFromWpf(e);
                }
                var wpfEmp = e?.EmployeeId.HasValue == true ? _employees.FirstOrDefault(x => x.EmpId == e.EmployeeId.Value) : null;
                var borrowed = await BorrowAsync(wpfEmp, departmentOnly: e != null && e.BorrowForDepartmentOnly, departmentId: e?.DepartmentId);
                if (borrowed) _transactionHost.Clear();
            };
            _transactionHost.ReturnClicked += async (s, e) =>
            {
                if (e != null)
                {
                    txtReturnSerial.Text = e.SerialNumber;
                    SyncReturnFromWpf(e);
                }
                var wpfEmp = e?.EmployeeId.HasValue == true ? _employees.FirstOrDefault(x => x.EmpId == e.EmployeeId.Value) : null;
                await ReturnAsync(wpfEmp, departmentOnly: e != null && e.BorrowForDepartmentOnly, departmentId: e?.DepartmentId);
                _transactionHost.Clear();
            };
            _transactionHost.AddEmployeeClicked += async (s, forBorrow) => await AddEmployeeAndSelectAsync(forBorrow);
            _transactionHost.BackdateToggled += (s, e) =>
            {
                chkBorrowBackdate.Checked = _transactionHost.IsBackdateChecked;
                UpdateBorrowBackdateVisibility();
            };

            contentGrid.Controls.Add(_transactionHost, 0, 0);
            contentGrid.Controls.Add(_tabsHost, 1, 0);

            var borrowGrid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, RowCount = 12, Padding = new Padding(10, 8, 10, 10), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            borrowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            borrowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            borrowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            borrowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            borrowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            borrowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            borrowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            borrowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            borrowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            borrowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            borrowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            borrowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var borrowSerialGrid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
            borrowSerialGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            borrowSerialGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            borrowSerialGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            txtBorrowSerial = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0), Font = new Font("Segoe UI", 11) };
            txtBorrowSerial.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await ResolveBorrowAsync(); } };
            btnResolveBorrow = NewBtn("Resolve", 120, Color.FromArgb(70, 92, 118)); btnResolveBorrow.Dock = DockStyle.Fill; btnResolveBorrow.Click += async (_, __) => await ResolveBorrowAsync();
            btnAddNewItem = NewBtn("Add New", 110, Color.FromArgb(90, 96, 106)); btnAddNewItem.Dock = DockStyle.Fill; btnAddNewItem.Margin = new Padding(0); btnAddNewItem.Click += async (_, __) => await AddNewAndBorrowAsync();
            borrowSerialGrid.Controls.Add(txtBorrowSerial, 0, 0);
            borrowSerialGrid.Controls.Add(btnResolveBorrow, 1, 0);
            borrowSerialGrid.Controls.Add(btnAddNewItem, 2, 0);

            lblBorrowItem = new Label { Dock = DockStyle.Top, Text = "Item: --", Font = new Font("Segoe UI", 10.5F), AutoEllipsis = true, Margin = new Padding(0, 0, 0, 2) };
            lblBorrowDesc = new Label { Dock = DockStyle.Top, Text = "Description: --", Font = new Font("Segoe UI", 10.5F), AutoEllipsis = true, Margin = new Padding(0, 0, 0, 10) };

            cboBorrowCompany = NewCombo();
            cboBorrowCompany.SelectedIndexChanged += (_, __) =>
            {
                if (_suppressBorrowEmp) return;
                if (_borrowEmpLocked) UnlockBorrowEmpSearch();
                BindDepts(cboBorrowCompany, cboBorrowDept);
                BindEmps(cboBorrowDept, cboBorrowEmp);
            };
            cboBorrowDept = NewCombo();
            cboBorrowDept.SelectedIndexChanged += (_, __) =>
            {
                if (_suppressBorrowEmp) return;
                if (_borrowEmpLocked) UnlockBorrowEmpSearch();
                BindEmps(cboBorrowDept, cboBorrowEmp);
            };
            cboBorrowEmp = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDown,
                Font = new Font("Segoe UI", 10.5F),
                DisplayMember = "DisplayText",
                ValueMember = "EmpId",
                Margin = new Padding(0)
            };
            cboBorrowEmp.SelectedIndexChanged += (_, __) => UpdateButtons();
            cboBorrowEmp.TextChanged += OnBorrowEmpTextChanged;
            cboBorrowEmp.SelectionChangeCommitted += OnBorrowEmpSelected;

            var borrowEmpMode = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 4) };
            rbBorrowEmpListed = new RadioButton { Text = "Listed employee", AutoSize = true, Checked = true };
            rbBorrowEmpAddNew = new RadioButton { Text = "Not listed (Add employee)", AutoSize = true, Margin = new Padding(18, 0, 0, 0) };
            borrowEmpMode.Controls.Add(rbBorrowEmpListed);
            borrowEmpMode.Controls.Add(rbBorrowEmpAddNew);

            var borrowEmpHost = new Panel { Dock = DockStyle.Top, Height = 42, Margin = new Padding(0) };
            pnlBorrowEmpListed = new Panel { Dock = DockStyle.Fill };
            pnlBorrowEmpListed.Controls.Add(BorrowUiTheme.CreateComboHost(cboBorrowEmp));

            pnlBorrowEmpAddNew = new Panel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(0) };
            var borrowEmpAddGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
            borrowEmpAddGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
            borrowEmpAddGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            btnBorrowAddEmp = NewBtn("Add Employee...", 170, Color.FromArgb(90, 96, 106));
            btnBorrowAddEmp.Dock = DockStyle.Fill;
            btnBorrowAddEmp.Click += async (_, __) => await AddEmployeeAndSelectAsync(forBorrow: true);
            var lblBorrowEmpHint = new Label { Text = "Creates a new employee then returns here.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(90, 100, 110), Padding = new Padding(10, 0, 0, 0) };
            borrowEmpAddGrid.Controls.Add(btnBorrowAddEmp, 0, 0);
            borrowEmpAddGrid.Controls.Add(lblBorrowEmpHint, 1, 0);
            pnlBorrowEmpAddNew.Controls.Add(borrowEmpAddGrid);

            borrowEmpHost.Controls.Add(pnlBorrowEmpListed);
            borrowEmpHost.Controls.Add(pnlBorrowEmpAddNew);

            rbBorrowEmpListed.CheckedChanged += (_, __) =>
            {
                var listed = rbBorrowEmpListed.Checked;
                if (pnlBorrowEmpListed != null) pnlBorrowEmpListed.Visible = listed;
                if (pnlBorrowEmpAddNew != null) pnlBorrowEmpAddNew.Visible = !listed;
                UpdateButtons();
            };
            rbBorrowEmpAddNew.CheckedChanged += (_, __) =>
            {
                var listed = rbBorrowEmpListed.Checked;
                if (pnlBorrowEmpListed != null) pnlBorrowEmpListed.Visible = listed;
                if (pnlBorrowEmpAddNew != null) pnlBorrowEmpAddNew.Visible = !listed;
                UpdateButtons();
            };

            chkBorrowBackdate = new CheckBox
            {
                AutoSize = true,
                Text = "Backdate from logbook",
                Margin = new Padding(0, 2, 0, 0)
            };
            chkBorrowBackdate.CheckedChanged += (_, __) => UpdateBorrowBackdateVisibility();

            dtpBorrowDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Width = 150,
                Margin = new Padding(0)
            };
            dtpBorrowTime = new DateTimePicker
            {
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Width = 120,
                Margin = new Padding(0)
            };

            var borrowBackdateGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 5,
                Margin = new Padding(0)
            };
            borrowBackdateGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            borrowBackdateGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46F));
            borrowBackdateGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116F));
            borrowBackdateGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46F));
            borrowBackdateGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            lblBorrowBackdateDate = new Label
            {
                Text = "📅",
                AutoSize = false,
                Width = 46,
                Height = 24,
                Margin = new Padding(8, 4, 4, 0),
                Font = new Font("Segoe UI Emoji", 10F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter
            };
            lblBorrowBackdateTime = new Label
            {
                Text = "🕒",
                AutoSize = false,
                Width = 46,
                Height = 24,
                Margin = new Padding(8, 4, 4, 0),
                Font = new Font("Segoe UI Emoji", 10F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter
            };
            borrowBackdateGrid.Controls.Add(chkBorrowBackdate, 0, 0);
            borrowBackdateGrid.Controls.Add(lblBorrowBackdateDate, 1, 0);
            borrowBackdateGrid.Controls.Add(dtpBorrowDate, 2, 0);
            borrowBackdateGrid.Controls.Add(lblBorrowBackdateTime, 3, 0);
            borrowBackdateGrid.Controls.Add(dtpBorrowTime, 4, 0);

            pnlBorrowBackdate = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Visible = true,
                Margin = new Padding(0, 10, 0, 0)
            };
            pnlBorrowBackdate.Controls.Add(borrowBackdateGrid);
            ResetBorrowBackdateControls();
            UpdateBorrowBackdateVisibility();

            btnBorrow = NewBtn("Borrow", 356, Color.FromArgb(45, 137, 196)); btnBorrow.Dock = DockStyle.Fill; btnBorrow.Click += async (_, __) => await BorrowAsync();
            var borrowFooter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 10), Margin = new Padding(0) };
            borrowFooter.Controls.Add(btnBorrow);

            var borrowScrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(0) };
            borrowScrollHost.Controls.Add(borrowGrid);

            var borrowSection = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
            borrowSection.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            borrowSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            borrowSection.Controls.Add(borrowScrollHost, 0, 0);
            borrowSection.Controls.Add(borrowFooter, 0, 1);

            borrowGrid.Controls.Add(new Label { Text = "Serial Number (scan/type)", AutoSize = true, Margin = new Padding(0, 0, 0, 6), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 0);
            borrowGrid.Controls.Add(borrowSerialGrid, 0, 1);
            borrowGrid.Controls.Add(lblBorrowItem, 0, 2);
            borrowGrid.Controls.Add(lblBorrowDesc, 0, 3);
            borrowGrid.Controls.Add(new Label { Text = "Company", AutoSize = true, Margin = new Padding(0, 0, 0, 6), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 4);
            borrowGrid.Controls.Add(BorrowUiTheme.CreateComboHost(cboBorrowCompany), 0, 5);
            borrowGrid.Controls.Add(new Label { Text = "Department", AutoSize = true, Margin = new Padding(0, 10, 0, 6), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 6);
            borrowGrid.Controls.Add(BorrowUiTheme.CreateComboHost(cboBorrowDept), 0, 7);
            borrowGrid.Controls.Add(new Label { Text = "Borrowed By", AutoSize = true, Margin = new Padding(0, 10, 0, 6), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 8);
            borrowGrid.Controls.Add(borrowEmpMode, 0, 9);
            borrowGrid.Controls.Add(borrowEmpHost, 0, 10);
            borrowGrid.Controls.Add(pnlBorrowBackdate, 0, 11);
            _borrowGroupBox = BuildSectionCard("Borrow Item", Color.FromArgb(53, 96, 201), borrowSection);

            var returnGrid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, RowCount = 10, Padding = new Padding(10, 8, 10, 10), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            returnGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            returnGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            returnGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            returnGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            returnGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            returnGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            returnGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            returnGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            returnGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            returnGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var returnSerialGrid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
            returnSerialGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            returnSerialGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128F));
            txtReturnSerial = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0), Font = new Font("Segoe UI", 11) };
            txtReturnSerial.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await ResolveReturnAsync(); } };
            btnFindOpen = NewBtn("Find Open", 120, Color.FromArgb(70, 92, 118)); btnFindOpen.Dock = DockStyle.Fill; btnFindOpen.Click += async (_, __) => await ResolveReturnAsync();
            returnSerialGrid.Controls.Add(txtReturnSerial, 0, 0);
            returnSerialGrid.Controls.Add(btnFindOpen, 1, 0);

            lblReturnInfo = new Label { Dock = DockStyle.Fill, Text = "Borrowed by --", Font = new Font("Segoe UI", 10.5F), AutoEllipsis = true, Margin = new Padding(0, 0, 0, 8) };
            cboReturnCompany = NewCombo(); cboReturnCompany.SelectedIndexChanged += (_, __) => { if (_suppressBorrowEmp) return; BindDepts(cboReturnCompany, cboReturnDept); BindEmps(cboReturnDept, cboReturnEmp); };
            cboReturnDept = NewCombo(); cboReturnDept.SelectedIndexChanged += (_, __) => { if (_suppressBorrowEmp) return; BindEmps(cboReturnDept, cboReturnEmp); };
            cboReturnEmp = NewCombo(); cboReturnEmp.SelectedIndexChanged += (_, __) => UpdateButtons();

            var returnEmpMode = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 4) };
            rbReturnEmpListed = new RadioButton { Text = "Listed employee", AutoSize = true, Checked = true };
            rbReturnEmpAddNew = new RadioButton { Text = "Not listed (Add employee)", AutoSize = true, Margin = new Padding(18, 0, 0, 0) };
            returnEmpMode.Controls.Add(rbReturnEmpListed);
            returnEmpMode.Controls.Add(rbReturnEmpAddNew);

            var returnEmpHost = new Panel { Dock = DockStyle.Top, Height = 42, Margin = new Padding(0) };
            pnlReturnEmpListed = new Panel { Dock = DockStyle.Fill };
            pnlReturnEmpListed.Controls.Add(BorrowUiTheme.CreateComboHost(cboReturnEmp));

            pnlReturnEmpAddNew = new Panel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(0) };
            var returnEmpAddGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
            returnEmpAddGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
            returnEmpAddGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            btnReturnAddEmp = NewBtn("Add Employee...", 170, Color.FromArgb(90, 96, 106));
            btnReturnAddEmp.Dock = DockStyle.Fill;
            btnReturnAddEmp.Click += async (_, __) => await AddEmployeeAndSelectAsync(forBorrow: false);
            var lblReturnEmpHint = new Label { Text = "Creates a new employee then returns here.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(90, 100, 110), Padding = new Padding(10, 0, 0, 0) };
            returnEmpAddGrid.Controls.Add(btnReturnAddEmp, 0, 0);
            returnEmpAddGrid.Controls.Add(lblReturnEmpHint, 1, 0);
            pnlReturnEmpAddNew.Controls.Add(returnEmpAddGrid);

            returnEmpHost.Controls.Add(pnlReturnEmpListed);
            returnEmpHost.Controls.Add(pnlReturnEmpAddNew);

            rbReturnEmpListed.CheckedChanged += (_, __) =>
            {
                var listed = rbReturnEmpListed.Checked;
                if (pnlReturnEmpListed != null) pnlReturnEmpListed.Visible = listed;
                if (pnlReturnEmpAddNew != null) pnlReturnEmpAddNew.Visible = !listed;
                UpdateButtons();
            };
            rbReturnEmpAddNew.CheckedChanged += (_, __) =>
            {
                var listed = rbReturnEmpListed.Checked;
                if (pnlReturnEmpListed != null) pnlReturnEmpListed.Visible = listed;
                if (pnlReturnEmpAddNew != null) pnlReturnEmpAddNew.Visible = !listed;
                UpdateButtons();
            };
            btnReturn = NewBtn("Return", 356, Color.FromArgb(20, 150, 125)); btnReturn.Dock = DockStyle.Fill; btnReturn.Click += async (_, __) => await ReturnAsync();
            var returnFooter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 10), Margin = new Padding(0) };
            returnFooter.Controls.Add(btnReturn);

            var returnScrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(0) };
            returnScrollHost.Controls.Add(returnGrid);

            var returnSection = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
            returnSection.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            returnSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            returnSection.Controls.Add(returnScrollHost, 0, 0);
            returnSection.Controls.Add(returnFooter, 0, 1);

            returnGrid.Controls.Add(new Label { Text = "Serial Number (scan/type)", AutoSize = true, Margin = new Padding(0, 0, 0, 6), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 0);
            returnGrid.Controls.Add(returnSerialGrid, 0, 1);
            returnGrid.Controls.Add(lblReturnInfo, 0, 2);
            returnGrid.Controls.Add(new Label { Text = "Company", AutoSize = true, Margin = new Padding(0, 0, 0, 6), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 3);
            returnGrid.Controls.Add(BorrowUiTheme.CreateComboHost(cboReturnCompany), 0, 4);
            returnGrid.Controls.Add(new Label { Text = "Department", AutoSize = true, Margin = new Padding(0, 10, 0, 6), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 5);
            returnGrid.Controls.Add(BorrowUiTheme.CreateComboHost(cboReturnDept), 0, 6);
            returnGrid.Controls.Add(new Label { Text = "Returned By", AutoSize = true, Margin = new Padding(0, 10, 0, 6), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 7);
            returnGrid.Controls.Add(returnEmpMode, 0, 8);
            returnGrid.Controls.Add(returnEmpHost, 0, 9);
            _returnGroupBox = BuildSectionCard("Return Item", Color.FromArgb(20, 138, 110), returnSection);

            left.Controls.Add(_borrowGroupBox, 0, 0);
            left.Controls.Add(_returnGroupBox, 0, 1);

            Controls.Add(_contentPanel);
            Controls.Add(_headerPanel);
            RefreshKpi();
            ApplyTheme();
            SetDisplayMode(BorrowDisplayMode.Open);


        }

        private void HandleThemeChanged(object sender, EventArgs e)
        {
            if (!IsDisposed)
                ApplyTheme();
        }

        private void ApplyTheme()
        {
            var palette = BorrowUiTheme.Current;

            SuspendLayout();

            BorrowUiTheme.ApplyForm(this);
            if (_headerPanel != null)
                _headerPanel.BackColor = palette.HeaderBack;
            if (_contentPanel != null)
                _contentPanel.BackColor = palette.PageBack;
            if (_headerTitleLabel != null)
                _headerTitleLabel.ForeColor = palette.HeaderText;
            if (_headerSubLabel != null)
                _headerSubLabel.ForeColor = palette.HeaderSubText;

            if (_searchHost != null)
            {
                _searchHost.BackColor = palette.InputBack;
                _searchHost.BorderStyle = BorrowUiTheme.IsDarkMode ? BorderStyle.None : BorderStyle.FixedSingle;
            }

            ApplyDashboardSurface(_contentPanel, false);

            BorrowUiTheme.ApplyTextBox(txtSearch, true);
            BorrowUiTheme.ApplyTextBox(txtBorrowSerial, false);
            BorrowUiTheme.ApplyTextBox(txtReturnSerial, false);

            BorrowUiTheme.ApplyComboBox(cboBorrowCompany);
            BorrowUiTheme.ApplyComboBox(cboBorrowDept);
            BorrowUiTheme.ApplyComboBox(cboBorrowEmp);
            BorrowUiTheme.ApplyComboBox(cboReturnCompany);
            BorrowUiTheme.ApplyComboBox(cboReturnDept);
            BorrowUiTheme.ApplyComboBox(cboReturnEmp);

            ApplySectionCardTheme(_borrowGroupBox);
            ApplySectionCardTheme(_returnGroupBox);
            if (_tabsHost != null)
                _tabsHost.BackColor = palette.SurfaceBack;
            if (_tabButtonBar != null)
                _tabButtonBar.BackColor = palette.SurfaceBack;
            if (_tabContentHost != null)
            {
                _tabContentHost.BackColor = BorrowUiTheme.IsDarkMode ? palette.SurfaceBack : palette.Border;
                _tabContentHost.Padding = BorrowUiTheme.IsDarkMode ? new Padding(0) : new Padding(1);
            }
            BorrowUiTheme.ApplySurface(_openHost);
            BorrowUiTheme.ApplySurface(_historyHost);
            ApplyDashboardButtonThemes();
            ApplyKpiTheme(_kpiOldestCard, palette.KpiOldest);

            if (btnClearSearch != null)
            {
                btnClearSearch.BackColor = palette.InputBack;
                btnClearSearch.ForeColor = palette.TextSecondary;
            }

            BorrowUiTheme.ApplyLabel(lblBorrowItem, false);
            BorrowUiTheme.ApplyLabel(lblBorrowDesc, false);
            BorrowUiTheme.ApplyLabel(lblReturnInfo, false);
            BorrowUiTheme.ApplyLabel(lblStatus, true);
            BorrowUiTheme.ApplyLabel(lblLastRefresh, true);
            BorrowUiTheme.ApplyLabel(lblLoading, true);

            ApplyBorrowBackdateTheme();

            if (lblEmptyOpen != null)
            {
                lblEmptyOpen.ForeColor = palette.EmptyFore;
                lblEmptyOpen.BackColor = palette.SurfaceBack;
            }

            if (lblEmptyHistory != null)
            {
                lblEmptyHistory.ForeColor = palette.EmptyFore;
                lblEmptyHistory.BackColor = palette.SurfaceBack;
            }

            if (pbLoading != null)
                pbLoading.BackColor = palette.HeaderBack;

            ResumeLayout(true);
            Invalidate(true);
            RefreshKpi();
        }

        private void ApplyDashboardButtonThemes()
        {
            BorrowUiTheme.ApplyToggleButton(btnThemeToggle);
            if (btnThemeToggle != null)
                btnThemeToggle.Text = BorrowUiTheme.IsDarkMode ? "Light" : "Dark";

            BorrowUiTheme.ApplyButton(btnSearch, BorrowButtonKind.Primary);
            BorrowUiTheme.ApplyButton(btnRefresh, BorrowButtonKind.Secondary);
            BorrowUiTheme.ApplyButton(btnResolveBorrow, BorrowButtonKind.Secondary);
            BorrowUiTheme.ApplyButton(btnFindOpen, BorrowButtonKind.Secondary);
            BorrowUiTheme.ApplyButton(btnAddNewItem, BorrowButtonKind.Secondary);
            BorrowUiTheme.ApplyButton(btnBorrowAddEmp, BorrowButtonKind.Secondary);
            BorrowUiTheme.ApplyButton(btnReturnAddEmp, BorrowButtonKind.Secondary);
            BorrowUiTheme.ApplyButton(btnBorrow, BorrowButtonKind.Primary);
            BorrowUiTheme.ApplyButton(btnReturn, BorrowButtonKind.Success);
            BorrowUiTheme.ApplyButton(btnExport, BorrowButtonKind.Success);
            BorrowUiTheme.ApplyButton(btnReload, BorrowButtonKind.Primary);
            BorrowUiTheme.ApplyButton(btnBackToPortal, BorrowButtonKind.Info);
            ApplyDisplayModeButtonTheme(btnDisplayBoard, _displayMode == BorrowDisplayMode.Board);
            ApplyDisplayModeButtonTheme(btnDisplayOpen, _displayMode == BorrowDisplayMode.Open);
            ApplyDisplayModeButtonTheme(btnDisplayHistory, _displayMode == BorrowDisplayMode.History);
        }

        private void ApplyDisplayModeButtonTheme(Button button, bool selected)
        {
            if (button == null)
                return;

            BorrowUiTheme.ApplyButton(button, selected ? BorrowButtonKind.Primary : BorrowButtonKind.Secondary);
            button.Font = new Font("Segoe UI", 10F, selected ? FontStyle.Bold : FontStyle.Regular);
        }

        private void ApplyBorrowBackdateTheme()
        {
            var palette = BorrowUiTheme.Current;

            if (chkBorrowBackdate != null)
            {
                chkBorrowBackdate.BackColor = palette.SurfaceBack;
                chkBorrowBackdate.ForeColor = palette.TextPrimary;
            }

            if (pnlBorrowBackdate != null)
                pnlBorrowBackdate.BackColor = palette.SurfaceBack;

            if (dtpBorrowDate != null)
            {
                dtpBorrowDate.CalendarMonthBackground = palette.InputBack;
                dtpBorrowDate.CalendarForeColor = palette.InputFore;
                dtpBorrowDate.CalendarTitleBackColor = palette.HeaderBack;
                dtpBorrowDate.CalendarTitleForeColor = palette.HeaderText;
            }

            if (dtpBorrowTime != null)
            {
                dtpBorrowTime.CalendarMonthBackground = palette.InputBack;
                dtpBorrowTime.CalendarForeColor = palette.InputFore;
                dtpBorrowTime.CalendarTitleBackColor = palette.HeaderBack;
                dtpBorrowTime.CalendarTitleForeColor = palette.HeaderText;
            }
        }

        private void ApplyKpiTheme(Panel panel, Color backColor)
        {
            if (panel == null)
                return;

            panel.BackColor = backColor;
            foreach (Control child in panel.Controls)
            {
                var label = child as Label;
                if (label != null)
                {
                    label.BackColor = backColor;
                    label.ForeColor = Color.White;
                }
            }
        }

        private void ApplySectionCardTheme(SectionCardPanel card)
        {
            if (card == null)
                return;

            var palette = BorrowUiTheme.Current;
            card.BackColor = palette.SurfaceBack;
            card.BorderColor = palette.Border;
            card.TitleForeColor = palette.TextPrimary;
            card.TitleBackColor = palette.SurfaceBack;
            var shell = card.Controls.OfType<TableLayoutPanel>().FirstOrDefault();
            if (shell != null)
            {
                shell.BackColor = palette.SurfaceBack;
                var titleLabel = shell.Controls.OfType<Label>().FirstOrDefault();
                if (titleLabel != null)
                {
                    titleLabel.ForeColor = palette.TextPrimary;
                    titleLabel.BackColor = palette.SurfaceBack;
                }
            }
        }

        private void SetTabContentHost(Control activeHost, params Control[] inactiveHosts)
        {
            if (_tabContentHost == null)
                return;

            if (inactiveHosts != null)
            {
                foreach (var inactiveHost in inactiveHosts)
                {
                    if (inactiveHost != null && inactiveHost != activeHost && inactiveHost.Parent == _tabContentHost)
                        _tabContentHost.Controls.Remove(inactiveHost);
                }
            }

            if (activeHost == null)
                return;

            if (activeHost.Parent != null && activeHost.Parent != _tabContentHost)
                activeHost.Parent.Controls.Remove(activeHost);

            if (!_tabContentHost.Controls.Contains(activeHost))
                _tabContentHost.Controls.Add(activeHost);

            activeHost.Dock = DockStyle.Fill;
            activeHost.BringToFront();
        }

        private void SetDisplayMode(BorrowDisplayMode mode)
        {
            _displayMode = mode;

            if (mode == BorrowDisplayMode.Board)
            {
                _boardHostControl?.BindData(_open);
                SetTabContentHost(_boardHost, _wpfRight);
            }
            else
            {
                if (_wpfRight != null)
                    _wpfRight.SelectedTabIndex = mode == BorrowDisplayMode.History ? 1 : 0;

                SetTabContentHost(_wpfRight, _boardHost);
            }

            ApplyDashboardButtonThemes();
        }

        private void ApplyDashboardSurface(Control root, bool surface)
        {
            if (root == null)
                return;

            var palette = BorrowUiTheme.Current;

            foreach (Control child in root.Controls)
            {
                if (child == null)
                    continue;

                if (child is SectionCardPanel)
                {
                    ApplySectionCardTheme((SectionCardPanel)child);
                    ApplyDashboardSurface(child, true);
                    continue;
                }

                if (child is TextBox)
                {
                    BorrowUiTheme.ApplyTextBox((TextBox)child, ((TextBox)child).BorderStyle == BorderStyle.None);
                    continue;
                }

                if (BorrowUiTheme.IsComboHost(child))
                {
                    BorrowUiTheme.ApplyComboHost((Panel)child);
                    continue;
                }

                if (child is ComboBox)
                {
                    BorrowUiTheme.ApplyComboBox((ComboBox)child);
                    continue;
                }

                if (child is DataGridView)
                {
                    BorrowUiTheme.ApplyGrid((DataGridView)child);
                    continue;
                }

                if (child is TabControl)
                {
                    BorrowUiTheme.ApplyTabControl((TabControl)child);
                    ApplyDashboardSurface(child, true);
                    continue;
                }

                if (child is TabPage)
                {
                    BorrowUiTheme.ApplySurface(child);
                    ApplyDashboardSurface(child, true);
                    continue;
                }

                if (child is Button)
                    continue;

                if (child is RadioButton)
                {
                    child.BackColor = surface ? palette.SurfaceBack : palette.PageBack;
                    child.ForeColor = palette.TextPrimary;
                    continue;
                }

                if (child is Label)
                {
                    var label = (Label)child;
                    var secondary = !label.Font.Bold && label.Font.Size <= 10F;
                    BorrowUiTheme.ApplyLabel(label, secondary);
                    continue;
                }

                if (child is Panel || child is TableLayoutPanel || child is FlowLayoutPanel)
                {
                    child.BackColor = surface ? palette.SurfaceBack : palette.PageBack;
                    child.ForeColor = palette.TextPrimary;
                    ApplyDashboardSurface(child, surface);
                    continue;
                }

                child.BackColor = surface ? palette.SurfaceBack : palette.PageBack;
                child.ForeColor = palette.TextPrimary;
                ApplyDashboardSurface(child, surface);
            }
        }

        private async Task InitAsync()
        {
            try
            {
                var schemaState = await TryRefreshSchemaStateAsync(showErrors: true);
                if (!schemaState.HasValue)
                    return;

                _schema = schemaState.Value;
                if (!_schema)
                {
                    var dbHint = GetDbHint();
                    MessageBox.Show($"Borrow Items schema is missing in the connected database ({dbHint}).\r\n\r\nRun BorrowItems.Prod.Install.sql against that database.", "Borrow Items", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    lblStatus.Text = $"Schema missing in {dbHint}. Install BorrowItems.Prod.Install.sql.";
                }

                var all = await _repo.GetActiveEmployeesAsync(12000);
                _employees.Clear();
                _employees.AddRange((all ?? new List<BorrowEmployeeLookup>()).Where(e => e != null && e.EmpId > 0));
                _employeesDb.Clear();
                _employeesDb.AddRange(_employees);

                try
                {
                    var companies = await _repo.GetActiveCompaniesAsync(5000);
                    _companiesDb.Clear();
                    _companiesDb.AddRange((companies ?? new List<CompanyDto>()).Where(c => c != null && c.ComId > 0));
                }
                catch
                {
                    _companiesDb.Clear();
                }

                BindCompanies();
                _timer = new System.Windows.Forms.Timer { Interval = 1000 };
                _timer.Tick += (_, __) =>
                {
                    RefreshKpi();
                    _boardHostControl?.RefreshElapsed();
                };
                _timer.Start();
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                ShowBorrowError("Borrow Items", "We couldn't initialize the Borrow Items workspace right now. Please try again.", ex, "InitAsync");
            }
        }

        private async Task ResolveBorrowAsync()
        {
            if (_busy) return;
            var serial = (txtBorrowSerial.Text ?? string.Empty).Trim();
            if (serial.Length == 0) return;

            BorrowItemLookup item = null;
            BorrowLogRow dbOpen = null;
            var ok = await WithBusy(async () =>
            {
                item = await _repo.FindItemBySerialAsync(serial);
                if (_schema)
                    dbOpen = await _repo.GetOpenBorrowBySerialAsync(serial);
            });
            if (!ok) return;

            if (item == null)
            {
                _resolvedItem = null;
                lblBorrowItem.Text = "Item: --";
                lblBorrowDesc.Text = "Description: --";
                _transactionHost?.Clear();
                var res = MessageBox.Show("Item not found.\r\n\r\nDo you want to add it now (External/New) and borrow immediately?", "Borrow", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res == DialogResult.Yes)
                    await AddNewAndBorrowAsync();
                return;
            }
            if (_schema && dbOpen != null)
            {
                _resolvedItem = null;
                _transactionHost?.SetReturnInfo(dbOpen);
                _transactionHost?.SetMode(Yakult.Inventory.App.Wpf.BorrowItems.TransactionMode.Return);
                MessageBox.Show("Item is already borrowed.", "Borrow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _resolvedItem = item;
            lblBorrowItem.Text = "Item: " + item.DisplayText;
            lblBorrowDesc.Text = "Description: " + (string.IsNullOrWhiteSpace(item.ItemDescription) ? "--" : item.ItemDescription.Trim());
            _transactionHost?.SetResolvedItem(_resolvedItem);
            _transactionHost?.SetMode(Yakult.Inventory.App.Wpf.BorrowItems.TransactionMode.Borrow);
            if (!_schema)
                lblStatus.Text = $"Borrow logging is unavailable (schema missing in {GetDbHint()}). You can add items to inventory, but borrows/returns cannot be logged.";
            UpdateButtons();
        }

        /// <summary>
        /// Third way to pick an item to borrow, alongside scanning its QR code or typing its
        /// serial number: browse a list of items flagged IsBorrowable and pick one directly.
        /// Selecting an item just fills the serial textbox and runs the normal resolve pipeline
        /// — it doesn't bypass or duplicate the confirm/borrow flow.
        /// </summary>
        private async Task BrowseAndResolveBorrowAsync()
        {
            if (_busy) return;

            var picker = new Yakult.Inventory.App.Wpf.BorrowItems.WpfBorrowableItemsPickerDialog(_repo);
            new System.Windows.Interop.WindowInteropHelper(picker).Owner = this.Handle;

            if (picker.ShowDialog() != true || picker.SelectedItem == null)
                return;

            txtBorrowSerial.Text = picker.SelectedItem.SerialNumber;
            await ResolveBorrowAsync();
            if (_resolvedItem != null)
            {
                _transactionHost?.SetResolvedItem(_resolvedItem);
                _transactionHost?.SetMode(Yakult.Inventory.App.Wpf.BorrowItems.TransactionMode.Borrow);
            }
        }

        private async Task AddNewAndBorrowAsync()
        {
            if (_busy) return;

            try
            {
                var serial = (txtBorrowSerial.Text ?? string.Empty).Trim();

                var canBorrow = _schema;
                if (!canBorrow)
                {
                    var r = MessageBox.Show(
                        $"Borrow Items schema is missing in the connected database ({GetDbHint()}).\r\n\r\nDo you want to add this item to inventory only (no borrow log)?",
                        "Borrow",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    if (r != DialogResult.Yes)
                        return;
                }

                int? defaultEmpId = null;
                if (canBorrow && rbBorrowEmpListed != null && rbBorrowEmpListed.Checked)
                    defaultEmpId = (cboBorrowEmp.SelectedItem as BorrowEmployeeLookup)?.EmpId;

                using (var dlg = new Yakult.Inventory.App.Wpf.BorrowItems.WpfAddExternalAndBorrowDialog(_repo, serial, requireBorrower: canBorrow, defaultBorrowEmpId: defaultEmpId))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK || dlg.NewItem == null)
                    {
                        // Listed in inventory mode returns serial-only to borrow.
                        var listed = (dlg.ListedSerialToBorrow ?? string.Empty).Trim();
                        if (listed.Length == 0)
                            return;

                        var borrowEmpId = dlg.BorrowEmployeeId;
                        var borrowDeptId = dlg.BorrowedByDeptId;
                        var borrowDeptName = dlg.BorrowedByDeptName;
                        var hasBorrower = (borrowEmpId.HasValue && borrowEmpId.Value > 0)
                            || (borrowDeptId.HasValue && borrowDeptId.Value > 0 && !string.IsNullOrWhiteSpace(borrowDeptName));
                        if (!canBorrow || !hasBorrower)
                        {
                            MessageBox.Show("Borrow logging is unavailable (schema missing).", "Borrow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        var okListedBorrow = await WithBusy(async () =>
                        {
                            await _repo.BorrowAsync(listed, borrowEmpId, AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1, null, borrowDeptId, borrowDeptName);
                            ClearBorrow();
                            _openPageIndex = 0;
                            _historyPageIndex = 0;
                            await ReloadAsync(fromWithinBusy: true);
                        });
                        if (!okListedBorrow)
                            return;

                        txtReturnSerial.Text = listed;
                        if (_wpfRight != null) _wpfRight.SelectedTabIndex = 0;
                        SelectOpenRowBySerial(listed);
                        MessageBox.Show("Borrow logged.", "Borrow", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    var itemRepo = new ItemRepository();
                    var createdSerialOuter = string.Empty;
                    var borrowedOuter = false;
                    var promptBorrowLaterOuter = false;
                    var okAddAndMaybeBorrow = await WithBusy(async () =>
                    {
                        int? createdItemId = null;
                        var createdSerial = ((dlg.NewItem.SerialNumber ?? string.Empty).Trim().Length > 0
                            ? dlg.NewItem.SerialNumber
                            : serial).Trim();
                        createdSerialOuter = createdSerial;

                        try
                        {
                            // Step 1: Create the inventory item.
                            createdItemId = itemRepo.AddItem(dlg.NewItem);

                            // Step 2: Borrow it immediately (if schema installed).
                            var borrowEmpId = dlg.BorrowEmployeeId;
                            var borrowDeptId = dlg.BorrowedByDeptId;
                            var borrowDeptName = dlg.BorrowedByDeptName;
                            var hasBorrower = (borrowEmpId.HasValue && borrowEmpId.Value > 0)
                                || (borrowDeptId.HasValue && borrowDeptId.Value > 0 && !string.IsNullOrWhiteSpace(borrowDeptName));
                            if (canBorrow && hasBorrower && createdSerial.Length > 0)
                            {
                                await _repo.BorrowAsync(createdSerial, borrowEmpId, AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1, null, borrowDeptId, borrowDeptName);
                                borrowedOuter = true;
                            }

                            ClearBorrow();
                            _openPageIndex = 0;
                            _historyPageIndex = 0;
                            await ReloadAsync(fromWithinBusy: true);
                            if (canBorrow && hasBorrower && createdSerial.Length == 0)
                            {
                                promptBorrowLaterOuter = true;
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (createdItemId.HasValue && createdItemId.Value > 0 && !borrowedOuter)
                            {
                                try
                                {
                                    var rollback = await itemRepo.ForceDeleteItemWithRelations(createdItemId.Value);
                                    if (!rollback.Success)
                                        throw new InvalidOperationException(rollback.Message ?? "Rollback could not delete the newly created item.");

                                    throw new InvalidOperationException(
                                        ex.Message + "\r\n\r\nThe new item was rolled back automatically so Add New + Borrow stays consistent.",
                                        ex);
                                }
                                catch (Exception rollbackEx)
                                {
                                    throw new InvalidOperationException(
                                        ex.Message + "\r\n\r\nThe borrow step failed after the item was created, and automatic rollback also failed: " +
                                        rollbackEx.Message + "\r\nPlease review the newly created item manually.",
                                        ex);
                                }
                            }

                            throw;
                        }
                    });
                    if (!okAddAndMaybeBorrow)
                        return;

                    if (borrowedOuter && createdSerialOuter.Length > 0)
                    {
                        txtReturnSerial.Text = createdSerialOuter;
                        if (_wpfRight != null) _wpfRight.SelectedTabIndex = 0;
                        SelectOpenRowBySerial(createdSerialOuter);
                    }

                    if (promptBorrowLaterOuter)
                    {
                        MessageBox.Show("Item added to inventory.\r\n\r\nTo borrow it, enter its Serial Number then click Resolve/Borrow.", "Borrow", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    MessageBox.Show(canBorrow && borrowedOuter ? "Item added and borrow logged." : "Item added to inventory.", "Borrow", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                ShowBorrowError("Borrow Items", "We couldn't complete the Add New flow right now. Please try again.", ex, "AddNewAndBorrow");
            }
        }

        private async Task<bool> BorrowAsync(BorrowEmployeeLookup explicitEmp = null, bool departmentOnly = false, int? departmentId = null)
        {
            if (_busy) return false;
            if (_resolvedItem == null)
            {
                MessageBox.Show("Resolve a serial number first (scan, type it and press Enter, or use Select from List).", "Borrow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (rbBorrowEmpListed != null && !rbBorrowEmpListed.Checked) return false;

            BorrowEmployeeLookup emp = null;
            Dept dept = null;
            string borrowedByDisplay, companyDisplay, deptDisplay;

            if (departmentOnly)
            {
                dept = ResolveDeptById(departmentId.GetValueOrDefault());
                if (dept == null || dept.Id <= 0)
                {
                    MessageBox.Show("Select a department first.", "Borrow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                deptDisplay = dept.Name;
                companyDisplay = _companiesDb.FirstOrDefault(c => c != null && c.ComId == dept.CompanyId)?.CompanyName;
                borrowedByDisplay = $"{dept.Name} (no specific employee)";
            }
            else
            {
                emp = explicitEmp ?? cboBorrowEmp.SelectedItem as BorrowEmployeeLookup;
                if (emp == null)
                {
                    MessageBox.Show("Select an employee first.", "Borrow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                deptDisplay = emp.DepartmentName;
                companyDisplay = emp.CompanyName;
                borrowedByDisplay = emp.EmployeeName;
            }

            var borrowedSerial = (_resolvedItem.SerialNumber ?? string.Empty).Trim();
            DateTime? borrowedAtUtc;
            string borrowedAtDisplay;

            if (!TryGetSelectedBorrowedAt(out borrowedAtUtc, out borrowedAtDisplay))
                return false;

            using (var dlg = new Yakult.Inventory.App.Forms.BorrowItems.ConfirmBorrowDialog(
                new Yakult.Inventory.App.Forms.BorrowItems.ConfirmBorrowDialog.BorrowConfirmationModel
            {
                Item = _resolvedItem.DisplayText,
                SerialNumber = _resolvedItem.SerialNumber,
                BorrowedBy = borrowedByDisplay,
                Company = companyDisplay,
                Department = deptDisplay,
                EncodedBy = string.IsNullOrWhiteSpace(AppSession.CurrentUserName) ? $"UserId {AppSession.CurrentUserId}" : AppSession.CurrentUserName,
                TimestampLabel = "Borrowed At",
                Timestamp = borrowedAtDisplay
            }))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return false;
            }

            var ok = await WithBusy(async () =>
            {
                if (departmentOnly)
                    await _repo.BorrowAsync(_resolvedItem.SerialNumber, null, AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1, borrowedAtUtc, dept.Id, dept.Name);
                else
                    await _repo.BorrowAsync(_resolvedItem.SerialNumber, emp.EmpId, AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1, borrowedAtUtc);
                ClearBorrow();
                _openPageIndex = 0;
                _historyPageIndex = 0;
                await ReloadAsync(fromWithinBusy: true);
            });
            if (!ok) return false;

            if (borrowedSerial.Length > 0)
            {
                txtReturnSerial.Text = borrowedSerial;
                if (_wpfRight != null) _wpfRight.SelectedTabIndex = 0;
                SelectOpenRowBySerial(borrowedSerial);
            }

            MessageBox.Show("Borrow logged.", "Borrow", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        private async Task ResolveReturnAsync()
        {
            if (_busy) return;
            if (!_schema)
            {
                MessageBox.Show($"Borrow logging is unavailable (schema missing in {GetDbHint()}).\r\n\r\nInstall BorrowItems.Prod.Install.sql to enable Find Open / Return.", "Return", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var serial = (txtReturnSerial.Text ?? string.Empty).Trim();
            if (serial.Length == 0) return;
            BorrowLogRow dbRow = null;
            var ok = await WithBusy(async () => { dbRow = await _repo.GetOpenBorrowBySerialAsync(serial); });
            if (!ok) return;
            if (dbRow == null)
            {
                _resolvedOpen = null;
                lblReturnInfo.Text = "Borrowed by --";
                _transactionHost?.Clear();
                MessageBox.Show("No open borrow found.", "Return", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _resolvedOpen = dbRow;
            lblReturnInfo.Text = $"Borrowed by {dbRow.BorrowedByEmpName} ({dbRow.BorrowedByDeptName}) @ {dbRow.BorrowedAtLocal}";
            SelectCompanyDeptEmp(cboReturnCompany, cboReturnDept, cboReturnEmp, FindCompanyIdByEmpId(dbRow.BorrowedByEmpId.GetValueOrDefault()), dbRow.BorrowedByDeptId.GetValueOrDefault(), dbRow.BorrowedByEmpId.GetValueOrDefault());
            if (_transactionHost != null)
            {
                _transactionHost.SetReturnInfo(_resolvedOpen);
                _transactionHost.SetMode(Yakult.Inventory.App.Wpf.BorrowItems.TransactionMode.Return);
                _transactionHost.SelectCompany(FindCompanyIdByEmpId(dbRow.BorrowedByEmpId.GetValueOrDefault()));
                _transactionHost.SelectDepartment(dbRow.BorrowedByDeptId.GetValueOrDefault());
                _transactionHost.SelectBranch(FindBranchIdByEmpId(dbRow.BorrowedByEmpId.GetValueOrDefault()));
                _transactionHost.SelectEmployee(dbRow.BorrowedByEmpId.GetValueOrDefault());
            }
            UpdateButtons();
        }

        private async Task ReturnAsync(BorrowEmployeeLookup explicitEmp = null, bool departmentOnly = false, int? departmentId = null)
        {
            if (_busy) return;
            if (rbReturnEmpListed != null && !rbReturnEmpListed.Checked) return;

            BorrowEmployeeLookup emp = null;
            Dept dept = null;
            string returnedByDisplay, returnDeptDisplay;

            if (departmentOnly)
            {
                dept = ResolveDeptById(departmentId.GetValueOrDefault());
                if (dept == null && _resolvedOpen != null && _resolvedOpen.BorrowedByDeptId.GetValueOrDefault() > 0)
                {
                    // Board quick-return on a department-only borrow: no department combo was
                    // ever touched, so fall back to the same department that borrowed it.
                    dept = new Dept { Id = _resolvedOpen.BorrowedByDeptId.GetValueOrDefault(), Name = _resolvedOpen.BorrowedByDeptName };
                }
                if (dept == null || dept.Id <= 0)
                {
                    MessageBox.Show("Select a department first.", "Return", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                returnDeptDisplay = dept.Name;
                returnedByDisplay = $"{dept.Name} (no specific employee)";
            }
            else
            {
                emp = explicitEmp ?? cboReturnEmp.SelectedItem as BorrowEmployeeLookup;
                if (emp == null)
                {
                    // Quick-return actions (Board card, grid row) pre-select the Return combo from
                    // the original borrower's employee id. That pre-fill has nothing to match when
                    // the item was borrowed department-only (no BorrowedByEmpId) — surface that
                    // instead of silently doing nothing.
                    MessageBox.Show("Select who is returning this item on the Return panel first.", "Return", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                returnDeptDisplay = emp.DepartmentName;
                returnedByDisplay = emp.EmployeeName;
            }

            var serialText = (txtReturnSerial.Text ?? string.Empty).Trim();
            if (serialText.Length > 0 && (_resolvedOpen == null || !string.Equals((_resolvedOpen.SerialNumber ?? string.Empty).Trim(), serialText, StringComparison.OrdinalIgnoreCase)))
            {
                BorrowLogRow dbRow = null;
                var okResolve = await WithBusy(async () => { dbRow = await _repo.GetOpenBorrowBySerialAsync(serialText); });
                if (!okResolve) return;
                if (dbRow == null)
                {
                    _resolvedOpen = null;
                    lblReturnInfo.Text = "Borrowed by --";
                    MessageBox.Show("No open borrow found.", "Return", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _resolvedOpen = dbRow;
                lblReturnInfo.Text = $"Borrowed by {dbRow.BorrowedByEmpName} ({dbRow.BorrowedByDeptName}) @ {dbRow.BorrowedAtLocal}";
                UpdateButtons();
            }
            if (_resolvedOpen == null) return;

            using (var dlg = new Yakult.Inventory.App.Forms.BorrowItems.ConfirmReturnDialog(
                new Yakult.Inventory.App.Forms.BorrowItems.ConfirmReturnDialog.ReturnConfirmationModel
                {
                    Item = _resolvedOpen.ItemDisplay,
                    SerialNumber = _resolvedOpen.SerialNumber,
                    BorrowedBy = _resolvedOpen.BorrowedByEmpName,
                    BorrowedAt = _resolvedOpen.BorrowedAtLocal,
                    ReturnedBy = returnedByDisplay,
                    Department = returnDeptDisplay,
                    EncodedBy = string.IsNullOrWhiteSpace(AppSession.CurrentUserName) ? $"UserId {AppSession.CurrentUserId}" : AppSession.CurrentUserName,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                }))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
            }

            var ok = await WithBusy(async () =>
            {
                if (departmentOnly)
                    await _repo.ReturnAsync(_resolvedOpen.BorrowId, null, AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1, dept.Id, dept.Name);
                else
                    await _repo.ReturnAsync(_resolvedOpen.BorrowId, emp.EmpId, AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1);
                ClearReturn();
                _openPageIndex = 0;
                _historyPageIndex = 0;
                await ReloadAsync(fromWithinBusy: true);
            });
            if (!ok) return;

            MessageBox.Show("Return logged.", "Return", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task ReturnSelectedAsync()
        {
            if (_busy || !_schema) return;
            var selected = _wpfRight?.SelectedOpenRow;
            if (selected == null) { MessageBox.Show("Select an open row first.", "Return", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            _resolvedOpen = selected;
            txtReturnSerial.Text = _resolvedOpen.SerialNumber;
            lblReturnInfo.Text = $"Borrowed by {_resolvedOpen.BorrowedByEmpName} ({_resolvedOpen.BorrowedByDeptName}) @ {_resolvedOpen.BorrowedAtLocal}";
            SelectCompanyDeptEmp(cboReturnCompany, cboReturnDept, cboReturnEmp, FindCompanyIdByEmpId(_resolvedOpen.BorrowedByEmpId.GetValueOrDefault()), _resolvedOpen.BorrowedByDeptId.GetValueOrDefault(), _resolvedOpen.BorrowedByEmpId.GetValueOrDefault());
            if (_transactionHost != null)
            {
                _transactionHost.ScanText = _resolvedOpen.SerialNumber;
                _transactionHost.SetReturnInfo(_resolvedOpen);
                _transactionHost.SetMode(Yakult.Inventory.App.Wpf.BorrowItems.TransactionMode.Return);
                _transactionHost.SelectCompany(FindCompanyIdByEmpId(_resolvedOpen.BorrowedByEmpId.GetValueOrDefault()));
                _transactionHost.SelectDepartment(_resolvedOpen.BorrowedByDeptId.GetValueOrDefault());
                _transactionHost.SelectBranch(FindBranchIdByEmpId(_resolvedOpen.BorrowedByEmpId.GetValueOrDefault()));
                _transactionHost.SelectEmployee(_resolvedOpen.BorrowedByEmpId.GetValueOrDefault());
            }
            var selectedWasDepartmentOnly = !(selected.BorrowedByEmpId.HasValue && selected.BorrowedByEmpId.Value > 0);
            await ReturnAsync(departmentOnly: selectedWasDepartmentOnly, departmentId: selected.BorrowedByDeptId);
        }

        private async Task DeleteSelectedBorrowAsync()
        {
            if (_busy) return;
            if (!AppSession.IsAdmin)
            {
                MessageBox.Show("Delete Borrow is an admin-only action.", "Delete Borrow", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var isHistoryTab = _wpfRight != null && _wpfRight.SelectedTabIndex == 1;
            if (isHistoryTab)
            {
                MessageBox.Show("Delete Borrow is only available for open borrow records.", "Delete Borrow", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var row = _wpfRight?.SelectedOpenRow ?? _resolvedOpen;
            if (row == null)
            {
                MessageBox.Show("Select an open borrow first.", "Delete Borrow", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                "Delete this borrow record?\r\n\r\n" +
                $"Serial: {row.SerialNumber}\r\n" +
                $"Borrowed By: {row.BorrowedByEmpName}\r\n" +
                $"Borrowed At: {row.BorrowedAtLocal}\r\n\r\n" +
                "Use this only for encoding mistakes. This removes the open borrow log entry.",
                "Delete Borrow",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes)
                return;

            var ok = await WithBusy(async () =>
            {
                await _repo.DeleteOpenBorrowAsync(row.BorrowId);
                ClearReturn();
                _openPageIndex = 0;
                _historyPageIndex = 0;
                await ReloadAsync(fromWithinBusy: true);
            });
            if (!ok) return;

            MessageBox.Show("Borrow record deleted.", "Delete Borrow", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private Task ReloadAsync() => ReloadAsync(fromWithinBusy: false);

        private async Task ReloadAsync(bool fromWithinBusy)
        {
            if (_busy && !fromWithinBusy) return;
            if (!_schema)
            {
                // Re-check schema on refresh so the dashboard becomes functional immediately after installation.
                var schemaState = await TryRefreshSchemaStateAsync(showErrors: true);
                if (!schemaState.HasValue)
                {
                    _open.Clear();
                    _history.Clear();
                    _openTotalCount = 0;
                    _historyTotalCount = 0;
                    _openOldestBorrowedAtUtc = null;
                    _openPageIndex = 0;
                    _historyPageIndex = 0;
                    FillGrids();
                    RefreshKpi();
                    UpdateLastRefreshLabel();
                    return;
                }

                _schema = schemaState.Value;
                if (!_schema)
                {
                    var dbHint = GetDbHint();
                    _open.Clear();
                    _history.Clear();
                    _openTotalCount = 0;
                    _historyTotalCount = 0;
                    _openOldestBorrowedAtUtc = null;
                    _openPageIndex = 0;
                    _historyPageIndex = 0;
                    FillGrids();
                    RefreshKpi();
                    lblStatus.Text = $"Schema missing in {dbHint}. Install BorrowItems.Prod.Install.sql.";
                    UpdateLastRefreshLabel();
                    return;
                }
            }

            var q = (txtSearch.Text ?? string.Empty).Trim();
            if (!string.Equals(_lastSearch, q, StringComparison.OrdinalIgnoreCase))
            {
                _lastSearch = q;
                _openPageIndex = 0;
                _historyPageIndex = 0;
            }

            Func<Task> load = async () =>
            {
                var fromUtc = AppTime.UtcNow.AddYears(-5);
                var toUtcExclusive = AppTime.UtcNow.AddDays(1);

                var openPage = await _repo.GetOpenBorrowsPageAsync(q, _openPageIndex, _pageSize);
                if (openPage != null && openPage.TotalCount > 0 && _openPageIndex > 0 && (openPage.Rows == null || openPage.Rows.Count == 0))
                {
                    _openPageIndex = Math.Max(0, (openPage.TotalCount - 1) / _pageSize);
                    openPage = await _repo.GetOpenBorrowsPageAsync(q, _openPageIndex, _pageSize);
                }

                var historyPage = await _repo.GetHistoryPageAsync(fromUtc, toUtcExclusive, q, _historyPageIndex, _pageSize);
                if (historyPage != null && historyPage.TotalCount > 0 && _historyPageIndex > 0 && (historyPage.Rows == null || historyPage.Rows.Count == 0))
                {
                    _historyPageIndex = Math.Max(0, (historyPage.TotalCount - 1) / _pageSize);
                    historyPage = await _repo.GetHistoryPageAsync(fromUtc, toUtcExclusive, q, _historyPageIndex, _pageSize);
                }

                _open.Clear(); _history.Clear();
                _openTotalCount = openPage?.TotalCount ?? 0;
                _historyTotalCount = historyPage?.TotalCount ?? 0;
                _openOldestBorrowedAtUtc = openPage?.OldestBorrowedAtUtc;
                if (openPage?.Rows != null) _open.AddRange(openPage.Rows);
                if (historyPage?.Rows != null) _history.AddRange(historyPage.Rows);
                FillGrids();
                if (_displayMode == BorrowDisplayMode.Board)
                    _boardHostControl?.BindData(_open);
                RefreshKpi();
                if (_wpfRight != null)
                    _wpfRight.SetPaging(_openPageIndex, _openTotalCount, _historyPageIndex, _historyTotalCount, _pageSize);
                _lastRefreshLocal = DateTime.Now;
                UpdateLastRefreshLabel();
                UpdateStatusText();
            };

            if (fromWithinBusy)
                await load();
            else
                await WithBusy(load);
        }

        private void FillGrids()
        {
            if (_wpfRight != null)
            {
                _wpfRight.BindData(_open, _history);
            }
        }

        private void SelectOpenRowBySerial(string serial)
        {
            if (_wpfRight != null)
            {
                _wpfRight.SelectOpenRowBySerial(serial);
            }
        }

        private void JumpToOpenBorrow(BorrowLogRow row)
        {
            if (row == null) return;
            if (_wpfRight != null) _wpfRight.SelectedTabIndex = 0; // Open tab
            SelectOpenRowBySerial(row.SerialNumber);
            UpdateButtons();
        }



        private void RefreshKpi()
        {
            if (_wpfRight != null)
            {
                var oldestText = !_openOldestBorrowedAtUtc.HasValue ? "00:00:00" : Elapsed(_openOldestBorrowedAtUtc.Value, null);
                var lastRefreshText = _lastRefreshLocal.HasValue ? _lastRefreshLocal.Value.ToString("MMM d, yyyy h:mm tt") : "never";
                var todayDate = DateTime.Now.Date;
                var todayCount = _open.Count(x => x != null && x.BorrowedAtUtc != default(DateTime) && AppTime.AssumeUtc(x.BorrowedAtUtc).ToLocalTime().Date == todayDate);
                var returnedTodayCount = _history.Count(x => x != null && x.ReturnedAtUtc.HasValue && AppTime.AssumeUtc(x.ReturnedAtUtc.Value).ToLocalTime().Date == todayDate);
                var overdueCount = _open.Count(x => (DateTime.UtcNow - x.BorrowedAtUtc).TotalDays >= 8);
                _wpfRight.SetKpis(_openTotalCount, todayCount, overdueCount, returnedTodayCount, oldestText, lastRefreshText);
            }
        }

        private void UpdateStatusText()
        {
            if (lblStatus == null)
                return;

            // When schema is missing, callers set a specific message; don't override it.
            if (!_schema)
                return;

            var q = (_lastSearch ?? string.Empty).Trim();
            var filterSuffix = q.Length > 0 ? $" (filter: {q})" : string.Empty;
            lblStatus.Text = $"Open: {_openTotalCount} • History: {_historyTotalCount}{filterSuffix}";
        }

        private void ExportCsv()
        {
            var onHistory = _wpfRight != null && _wpfRight.SelectedTabIndex == 1; // 0=Open, 1=History
            var rows = onHistory ? _history : _open;
            if (rows.Count == 0) { MessageBox.Show("No rows to export.", "CSV Export", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            using (var sfd = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "BorrowLog_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv" })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                using (var w = new StreamWriter(sfd.FileName, false, Encoding.UTF8))
                {
                    if (onHistory)
                    {
                        w.WriteLine("SerialNumber,ItemDescription,BorrowedBy,Department,BorrowedAt,ReturnedAt,ReturnedBy,EncodedBy");
                        foreach (var r in _history) w.WriteLine($"{Csv(r.SerialNumber)},{Csv(r.ItemDisplay)},{Csv(r.BorrowedByEmpName)},{Csv(r.BorrowedByDeptName)},{Csv(r.BorrowedAtLocal)},{Csv(r.ReturnedAtLocal)},{Csv(r.ReturnedByEmpName)},{Csv(r.BorrowEncodedByUserName)}");
                    }
                    else
                    {
                        w.WriteLine("SerialNumber,ItemDescription,BorrowedBy,Department,Elapsed,EncodedBy,BorrowedAt");
                        foreach (var r in _open) w.WriteLine($"{Csv(r.SerialNumber)},{Csv(r.ItemDisplay)},{Csv(r.BorrowedByEmpName)},{Csv(r.BorrowedByDeptName)},{Csv(Elapsed(r.BorrowedAtUtc, null))},{Csv(r.BorrowEncodedByUserName)},{Csv(r.BorrowedAtLocal)}");
                    }
                }
                MessageBox.Show("CSV export completed.", "CSV Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async Task<bool> WithBusy(Func<Task> action)
        {
            if (_busy) return false;
            _busy = true;
            UseWaitCursor = true;
            if (pbLoading != null) pbLoading.Visible = true;
            if (lblLoading != null) lblLoading.Visible = true;
            _transactionHost?.SetBusy(true);
            UpdateButtons();
            try
            {
                await action();
                return true;
            }
            catch (Exception ex)
            {
                ShowBorrowError("Borrow Items", "We couldn't complete that borrow action right now. Please try again.", ex, "WithBusy");
                return false;
            }
            finally
            {
                _busy = false;
                UseWaitCursor = false;
                if (pbLoading != null) pbLoading.Visible = false;
                if (lblLoading != null) lblLoading.Visible = false;
                _transactionHost?.SetBusy(false);
                UpdateButtons();
            }
        }

        private void BindCompanies()
        {
            var hasUnknownEmployees = _employees.Any(e => e != null && e.ComId <= 0);

            var companies = new List<CompanyOpt>
            {
                new CompanyOpt { Id = -1, Name = "All Companies" }
            };

            companies.AddRange(_companiesDb
                .Select(c => new CompanyOpt { Id = c.ComId, Name = (c.CompanyName ?? string.Empty).Trim().Length == 0 ? $"ComId {c.ComId}" : c.CompanyName.Trim() })
                .OrderBy(c => c.Name)
                .ToList());

            if (hasUnknownEmployees)
                companies.Add(new CompanyOpt { Id = 0, Name = "Unknown" });

            cboBorrowCompany.DataSource = companies.ToList(); cboBorrowCompany.DisplayMember = "Name"; cboBorrowCompany.ValueMember = "Id";
            cboReturnCompany.DataSource = companies.ToList(); cboReturnCompany.DisplayMember = "Name"; cboReturnCompany.ValueMember = "Id";

            BindDepts(cboBorrowCompany, cboBorrowDept);
            BindEmps(cboBorrowDept, cboBorrowEmp);
            BindDepts(cboReturnCompany, cboReturnDept);
            BindEmps(cboReturnDept, cboReturnEmp);

            if (_transactionHost != null)
            {
                var workspaceCompanies = companies
                    .Select(c => new Yakult.Inventory.App.Wpf.BorrowItems.IdNamePair { Id = c.Id, Name = c.Name })
                    .ToList();
                _transactionHost.SetCompanies(workspaceCompanies);
                _transactionHost.SetEmployees(_employees);
            }
        }

        private void BindDepts(ComboBox company, ComboBox dept)
        {
            var co = company?.SelectedItem as CompanyOpt;
            var comId = co?.Id ?? 0;
            var filtered = _employees.Where(e => e != null && (comId < 0 || e.ComId == comId));

            var depts = filtered
                .Where(e => e.DeptId > 0)
                .GroupBy(e => e.DeptId)
                .Select(g => new Dept { Id = g.Key, CompanyId = g.First().ComId, Name = g.First().DepartmentName })
                .OrderBy(d => d.Name)
                .ToList();

            if (filtered.Any(e => e.DeptId <= 0))
                depts.Add(new Dept { Id = 0, CompanyId = comId, Name = "N/A" });

            dept.DataSource = depts.ToList(); dept.DisplayMember = "Name"; dept.ValueMember = "Id";
        }

        private void BindEmps(ComboBox dept, ComboBox emp)
        {
            var d = dept.SelectedItem as Dept;
            var comId = GetSelectedCompanyIdForDeptCombo(dept);
            var list = _employees
                .Where(x => d != null && ((d.Id > 0 && x.DeptId == d.Id) || (d.Id == 0 && x.DeptId <= 0)) && (comId < 0 || x.ComId == comId))
                .OrderBy(x => x.EmployeeName)
                .ToList();
            emp.DataSource = list; emp.DisplayMember = "DisplayText"; emp.ValueMember = "EmpId"; UpdateButtons();
        }

        private void OnOpenGridSelectionChanged()
        {
            try
            {
                if (txtReturnSerial == null)
                {
                    UpdateButtons();
                    return;
                }

                var selected = _wpfRight?.SelectedOpenRow ?? _resolvedOpen;
                if (selected != null)
                {
                    _resolvedOpen = selected;
                    var serial = (selected?.SerialNumber ?? string.Empty).Trim();
                    if (serial.Length > 0)
                    {
                        var current = (txtReturnSerial.Text ?? string.Empty).Trim();
                        if (!string.Equals(current, serial, StringComparison.OrdinalIgnoreCase))
                        {
                            txtReturnSerial.Text = serial;
                        }
                    }

                    if (selected != null)
                    {
                        if (lblReturnInfo != null)
                            lblReturnInfo.Text = $"Borrowed by {selected.BorrowedByEmpName} ({selected.BorrowedByDeptName}) @ {selected.BorrowedAtLocal}";

                        if (cboReturnCompany != null && cboReturnDept != null && cboReturnEmp != null && selected.BorrowedByEmpId > 0 && selected.BorrowedByDeptId > 0)
                        {
                            var comId = FindCompanyIdByEmpId(selected.BorrowedByEmpId.GetValueOrDefault());
                            SelectCompanyDeptEmp(cboReturnCompany, cboReturnDept, cboReturnEmp, comId, selected.BorrowedByDeptId.GetValueOrDefault(), selected.BorrowedByEmpId.GetValueOrDefault());
                        }

                        if (_transactionHost != null)
                        {
                            var comId = FindCompanyIdByEmpId(selected.BorrowedByEmpId.GetValueOrDefault());
                            _transactionHost.ScanText = serial;
                            _transactionHost.SetReturnInfo(selected);
                            _transactionHost.SetMode(Yakult.Inventory.App.Wpf.BorrowItems.TransactionMode.Return);
                            _transactionHost.SelectCompany(comId);
                            _transactionHost.SelectDepartment(selected.BorrowedByDeptId.GetValueOrDefault());
                            _transactionHost.SelectBranch(FindBranchIdByEmpId(selected.BorrowedByEmpId.GetValueOrDefault()));
                            _transactionHost.SelectEmployee(selected.BorrowedByEmpId.GetValueOrDefault());
                        }
                    }
                }
            }
            catch
            {
            }
            finally
            {
                UpdateButtons();
            }
        }

        private void SelectCompanyDeptEmp(ComboBox company, ComboBox dept, ComboBox emp, int comId, int deptId, int empId)
        {
            // Force rebind in the correct order so selections don't get overwritten by change events.
            for (var i = 0; i < company.Items.Count; i++) { var c = company.Items[i] as CompanyOpt; if (c != null && c.Id == comId) { company.SelectedIndex = i; break; } }
            BindDepts(company, dept);
            for (var i = 0; i < dept.Items.Count; i++) { var d = dept.Items[i] as Dept; if (d != null && d.Id == deptId) { dept.SelectedIndex = i; break; } }
            BindEmps(dept, emp);
            for (var i = 0; i < emp.Items.Count; i++) { var e = emp.Items[i] as BorrowEmployeeLookup; if (e != null && e.EmpId == empId) { emp.SelectedIndex = i; break; } }
        }

        private void SyncBorrowFromWpf(BorrowFormEventArgs e)
        {
            if (e == null) return;
            _suppressBorrowEmp = true;
            try
            {
                if (e.CompanyId.HasValue)
                {
                    for (int i = 0; i < cboBorrowCompany.Items.Count; i++)
                        if ((cboBorrowCompany.Items[i] as CompanyOpt)?.Id == e.CompanyId.Value)
                        { cboBorrowCompany.SelectedIndex = i; break; }
                }
                BindDepts(cboBorrowCompany, cboBorrowDept);
                if (e.DepartmentId.HasValue)
                {
                    for (int i = 0; i < cboBorrowDept.Items.Count; i++)
                        if ((cboBorrowDept.Items[i] as Dept)?.Id == e.DepartmentId.Value)
                        { cboBorrowDept.SelectedIndex = i; break; }
                }
                BindEmps(cboBorrowDept, cboBorrowEmp);
                if (e.EmployeeId.HasValue)
                {
                    for (int i = 0; i < cboBorrowEmp.Items.Count; i++)
                        if ((cboBorrowEmp.Items[i] as BorrowEmployeeLookup)?.EmpId == e.EmployeeId.Value)
                        { cboBorrowEmp.SelectedIndex = i; break; }
                }
            }
            finally { _suppressBorrowEmp = false; }
        }

        private void SyncReturnFromWpf(BorrowFormEventArgs e)
        {
            if (e == null) return;
            _suppressBorrowEmp = true;
            try
            {
                if (e.CompanyId.HasValue)
                {
                    for (int i = 0; i < cboReturnCompany.Items.Count; i++)
                        if ((cboReturnCompany.Items[i] as CompanyOpt)?.Id == e.CompanyId.Value)
                        { cboReturnCompany.SelectedIndex = i; break; }
                }
                BindDepts(cboReturnCompany, cboReturnDept);
                if (e.DepartmentId.HasValue)
                {
                    for (int i = 0; i < cboReturnDept.Items.Count; i++)
                        if ((cboReturnDept.Items[i] as Dept)?.Id == e.DepartmentId.Value)
                        { cboReturnDept.SelectedIndex = i; break; }
                }
                BindEmps(cboReturnDept, cboReturnEmp);
                if (e.EmployeeId.HasValue)
                {
                    for (int i = 0; i < cboReturnEmp.Items.Count; i++)
                        if ((cboReturnEmp.Items[i] as BorrowEmployeeLookup)?.EmpId == e.EmployeeId.Value)
                        { cboReturnEmp.SelectedIndex = i; break; }
                }
            }
            finally { _suppressBorrowEmp = false; }
        }

        private int FindCompanyIdByEmpId(int empId)
        {
            if (empId <= 0) return 0;
            var e = _employeesDb.FirstOrDefault(x => x != null && x.EmpId == empId);
            return e?.ComId ?? 0;
        }

        private int FindBranchIdByEmpId(int empId)
        {
            if (empId <= 0) return 0;
            var e = _employeesDb.FirstOrDefault(x => x != null && x.EmpId == empId);
            return e?.BranchId ?? 0;
        }

        /// <summary>
        /// Resolves a department id straight from the employee list, independent of whatever
        /// the hidden legacy Company/Department combos currently have selected. The WPF workspace
        /// (the visible Borrow card) and the hidden combos build their dropdown options from the
        /// same _employees list but filter "no company selected" differently (&lt;=0 vs &lt;0), so
        /// syncing by combo selection can silently fail to find a match. Looking the department up
        /// directly by id sidesteps that sync entirely for the department-only borrow path.
        /// </summary>
        private Dept ResolveDeptById(int deptId)
        {
            if (deptId <= 0) return null;
            var match = _employees.FirstOrDefault(e => e != null && e.DeptId == deptId);
            if (match == null) return null;
            return new Dept { Id = deptId, CompanyId = match.ComId, Name = match.DepartmentName };
        }

        private int GetSelectedCompanyIdForDeptCombo(ComboBox deptCombo)
        {
            var co = (deptCombo == cboBorrowDept ? cboBorrowCompany?.SelectedItem as CompanyOpt : cboReturnCompany?.SelectedItem as CompanyOpt);
            return co?.Id ?? 0;
        }

        private void ResetBorrowBackdateControls()
        {
            var now = DateTime.Now;

            if (dtpBorrowDate != null)
                dtpBorrowDate.Value = now.Date;

            if (dtpBorrowTime != null)
                dtpBorrowTime.Value = DateTime.Today.Add(now.TimeOfDay);
        }

        private void UpdateBorrowBackdateVisibility()
        {
            if (pnlBorrowBackdate == null || chkBorrowBackdate == null)
                return;

            var showDetails = chkBorrowBackdate.Checked;

            if (showDetails && dtpBorrowDate != null && dtpBorrowTime != null)
                ResetBorrowBackdateControls();

            pnlBorrowBackdate.Visible = true;

            if (lblBorrowBackdateDate != null)
                lblBorrowBackdateDate.Visible = showDetails;

            if (dtpBorrowDate != null)
                dtpBorrowDate.Visible = showDetails;

            if (lblBorrowBackdateTime != null)
                lblBorrowBackdateTime.Visible = showDetails;

            if (dtpBorrowTime != null)
                dtpBorrowTime.Visible = showDetails;
        }

        private bool TryGetSelectedBorrowedAt(out DateTime? borrowedAtUtc, out string borrowedAtDisplay)
        {
            borrowedAtUtc = null;
            borrowedAtDisplay = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            if (chkBorrowBackdate == null || !chkBorrowBackdate.Checked || dtpBorrowDate == null || dtpBorrowTime == null)
                return true;

            var actualLocal = dtpBorrowDate.Value.Date + dtpBorrowTime.Value.TimeOfDay;
            if (actualLocal > DateTime.Now.AddMinutes(1))
            {
                MessageBox.Show("Borrowed date/time cannot be in the future.", "Borrow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            borrowedAtDisplay = actualLocal.ToString("yyyy-MM-dd HH:mm");
            borrowedAtUtc = DateTime.SpecifyKind(actualLocal, DateTimeKind.Local).ToUniversalTime();
            return true;
        }

        private void OnBorrowEmpTextChanged(object sender, EventArgs e)
        {
            if (_suppressBorrowEmp) return;

            // Preserve the raw input. Trimming the ComboBox text on every keystroke
            // removes a trailing space and prevents typing names such as "Mary Ann".
            var rawText = cboBorrowEmp.Text ?? string.Empty;
            var query = rawText.Trim();
            if (query.Length < 2)
            {
                if (_borrowEmpLocked) UnlockBorrowEmpSearch();
                return;
            }

            var tokens = query.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            var filtered = _employees
                .Where(x => x != null)
                .Where(x =>
                {
                    var searchable = string.Join(" ",
                        x.EmployeeName ?? string.Empty,
                        x.DisplayText ?? string.Empty,
                        x.DepartmentName ?? string.Empty,
                        x.BranchName ?? string.Empty);
                    return tokens.All(token => searchable.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
                })
                .OrderBy(x => x.EmployeeName)
                .ThenBy(x => x.DepartmentName)
                .ThenBy(x => x.BranchName)
                .ToList();

            _suppressBorrowEmp = true;
            cboBorrowEmp.DataSource = filtered;
            cboBorrowEmp.DisplayMember = "DisplayText";
            cboBorrowEmp.ValueMember = "EmpId";
            cboBorrowEmp.Text = rawText;
            cboBorrowEmp.SelectionStart = cboBorrowEmp.Text.Length;
            if (filtered.Count > 0) cboBorrowEmp.DroppedDown = true;
            _suppressBorrowEmp = false;
        }

        private void OnBorrowEmpSelected(object sender, EventArgs e)
        {
            if (_suppressBorrowEmp) return;
            UpdateButtons();

            var emp = cboBorrowEmp.SelectedItem as BorrowEmployeeLookup;
            if (emp == null || emp.EmpId <= 0) return;

            _suppressBorrowEmp = true;
            SelectCompanyDeptEmp(cboBorrowCompany, cboBorrowDept, cboBorrowEmp, emp.ComId, emp.DeptId, emp.EmpId);
            _borrowEmpLocked = true;
            cboBorrowCompany.Enabled = false;
            cboBorrowDept.Enabled = false;
            _suppressBorrowEmp = false;
        }

        private void UnlockBorrowEmpSearch()
        {
            if (!_borrowEmpLocked) return;
            _borrowEmpLocked = false;
            cboBorrowCompany.Enabled = true;
            cboBorrowDept.Enabled = true;
            _suppressBorrowEmp = true;
            cboBorrowEmp.Text = string.Empty;
            _suppressBorrowEmp = false;
        }

        private void ClearBorrow()
        {
            _resolvedItem = null;
            txtBorrowSerial.Clear();
            lblBorrowItem.Text = "Item: --";
            lblBorrowDesc.Text = "Description: --";

            if (chkBorrowBackdate != null)
                chkBorrowBackdate.Checked = false;

            ResetBorrowBackdateControls();
            UpdateBorrowBackdateVisibility();
            _transactionHost?.Clear();
        }

        private void ClearReturn()
        {
            _resolvedOpen = null;
            txtReturnSerial.Clear();
            lblReturnInfo.Text = "Borrowed by --";
            _transactionHost?.Clear();
        }
        private void UpdateButtons()
        {
            var ready = !_busy && _schema;
            btnBorrow.Enabled = ready && _resolvedItem != null && (rbBorrowEmpListed == null || rbBorrowEmpListed.Checked) && cboBorrowEmp.SelectedItem is BorrowEmployeeLookup;
            btnReturn.Enabled = ready && _resolvedOpen != null && (rbReturnEmpListed == null || rbReturnEmpListed.Checked) && cboReturnEmp.SelectedItem is BorrowEmployeeLookup;
            if (btnAddNewItem != null) btnAddNewItem.Enabled = !_busy;
            if (btnSearch != null) btnSearch.Enabled = !_busy;
            if (btnRefresh != null) btnRefresh.Enabled = !_busy;
        }

        private void UpdateLastRefreshLabel()
        {
            if (lblLastRefresh == null) return;
            lblLastRefresh.Text = "Last refreshed: " + (_lastRefreshLocal.HasValue ? _lastRefreshLocal.Value.ToString("yyyy-MM-dd HH:mm:ss") : "--");
        }

        private async Task AddEmployeeAndSelectAsync(bool forBorrow)
        {
            if (_busy) return;

            using (var dlg = new QuickAddEmployeeDialog())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || !dlg.NewEmployeeId.HasValue)
                    return;

                var newEmpId = dlg.NewEmployeeId.Value;

                var ok = await WithBusy(async () =>
                {
                    var all = await _repo.GetActiveEmployeesAsync(12000);
                    _employees.Clear();
                _employees.AddRange((all ?? new List<BorrowEmployeeLookup>()).Where(e => e != null && e.EmpId > 0));
                    _employeesDb.Clear();
                    _employeesDb.AddRange(_employees);
                });
                if (!ok) return;

                // Rebind dropdowns and auto-select the newly added employee.
                BindCompanies();
                var emp = _employeesDb.FirstOrDefault(e => e != null && e.EmpId == newEmpId);
                if (emp != null)
                {
                    if (forBorrow)
                    {
                        SelectCompanyDeptEmp(cboBorrowCompany, cboBorrowDept, cboBorrowEmp, emp.ComId, emp.DeptId, emp.EmpId);
                        if (rbBorrowEmpListed != null) rbBorrowEmpListed.Checked = true;
                        if (_transactionHost != null)
                        {
                            _transactionHost.SelectCompany(emp.ComId);
                            _transactionHost.SelectDepartment(emp.DeptId);
                            _transactionHost.SelectEmployee(emp.EmpId);
                        }
                    }
                    else
                    {
                        SelectCompanyDeptEmp(cboReturnCompany, cboReturnDept, cboReturnEmp, emp.ComId, emp.DeptId, emp.EmpId);
                        if (rbReturnEmpListed != null) rbReturnEmpListed.Checked = true;
                        if (_transactionHost != null)
                        {
                            _transactionHost.SelectCompany(emp.ComId);
                            _transactionHost.SelectDepartment(emp.DeptId);
                            _transactionHost.SelectEmployee(emp.EmpId);
                        }
                    }
                }
            }
        }

        private async Task ClearSearchAndReloadAsync()
        {
            if (_busy) return;
            if (txtSearch == null) return;
            txtSearch.Clear();
            if (btnClearSearch != null) btnClearSearch.Visible = false;
            await ReloadAsync();
        }

        // --- BEGIN: Borrow Items "See Guide" button (open guide) ----------------------------
        // Wired to btnHelp above. Opens Docs\BorrowItemsGuide.html (deployed next to the exe
        // via the csproj's <None Include="Docs\BorrowItemsGuide.html"> CopyToOutputDirectory
        // entry) in the user's default browser. Comment out this method and the two blocks
        // marked "See Guide" earlier in the file to remove the feature entirely.
        private void OpenBorrowItemsGuide()
        {
            try
            {
                var guidePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Docs", "BorrowItemsGuide.html");
                if (!File.Exists(guidePath))
                {
                    MessageBox.Show(this, $"Guide file not found:\r\n{guidePath}", "See Guide",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Process.Start(new ProcessStartInfo(guidePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not open the guide:\r\n{ex.Message}", "See Guide",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // --- END: Borrow Items "See Guide" button (open guide) ------------------------------

        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private static void SetCueBanner(TextBox textBox, string cueText)
        {
            try
            {
                if (textBox == null || textBox.IsDisposed) return;
                SendMessage(textBox.Handle, EM_SETCUEBANNER, IntPtr.Zero, cueText ?? string.Empty);
            }
            catch
            {
            }
        }

        private static Image TryLoadYakultNameLogo()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var candidates = new[]
                {
                    Path.Combine(baseDir, "Images", "yakult_Name.png"),
                    Path.Combine(baseDir, "images", "yakult_Name.png"),
                    Path.Combine(baseDir, "yakult_Name.png")
                };

                foreach (var path in candidates)
                {
                    if (!File.Exists(path)) continue;
                    return LoadImageUnlocked(path);
                }
            }
            catch
            {
            }

            return null;
        }

        private static Image LoadImageUnlocked(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var img = Image.FromStream(fs))
            {
                return (Image)img.Clone();
            }
        }



        private async Task PrevPageAsync()
        {
            if (_busy || !_schema) return;
            var isHistoryTab = _wpfRight != null && _wpfRight.SelectedTabIndex == 1;
            if (isHistoryTab)
            {
                if (_historyPageIndex <= 0) return;
                _historyPageIndex--;
            }
            else
            {
                if (_openPageIndex <= 0) return;
                _openPageIndex--;
            }
            await ReloadAsync();
        }

        private async Task NextPageAsync()
        {
            if (_busy || !_schema) return;
            var isHistoryTab = _wpfRight != null && _wpfRight.SelectedTabIndex == 1;
            if (isHistoryTab)
            {
                if (((_historyPageIndex + 1) * _pageSize) >= _historyTotalCount) return;
                _historyPageIndex++;
            }
            else
            {
                if (((_openPageIndex + 1) * _pageSize) >= _openTotalCount) return;
                _openPageIndex++;
            }
            await ReloadAsync();
        }



        private static string GetDbHint()
        {
            try
            {
                var cs = DatabaseConfig.ConnectionString;
                if (string.IsNullOrWhiteSpace(cs))
                    return "unknown-server/unknown-db";

                var b = new SqlConnectionStringBuilder(cs);
                var server = string.IsNullOrWhiteSpace(b.DataSource) ? "unknown-server" : b.DataSource.Trim();
                var db = string.IsNullOrWhiteSpace(b.InitialCatalog) ? "unknown-db" : b.InitialCatalog.Trim();
                return server + "/" + db;
            }
            catch
            {
                return "unknown-server/unknown-db";
            }
        }





        private void OpenBorrowDetails(BorrowLogRow row)
        {
            if (row == null)
                return;

            var dlg = new WpfBorrowDetailsDialog(row);
            var owner = Form.ActiveForm;
            if (owner != null)
                new System.Windows.Interop.WindowInteropHelper(dlg).Owner = owner.Handle;
            dlg.ShowDialog();
        }

        private static SectionCardPanel BuildSectionCard(string title, Color accentColor, Control content)
        {
            var card = new SectionCardPanel
            {
                Dock = DockStyle.Fill,
                AccentColor = accentColor,
                Padding = new Padding(14, 14, 14, 12)
            };

            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var titleLabel = new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 10),
                BackColor = Color.Transparent
            };

            content.Margin = new Padding(0);
            content.Dock = DockStyle.Fill;

            shell.Controls.Add(titleLabel, 0, 0);
            shell.Controls.Add(content, 0, 1);
            card.Controls.Add(shell);

            return card;
        }

        private static Button NewBtn(string text, int w, Color c) { var b = new Button { Text = text, Width = w, Height = 36, BackColor = c, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) }; b.FlatAppearance.BorderSize = 0; return b; }
        private static Button NewTabButton(string text)
        {
            var button = new Button
            {
                Text = text,
                Width = 150,
                Height = 34,
                Dock = DockStyle.Left,
                Margin = new Padding(0),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseDownBackColor = Color.Empty;
            button.FlatAppearance.MouseOverBackColor = Color.Empty;
            return button;
        }
        private static ComboBox NewCombo() => new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10.5F), Margin = new Padding(0) };
        private static DataGridView NewGrid()
        {
            var clrTextMuted = Color.FromArgb(80, 90, 100);
            var clrTextDark = Color.FromArgb(44, 62, 80);

            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(229, 231, 235)
            };

            try
            {
                typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(g, true, null);
            }
            catch
            {
            }

            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            g.ColumnHeadersDefaultCellStyle.ForeColor = clrTextMuted;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(10);
            g.ColumnHeadersHeight = 40;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            g.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            g.DefaultCellStyle.ForeColor = clrTextDark;
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(239, 246, 255);
            g.DefaultCellStyle.SelectionForeColor = clrTextDark;
            g.DefaultCellStyle.Padding = new Padding(8, 4, 8, 4);
            g.RowTemplate.Height = 40;

            return g;
        }

        private Panel BuildBoardHost()
        {
            return new Panel { Dock = DockStyle.Fill };
        }

        private static string SafeText(string value, string fallback)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length == 0 ? fallback : text;
        }

        private void ApplyTabButtonTheme(Button button, bool selected)
        {
            if (button == null)
                return;

            var palette = BorrowUiTheme.Current;
            button.BackColor = selected ? palette.TabActiveBack : palette.TabInactiveBack;
            button.ForeColor = selected ? palette.TabActiveFore : palette.TabInactiveFore;
            button.Font = new Font("Segoe UI", 10F, selected ? FontStyle.Bold : FontStyle.Regular);
        }
        private static Panel NewKpi(string t, out Label v, Color c) { var p = new Panel { Width = 198, Height = 86, BackColor = c }; p.Controls.Add(new Label { Text = t, Left = 12, Top = 10, ForeColor = Color.White, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), AutoSize = true }); v = new Label { Text = "0", Left = 12, Top = 34, ForeColor = Color.White, Font = new Font("Segoe UI", 22, FontStyle.Bold), AutoSize = true }; p.Controls.Add(v); return p; }
        private static Label NewEmpty(string text) => new Label { Dock = DockStyle.Fill, Text = text, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(110, 120, 130), BackColor = Color.White, Font = new Font("Segoe UI", 11F, FontStyle.Regular) };
        private static string Elapsed(DateTime startUtc, DateTime? endUtc) { if (startUtc == default(DateTime)) return "00:00:00"; var s = AppTime.AssumeUtc(startUtc); var e = endUtc.HasValue ? AppTime.AssumeUtc(endUtc.Value) : AppTime.UtcNow; var ts = e - s; if (ts.TotalSeconds < 0) ts = TimeSpan.Zero; return ts.TotalDays >= 1 ? $"{(int)ts.TotalDays}d {ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}" : $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"; }
        private static string Csv(string s) { s = s ?? string.Empty; if (s.Contains("\"")) s = s.Replace("\"", "\"\""); return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s + "\"" : s; }

        private async Task<bool?> TryRefreshSchemaStateAsync(bool showErrors)
        {
            try
            {
                return await _repo.BorrowSchemaExistsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("[BorrowItemsDashboard] Failed to check borrow schema.", ex);
                if (showErrors)
                {
                    if (lblStatus != null)
                        lblStatus.Text = $"Borrow items database is unavailable in {GetDbHint()}.";

                    MessageBox.Show(
                        "We couldn't reach the borrow items database right now. Please check the connection and try again.",
                        "Borrow Items",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                return null;
            }
        }

        private void ShowBorrowError(string title, string userMessage, Exception ex, string context)
        {
            Logger.LogError($"[BorrowItemsDashboard] {context}", ex);

            var message = IsDatabaseUnavailable(ex)
                ? "The borrow items database is unavailable right now. Please check the connection and try again."
                : userMessage;

            MessageBox.Show(
                message,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static bool IsDatabaseUnavailable(Exception ex)
        {
            if (ex == null)
                return false;

            if (ex is SqlException)
                return true;

            var message = ex.Message ?? string.Empty;
            return message.IndexOf("network-related", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("server was not found", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("establishing a connection", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("connection string", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<Yakult.Inventory.App.Wpf.BorrowItems.IdNamePair> GetOldDeptList(ComboBox deptCombo)
        {
            var result = new List<Yakult.Inventory.App.Wpf.BorrowItems.IdNamePair>();
            foreach (var item in deptCombo.Items)
            {
                if (item is Dept d)
                    result.Add(new Yakult.Inventory.App.Wpf.BorrowItems.IdNamePair { Id = d.Id, Name = d.Name });
            }
            return result;
        }

        private static List<BorrowEmployeeLookup> GetOldEmpList(ComboBox empCombo)
        {
            var result = new List<BorrowEmployeeLookup>();
            foreach (var item in empCombo.Items)
            {
                if (item is BorrowEmployeeLookup e)
                    result.Add(e);
            }
            return result;
        }

        private sealed class CompanyOpt { public int Id { get; set; } public string Name { get; set; } }
        private sealed class Dept { public int Id { get; set; } public int CompanyId { get; set; } public string Name { get; set; } }

        private sealed class SectionCardPanel : Panel
        {
            public Color BorderColor { get; set; } = Color.FromArgb(52, 63, 78);
            public Color AccentColor { get; set; } = Color.FromArgb(53, 96, 201);
            public Color TitleForeColor { get; set; } = Color.White;
            public Color TitleBackColor { get; set; } = Color.FromArgb(31, 37, 48);

            public SectionCardPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
                Margin = new Padding(0);
            }

            protected override void OnControlAdded(ControlEventArgs e)
            {
                base.OnControlAdded(e);
                var shell = e.Control as TableLayoutPanel;
                if (shell == null || shell.Controls.Count == 0)
                    return;

                var titleLabel = shell.Controls[0] as Label;
                if (titleLabel != null)
                {
                    titleLabel.ForeColor = TitleForeColor;
                    titleLabel.BackColor = TitleBackColor;
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                var rect = ClientRectangle;
                if (rect.Width <= 1 || rect.Height <= 1)
                    return;

                rect.Width -= 1;
                rect.Height -= 1;

                using (var borderPen = new Pen(BorderColor))
                using (var accentBrush = new SolidBrush(AccentColor))
                {
                    e.Graphics.DrawRectangle(borderPen, rect);
                    e.Graphics.FillRectangle(accentBrush, rect.Left + 1, rect.Top + 1, Math.Min(72, rect.Width - 1), 3);
                }
            }
        }
    }
}
