using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.WPF.RequestPortal;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Wpf.Notifications;
using Yakult.Inventory.App.Models.ViewModels;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Employee;
using Yakult.Inventory.App.WPF.RequestPortal.NewRequest.ViewModels;
using Yakult.Inventory.App.WPF.RequestPortal.NewRequest.Views;
using Yakult.Inventory.App.WPF.RequestPortal.AssistedRequest.ViewModels;
using Yakult.Inventory.App.WPF.RequestPortal.AssistedRequest.Views;
using Yakult.Inventory.App.WPF.RequestPortal.RequestHistory.ViewModels;
using Yakult.Inventory.App.WPF.RequestPortal.RequestHistory.Views;
using Yakult.Inventory.App.WPF.RequestPortal.AuthorizationHistory.ViewModels;
using Yakult.Inventory.App.WPF.RequestPortal.AuthorizationHistory.Views;
using Yakult.Inventory.App.WPF.RequestPortal.AuthorizeRequest.Views;
using Yakult.Inventory.App.WPF.RequestPortal.WalkThrough;
using Yakult.Inventory.App.Wpf.Portal;

namespace Yakult.Inventory.App.Pages.RequestPortal
{
    /// <summary>
    /// Requester Portal Form — dual-mode request submission.
    ///
    /// Self-Request (all users):
    ///   Identity and destination are resolved from the login session and cannot be edited.
    ///   The submitter always requests for themselves.
    ///
    /// Assisted Request (IT staff only — Developer / Admin / IT Manager / Supervisor / Tech Support):
    ///   IT user selects the target employee and creates the request on their behalf.
    ///   Destination auto-fills from the selected employee's profile.
    ///   Visually distinguished with an amber header.
    ///   Backend enforces the role check regardless of UI visibility.
    /// </summary>
    public partial class RequesterPortalForm : Form
    {
        private RequesterPortalService _portalService;
        private int _currentUserId;
        private NewRequestViewModel _newRequestViewModel;
        private AssistedRequestViewModel _assistedRequestViewModel;
        private RequestHistoryViewModel _requestHistoryViewModel;
        private AuthorizationHistoryViewModel _authHistoryViewModel;

        // Guided tour
        private WpfPortalTourService _tourService;
        private NewRequestView _newRequestViewRef;
        private AssistedRequestView _assistedRequestViewRef;
        private RequestHistoryView _requestHistoryViewRef;
        private AuthorizationHistoryView _authHistoryViewRef;
        private Button _btnHelp;

        // Portal hamburger side menu
        private Panel  _portalSideMenu;
        private Panel  _portalMenuOverlay;
        private bool   _portalMenuOpen;
        private const int PortalMenuWidth = 280;

        /// <summary>True when the user clicked Logout from the side menu.</summary>
        public bool LogoutRequested { get; private set; }

        // Notification bell (WPF UserControl hosted via ElementHost)
        private NotificationBellControl  _notifBell;
        private ElementHost              _notifBellHost;
        private int                      _notifUnreadCount;
        private NotificationDropdownForm _notifPopup;
        private DateTime                 _notifPopupClosedAt = DateTime.MinValue;
        private Services.DesktopNotificationPoller _notifPoller;
        private Timer                    _badgeTimer;

        // Tab layout
        private TabControl tabControl;
        private TabPage tabNewRequest;       // "New Request" — self-request, visible to all
        private TabPage tabAssistedRequest;  // "Assisted Request" — IT staff only
        private TabPage tabMyRequests;       // "Request History" — visible to all
        private TabPage tabApprovals;        // "Approvals" — approver positions only
        private TabPage tabAuthHistory;      // "Authorization History" — visible to all

        // ── Approver-mode panels (IsApprover == true) ─────────────────────────
        private Panel _pnlLanding;           // Home landing with three module cards
        private Panel _pnlApproverArea;      // Authorize Cartridge Requests area
        private Panel _pnlRequestArea;       // Submit a Request area (wraps tabControl)
        private AuthorizeRequestView _approvalPage; // Reference for self-sign navigation
        // Landing page card references — used by the approver homepage tour
        private Panel _landingCardAuthorize;
        private Panel _landingCardSubmit;
        private Panel _landingCardHistory;

        // ── Self-Request Tab Controls ──────────────────────────────────────────
        private ComboBox cmbCartridgeModel;
        private Label lblNoStockWarning;
        private NumericUpDown nudQuantity;
        private NumericUpDown nudGoodQty;
        private NumericUpDown nudDamagedQty;
        private Button btnAddItem;
        private DataGridView dgvRequestItems;
        private Button btnRemoveItem;
        private RadioButton rbWithCartridge;
        private RadioButton rbWithoutCartridge;
        private RadioButton rbPickup;
        private RadioButton rbDelivery;
        private Label lblReceivedBy;
        private ComboBox cmbReceivedBy;
        private TextBox txtRemarks;
        private Button btnSubmit;
        private Button btnClear;

        // ── IT Assisted Request Tab Controls ──────────────────────────────────
        private ComboBox cmbAssistedEmployee;
        private bool _suppressAssistedEmpEvents;
        private Button btnQuickAddEmployee;
        private ComboBox cmbAssistedCartridgeModel;
        private NumericUpDown nudAssistedQuantity;
        private NumericUpDown nudAssistedGoodQty;
        private NumericUpDown nudAssistedDamagedQty;
        private DataGridView dgvAssistedRequestItems;
        private Button btnAssistedAddItem;
        private Button btnAssistedRemoveItem;
        private RadioButton rbAssistedWithCartridge;
        private RadioButton rbAssistedWithoutCartridge;
        private ComboBox cmbAssistedCompany;
        private ComboBox cmbAssistedBranch;
        private ComboBox cmbAssistedDepartment;
        private RadioButton rbAssistedPickup;
        private RadioButton rbAssistedDelivery;
        private Label lblAssistedReceivedBy;
        private ComboBox cmbAssistedReceivedBy;
        private TextBox txtAssistedRemarks;
        private Button btnAssistedSubmit;
        private Button btnAssistedClear;

        // ── My Requests Tab Controls ───────────────────────────────────────────
        private DataGridView dgvMyRequests;
        private Button btnRefresh;
        private Label lblRequestCount;

        // ── Department Account mode: employee selection ─────────────────────────
        private ComboBox cmbDeptEmployee;
        private Label lblDeptEmpInfo;
        private EmployeeViewModel _deptAccountEmployee;

        // ── Data ───────────────────────────────────────────────────────────────
        private List<CartridgeModelAvailabilityViewModel> _cartridgeModels = new List<CartridgeModelAvailabilityViewModel>();
        private List<EmployeeViewModel> _employees = new List<EmployeeViewModel>();
        private List<CompanyViewModel> _companies = new List<CompanyViewModel>();
        private List<BranchViewModel> _branches = new List<BranchViewModel>();
        private List<DepartmentViewModel> _departments = new List<DepartmentViewModel>();

        private List<CartridgeRequestItemViewModel> _requestItems = new List<CartridgeRequestItemViewModel>();
        private List<CartridgeRequestItemViewModel> _assistedRequestItems = new List<CartridgeRequestItemViewModel>();

        // ══════════════════════════════════════════════════════════════════════
        // Constructor
        // ══════════════════════════════════════════════════════════════════════

        public RequesterPortalForm()
        {
            InitializeComponent();
            _portalService = new RequesterPortalService();

            try { _currentUserId = AppSession.CurrentUserId; }
            catch { _currentUserId = 1; }

            LoadData();

            if (AppSession.IsReadOnly)
                ApplyReadOnlyMode();

            InitializePortalSideMenu();
        }

        private void ApplyReadOnlyMode()
        {
            // Viewers can browse history but cannot submit new requests
            if (tabControl != null && tabNewRequest != null)
                tabControl.TabPages.Remove(tabNewRequest);
        }

        // ── Portal hamburger side menu ─────────────────────────────────────────

        private void InitializePortalSideMenu()
        {
            const int headerHeight = 70;

            // Overlay: captures click-outside-to-close, sits behind the side panel
            _portalMenuOverlay = new Panel
            {
                Location  = new Point(PortalMenuWidth, headerHeight),
                Size      = new Size(this.ClientSize.Width - PortalMenuWidth,
                                     this.ClientSize.Height - headerHeight),
                BackColor = Color.Transparent,
                Visible   = false,
                Anchor    = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _portalMenuOverlay.Click += (s, e) => { if (_portalMenuOpen) TogglePortalMenu(); };
            this.Controls.Add(_portalMenuOverlay);
            this.Controls.SetChildIndex(_portalMenuOverlay, this.Controls.Count - 1);

            // Side panel
            _portalSideMenu = new Panel
            {
                Width      = PortalMenuWidth,
                Height     = this.ClientSize.Height - headerHeight,
                Location   = new Point(0, headerHeight),
                BackColor  = Color.White,
                Anchor     = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
                AutoScroll = true,
                Visible    = false
            };
            _portalSideMenu.HorizontalScroll.Enabled = false;
            _portalSideMenu.HorizontalScroll.Visible = false;
            _portalSideMenu.HorizontalScroll.Maximum = 0;
            _portalSideMenu.VerticalScroll.Enabled   = true;
            _portalSideMenu.VerticalScroll.Visible   = false;

            // Right-edge shadow
            _portalSideMenu.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(200, 200, 200), 1))
                    e.Graphics.DrawLine(pen,
                        _portalSideMenu.Width - 1, 0,
                        _portalSideMenu.Width - 1, _portalSideMenu.Height);
            };

            PopulatePortalSideMenu();

            this.Controls.Add(_portalSideMenu);
            _portalSideMenu.BringToFront();
        }

        private void PopulatePortalSideMenu()
        {
            // Controls added in BOTTOM-TO-TOP visual order (DockStyle.Top — last added = top).
            const int btnH = 50;

            // ── LOGOUT (bottom) ───────────────────────────────────────────────
            var btnLogout = new Button
            {
                Text      = "🚪  Logout",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                Height    = btnH,
                Dock      = DockStyle.Top,
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(10, 0, 0, 0)
            };
            btnLogout.FlatAppearance.BorderSize         = 0;
            btnLogout.FlatAppearance.MouseOverBackColor = Color.FromArgb(176, 0, 32);
            btnLogout.Click += (s, e) =>
            {
                TogglePortalMenu();
                LogoutRequested = true;
                this.Close();
            };
            _portalSideMenu.Controls.Add(btnLogout);

            _portalSideMenu.Controls.Add(new Panel { Height = 8, Dock = DockStyle.Top, BackColor = Color.White });

            // ── NOTIFICATION SETTINGS ─────────────────────────────────────────
            var btnNotifSettings = new Button
            {
                Text      = "🔔  Notification Settings",
                Font      = new Font("Segoe UI", 10F),
                Height    = btnH,
                Dock      = DockStyle.Top,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(10, 0, 0, 0)
            };
            btnNotifSettings.FlatAppearance.BorderSize         = 0;
            btnNotifSettings.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 240, 240);
            btnNotifSettings.Click += (s, e) =>
            {
                TogglePortalMenu();
                var win = new NotificationSettingsWindow();
                win.SettingsSaved += () => BeginInvoke(new Action(RefreshNotificationBadge));
                win.ShowDialog();
            };
            _portalSideMenu.Controls.Add(btnNotifSettings);

            // ── BACK TO PORTAL (Yakult Internal Systems) ──────────────────────
            var btnBackToSystem = new Button
            {
                Text      = "↩  Back to Portal",
                Font      = new Font("Segoe UI", 10F),
                Height    = btnH,
                Dock      = DockStyle.Top,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(10, 0, 0, 0)
            };
            btnBackToSystem.FlatAppearance.BorderSize         = 0;
            btnBackToSystem.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 240, 240);
            btnBackToSystem.Click += (s, e) =>
            {
                TogglePortalMenu();
                var dashboard = Application.OpenForms["MainDashboardForm"];
                this.Close();
                if (dashboard != null && !dashboard.IsDisposed)
                {
                    if (dashboard.WindowState == FormWindowState.Minimized)
                        dashboard.WindowState = FormWindowState.Normal;
                    dashboard.BringToFront();
                }
            };
            _portalSideMenu.Controls.Add(btnBackToSystem);

            // ── HOME ──────────────────────────────────────────────────────────
            var btnHome = new Button
            {
                Text      = "🏠  Home",
                Font      = new Font("Segoe UI", 10F),
                Height    = btnH,
                Dock      = DockStyle.Top,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(10, 0, 0, 0)
            };
            btnHome.FlatAppearance.BorderSize         = 0;
            btnHome.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 240, 240);
            btnHome.Click += (s, e) =>
            {
                TogglePortalMenu();
                if (AppSession.IsApprover && _pnlLanding != null)
                    ShowLanding();
                else if (tabControl != null && tabNewRequest != null)
                    tabControl.SelectedTab = tabNewRequest;
            };
            _portalSideMenu.Controls.Add(btnHome);

            // ── SEPARATOR ─────────────────────────────────────────────────────
            _portalSideMenu.Controls.Add(new Panel
            {
                Height    = 1,
                Dock      = DockStyle.Top,
                BackColor = Color.FromArgb(230, 230, 230)
            });

            _portalSideMenu.Controls.Add(new Panel { Height = 10, Dock = DockStyle.Top, BackColor = Color.White });

            // ── HEADER LABEL (top) ────────────────────────────────────────────
            var lblMenuHeader = new Label
            {
                Text      = "REQUESTER PORTAL",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Height    = 40,
                AutoSize  = false,
                Dock      = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(16, 0, 0, 0)
            };
            _portalSideMenu.Controls.Add(lblMenuHeader);

            _portalSideMenu.Controls.Add(new Panel { Height = 16, Dock = DockStyle.Top, BackColor = Color.White });
        }

        private void TogglePortalMenu()
        {
            _portalMenuOpen = !_portalMenuOpen;

            if (_portalMenuOpen)
            {
                // Show overlay first, then bring content on top of it (same pattern as MainForm.ToggleMenu)
                _portalMenuOverlay.Visible = true;
                _portalMenuOverlay.BringToFront();

                // Keep main content visible on top of the overlay
                if (tabControl != null)       tabControl.BringToFront();
                if (_pnlLanding != null)      _pnlLanding.BringToFront();
                if (_pnlApproverArea != null) _pnlApproverArea.BringToFront();
                if (_pnlRequestArea != null)  _pnlRequestArea.BringToFront();

                // Side menu on top of everything
                _portalSideMenu.Visible = true;
                _portalSideMenu.BringToFront();
            }
            else
            {
                _portalSideMenu.Visible    = false;
                _portalMenuOverlay.Visible = false;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Form initialisation
        // ══════════════════════════════════════════════════════════════════════

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.AutoScaleMode = AutoScaleMode.Font;
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Requester Portal - Consumable Requests";
            this.BackColor = Color.FromArgb(245, 246, 250);
            this.Font = new Font("Segoe UI", 9F);
            this.MinimumSize = new Size(1200, 800);

            // Header
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.FromArgb(78, 154, 252),
                Padding = new Padding(20, 0, 20, 0)
            };

            var logoBox = new PictureBox
            {
                Size = new Size(220, 46),
                Location = new Point(85, 12),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            try
            {
                string logoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "yakult_Name.png");
                if (System.IO.File.Exists(logoPath))
                    logoBox.Image = Image.FromFile(logoPath);
            }
            catch { }
            pnlHeader.Controls.Add(logoBox);

            var lblTitle = new Label
            {
                Text = "Consumable Request Portal",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(315, 14)
            };

            pnlHeader.Controls.Add(lblTitle);

            var btnBackToPortal = new Button
            {
                Text      = "To Portal",
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(41, 128, 185),
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(130, 36),
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                Cursor    = Cursors.Hand
            };
            btnBackToPortal.FlatAppearance.BorderSize = 0;
            btnBackToPortal.Click += (s, e) =>
            {
                // Restore and focus the dashboard before closing this window
                var dashboard = Application.OpenForms["MainDashboardForm"];
                this.Close();
                if (dashboard != null && !dashboard.IsDisposed)
                {
                    if (dashboard.WindowState == FormWindowState.Minimized)
                        dashboard.WindowState = FormWindowState.Normal;
                    dashboard.BringToFront();
                }
            };
            // ── Notification bell ───────────────────────────────────────────────────
            // ── WPF notification bell (ElementHost) ────────────────────────────
            _notifBell = new NotificationBellControl();
            _notifBell.BellClicked += (s, e) => OpenNotificationPanel(_notifBellHost);

            _notifBellHost = new ElementHost
            {
                Child     = _notifBell,
                Size      = new Size(46, 46),
                BackColor = Color.FromArgb(78, 154, 252),  // match header colour
            };

            // Help (?) button — launches the guided tour for the current tab
            _btnHelp = new Button
            {
                Text      = string.Empty,
                Font      = new Font("Segoe UI", 16F, FontStyle.Bold),
                Size      = new Size(40, 40),
                BackColor = Color.FromArgb(78, 154, 252),  // same as header — base for alpha blending
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnHelp.FlatAppearance.BorderSize = 0;

            // Circular clip region
            var helpCirclePath = new System.Drawing.Drawing2D.GraphicsPath();
            helpCirclePath.AddEllipse(0, 0, 39, 39);
            _btnHelp.Region = new System.Drawing.Region(helpCirclePath);
            helpCirclePath.Dispose();

            // Track hover/press for alpha variation in Paint
            bool _helpHovered = false;
            bool _helpPressed = false;

            // Smooth antialiased circle — matches MainDashboardShell style
            _btnHelp.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                pe.Graphics.PixelOffsetMode   = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                pe.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                // Higher alpha needed on the medium-blue portal header to match the
                // visual contrast the dashboard achieves on its dark-navy background.
                int alpha = _helpPressed ? 140 : _helpHovered ? 110 : 80;
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                using (var fill = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
                {
                    path.AddEllipse(0, 0, _btnHelp.Width - 1, _btnHelp.Height - 1);
                    pe.Graphics.FillPath(fill, path);
                }
                using (var sf = new System.Drawing.StringFormat
                {
                    Alignment     = System.Drawing.StringAlignment.Center,
                    LineAlignment = System.Drawing.StringAlignment.Center
                })
                {
                    pe.Graphics.DrawString("?", _btnHelp.Font, Brushes.White,
                        new RectangleF(0, 0, _btnHelp.Width, _btnHelp.Height), sf);
                }
            };

            _btnHelp.MouseEnter += (s, e) => { _helpHovered = true;  _helpPressed = false; _btnHelp.Invalidate(); };
            _btnHelp.MouseLeave += (s, e) => { _helpHovered = false; _helpPressed = false; _btnHelp.Invalidate(); };
            _btnHelp.MouseDown  += (s, e) => { _helpPressed = true;  _btnHelp.Invalidate(); };
            _btnHelp.MouseUp    += (s, e) => { _helpPressed = false; _btnHelp.Invalidate(); };

            new ToolTip().SetToolTip(_btnHelp, "User Guide — click to start the guided tour");
            _btnHelp.Click += (s, e) =>
            {
                if (AppSession.IsApprover && _pnlLanding?.Visible == true)
                    _tourService?.StartTour(WpfPortalTourService.TourSection.ApproverHomepage);
                else if (AppSession.IsApprover && _pnlApproverArea?.Visible == true)
                    _tourService?.StartTour(WpfPortalTourService.TourSection.ApprovalQueue);
                else
                    _tourService?.StartTourForCurrentTab(tabControl, tabNewRequest, tabAssistedRequest, tabMyRequests, tabAuthHistory);
            };
            pnlHeader.Controls.Add(_btnHelp);

            // Hamburger button — left side of header, same style as MainForm
            var btnHamburger = new Button
            {
                Text      = "☰",
                Font      = new Font("Segoe UI", 24F, FontStyle.Bold),
                Size      = new Size(70, 60),
                Location  = new Point(5, 5),
                BackColor = Color.FromArgb(58, 134, 232),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            btnHamburger.FlatAppearance.BorderSize         = 0;
            btnHamburger.FlatAppearance.MouseOverBackColor = Color.FromArgb(78, 154, 252);
            btnHamburger.Click += (s, e) => TogglePortalMenu();
            pnlHeader.Controls.Add(btnHamburger);

            pnlHeader.Controls.Add(btnBackToPortal);
            pnlHeader.Controls.Add(_notifBellHost);

            // Position right-side buttons — update on resize
            void PositionBackButton()
            {
                btnBackToPortal.Location = new Point(
                    pnlHeader.Width - btnBackToPortal.Width - 20,
                    (pnlHeader.Height - btnBackToPortal.Height) / 2);
                _notifBellHost.Location = new Point(
                    btnBackToPortal.Left - _notifBellHost.Width - 8,
                    (pnlHeader.Height - _notifBellHost.Height) / 2);
                if (_btnHelp != null)
                    _btnHelp.Location = new Point(
                        _notifBellHost.Left - _btnHelp.Width - 8,
                        (pnlHeader.Height - _btnHelp.Height) / 2);
            }
            PositionBackButton();
            pnlHeader.Resize += (s, e) => PositionBackButton();


            // Tab control
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F),
                Padding = new Point(20, 8)
            };

            tabNewRequest = new TabPage("New Request");
            tabMyRequests = new TabPage("Request History");

            tabControl.TabPages.Add(tabNewRequest);

            if (AppSession.IsITStaff)
            {
                tabAssistedRequest = new TabPage("⚡ Assisted Request");
                tabAssistedRequest.BackColor = Color.FromArgb(235, 244, 255);
                tabControl.TabPages.Add(tabAssistedRequest);
            }

            tabControl.TabPages.Add(tabMyRequests);

            tabAuthHistory = new TabPage("Authorization History");
            tabControl.TabPages.Add(tabAuthHistory);

            InitializeSelfRequestTab();
            if (AppSession.IsITStaff)
                InitializeAssistedRequestTab();
            InitializeMyRequestsTab();
            InitializeAuthHistoryTab();

            // Refresh model availability whenever the user navigates back to a request tab
            // so the count reflects any fulfillments that occurred while the form was open.
            tabControl.SelectedIndexChanged += (s, e) =>
            {
                // Close any active tour overlay — it belongs to a specific tab.
                _tourService?.StopTour();
                var selected = tabControl.SelectedTab;
                if (selected == tabNewRequest || selected == tabAssistedRequest)
                    RefreshCartridgeModelDropdowns();
            };

            if (AppSession.IsApprover)
            {
                _pnlLanding      = BuildLandingPanel();
                _pnlApproverArea = BuildApproverAreaPanel();
                _pnlRequestArea  = BuildRequestAreaPanel();

                // Fill panels overlap — only the visible one displays at a time
                this.Controls.Add(_pnlRequestArea);
                this.Controls.Add(_pnlApproverArea);
                this.Controls.Add(_pnlLanding);

                // Show the landing page first on load
                ShowLanding();
            }
            else
            {
                this.Controls.Add(tabControl);
            }
            this.Controls.Add(pnlHeader);

            this.ResumeLayout(false);

            // Refresh badge on load; start badge polling timer and desktop notification poller
            this.Load += (s, e) =>
            {
                RefreshNotificationBadge();

                // Refresh badge every 30 s without user interaction
                _badgeTimer = new Timer { Interval = 30_000 };
                _badgeTimer.Tick += (ts, te) => RefreshNotificationBadge();
                _badgeTimer.Start();

                _notifPoller = new Services.DesktopNotificationPoller();

                _notifPoller.NewNotificationsArrived += notifications =>
                    BeginInvoke(new Action(() => OnNewNotificationsArrived(notifications)));

                // System tray presence (icon, context menu, show/exit) — independent of the
                // notification popup, which is handled entirely by ToastNotificationService.
                Services.TrayIconService.Initialize(this);

                // Guided tour — initialise service and auto-launch on first visit
                _tourService = new WpfPortalTourService();
                _tourService.SetViews(
                    _newRequestViewRef,
                    _requestHistoryViewRef,
                    _authHistoryViewRef,
                    _notifBellHost,
                    tabControl,
                    _approvalPage,
                    _landingCardAuthorize,
                    _landingCardSubmit,
                    _landingCardHistory,
                    _assistedRequestViewRef);

                // Slight delay so the WPF views complete their layout pass before the tour tries
                // to measure element positions.
                var tourTimer = new Timer { Interval = 800 };
                tourTimer.Tick += (tt, te) =>
                {
                    tourTimer.Stop();
                    tourTimer.Dispose();
                    _tourService.LaunchIfFirstTime();
                };
                tourTimer.Start();

                // Hide the tour overlay when Yakult.Inventory.App loses focus to another
                // application (Alt+Tab, minimize), and restore it when the app regains focus.
                // SuppressHostEvents is true while the overlay itself is taking or releasing
                // focus — those internal transitions must not trigger suspend/resume.
                this.Deactivate += (ds, de) =>
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Portal] Deactivate  suppress={_tourService?.SuppressHostEvents}  tourActive={_tourService?.IsActive}");
                    if (_tourService?.SuppressHostEvents == true) return;
                    _tourService?.SuspendTour();
                };
                this.Activated += (as_, ae) =>
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Portal] Activated  suppress={_tourService?.SuppressHostEvents}  tourActive={_tourService?.IsActive}");
                    if (_tourService?.SuppressHostEvents == true) return;
                    _tourService?.ResumeTour();
                };
            };
            this.FormClosing += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Portal] FormClosing  CloseReason={e.CloseReason}  cancel={e.Cancel}");
                System.Diagnostics.Debug.WriteLine(
                    $"[Portal] FormClosing StackTrace:\n{new System.Diagnostics.StackTrace(true)}");
            };
            this.FormClosed += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[Portal] FormClosed  CloseReason={e.CloseReason}");
                _badgeTimer?.Stop();
                _badgeTimer?.Dispose();
                _badgeTimer = null;
                _notifPoller?.Dispose();
                _notifPoller = null;
                _tourService?.Dispose();
                _tourService = null;
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        // Notification bell helpers
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshNotificationBadge()
        {
            bool silent = !AppSession.NotificationsEnabled;

            // When silenced, hide badge but still update the silent indicator.
            if (silent)
            {
                _notifUnreadCount = 0;
                if (_notifBell != null)
                    _notifBell.Dispatcher.Invoke(() =>
                    {
                        _notifBell.UnreadCount = 0;
                        _notifBell.IsSilent    = true;
                    });
                return;
            }

            int count = 0;
            try
            {
                var repo = new Yakult.Inventory.App.Repositories.NotificationRepository();
                // Only count this portal's own rows — dbo.Notification is shared with the Repair
                // Portal and others; ownership is the PortalId FK.
                count    = repo.GetUnreadCountByPortal(
                    AppSession.CurrentUserId, Models.NotificationType.RequesterPortalKey);
            }
            catch { /* badge is non-critical */ }

            _notifUnreadCount = count;

            if (_notifBell != null)
                _notifBell.Dispatcher.Invoke(() =>
                {
                    _notifBell.UnreadCount = count;
                    _notifBell.IsSilent    = false;
                });
        }

        private void OpenNotificationPanel(System.Windows.Forms.Control anchor)
        {
            // If the popup was just closed by the Deactivate event that fired when
            // the user clicked this same bell button, don't reopen it immediately.
            if ((DateTime.Now - _notifPopupClosedAt).TotalMilliseconds < 300)
                return;

            // Toggle: clicking the bell while the panel is open closes it
            if (_notifPopup != null && !_notifPopup.IsDisposed)
            {
                _notifPopup.Close();
                return;
            }

            try
            {
                var repo          = new Yakult.Inventory.App.Repositories.NotificationRepository();
                // Only this portal's own rows — dbo.Notification is shared.
                var notifications = repo.GetByUserAndPortal(
                    AppSession.CurrentUserId, Models.NotificationType.RequesterPortalKey, 50);

                // Position: below and right-aligned to the host control
                var screenPt = anchor.PointToScreen(Point.Empty);
                const int popupW = 500;
                int x = screenPt.X + anchor.Width - popupW;
                int y = screenPt.Y + anchor.Height + 4;

                // Clamp to screen so the panel never goes off-edge
                var screen = Screen.FromPoint(screenPt);
                x = Math.Max(screen.WorkingArea.Left + 8,
                    Math.Min(x, screen.WorkingArea.Right - popupW - 8));
                y = Math.Max(screen.WorkingArea.Top + 8, y);

                _notifPopup = new NotificationDropdownForm(
                    notifications,
                    AppSession.CurrentUserId,
                    n => BeginInvoke(new Action(() => OnNotificationNavigate(n))),
                    Models.NotificationType.RequesterPortalKey);

                _notifPopup.Location = new Point(x, y);

                // When the popup closes for any reason, clear the reference and refresh badge
                _notifPopup.FormClosed += (s, e) =>
                {
                    _notifPopupClosedAt = DateTime.Now;
                    _notifPopup         = null;
                    RefreshNotificationBadge();
                };

                // Non-modal: Show(owner) keeps parent active so click-away works via Deactivate
                _notifPopup.Show(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load notifications: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnNewNotificationsArrived(IReadOnlyList<NotificationDto> notifications)
        {
            if (!AppSession.NotificationsEnabled) return;

            // Refresh the bell badge so the unread count updates immediately.
            RefreshNotificationBadge();

            // One ToastNotifications popup per new notification — this is now the only
            // notification popup mechanism (see ToastNotificationService for why).
            foreach (var n in notifications)
            {
                switch (n.NotificationType)
                {
                    case Models.NotificationType.AuthorizationApproved:
                    case Models.NotificationType.RequestApproved:
                    case Models.NotificationType.RequestFulfilled:
                        Services.ToastNotificationService.Instance.ShowSuccess(n.Title, n.Message);
                        break;

                    case Models.NotificationType.AuthorizationRejected:
                    case Models.NotificationType.RequestRejected:
                    case Models.NotificationType.RequestUnfulfilled:
                        Services.ToastNotificationService.Instance.ShowError(n.Title, n.Message);
                        break;

                    case Models.NotificationType.RequestPartiallyFulfilled:
                        Services.ToastNotificationService.Instance.ShowWarning(n.Title, n.Message);
                        break;

                    default:
                        Services.ToastNotificationService.Instance.ShowInfo(n.Title, n.Message);
                        break;
                }
            }
        }

        private void NavigateToAuthorizationDetail(int authorizationId)
        {
            // If the approver landing is showing, switch to the request area panel
            if (AppSession.IsApprover && _pnlRequestArea != null)
                ShowRequestArea();

            // Switch to the Authorization History tab
            if (tabControl != null && tabAuthHistory != null)
                tabControl.SelectedTab = tabAuthHistory;

            if (_authHistoryViewModel == null) return;

            if (_authHistoryViewModel.TotalCount > 0)
            {
                // Data already loaded — highlight immediately
                _authHistoryViewModel.NavigateToAndHighlight(authorizationId);
            }
            else
            {
                // Data still loading (LoadAsync triggered by tab Loaded event) —
                // store the id; LoadAsync will apply the highlight once it finishes
                _authHistoryViewModel.SetPendingHighlight(authorizationId);
            }
        }

        private void OnNotificationNavigate(Models.NotificationDto n)
        {
            if (!n.ReferenceId.HasValue) return;

            switch (n.NotificationType)
            {
                case Models.NotificationType.AuthorizationApproved:
                case Models.NotificationType.AuthorizationRejected:
                    NavigateToAuthorizationDetail(n.ReferenceId.Value);
                    break;

                default:
                    NavigateToRequestDetail(n.ReferenceId.Value);
                    break;
            }
        }

        private void NavigateToRequestDetail(int authorizationId)
        {
            var repo    = new Repositories.NotificationRepository();
            string code = repo.GetSetCodeByAuthorizationId(authorizationId);
            if (string.IsNullOrWhiteSpace(code)) return;

            if (AppSession.IsApprover && _pnlRequestArea != null)
                ShowRequestArea();

            if (tabControl != null && tabMyRequests != null)
                tabControl.SelectedTab = tabMyRequests;

            if (_requestHistoryViewModel == null) return;

            if (_requestHistoryViewModel.TotalCount > 0)
                _requestHistoryViewModel.NavigateToAndHighlight(code);
            else
                _requestHistoryViewModel.SetPendingHighlight(code);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Self-Request Tab  (identity locked to session — all users)
        // ──────────────────────────────────────────────────────────────────────

        private void InitializeSelfRequestTab()
        {
            _newRequestViewModel = new NewRequestViewModel();
            _newRequestViewModel.RequestSubmitted += OnNewRequestSubmitted;

            _newRequestViewRef = new NewRequestView(_newRequestViewModel);
            var host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _newRequestViewRef
            };
            tabNewRequest.Controls.Add(host);
        }

        private void OnNewRequestSubmitted(List<int> createdIds, bool wasAutoApproved, int newAuthId)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => OnNewRequestSubmitted(createdIds, wasAutoApproved, newAuthId))); return; }

            LoadMyRequests();

            bool isSelfSign = AppSession.IsApprover && !wasAutoApproved && newAuthId > 0;
            if (isSelfSign)
            {
                MessageBox.Show(
                    "Request submitted successfully!\n\n" +
                    "As an approver, you are required to sign and authorize your own request.\n\n" +
                    "You will now be taken to the Authorization page.",
                    "Authorization Required", MessageBoxButtons.OK, MessageBoxIcon.Information);

                ShowApproverArea();
                _ = _approvalPage.LoadAndSelectAsync(newAuthId);
            }
            else
            {
                MessageBox.Show(
                    $"Request submitted successfully!\n\n" +
                    $"Created {createdIds.Count} request(s).\n\n" +
                    (wasAutoApproved
                        ? "Your request has been auto-approved."
                        : "Your request is awaiting supervisor authorization before it will be processed."),
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                tabControl.SelectedTab = tabMyRequests;
                if (AppSession.IsApprover) ShowRequestArea();
            }
        }

        // Dead method — preserved only to keep old field-initialisation code from breaking compile.
        // Not called anywhere. Body is the original InitializeSelfRequestTab implementation.
        private void _InitializeSelfRequestTab_Legacy()
        {
            var scrollPanel = new Panel();
            var cardPanel = new ReaLTaiizor.Controls.Panel
            {
                BackColor = Color.White,
                EdgeColor = Color.FromArgb(220, 220, 220),
                Width = 950,
                AutoSize = false,
                Padding = new Padding(40),
                SmoothingType = SmoothingMode.HighQuality,
                Top = 20,
                Left = 30
            };

            int yPos = 20;
            const int labelWidth = 200;
            const int controlWidth = 650;
            const int spacing = 55;

            // ── Identity section ───────────────────────────────────────────────
            var lblSectionIdentity = new Label
            {
                Text = "Request Details",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Location = new Point(20, yPos),
                AutoSize = true
            };
            yPos += 40;

            if (AppSession.IsDepartmentAccountSession)
            {
                // Department account mode: show employee selection dropdown
                var lblSelectEmp = new Label
                {
                    Text = "Select Employee:",
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(60, 60, 60),
                    Location = new Point(20, yPos),
                    AutoSize = true
                };
                yPos += 28;

                cmbDeptEmployee = new ComboBox
                {
                    Location = new Point(20, yPos),
                    Width = 870,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new Font("Segoe UI", 10F)
                };
                cmbDeptEmployee.DisplayMember = "DisplayName";
                cmbDeptEmployee.SelectedIndexChanged += CmbDeptEmployee_SelectedIndexChanged;
                yPos += 38;

                lblDeptEmpInfo = new Label
                {
                    Location = new Point(20, yPos),
                    Size = new Size(870, 50),
                    BackColor = Color.FromArgb(248, 249, 250),
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font("Segoe UI", 9.5F),
                    ForeColor = Color.FromArgb(80, 80, 80),
                    Text = "— Select an employee above —",
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 0, 0)
                };
                yPos += 65;

                cardPanel.Controls.Add(lblSelectEmp);
                cardPanel.Controls.Add(cmbDeptEmployee);
                cardPanel.Controls.Add(lblDeptEmpInfo);
            }
            else
            {
                // Normal mode: read-only identity from session
                string empName    = AppSession.CurrentEmployeeName     ?? AppSession.CurrentUserName ?? "(Unknown)";
                string empPos     = AppSession.CurrentEmployeePosition ?? "—";
                string compName   = AppSession.CurrentCompanyName      ?? "—";
                string branchName = AppSession.CurrentBranchName       ?? "—";
                string deptName   = AppSession.CurrentDepartmentName   ?? "—";

                var pnlIdentity = new Panel
                {
                    Location = new Point(20, yPos),
                    Width = 870,
                    Height = 80,
                    BackColor = Color.FromArgb(248, 249, 250),
                    BorderStyle = BorderStyle.FixedSingle
                };

                var lblIdentityName = new Label
                {
                    Text = $"👤  Requesting for: {empName}   |   {empPos}",
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(33, 37, 41),
                    Location = new Point(12, 10),
                    AutoSize = true
                };

                var lblIdentityOrg = new Label
                {
                    Text = $"🏢  {compName}   |   {branchName}   |   {deptName}",
                    Font = new Font("Segoe UI", 9.5F),
                    ForeColor = Color.FromArgb(80, 80, 80),
                    Location = new Point(12, 38),
                    AutoSize = true
                };

                pnlIdentity.Controls.Add(lblIdentityName);
                pnlIdentity.Controls.Add(lblIdentityOrg);

                if (!AppSession.CurrentEmployeeId.HasValue)
                {
                    pnlIdentity.Height = 100;
                    var lblNoEmp = new Label
                    {
                        Text = "⚠  No employee profile linked to your account. Contact IT to link your account.",
                        Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                        ForeColor = Color.FromArgb(220, 53, 69),
                        Location = new Point(12, 62),
                        AutoSize = true
                    };
                    pnlIdentity.Controls.Add(lblNoEmp);
                }

                yPos += pnlIdentity.Height + 20;
                cardPanel.Controls.Add(pnlIdentity);
            }

            // ── Cartridge selection ────────────────────────────────────────────
            var lblCartridgeHeader = new Label
            {
                Text = "Cartridge Selection",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Location = new Point(20, yPos),
                AutoSize = true
            };
            yPos += 40;

            var lblCartridgeModel = CreateLabel("Cartridge Model:", yPos);
            lblCartridgeModel.Width = labelWidth;
            cmbCartridgeModel = new ComboBox
            {
                Location = new Point(labelWidth + 20, yPos - 3),
                Width = controlWidth,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F)
            };
            cmbCartridgeModel.SelectedIndexChanged += CmbCartridgeModel_SelectedIndexChanged;
            cmbCartridgeModel.MouseWheel += (s, ev) => { if (!cmbCartridgeModel.Focused) ((HandledMouseEventArgs)ev).Handled = true; };

            lblNoStockWarning = new Label
            {
                Location = new Point(labelWidth + 20, cmbCartridgeModel.Bottom + 2),
                Width = controlWidth,
                Height = 34,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(78, 154, 252),
                Text = "Sorry, there is no available stock for the selected model. You may still submit a request and the IT Department will process it once stock becomes available.",
                Visible = false
            };
            yPos += spacing;

            var lblQuantity = CreateLabel("Qty Requested:", yPos);
            lblQuantity.Width = labelWidth;
            nudQuantity = new NumericUpDown
            {
                Location = new Point(labelWidth + 20, yPos - 3),
                Width = 150,
                Minimum = 0,
                Maximum = 3,
                Value = 1,
                Font = new Font("Segoe UI", 10F)
            };
            yPos += spacing;

            var lblConditionHeader = new Label
            {
                Text = "Returned Empty Cartridges (Condition)",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Location = new Point(20, yPos),
                AutoSize = true
            };
            yPos += 35;

            var lblGoodQty = CreateLabel("Good:", yPos);
            lblGoodQty.Width = 70;
            nudGoodQty = new NumericUpDown
            {
                Location = new Point(95, yPos - 3),
                Width = 120,
                Minimum = 0,
                Maximum = 3,
                Value = 0,
                Font = new Font("Segoe UI", 10F)
            };
            nudGoodQty.ValueChanged += (s, e) =>
            {
                int cap = (int)nudQuantity.Maximum;
                nudDamagedQty.Maximum = Math.Max(0, cap - (int)nudGoodQty.Value);
            };
            var lblDamagedInline = new Label
            {
                Text = "Damaged:",
                Location = new Point(230, yPos),
                AutoSize = false,
                Width = 115,
                Height = 30,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60)
            };
            nudDamagedQty = new NumericUpDown
            {
                Location = new Point(355, yPos - 3),
                Width = 120,
                Minimum = 0,
                Maximum = 3,
                Value = 0,
                Font = new Font("Segoe UI", 10F)
            };
            nudDamagedQty.ValueChanged += (s, e) =>
            {
                int cap = (int)nudQuantity.Maximum;
                nudGoodQty.Maximum = Math.Max(0, cap - (int)nudDamagedQty.Value);
            };
            yPos += spacing;

            // With Cartridge is always required per business rule — field hidden, value hardcoded at submission
            var lblTypeRow = CreateLabel("Cartridge Type:", yPos);
            lblTypeRow.Width = labelWidth;
            lblTypeRow.Visible = false;
            var gbType = new GroupBox
            {
                Location = new Point(labelWidth + 20, yPos - 10),
                Width = 380,
                Height = 50,
                FlatStyle = FlatStyle.Flat,
                Visible = false
            };
            rbWithCartridge = new RadioButton
            {
                Text = "With Cartridge",
                Location = new Point(10, 18),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9.5F)
            };
            rbWithoutCartridge = new RadioButton
            {
                Text = "Without Cartridge",
                Location = new Point(175, 18),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F)
            };
            gbType.Controls.Add(rbWithCartridge);
            gbType.Controls.Add(rbWithoutCartridge);

            btnAddItem = new Button
            {
                Text = "+ Add to Request",
                Location = new Point(labelWidth + 20, yPos),
                Width = 185,
                Height = 38,
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAddItem.FlatAppearance.BorderSize = 0;
            btnAddItem.Click += BtnAddItem_Click;
            yPos += 55;

            var lblItems = new Label
            {
                Text = "Request Items:",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Location = new Point(20, yPos),
                AutoSize = true
            };
            yPos += 40;

            dgvRequestItems = new DataGridView
            {
                Location = new Point(20, yPos),
                Width = 870,
                Height = 150,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                Font = new Font("Segoe UI", 9F),
                RowTemplate = { Height = 32 }
            };
            dgvRequestItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "CartridgeModel", HeaderText = "Cartridge Model", Width = 280 });
            dgvRequestItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity",        HeaderText = "Qty Requested",   Width = 100 });
            dgvRequestItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "GoodQty",         HeaderText = "Returned Cartridges (Good)",    Width = 160 });
            dgvRequestItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "DamagedQty",      HeaderText = "Returned Cartridges (Damaged)", Width = 180 });
            dgvRequestItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "CartridgeType",   HeaderText = "Type",            Width = 140 });
            dgvRequestItems.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(78, 154, 252);
            dgvRequestItems.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvRequestItems.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvRequestItems.ColumnHeadersHeight = 38;
            dgvRequestItems.EnableHeadersVisualStyles = false;
            yPos += 165;

            btnRemoveItem = new Button
            {
                Text = "Remove Selected",
                Location = new Point(20, yPos),
                Width = 150,
                Height = 35,
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRemoveItem.FlatAppearance.BorderSize = 0;
            btnRemoveItem.Click += BtnRemoveItem_Click;
            yPos += 60;

            // ── Fulfillment ────────────────────────────────────────────────────
            var lblFulfillmentHeader = new Label
            {
                Text = "Fulfillment & Delivery",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Location = new Point(20, yPos),
                AutoSize = true
            };
            yPos += 45;

            var lblFulfillment = CreateLabel("Fulfillment Method:", yPos);
            lblFulfillment.Width = labelWidth;
            var gbFulfillment = new GroupBox
            {
                Location = new Point(labelWidth + 20, yPos - 10),
                Width = 400,
                Height = 50,
                FlatStyle = FlatStyle.Flat
            };
            rbPickup = new RadioButton
            {
                Text = "PICKUP",
                Location = new Point(10, 18),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9.5F)
            };
            rbDelivery = new RadioButton
            {
                Text = "DELIVERY",
                Location = new Point(150, 18),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F)
            };
            gbFulfillment.Controls.Add(rbPickup);
            gbFulfillment.Controls.Add(rbDelivery);
            yPos += 75;

            lblReceivedBy = CreateLabel("To Be Received By:", yPos);
            lblReceivedBy.Width = labelWidth;
            cmbReceivedBy = new ComboBox
            {
                Location = new Point(labelWidth + 20, yPos - 3),
                Width = controlWidth,
                DropDownStyle = ComboBoxStyle.DropDown,
                Font = new Font("Segoe UI", 10F)
            };
            ConfigureSearchableComboBox(cmbReceivedBy);
            rbPickup.CheckedChanged   += (s, e) => { lblReceivedBy.Visible = cmbReceivedBy.Visible = rbPickup.Checked; };
            rbDelivery.CheckedChanged += (s, e) => { lblReceivedBy.Visible = cmbReceivedBy.Visible = rbPickup.Checked; };
            yPos += spacing;

            var lblRemarks = CreateLabel("Additional Remarks:", yPos);
            lblRemarks.Width = labelWidth;
            txtRemarks = new TextBox
            {
                Location = new Point(labelWidth + 20, yPos - 3),
                Width = controlWidth,
                Height = 70,
                Multiline = true,
                Font = new Font("Segoe UI", 10F),
                ScrollBars = ScrollBars.Vertical
            };
            yPos += 90;

            // Buttons
            var flowButtons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Location = new Point(labelWidth + 20, yPos),
                Width = controlWidth,
                Height = 50,
                WrapContents = false
            };

            btnClear = new Button
            {
                Text = "Clear",
                Width = 130,
                Height = 44,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(5, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += BtnClear_Click;

            btnSubmit = new Button
            {
                Text = "Submit Request",
                Width = 160,
                Height = 44,
                BackColor = Color.FromArgb(78, 154, 252),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(10, 0, 0, 0)
            };
            btnSubmit.FlatAppearance.BorderSize = 0;
            btnSubmit.Click += BtnSelfSubmit_Click;

            flowButtons.Controls.Add(btnClear);
            flowButtons.Controls.Add(btnSubmit);

            cardPanel.Height = yPos + 40;

            cardPanel.Controls.Add(lblSectionIdentity);
            // identity controls (pnlIdentity or dept-account dropdown) are added inside InitializeSelfRequestTab conditionally
            cardPanel.Controls.Add(lblCartridgeHeader);
            cardPanel.Controls.Add(lblCartridgeModel);
            cardPanel.Controls.Add(cmbCartridgeModel);
            cardPanel.Controls.Add(lblNoStockWarning);
            cardPanel.Controls.Add(lblQuantity);
            cardPanel.Controls.Add(nudQuantity);
            cardPanel.Controls.Add(lblConditionHeader);
            cardPanel.Controls.Add(lblGoodQty);
            cardPanel.Controls.Add(nudGoodQty);
            cardPanel.Controls.Add(lblDamagedInline);
            cardPanel.Controls.Add(nudDamagedQty);
            cardPanel.Controls.Add(lblTypeRow);
            cardPanel.Controls.Add(gbType);
            cardPanel.Controls.Add(btnAddItem);
            cardPanel.Controls.Add(lblItems);
            cardPanel.Controls.Add(dgvRequestItems);
            cardPanel.Controls.Add(btnRemoveItem);
            cardPanel.Controls.Add(lblFulfillmentHeader);
            cardPanel.Controls.Add(lblFulfillment);
            cardPanel.Controls.Add(gbFulfillment);
            cardPanel.Controls.Add(lblReceivedBy);
            cardPanel.Controls.Add(cmbReceivedBy);
            cardPanel.Controls.Add(lblRemarks);
            cardPanel.Controls.Add(txtRemarks);
            cardPanel.Controls.Add(flowButtons);

            scrollPanel.Controls.Add(cardPanel);
            tabNewRequest.Controls.Add(scrollPanel);

            EventHandler centerCard = (s, e) =>
            {
                int w = scrollPanel.ClientSize.Width;
                cardPanel.Left = w > cardPanel.Width + 60 ? (w - cardPanel.Width) / 2 : 30;
            };
            tabNewRequest.Resize += centerCard;
            scrollPanel.Resize += centerCard;
            scrollPanel.ClientSizeChanged += centerCard;
            this.Load += (s, e) => centerCard(null, EventArgs.Empty);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Assisted-Request Tab  (IT staff only — amber visual treatment)
        // ──────────────────────────────────────────────────────────────────────

        private void InitializeAssistedRequestTab()
        {
            _assistedRequestViewModel = new AssistedRequestViewModel();
            _assistedRequestViewModel.RequestSubmitted += OnAssistedRequestSubmitted;
            _assistedRequestViewRef = new AssistedRequestView(_assistedRequestViewModel);
            var host = new ElementHost { Dock = DockStyle.Fill, Child = _assistedRequestViewRef };
            tabAssistedRequest.Controls.Add(host);
        }

        private void OnAssistedRequestSubmitted(int authId, string targetDescription, string decision, bool isCartridgeOnly, int? setId)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => OnAssistedRequestSubmitted(authId, targetDescription, decision, isCartridgeOnly, setId))); return; }
            _ = _requestHistoryViewModel?.LoadAsync();
            bool approved = string.Equals(decision, "Approved", StringComparison.OrdinalIgnoreCase);
            string detail = approved
                ? "Authorization recorded as Approved. The request is now Under Review."
                : "Authorization recorded as Rejected. The request remains Awaiting Authorization.";
            MessageBox.Show(
                $"Assisted request submitted successfully for {targetDescription}.\n\n{detail}",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            if (isCartridgeOnly)
            {
                var goToCartridgeMgmt = MessageBox.Show(
                    "This was a cartridge-only request. Go to Cartridge Management now?",
                    "Go to Cartridge Management?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (goToCartridgeMgmt == DialogResult.Yes)
                    OpenConsumableManagementAndClose(openCartridgeManagementDirectly: true);
            }
            else
            {
                var goToRequestSetMgmt = MessageBox.Show(
                    "This was a mixed-consumable request (Option 2). Go to Request & Set Management now?",
                    "Go to Request & Set Management?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (goToRequestSetMgmt == DialogResult.Yes)
                    OpenConsumableManagementAndClose(openRequestSetManagementDirectly: true, highlightSetId: setId);
            }
        }

        private void OpenConsumableManagementAndClose(
            bool openCartridgeManagementDirectly = false,
            bool openRequestSetManagementDirectly = false,
            int? highlightSetId = null)
        {
            var consumableMgmt = new ConsumableManagementPortalShell(
                openCartridgeManagementDirectly, openRequestSetManagementDirectly, highlightSetId);
            consumableMgmt.ShowDialog();
            if (consumableMgmt.LogoutRequested)
                LogoutRequested = true;

            // Whether the user logged out or just backed all the way out of the
            // Consumable Management Portal, close this form too — MainForm's
            // navigation loop (ShowPortalAndNavigate) always returns to
            // WpfMainDashboardShell (or triggers logout) once RequesterPortalForm
            // closes, so this preserves the normal top-level navigation flow
            // instead of stranding the user back on the Requester/Consumable
            // Request Portal.
            this.Close();
        }

        private void _InitializeAssistedRequestTab_Legacy()
        {
            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(235, 244, 255)
            };

            // Assisted Request banner
            var pnlBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Color.FromArgb(78, 154, 252)
            };
            pnlBanner.Controls.Add(new Label
            {
                Text = "⚡  ASSISTED REQUEST — IT Support Only",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 255, 255),
                AutoSize = true,
                Location = new Point(20, 8)
            });
            pnlBanner.Controls.Add(new Label
            {
                Text = $"Requested By: {AppSession.CurrentUserName}  —  Creating a request on behalf of the selected employee",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(220, 235, 255),
                AutoSize = true,
                Location = new Point(22, 38)
            });
            scrollPanel.Controls.Add(pnlBanner);

            var cardPanel = new ReaLTaiizor.Controls.Panel
            {
                BackColor = Color.White,
                EdgeColor = Color.FromArgb(78, 154, 252),
                Width = 950,
                AutoSize = false,
                Padding = new Padding(40),
                SmoothingType = SmoothingMode.HighQuality,
                Top = 85,
                Left = 30
            };

            int yPos = 20;
            const int labelWidth = 200;
            const int controlWidth = 650;
            const int spacing = 55;

            // ── Employee selector ──────────────────────────────────────────────
            var lblEmpHeader = new Label
            {
                Text = "Select Target Employee",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Location = new Point(20, yPos),
                AutoSize = true
            };
            yPos += 40;

            var lblEmployee = CreateLabel("Select Employee:", yPos);
            lblEmployee.Width = labelWidth;
            cmbAssistedEmployee = new ComboBox
            {
                Location = new Point(labelWidth + 20, yPos - 3),
                Width = controlWidth - 100,
                DropDownStyle = ComboBoxStyle.DropDown,
                Font = new Font("Segoe UI", 10F),
                DisplayMember = "DisplayName",
                AutoCompleteMode = AutoCompleteMode.None
            };
            cmbAssistedEmployee.SelectedIndexChanged += CmbAssistedEmployee_SelectedIndexChanged;
            ConfigureSearchableEmployeeComboBox(cmbAssistedEmployee);

            btnQuickAddEmployee = new Button
            {
                Text = "+ New",
                Location = new Point(labelWidth + 20 + (controlWidth - 100) + 10, yPos - 3),
                Width = 85,
                Height = cmbAssistedEmployee.PreferredHeight,
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnQuickAddEmployee.FlatAppearance.BorderSize = 0;
            btnQuickAddEmployee.Click += BtnQuickAddEmployee_Click;
            yPos += spacing;

            // ── Cartridge selection ────────────────────────────────────────────
            var lblCartridgeHeader = new Label
            {
                Text = "Cartridge Selection",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Location = new Point(20, yPos),
                AutoSize = true
            };
            yPos += 40;

            var lblCartridgeModel = CreateLabel("Cartridge Model:", yPos);
            lblCartridgeModel.Width = labelWidth;
            cmbAssistedCartridgeModel = new ComboBox
            {
                Location = new Point(labelWidth + 20, yPos - 3),
                Width = controlWidth,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F)
            };
            cmbAssistedCartridgeModel.SelectedIndexChanged += CmbAssistedCartridgeModel_SelectedIndexChanged;
            cmbAssistedCartridgeModel.MouseWheel += (s, ev) => { if (!cmbAssistedCartridgeModel.Focused) ((HandledMouseEventArgs)ev).Handled = true; };
            yPos += spacing;

            var lblQuantity = CreateLabel("Qty Requested:", yPos);
            lblQuantity.Width = labelWidth;
            nudAssistedQuantity = new NumericUpDown
            {
                Location = new Point(labelWidth + 20, yPos - 3),
                Width = 150,
                Minimum = 0,
                Maximum = 3,
                Value = 1,
                Font = new Font("Segoe UI", 10F)
            };
            yPos += spacing;

            var lblAssistedCondHeader = new Label
            {
                Text = "Returned Empty Cartridges (Condition)",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Location = new Point(20, yPos),
                AutoSize = true
            };
            yPos += 35;

            var lblGoodQty = CreateLabel("Good:", yPos);
            lblGoodQty.Width = 70;
            nudAssistedGoodQty = new NumericUpDown
            {
                Location = new Point(95, yPos - 3),
                Width = 120,
                Minimum = 0,
                Maximum = 3,
                Value = 0,
                Font = new Font("Segoe UI", 10F)
            };
            nudAssistedGoodQty.ValueChanged += (s, e) =>
            {
                int cap = (int)nudAssistedQuantity.Maximum;
                nudAssistedDamagedQty.Maximum = Math.Max(0, cap - (int)nudAssistedGoodQty.Value);
            };
            var lblDmgInline = new Label
            {
                Text = "Damaged:",
                Location = new Point(230, yPos),
                AutoSize = false,
                Width = 115,
                Height = 30,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60)
            };
            nudAssistedDamagedQty = new NumericUpDown
            {
                Location = new Point(355, yPos - 3),
                Width = 120,
                Minimum = 0,
                Maximum = 3,
                Value = 0,
                Font = new Font("Segoe UI", 10F)
            };
            nudAssistedDamagedQty.ValueChanged += (s, e) =>
            {
                int cap = (int)nudAssistedQuantity.Maximum;
                nudAssistedGoodQty.Maximum = Math.Max(0, cap - (int)nudAssistedDamagedQty.Value);
            };
            yPos += spacing;

            // With Cartridge is always required per business rule — field hidden, value hardcoded at submission
            var lblAssistedTypeRow = CreateLabel("Cartridge Type:", yPos);
            lblAssistedTypeRow.Width = labelWidth;
            lblAssistedTypeRow.Visible = false;
            var gbAssistedType = new GroupBox
            {
                Location = new Point(labelWidth + 20, yPos - 10),
                Width = 380,
                Height = 50,
                FlatStyle = FlatStyle.Flat,
                Visible = false
            };
            rbAssistedWithCartridge = new RadioButton
            {
                Text = "With Cartridge",
                Location = new Point(10, 18),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9.5F)
            };
            rbAssistedWithoutCartridge = new RadioButton
            {
                Text = "Without Cartridge",
                Location = new Point(175, 18),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F)
            };
            gbAssistedType.Controls.Add(rbAssistedWithCartridge);
            gbAssistedType.Controls.Add(rbAssistedWithoutCartridge);

            btnAssistedAddItem = new Button
            {
                Text = "+ Add to Request",
                Location = new Point(labelWidth + 20, yPos),
                Width = 185,
                Height = 38,
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAssistedAddItem.FlatAppearance.BorderSize = 0;
            btnAssistedAddItem.Click += BtnAssistedAddItem_Click;
            yPos += 55;

            var lblAssistedItems = new Label
            {
                Text = "Request Items:",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Location = new Point(20, yPos),
                AutoSize = true
            };
            yPos += 40;

            dgvAssistedRequestItems = new DataGridView
            {
                Location = new Point(20, yPos),
                Width = 870,
                Height = 150,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                Font = new Font("Segoe UI", 9F),
                RowTemplate = { Height = 32 }
            };
            dgvAssistedRequestItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "CartridgeModel", HeaderText = "Cartridge Model", Width = 280 });
            dgvAssistedRequestItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity",        HeaderText = "Qty Requested",   Width = 100 });
            dgvAssistedRequestItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "GoodQty",         HeaderText = "Returned Cartridges (Good)",    Width = 160 });
            dgvAssistedRequestItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "DamagedQty",      HeaderText = "Returned Cartridges (Damaged)", Width = 180 });
            dgvAssistedRequestItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "CartridgeType",   HeaderText = "Type",            Width = 140 });
            dgvAssistedRequestItems.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(78, 154, 252);
            dgvAssistedRequestItems.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvAssistedRequestItems.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvAssistedRequestItems.ColumnHeadersHeight = 38;
            dgvAssistedRequestItems.EnableHeadersVisualStyles = false;
            yPos += 165;

            btnAssistedRemoveItem = new Button
            {
                Text = "Remove Selected",
                Location = new Point(20, yPos),
                Width = 150,
                Height = 35,
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAssistedRemoveItem.FlatAppearance.BorderSize = 0;
            btnAssistedRemoveItem.Click += BtnAssistedRemoveItem_Click;
            yPos += 60;

            // ── Destination (auto-filled from employee) ────────────────────────
            var lblDestHeader = new Label
            {
                Text = "Destination (Auto-filled from Employee)",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Location = new Point(20, yPos),
                AutoSize = true
            };
            yPos += 45;

            var lblCompany = CreateLabel("Company:", yPos);
            lblCompany.Width = labelWidth;
            cmbAssistedCompany = new ComboBox
            {
                Location = new Point(labelWidth + 20, yPos - 3),
                Width = controlWidth,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F)
            };
            cmbAssistedCompany.MouseWheel += (s, ev) => { if (!cmbAssistedCompany.Focused) ((HandledMouseEventArgs)ev).Handled = true; };
            yPos += spacing;

            var lblBranch = CreateLabel("Branch:", yPos);
            lblBranch.Width = labelWidth;
            cmbAssistedBranch = new ComboBox
            {
                Location = new Point(labelWidth + 20, yPos - 3),
                Width = controlWidth,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F)
            };
            cmbAssistedBranch.MouseWheel += (s, ev) => { if (!cmbAssistedBranch.Focused) ((HandledMouseEventArgs)ev).Handled = true; };
            yPos += spacing;

            var lblDepartment = CreateLabel("Department:", yPos);
            lblDepartment.Width = labelWidth;
            cmbAssistedDepartment = new ComboBox
            {
                Location = new Point(labelWidth + 20, yPos - 3),
                Width = controlWidth,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F)
            };
            cmbAssistedDepartment.MouseWheel += (s, ev) => { if (!cmbAssistedDepartment.Focused) ((HandledMouseEventArgs)ev).Handled = true; };
            yPos += spacing + 10;

            // ── Fulfillment ────────────────────────────────────────────────────
            var lblFulfillment = CreateLabel("Fulfillment Method:", yPos);
            lblFulfillment.Width = labelWidth;
            var gbFulfillment = new GroupBox
            {
                Location = new Point(labelWidth + 20, yPos - 10),
                Width = 400,
                Height = 50,
                FlatStyle = FlatStyle.Flat
            };
            rbAssistedPickup = new RadioButton
            {
                Text = "PICKUP",
                Location = new Point(10, 18),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9.5F)
            };
            rbAssistedDelivery = new RadioButton
            {
                Text = "DELIVERY",
                Location = new Point(150, 18),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F)
            };
            gbFulfillment.Controls.Add(rbAssistedPickup);
            gbFulfillment.Controls.Add(rbAssistedDelivery);
            yPos += 75;

            lblAssistedReceivedBy = CreateLabel("To Be Received By:", yPos);
            lblAssistedReceivedBy.Width = labelWidth;
            cmbAssistedReceivedBy = new ComboBox
            {
                Location = new Point(labelWidth + 20, yPos - 3),
                Width = controlWidth,
                DropDownStyle = ComboBoxStyle.DropDown,
                Font = new Font("Segoe UI", 10F)
            };
            ConfigureSearchableComboBox(cmbAssistedReceivedBy);
            rbAssistedPickup.CheckedChanged   += (s, e) => { lblAssistedReceivedBy.Visible = cmbAssistedReceivedBy.Visible = rbAssistedPickup.Checked; };
            rbAssistedDelivery.CheckedChanged += (s, e) => { lblAssistedReceivedBy.Visible = cmbAssistedReceivedBy.Visible = rbAssistedPickup.Checked; };
            yPos += spacing;

            var lblRemarks = CreateLabel("Additional Remarks:", yPos);
            lblRemarks.Width = labelWidth;
            txtAssistedRemarks = new TextBox
            {
                Location = new Point(labelWidth + 20, yPos - 3),
                Width = controlWidth,
                Height = 70,
                Multiline = true,
                Font = new Font("Segoe UI", 10F),
                ScrollBars = ScrollBars.Vertical
            };
            yPos += 90;

            var flowButtons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Location = new Point(labelWidth + 20, yPos),
                Width = controlWidth,
                Height = 50,
                WrapContents = false
            };

            btnAssistedClear = new Button
            {
                Text = "Clear",
                Width = 130,
                Height = 44,
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(5, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnAssistedClear.FlatAppearance.BorderSize = 0;
            btnAssistedClear.Click += BtnAssistedClear_Click;

            btnAssistedSubmit = new Button
            {
                Text = "Submit Assisted Request",
                Width = 210,
                Height = 44,
                BackColor = Color.FromArgb(78, 154, 252),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(10, 0, 0, 0)
            };
            btnAssistedSubmit.FlatAppearance.BorderSize = 0;
            btnAssistedSubmit.Click += BtnAssistedSubmit_Click;

            flowButtons.Controls.Add(btnAssistedClear);
            flowButtons.Controls.Add(btnAssistedSubmit);

            cardPanel.Height = yPos + 40;

            cardPanel.Controls.Add(lblEmpHeader);
            cardPanel.Controls.Add(lblEmployee);
            cardPanel.Controls.Add(cmbAssistedEmployee);
            cardPanel.Controls.Add(btnQuickAddEmployee);
            cardPanel.Controls.Add(lblCartridgeHeader);
            cardPanel.Controls.Add(lblCartridgeModel);
            cardPanel.Controls.Add(cmbAssistedCartridgeModel);
            cardPanel.Controls.Add(lblQuantity);
            cardPanel.Controls.Add(nudAssistedQuantity);
            cardPanel.Controls.Add(lblAssistedCondHeader);
            cardPanel.Controls.Add(lblGoodQty);
            cardPanel.Controls.Add(nudAssistedGoodQty);
            cardPanel.Controls.Add(lblDmgInline);
            cardPanel.Controls.Add(nudAssistedDamagedQty);
            cardPanel.Controls.Add(lblAssistedTypeRow);
            cardPanel.Controls.Add(gbAssistedType);
            cardPanel.Controls.Add(btnAssistedAddItem);
            cardPanel.Controls.Add(lblAssistedItems);
            cardPanel.Controls.Add(dgvAssistedRequestItems);
            cardPanel.Controls.Add(btnAssistedRemoveItem);
            cardPanel.Controls.Add(lblDestHeader);
            cardPanel.Controls.Add(lblCompany);
            cardPanel.Controls.Add(cmbAssistedCompany);
            cardPanel.Controls.Add(lblBranch);
            cardPanel.Controls.Add(cmbAssistedBranch);
            cardPanel.Controls.Add(lblDepartment);
            cardPanel.Controls.Add(cmbAssistedDepartment);
            cardPanel.Controls.Add(lblFulfillment);
            cardPanel.Controls.Add(gbFulfillment);
            cardPanel.Controls.Add(lblAssistedReceivedBy);
            cardPanel.Controls.Add(cmbAssistedReceivedBy);
            cardPanel.Controls.Add(lblRemarks);
            cardPanel.Controls.Add(txtAssistedRemarks);
            cardPanel.Controls.Add(flowButtons);

            scrollPanel.Controls.Add(cardPanel);
            tabAssistedRequest.Controls.Add(scrollPanel);

            EventHandler centerCard = (s, e) =>
            {
                int w = scrollPanel.ClientSize.Width;
                cardPanel.Left = w > cardPanel.Width + 60 ? (w - cardPanel.Width) / 2 : 30;
            };
            tabAssistedRequest.Resize += centerCard;
            scrollPanel.Resize += centerCard;
            scrollPanel.ClientSizeChanged += centerCard;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Request History Tab  (unchanged — all users)
        // ──────────────────────────────────────────────────────────────────────

        private void InitializeMyRequestsTab()
        {
            _requestHistoryViewModel = new RequestHistoryViewModel();
            _requestHistoryViewModel.ShowDetailRequested += items =>
            {
                if (InvokeRequired) { BeginInvoke(new Action(() => OpenRequestDetail(items))); return; }
                OpenRequestDetail(items);
            };
            _requestHistoryViewRef = new RequestHistoryView(_requestHistoryViewModel);
            var host = new ElementHost { Dock = DockStyle.Fill, Child = _requestHistoryViewRef };
            tabMyRequests.Controls.Add(host);
        }

        private void OpenRequestDetail(List<PortalRequestStatusDto> items)
        {
            using (var dlg = new RequestDetailDialog(items))
                dlg.ShowDialog(this);
        }

        private void _InitializeMyRequestsTab_Legacy()
        {
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(20, 10, 20, 10)
            };

            lblRequestCount = new Label
            {
                Text = "Loading requests...",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60),
                Location = new Point(20, 18),
                AutoSize = true
            };

            btnRefresh = new Button
            {
                Text = "Refresh",
                Width = 100,
                Height = 36,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Location = new Point(pnlHeader.Width - 130, 12);
            btnRefresh.Click += BtnRefresh_Click;

            pnlHeader.Controls.Add(lblRequestCount);
            pnlHeader.Controls.Add(btnRefresh);
            pnlHeader.Resize += (s, e) => btnRefresh.Left = pnlHeader.Width - 130;

            dgvMyRequests = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                Font = new Font("Segoe UI", 9.5F),
                RowTemplate = { Height = 35 }
            };

            dgvMyRequests.Columns.Add(new DataGridViewTextBoxColumn { Name = "ReqId",             HeaderText = "Set Code",     Width = 110 });
            dgvMyRequests.Columns.Add(new DataGridViewTextBoxColumn { Name = "DateRequested",      HeaderText = "Date",         Width = 120 });
            dgvMyRequests.Columns.Add(new DataGridViewTextBoxColumn { Name = "ItemName",           HeaderText = "Cartridge",    Width = 250 });
            dgvMyRequests.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity",           HeaderText = "Qty",          Width = 80 });
            dgvMyRequests.Columns.Add(new DataGridViewTextBoxColumn { Name = "ReturnInfo",         HeaderText = "Return Info",  Width = 150 });
            dgvMyRequests.Columns.Add(new DataGridViewTextBoxColumn { Name = "FulfillmentMethod",  HeaderText = "Method",       Width = 100 });
            dgvMyRequests.Columns.Add(new DataGridViewTextBoxColumn { Name = "DestinationBranch",  HeaderText = "Destination",  Width = 200 });
            dgvMyRequests.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status",             HeaderText = "Status",       Width = 120 });

            dgvMyRequests.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(78, 154, 252);
            dgvMyRequests.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvMyRequests.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgvMyRequests.ColumnHeadersHeight = 40;
            dgvMyRequests.EnableHeadersVisualStyles = false;

            dgvMyRequests.RowsDefaultCellStyle.BackColor = Color.White;
            dgvMyRequests.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);

            dgvMyRequests.CellFormatting += DgvMyRequests_CellFormatting;
            dgvMyRequests.CellClick += DgvMyRequests_CellClick;
            dgvMyRequests.Cursor = Cursors.Hand;

            tabMyRequests.Controls.Add(dgvMyRequests);
            tabMyRequests.Controls.Add(pnlHeader);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Helper
        // ══════════════════════════════════════════════════════════════════════

        private Label CreateLabel(string text, int yPos)
        {
            return new Label
            {
                Text = text,
                Location = new Point(20, yPos),
                Width = 200,
                Height = 30,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        // Data loading
        // ══════════════════════════════════════════════════════════════════════

        private void LoadData()
        {
            try
            {
                // Load cartridge models for the self-request tab
                _cartridgeModels = _portalService.GetCartridgeModelsWithAvailability();
                if (cmbCartridgeModel != null)
                {
                    cmbCartridgeModel.DataSource = null;
                    cmbCartridgeModel.DisplayMember = "ShortDisplayName";
                    cmbCartridgeModel.ValueMember = "ModelKey";
                    cmbCartridgeModel.DataSource = _cartridgeModels;
                }

                // Load employees — filter by dept account if applicable
                if (AppSession.IsDepartmentAccountSession
                    && AppSession.DepartmentAccountCompanyId.HasValue
                    && AppSession.DepartmentAccountDepartmentId.HasValue
                    && AppSession.DepartmentAccountBranchId.HasValue)
                {
                    _employees = _portalService.GetEmployeesByAccount(
                        AppSession.DepartmentAccountCompanyId.Value,
                        AppSession.DepartmentAccountDepartmentId.Value,
                        AppSession.DepartmentAccountBranchId.Value);
                }
                else
                {
                    _employees = _portalService.GetActiveEmployees();
                }

                // Bind employee selection dropdown (dept account mode)
                if (AppSession.IsDepartmentAccountSession && cmbDeptEmployee != null)
                {
                    cmbDeptEmployee.DataSource = null;
                    cmbDeptEmployee.DisplayMember = "DisplayName";
                    cmbDeptEmployee.DataSource = new System.Collections.Generic.List<EmployeeViewModel>(_employees);
                    cmbDeptEmployee.SelectedIndex = -1;
                }

                // ReceivedBy dropdown (null when self-request tab is the WPF view)
                if (cmbReceivedBy != null)
                {
                    cmbReceivedBy.Items.Clear();
                    foreach (var emp in _employees) cmbReceivedBy.Items.Add(emp);
                    cmbReceivedBy.DisplayMember = "DisplayName";
                    cmbReceivedBy.Tag = cmbReceivedBy.Items.Cast<object>().ToList();
                    cmbReceivedBy.SelectedIndex = -1;
                    cmbReceivedBy.Text = string.Empty;
                }

                // Legacy WinForms assisted-request controls — null after WPF conversion, skip silently
                if (AppSession.IsITStaff && cmbAssistedEmployee != null)
                {
                    LoadEmployeeComboBox(_employees);
                    if (cmbAssistedReceivedBy != null)
                    {
                        cmbAssistedReceivedBy.Items.Clear();
                        foreach (var emp in _employees) cmbAssistedReceivedBy.Items.Add(emp);
                        cmbAssistedReceivedBy.DisplayMember = "DisplayName";
                        cmbAssistedReceivedBy.Tag = cmbAssistedReceivedBy.Items.Cast<object>().ToList();
                        cmbAssistedReceivedBy.SelectedIndex = -1;
                        cmbAssistedReceivedBy.Text = string.Empty;
                    }

                    if (cmbAssistedCartridgeModel != null)
                    {
                        cmbAssistedCartridgeModel.DataSource = null;
                        cmbAssistedCartridgeModel.DisplayMember = "DisplayName";
                        cmbAssistedCartridgeModel.ValueMember = "ModelKey";
                        cmbAssistedCartridgeModel.DataSource = new List<CartridgeModelAvailabilityViewModel>(_cartridgeModels);
                    }

                    var companiesDto = _portalService.GetActiveCompanies();
                    _companies = companiesDto.Select(c => new CompanyViewModel
                    {
                        ComId = c.ComId,
                        Name = c.Name,
                        Description = c.Description,
                        Active = true
                    }).ToList();

                    if (cmbAssistedCompany != null)
                    {
                        cmbAssistedCompany.DataSource = null;
                        cmbAssistedCompany.DisplayMember = "DisplayName";
                        cmbAssistedCompany.ValueMember = "ComId";
                        cmbAssistedCompany.DataSource = _companies;
                    }

                    LoadAllBranches();
                    LoadAllDepartments();
                }

                LoadMyRequests();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadAllBranches()
        {
            _branches = new List<BranchViewModel>();

            try
            {
                var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;
                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new System.Data.SqlClient.SqlConnection(cs))
                {
                    con.Open();
                    const string sql = @"
                        SELECT
                            b.BranchId,
                            b.Name,
                            b.Description,
                            ISNULL(bdc_co.CompanyID,  0) AS ComId,
                            bdc_dept.DepartmentID        AS DeptId,
                            b.Active
                        FROM dbo.Branch b
                        OUTER APPLY (
                            SELECT TOP 1 bdc.CompanyID
                            FROM   dbo.BranchDepartmentCompany bdc
                            WHERE  bdc.BranchID = b.BranchId
                            ORDER BY bdc.BranchDeptCompanyID
                        ) bdc_co
                        OUTER APPLY (
                            SELECT TOP 1 bdc.DepartmentID
                            FROM   dbo.BranchDepartmentCompany bdc
                            WHERE  bdc.BranchID = b.BranchId AND bdc.DepartmentID IS NOT NULL
                            ORDER BY bdc.BranchDeptCompanyID
                        ) bdc_dept
                        WHERE b.Active = 1
                        ORDER BY b.Name";

                    using (var cmd = new System.Data.SqlClient.SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader.IsDBNull(0) || reader.IsDBNull(1)) continue;
                            _branches.Add(new BranchViewModel
                            {
                                BranchId    = reader.GetInt32(0),
                                Name        = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                ComId       = reader.GetInt32(3),
                                DeptId      = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                                Active      = !reader.IsDBNull(5) && reader.GetBoolean(5)
                            });
                        }
                    }
                }

                if (cmbAssistedBranch != null)
                {
                    cmbAssistedBranch.DataSource = null;
                    cmbAssistedBranch.DisplayMember = "DisplayName";
                    cmbAssistedBranch.ValueMember = "BranchId";
                    cmbAssistedBranch.DataSource = _branches;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load branches: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadAllDepartments()
        {
            _departments = new List<DepartmentViewModel>();

            try
            {
                var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;
                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new System.Data.SqlClient.SqlConnection(cs))
                {
                    con.Open();
                    const string sql = @"
                        SELECT
                            d.DeptId,
                            d.Name,
                            d.Description,
                            ISNULL(bdc_co.CompanyID, 0) AS ComId,
                            d.Active
                        FROM dbo.Department d
                        OUTER APPLY (
                            SELECT TOP 1 bdc.CompanyID
                            FROM   dbo.BranchDepartmentCompany bdc
                            WHERE  bdc.DepartmentID = d.DeptId
                            ORDER BY bdc.BranchDeptCompanyID
                        ) bdc_co
                        WHERE d.Active = 1
                        ORDER BY d.Name";

                    using (var cmd = new System.Data.SqlClient.SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader.IsDBNull(0) || reader.IsDBNull(1)) continue;
                            _departments.Add(new DepartmentViewModel
                            {
                                DeptId      = reader.GetInt32(0),
                                Name        = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                ComId       = reader.GetInt32(3),
                                Active      = !reader.IsDBNull(4) && reader.GetBoolean(4)
                            });
                        }
                    }
                }

                if (cmbAssistedDepartment != null)
                {
                    cmbAssistedDepartment.DataSource = null;
                    cmbAssistedDepartment.DisplayMember = "DisplayName";
                    cmbAssistedDepartment.ValueMember = "DeptId";
                    cmbAssistedDepartment.DataSource = _departments;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load departments: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Combo-box change handlers
        // ══════════════════════════════════════════════════════════════════════

        // ── Searchable employee ComboBox helpers ──────────────────────────────────
        // Mirrors ConfigureSearchableComboBox / RestoreComboItems in BatchAddItemDialog.
        // Uses manual item filtering via TextUpdate instead of AutoCompleteMode so that
        // keyboard commit (Enter) and SelectedIndexChanged are fully deterministic.

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int ShowCursor(bool bShow);

        private void LoadEmployeeComboBox(List<EmployeeViewModel> employees)
        {
            if (cmbAssistedEmployee == null) return;
            _suppressAssistedEmpEvents = true;
            try
            {
                cmbAssistedEmployee.Items.Clear();
                foreach (var emp in employees ?? new List<EmployeeViewModel>())
                    cmbAssistedEmployee.Items.Add(emp);
            }
            finally
            {
                _suppressAssistedEmpEvents = false;
            }
            cmbAssistedEmployee.Tag = cmbAssistedEmployee.Items.Cast<object>().ToList();
            cmbAssistedEmployee.SelectedIndex = -1;
            cmbAssistedEmployee.Text = string.Empty;
        }

        private void RestoreAssistedEmployeeItems()
        {
            var all = cmbAssistedEmployee.Tag as List<object>;
            if (all == null || cmbAssistedEmployee.Items.Count == all.Count) return;

            _suppressAssistedEmpEvents = true;
            try
            {
                cmbAssistedEmployee.Items.Clear();
                foreach (var item in all)
                    cmbAssistedEmployee.Items.Add(item);
            }
            finally
            {
                _suppressAssistedEmpEvents = false;
            }
        }

        private void ConfigureSearchableEmployeeComboBox(ComboBox cmb)
        {
            // Mark arrow/Enter/Escape as input so the form doesn't intercept them.
            cmb.PreviewKeyDown += (s, ev) =>
            {
                if (ev.KeyCode == Keys.Up || ev.KeyCode == Keys.Down ||
                    ev.KeyCode == Keys.Enter || ev.KeyCode == Keys.Escape)
                    ev.IsInputKey = true;
            };

            // Enter: commit the best match from the full backing list.
            cmb.KeyDown += (s, ev) =>
            {
                if (ev.KeyCode == Keys.Enter)
                {
                    string typed = cmb.Text?.Trim();
                    if (cmb.DroppedDown) cmb.DroppedDown = false;

                    if (!string.IsNullOrEmpty(typed))
                    {
                        var all = cmb.Tag as List<object>;
                        var searchList = all ?? cmb.Items.Cast<object>().ToList();

                        int match = -1;
                        // Pass 1: exact
                        for (int i = 0; i < searchList.Count; i++)
                        {
                            if (string.Equals(cmb.GetItemText(searchList[i]), typed, StringComparison.OrdinalIgnoreCase))
                            { match = i; break; }
                        }
                        // Pass 2: starts-with
                        if (match < 0)
                        {
                            for (int i = 0; i < searchList.Count; i++)
                            {
                                if (cmb.GetItemText(searchList[i]).StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                                { match = i; break; }
                            }
                        }
                        if (match >= 0)
                        {
                            RestoreAssistedEmployeeItems();
                            cmb.SelectedIndex = match;
                        }
                    }
                    ev.Handled = true;
                    ev.SuppressKeyPress = true;
                }
                else if (ev.KeyCode == Keys.Escape)
                {
                    if (cmb.DroppedDown) { cmb.DroppedDown = false; ev.Handled = true; ev.SuppressKeyPress = true; }
                }
            };

            // Fix cursor-disappear bug on mouse wheel scroll.
            cmb.MouseWheel += (s, ev) =>
            {
                if (!cmb.Focused) { ((HandledMouseEventArgs)ev).Handled = true; return; }
                while (ShowCursor(true) < 0) { }
                Cursor.Current = Cursors.IBeam;
            };

            // TextUpdate: filter Items from backing list on every keystroke.
            cmb.TextUpdate += (s, ev) =>
            {
                cmb.BeginInvoke((Action)(() =>
                {
                    if (!cmb.Focused) return;
                    var all = cmb.Tag as List<object>;
                    if (all == null) return;

                    string savedText = cmb.Text;
                    var filtered = string.IsNullOrEmpty(savedText)
                        ? all
                        : all.Where(item => cmb.GetItemText(item).IndexOf(savedText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                    _suppressAssistedEmpEvents = true;
                    try
                    {
                        cmb.Items.Clear();
                        foreach (var item in filtered) cmb.Items.Add(item);
                    }
                    finally { _suppressAssistedEmpEvents = false; }

                    cmb.Text = savedText;
                    cmb.SelectionStart = savedText.Length;
                    cmb.SelectionLength = 0;

                    if (!cmb.DroppedDown && filtered.Count > 0 && !string.IsNullOrEmpty(savedText))
                        cmb.DroppedDown = true;

                    // Re-assert after opening the dropdown: the native Win32 combo
                    // auto-selects the first matching item when DroppedDown=true fires,
                    // which overwrites the edit box text with a highlighted suggestion.
                    cmb.Text = savedText;
                    cmb.SelectionStart = savedText.Length;
                    cmb.SelectionLength = 0;

                    // Restore cursor — Windows hides it when typing starts
                    while (ShowCursor(true) < 0) { }
                    Cursor.Current = Cursors.IBeam;
                }));
            };

            // DropDownClosed: restore full list and re-commit the user's selection.
            cmb.DropDownClosed += (s, ev) =>
            {
                var all = cmb.Tag as List<object>;
                if (all == null || cmb.Items.Count == all.Count) return;

                var selectedItem = cmb.SelectedItem;
                string currentText = cmb.Text?.Trim();

                _suppressAssistedEmpEvents = true;
                try
                {
                    cmb.Items.Clear();
                    foreach (var item in all) cmb.Items.Add(item);
                }
                finally { _suppressAssistedEmpEvents = false; }

                // Re-commit by object reference (mouse click) …
                if (selectedItem != null)
                {
                    for (int i = 0; i < cmb.Items.Count; i++)
                    {
                        if (ReferenceEquals(cmb.Items[i], selectedItem))
                        { if (cmb.SelectedIndex != i) cmb.SelectedIndex = i; return; }
                    }
                }
                // … or by exact text match (keyboard without explicit selection)
                if (!string.IsNullOrWhiteSpace(currentText))
                {
                    for (int i = 0; i < cmb.Items.Count; i++)
                    {
                        if (string.Equals(cmb.GetItemText(cmb.Items[i]), currentText, StringComparison.OrdinalIgnoreCase))
                        { cmb.SelectedIndex = i; return; }
                    }
                }
            };
        }

        /// <summary>
        /// Generic searchable combobox — type to filter, Enter/click to commit.
        /// Requires DropDownStyle = DropDown and cmb.Tag set to the full item backing list.
        /// </summary>
        private void ConfigureSearchableComboBox(ComboBox cmb)
        {
            bool suppress = false;

            cmb.PreviewKeyDown += (s, ev) =>
            {
                if (ev.KeyCode == Keys.Up || ev.KeyCode == Keys.Down ||
                    ev.KeyCode == Keys.Enter || ev.KeyCode == Keys.Escape)
                    ev.IsInputKey = true;
            };

            cmb.KeyDown += (s, ev) =>
            {
                if (ev.KeyCode == Keys.Enter)
                {
                    string typed = cmb.Text?.Trim();
                    if (cmb.DroppedDown) cmb.DroppedDown = false;

                    if (!string.IsNullOrEmpty(typed))
                    {
                        var all = cmb.Tag as List<object>;
                        var searchList = all ?? cmb.Items.Cast<object>().ToList();

                        int match = -1;
                        for (int i = 0; i < searchList.Count; i++)
                        {
                            if (string.Equals(cmb.GetItemText(searchList[i]), typed, StringComparison.OrdinalIgnoreCase))
                            { match = i; break; }
                        }
                        if (match < 0)
                        {
                            for (int i = 0; i < searchList.Count; i++)
                            {
                                if (cmb.GetItemText(searchList[i]).StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                                { match = i; break; }
                            }
                        }
                        if (match >= 0)
                        {
                            var all2 = cmb.Tag as List<object>;
                            if (all2 != null && cmb.Items.Count != all2.Count)
                            {
                                suppress = true;
                                try { cmb.Items.Clear(); foreach (var item in all2) cmb.Items.Add(item); }
                                finally { suppress = false; }
                            }
                            cmb.SelectedIndex = match;
                        }
                    }
                    ev.Handled = true;
                    ev.SuppressKeyPress = true;
                }
                else if (ev.KeyCode == Keys.Escape)
                {
                    if (cmb.DroppedDown) { cmb.DroppedDown = false; ev.Handled = true; ev.SuppressKeyPress = true; }
                }
            };

            cmb.MouseWheel += (s, ev) =>
            {
                if (!cmb.Focused) { ((HandledMouseEventArgs)ev).Handled = true; return; }
                while (ShowCursor(true) < 0) { }
                Cursor.Current = Cursors.IBeam;
            };

            cmb.TextUpdate += (s, ev) =>
            {
                cmb.BeginInvoke((Action)(() =>
                {
                    if (!cmb.Focused) return;
                    var all = cmb.Tag as List<object>;
                    if (all == null) return;

                    string savedText = cmb.Text;
                    var filtered = string.IsNullOrEmpty(savedText)
                        ? all
                        : all.Where(item => cmb.GetItemText(item).IndexOf(savedText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                    suppress = true;
                    try
                    {
                        cmb.Items.Clear();
                        foreach (var item in filtered) cmb.Items.Add(item);
                    }
                    finally { suppress = false; }

                    cmb.Text = savedText;
                    cmb.SelectionStart = savedText.Length;
                    cmb.SelectionLength = 0;

                    if (!cmb.DroppedDown && filtered.Count > 0 && !string.IsNullOrEmpty(savedText))
                        cmb.DroppedDown = true;

                    // Re-assert after opening the dropdown: the native Win32 combo
                    // auto-selects the first matching item when DroppedDown=true fires,
                    // which overwrites the edit box text with a highlighted suggestion.
                    cmb.Text = savedText;
                    cmb.SelectionStart = savedText.Length;
                    cmb.SelectionLength = 0;

                    // Restore cursor — Windows hides it when typing starts
                    while (ShowCursor(true) < 0) { }
                    Cursor.Current = Cursors.IBeam;
                }));
            };

            cmb.DropDownClosed += (s, ev) =>
            {
                var all = cmb.Tag as List<object>;
                if (all == null || cmb.Items.Count == all.Count) return;

                var selectedItem = cmb.SelectedItem;
                string currentText = cmb.Text?.Trim();

                suppress = true;
                try
                {
                    cmb.Items.Clear();
                    foreach (var item in all) cmb.Items.Add(item);
                }
                finally { suppress = false; }

                if (selectedItem != null)
                {
                    for (int i = 0; i < cmb.Items.Count; i++)
                    {
                        if (ReferenceEquals(cmb.Items[i], selectedItem))
                        { if (cmb.SelectedIndex != i) cmb.SelectedIndex = i; return; }
                    }
                }
                if (!string.IsNullOrWhiteSpace(currentText))
                {
                    for (int i = 0; i < cmb.Items.Count; i++)
                    {
                        if (string.Equals(cmb.GetItemText(cmb.Items[i]), currentText, StringComparison.OrdinalIgnoreCase))
                        { cmb.SelectedIndex = i; return; }
                    }
                }
            };
        }

        /// <summary>
        /// Assisted tab: employee selection auto-fills destination dropdowns.
        /// </summary>
        private void CmbAssistedEmployee_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressAssistedEmpEvents) return;
            try
            {
                var emp = cmbAssistedEmployee.SelectedItem as EmployeeViewModel;
                if (emp == null) return;

                if (emp.ComId > 0)
                {
                    for (int i = 0; i < cmbAssistedCompany.Items.Count; i++)
                    {
                        if (cmbAssistedCompany.Items[i] is CompanyViewModel c && c.ComId == emp.ComId)
                        { cmbAssistedCompany.SelectedIndex = i; break; }
                    }
                }

                if (emp.BranchId > 0)
                {
                    for (int i = 0; i < cmbAssistedBranch.Items.Count; i++)
                    {
                        if (cmbAssistedBranch.Items[i] is BranchViewModel b && b.BranchId == emp.BranchId)
                        { cmbAssistedBranch.SelectedIndex = i; break; }
                    }
                }

                if (emp.DeptId > 0)
                {
                    for (int i = 0; i < cmbAssistedDepartment.Items.Count; i++)
                    {
                        if (cmbAssistedDepartment.Items[i] is DepartmentViewModel d && d.DeptId == emp.DeptId)
                        { cmbAssistedDepartment.SelectedIndex = i; break; }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error auto-filling destination: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Opens QuickAddEmployeeDialog and, on success, reloads the employee list and
        /// selects the newly created employee.
        /// </summary>
        private void BtnQuickAddEmployee_Click(object sender, EventArgs e)
        {
            using (var dlg = new QuickAddEmployeeDialog())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                int? newEmpId = dlg.NewEmployeeId;
                if (!newEmpId.HasValue) return;

                try
                {
                    _employees = _portalService.GetActiveEmployees();
                    LoadEmployeeComboBox(_employees);

                    for (int i = 0; i < cmbAssistedEmployee.Items.Count; i++)
                    {
                        if (cmbAssistedEmployee.Items[i] is EmployeeViewModel emp && emp.EmpId == newEmpId.Value)
                        {
                            cmbAssistedEmployee.SelectedIndex = i;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Employee created, but failed to reload the list:\n\n{ex.Message}",
                        "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        /// <summary>
        /// Self-request tab: cap quantity to available stock.
        /// </summary>
        private void CmbCartridgeModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            var m = cmbCartridgeModel.SelectedItem as CartridgeModelAvailabilityViewModel;
            if (m == null)
            {
                nudQuantity.Maximum = decimal.MaxValue;
                if (lblNoStockWarning != null) lblNoStockWarning.Visible = false;
                return;
            }

            nudQuantity.Maximum = decimal.MaxValue;
            if (nudQuantity.Value < 1) nudQuantity.Value = 1;

            bool hasStock = m.AvailableQuantity > 0;
            if (lblNoStockWarning != null) lblNoStockWarning.Visible = !hasStock;

            nudGoodQty.Maximum    = decimal.MaxValue;
            nudDamagedQty.Maximum = decimal.MaxValue;
        }

        /// <summary>
        /// Assisted tab: cap quantity to available stock.
        /// </summary>
        private void CmbAssistedCartridgeModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            var m = cmbAssistedCartridgeModel.SelectedItem as CartridgeModelAvailabilityViewModel;
            if (m == null) { nudAssistedQuantity.Maximum = decimal.MaxValue; return; }

            decimal avail = m.AvailableQuantity;
            nudAssistedQuantity.Maximum = Math.Max(0, avail);
            if (nudAssistedQuantity.Value > nudAssistedQuantity.Maximum)
                nudAssistedQuantity.Value = nudAssistedQuantity.Maximum;

            int cap = (int)nudAssistedQuantity.Maximum;
            nudAssistedGoodQty.Maximum    = cap;
            nudAssistedDamagedQty.Maximum = cap;
            if (nudAssistedGoodQty.Value > cap)    nudAssistedGoodQty.Value    = cap;
            if (nudAssistedDamagedQty.Value > cap) nudAssistedDamagedQty.Value = cap;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Add / Remove item handlers — Self-Request
        // ══════════════════════════════════════════════════════════════════════

        private void BtnAddItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbCartridgeModel.SelectedValue == null)
                {
                    MessageBox.Show("Please select a cartridge model.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cmbCartridgeModel.Focus();
                    return;
                }

                var selectedModel = cmbCartridgeModel.SelectedItem as CartridgeModelAvailabilityViewModel;
                if (selectedModel == null) return;

                if (nudQuantity.Value <= 0)
                {
                    MessageBox.Show("Please enter a quantity greater than 0.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    nudQuantity.Focus();
                    return;
                }

                int goodQty    = (int)nudGoodQty.Value;
                int damagedQty = (int)nudDamagedQty.Value;

                bool returningEmpties = goodQty > 0 || damagedQty > 0;
                if (returningEmpties && goodQty + damagedQty != (int)nudQuantity.Value)
                {
                    MessageBox.Show(
                        $"Good ({goodQty}) + Damaged ({damagedQty}) must equal the requested quantity ({(int)nudQuantity.Value}) when returning empties.",
                        "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var item = new CartridgeRequestItemViewModel
                {
                    CartridgeModel  = selectedModel.ModelNumber,
                    ModelKey        = selectedModel.ModelKey,
                    Quantity        = (int)nudQuantity.Value,
                    GoodEmptyQty    = goodQty,
                    DamagedEmptyQty = damagedQty,
                    CartridgeType   = "With Cartridge"
                };

                if (_requestItems.Any(x => x.ModelKey == item.ModelKey))
                {
                    var result = MessageBox.Show(
                        $"Model '{item.CartridgeModel}' is already in the list.\n\nUpdate the quantity?",
                        "Duplicate Model", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        var existing = _requestItems.First(x => x.ModelKey == item.ModelKey);
                        int merged = existing.Quantity + item.Quantity;
                        existing.Quantity = merged;
                        RefreshItemsGrid();
                    }
                    return;
                }

                _requestItems.Add(item);
                RefreshItemsGrid();
                nudQuantity.Value = Math.Max(nudQuantity.Minimum, Math.Min(1M, nudQuantity.Maximum));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRemoveItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (dgvRequestItems.SelectedRows.Count == 0)
                {
                    MessageBox.Show("Please select an item to remove.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int idx = dgvRequestItems.SelectedRows[0].Index;
                if (idx >= 0 && idx < _requestItems.Count)
                {
                    _requestItems.RemoveAt(idx);
                    RefreshItemsGrid();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshItemsGrid()
        {
            dgvRequestItems.Rows.Clear();
            foreach (var item in _requestItems)
            {
                int i = dgvRequestItems.Rows.Add();
                dgvRequestItems.Rows[i].Cells["CartridgeModel"].Value = item.CartridgeModel;
                dgvRequestItems.Rows[i].Cells["Quantity"].Value       = item.Quantity;
                dgvRequestItems.Rows[i].Cells["GoodQty"].Value        = item.GoodEmptyQty;
                dgvRequestItems.Rows[i].Cells["DamagedQty"].Value     = item.DamagedEmptyQty;
                dgvRequestItems.Rows[i].Cells["CartridgeType"].Value  = item.CartridgeType ?? "With Cartridge";
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Add / Remove item handlers — Assisted Request
        // ══════════════════════════════════════════════════════════════════════

        private void BtnAssistedAddItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbAssistedCartridgeModel.SelectedValue == null)
                {
                    MessageBox.Show("Please select a cartridge model.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cmbAssistedCartridgeModel.Focus();
                    return;
                }

                var selectedModel = cmbAssistedCartridgeModel.SelectedItem as CartridgeModelAvailabilityViewModel;
                if (selectedModel == null) return;

                if (nudAssistedQuantity.Value <= 0)
                {
                    MessageBox.Show("Please enter a quantity greater than 0.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    nudAssistedQuantity.Focus();
                    return;
                }

                int goodQty    = (int)nudAssistedGoodQty.Value;
                int damagedQty = (int)nudAssistedDamagedQty.Value;

                bool returningEmpties = goodQty > 0 || damagedQty > 0;
                if (returningEmpties && goodQty + damagedQty != (int)nudAssistedQuantity.Value)
                {
                    MessageBox.Show(
                        $"Good ({goodQty}) + Damaged ({damagedQty}) must equal the requested quantity ({(int)nudAssistedQuantity.Value}) when returning empties.",
                        "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var item = new CartridgeRequestItemViewModel
                {
                    CartridgeModel  = selectedModel.ModelNumber,
                    ModelKey        = selectedModel.ModelKey,
                    Quantity        = (int)nudAssistedQuantity.Value,
                    GoodEmptyQty    = goodQty,
                    DamagedEmptyQty = damagedQty,
                    CartridgeType   = "With Cartridge"
                };

                if (_assistedRequestItems.Any(x => x.ModelKey == item.ModelKey))
                {
                    var result = MessageBox.Show(
                        $"Model '{item.CartridgeModel}' is already in the list.\n\nUpdate the quantity?",
                        "Duplicate Model", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        var existing = _assistedRequestItems.First(x => x.ModelKey == item.ModelKey);
                        int merged = existing.Quantity + item.Quantity;
                        existing.Quantity = merged;
                        RefreshAssistedItemsGrid();
                    }
                    return;
                }

                _assistedRequestItems.Add(item);
                RefreshAssistedItemsGrid();
                nudAssistedQuantity.Value = Math.Max(nudAssistedQuantity.Minimum, Math.Min(1M, nudAssistedQuantity.Maximum));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAssistedRemoveItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (dgvAssistedRequestItems.SelectedRows.Count == 0)
                {
                    MessageBox.Show("Please select an item to remove.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int idx = dgvAssistedRequestItems.SelectedRows[0].Index;
                if (idx >= 0 && idx < _assistedRequestItems.Count)
                {
                    _assistedRequestItems.RemoveAt(idx);
                    RefreshAssistedItemsGrid();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshAssistedItemsGrid()
        {
            dgvAssistedRequestItems.Rows.Clear();
            foreach (var item in _assistedRequestItems)
            {
                int i = dgvAssistedRequestItems.Rows.Add();
                dgvAssistedRequestItems.Rows[i].Cells["CartridgeModel"].Value = item.CartridgeModel;
                dgvAssistedRequestItems.Rows[i].Cells["Quantity"].Value       = item.Quantity;
                dgvAssistedRequestItems.Rows[i].Cells["GoodQty"].Value        = item.GoodEmptyQty;
                dgvAssistedRequestItems.Rows[i].Cells["DamagedQty"].Value     = item.DamagedEmptyQty;
                dgvAssistedRequestItems.Rows[i].Cells["CartridgeType"].Value  = item.CartridgeType ?? "With Cartridge";
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Department Account employee selection
        // ══════════════════════════════════════════════════════════════════════

        private void CmbDeptEmployee_SelectedIndexChanged(object sender, EventArgs e)
        {
            _deptAccountEmployee = cmbDeptEmployee.SelectedItem as EmployeeViewModel;
            if (_deptAccountEmployee == null)
            {
                if (lblDeptEmpInfo != null)
                    lblDeptEmpInfo.Text = "— Select an employee above —";
                return;
            }

            if (lblDeptEmpInfo != null)
            {
                lblDeptEmpInfo.Text =
                    $"  {_deptAccountEmployee.Name}   |   {_deptAccountEmployee.Position ?? "—"}   |   " +
                    $"{_deptAccountEmployee.CompanyName ?? "—"}   |   {_deptAccountEmployee.BranchName ?? "—"}   |   " +
                    $"{_deptAccountEmployee.DepartmentName ?? "—"}";
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Submit — Self-Request
        // ══════════════════════════════════════════════════════════════════════

        private void BtnSelfSubmit_Click(object sender, EventArgs e)
        {
            try
            {
                int submittingEmployeeId;
                string employeeName;
                string employeePosition;
                int destinationCompanyId;
                int destinationBranchId;
                int destinationDepartmentId;

                if (AppSession.IsDepartmentAccountSession)
                {
                    // Dept account mode: validate employee selection
                    if (_deptAccountEmployee == null)
                    {
                        MessageBox.Show(
                            "Please select an employee before submitting.",
                            "Employee Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        cmbDeptEmployee?.Focus();
                        return;
                    }

                    submittingEmployeeId   = _deptAccountEmployee.EmpId;
                    employeeName           = _deptAccountEmployee.Name;
                    employeePosition       = _deptAccountEmployee.Position ?? string.Empty;
                    destinationCompanyId   = _deptAccountEmployee.ComId;
                    destinationBranchId    = _deptAccountEmployee.BranchId;
                    destinationDepartmentId = _deptAccountEmployee.DeptId;
                }
                else
                {
                    // Normal mode: use session employee
                    if (!AppSession.CurrentEmployeeId.HasValue || AppSession.CurrentEmployeeId.Value <= 0)
                    {
                        MessageBox.Show(
                            "Your account does not have a linked employee profile.\n\n" +
                            "Contact IT to link your account, or use the Assisted Request tab if you have IT staff access.",
                            "No Employee Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (!AppSession.CurrentCompanyId.HasValue || AppSession.CurrentCompanyId.Value <= 0 ||
                        !AppSession.CurrentBranchId.HasValue  || AppSession.CurrentBranchId.Value  <= 0 ||
                        !AppSession.CurrentDepartmentId.HasValue || AppSession.CurrentDepartmentId.Value <= 0)
                    {
                        MessageBox.Show(
                            "Your employee profile is missing company, branch, or department information.\n\n" +
                            "Contact IT to update your profile.",
                            "Incomplete Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    submittingEmployeeId    = AppSession.CurrentEmployeeId.Value;
                    employeeName            = AppSession.CurrentEmployeeName ?? AppSession.CurrentUserName;
                    employeePosition        = AppSession.CurrentEmployeePosition ?? string.Empty;
                    destinationCompanyId    = AppSession.CurrentCompanyId.Value;
                    destinationBranchId     = AppSession.CurrentBranchId.Value;
                    destinationDepartmentId = AppSession.CurrentDepartmentId.Value;
                }

                if (_requestItems.Count == 0)
                {
                    MessageBox.Show("Please add at least one cartridge model to the request.",
                        "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    btnAddItem.Focus();
                    return;
                }

                if (rbPickup.Checked && !(cmbReceivedBy.SelectedItem is EmployeeViewModel))
                {
                    MessageBox.Show("Please select who will receive the cartridges (Received By).",
                        "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cmbReceivedBy.Focus();
                    return;
                }

                var requestViewModel = new Models.ViewModels.CartridgeRequestViewModel
                {
                    EmployeeName            = employeeName,
                    EmployeePosition        = employeePosition,
                    DestinationCompanyId    = destinationCompanyId,
                    DestinationBranchId     = destinationBranchId,
                    DestinationDepartmentId = destinationDepartmentId,
                    FulfillmentMethod       = rbPickup.Checked ? "PICKUP" : "DELIVERY",
                    AdditionalRemarks       = txtRemarks.Text.Trim(),
                    ReceivedById            = rbPickup.Checked && cmbReceivedBy.SelectedItem is EmployeeViewModel recvEmp
                                              ? recvEmp.EmpId : (int?)null
                };

                var (createdIds, wasAutoApproved, newAuthId, _) = _portalService.CreateCartridgeRequestByModel(
                    requestViewModel,
                    _requestItems,
                    submittingEmployeeId,
                    _currentUserId,
                    isAssisted: false);

                ClearSelfForm();
                LoadMyRequests();

                bool isSelfSign = AppSession.IsApprover && !wasAutoApproved && newAuthId > 0;
                if (isSelfSign)
                {
                    MessageBox.Show(
                        "Request submitted successfully!\n\n" +
                        "As an approver, you are required to sign and authorize your own request.\n\n" +
                        "You will now be taken to the Authorization page.",
                        "Authorization Required", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    ShowApproverArea();
                    _ = _approvalPage.LoadAndSelectAsync(newAuthId);
                }
                else
                {
                    MessageBox.Show(
                        $"Request submitted successfully!\n\n" +
                        $"Created {createdIds.Count} request(s).\n\n" +
                        (wasAutoApproved
                            ? "Your request has been auto-approved."
                            : "Your request is awaiting supervisor authorization before it will be processed."),
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    tabControl.SelectedTab = tabMyRequests;
                    if (AppSession.IsApprover) ShowRequestArea();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error submitting request:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Submit — Assisted Request  (IT staff only — backend role check)
        // ══════════════════════════════════════════════════════════════════════

        private void BtnAssistedSubmit_Click(object sender, EventArgs e)
        {
            try
            {
                // UI-level guard (redundant — service also enforces this)
                if (!AppSession.IsITStaff)
                {
                    MessageBox.Show(
                        "You do not have permission to submit assisted requests.",
                        "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!(cmbAssistedEmployee.SelectedItem is EmployeeViewModel))
                {
                    MessageBox.Show("Please select the target employee.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cmbAssistedEmployee.Focus();
                    return;
                }

                if (_assistedRequestItems.Count == 0)
                {
                    MessageBox.Show("Please add at least one cartridge model to the request.",
                        "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    btnAssistedAddItem.Focus();
                    return;
                }

                if (cmbAssistedCompany.SelectedValue == null)
                {
                    MessageBox.Show("Please select a company.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cmbAssistedCompany.Focus();
                    return;
                }

                if (cmbAssistedBranch.SelectedValue == null)
                {
                    MessageBox.Show("Please select a branch.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cmbAssistedBranch.Focus();
                    return;
                }

                if (cmbAssistedDepartment.SelectedValue == null)
                {
                    MessageBox.Show("Please select a department.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cmbAssistedDepartment.Focus();
                    return;
                }

                var selectedEmployee = cmbAssistedEmployee.SelectedItem as EmployeeViewModel;
                if (selectedEmployee == null) return;

                if (rbAssistedPickup.Checked && !(cmbAssistedReceivedBy.SelectedItem is EmployeeViewModel))
                {
                    MessageBox.Show("Please select who will receive the cartridges (Received By).",
                        "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cmbAssistedReceivedBy.Focus();
                    return;
                }

                var requestViewModel = new Models.ViewModels.CartridgeRequestViewModel
                {
                    EmployeeName          = selectedEmployee.Name,
                    EmployeePosition      = selectedEmployee.Position ?? string.Empty,
                    DestinationCompanyId    = Convert.ToInt32(cmbAssistedCompany.SelectedValue),
                    DestinationBranchId     = Convert.ToInt32(cmbAssistedBranch.SelectedValue),
                    DestinationDepartmentId = Convert.ToInt32(cmbAssistedDepartment.SelectedValue),
                    FulfillmentMethod     = rbAssistedPickup.Checked ? "PICKUP" : "DELIVERY",
                    AdditionalRemarks     = txtAssistedRemarks.Text.Trim(),
                    ReceivedById          = rbAssistedPickup.Checked && cmbAssistedReceivedBy.SelectedItem is EmployeeViewModel recvEmpA
                                            ? recvEmpA.EmpId : (int?)null
                };

                // isAssisted: true → service does backend role check + sets RequestSource = 'PORTAL_ASSISTED'
                var (createdIds, _, _, _) = _portalService.CreateCartridgeRequestByModel(
                    requestViewModel,
                    _assistedRequestItems,
                    selectedEmployee.EmpId,
                    _currentUserId,
                    isAssisted: true);

                MessageBox.Show(
                    $"Assisted request submitted successfully!\n\n" +
                    $"Employee: {selectedEmployee.Name}\n" +
                    $"Created {createdIds.Count} request(s): {string.Join(", ", createdIds)}\n\n" +
                    $"You can track progress in the 'Request History' tab.",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Reload availability so the next Assisted Request shows up-to-date stock counts.
                RefreshCartridgeModelDropdowns();

                ClearAssistedForm();
                LoadMyRequests();
                tabControl.SelectedTab = tabMyRequests;
            }
            catch (UnauthorizedAccessException uaEx)
            {
                MessageBox.Show(uaEx.Message, "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error submitting assisted request:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Clear forms
        // ══════════════════════════════════════════════════════════════════════

        private void BtnClear_Click(object sender, EventArgs e) => ClearSelfForm();

        private void BtnAssistedClear_Click(object sender, EventArgs e) => ClearAssistedForm();

        private void ClearSelfForm()
        {
            if (cmbCartridgeModel.Items.Count > 0)
                cmbCartridgeModel.SelectedIndex = 0;

            nudQuantity.Value = Math.Max(nudQuantity.Minimum, Math.Min(1M, nudQuantity.Maximum));
            nudGoodQty.Value = 0;
            nudDamagedQty.Value = 0;
            rbWithCartridge.Checked = true;

            _requestItems.Clear();
            RefreshItemsGrid();

            rbPickup.Checked = true;
            cmbReceivedBy.SelectedIndex = -1;
            txtRemarks.Clear();
        }

        private void ClearAssistedForm()
        {
            RestoreAssistedEmployeeItems();
            cmbAssistedEmployee.SelectedIndex = -1;
            cmbAssistedEmployee.Text = string.Empty;

            if (cmbAssistedCartridgeModel.Items.Count > 0)
                cmbAssistedCartridgeModel.SelectedIndex = 0;

            nudAssistedQuantity.Value = Math.Max(nudAssistedQuantity.Minimum, Math.Min(1M, nudAssistedQuantity.Maximum));
            nudAssistedGoodQty.Value = 0;
            nudAssistedDamagedQty.Value = 0;
            rbAssistedWithCartridge.Checked = true;

            _assistedRequestItems.Clear();
            RefreshAssistedItemsGrid();

            if (cmbAssistedCompany.Items.Count > 0)
                cmbAssistedCompany.SelectedIndex = 0;

            rbAssistedPickup.Checked = true;
            cmbAssistedReceivedBy.SelectedIndex = -1;
            txtAssistedRemarks.Clear();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Request History tab
        // ══════════════════════════════════════════════════════════════════════

        private void BtnRefresh_Click(object sender, EventArgs e) => LoadMyRequests();

        /// <summary>
        /// Re-fetches current cartridge availability from the database and rebinds both
        /// model dropdowns (self-request and assisted) without touching other form state.
        /// Call this after any fulfillment or issuance that changes stock levels.
        /// </summary>
        private void RefreshCartridgeModelDropdowns()
        {
            try
            {
                _cartridgeModels = _portalService.GetCartridgeModelsWithAvailability();

                if (cmbCartridgeModel != null)
                {
                    cmbCartridgeModel.DataSource = null;
                    cmbCartridgeModel.DisplayMember = "ShortDisplayName";
                    cmbCartridgeModel.ValueMember = "ModelKey";
                    cmbCartridgeModel.DataSource = _cartridgeModels;
                }

                _newRequestViewModel?.RefreshModels(_cartridgeModels);
                _assistedRequestViewModel?.RefreshModels(_cartridgeModels);
            }
            catch { /* non-critical: stale data is better than a crash */ }
        }

        private void LoadMyRequests()
        {
            if (_requestHistoryViewModel != null)
            {
                _ = _requestHistoryViewModel.LoadAsync();
                return;
            }

            // Legacy fallback (dgvMyRequests path — only reached if WPF tab not initialized)
            if (dgvMyRequests == null) return;
            try
            {
                var requests = _portalService.GetPortalRequestsByUser(_currentUserId);

                dgvMyRequests.Rows.Clear();

                // Group by SubmissionSessionId so multi-model submissions appear as one row.
                // Requests without a SubmissionSessionId are treated as individual rows.
                var groups = requests
                    .GroupBy(r => r.SubmissionSessionId.HasValue
                        ? r.SubmissionSessionId.Value.ToString()
                        : $"__solo_{r.ReqId}")
                    .OrderByDescending(g => g.Max(r => r.DateCreated))
                    .ToList();

                foreach (var group in groups)
                {
                    var items = group.OrderBy(r => r.ReqId).ToList();
                    var first = items[0];

                    // Set Code always comes from dbo.Set (computed column: 'SET-XXXX').
                    // Pending requests with no Set assigned yet show "—".
                    string setCode = items.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.SetCode))?.SetCode
                                     ?? "—";

                    // Cartridge display
                    string cartridgeDisplay = items.Count == 1
                        ? (!string.IsNullOrWhiteSpace(first.CartridgeName) ? first.CartridgeName : first.ItemName)
                        : $"Multiple Models ({items.Count})";

                    int rowIndex = dgvMyRequests.Rows.Add();
                    var row = dgvMyRequests.Rows[rowIndex];

                    row.Cells["ReqId"].Value             = setCode;
                    row.Cells["DateRequested"].Value      = first.DateRequested.ToString("yyyy-MM-dd");
                    row.Cells["ItemName"].Value           = cartridgeDisplay;
                    row.Cells["Quantity"].Value           = items.Sum(x => x.Quantity);
                    row.Cells["ReturnInfo"].Value         = $"G: {items.Sum(x => x.GoodEmptyQty)} / D: {items.Sum(x => x.DamagedEmptyQty)}";
                    row.Cells["FulfillmentMethod"].Value  = first.FulfillmentMethod;
                    row.Cells["DestinationBranch"].Value  = first.DestinationBranch;
                    row.Cells["Status"].Value             = AggregateGroupStatus(items);

                    row.Tag = items;
                }

                lblRequestCount.Text = $"Total Submissions: {groups.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading requests: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string AggregateGroupStatus(List<PortalRequestStatusDto> items)
        {
            var statuses = items.Select(x => x.Status?.ToUpper() ?? "").Distinct().ToList();
            if (statuses.Count == 1) return items[0].Status;

            // Return the least-processed status in the group
            string[] priority = { "SUBMITTED", "UNDER REVIEW", "RECEIVED", "REPLACED", "FULFILLED", "COMPLETED", "CANCELLED", "REJECTED" };
            foreach (var p in priority)
                if (statuses.Contains(p)) return p;

            return items[0].Status;
        }

        private void DgvMyRequests_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var items = dgvMyRequests.Rows[e.RowIndex].Tag as List<PortalRequestStatusDto>;
            if (items == null || items.Count == 0) return;
            using (var dlg = new RequestDetailDialog(items))
                dlg.ShowDialog(this);
        }

        private void DgvMyRequests_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string status = dgvMyRequests.Rows[e.RowIndex].Cells["Status"].Value?.ToString() ?? string.Empty;

            var statusUpper = status.ToUpperInvariant();
            Color statusColor = Color.Black;
            if (statusUpper == "UNFULFILLED")
                statusColor = Color.FromArgb(220, 53, 69);
            else if (statusUpper.Contains("PARTIALLY"))
                statusColor = Color.FromArgb(255, 153, 0);
            else if (statusUpper.Contains("SUBMITTED") || statusUpper.Contains("UNDER REVIEW") || statusUpper.Contains("PROCESSING"))
                statusColor = Color.FromArgb(0, 123, 255);
            else if (statusUpper.Contains("RECEIVED"))
                statusColor = Color.FromArgb(255, 193, 7);
            else if (statusUpper.Contains("REPLACED") || statusUpper.Contains("COMPLETED"))
                statusColor = Color.FromArgb(40, 167, 69);
            else if (statusUpper.Contains("CANCELLED") || statusUpper.Contains("REJECTED"))
                statusColor = Color.FromArgb(220, 53, 69);

            if (dgvMyRequests.Columns[e.ColumnIndex].Name == "Status")
            {
                e.CellStyle.ForeColor = statusColor;
                e.CellStyle.Font = new Font(dgvMyRequests.Font, FontStyle.Bold);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Approvals Tab  (approver positions only — mirrors the NET portal)
        // ──────────────────────────────────────────────────────────────────────

        private void InitializeApprovalsTab()
        {
            var wpfView = new AuthorizeRequestView();
            var host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = wpfView
            };
            tabApprovals?.Controls.Add(host);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Approver Landing + Panel Navigation
        // ══════════════════════════════════════════════════════════════════════

        private Panel BuildLandingPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 246, 250) };

            var lblSub = new Label
            {
                Text      = "What would you like to do today?",
                Font      = new Font("Segoe UI", 11F),
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoSize  = true
            };

            var flp = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = true,
                AutoSize      = true,
                AutoSizeMode  = AutoSizeMode.GrowAndShrink,
                BackColor     = Color.Transparent,
                Padding       = new Padding(0)
            };

            _landingCardAuthorize = CreatePortalCard(
                "Authorize Cartridge Requests",
                "Review and sign off on cartridge request authorizations from your department.",
                "✏",
                Color.FromArgb(213, 0, 50),
                (s, e) => ShowApproverArea());
            flp.Controls.Add(_landingCardAuthorize);

            _landingCardSubmit = CreatePortalCard(
                "Submit a Request",
                "Submit a new cartridge request or track your previous requests.",
                "📋",
                Color.FromArgb(78, 154, 252),
                (s, e) => ShowRequestArea());
            flp.Controls.Add(_landingCardSubmit);

            _landingCardHistory = CreatePortalCard(
                "Authorization History",
                "View all your past cartridge authorizations — approved, rejected, and pending.",
                "🕓",
                Color.FromArgb(8, 145, 178),
                (s, e) => { ShowRequestArea(); tabControl.SelectedTab = tabAuthHistory; });
            flp.Controls.Add(_landingCardHistory);

            panel.Controls.Add(lblSub);
            panel.Controls.Add(flp);

            panel.Resize += (s, e) =>
            {
                lblSub.Location = new Point((panel.Width - lblSub.Width) / 2, 52);
                flp.Location    = new Point(Math.Max(0, (panel.Width - flp.Width) / 2), 96);
            };

            return panel;
        }

        private Panel BuildApproverAreaPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 246, 250) };

            // Nav bar with Home button
            var pnlNav = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 46,
                BackColor = Color.FromArgb(235, 237, 242),
                Padding   = new Padding(10, 6, 10, 6)
            };
            var btnHome = new Button
            {
                Text      = "⌂  Home",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(95, 32),
                Location  = new Point(10, 7),
                Cursor    = Cursors.Hand
            };
            btnHome.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            btnHome.Click += (s, e) => ShowLanding();
            pnlNav.Controls.Add(btnHome);

            // Content area — WPF AuthorizeRequestView hosted inside an ElementHost
            var pnlContent = new Panel { Dock = DockStyle.Fill };

            _approvalPage = new AuthorizeRequestView();

            var host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _approvalPage
            };
            pnlContent.Controls.Add(host);

            // Docking order: Fill first, then Top
            panel.Controls.Add(pnlContent);
            panel.Controls.Add(pnlNav);
            return panel;
        }

        private Panel BuildRequestAreaPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 246, 250) };

            // Nav bar with Home button
            var pnlNav = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 46,
                BackColor = Color.FromArgb(235, 237, 242),
                Padding   = new Padding(10, 6, 10, 6)
            };
            var btnHome = new Button
            {
                Text      = "⌂  Home",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(95, 32),
                Location  = new Point(10, 7),
                Cursor    = Cursors.Hand
            };
            btnHome.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            btnHome.Click += (s, e) => ShowLanding();
            pnlNav.Controls.Add(btnHome);

            tabControl.Dock = DockStyle.Fill;

            // Docking order: Fill first, then Top
            panel.Controls.Add(tabControl);
            panel.Controls.Add(pnlNav);
            return panel;
        }

        private Panel CreatePortalCard(string title, string desc, string icon, Color accent, EventHandler onClick)
        {
            var card = new Panel
            {
                Size      = new Size(340, 240),
                BackColor = Color.White,
                Cursor    = Cursors.Hand,
                Margin    = new Padding(20)
            };

            var bar = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = accent };

            var lblTitle = new Label
            {
                Text        = title,
                Font        = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor   = Color.FromArgb(64, 64, 64),
                Location    = new Point(20, 22),
                AutoSize    = true,
                MaximumSize = new Size(280, 0),
                BackColor   = Color.Transparent
            };

            var lblAction = new Label
            {
                Text      = "Click to Open →",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = accent,
                Location  = new Point(20, 195),
                AutoSize  = true,
                BackColor = Color.Transparent
            };

            var descFont = new Font("Segoe UI", 9.5F);
            var iconFont = new Font("Segoe UI Emoji", 52F);
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                using (var b = new SolidBrush(Color.Gray))
                    g.DrawString(desc, descFont, b, new RectangleF(22, 76, 270, 100),
                        new StringFormat { Trimming = StringTrimming.Word });
                using (var b = new SolidBrush(Color.FromArgb(218, 218, 218)))
                    g.DrawString(icon, iconFont, b, new PointF(230, 110));
            };

            card.Controls.Add(bar);
            card.Controls.Add(lblTitle);
            card.Controls.Add(lblAction);

            card.Click += onClick;
            foreach (Control c in card.Controls)
                c.Click += onClick;

            void SetHover(bool on)
            {
                card.BackColor   = on ? Color.FromArgb(248, 250, 252) : Color.White;
                card.BorderStyle = on ? BorderStyle.FixedSingle : BorderStyle.None;
                card.Invalidate();
            }

            card.MouseEnter += (s, e) => SetHover(true);
            card.MouseLeave += (s, e) => SetHover(false);
            foreach (Control c in card.Controls)
            {
                c.MouseEnter += (s, e) => SetHover(true);
                c.MouseLeave += (s, e) => SetHover(false);
            }

            return card;
        }

        private void ShowLanding()
        {
            _pnlApproverArea.Visible = false;
            _pnlRequestArea.Visible  = false;
            _pnlLanding.Visible      = true;
            _pnlLanding.BringToFront();
        }

        private void ShowApproverArea()
        {
            _pnlLanding.Visible      = false;
            _pnlRequestArea.Visible  = false;
            _pnlApproverArea.Visible = true;
            _pnlApproverArea.BringToFront();
        }

        private void ShowRequestArea()
        {
            _pnlLanding.Visible      = false;
            _pnlApproverArea.Visible = false;
            _pnlRequestArea.Visible  = true;
            _pnlRequestArea.BringToFront();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Authorization History Tab  (non-approver users)
        // ──────────────────────────────────────────────────────────────────────

        private void InitializeAuthHistoryTab()
        {
            _authHistoryViewModel = new AuthorizationHistoryViewModel();

            // When the user clicks a row, open the detail dialog
            _authHistoryViewModel.AuthorizationDetailRequested += authId =>
            {
                if (InvokeRequired)
                    BeginInvoke(new Action(() => OpenAuthDetailDialog(authId)));
                else
                    OpenAuthDetailDialog(authId);
            };

            _authHistoryViewRef = new AuthorizationHistoryView(_authHistoryViewModel);
            var host = new ElementHost { Dock = DockStyle.Fill, Child = _authHistoryViewRef };
            tabAuthHistory.Controls.Add(host);
        }

        private void OpenAuthDetailDialog(int authorizationId)
        {
            using (var dlg = new AuthorizationDetailDialog(authorizationId))
                dlg.ShowDialog(this);
        }
    }
}
