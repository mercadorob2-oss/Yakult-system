using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Security;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Forms.CallMonitoring;
using Yakult.Inventory.App.Forms.CallMonitoring.Hybrid;
using Yakult.Inventory.App.Pages.Update;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Wpf.CallMonitoring;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public partial class CallMonitoringDashboard : Form, ICallMonitoringNavigator
    {
        // UI Controls
        private Panel panelNav;
        private Label lblNavTitle;
        private Label lblNavSubtitle;
        private Button btnNavDashboard;
        private Button btnNavTickets;
        private Button btnNavIncomingTickets;
        private Button btnNavDisplay;
        private Button btnNavEmail;
        private Button btnNavReports;
        private Button btnNavDiagnostics;
        private Button btnNavProfiles;
        private Button btnNavLogout;

        private Panel panelContent;

        private Panel pnlHeader;
        private PictureBox picBrand;
        private Label lblHeaderTitle;
        private Label lblHeaderUser;
        private Button btnBackToPortal;
        private Button btnNotifications;
        private Label lblNotificationsBadge;
        private BackdropPanel pnlNotificationsOverlay;
        private Panel pnlNotificationsDrawerHost;
        private NotificationDrawerControl notificationDrawer;
        private System.Windows.Forms.Timer _notificationsAnimTimer;
        private bool _notificationsOpening;

        // Hosted Views
        private WpfCallMonitoringHostControl dashboardView;
        private WpfTicketManagementHostControl ticketManagementView;
        private WpfDisplayModeHostControl displayModeView;
        private WpfEmailNotificationHostControl emailNotificationView;
        private WpfReportsHostControl reportsView;
        private WpfDiagnosticsHostControl diagnosticsView;
        private WpfCallMonitoringProfilesHostControl profilesView;
        private WpfIncomingTicketsHostControl incomingTicketsView;

        private readonly ICallMonitoringRepository _callRepo = new CallMonitoringRepository();
        private readonly CallEmailNotificationService _callEmail;
        
        private bool _callSchemaMissingShown;
        private System.Threading.Timer _reminderTimer;
        private int _reminderRunGate;
        private int _notificationsAttentionCount;
        private bool _initialDashboardLoadQueued;

        public CallMonitoringDashboard()
        {
            _callEmail = new CallEmailNotificationService(_callRepo);
            InitializeComponent();
            
            // Wire up Load event
            this.Load += CallMonitoringDashboard_Load;
            this.Shown += CallMonitoringDashboard_Shown;
            this.FormClosed += (_, __) => StopReminderTimer();
            this.FormClosed += (_, __) => ItcmPresenceReporter.Stop();
            
            // Form properties
            this.KeyPreview = true;
            this.KeyDown += CallMonitoringDashboard_KeyDown;
        }

        private void InitializeComponent()
        {
            // 1. FORM SETUP
            this.SuspendLayout();
            this.AutoScaleMode = AutoScaleMode.Font;
            this.AutoScaleDimensions = new SizeF(7F, 17F);
            this.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular);
            this.ClientSize = new Size(1280, 720);
            this.Text = "IT Call Monitoring - Dashboard";
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 242, 245);
            this.MinimumSize = new Size(1024, 768);
            this.FormBorderStyle = FormBorderStyle.Sizable;

            // 2. HEADER
            this.pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.FromArgb(0, 150, 136),
                Padding = new Padding(16, 10, 16, 10)
            };
            
            var tlpHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            tlpHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var pnlHeaderLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = new Padding(0) };

            this.picBrand = new PictureBox
            {
                Size = new Size(200, 40),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 2, 14, 0)
            };
            this.picBrand.Image = TryLoadYakultNameLogo();
            this.picBrand.Visible = this.picBrand.Image != null;

            var flpHeaderLeft = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            this.lblHeaderTitle = new Label
            {
                Text = "IT CALL MONITORING",
                AutoSize = true,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 0, 0)
            };

            this.lblHeaderUser = new Label
            {
                Text = "Not signed in",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(230, 240, 240),
                Margin = new Padding(0, 2, 0, 0)
            };

            flpHeaderLeft.Controls.Add(this.lblHeaderTitle);
            flpHeaderLeft.Controls.Add(this.lblHeaderUser);

            var flpHeaderBrand = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            flpHeaderBrand.Controls.Add(this.picBrand);
            flpHeaderBrand.Controls.Add(flpHeaderLeft);
            pnlHeaderLeft.Controls.Add(flpHeaderBrand);

            var flpHeaderRight = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            this.btnBackToPortal = new Button
            {
                Text = "Back to Portal",
                Size = new Size(120, 32),
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0)
            };
            this.btnBackToPortal.FlatAppearance.BorderSize = 0;
            this.btnBackToPortal.Click += (s, e) => this.Close();

            var pnlNotifHost = new Panel
            {
                Size = new Size(46, 32),
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 12, 0)
            };

            this.btnNotifications = new Button
            {
                Text = "🔔",
                Size = new Size(40, 32),
                Location = new Point(0, 0),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(44, 62, 80),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            this.btnNotifications.FlatAppearance.BorderSize = 0;
            this.btnNotifications.Click += async (s, e) => await ToggleNotificationsAsync();

            this.lblNotificationsBadge = new Label
            {
                AutoSize = true,
                BackColor = Color.FromArgb(231, 76, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Padding = new Padding(6, 2, 6, 2),
                MinimumSize = new Size(16, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "0",
                Visible = false
            };

            pnlNotifHost.Controls.Add(this.btnNotifications);
            pnlNotifHost.Controls.Add(this.lblNotificationsBadge);
            pnlNotifHost.SizeChanged += (_, __) => PositionNotificationsBadge();
            PositionNotificationsBadge();

            flpHeaderRight.Controls.Add(this.btnBackToPortal);
            flpHeaderRight.Controls.Add(pnlNotifHost);

            tlpHeader.Controls.Add(pnlHeaderLeft, 0, 0);
            tlpHeader.Controls.Add(flpHeaderRight, 1, 0);

            this.pnlHeader.Controls.Add(tlpHeader);

            // 3. LEFT NAV (Sidebar)
            this.panelNav = new Panel
            {
                Name = "panelNav",
                Dock = DockStyle.Left,
                Width = 240,
                BackColor = Color.FromArgb(41, 58, 74),
                Padding = new Padding(0)
            };

            var panelNavHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(34, 49, 63),
                Padding = new Padding(20, 15, 0, 0)
            };

            this.lblNavTitle = new Label
            {
                Text = "CALL MONITORING",
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft
            };

            this.lblNavSubtitle = new Label
            {
                Text = "IT SUPPORT SYSTEM",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(149, 165, 166),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panelNavHeader.Controls.Add(this.lblNavSubtitle);
            panelNavHeader.Controls.Add(this.lblNavTitle);

            var pnlNavButtons = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };

            this.btnNavDashboard = CreateNavButton("Dashboard", "📊");
            this.btnNavDashboard.Click += (s, e) => { SetActiveNav(this.btnNavDashboard); ShowDashboardView(); };

            this.btnNavTickets = CreateNavButton("Ticket List", "🎫");
            this.btnNavTickets.Click += (s, e) => { SetActiveNav(this.btnNavTickets); ShowTicketListView(); };

            this.btnNavIncomingTickets = CreateNavButton("Incoming Tickets", "📥");
            this.btnNavIncomingTickets.Click += (s, e) => { SetActiveNav(this.btnNavIncomingTickets); ShowIncomingTicketsView(); };

            this.btnNavDisplay = CreateNavButton("Display Mode", "▣");
            this.btnNavDisplay.Click += (s, e) => { SetActiveNav(this.btnNavDisplay); ShowDisplayModeView(); };
            this.btnNavDisplay.Dock = DockStyle.Bottom;
            this.btnNavDisplay.BackColor = Color.FromArgb(34, 49, 63);

            this.btnNavEmail = CreateNavButton("Email Notification", "📩");
            this.btnNavEmail.Click += (s, e) => { SetActiveNav(this.btnNavEmail); ShowEmailNotificationView(); };

            this.btnNavReports = CreateNavButton("Reports", "📊");
            this.btnNavReports.Click += (s, e) => { SetActiveNav(this.btnNavReports); ShowReportsView(); };

            this.btnNavDiagnostics = CreateNavButton("Diagnostics", "🩺");
            this.btnNavDiagnostics.Click += (s, e) => { SetActiveNav(this.btnNavDiagnostics); ShowDiagnosticsView(); };
            this.btnNavDiagnostics.Dock = DockStyle.Bottom;
            this.btnNavDiagnostics.BackColor = Color.FromArgb(34, 49, 63);

            this.btnNavProfiles = CreateNavButton("Profiles", "👤");
            this.btnNavProfiles.Click += (s, e) => { SetActiveNav(this.btnNavProfiles); ShowProfilesView(); };

            pnlNavButtons.Controls.Add(this.btnNavEmail);
            pnlNavButtons.Controls.Add(this.btnNavProfiles);
            pnlNavButtons.Controls.Add(this.btnNavReports);
            pnlNavButtons.Controls.Add(this.btnNavTickets);
            pnlNavButtons.Controls.Add(this.btnNavIncomingTickets);
            pnlNavButtons.Controls.Add(this.btnNavDashboard);

            this.btnNavLogout = CreateNavButton("Logout", "🚪");
            this.btnNavLogout.Dock = DockStyle.Bottom;
            this.btnNavLogout.BackColor = Color.FromArgb(34, 49, 63);
            this.btnNavLogout.Click += btnNavLogout_Click;

            this.panelNav.Controls.Add(pnlNavButtons);
            this.panelNav.Controls.Add(this.btnNavDisplay);
            this.panelNav.Controls.Add(this.btnNavDiagnostics);
            this.panelNav.Controls.Add(this.btnNavLogout);
            this.panelNav.Controls.Add(panelNavHeader);

            // 4. MAIN CONTENT PANEL
            this.panelContent = new Panel
            {
                Name = "panelContent",
                Dock = DockStyle.Fill,
                BackColor = this.BackColor
            };

            // Views Setup
            
            // DashboardView (WPF hosted inside WinForms)
            this.dashboardView = new WpfCallMonitoringHostControl();
            this.dashboardView.Initialize(_callRepo, this);
            this.dashboardView.Dock = DockStyle.Fill;
            this.dashboardView.Visible = true; // Default
            this.panelContent.Controls.Add(this.dashboardView);

            this.Controls.Add(this.panelContent);
            this.Controls.Add(this.panelNav);
            this.Controls.Add(this.pnlHeader);

            // 5. NOTIFICATIONS OVERLAY (Right Drawer)
            this.pnlNotificationsOverlay = new BackdropPanel
            {
                Dock = DockStyle.Fill,
                Visible = false
            };

            this.pnlNotificationsDrawerHost = new Panel
            {
                Width = 560,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right
            };

            this.notificationDrawer = new NotificationDrawerControl
            {
                Dock = DockStyle.Fill
            };
            this.notificationDrawer.Initialize(_callRepo);
            this.notificationDrawer.CloseRequested += CloseNotifications;
            this.notificationDrawer.AttentionCountChanged += count => SetNotificationsBadge(count);
            this.notificationDrawer.OpenTicketAsync = async ticketId =>
            {
                CloseNotifications();
                OpenTicketDetailsDialog(ticketId);
                await Task.CompletedTask;
            };
            this.notificationDrawer.OpenTicketListAsync = () =>
            {
                CloseNotifications();
                SetActiveNav(this.btnNavTickets);
                ShowTicketListView();
                return Task.CompletedTask;
            };
            this.notificationDrawer.AfterMutationAsync = async () =>
            {
                await RefreshNotificationsBadgeAsync();
                if (this.ticketManagementView != null)
                    _ = this.ticketManagementView.LoadDataAsync();
                if (this.displayModeView != null)
                    _ = this.displayModeView.LoadDataAsync(force: true);
                this.dashboardView.RefreshData();
            };

            this.pnlNotificationsDrawerHost.Controls.Add(this.notificationDrawer);
            this.pnlNotificationsOverlay.Controls.Add(this.pnlNotificationsDrawerHost);
            this.pnlNotificationsOverlay.MouseDown += (_, __) => CloseNotifications();
            this.Controls.Add(this.pnlNotificationsOverlay);
            this.pnlNotificationsOverlay.BringToFront();

            this._notificationsAnimTimer = new System.Windows.Forms.Timer { Interval = 15 };
            this._notificationsAnimTimer.Tick += (_, __) => AnimateNotificationsDrawer();

            this.Resize += (_, __) => LayoutNotificationsDrawer();
            LayoutNotificationsDrawer();

            this.ResumeLayout(false);
        }

        // --- NAVIGATION HELPERS ---

        private Button CreateNavButton(string text, string icon = "")
        {
            var btn = new Button
            {
                Text = $"   {icon}   {text}",
                Dock = DockStyle.Top,
                Height = 50,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(189, 195, 199),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular)
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 73, 94);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(44, 62, 80);
            return btn;
        }

        private void SetActiveNav(Button activeBtn)
        {
            foreach (Control c in this.panelNav.Controls)
            {
                if (c is Panel p)
                {
                    foreach(Control child in p.Controls)
                    {
                        if (child is Button b) ResetNavButton(b);
                    }
                }
                else if (c is Button b)
                {
                    ResetNavButton(b);
                }
            }

            if (activeBtn != null)
            {
                activeBtn.ForeColor = Color.White;
                activeBtn.BackColor = Color.FromArgb(52, 73, 94);
                activeBtn.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            }
        }

        private void ResetNavButton(Button btn)
        {
            if (btn == this.btnNavLogout) return;

            btn.ForeColor = Color.FromArgb(189, 195, 199);
            btn.BackColor = Color.Transparent;
            btn.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
        }

        private WpfTicketManagementHostControl EnsureTicketManagementView()
        {
            if (this.ticketManagementView != null && !this.ticketManagementView.IsDisposed)
                return this.ticketManagementView;

            this.ticketManagementView = new WpfTicketManagementHostControl();
            this.ticketManagementView.Initialize(_callRepo);
            this.ticketManagementView.Dock = DockStyle.Fill;
            this.ticketManagementView.Visible = false;
            this.panelContent.Controls.Add(this.ticketManagementView);
            return this.ticketManagementView;
        }

        private WpfDisplayModeHostControl EnsureDisplayModeView()
        {
            if (this.displayModeView != null && !this.displayModeView.IsDisposed)
                return this.displayModeView;

            this.displayModeView = new WpfDisplayModeHostControl(_callRepo, this)
            {
                Dock = DockStyle.Fill,
                Visible = false
            };
            this.panelContent.Controls.Add(this.displayModeView);
            return this.displayModeView;
        }

        private WpfEmailNotificationHostControl EnsureEmailNotificationView()
        {
            if (this.emailNotificationView != null && !this.emailNotificationView.IsDisposed)
                return this.emailNotificationView;

            this.emailNotificationView = new WpfEmailNotificationHostControl(_callRepo)
            {
                Dock = DockStyle.Fill,
                Visible = false
            };
            this.panelContent.Controls.Add(this.emailNotificationView);
            this.emailNotificationView.Visible = false;
            return this.emailNotificationView;
        }

        private WpfReportsHostControl EnsureReportsView()
        {
            if (this.reportsView != null && !this.reportsView.IsDisposed)
                return this.reportsView;

            this.reportsView = new WpfReportsHostControl(_callRepo, this)
            {
                Dock = DockStyle.Fill,
                Visible = false
            };
            this.panelContent.Controls.Add(this.reportsView);
            this.reportsView.Visible = false;
            return this.reportsView;
        }

        private WpfDiagnosticsHostControl EnsureDiagnosticsView()
        {
            if (this.diagnosticsView != null && !this.diagnosticsView.IsDisposed)
                return this.diagnosticsView;

            this.diagnosticsView = new WpfDiagnosticsHostControl(_callRepo, this)
            {
                Dock = DockStyle.Fill,
                Visible = false
            };
            this.panelContent.Controls.Add(this.diagnosticsView);
            return this.diagnosticsView;
        }

        private WpfCallMonitoringProfilesHostControl EnsureProfilesView()
        {
            if (this.profilesView != null && !this.profilesView.IsDisposed)
                return this.profilesView;

            this.profilesView = new WpfCallMonitoringProfilesHostControl();
            this.profilesView.Initialize(_callRepo, itDepartmentName: null, this);
            this.profilesView.Dock = DockStyle.Fill;
            this.profilesView.Visible = false;
            this.panelContent.Controls.Add(this.profilesView);
            return this.profilesView;
        }

        // --- VIEW SWITCHING ---

        /// <summary>Hides every hosted view, then shows and brings <paramref name="next"/> to the front.</summary>
        private void SwitchView(Control next)
        {
            foreach (Control c in this.panelContent.Controls)
                c.Visible = false;

            if (next == null) return;
            next.Visible = true;
            next.BringToFront();
        }

        /// <summary>Runs a Task in a fire-and-forget manner, logging any exception instead of silently swallowing it.</summary>
        private static void SafeFireAndForget(Task task)
        {
            if (task == null) return;
            task.ContinueWith(
                t => Core.Logger.LogError("[CallMonitoringDashboard] Background task failed.", t.Exception?.GetBaseException()),
                System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        }

        private void ShowDashboardView(bool refresh = true)
        {
            if (!PermissionResolver.HasPageAccess("CallDashboardView"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (refresh)
                this.dashboardView.RefreshData();

            SwitchView(this.dashboardView);
            this.lblNavTitle.Text = "DASHBOARD";
            this.lblNavSubtitle.Text = "OVERVIEW";
        }

        private void ShowTicketListView()
        {
            if (!PermissionResolver.HasPageAccess("TicketListView"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var ticketView = EnsureTicketManagementView();
            SafeFireAndForget(ticketView.LoadDataAsync());
            SwitchView(ticketView);
            this.lblNavTitle.Text = "TICKET MANAGEMENT";
            this.lblNavSubtitle.Text = "CREATE & MANAGE";
        }

        private void ShowDisplayModeView()
        {
            if (!PermissionResolver.HasPageAccess("DisplayModeView"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var displayView = EnsureDisplayModeView();
            SafeFireAndForget(displayView.LoadDataAsync(force: true));
            SwitchView(displayView);
            this.lblNavTitle.Text = "DISPLAY MODE";
            this.lblNavSubtitle.Text = "LIVE OPEN TICKETS";
        }

        private void ShowEmailNotificationView()
        {
            if (!PermissionResolver.HasPageAccess("EmailNotificationView"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var emailView = EnsureEmailNotificationView();
            SwitchView(emailView);
            this.lblNavTitle.Text = "EMAIL NOTIFICATIONS";
            this.lblNavSubtitle.Text = "STATUS & LOGS";
        }

        private void ShowReportsView()
        {
            if (!PermissionResolver.HasPageAccess("CallReportsView"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var reportsView = EnsureReportsView();
            SafeFireAndForget(reportsView.LoadDataAsync());
            SwitchView(reportsView);
            this.lblNavTitle.Text = "REPORTS";
            this.lblNavSubtitle.Text = "ANALYTICS & METRICS";
        }

        private void ShowDiagnosticsView()
        {
            if (!PermissionResolver.HasPageAccess("DiagnosticsView"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var diagnosticsView = EnsureDiagnosticsView();
            SwitchView(diagnosticsView);
            this.lblNavTitle.Text = "DIAGNOSTICS";
            this.lblNavSubtitle.Text = "SUPPORT & HEALTH";
        }

        public Task NavigateToReportsAsync(DateTime fromLocal, DateTime toLocal, string type)
        {
            if (this.IsDisposed)
                return Task.CompletedTask;

            if (this.InvokeRequired)
            {
                var tcs = new TaskCompletionSource<object>();
                this.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await NavigateToReportsAsync(fromLocal, toLocal, type);
                        tcs.SetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }));
                return tcs.Task;
            }

            SetActiveNav(this.btnNavReports);
            ShowReportsView();

            return this.reportsView != null
                ? this.reportsView.ApplyFilterAsync(fromLocal, toLocal, type)
                : Task.CompletedTask;
        }
         
        private void ShowProfilesView()
        {
            if (!PermissionResolver.HasPageAccess("CallProfilesView"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var profilesView = EnsureProfilesView();
            SwitchView(profilesView);
            this.lblNavTitle.Text = "EMPLOYEE PROFILES";
            this.lblNavSubtitle.Text = "HISTORY & ASSETS";
        }

        private WpfIncomingTicketsHostControl EnsureIncomingTicketsView()
        {
            if (this.incomingTicketsView != null && !this.incomingTicketsView.IsDisposed)
                return this.incomingTicketsView;

            this.incomingTicketsView = new WpfIncomingTicketsHostControl();
            this.incomingTicketsView.Initialize(_callRepo, this);
            this.incomingTicketsView.Dock = DockStyle.Fill;
            this.incomingTicketsView.Visible = false;
            this.panelContent.Controls.Add(this.incomingTicketsView);
            return this.incomingTicketsView;
        }

        private void ShowIncomingTicketsView()
        {
            if (!PermissionResolver.HasPageAccess("IncomingTicketsView"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var view = EnsureIncomingTicketsView();
            SafeFireAndForget(view.LoadDataAsync());
            SwitchView(view);
            this.lblNavTitle.Text = "INCOMING TICKETS";
            this.lblNavSubtitle.Text = "PORTAL SUBMISSIONS";
        }

        // --- ICallMonitoringNavigator ---

        public void OpenDashboard() => ShowDashboardView();
        public void OpenTicketWorkspace() { SetActiveNav(this.btnNavTickets); ShowTicketListView(); }
        public void OpenIncomingTickets() { SetActiveNav(this.btnNavIncomingTickets); ShowIncomingTicketsView(); }
        public void OpenDisplayMode() { SetActiveNav(this.btnNavDisplay); ShowDisplayModeView(); }
        public void OpenReports() { SetActiveNav(this.btnNavReports); ShowReportsView(); }
        public void OpenMobileUpdates() => OpenMobileUpdatesDialog();
        public void OpenEmailNotifications() { SetActiveNav(this.btnNavEmail); ShowEmailNotificationView(); }
        public void OpenDiagnostics() { SetActiveNav(this.btnNavDiagnostics); ShowDiagnosticsView(); }
        public void OpenProfiles() { SetActiveNav(this.btnNavProfiles); ShowProfilesView(); }
        public void OpenTicket(int ticketId) => OpenTicketDetailsDialog(ticketId);

        public void OpenEmailDeepLink(WpfEmailNotificationDeepLinkTarget target, int? deptId = null, int? branchId = null, string emailLogSearch = null)
        {
            try
            {
                SetActiveNav(this.btnNavEmail);
                ShowEmailNotificationView();
                this.emailNotificationView?.NavigateTo(target, deptId: deptId, branchId: branchId, emailLogSearch: emailLogSearch);
            }
            catch { }
        }

        public void OpenSmtpTest()
        {
            try
            {
                using (var dlg = new SmtpTestDialog(_callRepo, null))
                    dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SMTP Test", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --- FORM EVENTS ---

        private void CallMonitoringDashboard_Load(object sender, EventArgs e)
        {
            if (Yakult.Inventory.App.Session.AppSession.IsLoggedIn)
            {
                var role = Yakult.Inventory.App.Session.AppSession.IsAdmin ? "Admin" : 
                           (Yakult.Inventory.App.Session.AppSession.IsDeveloper ? "Developer" : string.Join(",", Yakult.Inventory.App.Session.AppSession.CurrentUserRoles));
                this.lblHeaderUser.Text = $"User: {Yakult.Inventory.App.Session.AppSession.CurrentUserName} ({role})";
            }
            SetActiveNav(this.btnNavDashboard);
            // Default view is Dashboard
            ShowDashboardView(refresh: false);
        }

        private void CallMonitoringDashboard_Shown(object sender, EventArgs e)
        {
            if (_initialDashboardLoadQueued)
                return;

            _initialDashboardLoadQueued = true;
            BeginInvoke((Action)(() => _ = LoadInitialDashboardStateAsync()));
        }

        private async Task LoadInitialDashboardStateAsync()
        {
            await LoadCallMonitoringDataAsync();
            await RefreshNotificationsBadgeAsync();
            StartReminderTimerIfEligible();
            ItcmPresenceReporter.Start();
        }

        private void CallMonitoringDashboard_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (IsNotificationsOpen())
                {
                    CloseNotifications();
                    e.Handled = true;
                    return;
                }

                // Optional: Confirm exit?
            }
        }

        private void btnNavLogout_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to logout?", "Logout", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                this.Close();
            }
        }

        // --- NOTIFICATIONS (Overlay Drawer) ---

        private bool IsNotificationsOpen()
        {
            return this.pnlNotificationsOverlay != null && this.pnlNotificationsOverlay.Visible;
        }

        private async Task ToggleNotificationsAsync()
        {
            if (IsNotificationsOpen())
            {
                CloseNotifications();
                return;
            }

            await OpenNotificationsAsync();
        }

        private async Task OpenNotificationsAsync()
        {
            if (this.pnlNotificationsOverlay == null || this.pnlNotificationsDrawerHost == null || this.notificationDrawer == null)
                return;

            // Ensure correct sizing before the first open (Docked + hidden controls may report Width/Height = 0).
            this.pnlNotificationsOverlay.Bounds = this.ClientRectangle;

            LayoutNotificationsDrawer();

            TryCaptureNotificationsBackdrop();

            this.pnlNotificationsOverlay.Visible = true;
            this.pnlNotificationsOverlay.BringToFront();

            _notificationsOpening = true;
            this.pnlNotificationsDrawerHost.Left = Math.Max(1, this.ClientSize.Width);
            this._notificationsAnimTimer?.Start();

            try
            {
                await this.notificationDrawer.RefreshAsync();
            }
            catch
            {
                // Best-effort: drawer handles its own errors.
            }
        }

        private void CloseNotifications()
        {
            if (this.pnlNotificationsOverlay == null || this.pnlNotificationsDrawerHost == null)
                return;

            if (!this.pnlNotificationsOverlay.Visible)
                return;

            _notificationsOpening = false;
            this._notificationsAnimTimer?.Start();
        }

        private void LayoutNotificationsDrawer()
        {
            if (this.pnlNotificationsOverlay == null || this.pnlNotificationsDrawerHost == null)
                return;

            // Width: extend the drawer further left (roughly ~45% of the app width) while keeping a small
            // click-outside strip so the user can close it easily.
            var overlayWidth = Math.Max(1, this.ClientSize.Width);
            const int minWidth = 560;
            const int minOutsideClickSpace = 80;
            const float targetFraction = 0.50f; // ~20% narrower than 0.62

            var preferredWidth = (int)Math.Round(overlayWidth * targetFraction);
            var maxWidth = Math.Max(minWidth, overlayWidth - minOutsideClickSpace);

            var width = preferredWidth;
            if (width < minWidth) width = minWidth;
            if (width > maxWidth) width = maxWidth;
            if (width > overlayWidth) width = overlayWidth;
            if (width < 360) width = 360; // absolute safety for tiny windows

            this.pnlNotificationsDrawerHost.Width = width;

            this.pnlNotificationsDrawerHost.Top = 0;
            this.pnlNotificationsDrawerHost.Height = Math.Max(1, this.ClientSize.Height);

            // Keep it off-screen if overlay is closed.
            if (!this.pnlNotificationsOverlay.Visible)
            {
                this.pnlNotificationsDrawerHost.Left = overlayWidth;
                return;
            }

            // If we're visible but not animating, snap to the open position.
            if (this._notificationsAnimTimer == null || !this._notificationsAnimTimer.Enabled)
            {
                this.pnlNotificationsDrawerHost.Left = Math.Max(0, overlayWidth - this.pnlNotificationsDrawerHost.Width);
            }
        }

        private void AnimateNotificationsDrawer()
        {
            if (this.pnlNotificationsOverlay == null || this.pnlNotificationsDrawerHost == null || this._notificationsAnimTimer == null)
                return;

            var overlayWidth = Math.Max(1, this.ClientSize.Width);
            var openLeft = Math.Max(0, overlayWidth - this.pnlNotificationsDrawerHost.Width);
            var closedLeft = overlayWidth;
            var step = 44;

            if (_notificationsOpening)
            {
                this.pnlNotificationsDrawerHost.Left = Math.Max(openLeft, this.pnlNotificationsDrawerHost.Left - step);
                if (this.pnlNotificationsDrawerHost.Left <= openLeft)
                {
                    this.pnlNotificationsDrawerHost.Left = openLeft;
                    this._notificationsAnimTimer.Stop();
                }
            }
            else
            {
                this.pnlNotificationsDrawerHost.Left = Math.Min(closedLeft, this.pnlNotificationsDrawerHost.Left + step);
                if (this.pnlNotificationsDrawerHost.Left >= closedLeft)
                {
                    this.pnlNotificationsDrawerHost.Left = closedLeft;
                    this._notificationsAnimTimer.Stop();
                    this.pnlNotificationsOverlay.Visible = false;
                    ClearNotificationsBackdrop();
                }
            }
        }

        private void TryCaptureNotificationsBackdrop()
        {
            try
            {
                // Snapshot the dashboard before the overlay is shown.
                if (this.pnlNotificationsOverlay == null)
                    return;

                var w = Math.Max(1, this.ClientSize.Width);
                var h = Math.Max(1, this.ClientSize.Height);

                using (var bmp = new Bitmap(w, h))
                {
                    // DrawToBitmap is best-effort (some controls may not render perfectly).
                    this.DrawToBitmap(bmp, new Rectangle(Point.Empty, new Size(w, h)));
                    this.pnlNotificationsOverlay.SetSnapshot((Bitmap)bmp.Clone());
                }

                this.pnlNotificationsOverlay.Invalidate();
            }
            catch
            {
                // Best-effort.
                ClearNotificationsBackdrop();
            }
        }

        private void ClearNotificationsBackdrop()
        {
            try { this.pnlNotificationsOverlay?.SetSnapshot(null); } catch { }
        }

        private async Task RefreshNotificationsBadgeAsync()
        {
            if (this.IsDisposed || _callRepo == null)
                return;

            try
            {
                if (!await _callRepo.CallSchemaExistsAsync())
                {
                    SetNotificationsBadge(0);
                    return;
                }

                // Stateless approximation: "requires attention" list is capped.
                const int max = 150;
                var rows = await _callRepo.GetRequiresAttentionAsync(maxRows: max);
                var count = rows?.Count ?? 0;
                SetNotificationsBadge(count >= max ? max : count);
            }
            catch
            {
                // Silent: notifications are auxiliary.
                SetNotificationsBadge(0);
            }
        }

        private void SetNotificationsBadge(int count)
        {
            _notificationsAttentionCount = Math.Max(0, count);

            if (this.IsDisposed || this.lblNotificationsBadge == null)
                return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SetNotificationsBadge(count)));
                return;
            }

            if (count <= 0)
            {
                this.lblNotificationsBadge.Visible = false;
                return;
            }

            this.lblNotificationsBadge.Text = count > 99 ? "99+" : count.ToString();
            this.lblNotificationsBadge.Visible = true;
            PositionNotificationsBadge();
        }

        private void PositionNotificationsBadge()
        {
            if (this.lblNotificationsBadge == null || this.lblNotificationsBadge.Parent == null)
                return;

            var host = this.lblNotificationsBadge.Parent;
            this.lblNotificationsBadge.BringToFront();
            this.lblNotificationsBadge.AutoSize = true;

            var x = Math.Max(0, host.ClientSize.Width - this.lblNotificationsBadge.Width - 2);
            this.lblNotificationsBadge.Location = new Point(x, -2);
        }

        // --- DATA LOADING & ACTIONS ---

        public async Task LoadCallMonitoringDataAsync()
        {
            try
            {
                // Check Schema
                if (!await _callRepo.CallSchemaExistsAsync())
                {
                    if (!_callSchemaMissingShown)
                    {
                        var msg = "Call Monitoring Schema (tables) missing. Please run database migration scripts.";
                        MessageBox.Show(msg, "System Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        _callSchemaMissingShown = true;
                    }
                    return;
                }

                // Load Dashboard
                this.dashboardView.RefreshData();
                
                // Pre-load Ticket View? Optional. Let's do it to be snappy.
                // await this.ticketManagementView.LoadDataAsync(); 
            }
            catch (Exception ex)
            {
                 Logger.LogError("[CallMonitoringDashboard] Error loading data.", ex);
                 MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartReminderTimerIfEligible()
        {
            try
            {
                // ── MIGRATION NOTE ───────────────────────────────────────────────────────────
                // Yakult.ITCM.Server is now the single scheduler owner. This in-app
                // timer is a FALLBACK only (ItcmServerOwned=false in App.config) for
                // environments where the server has not yet been deployed. The SQL
                // application lock inside
                // ProcessRemindersAndAutoEscalationsWithGlobalLockAsync() prevents duplicate
                // runs, so it is safe to have both active simultaneously.
                // ─────────────────────────────────────────────────────────────────────────────

                // Server owns the schedule by default; keep the client timer off.
                if (AppConfig.ItcmServerOwned)
                    return;

                // Run reminders only from a single "operator" UI (Admin/Developer) to avoid duplicates on every client.
                if (!AppSession.IsLoggedIn)
                    return;

                if (!AppSession.IsAdmin && !AppSession.IsDeveloper)
                    return;

                if (_reminderTimer != null)
                    return;

                // Check every 15 minutes; reminder eligibility itself is day-based (ReminderDays).
                var due = TimeSpan.FromMinutes(2);
                var period = TimeSpan.FromMinutes(15);
                Yakult.Inventory.App.Services.CallEmailNotificationService.SetNextBackgroundRunUtc(DateTime.UtcNow.Add(due));
                _reminderTimer = new System.Threading.Timer(
                    _ =>
                    {
                        if (Interlocked.Exchange(ref _reminderRunGate, 1) == 1)
                            return;

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                 Yakult.Inventory.App.Services.CallEmailNotificationService.SetNextBackgroundRunUtc(DateTime.UtcNow.Add(period));
                                 // Schema check is inside repo calls; keep this best-effort.
                                 var userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                                 await _callEmail.ProcessRemindersAndAutoEscalationsWithGlobalLockAsync(maxTickets: 20, triggeredByUserId: userId);
                             }
                             catch (Exception ex)
                             {
                                 Logger.LogError("[CallMonitoringDashboard] Reminder/escalation background run failed.", ex);
                             }
                            finally
                            {
                                Interlocked.Exchange(ref _reminderRunGate, 0);
                            }
                        });
                    },
                    null,
                    dueTime: due,
                    period: period);
            }
            catch (Exception ex)
            {
                Logger.LogError("[CallMonitoringDashboard] Failed to start reminder timer.", ex);
                StopReminderTimer();
            }
        }

        private void StopReminderTimer()
        {
            try
            {
                _reminderTimer?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[CallMonitoringDashboard] Failed to dispose reminder timer: {ex.Message}");
            }
            finally
            {
                _reminderTimer = null;
            }
        }
        
        // Exposed for Report/Profile views to open details
        public void OpenTicketDetailsDialog(int ticketId)
        {
            using (var dlg = new Yakult.Inventory.App.Forms.CallMonitoring.TicketDetailsDialog(_callRepo, ticketId))
            {
                dlg.ShowDialog(this);
                // If details changed, refresh views
                if (this.ticketManagementView != null)
                    _ = this.ticketManagementView.LoadDataAsync();
                if (this.displayModeView != null)
                    _ = this.displayModeView.LoadDataAsync(force: true);
                this.dashboardView.RefreshData();
            }
        }

        private void OpenMobileUpdatesDialog()
        {
            try
            {
                using (var form = new Form
                {
                    Text = "Mobile Updates",
                    StartPosition = FormStartPosition.CenterParent,
                    Size = new Size(1200, 750),
                    MinimumSize = new Size(1024, 650)
                })
                {
                    var page = new ViewUpdatesPage
                    {
                        Dock = DockStyle.Fill
                    };

                    form.Controls.Add(page);
                    form.ShowDialog(this);
                }

                this.dashboardView?.RefreshData();
            }
            catch (Exception ex)
            {
                Logger.LogError("[CallMonitoringDashboard] Unable to open Mobile Updates.", ex);
                MessageBox.Show($"Unable to open Mobile Updates.\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public async Task NavigateToTicketAsync(int ticketId)
        {
             // Switch to ticket view
             SetActiveNav(this.btnNavTickets);
             ShowTicketListView();
             
             // Delegate selection to the control
             await EnsureTicketManagementView().SelectTicketAsync(ticketId);
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

        private sealed class BackdropPanel : Panel
        {
            public int OverlayAlpha { get; set; } = 140;
            private Bitmap _snapshot;

            public BackdropPanel()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
                this.UpdateStyles();
                this.BackColor = Color.Transparent;
            }

            public void SetSnapshot(Bitmap snapshot)
            {
                var old = _snapshot;
                _snapshot = snapshot;
                if (old != null)
                {
                    try { old.Dispose(); } catch { }
                }
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                if (_snapshot != null)
                {
                    e.Graphics.DrawImage(_snapshot, this.ClientRectangle);
                }
                else
                {
                    base.OnPaintBackground(e);
                }

                using (var b = new SolidBrush(Color.FromArgb(OverlayAlpha, 0, 0, 0)))
                {
                    e.Graphics.FillRectangle(b, this.ClientRectangle);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    try { _snapshot?.Dispose(); } catch { }
                    _snapshot = null;
                }

                base.Dispose(disposing);
            }
        }
    }
}
