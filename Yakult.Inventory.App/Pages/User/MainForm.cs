using System.Configuration;
using System.Data.SqlClient;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Wpf.Notifications;
using Yakult.Inventory.App.Pages.Archive;
using Yakult.Inventory.App.Pages.Branch;
using Yakult.Inventory.App.Pages.BranchAssignment;
using Yakult.Inventory.App.Pages.Cartridge;
using Yakult.Inventory.App.Pages.Category;
using Yakult.Inventory.App.Pages.Company;
using Yakult.Inventory.App.Pages.Department;
using Yakult.Inventory.App.Pages.Employee;
using Yakult.Inventory.App.Pages.FixedAsset;
using Yakult.Inventory.App.Pages.Inventory;
using Yakult.Inventory.App.Pages.Invoice;
using Yakult.Inventory.App.Pages.Item;
using Yakult.Inventory.App.Pages.ItemAudit;
using Yakult.Inventory.App.Pages.Receipt;
using Yakult.Inventory.App.Pages.Renewal;
using Yakult.Inventory.App.Pages.Request;
using Yakult.Inventory.App.Pages.Set;
using Yakult.Inventory.App.Pages.Update;
using Yakult.Inventory.App.Pages.Vendor;
using Yakult.Inventory.App.Pages.Warranty;
using Yakult.Inventory.App.Forms.CallMonitoring;
using Yakult.Inventory.App.Forms.BorrowItems;
using Yakult.Inventory.App.Forms.Dashboard;
using Yakult.Inventory.App.Forms.Portal;
using Yakult.Inventory.App.Forms.CartridgeManagement;
using Yakult.Inventory.App.Forms.Reports;
using Yakult.Inventory.App.Forms.Admin;
using Yakult.Inventory.App.Forms.Admin.AccountManagement;
using Yakult.Inventory.App.Forms.Notifications;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Net.Http;
using Newtonsoft.Json;
using ReaLTaiizor.Controls;
using Yakult.Inventory.App.Pages.RequestPortal;
using Yakult.Inventory.App.Pages.Admin.AccountManagement;
using Yakult.Inventory.App.Pages.Software;

namespace Yakult.Inventory.App.Pages.User
{
    public partial class MainForm : Form
    {
        // API URL now comes from centralized config (App.config -> AppConfig.ApiHealthUrl)
        private static string ApiHealthUrl => AppConfig.ApiHealthUrl;

        private static readonly HttpClient _connectionHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        private int _isCheckingConnection;

        // Hamburger menu fields
        private System.Windows.Forms.Panel sideMenuPanel;
        private System.Windows.Forms.Panel sideMenuContent;
        private System.Windows.Forms.Panel menuOverlay;
        private System.Windows.Forms.Button hamburgerButton;
        private bool isMenuOpen = false;
        private const int MENU_WIDTH = 320;
        private bool isAddMasterDataExpanded = false;
        private bool isViewMasterDataExpanded = false;
        private bool isViewTransactionsExpanded = false;
        private bool isCartridgeManagementExpanded = false;
        private bool isViewHistoryExpanded = false;
        private bool isAccountManagementExpanded = false;
        private bool isEmailSmtpExpanded = false;
        private bool isAdminExpanded = false;
        private System.Windows.Forms.Panel addMasterDataPanel;
        private System.Windows.Forms.Panel viewMasterDataPanel;
        private System.Windows.Forms.Panel viewTransactionsPanel;
        private System.Windows.Forms.Panel cartridgeManagementPanel;
        private System.Windows.Forms.Panel viewHistoryPanel;
        private System.Windows.Forms.Panel accountManagementPanel;
        private System.Windows.Forms.Panel emailSmtpPanel;
        private System.Windows.Forms.Panel adminPanel;
        private System.Windows.Forms.Button addMasterDataButton;
        private System.Windows.Forms.Button viewMasterDataButton;
        private System.Windows.Forms.Button viewTransactionsButton;
        private System.Windows.Forms.Button cartridgeManagementButton;
        private System.Windows.Forms.Button viewHistoryButton;
        private System.Windows.Forms.Button accountManagementButton;
        private System.Windows.Forms.Button emailSmtpButton;
        private System.Windows.Forms.Button adminButton;
        private System.Windows.Forms.Button homeButton;
        private System.Windows.Forms.Label menuHeaderLabel;
        private System.Windows.Forms.Panel menuSeparator;
        private System.Windows.Forms.Button logoutButton;
        private System.Windows.Forms.Button resetPasswordButton;
        private System.Windows.Forms.Button myAccountButton;
        private System.Windows.Forms.Button notifSettingsButton;
        private System.Windows.Forms.Button howToUseButton;
        private System.Windows.Forms.Button dashboardButton;
        private System.Windows.Forms.Button backToPortalButton;

        private int _pendingMobileUpdatesCount;
        private NotificationCenterHost _notificationCenterHost;
        private DashboardView _dashboardView;
        private HomeNotificationPoller _homeNotifPoller;
        private DesktopNotificationPoller _inventoryActivityPoller;

        // Tracked across navigation (not disposed when another page is shown) purely so
        // MainForm_FormClosing can warn about unsaved Invoice License Review classification
        // changes even if the user already navigated to a different page since staging them.
        private ViewInvoiceLicenseReviewPage _invoiceLicenseReviewPage;

        public MainForm()
        {
            InitializeComponent();
            //  Make the form open maximized but not cover the taskbar
            this.WindowState = FormWindowState.Maximized;
            //this.MaximizeBox = false;    // optional (disables the Maximize button)

            // Initialize hamburger menu
            InitializeHamburgerMenu();

            // Initialize notification panel
            InitializeNotificationPanel();

            // Initialize system settings menu
            InitializeSystemSettingsMenu();

            // Add resize event handler to reposition notification panel
            this.Resize += MainForm_Resize;

            this.FormClosing += MainForm_FormClosing;

            // Home dashboard toasts for the same expiry/mobile-update data the bell already
            // shows on demand — see HomeNotificationPoller for the check cadence/dedup rules.
            _homeNotifPoller = new HomeNotificationPoller();
            _homeNotifPoller.NewLicenseExpiryItemsArrived  += items => BeginInvoke(new Action(() => OnNewLicenseExpiryItems(items)));
            _homeNotifPoller.NewWarrantyExpiryItemsArrived += items => BeginInvoke(new Action(() => OnNewWarrantyExpiryItems(items)));
            _homeNotifPoller.NewMobileUpdatesArrived       += items => BeginInvoke(new Action(() => OnNewMobileUpdateItems(items)));

            // Persisted Inventory System "Activity" notifications (dbo.Notification, InventorySystem
            // portal) — toast when a new one arrives, then refresh the bell's Activity list + badge.
            _inventoryActivityPoller = new DesktopNotificationPoller(Models.NotificationType.InventorySystemKey, silentFirstPoll: true);
            _inventoryActivityPoller.NewNotificationsArrived += list => BeginInvoke(new Action(() => OnNewActivityNotifications(list)));

            // The actor's own item-added rows are written read (no toast, no poller pickup), so the
            // open Activity panel would otherwise not refresh until reopened — do it here.
            InventoryActivityNotifier.Emitted += OnInventoryActivityEmitted;
        }

        private void OnInventoryActivityEmitted()
        {
            try
            {
                if (IsDisposed || Disposing) return;
                BeginInvoke(new Action(async () =>
                {
                    if (_notificationCenterHost != null)
                    {
                        try { await _notificationCenterHost.LoadAsync(); } catch { /* best-effort */ }
                    }
                    UpdateNotificationBadge();
                }));
            }
            catch { /* form gone — nothing to refresh */ }
        }

        // ── Home dashboard toast handlers ───────────────────────────────────────
        // Severity: overdue = Error, due within 30 days = Warning, due within 90 = Info.
        private static string FormatDaysRemaining(int daysRemaining) =>
            daysRemaining < 0 ? $"{-daysRemaining}d overdue" : $"{daysRemaining}d remaining";

        private void OnNewLicenseExpiryItems(IReadOnlyList<ExpiryNotificationItem> items)
        {
            if (!Session.AppSession.NotificationsEnabled) return;

            foreach (var item in items)
            {
                string title   = item.DaysRemaining < 0 ? "License/Service Expired" : "License/Service Expiring Soon";
                string message = $"{item.ItemName} ({item.ItemType}) — {FormatDaysRemaining(item.DaysRemaining)}";

                if (item.DaysRemaining < 0)       ToastNotificationService.Instance.ShowError(title, message);
                else if (item.DaysRemaining <= 30) ToastNotificationService.Instance.ShowWarning(title, message);
                else                               ToastNotificationService.Instance.ShowInfo(title, message);
            }
        }

        private void OnNewWarrantyExpiryItems(IReadOnlyList<ExpiryNotificationItem> items)
        {
            if (!Session.AppSession.NotificationsEnabled) return;

            foreach (var item in items)
            {
                string title   = item.DaysRemaining < 0 ? "Warranty Expired" : "Warranty Expiring Soon";
                string message = $"{item.ItemName} ({item.ItemType}) — {FormatDaysRemaining(item.DaysRemaining)}";

                if (item.DaysRemaining < 0)       ToastNotificationService.Instance.ShowError(title, message);
                else if (item.DaysRemaining <= 30) ToastNotificationService.Instance.ShowWarning(title, message);
                else                               ToastNotificationService.Instance.ShowInfo(title, message);
            }
        }

        private void OnNewMobileUpdateItems(IReadOnlyList<MobileUpdateNotificationItem> items)
        {
            if (!Session.AppSession.NotificationsEnabled) return;

            foreach (var item in items)
            {
                string message = $"{item.SetCode ?? "Unknown Set"} / {item.SerialNumber ?? "Unknown Serial"} — {item.NewStatus}";
                ToastNotificationService.Instance.ShowWarning("New Mobile Update", message);
            }
        }

        private async void OnNewActivityNotifications(IReadOnlyList<Models.NotificationDto> items)
        {
            if (Session.AppSession.NotificationsEnabled)
            {
                var today = DateTime.Now.Date;
                foreach (var n in items)
                {
                    // Popup only for activity registered today.
                    if (n.CreatedDate.ToLocalTime().Date != today) continue;
                    ToastNotificationService.Instance.ShowInfo(n.Title ?? "Activity", n.Message ?? string.Empty);
                }
            }

            // Refresh the bell's Activity tab + unread badge (rare event — full reload is fine).
            if (_notificationCenterHost != null)
            {
                try { await _notificationCenterHost.LoadAsync(); } catch { /* best-effort */ }
                UpdateNotificationBadge();
            }
        }

        // Only warns about the Invoice License Review page — see the field comment above for why
        // this doesn't (and, given the sidebar's per-item Click-handler navigation with no shared
        // "leaving the current page" hook, currently can't cleanly) cover every other page's
        // in-app unsaved-work the same way. Refresh on that page and this close guard are the two
        // interceptable points; navigating to a different sidebar item mid-edit still discards
        // pending (unsaved-to-DB) classification decisions without prompting.
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_invoiceLicenseReviewPage != null && _invoiceLicenseReviewPage.HasUnsavedChanges)
            {
                if (!_invoiceLicenseReviewPage.ConfirmNavigateAway())
                    e.Cancel = true;
            }

            _homeNotifPoller?.Dispose();
            _homeNotifPoller = null;

            _inventoryActivityPoller?.Dispose();
            _inventoryActivityPoller = null;

            InventoryActivityNotifier.Emitted -= OnInventoryActivityEmitted;

            // Emit any item-added notification still buffered in the coalesce window.
            try { InventoryActivityNotifier.FlushPending(); } catch { }
        }

        private void OpenHowToUseLink()
        {
            const string manualUrl = "https://mercadorob2-oss.github.io/Yakult-inventory-monitoring-system-User-manual/";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = manualUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open user manual.\n\n{ex.Message}", AppConfig.ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            // Reposition notification panel if it's visible
            if (isNotificationPanelVisible && notificationPanel != null)
            {
                PositionNotificationPanel();
            }
        }

        private DevStealthKeyFilter _stealthKeyFilter;

        private void MainForm_Load(object sender, EventArgs e)
        {
            _stealthKeyFilter = new DevStealthKeyFilter();
            Application.AddMessageFilter(_stealthKeyFilter);

            // Show login/register dialog until user successfully logs in
            if (!ShowAuthenticationDialog())
            {
                Application.RemoveMessageFilter(_stealthKeyFilter);
                Application.Exit();
                return;
            }

            ShowPortalAndNavigate();
        }

        private async void ShowPortalAndNavigate()
        {
            // Department account sessions go directly to the Request Portal
            if (Yakult.Inventory.App.Session.AppSession.IsDepartmentAccountSession)
            {
                this.Hide();
                bool backToPortal;
                using (var requesterPortal = new RequesterPortalForm())
                {
                    requesterPortal.ShowDialog();
                    backToPortal = requesterPortal.BackToPortalRequested && !requesterPortal.LogoutRequested;
                }

                // Only "Back to Portal" continues to the dashboard loop below; closing the
                // window or Logout still ends the department session.
                if (!backToPortal)
                {
                    LogOutOnMenu_Click(this, EventArgs.Empty);
                    return;
                }
            }

            while (true)
            {
                // If a sub-portal cleared the session (e.g. its own Logout button),
                // treat it the same as an explicit logout rather than re-showing the dashboard.
                if (!Yakult.Inventory.App.Session.AppSession.IsLoggedIn)
                {
                    LogOutOnMenu_Click(this, EventArgs.Empty);
                    return;
                }

                // Keep the Inventory MainForm hidden while the user is in the portal or Call Monitoring,
                // so it doesn't appear behind other modules.
                this.Hide();

                PortalSelection selectedModule;
                var portal = new Yakult.Inventory.App.Wpf.Portal.WpfMainDashboardShell();
                portal.ShowDialog();
                selectedModule = portal.SelectedModule;

                // If user selected Logout OR restricted closing the window (None)
                if (selectedModule == PortalSelection.Logout || selectedModule == PortalSelection.None)
                {
                    LogOutOnMenu_Click(this, EventArgs.Empty);
                    return;
                }

                if (selectedModule == PortalSelection.ITCallMonitoring)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    var callMonitoring = new Yakult.Inventory.App.Wpf.CallMonitoring.WpfCallMonitoringShell();
                    callMonitoring.Closed += (s, e) =>
                    {
                        tcs.TrySetResult(true);
                    };
                    callMonitoring.Show();
                    await tcs.Task;

                    if (!Yakult.Inventory.App.Session.AppSession.IsLoggedIn)
                    {
                        LogOutOnMenu_Click(this, EventArgs.Empty);
                        return;
                    }

                    // After closing Call Monitoring, return to the portal selector again.
                    continue;
                }

                if (selectedModule == PortalSelection.RepairTechnicianPortal)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    var repairPortal = new Yakult.Inventory.App.Wpf.RepairPortal.Shell.Views.RepairPortalShellWindow();
                    repairPortal.Closed += (s, e) =>
                    {
                        tcs.TrySetResult(true);
                    };
                    // This WPF window is the root of the entire Repair Portal's window tree
                    // (Reports list, Signatory picker, Ticket detail, etc. all trace back to it).
                    // Without an explicit Win32 owner here, that whole tree floats independently of
                    // MainForm's ownership chain — closing a window deep in it can then leave
                    // nothing valid for Windows to reactivate, which has been observed to minimize
                    // the entire app. Assign the owner BEFORE Show() so the HWND is created with it.
                    new System.Windows.Interop.WindowInteropHelper(repairPortal).Owner = this.Handle;
                    repairPortal.Show();
                    await tcs.Task;

                    if (!Yakult.Inventory.App.Session.AppSession.IsLoggedIn)
                    {
                        LogOutOnMenu_Click(this, EventArgs.Empty);
                        return;
                    }

                    // After closing the Repair Technician Portal, return to the portal selector again.
                    continue;
                }

                if (selectedModule == PortalSelection.Reports)
                {
                    ReportsForm.ReportsExitAction reportsExit;
                    using (var reports = new ReportsForm())
                    {
                        reports.ShowDialog();
                        reportsExit = reports.ExitAction;
                    }

                    if (reportsExit == ReportsForm.ReportsExitAction.Logout)
                    {
                        LogOutOnMenu_Click(this, EventArgs.Empty);
                        return;
                    }

                    // After closing Reports, return to the portal selector again.
                    continue;
                }

                if (selectedModule == PortalSelection.RequesterPortal)
                {
                    using (var requesterPortal = new RequesterPortalForm())
                    {
                        requesterPortal.ShowDialog();

                        if (requesterPortal.LogoutRequested)
                        {
                            LogOutOnMenu_Click(this, EventArgs.Empty);
                            return;
                        }
                    }

                    continue;
                }

                if (selectedModule == PortalSelection.CartridgeManagement)
                {
                    // Open Consumable Management Portal. Card 1 embeds the existing Cartridge
                    // Management Portal directly inside this window (no separate popup) — see
                    // ConsumableManagementPortalShell for the embedding details.
                    var consumableMgmt = new Yakult.Inventory.App.Wpf.Portal.ConsumableManagementPortalShell();
                    consumableMgmt.ShowDialog();

                    if (consumableMgmt.LogoutRequested)
                    {
                        LogOutOnMenu_Click(this, EventArgs.Empty);
                        return;
                    }

                    // After closing Consumable Management, return to the portal selector again.
                    continue;
                }

                if (selectedModule == PortalSelection.Reports)
                {
                    using (var reports = new ReportsForm())
                    {
                        reports.ShowDialog();
                    }

                    continue;
                }

                if (selectedModule == PortalSelection.BorrowItems)
                {
                    using (var borrowItems = new BorrowItemsDashboard())
                    {
                        borrowItems.ShowDialog();
                    }

                    if (!Yakult.Inventory.App.Session.AppSession.IsLoggedIn)
                    {
                        LogOutOnMenu_Click(this, EventArgs.Empty);
                        return;
                    }

                    continue;
                }

                if (selectedModule == PortalSelection.AdminPortal)
                {
                    if (!Yakult.Inventory.App.Session.AppSession.IsDeveloper
                        && !Yakult.Inventory.App.Session.AppSession.IsSuperAdmin)
                    {
                        MessageBox.Show("Access denied. Developer or Super Admin privileges required.",
                            "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        continue;
                    }

                    using (var adminPortal = new AdminPortalForm())
                    {
                        adminPortal.ShowDialog();

                        if (adminPortal.ExitAction == AdminPortalExitAction.Logout)
                        {
                            LogOutOnMenu_Click(this, EventArgs.Empty);
                            return;
                        }
                    }

                    continue; 
                }

                // Default / Inventory selection
                this.Show();
                this.Activate();

                ShowHomePage();
                UserStatusLbl.Text =
                    $"Logged in as: {Yakult.Inventory.App.Session.AppSession.CurrentUserName}  |  {Yakult.Inventory.App.Session.AppSession.CurrentEmail}";

                // Start periodic API connection checks for the top-right status indicator
                if (!connectionCheckTimer.Enabled)
                {
                    connectionCheckTimer.Start();
                }

                _ = UpdateConnectionStatusAsync();
                return;
            }
        }

        private void UpdateResetPasswordButtonVisibility()
        {
            if (resetPasswordButton != null)
            {
                resetPasswordButton.Visible = Yakult.Inventory.App.Session.AppSession.IsDeveloper;
                // Reflow the menu to adjust positions
                ReflowSideMenu();
            }
        }

        private void UpdateAccountManagementMenuVisibility()
        {
            if (accountManagementButton != null)
            {
                bool canSee = Yakult.Inventory.App.Session.AppSession.IsDeveloper
                    || Yakult.Inventory.App.Session.AppSession.HasRole("Admin");

                accountManagementButton.Visible = canSee;
                if (!canSee)
                {
                    isAccountManagementExpanded = false;
                    if (accountManagementPanel != null)
                        accountManagementPanel.Visible = false;
                }

                ReflowSideMenu();
            }
        }

        private void UpdateEmailSmtpMenuVisibility()
        {
            if (emailSmtpButton != null)
            {
                bool canSee = Yakult.Inventory.App.Session.AppSession.IsDeveloper
                    || Yakult.Inventory.App.Session.AppSession.HasRole("Admin");

                emailSmtpButton.Visible = canSee;
                if (!canSee)
                {
                    isEmailSmtpExpanded = false;
                    if (emailSmtpPanel != null)
                        emailSmtpPanel.Visible = false;
                }

                ReflowSideMenu();
            }
        }

private void UpdateAdminMenuVisibility()
        {
            // Admin section is permanently hidden — User Activity is accessed via Account Management
            if (adminButton != null)
            {
                adminButton.Visible = false;
                isAdminExpanded = false;
                if (adminPanel != null)
                    adminPanel.Visible = false;

                ReflowSideMenu();
            }
        }

        private void InitializeHamburgerMenu()
        {
            // Hide the traditional menu strip
            menuStrip1.Visible = false;

            // Create a top bar panel for better organization
            var topBar = new System.Windows.Forms.Panel
            {
                Height = 70,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(78, 154, 252)
            };
            this.Controls.Add(topBar);
            topBar.BringToFront();

            // Adjust ContentPanel to be below the top bar and above status strip
            ContentPanel.Location = new Point(0, topBar.Height);
            ContentPanel.Size = new Size(this.ClientSize.Width, this.ClientSize.Height - topBar.Height - statusStrip1.Height);
            ContentPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // Create hamburger button on the top bar
            hamburgerButton = new System.Windows.Forms.Button
            {
                Text = "☰",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                Size = new Size(70, 60),
                Location = new Point(5, 5),
                BackColor = Color.FromArgb(58, 134, 232),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            hamburgerButton.FlatAppearance.BorderSize = 0;
            hamburgerButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(78, 154, 252);
            hamburgerButton.Click += HamburgerButton_Click;
            topBar.Controls.Add(hamburgerButton);

            // Add Yakult name logo (if available in output folder)
            var brandLogo = TryLoadYakultNameLogo();
            PictureBox picBrand = null;
            if (brandLogo != null)
            {
                picBrand = new PictureBox
                {
                    Image = brandLogo,
                    Size = new Size(220, 46),
                    Location = new Point(85, 12),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent
                };
                topBar.Controls.Add(picBrand);
            }

            // Add app title to top bar
            var titleLabel = new Label
            {
                Text = "Inventory Monitoring System",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(picBrand != null ? 315 : 85, 14),
                AutoSize = true
            };
            topBar.Controls.Add(titleLabel);

            // Move connection status and notification button to top bar
            lblConnectionStatus.Parent = topBar;
            lblConnectionStatus.Location = new Point(this.ClientSize.Width - 220, 25);
            lblConnectionStatus.BackColor = Color.Transparent;
            lblConnectionStatus.ForeColor = Color.White; // Always white on teal background

            btnNotification.Parent = topBar;
            btnNotification.Location = new Point(this.ClientSize.Width - 60, 15);
            btnNotification.BackColor = Color.FromArgb(255, 152, 0);
            btnNotification.Size = new Size(50, 40);

            // Create overlay panel for click-outside-to-close functionality
            // Covers area to the right of the menu
            menuOverlay = new System.Windows.Forms.Panel
            {
                Location = new Point(MENU_WIDTH, topBar.Height),
                Size = new Size(this.ClientSize.Width - MENU_WIDTH, this.ClientSize.Height - topBar.Height - statusStrip1.Height),
                BackColor = Color.Transparent, // Transparent so content stays visible
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            menuOverlay.Click += MenuOverlay_Click;
            this.Controls.Add(menuOverlay);
            // Insert overlay behind ContentPanel so it doesn't block content interaction
            this.Controls.SetChildIndex(menuOverlay, this.Controls.Count - 1);

            // Create side menu panel (hidden initially)
            sideMenuPanel = new System.Windows.Forms.Panel
            {
                Width = MENU_WIDTH,
                Height = this.ClientSize.Height - topBar.Height,
                Location = new Point(0, topBar.Height),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
                AutoScroll = true,
                Visible = false
            };

            // Disable horizontal scrolling in the hamburger menu (keep vertical scrolling).
            sideMenuPanel.HorizontalScroll.Enabled = false;
            sideMenuPanel.HorizontalScroll.Visible = false;
            sideMenuPanel.HorizontalScroll.Maximum = 0;
            sideMenuPanel.VerticalScroll.Enabled = true;
            sideMenuPanel.VerticalScroll.Visible = false;

            // Scroll/Resize handlers removed: DockStyle.Top handles repositioning automatically.

            // Add shadow effect to side menu
            sideMenuPanel.Paint += (s, e) =>
            {
                // Draw a subtle right border shadow
                using (var pen = new Pen(Color.FromArgb(200, 200, 200), 1))
                {
                    e.Graphics.DrawLine(pen, sideMenuPanel.Width - 1, 0, sideMenuPanel.Width - 1, sideMenuPanel.Height);
                }
            };

            // Fixed-width inner content panel: menu buttons dock inside this instead of
            // sideMenuPanel directly. Its width never changes, so when the outer AutoScroll
            // panel's vertical scrollbar appears/disappears (as sub-menus expand/collapse),
            // the scrollbar only eats into the reserved margin below, and the buttons never
            // resize/jump in width.
            sideMenuContent = new System.Windows.Forms.Panel
            {
                Width = MENU_WIDTH - SystemInformation.VerticalScrollBarWidth,
                Location = new Point(0, 0),
                BackColor = Color.White
            };
            sideMenuPanel.Controls.Add(sideMenuContent);

            // Add menu items to panel
            PopulateSideMenu();
            UpdateSideMenuContentHeight();

            this.Controls.Add(sideMenuPanel);
            sideMenuPanel.BringToFront();
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

        private void MenuOverlay_Click(object sender, EventArgs e)
        {
            // Close menu when clicking outside
            if (isMenuOpen)
            {
                ToggleMenu();
            }
        }

        private void PopulateSideMenu()
        {
            // NOTE: Controls are added in BOTTOM-TO-TOP visual order.
            // With DockStyle.Top, the last control added ends up at the top.
            // This mirrors the approach used in the Modern-Media-Player-UI-C-Sharp reference project.
            // Sub-buttons inside each sub-panel are also added in reverse (last item first).

            int buttonHeight = 50;

            // ── LOGOUT (very bottom) ─────────────────────────────────────────────────
            logoutButton = new System.Windows.Forms.Button
            {
                Text = "🚪 Logout",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Height = buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            logoutButton.FlatAppearance.BorderSize = 0;
            logoutButton.Click += (s, e) => { LogOutOnMenu_Click(s, e); ToggleMenu(); };
            sideMenuContent.Controls.Add(logoutButton);

            sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 10, Dock = DockStyle.Top, BackColor = Color.White });

            // ── BACK TO PORTAL ───────────────────────────────────────────────────────
            backToPortalButton = new System.Windows.Forms.Button
            {
                Text = "Back to Portal",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Height = buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            backToPortalButton.FlatAppearance.BorderSize = 0;
            backToPortalButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(41, 128, 185);
            backToPortalButton.Click += (s, e) => { ToggleMenu(); ShowPortalAndNavigate(); };
            sideMenuContent.Controls.Add(backToPortalButton);

            //sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            //// ── RESET PASSWORD (Developer only, initially hidden) — moved to Admin Portal ──
            //resetPasswordButton = new System.Windows.Forms.Button
            //{
            //    Text = "🔧 Reset Users Password",
            //    Font = new Font("Segoe UI", 10F),
            //    Height = buttonHeight,
            //    Dock = DockStyle.Top,
            //    BackColor = Color.FromArgb(255, 152, 0),
            //    ForeColor = Color.White,
            //    FlatStyle = FlatStyle.Flat,
            //    Cursor = Cursors.Hand,
            //    TextAlign = ContentAlignment.MiddleLeft,
            //    Padding = new Padding(10, 0, 0, 0),
            //    Visible = false
            //};
            //resetPasswordButton.FlatAppearance.BorderSize = 0;
            //resetPasswordButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 126, 0);
            //resetPasswordButton.Click += (s, e) => { ResetPassword_Click(s, e); ToggleMenu(); };
            //sideMenuContent.Controls.Add(resetPasswordButton);

            //sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── MY ACCOUNT ───────────────────────────────────────────────────────────
            myAccountButton = new System.Windows.Forms.Button
            {
                Text = "👤 My Account",
                Font = new Font("Segoe UI", 10F),
                Height = buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            myAccountButton.FlatAppearance.BorderSize = 0;
            myAccountButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 240, 240);
            myAccountButton.Click += (s, e) => { ShowMyAccountPage(); ToggleMenu(); };
            sideMenuContent.Controls.Add(myAccountButton);

            sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── NOTIFICATION SETTINGS ────────────────────────────────────────────────
            // Same toggle + Test Notification flow as the Request Portal's side menu
            // (Pages\Request-Portal\RequesterPortalForm.cs) — same NotificationSettingsWindow,
            // same AppSession.NotificationsEnabled flag, so the setting is shared app-wide.
            notifSettingsButton = AddMenuButton("🔔 Notification Settings", (s, e) =>
            {
                ToggleMenu();
                var win = new NotificationSettingsWindow();
                win.ShowDialog();
            }, false);
            sideMenuContent.Controls.Add(notifSettingsButton);

            sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 15, Dock = DockStyle.Top, BackColor = Color.White });

            // ── ADMIN PANEL (Admin/Developer only) ───────────────────────────────────
            adminPanel = new System.Windows.Forms.Panel
            {
                // "Users" (Account Management) is Super Admin only.
                Height = (CanOpenUserAccountManagement ? 2 : 1) * buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            // Sub-buttons added in reverse visual order (last added = top)
            if (CanOpenUserAccountManagement)
                AddSubMenuButton(adminPanel, "Users", (s, e) => { ShowUserAccountManagementPage(); ToggleMenu(); });
            AddSubMenuButton(adminPanel, "User Activity", (s, e) => { ShowUserActivityPage(); ToggleMenu(); });
            sideMenuContent.Controls.Add(adminPanel);

            adminButton = AddCollapsibleSection("Admin", "Admin");
            adminButton.Visible = false;
            adminButton.Click += (s, e) => ToggleSection(adminPanel, adminButton, "Admin");
            sideMenuContent.Controls.Add(adminButton);

            //sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            //// ── EMAIL SETTINGS PANEL (Admin/Developer only) — moved to Admin Portal ──
            //emailSmtpPanel = new System.Windows.Forms.Panel
            //{
            //    Height = 3 * buttonHeight,
            //    Dock = DockStyle.Top,
            //    BackColor = Color.FromArgb(250, 250, 250),
            //    Visible = false
            //};
            //// Sub-buttons added in reverse visual order (first added = bottom, last added = top)
            //// Note: Branch Email Binding merged into Department Accounts page
            //// Note: Employee Email Binding merged into Employee Management page
            //AddSubMenuButton(emailSmtpPanel, "Email Logs", (s, e) => { ShowEmailLogsPage(); ToggleMenu(); });
            //AddSubMenuButton(emailSmtpPanel, "SMTP Settings", (s, e) => { ShowSmtpSettingsPage(); ToggleMenu(); });
            //AddSubMenuButton(emailSmtpPanel, "Email Configuration", (s, e) => { ShowEmailConfigurationPage(); ToggleMenu(); });
            //sideMenuContent.Controls.Add(emailSmtpPanel);

            //emailSmtpButton = AddCollapsibleSection("Email Settings", "EmailSmtp");
            //emailSmtpButton.Visible = false;
            //emailSmtpButton.Click += (s, e) => ToggleSection(emailSmtpPanel, emailSmtpButton, "EmailSmtp");
            //sideMenuContent.Controls.Add(emailSmtpButton);

            //sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            //// ── ACCOUNT MANAGEMENT PANEL (Admin/Developer only) — moved to Admin Portal ──
            //accountManagementPanel = new System.Windows.Forms.Panel
            //{
            //    Height = 4 * buttonHeight,
            //    Dock = DockStyle.Top,
            //    BackColor = Color.FromArgb(250, 250, 250),
            //    Visible = false
            //};
            //// Sub-buttons added in reverse visual order
            //AddSubMenuButton(accountManagementPanel, "User Activity", (s, e) => { ShowUserActivityPage(); ToggleMenu(); });
            //AddSubMenuButton(accountManagementPanel, "Account Permissions", (s, e) => { ShowAccountPermissionsPage(); ToggleMenu(); });
            //AddSubMenuButton(accountManagementPanel, "Account Management", (s, e) => { ShowUserAccountManagementPage(); ToggleMenu(); });
            //AddSubMenuButton(accountManagementPanel, "Employee Management", (s, e) => { ShowEmployeeManagementPage(); ToggleMenu(); });
            //AddSubMenuButton(accountManagementPanel, "Department Accounts", (s, e) => { ShowDepartmentAccountsPage(); ToggleMenu(); });
            //sideMenuContent.Controls.Add(accountManagementPanel);

            //accountManagementButton = AddCollapsibleSection("Account Management", "AccountManagement");
            //accountManagementButton.Visible = false;
            //accountManagementButton.Click += (s, e) => ToggleSection(accountManagementPanel, accountManagementButton, "AccountManagement");
            //sideMenuContent.Controls.Add(accountManagementButton);

            //sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── VIEW HISTORY PANEL ───────────────────────────────────────────────────
            viewHistoryPanel = new System.Windows.Forms.Panel
            {
                Height = 4 * buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            // Sub-buttons added in reverse visual order
            AddSubMenuButton(viewHistoryPanel, "View Item Audit Trail", (s, e) => { viewItemMovementAuditToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(viewHistoryPanel, "View Updates", (s, e) => { viewUpdatesToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(viewHistoryPanel, "View Archive", (s, e) => { viewArchiveToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(viewHistoryPanel, "Ownership History", (s, e) => { ShowOwnershipHistoryPage(); ToggleMenu(); });
            sideMenuContent.Controls.Add(viewHistoryPanel);

            viewHistoryButton = AddCollapsibleSection("View History", "ViewHistory");
            viewHistoryButton.Click += (s, e) => ToggleSection(viewHistoryPanel, viewHistoryButton, "ViewHistory");
            sideMenuContent.Controls.Add(viewHistoryButton);

            sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── VIEW TRANSACTIONS PANEL ──────────────────────────────────────────────
            viewTransactionsPanel = new System.Windows.Forms.Panel
            {
                Height = 10 * buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            // Sub-buttons added in reverse visual order
            AddSubMenuButton(viewTransactionsPanel, "View Warranty", (s, e) => { viewWarrantyToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(viewTransactionsPanel, "View Renewals (Grouped)", (s, e) => { ShowViewRenewalsGroupPage(); ToggleMenu(); });
            AddSubMenuButton(viewTransactionsPanel, "View Renewals", (s, e) => { viewRenewalsToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(viewTransactionsPanel, "Invoice Sub Groups", (s, e) => { ShowInvoicePreparationPage(); ToggleMenu(); });
            AddSubMenuButton(viewTransactionsPanel, "View Invoices", (s, e) => { viewInvoicesToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(viewTransactionsPanel, "View Sets", (s, e) => { viewSetsToolStripMenuItem_Click_1(s, e); ToggleMenu(); });
            AddSubMenuButton(viewTransactionsPanel, "Send Notifications (Sets)", (s, e) => { ShowSetDispatchNotificationPage(); ToggleMenu(); });
            AddSubMenuButton(viewTransactionsPanel, "Unfulfilled Requests", (s, e) => { ShowUnfulfilledRequestsPage(); ToggleMenu(); });
            AddSubMenuButton(viewTransactionsPanel, "Partially Fulfilled Requests", (s, e) => { ShowPartiallyFulfilledRequestsPage(); ToggleMenu(); });
            AddSubMenuButton(viewTransactionsPanel, "View Requests", (s, e) => { viewRequestsToolStripMenuItem1_Click(s, e); ToggleMenu(); });
            sideMenuContent.Controls.Add(viewTransactionsPanel);

            viewTransactionsButton = AddCollapsibleSection("View Transactions", "ViewTransactions");
            viewTransactionsButton.Click += (s, e) => ToggleSection(viewTransactionsPanel, viewTransactionsButton, "ViewTransactions");
            sideMenuContent.Controls.Add(viewTransactionsButton);

            sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── VIEW MASTER DATA PANEL ───────────────────────────────────────────────
            viewMasterDataPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            int viewMasterDataButtonCount = 0;
            // Sub-buttons added in reverse visual order
            if (Session.AppSession.IsSuperAdmin)
            {
                AddSubMenuButton(viewMasterDataPanel, "Non-Licensed Invoices", (s, e) => { ShowNonLicensedInvoicesPage(); ToggleMenu(); });
                AddSubMenuButton(viewMasterDataPanel, "Invoice License Review", (s, e) => { ShowInvoiceLicenseReviewPage(); ToggleMenu(); });
                viewMasterDataButtonCount += 2;
            }
            AddSubMenuButton(viewMasterDataPanel, "View Assets", (s, e) => { ViewAssets_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(viewMasterDataPanel, "Consumable Models", (s, e) => { ShowConsumableModelsPage(); ToggleMenu(); });
            AddSubMenuButton(viewMasterDataPanel, "View Fixed Assets", (s, e) => { ViewFixedAssets_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(viewMasterDataPanel, "View Receipts", (s, e) => { viewReceiptsToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(viewMasterDataPanel, "View Vendors", (s, e) => { viewVendorsToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(viewMasterDataPanel, "View Departments", (s, e) => { viewDepartmentsToolStripMenuItem_Click_1(s, e); ToggleMenu(); });
            AddSubMenuButton(viewMasterDataPanel, "View Branches", (s, e) => { viewBranchesToolStripMenuItem_Click_1(s, e); ToggleMenu(); });
            // AddSubMenuButton(viewMasterDataPanel, "Branch Assignments", (s, e) => { ShowViewBranchAssignmentsPage(); ToggleMenu(); });
            AddSubMenuButton(viewMasterDataPanel, "View Companies", (s, e) => { viewCompaniesToolStripMenuItem_Click_1(s, e); ToggleMenu(); });
            AddSubMenuButton(viewMasterDataPanel, "View Employees", (s, e) => { viewEmployeesToolStripMenuItem_Click_1(s, e); ToggleMenu(); });
            AddSubMenuButton(viewMasterDataPanel, "View Categories", (s, e) => { viewCategoriesToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(viewMasterDataPanel, "View Repair Items", (s, e) => { viewRepairedItemsToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(viewMasterDataPanel, "View Items", (s, e) => { viewItemsToolStripMenuItem_Click_1(s, e); ToggleMenu(); });
            AddSubMenuButton(viewMasterDataPanel, "View Inventory", (s, e) => { viewInventoryToolStripMenuItem_Click_1(s, e); ToggleMenu(); });
            viewMasterDataButtonCount += 13;
            viewMasterDataPanel.Height = viewMasterDataButtonCount * buttonHeight;
            sideMenuContent.Controls.Add(viewMasterDataPanel);

            viewMasterDataButton = AddCollapsibleSection("View Master Data", "ViewMasterData");
            viewMasterDataButton.Click += (s, e) => ToggleSection(viewMasterDataPanel, viewMasterDataButton, "ViewMasterData");
            sideMenuContent.Controls.Add(viewMasterDataButton);

            sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── ADD MASTER DATA PANEL ────────────────────────────────────────────────
            addMasterDataPanel = new System.Windows.Forms.Panel
            {
                Height = 9 * buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            // Sub-buttons added in reverse visual order
            AddSubMenuButton(addMasterDataPanel, "Sales Invoice Set", (s, e) => { softwareServiceSetToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(addMasterDataPanel, "Request Set", (s, e) => { requestSetMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(addMasterDataPanel, "Request", (s, e) => { requestToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(addMasterDataPanel, "Item", (s, e) => { itemToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(addMasterDataPanel, "Employee", (s, e) => { employeeToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(addMasterDataPanel, "Vendor", (s, e) => { vendorToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(addMasterDataPanel, "Department", (s, e) => { departmentToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(addMasterDataPanel, "Branch", (s, e) => { branchToolStripMenuItem_Click(s, e); ToggleMenu(); });
            AddSubMenuButton(addMasterDataPanel, "Company", (s, e) => { companyToolStripMenuItem_Click(s, e); ToggleMenu(); });
            sideMenuContent.Controls.Add(addMasterDataPanel);

            addMasterDataButton = AddCollapsibleSection("Add Master Data", "AddMasterData");
            addMasterDataButton.Click += (s, e) => ToggleSection(addMasterDataPanel, addMasterDataButton, "AddMasterData");
            sideMenuContent.Controls.Add(addMasterDataButton);

            sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── HOW TO USE ───────────────────────────────────────────────────────────
            howToUseButton = AddMenuButton("📖 User Manual", (s, e) => { OpenHowToUseLink(); ToggleMenu(); }, false);
            sideMenuContent.Controls.Add(howToUseButton);

            sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── DASHBOARD ────────────────────────────────────────────────────────────
            dashboardButton = AddMenuButton("📊 Dashboard", (s, e) => { ShowDashboard(); ToggleMenu(); }, false);
            sideMenuContent.Controls.Add(dashboardButton);

            sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── HOME ─────────────────────────────────────────────────────────────────
            homeButton = AddMenuButton("🏠 Home", (s, e) => { homeToolStripMenuItem_Click(s, e); ToggleMenu(); }, false);
            sideMenuContent.Controls.Add(homeButton);

            sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── SEPARATOR ────────────────────────────────────────────────────────────
            menuSeparator = new System.Windows.Forms.Panel
            {
                Height = 2,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(230, 230, 230)
            };
            sideMenuContent.Controls.Add(menuSeparator);

            sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 10, Dock = DockStyle.Top, BackColor = Color.White });

            // ── HEADER LABEL (top of menu) ───────────────────────────────────────────
            menuHeaderLabel = new Label
            {
                Text = "YAKULT INVENTORY",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Height = 40,
                AutoSize = false,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0)
            };
            sideMenuContent.Controls.Add(menuHeaderLabel);

            // Top padding spacer — added LAST so it appears at the very top
            sideMenuContent.Controls.Add(new System.Windows.Forms.Panel { Height = 20, Dock = DockStyle.Top, BackColor = Color.White });
        }

        private System.Windows.Forms.Button AddCollapsibleSection(string title, string sectionName)
        {
            var button = new System.Windows.Forms.Button
            {
                Text = $"{title}     ▼",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Tag = sectionName
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = button.BackColor;

            // Custom paint to draw arrow on the right
            button.Paint += (s, e) =>
            {
                var btn = (System.Windows.Forms.Button)s;
                // Determine if this section is expanded
                bool expanded = false;
                switch (btn.Tag?.ToString())
                {
                    case "AddMasterData":
                        expanded = isAddMasterDataExpanded;
                        break;
                    case "ViewMasterData":
                        expanded = isViewMasterDataExpanded;
                        break;
                    case "ViewTransactions":
                        expanded = isViewTransactionsExpanded;
                        break;
                    case "CartridgeManagement":
                        expanded = isCartridgeManagementExpanded;
                        break;
                    case "ViewHistory":
                        expanded = isViewHistoryExpanded;
                        break;
                    case "AccountManagement":
                        expanded = isAccountManagementExpanded;
                        break;
                    case "EmailSmtp":
                        expanded = isEmailSmtpExpanded;
                        break;
                }

                // Draw text
                TextRenderer.DrawText(e.Graphics, title, btn.Font,
                    new Rectangle(10, 0, btn.Width - 40, btn.Height),
                    btn.ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                // Draw arrow on the right
                string arrow = expanded ? "▲" : "▼";
                TextRenderer.DrawText(e.Graphics, arrow, new Font("Segoe UI", 10F),
                    new Rectangle(btn.Width - 30, 0, 30, btn.Height),
                    btn.ForeColor, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            };

            button.Text = ""; // Clear text since we're custom drawing
            return button;
        }

        private System.Windows.Forms.Button AddMenuButton(string text, EventHandler onClick, bool isSubMenu)
        {
            var button = new System.Windows.Forms.Button
            {
                Text = text,
                Font = new Font("Segoe UI", 10F),
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(isSubMenu ? 30 : 10, 0, 0, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = button.BackColor;
            button.Click += onClick;
            return button;
        }

        private void AddSubMenuButton(System.Windows.Forms.Panel parentPanel, string text, EventHandler onClick)
        {
            var button = new System.Windows.Forms.Button
            {
                Text = text,
                Font = new Font("Segoe UI", 9.5F),
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                ForeColor = Color.FromArgb(80, 80, 80),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(40, 0, 0, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = button.BackColor;
            button.Click += onClick;
            parentPanel.Controls.Add(button);
        }

        private void ToggleSection(System.Windows.Forms.Panel panel, System.Windows.Forms.Button button, string sectionName)
        {
            // Toggle the appropriate section
            switch (sectionName)
            {
                case "AddMasterData":
                    isAddMasterDataExpanded = !isAddMasterDataExpanded;
                    panel.Visible = isAddMasterDataExpanded;
                    break;
                case "ViewMasterData":
                    isViewMasterDataExpanded = !isViewMasterDataExpanded;
                    panel.Visible = isViewMasterDataExpanded;
                    break;
                case "ViewTransactions":
                    isViewTransactionsExpanded = !isViewTransactionsExpanded;
                    panel.Visible = isViewTransactionsExpanded;
                    break;
                case "CartridgeManagement":
                    isCartridgeManagementExpanded = !isCartridgeManagementExpanded;
                    panel.Visible = isCartridgeManagementExpanded;
                    break;
                case "ViewHistory":
                    isViewHistoryExpanded = !isViewHistoryExpanded;
                    panel.Visible = isViewHistoryExpanded;
                    break;
                case "AccountManagement":
                    if (accountManagementButton != null && !accountManagementButton.Visible)
                        return;
                    isAccountManagementExpanded = !isAccountManagementExpanded;
                    panel.Visible = isAccountManagementExpanded;
                    break;
                case "EmailSmtp":
                    if (emailSmtpButton != null && !emailSmtpButton.Visible)
                        return;
                    isEmailSmtpExpanded = !isEmailSmtpExpanded;
                    panel.Visible = isEmailSmtpExpanded;
                    break;
                case "Admin":
                    if (adminButton != null && !adminButton.Visible)
                        return;
                    isAdminExpanded = !isAdminExpanded;
                    panel.Visible = isAdminExpanded;
                    break;
            }

            // Trigger repaint to update arrow
            button.Invalidate();

            // sideMenuContent has a fixed, explicit Height (Dock=Top alone won't grow a
            // plain Panel to fit its children) — recompute it whenever a sub-panel's
            // Visible state changes, or newly-revealed buttons get clipped/invisible.
            UpdateSideMenuContentHeight();
        }

        private void UpdateSideMenuContentHeight()
        {
            if (sideMenuContent == null) return;
            int total = 0;
            foreach (Control c in sideMenuContent.Controls)
            {
                if (c.Visible) total += c.Height;
            }
            sideMenuContent.Height = total;
        }

        private void ReflowSideMenu()
        {
            // No-op: DockStyle.Top on all sideMenuPanel children handles repositioning automatically.
            // Kept to avoid breaking existing callers (UpdateXxxVisibility, ToggleMenu, etc.).
        }

        private void HamburgerButton_Click(object sender, EventArgs e)
        {
            ToggleMenu();
        }

        private void ToggleMenu()
        {
            isMenuOpen = !isMenuOpen;

            if (isMenuOpen)
            {
                // Show overlay first (it should be behind ContentPanel)
                menuOverlay.Visible = true;
                menuOverlay.BringToFront();

                // Make sure ContentPanel stays on top of overlay
                ContentPanel.BringToFront();

                // Show menu on top of everything
                sideMenuPanel.Visible = true;
                sideMenuPanel.BringToFront();

                // Control.Visible reads false while any ancestor is hidden, so the height
                // computed during construction (while sideMenuPanel.Visible was still false)
                // summed to 0 for every child. Recompute now that the panel is actually visible.
                UpdateSideMenuContentHeight();

                // Reflow menu to ensure correct positioning
                ReflowSideMenu();
            }
            else
            {
                // Hide menu and overlay
                sideMenuPanel.Visible = false;
                menuOverlay.Visible = false;
            }
        }

        private bool ShowAuthenticationDialog()
        {
            string connectionString = DatabaseConfig.ConnectionString;

            var userRepo = new Yakult.Inventory.App.Data.UserRepository(connectionString);

            // Hide the Inventory MainForm and show the WPF portal shell as background
            this.Hide();

            var background = new Yakult.Inventory.App.Wpf.Portal.WpfMainDashboardShell();
            background.IsEnabled = false;
            background.WindowState = System.Windows.WindowState.Maximized;

            // Force the HWND to exist before Show so WinForms dialogs can be parented to it
            var wpfHelper = new System.Windows.Interop.WindowInteropHelper(background);
            wpfHelper.EnsureHandle();

            var wpfOwner = new NativeWindowWrapper(wpfHelper.Handle);

            background.Show();
            try
            {
                using (var loginForm = new LoginPage(userRepo))
                {
                    var result = loginForm.ShowDialog(wpfOwner);

                    if (result == DialogResult.OK)
                    {
                        UpdateResetPasswordButtonVisibility();
                        UpdateAccountManagementMenuVisibility();
                        UpdateEmailSmtpMenuVisibility();
                        UpdateAdminMenuVisibility();
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            finally
            {
                background.Close();
            }
        }

        private sealed class NativeWindowWrapper : System.Windows.Forms.IWin32Window
        {
            private readonly IntPtr _handle;
            public NativeWindowWrapper(IntPtr handle) { _handle = handle; }
            public IntPtr Handle => _handle;
        }

        private void ShowPage(UserControl page)
        {
            // clear previous page
            foreach (Control c in ContentPanel.Controls) c.Dispose();
            ContentPanel.Controls.Clear();

            // Clear background image when showing a page
            ContentPanel.BackgroundImage = null;
            ContentPanel.BackgroundImageLayout = ImageLayout.None;

            // host the new page
            page.Dock = DockStyle.Fill;
            ContentPanel.Controls.Add(page);
        }

        // ── HOME PAGE FIELDS ─────────────────────────────────────────────────────
        private System.Windows.Forms.TextBox _homeSearchBox;
        private System.Windows.Forms.TableLayoutPanel _homeCardsPanel;
        private System.Windows.Forms.Panel   _homeResultsWrapper;
        private System.Windows.Forms.Label   _homeStatusLabel;
        private System.Windows.Forms.Panel   _homeLogoWrapper;
        private System.Windows.Forms.Panel   _homeSearchWrapper;
        private System.Windows.Forms.TableLayoutPanel _homeOuterLayout;
        
        private System.Threading.CancellationTokenSource _homeSearchCts;
        private System.Threading.CancellationTokenSource _homeSuggestCts;
        
        private System.Windows.Forms.ListBox _homeSuggestionsList;
        private System.Windows.Forms.Panel   _homePaginationPanel;
        private System.Windows.Forms.Button  _homePrevButton;
        private System.Windows.Forms.Button  _homeNextButton;
        private System.Windows.Forms.Button  _homeBackButton;
        private System.Windows.Forms.Label   _homePageLabel;

        private System.Collections.Generic.List<Pages.ItemDto> _homeCurrentSearchResults = new System.Collections.Generic.List<Pages.ItemDto>();
        private int _homeCurrentPage = 1;
        private int _homeTotalCardEntries = 0;
        private const int HomeCardsPerPage = 9;
        private string _homeCurrentSearchQuery = "";
        private System.Windows.Forms.Form _activeTooltipForm;

        private void ShowHomePage()
        {
            // ── Clear content panel ──────────────────────────────────────────────
            foreach (Control c in ContentPanel.Controls) c.Dispose();
            ContentPanel.Controls.Clear();
            ContentPanel.BackgroundImage = null;
            ContentPanel.BackColor = Color.FromArgb(240, 243, 247);

            // ── Host the WPF SearchView via ElementHost ───────────────────────────
            var searchView = new Yakult.Inventory.App.Wpf.Search.Views.SearchView();
            searchView.NavigateRequested += (item, destination, title) =>
                NavigateToItemPage(item, destination, title);

            var host = new System.Windows.Forms.Integration.ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = searchView,
                BackColorTransparent = false,
                BackColor = Color.FromArgb(240, 243, 247)
            };

            ContentPanel.Controls.Add(host);
            return;

            // ── Legacy WinForms home page (kept for reference; unreachable) ──────
            #pragma warning disable CS0162
            ContentPanel.BackColor = Color.FromArgb(245, 247, 250);

            // ── Root scroll-capable panel ────────────────────────────────────────
            var root = new System.Windows.Forms.Panel
            {
                Dock      = DockStyle.Fill,
                AutoScroll= true,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding   = new Padding(0)
            };

            // ── Outer vertical layout ────────────────────────────────────────────
            _homeOuterLayout = new System.Windows.Forms.TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount    = 3,
                Dock        = DockStyle.Fill,
                BackColor   = Color.Transparent,
                Padding     = new Padding(0)
            };
            _homeOuterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _homeOuterLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // logo
            _homeOuterLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // search bar
            _homeOuterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // results

            // ─────────────────────────────────────────────────────────────────────
            // ROW 0 : Yakult Logo (centred, fixed height)
            // ─────────────────────────────────────────────────────────────────────
            _homeLogoWrapper = new System.Windows.Forms.Panel
            {
                Dock      = DockStyle.Fill,
                Height    = 160,
                BackColor = Color.Transparent,
                Padding   = new Padding(0, 40, 0, 10)
            };

            var logoPictureBox = new System.Windows.Forms.PictureBox
            {
                SizeMode  = System.Windows.Forms.PictureBoxSizeMode.Zoom,
                Width     = 420,
                Height    = 140,
                BackColor = Color.Transparent,
                Anchor    = AnchorStyles.None
            };

            // Centre the PictureBox inside logoWrapper
            logoPictureBox.Location = new System.Drawing.Point(
                (_homeLogoWrapper.ClientSize.Width  - logoPictureBox.Width)  / 2,
                (_homeLogoWrapper.ClientSize.Height - logoPictureBox.Height) / 2
            );
            _homeLogoWrapper.Resize += (s, e) =>
            {
                logoPictureBox.Location = new System.Drawing.Point(
                    (_homeLogoWrapper.ClientSize.Width  - logoPictureBox.Width)  / 2,
                    (_homeLogoWrapper.ClientSize.Height - logoPictureBox.Height) / 2
                );
            };

            string logoPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Images", "yakult_Name.png");
            if (System.IO.File.Exists(logoPath))
                logoPictureBox.Image = Image.FromFile(logoPath);

            _homeLogoWrapper.Controls.Add(logoPictureBox);
            _homeOuterLayout.Controls.Add(_homeLogoWrapper, 0, 0);

            // ─────────────────────────────────────────────────────────────────────
            // ROW 1 : Search Bar
            // ─────────────────────────────────────────────────────────────────────
            _homeSearchWrapper = new System.Windows.Forms.Panel
            {
                Dock      = DockStyle.Fill,
                Height    = 80,
                BackColor = Color.Transparent,
                Padding   = new Padding(0, 0, 0, 10)
            };

            // Pill-shaped search container
            var searchContainer = new System.Windows.Forms.Panel
            {
                Width     = 700,
                Height    = 60,
                BackColor = Color.White,
                Cursor    = Cursors.IBeam,
                Anchor    = AnchorStyles.None
            };
            searchContainer.Location = new System.Drawing.Point(
                (_homeSearchWrapper.ClientSize.Width - searchContainer.Width) / 2,
                (_homeSearchWrapper.ClientSize.Height - searchContainer.Height) / 2
            );

            _homeBackButton = new System.Windows.Forms.Button
            {
                Text = "←  Back",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 60, 80),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                AutoSize = true,
                Cursor = Cursors.Hand,
                Visible = false,
                Padding = new Padding(8, 4, 8, 4),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _homeBackButton.FlatAppearance.BorderSize = 0;
            _homeBackButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 235, 245);
            _homeBackButton.Click += (s, e) => ResetHomeView();
            _homeBackButton.Location = new Point(20, (_homeSearchWrapper.ClientSize.Height - 35) / 2);
            _homeSearchWrapper.Controls.Add(_homeBackButton);

            _homeSearchWrapper.Resize += (s, e) =>
            {
                searchContainer.Location = new System.Drawing.Point(
                    (_homeSearchWrapper.ClientSize.Width  - searchContainer.Width)  / 2,
                    (_homeSearchWrapper.ClientSize.Height - searchContainer.Height) / 2
                );
                if (_homeBackButton != null)
                {
                    _homeBackButton.Location = new Point(20, (_homeSearchWrapper.ClientSize.Height - _homeBackButton.Height) / 2);
                }
            };

            // Rounded border via Paint
            searchContainer.Paint += (s, pe) =>
            {
                var panel = (System.Windows.Forms.Panel)s;
                using (var path   = HomePageRoundRect(panel.ClientRectangle, 30))
                using (var penBdr = new System.Drawing.Pen(Color.FromArgb(210, 215, 225), 1.5f))
                using (var brBg   = new System.Drawing.SolidBrush(Color.White))
                {
                    pe.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    pe.Graphics.FillPath(brBg,   path);
                    pe.Graphics.DrawPath(penBdr, path);
                }
            };
            searchContainer.Region = System.Drawing.Region.FromHrgn(
                CreateRoundRectRgn(0, 0, searchContainer.Width, searchContainer.Height, 30, 30));
            searchContainer.Resize += (s, e) =>
            {
                var p = (System.Windows.Forms.Panel)s;
                p.Region = System.Drawing.Region.FromHrgn(
                    CreateRoundRectRgn(0, 0, p.Width, p.Height, 30, 30));
            };

            // 🔍 Search icon label
            var searchIcon = new System.Windows.Forms.Label
            {
                Text      = "🔍",
                Font      = new Font("Segoe UI Emoji", 18F),
                Size      = new System.Drawing.Size(54, 60),
                Location  = new System.Drawing.Point(10, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(130, 140, 160),
                Cursor    = Cursors.IBeam
            };

            // Text input (with manual placeholder — PlaceholderText not in .NET Framework)
            const string placeholderText = "Search by name, description, serial number or model...";
            _homeSearchBox = new System.Windows.Forms.TextBox
            {
                Font        = new Font("Segoe UI", 14F),
                BorderStyle = BorderStyle.None,
                BackColor   = Color.White,
                ForeColor   = Color.FromArgb(160, 170, 185),
                Location    = new System.Drawing.Point(64, 16),
                Width       = searchContainer.Width - 80,
                Height      = 26,
                Text        = placeholderText
            };
            _homeSearchBox.Enter += (s, e) =>
            {
                if (_homeSearchBox.Text == placeholderText && _homeSearchBox.ForeColor == Color.FromArgb(160, 170, 185))
                {
                    _homeSearchBox.Text      = "";
                    _homeSearchBox.ForeColor = Color.FromArgb(30, 40, 55);
                }
            };
            _homeSearchBox.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_homeSearchBox.Text))
                {
                    _homeSearchBox.Text      = placeholderText;
                    _homeSearchBox.ForeColor = Color.FromArgb(160, 170, 185);
                    if (_homeSuggestionsList != null) _homeSuggestionsList.Visible = false;
                }
            };

            _homeSearchBox.KeyDown += HomeSearchBox_KeyDown;
            _homeSearchBox.TextChanged += HomeSearchBox_TextChanged;

            searchContainer.Controls.Add(searchIcon);
            searchContainer.Controls.Add(_homeSearchBox);
            _homeSearchWrapper.Controls.Add(searchContainer);
            _homeOuterLayout.Controls.Add(_homeSearchWrapper, 0, 1);

            // ─────────────────────────────────────────────────────────────────────
            // ROW 2 : Results area
            // ─────────────────────────────────────────────────────────────────────
            _homeResultsWrapper = new System.Windows.Forms.Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding   = new Padding(30, 10, 30, 30)
            };



            _homeStatusLabel = new System.Windows.Forms.Label
            {
                Text      = "Start typing and press Enter to search…",
                Font      = new Font("Segoe UI", 11F),
                ForeColor = Color.FromArgb(160, 170, 185),
                AutoSize  = false,
                Dock      = DockStyle.Top,
                Height    = 60,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible   = false
            };

            _homeCardsPanel = new System.Windows.Forms.TableLayoutPanel
            {
                ColumnCount = 3,
                RowCount    = 3,
                Dock        = DockStyle.Fill,
                BackColor   = Color.Transparent,
                Padding     = new Padding(0),
                Visible     = false
            };
            for(int i=0; i<3; i++) _homeCardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            for(int i=0; i<3; i++) _homeCardsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));

            _homePaginationPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Visible = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 10, 0, 0)
            };

            _homePrevButton = new System.Windows.Forms.Button { Text = "◀", Width = 50, Height = 32, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 14F) };
            _homeNextButton = new System.Windows.Forms.Button { Text = "▶", Width = 50, Height = 32, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 14F) };
            _homePageLabel = new System.Windows.Forms.Label
            {
                Text = "1 / 1",
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 100, 100),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _homePrevButton.Click += (s, e) => { if (_homeCurrentPage > 1) { _homeCurrentPage--; RenderHomeCurrentPage(); } };
            _homeNextButton.Click += (s, e) => { if (_homeCurrentPage * HomeCardsPerPage < _homeTotalCardEntries) { _homeCurrentPage++; RenderHomeCurrentPage(); } };

            _homePaginationPanel.Controls.Add(_homePrevButton);
            _homePaginationPanel.Controls.Add(_homePageLabel);
            _homePaginationPanel.Controls.Add(_homeNextButton);

            _homePaginationPanel.Resize += (s, e) =>
            {
                int cx = _homePaginationPanel.Width / 2;
                _homePrevButton.Location = new Point(cx - _homePrevButton.Width - _homePageLabel.Width - 20, 10);
                _homePageLabel.Location = new Point(cx - _homePageLabel.Width / 2, 14);
                _homeNextButton.Location = new Point(cx + _homePageLabel.Width / 2 + 10, 10);
            };

            _homeResultsWrapper.Controls.Add(_homeStatusLabel);
            _homeResultsWrapper.Controls.Add(_homeCardsPanel);
            
            _homeCardsPanel.BringToFront();
            _homeStatusLabel.BringToFront();

            _homeOuterLayout.Controls.Add(_homeResultsWrapper, 0, 2);
            root.Controls.Add(_homeOuterLayout);

            // ─────────────────────────────────────────────────────────────────────
            // Floating Suggestions ListBox
            // ─────────────────────────────────────────────────────────────────────
            _homeSuggestionsList = new System.Windows.Forms.ListBox
            {
                Visible = false,
                Font = new Font("Segoe UI", 11F),
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 40, 55),
                Cursor = Cursors.Hand,
                IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 35
            };
            
            _homeSuggestionsList.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                var lb = (System.Windows.Forms.ListBox)s;
                e.DrawBackground();
                bool isSel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                
                using (var bgBrush = new System.Drawing.SolidBrush(isSel ? Color.FromArgb(240, 245, 255) : Color.White))
                    e.Graphics.FillRectangle(bgBrush, e.Bounds);
                    
                using (var txtBrush = new System.Drawing.SolidBrush(isSel ? Color.FromArgb(20, 60, 140) : Color.FromArgb(40, 50, 65)))
                {
                    var text = lb.Items[e.Index].ToString();
                    e.Graphics.DrawString(text, lb.Font, txtBrush, new PointF(e.Bounds.X + 16, e.Bounds.Y + 6));
                }
            };
            
            _homeSuggestionsList.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && _homeSuggestionsList.SelectedItem != null)
                {
                    _homeSearchBox.Text = _homeSuggestionsList.SelectedItem.ToString();
                    _homeSearchBox.ForeColor = Color.FromArgb(30, 40, 55);
                    _homeSearchBox.SelectionStart = _homeSearchBox.Text.Length;
                    _homeSuggestionsList.Visible = false;
                    ExecuteHomeSearch(_homeSearchBox.Text);
                }
                else if (e.KeyCode == Keys.Up && _homeSuggestionsList.SelectedIndex <= 0)
                {
                    _homeSearchBox.Focus();
                    e.Handled = true;
                }
            };
            
            _homeSuggestionsList.Click += (s, e) =>
            {
                if (_homeSuggestionsList.SelectedItem != null)
                {
                    _homeSearchBox.Text = _homeSuggestionsList.SelectedItem.ToString();
                    _homeSearchBox.ForeColor = Color.FromArgb(30, 40, 55);
                    _homeSearchBox.SelectionStart = _homeSearchBox.Text.Length;
                    _homeSuggestionsList.Visible = false;
                    ExecuteHomeSearch(_homeSearchBox.Text);
                }
            };

            root.Controls.Add(_homeSuggestionsList);

            void RelocateSuggestions()
            {
                if (!_homeSuggestionsList.Visible) return;
                var pt = searchContainer.PointToScreen(new Point(0, searchContainer.Height));
                var localPt = root.PointToClient(pt);
                _homeSuggestionsList.Location = new Point(localPt.X + 24, localPt.Y + 2);
                _homeSuggestionsList.Width = searchContainer.Width - 48;
            }

            root.Resize += (s, e) => { CenterHomeLayout(); RelocateSuggestions(); };
            root.Scroll += (s, e) => RelocateSuggestions();
            root.Click += (s, e) => { if (_homeSuggestionsList != null) _homeSuggestionsList.Visible = false; };
            _homeResultsWrapper.Click += (s, e) => { if (_homeSuggestionsList != null) _homeSuggestionsList.Visible = false; };
            _homeOuterLayout.Click += (s, e) => { if (_homeSuggestionsList != null) _homeSuggestionsList.Visible = false; };
            ContentPanel.Click += (s, e) => { if (_homeSuggestionsList != null) _homeSuggestionsList.Visible = false; };
            this.Click += (s, e) => { if (_homeSuggestionsList != null) _homeSuggestionsList.Visible = false; };

            ContentPanel.Controls.Add(root);
            _homePaginationPanel.Dock = DockStyle.Bottom;
            ContentPanel.Controls.Add(_homePaginationPanel);
            _homePaginationPanel.BringToFront();

            // Give focus to the search box
            _homeSearchBox.BeginInvoke((Action)(() => _homeSearchBox.Focus()));
            #pragma warning restore CS0162
        }

        private void ResetHomeView()
        {
            const string placeholderText = "Search by name, description, serial number or model...";
            _homeSearchBox.Text = placeholderText;
            _homeSearchBox.ForeColor = Color.FromArgb(160, 170, 185);
            if (_homeSuggestionsList != null) _homeSuggestionsList.Visible = false;
            
            if (_homeCardsPanel != null) _homeCardsPanel.Visible = false;
            if (_homePaginationPanel != null) _homePaginationPanel.Visible = false;
            if (_homeBackButton != null) _homeBackButton.Visible = false;
            
            if (_homeStatusLabel != null)
            {
                _homeStatusLabel.Text = "Start typing and press Enter to search…";
                _homeStatusLabel.Visible = false; 
            }

            if (_homeLogoWrapper != null) _homeLogoWrapper.Visible = true;
            CenterHomeLayout();
            
            _homeSearchBox.Focus();
        }

        private void CenterHomeLayout()
        {
            if (_homeOuterLayout == null || _homeLogoWrapper == null || _homeSearchWrapper == null) return;
            var root = _homeOuterLayout.Parent as System.Windows.Forms.Panel;
            if (root == null) return;

            if (_homeLogoWrapper.Visible)
            {
                int contentHeight = _homeLogoWrapper.Height + _homeSearchWrapper.Height;
                int topPadding = (root.ClientSize.Height - contentHeight) / 2;
                if (topPadding < 0) topPadding = 0;
                topPadding = Math.Max(0, topPadding - 40); // Move it slightly up for visual balance
                
                _homeOuterLayout.Padding = new Padding(0, topPadding, 0, 0);
            }
            else
            {
                _homeOuterLayout.Padding = new Padding(0, 20, 0, 0);
            }
        }

        // ── P/Invoke for rounded region ─────────────────────────────────────────
        [System.Runtime.InteropServices.DllImport("Gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect,
            int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        private static System.Drawing.Drawing2D.GraphicsPath HomePageRoundRect(
            System.Drawing.Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private async void HomeSearchBox_TextChanged(object sender, EventArgs e)
        {
            if (_homeSearchBox.ForeColor == Color.FromArgb(160, 170, 185)) return;

            string query = _homeSearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                if (_homeSuggestionsList != null) _homeSuggestionsList.Visible = false;
                return;
            }

            _homeSuggestCts?.Cancel();
            _homeSuggestCts = new System.Threading.CancellationTokenSource();
            var token = _homeSuggestCts.Token;

            try { await System.Threading.Tasks.Task.Delay(300, token); }
            catch (System.OperationCanceledException) { return; }

            if (token.IsCancellationRequested) return;

            try
            {
                var repo = new Yakult.Inventory.App.Repositories.SearchRepository();
                var suggestions = await repo.GetSearchSuggestionsAsync(query, 8);
                if (token.IsCancellationRequested) return;

                if (suggestions != null && suggestions.Count > 0 && _homeSuggestionsList != null)
                {
                    _homeSuggestionsList.Items.Clear();
                    foreach (var s in suggestions) _homeSuggestionsList.Items.Add(s);
                    
                    var sc = _homeSearchBox.Parent;
                    var pt = sc.PointToScreen(new Point(0, sc.Height));
                    var root = _homeOuterLayout.Parent as System.Windows.Forms.Panel;
                    var localPt = root.PointToClient(pt);
                    
                    _homeSuggestionsList.Location = new Point(localPt.X + 24, localPt.Y + 2);
                    _homeSuggestionsList.Width = sc.Width - 48;
                    _homeSuggestionsList.Height = Math.Min(suggestions.Count * _homeSuggestionsList.ItemHeight + 4, 180);
                    
                    _homeSuggestionsList.Visible = true;
                    _homeSuggestionsList.BringToFront();
                }
                else if (_homeSuggestionsList != null)
                {
                    _homeSuggestionsList.Visible = false;
                }
            }
            catch { }
        }

        private void HomeSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down && _homeSuggestionsList != null && _homeSuggestionsList.Visible && _homeSuggestionsList.Items.Count > 0)
            {
                _homeSuggestionsList.SelectedIndex = 0;
                _homeSuggestionsList.Focus();
                e.Handled = true;
                return;
            }

            if (e.KeyCode != Keys.Enter)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true; // prevent ding sound

            if (_homeSearchBox == null || _homeSearchBox.ForeColor == Color.FromArgb(160, 170, 185))
                return;

            ExecuteHomeSearch(_homeSearchBox.Text);
        }

        private async void ExecuteHomeSearch(string query)
        {
            _homeSuggestCts?.Cancel();
            if (_homeSuggestionsList != null) _homeSuggestionsList.Visible = false;
            query = query?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(query))
            {
                ResetHomeView();
                return;
            }

            if (_homeLogoWrapper != null) _homeLogoWrapper.Visible = false;
            CenterHomeLayout();

            _homeSearchCts?.Cancel();
            _homeSearchCts = new System.Threading.CancellationTokenSource();
            var token = _homeSearchCts.Token;

            if (_homeStatusLabel != null)
            {
                _homeStatusLabel.Text    = "Searching…";
                _homeStatusLabel.Visible = true;
            }
            if (_homeCardsPanel != null) _homeCardsPanel.Visible = false;
            if (_homePaginationPanel != null) _homePaginationPanel.Visible = false;
            if (_homeBackButton != null) _homeBackButton.Visible = true;

            try
            {
                var repo = new Yakult.Inventory.App.Repositories.SearchRepository();
                _homeCurrentSearchResults = await repo.SearchItemsAsync(query, maxResults: 500);
                _homeCurrentSearchQuery = query;
            }
            catch (Exception ex)
            {
                if (_homeStatusLabel != null)
                    _homeStatusLabel.Text = $"Search failed: {ex.Message}";
                return;
            }

            if (token.IsCancellationRequested) return;

            _homeCurrentPage = 1;
            RenderHomeCurrentPage();
        }

        private void RenderHomeCurrentPage()
        {
            if (_homeCardsPanel == null || _homeStatusLabel == null) return;

            foreach (Control c in _homeCardsPanel.Controls) c.Dispose();
            _homeCardsPanel.Controls.Clear();

            if (_homeCurrentSearchResults == null || _homeCurrentSearchResults.Count == 0)
            {
                _homeStatusLabel.Text    = $"No items found for \"{_homeSearchBox.Text}\".";
                _homeStatusLabel.Visible = true;
                _homeCardsPanel.Visible  = false;
                _homePaginationPanel.Visible = false;
                return;
            }

            // Build a flat list of (item, destination) entries based on ValidDestinations
            var cardEntries = new System.Collections.Generic.List<(Pages.ItemDto item, string destination)>();
            foreach (var item in _homeCurrentSearchResults)
            {
                foreach (var destination in item.ValidDestinations)
                {
                    cardEntries.Add((item, destination));
                }
            }

            _homeStatusLabel.Visible = false;
            _homeCardsPanel.Visible  = true;

            string searchQuery = _homeSearchBox?.Text?.Trim() ?? "";

            // Group items intelligently based on what the search query matched
            // Priority: Serial Number > Model Number > Name
            // This ensures unique serials show as 1 result, while items with same name are grouped
            var groupedEntries = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Pages.ItemDto>>();
            string searchQueryLower = searchQuery.ToLowerInvariant();
            
            foreach (var (item, destination) in cardEntries)
            {
                string groupKey;
                
                // Determine grouping key based on what the search matched
                if (!string.IsNullOrWhiteSpace(item.SerialNumber) && 
                    item.SerialNumber.ToLowerInvariant().Contains(searchQueryLower))
                {
                    // Search matched serial number - group by serial (unique)
                    groupKey = $"SERIAL:{item.SerialNumber.ToUpper()}|{destination}";
                }
                else if (!string.IsNullOrWhiteSpace(item.ModelNumber) && 
                         item.ModelNumber.ToLowerInvariant().Contains(searchQueryLower))
                {
                    // Search matched model number - group by model
                    groupKey = $"MODEL:{item.ModelNumber.ToUpper()}|{destination}";
                }
                else
                {
                    // Search matched name - group by name
                    groupKey = $"NAME:{(item.Name ?? "").ToUpper()}|{destination}";
                }
                
                if (!groupedEntries.ContainsKey(groupKey))
                {
                    groupedEntries[groupKey] = new System.Collections.Generic.List<Pages.ItemDto>();
                }
                groupedEntries[groupKey].Add(item);
            }

            _homeTotalCardEntries = groupedEntries.Count;

            int totalItems = groupedEntries.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)HomeCardsPerPage);
            
            if (_homeCurrentPage < 1) _homeCurrentPage = 1;
            if (_homeCurrentPage > totalPages) _homeCurrentPage = totalPages;

            int startIndex = (_homeCurrentPage - 1) * HomeCardsPerPage;
            int count = Math.Min(HomeCardsPerPage, totalItems - startIndex);

            var groupedKeys = groupedEntries.Keys.ToList();
            for (int i = 0; i < count; i++)
            {
                var key = groupedKeys[startIndex + i];
                var items = groupedEntries[key];
                var destination = key.Split('|')[1];
                int col = i % 3;
                int row = i / 3;

                var card = BuildHomeItemCard(items, destination, searchQuery);
                _homeCardsPanel.Controls.Add(card, col, row);
            }


            if (totalPages > 1)
            {
                _homePaginationPanel.Visible = true;
                _homePrevButton.Enabled = _homeCurrentPage > 1;
                _homeNextButton.Enabled = _homeCurrentPage < totalPages;
                _homePageLabel.Text = $"{_homeCurrentPage} / {totalPages}";

                // Trigger resize to perfectly center pagination controls
                _homePaginationPanel.Width = _homePaginationPanel.Width - 1;
                _homePaginationPanel.Width = _homePaginationPanel.Width + 1;
            }
            else
            {
                _homePaginationPanel.Visible = false;
            }
        }

        private System.Windows.Forms.Panel BuildHomeItemCard(System.Collections.Generic.List<Pages.ItemDto> items, string destination, string searchQuery)
        {
            // Use the first item as representative for display
            var item = items[0];
            int itemCount = items.Count;

            // Determine which field to display prominently based on search match
            string displayTitle = GetDisplayTitle(item, searchQuery);
            string displaySubtitle = GetDisplaySubtitle(item, searchQuery, displayTitle);

            // Enhanced card with shadow container
            var shadowContainer = new System.Windows.Forms.Panel
            {
                Size      = new System.Drawing.Size(300, 180),
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand,
                Dock      = DockStyle.Fill,
                Margin    = new Padding(12)
            };

            // Shadow panel (slightly offset)
            var shadowPanel = new System.Windows.Forms.Panel
            {
                BackColor = Color.FromArgb(180, 180, 180),
                Location  = new System.Drawing.Point(4, 4),
                Size      = new System.Drawing.Size(292, 172),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            shadowPanel.Resize += (s, e) =>
            {
                var p = (System.Windows.Forms.Panel)s;
                if (p.Width > 0 && p.Height > 0)
                    p.Region = System.Drawing.Region.FromHrgn(
                        CreateRoundRectRgn(0, 0, p.Width, p.Height, 8, 8));
            };
            shadowContainer.Controls.Add(shadowPanel);

            // Main card panel
            var card = new System.Windows.Forms.Panel
            {
                BackColor = Color.White,
                Location  = new System.Drawing.Point(0, 0),
                Size      = new System.Drawing.Size(292, 172),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Tag       = item
            };

            // Enhanced hover effect with shadow
            Color normalBorderColor = Color.FromArgb(220, 220, 220);
            Color hoverBorderColor = Color.FromArgb(100, 149, 237); // Cornflower blue
            Color normalBackColor = Color.White;
            Color hoverBackColor = Color.FromArgb(248, 250, 252);

            card.Resize += (s, e) =>
            {
                var p = (System.Windows.Forms.Panel)s;
                if (p.Width > 0 && p.Height > 0)
                    p.Region = System.Drawing.Region.FromHrgn(
                        CreateRoundRectRgn(0, 0, p.Width, p.Height, 8, 8));
                p.Invalidate();
            };

            card.Paint += (s, pe) =>
            {
                var p = (System.Windows.Forms.Panel)s;
                if (p.Width <= 1 || p.Height <= 1) return;
                using (var pathCard = HomePageRoundRect(new System.Drawing.Rectangle(0, 0, p.Width - 1, p.Height - 1), 7))
                using (var penBorder = new System.Drawing.Pen(p.BackColor == hoverBackColor ? hoverBorderColor : normalBorderColor, 
                    p.BackColor == hoverBackColor ? 2f : 1f))
                {
                    pe.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    pe.Graphics.DrawPath(penBorder, pathCard);
                }
            };

            card.MouseEnter += (s, e) => 
            { 
                card.BackColor = hoverBackColor; 
                shadowPanel.BackColor = Color.FromArgb(150, 150, 150);
                shadowPanel.Location = new System.Drawing.Point(6, 6);
                card.Invalidate();
            };
            card.MouseLeave += (s, e) => 
            { 
                card.BackColor = normalBackColor; 
                shadowPanel.BackColor = Color.FromArgb(180, 180, 180);
                shadowPanel.Location = new System.Drawing.Point(4, 4);
                card.Invalidate();
            };

            shadowContainer.Controls.Add(card);
            card.BringToFront();

            // Detail tooltip — shown for every card on hover
            {
                var tooltipCard = CreateItemDetailTooltipCard(item, destination, itemCount);
                tooltipCard.Visible = false;

                // Timer-based hide so MouseLeave never blocks the UI thread
                var hideTimer = new System.Windows.Forms.Timer { Interval = 120 };
                hideTimer.Tick += (ts, te) =>
                {
                    hideTimer.Stop();
                    var mp = System.Windows.Forms.Control.MousePosition;
                    var tipRect  = new System.Drawing.Rectangle(tooltipCard.Location, tooltipCard.Size);
                    var cardRect = new System.Drawing.Rectangle(
                        card.PointToScreen(System.Drawing.Point.Empty), card.Size);
                    if (!tipRect.Contains(mp) && !cardRect.Contains(mp))
                    {
                        tooltipCard.Hide();
                        if (_activeTooltipForm == tooltipCard)
                            _activeTooltipForm = null;
                    }
                };

                void ShowTooltip()
                {
                    hideTimer.Stop();
                    if (_activeTooltipForm != null && _activeTooltipForm != tooltipCard)
                    {
                        _activeTooltipForm.Hide();
                        _activeTooltipForm = null;
                    }
                    if (tooltipCard.Visible) return;

                    var screenPos   = card.PointToScreen(System.Drawing.Point.Empty);
                    var workingArea = System.Windows.Forms.Screen.GetWorkingArea(screenPos);

                    int x = screenPos.X + card.Width + 12;
                    int y = screenPos.Y - 8;

                    if (x + tooltipCard.Width  > workingArea.Right)  x = screenPos.X - tooltipCard.Width - 12;
                    if (y + tooltipCard.Height > workingArea.Bottom) y = workingArea.Bottom - tooltipCard.Height - 10;
                    if (y < workingArea.Top)                          y = workingArea.Top + 10;

                    tooltipCard.Location = new System.Drawing.Point(x, y);
                    tooltipCard.Show();
                    _activeTooltipForm = tooltipCard;
                }

                void StartHide()
                {
                    if (tooltipCard.Visible)
                        hideTimer.Start();
                }

                card.MouseEnter += (s, e) => ShowTooltip();
                card.MouseLeave += (s, e) => StartHide();

                tooltipCard.MouseEnter += (s, e) => { hideTimer.Stop(); };
                tooltipCard.MouseLeave += (s, e) => StartHide();

                // Propagate tooltip hover to every child label/panel inside the card
                void WireTooltipToChildren(System.Windows.Forms.Control ctrl)
                {
                    ctrl.MouseEnter += (s, e) => ShowTooltip();
                    ctrl.MouseLeave += (s, e) => StartHide();
                    foreach (System.Windows.Forms.Control child in ctrl.Controls)
                        WireTooltipToChildren(child);
                }
                foreach (System.Windows.Forms.Control child in card.Controls)
                    WireTooltipToChildren(child);
            }

            // ── Consistent accent strip (left) ───────────────────────────────────
            var accentStrip = new System.Windows.Forms.Panel
            {
                BackColor = Color.FromArgb(251, 206, 177), // Dirty white accent
                Dock      = DockStyle.Left,
                Width     = 4
            };
            card.Controls.Add(accentStrip);

            // ── Display title (prominent - shows matched field) ─────────────────
            var titleLabel = new System.Windows.Forms.Label
            {
                Text         = displayTitle,
                Font         = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
                ForeColor    = Color.FromArgb(20, 20, 20),
                Location     = new System.Drawing.Point(16, 12),
                Width        = card.Width - 80,
                Height       = 28,
                Anchor       = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true,
                Cursor       = Cursors.Hand
            };
            card.Controls.Add(titleLabel);

            // ── Count badge (top-right) ───────────────────────────────────────
            var countBadge = new System.Windows.Forms.Panel
            {
                BackColor   = Color.FromArgb(100, 149, 237),
                Location    = new System.Drawing.Point(card.Width - 50, 12),
                Size        = new System.Drawing.Size(34, 24),
                Cursor      = Cursors.Hand,
                Anchor      = AnchorStyles.Top | AnchorStyles.Right
            };
            countBadge.Resize += (s, e) =>
            {
                var p = (System.Windows.Forms.Panel)s;
                if (p.Width > 0 && p.Height > 0)
                    p.Region = System.Drawing.Region.FromHrgn(
                        CreateRoundRectRgn(0, 0, p.Width, p.Height, 12, 12));
            };
            var countLabel = new System.Windows.Forms.Label
            {
                Text         = itemCount.ToString(),
                Font         = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
                ForeColor    = Color.White,
                TextAlign    = ContentAlignment.MiddleCenter,
                Dock         = DockStyle.Fill,
                Cursor       = Cursors.Hand
            };
            countBadge.Controls.Add(countLabel);
            card.Controls.Add(countBadge);
            countBadge.BringToFront();

            // ── Subtitle (below title - shows item name if different from title) ──
            if (!string.IsNullOrWhiteSpace(displaySubtitle))
            {
                var subtitleLabel = new System.Windows.Forms.Label
                {
                    Text         = displaySubtitle,
                    Font         = new Font("Segoe UI", 9F, FontStyle.Regular),
                    ForeColor    = Color.FromArgb(100, 100, 100),
                    Location     = new System.Drawing.Point(16, 44),
                    Width        = card.Width - 32,
                    Height       = 20,
                    Anchor       = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    AutoEllipsis = true,
                    Cursor       = Cursors.Hand
                };
                card.Controls.Add(subtitleLabel);
            }

            // ── Category (below subtitle) ───────────────────────────────────────
            var categoryLabel = new System.Windows.Forms.Label
            {
                Text         = item.Category ?? "Uncategorized",
                Font         = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor    = Color.FromArgb(140, 140, 140),
                Location     = new System.Drawing.Point(16, string.IsNullOrWhiteSpace(displaySubtitle) ? 44 : 66),
                Width        = card.Width - 32,
                Height       = 18,
                Anchor       = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true,
                Cursor       = Cursors.Hand
            };
            card.Controls.Add(categoryLabel);

            // ── Model/Serial (middle section) ─────────────────────────────────
            var detailsLabel = new System.Windows.Forms.Label
            {
                Text         = GetItemDetailsText(item, displayTitle),
                Font         = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor    = Color.FromArgb(80, 80, 80),
                Location     = new System.Drawing.Point(16, string.IsNullOrWhiteSpace(displaySubtitle) ? 66 : 88),
                Width        = card.Width - 32,
                Height       = 18,
                Anchor       = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true,
                Cursor       = Cursors.Hand
            };
            card.Controls.Add(detailsLabel);

            // ── Destination (bottom-left) ───────────────────────────────────────
            var destLabel = new System.Windows.Forms.Label
            {
                Text      = destination,
                Font      = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                ForeColor = GetDestinationColor(destination),
                Location  = new System.Drawing.Point(16, 145),
                AutoSize  = true,
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left
            };
            card.Controls.Add(destLabel);

            // ── Click + hover propagation ─────────────────────────────────────
            shadowContainer.Click += (s, e) => NavigateToItemPage(item, destination, displayTitle);
            card.Click += (s, e) => NavigateToItemPage(item, destination, displayTitle);

            void PropagateInteraction(System.Windows.Forms.Control ctrl)
            {
                ctrl.MouseEnter += (s, e) => 
                { 
                    card.BackColor = hoverBackColor; 
                    shadowPanel.BackColor = Color.FromArgb(150, 150, 150);
                    shadowPanel.Location = new System.Drawing.Point(6, 6);
                    card.Invalidate();
                };
                ctrl.MouseLeave += (s, e) => 
                { 
                    card.BackColor = normalBackColor; 
                    shadowPanel.BackColor = Color.FromArgb(180, 180, 180);
                    shadowPanel.Location = new System.Drawing.Point(4, 4);
                    card.Invalidate();
                };
                ctrl.Click += (s, e) => NavigateToItemPage(item, destination, displayTitle);
                foreach (System.Windows.Forms.Control child in ctrl.Controls)
                    PropagateInteraction(child);
            }
            foreach (System.Windows.Forms.Control child in card.Controls)
                PropagateInteraction(child);

            return shadowContainer;
        }

        private static string GetDisplayTitle(Pages.ItemDto item, string searchQuery)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
                return item.Name ?? "(Unnamed)";

            string query = searchQuery.ToLowerInvariant();

            // Priority: Serial Number > Model Number > Name
            if (!string.IsNullOrWhiteSpace(item.SerialNumber) && 
                item.SerialNumber.ToLowerInvariant().Contains(query))
                return item.SerialNumber;

            if (!string.IsNullOrWhiteSpace(item.ModelNumber) && 
                item.ModelNumber.ToLowerInvariant().Contains(query))
                return item.ModelNumber;

            // Default to item name
            return item.Name ?? "(Unnamed)";
        }

        private static string GetDisplaySubtitle(Pages.ItemDto item, string searchQuery, string displayTitle)
        {
            // If title is not the item name, show the item name as subtitle
            if (!string.IsNullOrWhiteSpace(item.Name) && 
                !string.Equals(displayTitle, item.Name, StringComparison.OrdinalIgnoreCase))
                return item.Name;

            return null;
        }

        private static string GetItemDetailsText(Pages.ItemDto item, string displayTitle)
        {
            var parts = new System.Collections.Generic.List<string>();
            
            // Don't show Model/Serial in details if they're already the title
            if (!string.IsNullOrWhiteSpace(item.ModelNumber) && 
                !string.Equals(displayTitle, item.ModelNumber, StringComparison.OrdinalIgnoreCase))
                parts.Add($"Model: {item.ModelNumber}");
            
            if (!string.IsNullOrWhiteSpace(item.SerialNumber) && 
                !string.Equals(displayTitle, item.SerialNumber, StringComparison.OrdinalIgnoreCase))
                parts.Add($"SN: {item.SerialNumber}");
            
            return parts.Count > 0 ? string.Join(" • ", parts) : "No details";
        }

        private static string GetFullItemDetailsTooltip(Pages.ItemDto item, string destination)
        {
            var lines = new System.Collections.Generic.List<string>();
            
            lines.Add($"Name: {item.Name ?? "N/A"}");
            
            if (!string.IsNullOrWhiteSpace(item.SerialNumber))
                lines.Add($"Serial Number: {item.SerialNumber}");
            
            if (!string.IsNullOrWhiteSpace(item.ModelNumber))
                lines.Add($"Model Number: {item.ModelNumber}");
            
            if (!string.IsNullOrWhiteSpace(item.Category))
                lines.Add($"Category: {item.Category}");
            
            if (!string.IsNullOrWhiteSpace(item.VendorName))
                lines.Add($"Vendor: {item.VendorName}");
            
            if (item.ConditionId > 0 && !string.IsNullOrWhiteSpace(item.ConditionName))
                lines.Add($"Condition: {item.ConditionName}");
            
            if (item.WarrantyYears > 0)
                lines.Add($"Warranty: {item.WarrantyYears} year(s)");
            
            if (item.WarrantyStartDate.HasValue)
                lines.Add($"Warranty Start: {item.WarrantyStartDate.Value:MMM dd, yyyy}");
            
            if (item.WarrantyEndDate.HasValue)
                lines.Add($"Warranty End: {item.WarrantyEndDate.Value:MMM dd, yyyy}");
            
            if (!string.IsNullOrWhiteSpace(item.Remarks))
                lines.Add($"Remarks: {item.Remarks}");
            
            lines.Add($"");
            lines.Add($"Destination: {destination}");
            lines.Add($"Type: {item.ItemType ?? "Hardware"}");
            
            return string.Join("\n", lines);
        }

        private System.Windows.Forms.Form CreateItemDetailTooltipCard(Pages.ItemDto item, string destination, int groupCount)
        {
            var details      = GetDestinationSpecificDetails(item, destination);
            var accentColor  = GetDestinationColor(destination);

            const int accentH  = 5;
            const int headerH  = 38;
            const int rowH     = 24;
            const int footerH  = 28;
            const int sidePad  = 16;
            const int labelW   = 96;
            const int formW    = 310;

            bool showBadge   = groupCount > 1;
            int  bodyH       = details.Count * rowH + 10;
            int  totalH      = accentH + headerH + bodyH + (showBadge ? footerH : 6);

            var tooltipForm = new System.Windows.Forms.Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition   = FormStartPosition.Manual,
                TopMost         = true,
                ShowInTaskbar   = false,
                BackColor       = Color.White,
                Size            = new System.Drawing.Size(formW, totalH),
                Padding         = new Padding(0)
            };

            // Outer rounded border via Paint
            tooltipForm.Paint += (s, pe) =>
            {
                var f = (System.Windows.Forms.Form)s;
                pe.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = HomePageRoundRect(new System.Drawing.Rectangle(0, 0, f.Width - 1, f.Height - 1), 8))
                using (var pen  = new System.Drawing.Pen(Color.FromArgb(210, 210, 210), 1.2f))
                    pe.Graphics.DrawPath(pen, path);
            };
            tooltipForm.Region = System.Drawing.Region.FromHrgn(
                CreateRoundRectRgn(0, 0, formW, totalH, 10, 10));

            // Accent bar
            var accentBar = new System.Windows.Forms.Panel
            {
                BackColor = accentColor,
                Dock      = DockStyle.Top,
                Height    = accentH
            };
            tooltipForm.Controls.Add(accentBar);

            // Header
            var headerPanel = new System.Windows.Forms.Panel
            {
                BackColor = Color.FromArgb(248, 249, 252),
                Dock      = DockStyle.None,
                Location  = new System.Drawing.Point(0, accentH),
                Size      = new System.Drawing.Size(formW, headerH)
            };
            tooltipForm.Controls.Add(headerPanel);

            var destLabel = new System.Windows.Forms.Label
            {
                Text      = destination.ToUpperInvariant(),
                Font      = new Font("Segoe UI", 7.5F, FontStyle.Regular),
                ForeColor = accentColor,
                Location  = new System.Drawing.Point(sidePad, 8),
                AutoSize  = true
            };
            headerPanel.Controls.Add(destLabel);

            // Truncate item name to fit
            string displayName = item.Name ?? "Unknown";
            if (displayName.Length > 32) displayName = displayName.Substring(0, 30) + "…";
            var nameLabel = new System.Windows.Forms.Label
            {
                Text      = displayName,
                Font      = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                Location  = new System.Drawing.Point(sidePad, 20),
                AutoSize  = true
            };
            headerPanel.Controls.Add(nameLabel);

            // Divider line
            var divider = new System.Windows.Forms.Panel
            {
                BackColor = Color.FromArgb(230, 230, 235),
                Location  = new System.Drawing.Point(0, accentH + headerH),
                Size      = new System.Drawing.Size(formW, 1)
            };
            tooltipForm.Controls.Add(divider);

            // Body rows
            int yPos     = accentH + headerH + 1 + 5;
            int valueW   = formW - labelW - sidePad * 2 - 4;

            foreach (var (label, value) in details)
            {
                var lbl = new System.Windows.Forms.Label
                {
                    Text      = label,
                    Font      = new Font("Segoe UI", 8F, FontStyle.Regular),
                    ForeColor = Color.FromArgb(130, 130, 140),
                    Location  = new System.Drawing.Point(sidePad, yPos + 3),
                    Width     = labelW,
                    Height    = rowH - 2,
                    AutoSize  = false
                };
                tooltipForm.Controls.Add(lbl);

                var val = new System.Windows.Forms.Label
                {
                    Text         = value,
                    Font         = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                    ForeColor    = Color.FromArgb(40, 40, 50),
                    Location     = new System.Drawing.Point(sidePad + labelW, yPos + 3),
                    Width        = valueW,
                    Height       = rowH - 2,
                    AutoEllipsis = true,
                    AutoSize     = false
                };
                tooltipForm.Controls.Add(val);
                yPos += rowH;
            }

            // "Showing 1 of N" badge for grouped cards
            if (showBadge)
            {
                var badgeBar = new System.Windows.Forms.Panel
                {
                    BackColor = Color.FromArgb(245, 246, 250),
                    Location  = new System.Drawing.Point(0, totalH - footerH),
                    Size      = new System.Drawing.Size(formW, footerH)
                };
                tooltipForm.Controls.Add(badgeBar);

                var badgeLine = new System.Windows.Forms.Panel
                {
                    BackColor = Color.FromArgb(225, 225, 230),
                    Location  = new System.Drawing.Point(0, 0),
                    Size      = new System.Drawing.Size(formW, 1)
                };
                badgeBar.Controls.Add(badgeLine);

                var badgeLabel = new System.Windows.Forms.Label
                {
                    Text      = $"Showing 1 of {groupCount} items in this group",
                    Font      = new Font("Segoe UI", 7.5F, FontStyle.Regular),
                    ForeColor = Color.FromArgb(120, 120, 130),
                    Location  = new System.Drawing.Point(sidePad, 7),
                    AutoSize  = true
                };
                badgeBar.Controls.Add(badgeLabel);
            }

            return tooltipForm;
        }

        private System.Collections.Generic.List<(string label, string value)> GetDestinationSpecificDetails(Pages.ItemDto item, string destination)
        {
            var details = new System.Collections.Generic.List<(string, string)>();

            switch (destination)
            {
                case "Warranty":
                    // Warranty page focuses on warranty information
                    details.Add(("Name", item.Name ?? "N/A"));
                    details.Add(("Serial", item.SerialNumber ?? "N/A"));
                    details.Add(("Model", item.ModelNumber ?? "N/A"));
                    details.Add(("Type", item.ItemType ?? "Hardware"));
                    if (item.WarrantyYears > 0)
                        details.Add(("Warranty Years", item.WarrantyYears.ToString()));
                    if (item.WarrantyStartDate.HasValue)
                        details.Add(("Warranty Start", item.WarrantyStartDate.Value.ToString("MMM dd, yyyy")));
                    if (item.WarrantyEndDate.HasValue)
                        details.Add(("Warranty End", item.WarrantyEndDate.Value.ToString("MMM dd, yyyy")));
                    if (item.StartDate.HasValue)
                        details.Add(("Start Date", item.StartDate.Value.ToString("MMM dd, yyyy")));
                    if (item.EndDate.HasValue)
                        details.Add(("End Date", item.EndDate.Value.ToString("MMM dd, yyyy")));
                    if (!string.IsNullOrWhiteSpace(item.VendorName))
                        details.Add(("Vendor", item.VendorName));
                    break;

                case "Repaired Items":
                    // Repaired Items page focuses on repair history and condition
                    details.Add(("Name", item.Name ?? "N/A"));
                    details.Add(("Serial", item.SerialNumber ?? "N/A"));
                    details.Add(("Model", item.ModelNumber ?? "N/A"));
                    if (!string.IsNullOrWhiteSpace(item.Category))
                        details.Add(("Category", item.Category));
                    if (item.ConditionId > 0 && !string.IsNullOrWhiteSpace(item.ConditionName))
                        details.Add(("Condition", item.ConditionName));
                    if (item.StockOnHand > 0)
                        details.Add(("Stock On Hand", item.StockOnHand.ToString()));
                    details.Add(("Active", item.Active ? "Yes" : "No"));
                    if (item.DurationStartDate.HasValue)
                        details.Add(("Duration Start", item.DurationStartDate.Value.ToString("MMM dd, yyyy")));
                    if (!string.IsNullOrWhiteSpace(item.Remarks))
                        details.Add(("Remarks", item.Remarks));
                    details.Add(("Type", item.ItemType ?? "Hardware"));
                    break;

                case "Fixed Assets":
                    // Fixed Assets page focuses on asset tracking and requests
                    details.Add(("Name", item.Name ?? "N/A"));
                    details.Add(("Serial", item.SerialNumber ?? "N/A"));
                    details.Add(("Model", item.ModelNumber ?? "N/A"));
                    if (!string.IsNullOrWhiteSpace(item.Category))
                        details.Add(("Category", item.Category));
                    details.Add(("Type", item.ItemType ?? "Hardware"));
                    details.Add(("Tracked Asset", item.IsTrackedAsset ? "Yes" : "No"));
                    details.Add(("Date Created", item.DateCreated.ToString("MMM dd, yyyy")));
                    if (!string.IsNullOrWhiteSpace(item.CreatedByName))
                        details.Add(("Created By", item.CreatedByName));
                    if (item.Amount > 0)
                        details.Add(("Amount", item.Amount.ToString("C")));
                    if (!string.IsNullOrWhiteSpace(item.VendorName))
                        details.Add(("Vendor", item.VendorName));
                    break;

                case "Archive":
                    // Archive page focuses on archived records
                    details.Add(("Name", item.Name ?? "N/A"));
                    details.Add(("Serial", item.SerialNumber ?? "N/A"));
                    details.Add(("Model", item.ModelNumber ?? "N/A"));
                    if (!string.IsNullOrWhiteSpace(item.Category))
                        details.Add(("Category", item.Category));
                    details.Add(("Archived", item.IsArchived ? "Yes" : "No"));
                    if (!string.IsNullOrWhiteSpace(item.Remarks))
                        details.Add(("Remarks", item.Remarks));
                    if (item.ConditionId > 0 && !string.IsNullOrWhiteSpace(item.ConditionName))
                        details.Add(("Condition", item.ConditionName));
                    details.Add(("Type", item.ItemType ?? "Hardware"));
                    break;

                case "Items":
                    // Items page focuses on item management details
                    details.Add(("Name", item.Name ?? "N/A"));
                    details.Add(("Serial", item.SerialNumber ?? "N/A"));
                    details.Add(("Model", item.ModelNumber ?? "N/A"));
                    if (!string.IsNullOrWhiteSpace(item.Description))
                        details.Add(("Description", item.Description));
                    if (!string.IsNullOrWhiteSpace(item.Category))
                        details.Add(("Category", item.Category));
                    details.Add(("Type", item.ItemType ?? "Hardware"));
                    if (!string.IsNullOrWhiteSpace(item.UnitOfMeasure))
                        details.Add(("Unit of Measure", item.UnitOfMeasure));
                    details.Add(("Stock On Hand", item.StockOnHand.ToString()));
                    details.Add(("Active", item.Active ? "Yes" : "No"));
                    details.Add(("Date Created", item.DateCreated.ToString("MMM dd, yyyy")));
                    if (!string.IsNullOrWhiteSpace(item.CreatedByName))
                        details.Add(("Created By", item.CreatedByName));
                    if (item.ConditionId > 0 && !string.IsNullOrWhiteSpace(item.ConditionName))
                        details.Add(("Condition", item.ConditionName));
                    if (!string.IsNullOrWhiteSpace(item.VendorName))
                        details.Add(("Vendor", item.VendorName));
                    if (item.WarrantyYears > 0)
                        details.Add(("Warranty Years", item.WarrantyYears.ToString()));
                    if (!string.IsNullOrWhiteSpace(item.Remarks))
                        details.Add(("Remarks", item.Remarks));
                    if (!string.IsNullOrWhiteSpace(item.LicenseNumber))
                        details.Add(("License Number", item.LicenseNumber));
                    break;

                case "Inventory":
                default:
                    // Inventory page focuses on stock and inventory details
                    details.Add(("Name", item.Name ?? "N/A"));
                    details.Add(("Serial", item.SerialNumber ?? "N/A"));
                    details.Add(("Model", item.ModelNumber ?? "N/A"));
                    if (!string.IsNullOrWhiteSpace(item.Category))
                        details.Add(("Category", item.Category));
                    details.Add(("Type", item.ItemType ?? "Hardware"));
                    details.Add(("Stock On Hand", item.StockOnHand.ToString()));
                    details.Add(("Affects Inventory", item.AffectsInventory ? "Yes" : "No"));
                    if (!string.IsNullOrWhiteSpace(item.AcquisitionType))
                        details.Add(("Acquisition Type", item.AcquisitionType));
                    if (!string.IsNullOrWhiteSpace(item.UnitOfMeasure))
                        details.Add(("Unit of Measure", item.UnitOfMeasure));
                    if (item.ConditionId > 0 && !string.IsNullOrWhiteSpace(item.ConditionName))
                        details.Add(("Condition", item.ConditionName));
                    if (item.WarrantyYears > 0)
                        details.Add(("Warranty Years", item.WarrantyYears.ToString()));
                    break;
            }

            return details;
        }

        private static Color GetDestinationColor(string destination)
        {
            switch (destination)
            {
                case "Archive":       return Color.FromArgb(150, 150, 150);
                case "Repaired Items":return Color.FromArgb(200, 100, 50);
                case "Fixed Assets":  return Color.FromArgb(142, 68, 173);
                case "Warranty":      return Color.FromArgb(243, 156, 18);
                case "Items":         return Color.FromArgb(100, 116, 200);
                default:              return Color.FromArgb(52, 152, 219); // Inventory
            }
        }

        private string GetItemLocationText(Pages.ItemDto item)
        {
            if (item.IsArchived)
                return "Archive";
            
            if (!string.IsNullOrWhiteSpace(item.ConditionName) && 
                item.ConditionName.Equals("Damaged", StringComparison.OrdinalIgnoreCase))
                return "Repaired Items";
            
            return "Inventory";
        }

        private Color GetItemLocationColor(Pages.ItemDto item)
        {
            if (item.IsArchived)
                return Color.FromArgb(150, 150, 150);
            
            if (!string.IsNullOrWhiteSpace(item.ConditionName) && 
                item.ConditionName.Equals("Damaged", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(200, 100, 50);
            
            return Color.FromArgb(52, 152, 219);
        }

        private void NavigateToItemPage(Pages.ItemDto item, string destination, string cardTitle = null)
        {
            // Use the card title (which could be serial, model, or name) as the search query
            // so the destination page shows what was displayed on the card.
            string searchQuery = cardTitle;
            if (string.IsNullOrWhiteSpace(searchQuery) && item != null)
            {
                if (!string.IsNullOrWhiteSpace(item.Name))
                    searchQuery = item.Name.Trim();
                else if (!string.IsNullOrWhiteSpace(item.SerialNumber))
                    searchQuery = item.SerialNumber.Trim();
            }

            switch (destination)
            {
                case "Archive":
                {
                    ContentPanel.Controls.Clear();
                    var archivePage = new ViewArchivePage();
                    ContentPanel.Controls.Add(archivePage);
                    archivePage.Dock = DockStyle.Fill;
                    if (!string.IsNullOrWhiteSpace(searchQuery))
                        archivePage.ApplyInitialSearch(searchQuery);
                    break;
                }
                case "Repaired Items":
                {
                    ContentPanel.Controls.Clear();
                    var repairedPage = new ViewRepairedItemsPage();
                    ContentPanel.Controls.Add(repairedPage);
                    repairedPage.Dock = DockStyle.Fill;
                    if (!string.IsNullOrWhiteSpace(searchQuery))
                        repairedPage.ApplyInitialSearch(searchQuery);
                    break;
                }
                case "Fixed Assets":
                {
                    ContentPanel.Controls.Clear();
                    var fixedAssetsPage = new ViewFixedAssetsPage();
                    ContentPanel.Controls.Add(fixedAssetsPage);
                    fixedAssetsPage.Dock = DockStyle.Fill;
                    if (!string.IsNullOrWhiteSpace(searchQuery))
                        fixedAssetsPage.ApplyInitialSearch(searchQuery);
                    break;
                }
                case "Warranty":
                {
                    ContentPanel.Controls.Clear();
                    var warrantyPage = new ViewWarrantyPage();
                    ContentPanel.Controls.Add(warrantyPage);
                    warrantyPage.Dock = DockStyle.Fill;
                    if (!string.IsNullOrWhiteSpace(searchQuery))
                        warrantyPage.ApplyInitialSearch(searchQuery);
                    break;
                }
                case "Items":
                {
                    ContentPanel.Controls.Clear();
                    var itemsPage = new ViewItemsPage();
                    ContentPanel.Controls.Add(itemsPage);
                    itemsPage.Dock = DockStyle.Fill;
                    if (!string.IsNullOrWhiteSpace(searchQuery))
                        itemsPage.ApplyInitialSearch(searchQuery);
                    break;
                }
                case "Set":
                {
                    ContentPanel.Controls.Clear();
                    var setPage = new ViewSetPage();
                    ContentPanel.Controls.Add(setPage);
                    setPage.Dock = DockStyle.Fill;
                    if (!string.IsNullOrWhiteSpace(searchQuery))
                        setPage.ApplyInitialSearch(searchQuery);
                    break;
                }
                case "Request":
                {
                    ContentPanel.Controls.Clear();
                    var requestPage = new ViewRequestsPage();
                    ContentPanel.Controls.Add(requestPage);
                    requestPage.Dock = DockStyle.Fill;
                    if (!string.IsNullOrWhiteSpace(searchQuery))
                        requestPage.ApplyInitialSearch(searchQuery);
                    break;
                }
                case "Invoice":
                {
                    ContentPanel.Controls.Clear();
                    var invoicePage = new ViewInvoiceReportPage();
                    ContentPanel.Controls.Add(invoicePage);
                    invoicePage.Dock = DockStyle.Fill;
                    if (!string.IsNullOrWhiteSpace(searchQuery))
                        invoicePage.ApplyInitialSearch(searchQuery);
                    break;
                }
                case "Renewal":
                {
                    ContentPanel.Controls.Clear();
                    var renewalPage = new ViewRenewalPage();
                    ContentPanel.Controls.Add(renewalPage);
                    renewalPage.Dock = DockStyle.Fill;
                    if (!string.IsNullOrWhiteSpace(searchQuery))
                        renewalPage.ApplyInitialSearch(searchQuery);
                    break;
                }
                case "Renewals (Grouped)":
                {
                    ContentPanel.Controls.Clear();
                    var groupPage = new Yakult.Inventory.App.Pages.Renewal.ViewRenewalGroupPage();
                    ContentPanel.Controls.Add(groupPage);
                    groupPage.Dock = DockStyle.Fill;
                    if (!string.IsNullOrWhiteSpace(searchQuery))
                        groupPage.ApplyInitialSearch(searchQuery);
                    break;
                }
                default: // "Inventory"
                {
                    ContentPanel.Controls.Clear();
                    var inventoryPage = new ViewInventoryPage();
                    ContentPanel.Controls.Add(inventoryPage);
                    inventoryPage.Dock = DockStyle.Fill;
                    if (!string.IsNullOrWhiteSpace(searchQuery))
                        inventoryPage.ApplyInitialSearch(searchQuery);
                    break;
                }
            }
        }

        private static Color HomePageCategoryColor(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return Color.FromArgb(100, 116, 200);
            switch (category.ToLower())
            {
                case "cartridge":   return Color.FromArgb(72, 199, 142);
                case "hardware":    return Color.FromArgb(52, 152, 219);
                case "software":    return Color.FromArgb(155, 89, 182);
                case "furniture":   return Color.FromArgb(230, 126, 34);
                case "supplies":    return Color.FromArgb(231, 76, 60);
                default:            return Color.FromArgb(100, 116, 200);
            }
        }


        private void ShowDashboard()
        {
            ContentPanel.Controls.Clear();
            ContentPanel.BackgroundImage = null;
            ContentPanel.BackColor = Color.FromArgb(245, 246, 250);

            _dashboardView = new DashboardView
            {
                Dock = DockStyle.Fill
            };

            _dashboardView.OnViewInventoryClicked = () => ShowPage(new ViewInventoryPage());
            _dashboardView.OnViewUpdatesClicked = () => ShowPage(new ViewUpdatesPage());
            _dashboardView.OnRefreshClicked = () =>
            {
                var svc = new Yakult.Inventory.App.Forms.Dashboard.DashboardService();
                System.Threading.Tasks.Task.Run(() => svc.RefreshCategoryCache())
                    .ContinueWith(_ => BeginInvoke((Action)ShowDashboard));
            };

            var dashSvc = new Yakult.Inventory.App.Forms.Dashboard.DashboardService();
            System.Threading.Tasks.Task.Run(() => dashSvc.RefreshCategoryCache())
                .ContinueWith(_ => BeginInvoke((Action)(() =>
                {
                    _dashboardView.LoadDashboard(DashboardScope.Categories, null);
                    _dashboardView.SetMobileUpdatesPendingCount(_pendingMobileUpdatesCount);
                })));

            ContentPanel.Controls.Add(_dashboardView);
        }

        private void homeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowHomePage();
        }

        private void UpdateMenuVisibilityAfterLogin()
        {
            // Update account management menu visibility
            UpdateAccountManagementMenuVisibility();

            // Update email & SMTP menu visibility
            UpdateEmailSmtpMenuVisibility();

            // Update admin menu visibility
            UpdateAdminMenuVisibility();

            // Update user status label
            UserStatusLbl.Text = $"Logged in as: {Yakult.Inventory.App.Session.AppSession.CurrentUserName}  |  {Yakult.Inventory.App.Session.AppSession.CurrentEmail}";

            // Update reset password button visibility
            UpdateResetPasswordButtonVisibility();
        }

        private void LogOutOnMenu_Click(object sender, EventArgs e)
        {
            // 1. Clear the current session
            Yakult.Inventory.App.Session.AppSession.Clear();

            // 2. Clear the content panel to remove any displayed page
            if (ContentPanel != null)
            {
                ContentPanel.Controls.Clear();
            }

            // 3. Hide the main form
            this.Hide();

            // 4. Show authentication dialog again
            if (!ShowAuthenticationDialog())
            {
                // User cancelled login, close the application
                Application.Exit();
            }
            else
            {

                // User logged in successfully, refresh the UI
                UpdateMenuVisibilityAfterLogin();
                
                // Return to portal selection instead of showing home page

                // User logged in successfully; return to the portal selector flow

                ShowPortalAndNavigate();
            }
        }

        // Menu event handlers
        private void companyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            addCompanyToolStripMenuItem_Click(sender, e); // Reuse the same logic
        }

        private void departmentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Security.PermissionResolver.HasPageAccess("AddDepartmentDialog"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new AddDepartmentDialog())
            {
                dialog.ShowDialog(this);
            }
        }

        private void branchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Security.PermissionResolver.HasPageAccess("AddBranchDialog"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new AddBranchDialog())
            {
                dialog.ShowDialog(this);
            }
        }

        // NEW: Employee menu handler
        private void employeeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowAddEmployeePage();
        }

        // NEW: Item menu handler
        private void itemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowAddItemPage();
        }

        // NEW: Request menu handler
        private void requestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowAddRequestPage();
        }

        private void vendorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Security.PermissionResolver.HasPageAccess("AddVendorDialog"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new AddVendorDialog())
            {
                dialog.ShowDialog(this);
            }
        }

        // NEW: View Requests menu handler
        //private void viewRequestsToolStripMenuItem_Click(object sender, EventArgs e)
        //{
        //    ShowViewRequestsPage();
        //}

        // NEW: View Inventory menu handler
        //private void viewInventoryToolStripMenuItem_Click(object sender, EventArgs e)
        //{
        //    ShowViewInventoryPage();
        //}

        // NEW: View Items menu handler
        //private void viewItemsToolStripMenuItem_Click(object sender, EventArgs e)
        //{
        //    ShowViewItemsPage();
        //}

        // NEW: View Employees menu handler
        //private void viewEmployeesToolStripMenuItem_Click(object sender, EventArgs e)
        //{
        //    ShowViewEmployeesPage();
        //}

        // NEW: View Companies menu handler
        private void viewCompaniesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowViewCompaniesPage();
        }

        // NEW: View Branches menu handler
        private void viewBranchesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowViewBranchesPage();
        }

        // NEW: View Departments menu handler
        private void viewDepartmentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowViewDepartmentsPage();
        }

        // NEW: View Sets menu handler
        private void viewSetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowViewSetsPage();
        }

        // NEW: Bulk Add menu handler
        // COMMENTED OUT: Feature incomplete - missing transaction methods in repositories
        //private void bulkAddToolStripMenuItem_Click(object sender, EventArgs e)
        //{
        //    ShowBulkAddMasterDataPage();
        //}

        private void addCompanyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Security.PermissionResolver.HasPageAccess("AddCompanyDialog"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new CompanyDialog())
            {
                var result = dialog.ShowDialog();
                if (result == DialogResult.OK && dialog.ResultCompany != null)
                {
                    SaveCompanyToDatabase(dialog.ResultCompany);
                }
            }
        }

        // NEW: Independent Department Entry
        private void ShowAddDepartmentPage()
        {
            ContentPanel.Controls.Clear();

            var page = new AddDepartmentPage();
            page.Submitted += SaveDepartmentToDatabase;
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        private void ShowRoleManagementPage()
        {
            if (!Session.AppSession.IsDeveloper && !Session.AppSession.HasRole("Admin"))
            {
                MessageBox.Show("Access denied. Admin or Developer privileges required.",
                    "Unauthorized",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new RoleManagementPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: Independent Branch Entry
        private void ShowAddBranchPage()
        {
            ContentPanel.Controls.Clear();

            var page = new AddBranchPage_New();  // Use the new version with dynamic sections!
            page.Submitted += (branches) =>
            {
                MessageBox.Show($"Added {branches.Count} branch(es)!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: Employee Entry
        private void ShowAddEmployeePage()
        {
            if (!Security.PermissionResolver.HasPageAccess("AddEmployeePage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new EmployeeDialog())
            {
                var result = dialog.ShowDialog();
                if (result == DialogResult.OK && dialog.ResultEmployee != null)
                {
                    SaveEmployeeToDatabase(dialog.ResultEmployee);
                }
            }
        }

        // NEW: Item Entry
        private void ShowAddItemPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("AddItemPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new BatchAddItemDialog())
            {
                dialog.ShowDialog(this);
            }
        }

        // NEW: Request Entry
        private void ShowAddRequestPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("AddRequestPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new BatchAddRequestDialog())
            {
                dialog.ShowDialog(this);
            }
        }

        // NEW: View Requests
        private void ShowViewRequestsPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewRequestsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewRequestsPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: Partially Fulfilled Requests (WPF)
        private void ShowPartiallyFulfilledRequestsPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("PartiallyFulfilledRequestsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new Yakult.Inventory.App.Forms.Request.PartiallyFulfilledRequestsWpfHost();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: Unfulfilled Requests (WPF)
        private void ShowUnfulfilledRequestsPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("UnfulfilledRequestsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new Yakult.Inventory.App.Forms.Request.UnfulfilledRequestsWpfHost();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: Consumable Models (Ink/Toner/Print Head) (WPF)
        private void ShowConsumableModelsPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ConsumableModelsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new Yakult.Inventory.App.Forms.Consumables.ConsumableModelsWpfHost();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: View Cartridge Requests
        private void ShowCartridgeRequestsPage()
        {
            ContentPanel.Controls.Clear();

            var page = new ViewCartridgeRequestsPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: View Cartridges For Refill
        private void ShowCartridgesForRefillPage()
        {
            ContentPanel.Controls.Clear();

            var page = new ViewCartridgesForRefillPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: Brand New Cartridges
        private void ShowBrandNewCartridgesPage()
        {
            ContentPanel.Controls.Clear();

            var page = new ViewBrandNewCartridgesPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: Cartridge Tracking
        private void ShowCartridgeTrackingPage()
        {
            ContentPanel.Controls.Clear();

            var page = new ViewCartridgeTrackingPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        private void ShowUnfulfilledCartridgeExchangesPage()
        {
            ContentPanel.Controls.Clear();

            var page = new UnfulfilledCartridgeExchangesPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        private void ShowCartridgeExchangeForm()
        {
            using (var form = new CartridgeManagementWpfHost())
            {
                form.ShowDialog(this);
            }
        }

        // NEW: View Inventory
        private void ShowViewInventoryPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewInventoryPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewInventoryPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: View Items
        private void ShowViewItemsPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewItemsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewItemsPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        private void ShowViewRepairedItemsPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewRepairedItemsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewRepairedItemsPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: View Employees
        private void ShowViewEmployeesPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewEmployeesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewEmployeePage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: View Companies
        private void ShowViewCompaniesPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewCompaniesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewCompanyPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: View Branches
        private void ShowViewBranchesPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewBranchesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewBranchPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: Branch Assignments
        private void ShowViewBranchAssignmentsPage()
        {
            ContentPanel.Controls.Clear();

            var page = new ViewBranchAssignmentPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: View Departments
        private void ShowViewDepartmentsPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewDepartmentsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewDepartmentPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: View Categories
        private void ShowViewCategoriesPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewCategoriesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewCategoryPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: View Sets
        private void ShowViewSetsPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewSetsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewSetPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: Send Notifications (Sets)
        private void ShowSetDispatchNotificationPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("SetDispatchNotificationPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new Yakult.Inventory.App.Pages.Set.SetDispatchNotificationPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: View Invoice Report (summary of sets as invoices)
        private void ShowViewInvoiceReportPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewInvoicePage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewInvoiceReportPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: Invoice License Review — review queue for invoice lines not yet classified as
        // licensed/non-licensed (dbo.vw_InvoiceNonLicensedItems). See ViewInvoiceLicenseReviewPage.
        private void ShowInvoiceLicenseReviewPage()
        {
            if (!Session.AppSession.IsSuperAdmin)
            {
                MessageBox.Show("Access denied. Super Admin privileges required.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Security.PermissionResolver.HasPageAccess("InvoiceLicenseReviewPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewInvoiceLicenseReviewPage();
            _invoiceLicenseReviewPage = page;
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: Non-Licensed Invoices — read-only report of every Hardware invoice line already
        // confirmed as NonLicensed via Invoice License Review (dbo.vw_ConfirmedNonLicensedInvoiceItems).
        private void ShowNonLicensedInvoicesPage()
        {
            if (!Session.AppSession.IsSuperAdmin)
            {
                MessageBox.Show("Access denied. Super Admin privileges required.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Security.PermissionResolver.HasPageAccess("NonLicensedInvoicesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewNonLicensedInvoicesPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        private void ShowViewUpdatesPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewUpdatesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewUpdatesPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        private void ShowViewItemMovementAuditPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewItemMovementAuditPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewItemMovementAuditPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        private void ShowOwnershipHistoryPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewOwnershipHistoryPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new Yakult.Inventory.App.Pages.AuditHistory.ViewOwnershipHistoryPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: View Renewals (software/license and service renewals)
        private void ShowViewRenewalsPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewRenewalPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewRenewalPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // Notification row navigation: delegates to NavigateToItemPage, same as the global search.
        // destination = "Renewal" when the row is a Set (SetCode search), "Items" for standalone items.
        private void NavigateToLicenseItem(string itemName, string destination)
        {
            if (isNotificationPanelVisible) btnNotification_Click(this, EventArgs.Empty);
            NavigateToItemPage(null, destination, itemName);
        }

        // Notification row navigation: opens ViewWarrantyPage and pre-filters to the clicked item.
        private void NavigateToWarrantyItem(string itemName)
        {
            if (isNotificationPanelVisible) btnNotification_Click(this, EventArgs.Empty);
            NavigateToItemPage(null, "Warranty", itemName);
        }

        // Notification row navigation: opens ViewUpdatesPage (no filter API on that page yet).
        private void NavigateToMobileItem(string serialNumber)
        {
            if (isNotificationPanelVisible) btnNotification_Click(this, EventArgs.Empty);
            ShowViewUpdatesPage();
        }

        // One-value lookup helper for Activity-row navigation (best-effort; null on any failure).
        private string LookupActivityScalar(string sql, int id)
        {
            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    con.Open();
                    return cmd.ExecuteScalar() as string;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainForm.LookupActivityScalar] failed: {ex.Message}");
                return null;
            }
        }

        // Activity feed row navigation. referenceId meaning depends on the notification type:
        //   ITEM_ADDED       -> dbo.Item.ItemId       (Items page, filtered)
        //   SET_CREATED      -> dbo.[Set].SetId       (Set / Invoice list, filtered by SetCode)
        //   REQUEST_CREATED  -> dbo.Request.ReqId     (Requests page, filtered by item name)
        //   RENEWAL_CREATED  -> dbo.Item.ItemId       (Renewals page, filtered by item name)
        private void NavigateToActivityItem(string notificationType, int referenceId, string source)
        {
            if (isNotificationPanelVisible) btnNotification_Click(this, EventArgs.Empty);

            if (notificationType == Models.NotificationType.RequestCreated)
            {
                if (referenceId <= 0) return;
                var itemName = LookupActivityScalar(
                    "SELECT i.Name FROM dbo.Request r JOIN dbo.Item i ON i.ItemId = r.ItemId WHERE r.ReqId = @Id", referenceId);
                NavigateToItemPage(null, "Request", itemName);
                return;
            }

            if (notificationType == Models.NotificationType.RenewalCreated)
            {
                if (referenceId <= 0) return;
                var itemName = LookupActivityScalar("SELECT Name FROM dbo.Item WHERE ItemId = @Id", referenceId);
                NavigateToItemPage(null, "Renewal", itemName);
                return;
            }

            if (notificationType == Models.NotificationType.SetCreated)
            {
                if (referenceId <= 0) return;

                // Mirror the home-page search redirect: open the list page and pre-filter it to
                // this Set's row (search by SetCode), rather than popping a modal detail dialog.
                string setCode = null;
                try
                {
                    using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                    using (var cmd = new SqlCommand("SELECT SetCode FROM dbo.[Set] WHERE SetId = @Id", con))
                    {
                        cmd.Parameters.AddWithValue("@Id", referenceId);
                        con.Open();
                        setCode = cmd.ExecuteScalar() as string;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainForm.NavigateToActivityItem] SetCode lookup failed: {ex.Message}");
                }

                string query = string.IsNullOrWhiteSpace(setCode) ? referenceId.ToString() : setCode;
                string dest  = string.Equals(source, "Invoice", StringComparison.OrdinalIgnoreCase) ? "Invoice" : "Set";
                NavigateToItemPage(null, dest, query);
                return;
            }

            if (notificationType == Models.NotificationType.ItemAdded && referenceId > 0)
            {
                string itemName = null;
                try
                {
                    using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                    using (var cmd = new SqlCommand("SELECT Name FROM dbo.Item WHERE ItemId = @Id", con))
                    {
                        cmd.Parameters.AddWithValue("@Id", referenceId);
                        con.Open();
                        itemName = cmd.ExecuteScalar() as string;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainForm.NavigateToActivityItem] name lookup failed: {ex.Message}");
                }

                NavigateToItemPage(null, "Items", itemName);
            }
        }

        // NEW: View Renewals grouped by original item/set chain
        private void ShowViewRenewalsGroupPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewRenewalGroupPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new Yakult.Inventory.App.Pages.Renewal.ViewRenewalGroupPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // NEW: Employee Management (Admin Only)
        private void ShowEmployeeManagementPage()
        {
            // Check authorization
            if (!Session.AppSession.IsAdmin)
            {
                MessageBox.Show("Access denied. Admin privileges required.",
                    "Unauthorized",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new EmployeeManagementWpfHost();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // Account Permissions (SuperAdmin Only)
        private void ShowAccountPermissionsPage()
        {
            if (!Session.AppSession.IsSuperAdmin)
            {
                MessageBox.Show("Access denied. Super Admin privileges required.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new AccountPermissionsPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // Department Accounts (Admin Only)
        private void ShowDepartmentAccountsPage()
        {
            if (!Session.AppSession.IsAdmin)
            {
                MessageBox.Show("Access denied. Admin privileges required.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new DepartmentAccountsWpfHost();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        /// <summary>
        /// Super Admin only, and not restricted on User Access > Pages
        /// (PermissionItem 'UserAccountManagementPage').
        /// </summary>
        private static bool CanOpenUserAccountManagement =>
            Session.AppSession.IsSuperAdmin &&
            Yakult.Inventory.App.Security.PermissionResolver.HasPageAccess("UserAccountManagementPage");

        // User Account Management (SuperAdmin Only)
        private void ShowUserAccountManagementPage()
        {
            // Check authorization
            if (!CanOpenUserAccountManagement)
            {
                MessageBox.Show("Access denied. Super Admin privileges required.",
                    "Unauthorized",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new UserAccountManagementWpfHost();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // Admin Methods
        private void ShowUserActivityPage()
        {
            if (!Session.AppSession.IsAdmin)
            {
                MessageBox.Show("Access denied. Admin privileges required.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new UserActivityWpfHost();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        // Email & SMTP Management Methods
        private void ShowSmtpSettingsPage()
        {
            ContentPanel.Controls.Clear();

            var page = new Yakult.Inventory.App.Forms.Admin.EmailManagement.SmtpSendSettingsWpfHost();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        private void ShowEmailConfigurationPage()
        {
            using (var form = new Forms.SystemSettings.SystemEmailSettingsForm())
            {
                form.ShowDialog(this);
            }
        }

        private void ShowEmailLogsPage()
        {
            ContentPanel.Controls.Clear();

            var page = new Yakult.Inventory.App.Forms.Admin.EmailManagement.EmailLogsWpfHost();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }


        private async void connectionCheckTimer_Tick(object sender, EventArgs e)
        {
            await UpdateConnectionStatusAsync();
        }

        private async Task UpdateConnectionStatusAsync()
        {
            if (Interlocked.Exchange(ref _isCheckingConnection, 1) == 1)
                return;

            try
            {
                if (lblConnectionStatus == null)
                    return;

                lblConnectionStatus.Text = "Checking connection...";
                lblConnectionStatus.ForeColor = Color.White;

                var response = await _connectionHttpClient.GetAsync(ApiHealthUrl);
                if (response.IsSuccessStatusCode)
                {
                    lblConnectionStatus.Visible = true;
                    lblConnectionStatus.Text = "Connected to system";
                    lblConnectionStatus.ForeColor = Color.White;

                    int pendingCount = 0;
                    try
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            dynamic obj = JsonConvert.DeserializeObject(json);
                            if (obj != null && obj.pendingCount != null)
                            {
                                pendingCount = (int)obj.pendingCount;
                            }
                        }
                    }
                    catch
                    {
                        pendingCount = 0;
                    }

                    UpdateMobileUpdatesSection(pendingCount);
                }
                else
                {
                    //lblConnectionStatus.Text = "Unable to reach system";
                    //lblConnectionStatus.ForeColor = Color.White;
                    lblConnectionStatus.Visible = false;

                    UpdateMobileUpdatesSection(0);
                }
            }
            catch (Exception ex)
            {
                if (lblConnectionStatus == null)
                    return;

                System.Diagnostics.Debug.WriteLine(ex);
                //lblConnectionStatus.Text = "Unable to reach system";
                //lblConnectionStatus.ForeColor = Color.White;
                lblConnectionStatus.Visible = false;

                UpdateMobileUpdatesSection(0);
            }
            finally
            {
                Interlocked.Exchange(ref _isCheckingConnection, 0);
            }
        }

        private void UpdateMobileUpdatesSection(int pendingCount)
        {
            _pendingMobileUpdatesCount = pendingCount;
            _dashboardView?.SetMobileUpdatesPendingCount(pendingCount);
            _notificationCenterHost?.SetMobilePendingCount(pendingCount);

            UpdateNotificationBadge();
        }

        /// <summary>
        /// Refreshes the bell button badge from both signals it now represents: pending mobile
        /// updates (a standing "Reminders" condition) and unread Inventory System "Activity"
        /// notifications. Safe to call from any of the pollers / the notification host.
        /// </summary>
        private void UpdateNotificationBadge()
        {
            if (btnNotification == null) return;

            int mobile   = _pendingMobileUpdatesCount;
            int activity  = _notificationCenterHost?.UnreadActivityCount ?? 0;
            int total    = mobile + activity;

            if (total > 0)
            {
                btnNotification.Text = $"🔔 {total}";
                btnNotification.BackColor = Color.OrangeRed;
            }
            else
            {
                btnNotification.Text = "🔔";
                btnNotification.BackColor = Color.Orange;
            }
        }

        // NEW: Bulk Add Master Data
        // COMMENTED OUT: Feature incomplete
        //private void ShowBulkAddMasterDataPage()
        //{
        //    ContentPanel.Controls.Clear();
        //
        //    var page = new BulkAddMasterDataPage();
        //    page.BulkSubmitted += SaveBulkMasterData;
        //    ContentPanel.Controls.Add(page);
        //    page.Dock = DockStyle.Fill;
        //}

        private void SaveCompanyToDatabase(CompanyDto data)
        {
            var cs = DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(cs))
            {
                MessageBox.Show("Connection string not found.", "Config Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (var con = new SqlConnection(cs))
            {
                con.Open();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        // STEP 1: Insert Company FIRST and get its ID
                        int companyId;
                        using (var cmd = new SqlCommand(@"
                            INSERT INTO dbo.Company
                                (Name, Description, DateCreated, Createdby)
                            OUTPUT INSERTED.ComId
                            VALUES
                                (@Name, @Desc, @DateCreated, @Createdby);
                        ", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@Name", data.Name ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Desc", (object)data.Description ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@DateCreated", data.DateCreated);
                            cmd.Parameters.AddWithValue("@Createdby", data.CreatedByUserId);
                            companyId = (int)cmd.ExecuteScalar();
                        }

                        // STEP 2: Insert Departments (only if there are any)
                        var departmentIds = new List<int>();
                        if (data.Departments != null && data.Departments.Count > 0)
                        {
                            foreach (var d in data.Departments)
                            {
                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO dbo.Department
                                        (Name, Description, DateCreated, Createdby, ComId)
                                    OUTPUT INSERTED.DeptId
                                    VALUES
                                        (@Name, @Desc, @DateCreated, @Createdby, @ComId);
                                ", con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@Name", d.Name ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Desc", (object)d.Description ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@DateCreated", d.DateCreated);
                                    cmd.Parameters.AddWithValue("@Createdby", d.CreatedByUserId);
                                    cmd.Parameters.AddWithValue("@ComId", companyId);
                                    
                                    int deptId = (int)cmd.ExecuteScalar();
                                    departmentIds.Add(deptId);
                                }
                            }
                        }

                        // STEP 3: Insert Branches (only if there are any)
                        if (data.Branches != null && data.Branches.Count > 0)
                        {
                            // Link to first department if available, otherwise NULL
                            int? defaultDeptId = departmentIds.Count > 0 ? departmentIds[0] : (int?)null;
                            
                            foreach (var b in data.Branches)
                            {
                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO dbo.Branch
                                        (Name, Description, DateCreated, Createdby, ComId, DeptId)
                                    VALUES
                                        (@Name, @Desc, @DateCreated, @Createdby, @ComId, @DeptId);
                                ", con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@Name", b.Name ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Desc", (object)b.Description ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@DateCreated", b.DateCreated);
                                    cmd.Parameters.AddWithValue("@Createdby", b.CreatedByUserId);
                                    cmd.Parameters.AddWithValue("@ComId", companyId);
                                    cmd.Parameters.AddWithValue("@DeptId", defaultDeptId.HasValue ? (object)defaultDeptId.Value : DBNull.Value);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        tx.Commit();
                        
                        string message = $"✅ Company saved successfully!\n\n" +
                            $"Company: {data.Name}";
                        
                        if (data.Departments != null && data.Departments.Count > 0)
                            message += $"\nDepartments: {data.Departments.Count}";
                        
                        if (data.Branches != null && data.Branches.Count > 0)
                            message += $"\nBranches: {data.Branches.Count}";
                        
                        MessageBox.Show(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        
                        // Return to home page after successful save
                        ShowHomePage();
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        MessageBox.Show("❌ Save failed: " + ex.Message + "\n\n" + ex.StackTrace, 
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // NEW: Save Department independently
        private void SaveDepartmentToDatabase(DepartmentDto data)
        {
            var cs = DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(cs))
            {
                MessageBox.Show("Connection string not found.", "Config Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (var con = new SqlConnection(cs))
            {
                con.Open();
                try
                {
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.Department
                            (Name, Description, DateCreated, Createdby, ComId)
                        VALUES
                            (@Name, @Desc, @DateCreated, @Createdby, @ComId);
                    ", con))
                    {
                        cmd.Parameters.AddWithValue("@Name", data.Name ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Desc", (object)data.Description ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DateCreated", data.DateCreated);
                        cmd.Parameters.AddWithValue("@Createdby", data.CreatedByUserId);
                        // ComId is now NULLABLE - if not associated, insert NULL
                        cmd.Parameters.AddWithValue("@ComId", data.CompanyId.HasValue ? (object)data.CompanyId.Value : DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }

                    MessageBox.Show("✅ Department saved successfully.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    // Return to home page after successful save
                    ShowHomePage();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("❌ Save failed: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // NEW: Save Branch independently
        private void SaveBranchToDatabase(BranchDto data)
        {
            var cs = DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(cs))
            {
                MessageBox.Show("Connection string not found.", "Config Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (var con = new SqlConnection(cs))
            {
                con.Open();
                try
                {
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.Branch
                            (Name, Description, DateCreated, Createdby, ComId, DeptId, IsFactory, IsDepot, IsCenter, CenterRegion, IsDistributor)
                        VALUES
                            (@Name, @Desc, @DateCreated, @Createdby, @ComId, @DeptId, @IsFactory, @IsDepot, @IsCenter, @CenterRegion, @IsDistributor);
                    ", con))
                    {
                        cmd.Parameters.AddWithValue("@Name", data.Name ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Desc", (object)data.Description ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DateCreated", data.DateCreated);
                        cmd.Parameters.AddWithValue("@Createdby", data.CreatedByUserId);
                        // ComId is NULLABLE - if not associated, insert NULL
                        cmd.Parameters.AddWithValue("@ComId", data.CompanyId.HasValue ? (object)data.CompanyId.Value : DBNull.Value);
                        // DeptId is NULLABLE - if not associated, insert NULL
                        cmd.Parameters.AddWithValue("@DeptId", data.DepartmentId.HasValue ? (object)data.DepartmentId.Value : DBNull.Value);
                        // Branch category fields
                        cmd.Parameters.AddWithValue("@IsFactory", data.IsFactory);
                        cmd.Parameters.AddWithValue("@IsDepot", data.IsDepot);
                        cmd.Parameters.AddWithValue("@IsCenter", data.IsCenter);
                        cmd.Parameters.AddWithValue("@CenterRegion", (object)data.CenterRegion ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsDistributor", data.IsDistributor);
                        cmd.ExecuteNonQuery();
                    }

                    MessageBox.Show("✅ Branch saved successfully.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    // Return to home page after successful save
                    ShowHomePage();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("❌ Save failed: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // NEW: Save Employee
        private void SaveEmployeeToDatabase(EmployeeDto data)
        {
            var cs = DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(cs))
            {
                MessageBox.Show("Connection string not found.", "Config Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (var con = new SqlConnection(cs))
            {
                con.Open();
                try
                {    
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.Employee
                            (Name, Description, DateCreated, Createdby, ComId, BranchId, DeptId, Position, EmployeeNumber)
                        VALUES
                            (@Name, @Description, @DateCreated, @Createdby, @ComId, @BranchId, @DeptId, @Position, @EmployeeNumber);
                    ", con))
                    {
                        cmd.Parameters.AddWithValue("@Name", data.Name ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Description", (object)data.Description ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DateCreated", data.DateCreated);
                        cmd.Parameters.AddWithValue("@Createdby", data.CreatedByUserId);
                        cmd.Parameters.AddWithValue("@ComId", data.CompanyId);
                        cmd.Parameters.AddWithValue("@BranchId", data.BranchId);
                        cmd.Parameters.AddWithValue("@DeptId", data.DepartmentId.HasValue ? (object)data.DepartmentId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@Position", string.IsNullOrWhiteSpace(data.Position) ? (object)DBNull.Value : data.Position.Trim());
                        cmd.Parameters.AddWithValue("@EmployeeNumber", string.IsNullOrWhiteSpace(data.EmployeeNumber) ? (object)DBNull.Value : data.EmployeeNumber.Trim());
                        cmd.ExecuteNonQuery();
                    }

                    MessageBox.Show("✅ Employee saved successfully.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    // Return to home page after successful save
                    ShowHomePage();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("❌ Save failed: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // NEW: Save Item
        private void SaveItemToDatabase(ItemDto data)
        {
            try
            {
                var repo = new ItemRepository();
                int itemId = repo.AddItem(data);

                MessageBox.Show($"✅ Item saved successfully (ID: {itemId}).", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                // Return to home page after successful save
                ShowHomePage();
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Save failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // NEW: Save Request
        private void SaveRequestToDatabase(RequestDto data)
        {
            try
            {
                var repo = new RequestRepository();
                
                // First, add the request to get the ReqId
                int reqId = repo.AddRequest(data);
                
                // Check if status is "Submitted" - if so, process it immediately
                if (data.Status == "Submitted")
                {
                    // Use SubmitRequest to create Inventory entry and update stock
                    bool success = repo.SubmitRequest(reqId, data.CreatedByUserId);
                    
                    if (success)
                    {
                        MessageBox.Show(
                            $"✅ Request submitted successfully (ID: {reqId})!\n\n" +
                            $"Employee: {data.EmployeeName}\n" +
                            $"Item: {data.ItemName}\n" +
                            $"Quantity: {data.Quantity}\n" +
                            $"Status: {data.Status}\n\n" +
                            "✓ Inventory entry created\n" +
                            "✓ Stock updated",
                            "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    // Normal request (not submitted yet)
                    MessageBox.Show(
                        $"✅ Request saved successfully (ID: {reqId}).\n\n" +
                        $"Employee: {data.EmployeeName}\n" +
                        $"Item: {data.ItemName}\n" +
                        $"Quantity: {data.Quantity}\n" +
                        $"Status: {data.Status}",
                        "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                
                // Return to home page after successful save
                ShowHomePage();
            }
            catch (InvalidOperationException ex)
            {
                // Stock validation error
                MessageBox.Show(
                    $"Cannot create request:\n\n{ex.Message}\n\n" +
                    "Please check stock availability.",
                    "Insufficient Stock",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Save failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void viewInventoryToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            ShowViewInventoryPage();
        }

        private void viewItemsToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            ShowViewItemsPage();
        }

        private void viewRepairedItemsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowViewRepairedItemsPage();
        }

        private void viewEmployeesToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            ShowViewEmployeesPage();
        }

        private void viewCompaniesToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            ShowViewCompaniesPage();
        }

        private void viewBranchesToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            ShowViewBranchesPage();
        }

        private void viewDepartmentsToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            ShowViewDepartmentsPage();
        }

        private void viewCategoriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowViewCategoriesPage();
        }

        private void bulkAddToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void viewSetsToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            ShowViewSetsPage();
        }

        private void batchAddItemsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new BatchAddItemDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    MessageBox.Show("Items added successfully! You can view them in 'View Items'.",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void batchAddRequToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new BatchAddRequestDialog())
            {
                dialog.ShowDialog();
            }
        }

        private void masterDataToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void viewRequestsToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ShowViewRequestsPage();
        }

        private void viewInvoicesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowViewInvoiceReportPage();
        }

        private void ShowInvoicePreparationPage()
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewInvoicePreparationPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new Yakult.Inventory.App.Pages.InvoicePreparation.ViewInvoicePreparationPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        private void viewRenewalsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowViewRenewalsPage();
        }

        private void viewVendorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewVendorsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewVendorsPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        private void viewWarrantyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewWarrantyPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewWarrantyPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        private void viewReceiptsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewReceiptsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new Yakult.Inventory.App.Wpf.Receipt.ReceiptSetsWpfHost();
            page.GoToManageSetsRequested += () => ShowViewSetsPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        private void ViewFixedAssets_Click(object sender, EventArgs e)
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewFixedAssetsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewFixedAssetsPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        private void ViewAssets_Click(object sender, EventArgs e)
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewAssetPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new Yakult.Inventory.App.Pages.Asset.ViewAssetPage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        private void viewUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowViewUpdatesPage();
        }

        private void viewItemMovementAuditToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowViewItemMovementAuditPage();
        }

        private void softwareServiceSetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Security.PermissionResolver.HasPageAccess("AddSalesInvoiceSet"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new SoftwareServiceSetDialog())
            {
                var result = dialog.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    // After successfully creating a set, navigate to the Invoice Report view
                    ShowViewInvoiceReportPage();
                }
            }
        }

        private void requestSetMenuItem_Click(object sender, EventArgs e)
        {
            if (!Security.PermissionResolver.HasPageAccess("AddRequestSet"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var addDialog = new AddSetPage(showStatus: false))
            {
                if (addDialog.ShowDialog(this) == DialogResult.OK)
                {
                    if (addDialog.Tag is int newSetId && newSetId > 0)
                    {
                        using (var detailPage = new ViewSetDetailPage(newSetId))
                        {
                            detailPage.ShowDialog(this);
                        }
                    }
                }
            }
        }

        // Notification Panel Methods
        private void InitializeNotificationPanel()
        {
            notificationPanel.Padding = Padding.Empty;

            _notificationCenterHost = new NotificationCenterHost
            {
                // Section-level "View All →" links
                OnViewAllLicense  = () => { if (isNotificationPanelVisible) btnNotification_Click(this, EventArgs.Empty); ShowViewRenewalsPage(); },
                OnViewAllWarranty = () => { if (isNotificationPanelVisible) btnNotification_Click(this, EventArgs.Empty); viewWarrantyToolStripMenuItem_Click(this, EventArgs.Empty); },
                OnViewAllMobile   = () => { if (isNotificationPanelVisible) btnNotification_Click(this, EventArgs.Empty); ShowViewUpdatesPage(); },
                OnClose           = () => { if (isNotificationPanelVisible) btnNotification_Click(this, EventArgs.Empty); },

                // Row-level click: navigate directly to the item on the target page
                OnNavigateToLicenseItem  = (name, dest) => NavigateToLicenseItem(name, dest),
                OnNavigateToWarrantyItem = name => NavigateToWarrantyItem(name),
                OnNavigateToMobileItem   = sn   => NavigateToMobileItem(sn),

                // Activity feed
                OnNavigateToActivityItem = (type, refId, src) => NavigateToActivityItem(type, refId, src),
                OnActivityChanged        = () => UpdateNotificationBadge(),
            };

            notificationPanel.Controls.Clear();
            notificationPanel.Controls.Add(_notificationCenterHost);
        }

        private async void btnNotification_Click(object sender, EventArgs e)
        {
            if (!isNotificationPanelVisible)
            {
                await LoadLicenseExpiryNotifications();
                PositionNotificationPanel();
                notificationPanel.Visible = true;
                notificationPanel.BringToFront();
                isNotificationPanelVisible = true;
            }
            else
            {
                // Clear WPF keyboard focus before hiding to prevent the
                // ElementHost focus-restoration crash (see CLAUDE.md).
                _notificationCenterHost?.PrepareForHide();
                notificationPanel.Visible = false;
                isNotificationPanelVisible = false;
            }
        }

        private void PositionNotificationPanel()
        {
            // Position the notification panel on the right side of the form, below the green top bar
            // The top bar has a height of 70px, so position it right below
            const int panelWidth = 460;
            const int topBarHeight = 70;
            const int marginRight = 10;
            const int marginTop = 5;

            // Set panel size to be responsive
            notificationPanel.Width = panelWidth;
            notificationPanel.Height = this.ClientSize.Height - topBarHeight - statusStrip1.Height - marginTop - 10;

            // Position on the right side below the top bar
            int targetX = this.ClientSize.Width - panelWidth - marginRight;
            notificationPanel.Left = targetX;
            notificationPanel.Top = topBarHeight + marginTop;
        }

        private async Task LoadLicenseExpiryNotifications()
        {
            if (_notificationCenterHost == null) return;
            try
            {
                await _notificationCenterHost.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load notifications:\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // NEW: Save Bulk Master Data
        // COMMENTED OUT: Feature incomplete - missing transaction methods in repositories
        /*private void SaveBulkMasterData(List<DepartmentDto> departments, List<BranchDto> branches,
            List<EmployeeDto> employees, List<ItemDto> items, List<RequestDto> requests)
        {
            var cs = ConfigurationManager.ConnectionStrings[
                "Yakult.Inventory.App.Properties.Settings.Yakult_Inventory_SystemConnectionString"
            ]?.ConnectionString;

            if (string.IsNullOrWhiteSpace(cs))
            {
                MessageBox.Show("Connection string not found.", "Config Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int successCount = 0;
            int errorCount = 0;
            var errors = new List<string>();

            using (var con = new SqlConnection(cs))
            {
                con.Open();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        // 1. Save Departments
                        foreach (var dept in departments)
                        {
                            try
                            {
                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO dbo.Department
                                        (Name, Description, DateCreated, Createdby, ComId)
                                    VALUES
                                        (@Name, @Desc, @DateCreated, @Createdby, @ComId);
                                ", con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@Name", dept.Name ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Desc", (object)dept.Description ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@DateCreated", dept.DateCreated);
                                    cmd.Parameters.AddWithValue("@Createdby", dept.CreatedByUserId);
                                    cmd.Parameters.AddWithValue("@ComId", dept.CompanyId.HasValue ? (object)dept.CompanyId.Value : DBNull.Value);
                                    cmd.ExecuteNonQuery();
                                    successCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                errors.Add($"Department '{dept.Name}': {ex.Message}");
                            }
                        }

                        // 2. Save Branches
                        foreach (var branch in branches)
                        {
                            try
                            {
                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO dbo.Branch
                                        (Name, Description, DateCreated, Createdby, ComId, DeptId, IsFactory, IsDepot, IsCenter, CenterRegion, IsDistributor)
                                    VALUES
                                        (@Name, @Desc, @DateCreated, @Createdby, @ComId, @DeptId, @IsFactory, @IsDepot, @IsCenter, @CenterRegion, @IsDistributor);
                                ", con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@Name", branch.Name ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Desc", (object)branch.Description ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@DateCreated", branch.DateCreated);
                                    cmd.Parameters.AddWithValue("@Createdby", branch.CreatedByUserId);
                                    cmd.Parameters.AddWithValue("@ComId", branch.CompanyId.HasValue ? (object)branch.CompanyId.Value : DBNull.Value);
                                    cmd.Parameters.AddWithValue("@DeptId", branch.DepartmentId.HasValue ? (object)branch.DepartmentId.Value : DBNull.Value);
                                    cmd.Parameters.AddWithValue("@IsFactory", branch.IsFactory);
                                    cmd.Parameters.AddWithValue("@IsDepot", branch.IsDepot);
                                    cmd.Parameters.AddWithValue("@IsCenter", branch.IsCenter);
                                    cmd.Parameters.AddWithValue("@CenterRegion", (object)branch.CenterRegion ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@IsDistributor", branch.IsDistributor);
                                    cmd.ExecuteNonQuery();
                                    successCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                errors.Add($"Branch '{branch.Name}': {ex.Message}");
                            }
                        }

                        // 3. Save Employees
                        foreach (var emp in employees)
                        {
                            try
                            {
                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO dbo.Employee
                                        (Name, Description, DateCreated, Createdby, ComId, BranchId, DeptId, Position, EmployeeNumber)
                                    VALUES
                                        (@Name, @Description, @DateCreated, @Createdby, @ComId, @BranchId, @DeptId, @Position, @EmployeeNumber);
                                ", con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@Name", emp.Name ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Description", (object)emp.Description ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@DateCreated", emp.DateCreated);
                                    cmd.Parameters.AddWithValue("@Createdby", emp.CreatedByUserId);
                                    cmd.Parameters.AddWithValue("@ComId", emp.CompanyId);
                                    cmd.Parameters.AddWithValue("@BranchId", emp.BranchId);
                                    cmd.Parameters.AddWithValue("@DeptId", emp.DepartmentId.HasValue ? (object)emp.DepartmentId.Value : DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Position", string.IsNullOrWhiteSpace(emp.Position) ? (object)DBNull.Value : emp.Position.Trim());
                                    cmd.Parameters.AddWithValue("@EmployeeNumber", string.IsNullOrWhiteSpace(emp.EmployeeNumber) ? (object)DBNull.Value : emp.EmployeeNumber.Trim());
                                    cmd.ExecuteNonQuery();
                                    successCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                errors.Add($"Employee '{emp.Name}': {ex.Message}");
                            }
                        }

                        // 4. Save Items
                        var itemRepo = new ItemRepository();
                        foreach (var item in items)
                        {
                            try
                            {
                                itemRepo.AddItemWithTransaction(item, con, tx);
                                successCount++;
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                errors.Add($"Item '{item.Name}': {ex.Message}");
                            }
                        }

                        // 5. Save Requests
                        var requestRepo = new RequestRepository();
                        foreach (var req in requests)
                        {
                            try
                            {
                                req.EmpId = req.EmployeeId; // Set the alias
                                requestRepo.AddRequestWithTransaction(req, con, tx);
                                successCount++;
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                errors.Add($"Request for '{req.EmployeeName}': {ex.Message}");
                            }
                        }

                        tx.Commit();

                        string message = $"✅ Bulk save completed!\n\n" +
                            $"Success: {successCount}\n" +
                            $"Errors: {errorCount}";

                        if (errors.Count > 0)
                        {
                            message += "\n\nErrors:\n" + string.Join("\n", errors.Take(5));
                            if (errors.Count > 5)
                            {
                                message += $"\n...and {errors.Count - 5} more errors";
                            }
                        }

                        MessageBox.Show(message, errorCount > 0 ? "Partial Success" : "Success",
                            MessageBoxButtons.OK, errorCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

                        // Return to home page after save
                        ShowHomePage();
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        MessageBox.Show("❌ Bulk save failed: " + ex.Message, "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }*/

        private void viewArchiveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Security.PermissionResolver.HasPageAccess("ViewArchivePage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ContentPanel.Controls.Clear();

            var page = new ViewArchivePage();
            ContentPanel.Controls.Add(page);
            page.Dock = DockStyle.Fill;
        }

        private void ShowMyAccountPage()
        {
            try
            {
                ContentPanel.Controls.Clear();
                var page = new MyAccountPage();
                ContentPanel.Controls.Add(page);
                page.Dock = DockStyle.Fill;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error opening My Account page: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetPassword_Click(object sender, EventArgs e)
        {
            try
            {
                string connectionString = DatabaseConfig.ConnectionString;

                var userRepo = new Yakult.Inventory.App.Data.UserRepository(connectionString);
                using (var resetPasswordDialog = new ResetPasswordDialog(userRepo))
                {
                    var result = resetPasswordDialog.ShowDialog(this);
                    if (result == DialogResult.OK)
                    {
                        MessageBox.Show("Password reset successfully!", "Success", 
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error opening reset password dialog: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region Developer-Only Database Reset Shortcut

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Shift | Keys.D))
            {
                if (CanAccessDatabaseSwitcher())
                    HandleDeveloperDatabaseReset();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private static bool CanAccessDatabaseSwitcher()
        {
            if (Yakult.Inventory.App.Session.AppSession.IsDeveloper &&
                Yakult.Inventory.App.Session.AppSession.IsSuperAdmin)
                return true;

            return string.Equals(Yakult.Inventory.App.Session.AppSession.CurrentCompanyName,    "YPI",                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(Yakult.Inventory.App.Session.AppSession.CurrentDepartmentName, "INFORMATION TECHNOLOGY", StringComparison.OrdinalIgnoreCase)
                && string.Equals(Yakult.Inventory.App.Session.AppSession.CurrentBranchName,     "MANILA LIAISON OFFICE",  StringComparison.OrdinalIgnoreCase);
        }

        private void HandleDeveloperDatabaseReset()
        {
            using (var form = new Pages.Admin.DBConn.DatabaseSetupForm())
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    Application.Restart();
                    Environment.Exit(0);
                }
            }
        }

        #endregion

        #region System Settings Menu

        private System.Windows.Forms.ToolStripMenuItem systemSettingsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem emailSettingsToolStripMenuItem;

        private void InitializeSystemSettingsMenu()
        {
            // Create System Settings menu
            systemSettingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem
            {
                Name = "systemSettingsToolStripMenuItem",
                Text = "System Settings",
                BackColor = System.Drawing.Color.LightCyan
            };

            // Create Email Settings submenu
            emailSettingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem
            {
                Name = "emailSettingsToolStripMenuItem",
                Text = "Email Settings"
            };
            emailSettingsToolStripMenuItem.Click += EmailSettingsToolStripMenuItem_Click;

            // Add Email Settings to System Settings
            systemSettingsToolStripMenuItem.DropDownItems.Add(emailSettingsToolStripMenuItem);

            // Add System Settings to main menu (after View Data menu)
            if (menuStrip1.Items.Count >= 4)
            {
                menuStrip1.Items.Insert(4, systemSettingsToolStripMenuItem);
            }
            else
            {
                menuStrip1.Items.Add(systemSettingsToolStripMenuItem);
            }

            // Restrict to Admin/Developer only
            systemSettingsToolStripMenuItem.Visible =
                Yakult.Inventory.App.Session.AppSession.IsAdmin ||
                Yakult.Inventory.App.Session.AppSession.IsDeveloper;
        }

        private void EmailSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Check access rights (Admin or Developer only)
            if (!Yakult.Inventory.App.Session.AppSession.IsAdmin &&
                !Yakult.Inventory.App.Session.AppSession.IsDeveloper)
            {
                MessageBox.Show("Access denied. Email Settings are restricted to Admin and Developer users only.",
                    "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (var form = new Yakult.Inventory.App.Forms.SystemSettings.SystemEmailSettingsForm())
                {
                    form.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Email Settings: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

    }

    /// <summary>
    /// Intercepts Ctrl+Shift+S at the application message-loop level.
    /// Toggles developer stealth mode (suppresses UserActivityLog writes) when IsDeveloper is true.
    /// </summary>
    internal sealed class DevStealthKeyFilter : IMessageFilter
    {
        private const int WM_KEYDOWN = 0x0100;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_KEYDOWN)
                return false;

            var keys = (Keys)m.WParam | System.Windows.Forms.Control.ModifierKeys;

            if (keys != (Keys.Control | Keys.Shift | Keys.S))
                return false;

            if (!Yakult.Inventory.App.Session.AppSession.IsDeveloper)
                return false;

            Yakult.Inventory.App.Session.AppSession.SuppressActivityLogging =
                !Yakult.Inventory.App.Session.AppSession.SuppressActivityLogging;

            bool suppressed = Yakult.Inventory.App.Session.AppSession.SuppressActivityLogging;
            string state = suppressed ? "ON" : "OFF";
            MessageBox.Show(
                $"Developer stealth mode is now {state}.\n\n" +
                (suppressed
                    ? "Your actions will NOT be recorded in the User Activity log."
                    : "Your actions WILL be recorded in the User Activity log."),
                $"Activity Logging – {state}",
                MessageBoxButtons.OK,
                suppressed ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

            return true; // Consume the key — nothing else processes it
        }
    }
}
