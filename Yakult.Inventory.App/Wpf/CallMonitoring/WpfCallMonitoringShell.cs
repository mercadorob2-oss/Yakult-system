using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Forms.CallMonitoring;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    /// <summary>
    /// Pure WPF shell for the Call Monitoring module. Replaces the legacy
    /// WinForms <see cref="CallMonitoringDashboard"/> by hosting workspaces
    /// directly in a WPF <see cref="Window"/> without ElementHost wrappers.
    /// </summary>
    public sealed class WpfCallMonitoringShell : Window, ICallMonitoringNavigator
    {
        // --- Layout ---
        private readonly Grid _rootGrid;
        private Border _headerBorder;
        private Border _sidebarBorder;
        private ContentControl _contentArea;
        private Grid _notificationOverlay;
        private Border _notificationDrawerBorder;
        private TranslateTransform _drawerTranslate;

        // --- Header controls ---
        private TextBlock _lblHeaderTitle;
        private TextBlock _lblHeaderUser;
        private Button _btnBackToPortal;
        private Button _btnNotifications;
        private Border _badgeBorder;
        private TextBlock _lblNotificationsBadge;

        // --- Sidebar controls ---
        private TextBlock _lblNavTitle;
        private TextBlock _lblNavSubtitle;
        private Button _btnNavDashboard;
        private Button _btnNavTickets;
        private Button _btnNavIncomingTickets;
        private Button _btnNavDisplay;
        private Button _btnNavEmail;
        private Button _btnNavReports;
        private Button _btnNavDiagnostics;
        private Button _btnNavProfiles;
        private Button _btnNavLogout;

        // --- Workspaces (lazy-initialized) ---
        private WpfCallMonitoringWorkspace _dashboardWorkspace;
        private WpfTicketManagementWorkspace _ticketWorkspace;
        private WpfIncomingTicketsWorkspace _incomingTicketsWorkspace;
        private WpfDisplayModeWorkspace _displayWorkspace;
        private WpfEmailNotificationWorkspace _emailWorkspace;
        private WpfCallMonitoringReportsWorkspace _reportsWorkspace;
        private WpfCallMonitoringDiagnosticsWorkspace _diagnosticsWorkspace;
        private WpfCallMonitoringProfilesWorkspace _profilesWorkspace;

        // --- Services ---
        private readonly ICallMonitoringRepository _callRepo = new CallMonitoringRepository();
        private readonly CallEmailNotificationService _callEmail;

        // --- State ---
        private bool _callSchemaMissingShown;
        private System.Threading.Timer _reminderTimer;
        private int _reminderRunGate;
        private int _notificationsAttentionCount;
        private bool _initialDashboardLoadQueued;
        private WpfNotificationDrawer _wpfDrawer;

        // Colors
        private static readonly Color HeaderColor = Color.FromRgb(0, 150, 136);
        private static readonly Color SidebarColor = Color.FromRgb(41, 58, 74);
        private static readonly Color SidebarHeaderColor = Color.FromRgb(34, 49, 63);
        private static readonly Color NavTextColor = Color.FromRgb(189, 195, 199);
        private static readonly Color NavActiveColor = Color.FromRgb(52, 73, 94);
        private static readonly Color NavHoverColor = Color.FromRgb(52, 73, 94);
        private static readonly Color ContentBg = Color.FromRgb(240, 242, 245);
        private static readonly Color AccentBlue = Color.FromRgb(52, 152, 219);

        public WpfCallMonitoringShell()
        {
            _callEmail = new CallEmailNotificationService(_callRepo);

            Title = "IT Call Monitoring - Dashboard";
            WindowState = WindowState.Maximized;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            MinWidth = 1024;
            MinHeight = 768;
            Background = new SolidColorBrush(ContentBg);
            FontFamily = new FontFamily("Segoe UI");

            // Root grid: 2 rows (header, body), 2 cols (sidebar, content)
            _rootGrid = new Grid();
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            BuildHeader();
            BuildSidebar();
            BuildContentArea();
            BuildNotificationOverlay();

            Content = _rootGrid;

            // Events
            Loaded += WpfCallMonitoringShell_Loaded;
            KeyDown += WpfCallMonitoringShell_KeyDown;
            Closed += (_, __) => StopReminderTimer();
            Closed += (_, __) => ItcmPresenceReporter.Stop();
        }

        // ====================================================================
        // BUILD UI
        // ====================================================================

        private void BuildHeader()
        {
            _headerBorder = new Border
            {
                Background = new SolidColorBrush(HeaderColor),
                Padding = new Thickness(16, 10, 16, 10)
            };
            Grid.SetRow(_headerBorder, 0);
            Grid.SetColumnSpan(_headerBorder, 2);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left
            var spLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var imgLogo = TryLoadLogo();
            if (imgLogo != null)
                spLeft.Children.Add(imgLogo);

            var spTitles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _lblHeaderTitle = new TextBlock
            {
                Text = "IT CALL MONITORING",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            _lblHeaderUser = new TextBlock
            {
                Text = "Not signed in",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 240, 240)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            spTitles.Children.Add(_lblHeaderTitle);
            spTitles.Children.Add(_lblHeaderUser);
            spLeft.Children.Add(spTitles);

            Grid.SetColumn(spLeft, 0);
            grid.Children.Add(spLeft);

            // Dummy database indicator — only shown when connected to YIMS_PROD
            if (IsDummyDatabase())
            {
                var dummyBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(60, 255, 193, 7)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(0, 0, 16, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                dummyBadge.Child = new TextBlock
                {
                    Text = "DUMMY DATABASE",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7))
                };
                Grid.SetColumn(dummyBadge, 1);
                grid.Children.Add(dummyBadge);
            }
            // Right
            var spRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Notifications button with badge
            var btnNotifGrid = new Grid { Width = 46, Height = 32, Margin = new Thickness(0, 0, 12, 0) };
            _btnNotifications = new Button
            {
                Content = "\ud83d\udd14",
                Width = 40,
                Height = 32,
                Background = Brushes.White,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                BorderThickness = new Thickness(0),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            _btnNotifications.Click += async (_, __) => await ToggleNotificationsAsync();

            _badgeBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 2, 6, 2),
                MinWidth = 16,
                MinHeight = 16,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, -2, -2, 0)
            };
            _lblNotificationsBadge = new TextBlock
            {
                Text = "0",
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _badgeBorder.Child = _lblNotificationsBadge;

            btnNotifGrid.Children.Add(_btnNotifications);
            btnNotifGrid.Children.Add(_badgeBorder);
            spRight.Children.Add(btnNotifGrid);

            _btnBackToPortal = new Button
            {
                Content = "Back to Portal",
                Width = 120,
                Height = 32,
                Background = new SolidColorBrush(AccentBlue),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };
            _btnBackToPortal.Click += (_, __) => Close();
            spRight.Children.Add(_btnBackToPortal);

            Grid.SetColumn(spRight, 2);
            grid.Children.Add(spRight);

            _headerBorder.Child = grid;
            _rootGrid.Children.Add(_headerBorder);
        }

        // Matches the portal home indicator: YIMS_PROD is the dummy database.
        private static bool IsDummyDatabase()
        {
            try
            {
                var cs = DatabaseConfig.ConnectionString;
                if (string.IsNullOrWhiteSpace(cs)) return false;

                var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(cs);
                return string.Equals(builder.InitialCatalog, "YIMS_PROD", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
        private void BuildSidebar()
        {
            _sidebarBorder = new Border
            {
                Background = new SolidColorBrush(SidebarColor),
                Padding = new Thickness(0)
            };
            Grid.SetRow(_sidebarBorder, 1);
            Grid.SetColumn(_sidebarBorder, 0);

            var sidebarGrid = new Grid();
            sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Sidebar header
            var headerPanel = new Border
            {
                Background = new SolidColorBrush(SidebarHeaderColor),
                Height = 80,
                Padding = new Thickness(20, 15, 0, 0)
            };
            var headerStack = new StackPanel();
            _lblNavTitle = new TextBlock
            {
                Text = "CALL MONITORING",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            _lblNavSubtitle = new TextBlock
            {
                Text = "IT SUPPORT SYSTEM",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166))
            };
            headerStack.Children.Add(_lblNavTitle);
            headerStack.Children.Add(_lblNavSubtitle);
            headerPanel.Child = headerStack;
            Grid.SetRow(headerPanel, 0);
            sidebarGrid.Children.Add(headerPanel);

            // Top nav buttons
            var topNavStack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            _btnNavDashboard = CreateNavButton("Dashboard", "\ud83d\udcca");
            _btnNavDashboard.Click += (_, __) => { SetActiveNav(_btnNavDashboard); OpenDashboard(); };

            _btnNavTickets = CreateNavButton("Ticket List", "\ud83c\udfab");
            _btnNavTickets.Click += (_, __) => { SetActiveNav(_btnNavTickets); OpenTicketWorkspace(); };

            _btnNavIncomingTickets = CreateNavButton("Incoming Tickets", "\ud83d\udce5");
            _btnNavIncomingTickets.Click += (_, __) => { SetActiveNav(_btnNavIncomingTickets); OpenIncomingTickets(); };

            _btnNavReports = CreateNavButton("Reports", "\ud83d\udcca");
            _btnNavReports.Click += (_, __) => { SetActiveNav(_btnNavReports); OpenReports(); };

            _btnNavEmail = CreateNavButton("Email Notification", "\ud83d\udce9");
            _btnNavEmail.Click += (_, __) => { SetActiveNav(_btnNavEmail); OpenEmailNotifications(); };

            _btnNavProfiles = CreateNavButton("Profiles", "\ud83d\udc64");
            _btnNavProfiles.Click += (_, __) => { SetActiveNav(_btnNavProfiles); OpenProfiles(); };

            _btnNavDisplay = CreateNavButton("Display Mode", "\u25a3");
            _btnNavDisplay.Click += (_, __) => { SetActiveNav(_btnNavDisplay); OpenDisplayMode(); };

            _btnNavDiagnostics = CreateNavButton("Diagnostics", "\ud83e\uddea");
            _btnNavDiagnostics.Click += (_, __) => { SetActiveNav(_btnNavDiagnostics); OpenDiagnostics(); };

            _btnNavLogout = CreateNavButton("Logout", "\ud83d\udeaa");
            _btnNavLogout.Click += (_, __) =>
            {
                if (System.Windows.MessageBox.Show("Are you sure you want to logout?", "Logout",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    Close();
            };

            topNavStack.Children.Add(_btnNavDashboard);
            topNavStack.Children.Add(_btnNavTickets);
            topNavStack.Children.Add(_btnNavIncomingTickets);
            topNavStack.Children.Add(_btnNavReports);
            topNavStack.Children.Add(_btnNavProfiles);
            topNavStack.Children.Add(_btnNavEmail);
            Grid.SetRow(topNavStack, 1);
            sidebarGrid.Children.Add(topNavStack);

            // Bottom nav buttons
            var bottomNavStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            bottomNavStack.Children.Add(_btnNavDisplay);
            bottomNavStack.Children.Add(_btnNavDiagnostics);
            bottomNavStack.Children.Add(_btnNavLogout);
            Grid.SetRow(bottomNavStack, 2);
            sidebarGrid.Children.Add(bottomNavStack);

            _sidebarBorder.Child = sidebarGrid;
            _rootGrid.Children.Add(_sidebarBorder);
        }

        private void BuildContentArea()
        {
            _contentArea = new ContentControl
            {
                Background = new SolidColorBrush(ContentBg)
            };
            Grid.SetRow(_contentArea, 1);
            Grid.SetColumn(_contentArea, 1);
            _rootGrid.Children.Add(_contentArea);
        }

        private void BuildNotificationOverlay()
        {
            _notificationOverlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(100, 15, 23, 42)),
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(_notificationOverlay, 1);
            Grid.SetColumn(_notificationOverlay, 1);
            Panel.SetZIndex(_notificationOverlay, 100);

            _notificationOverlay.MouseDown += (s, e) =>
            {
                if (e.Source == _notificationOverlay)
                    CloseNotificationsAnimated();
            };

            // Slide transform applied to drawer panel
            _drawerTranslate = new TranslateTransform { X = 560 };

            _notificationDrawerBorder = new Border
            {
                Width = 560,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                RenderTransform = _drawerTranslate,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(15, 23, 42),
                    Direction = 180,
                    BlurRadius = 24,
                    ShadowDepth = 0,
                    Opacity = 0.25
                }
            };

            // Outer layout: chrome header + WinForms content
            var drawerLayout = new Grid();
            drawerLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            drawerLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // ── WPF Chrome Header ────────────────────────────────────────────
            var chromeHeader = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(15, 23, 42),
                    Color.FromRgb(30, 64, 120),
                    new Point(0, 0), new Point(1, 0)),
                Padding = new Thickness(20, 14, 16, 14)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Bell icon box
            var bellBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                CornerRadius = new CornerRadius(10),
                Width = 40,
                Height = 40,
                Margin = new Thickness(0, 0, 14, 0)
            };
            bellBox.Child = new TextBlock
            {
                Text = "\ud83d\udd14",
                FontSize = 18,
                FontFamily = new FontFamily("Segoe UI Emoji"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(bellBox, 0);
            headerGrid.Children.Add(bellBox);

            // Title + subtitle stack
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = "Notifications",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "IT Call Monitoring",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(titleStack, 1);
            headerGrid.Children.Add(titleStack);

            // Close button
            var btnClose = new Button
            {
                Content = "\u2715",
                Width = 36,
                Height = 36,
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(0),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            btnClose.MouseEnter += (_, __) => btnClose.Background = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
            btnClose.MouseLeave += (_, __) => btnClose.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            btnClose.Click += (_, __) => CloseNotificationsAnimated();
            Grid.SetColumn(btnClose, 2);
            headerGrid.Children.Add(btnClose);

            chromeHeader.Child = headerGrid;
            Grid.SetRow(chromeHeader, 0);
            drawerLayout.Children.Add(chromeHeader);

            // ── Pure WPF notification drawer ─────────────────────────────────
            _wpfDrawer = CreateWpfDrawer();
            Grid.SetRow(_wpfDrawer, 1);
            drawerLayout.Children.Add(_wpfDrawer);

            _notificationDrawerBorder.Child = drawerLayout;
            _notificationOverlay.Children.Add(_notificationDrawerBorder);
            _rootGrid.Children.Add(_notificationOverlay);
        }

        private WpfNotificationDrawer CreateWpfDrawer()
        {
            _wpfDrawer = new WpfNotificationDrawer();
            _wpfDrawer.Initialize(_callRepo);
            _wpfDrawer.CloseRequested += CloseNotifications;
            _wpfDrawer.AttentionCountChanged += count => SetNotificationsBadge(count);
            _wpfDrawer.OpenTicketAsync = async ticketId =>
            {
                CloseNotifications();
                OpenTicket(ticketId);
                await Task.CompletedTask;
            };
            _wpfDrawer.OpenTicketListAsync = () =>
            {
                CloseNotifications();
                SetActiveNav(_btnNavTickets);
                OpenTicketWorkspace();
                return Task.CompletedTask;
            };
            _wpfDrawer.AfterMutationAsync = async () =>
            {
                await RefreshNotificationsBadgeAsync();
                if (_ticketWorkspace != null)
                    _ = _ticketWorkspace.LoadDataAsync();
                if (_displayWorkspace != null)
                    _ = _displayWorkspace.LoadDataAsync(force: true);
                _dashboardWorkspace?.RefreshData();
            };
            return _wpfDrawer;
        }

        private Button CreateNavButton(string text, string icon)
        {
            var btn = new Button
            {
                Content = $"   {icon}   {text}",
                Height = 50,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(NavTextColor),
                BorderThickness = new Thickness(0),
                FontSize = 13,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(20, 0, 0, 0),
                Cursor = Cursors.Hand
            };

            btn.MouseEnter += (_, __) =>
            {
                if (!IsActiveNavButton(btn))
                    btn.Background = new SolidColorBrush(NavHoverColor);
            };
            btn.MouseLeave += (_, __) =>
            {
                if (!IsActiveNavButton(btn))
                    btn.Background = Brushes.Transparent;
            };

            return btn;
        }

        // ====================================================================
        // NAVIGATION
        // ====================================================================

        private bool IsActiveNavButton(Button btn)
        {
            return btn != null && Equals(btn.Tag, "Active");
        }

        private void SetActiveNav(Button activeBtn)
        {
            var buttons = new[] { _btnNavDashboard, _btnNavTickets, _btnNavIncomingTickets, _btnNavDisplay, _btnNavEmail, _btnNavReports, _btnNavDiagnostics, _btnNavProfiles };
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                btn.Tag = null;
                btn.Foreground = new SolidColorBrush(NavTextColor);
                btn.Background = Brushes.Transparent;
                btn.FontWeight = FontWeights.Normal;
            }

            if (activeBtn != null)
            {
                activeBtn.Tag = "Active";
                activeBtn.Foreground = Brushes.White;
                activeBtn.Background = new SolidColorBrush(NavActiveColor);
                activeBtn.FontWeight = FontWeights.Bold;
            }
        }

        private void SwitchView(UIElement view)
        {
            _contentArea.Content = view;
        }

        // --- ICallMonitoringNavigator ---

        public void OpenDashboard()
        {
            SetActiveNav(_btnNavDashboard);
            if (_dashboardWorkspace == null)
            {
                _dashboardWorkspace = new WpfCallMonitoringWorkspace();
                _dashboardWorkspace.Initialize(_callRepo, this);
            }
            _dashboardWorkspace.RefreshData();
            SwitchView(_dashboardWorkspace);
            _lblNavTitle.Text = "DASHBOARD";
            _lblNavSubtitle.Text = "OVERVIEW";
        }

        public void OpenTicketWorkspace()
        {
            SetActiveNav(_btnNavTickets);
            if (_ticketWorkspace == null)
            {
                _ticketWorkspace = new WpfTicketManagementWorkspace();
                _ticketWorkspace.Initialize(_callRepo);
            }
            SafeFireAndForget(_ticketWorkspace.LoadDataAsync());
            SwitchView(_ticketWorkspace);
            _lblNavTitle.Text = "TICKET MANAGEMENT";
            _lblNavSubtitle.Text = "CREATE & MANAGE";
        }

        public void OpenIncomingTickets()
        {
            SetActiveNav(_btnNavIncomingTickets);
            if (_incomingTicketsWorkspace == null)
            {
                _incomingTicketsWorkspace = new WpfIncomingTicketsWorkspace();
                _incomingTicketsWorkspace.Initialize(_callRepo, this);
            }
            SafeFireAndForget(_incomingTicketsWorkspace.LoadDataAsync());
            SwitchView(_incomingTicketsWorkspace);
            _lblNavTitle.Text = "INCOMING TICKETS";
            _lblNavSubtitle.Text = "PORTAL SUBMISSIONS";
        }

        public void OpenDisplayMode()
        {
            SetActiveNav(_btnNavDisplay);
            if (_displayWorkspace == null)
            {
                _displayWorkspace = new WpfDisplayModeWorkspace();
                _displayWorkspace.Initialize(_callRepo, this);
            }
            SafeFireAndForget(_displayWorkspace.LoadDataAsync(force: true));
            SwitchView(_displayWorkspace);
            _lblNavTitle.Text = "DISPLAY MODE";
            _lblNavSubtitle.Text = "LIVE OPEN TICKETS";
        }

        public void OpenReports()
        {
            SetActiveNav(_btnNavReports);
            if (_reportsWorkspace == null)
            {
                _reportsWorkspace = new WpfCallMonitoringReportsWorkspace();
                _reportsWorkspace.Initialize(_callRepo, this);
            }
            SafeFireAndForget(_reportsWorkspace.LoadDataAsync());
            SwitchView(_reportsWorkspace);
            _lblNavTitle.Text = "REPORTS";
            _lblNavSubtitle.Text = "ANALYTICS & METRICS";
        }

        public void OpenMobileUpdates() => OpenMobileUpdatesDialog();

        public void OpenEmailNotifications()
        {
            SetActiveNav(_btnNavEmail);
            if (_emailWorkspace == null)
            {
                _emailWorkspace = new WpfEmailNotificationWorkspace(_callRepo);
            }
            SwitchView(_emailWorkspace);
            _lblNavTitle.Text = "EMAIL NOTIFICATIONS";
            _lblNavSubtitle.Text = "STATUS & LOGS";
        }

        public void OpenDiagnostics()
        {
            SetActiveNav(_btnNavDiagnostics);
            if (_diagnosticsWorkspace == null)
            {
                _diagnosticsWorkspace = new WpfCallMonitoringDiagnosticsWorkspace();
                _diagnosticsWorkspace.Initialize(_callRepo, this);
            }
            SwitchView(_diagnosticsWorkspace);
            _lblNavTitle.Text = "DIAGNOSTICS";
            _lblNavSubtitle.Text = "SUPPORT & HEALTH";
        }

        public void OpenProfiles()
        {
            SetActiveNav(_btnNavProfiles);
            if (_profilesWorkspace == null)
            {
                _profilesWorkspace = new WpfCallMonitoringProfilesWorkspace();
                _profilesWorkspace.Initialize(_callRepo, itDepartmentName: null, this);
            }
            SwitchView(_profilesWorkspace);
            _lblNavTitle.Text = "EMPLOYEE PROFILES";
            _lblNavSubtitle.Text = "HISTORY & ASSETS";
        }

        public void OpenTicket(int ticketId)
        {
            var dlg = new WpfTicketDetailsDialog(_callRepo, ticketId)
            {
                Owner = this
            };
            dlg.ShowDialog();

            if (dlg.RequestedAction == TicketDetailsDialog.TicketDetailAction.OpenEmailLog)
            {
                OpenEmailDeepLink(WpfEmailNotificationDeepLinkTarget.EmailLog, emailLogSearch: ticketId.ToString());
            }
            else if (dlg.RequestedAction != TicketDetailsDialog.TicketDetailAction.None)
            {
                SetActiveNav(_btnNavTickets);
                OpenTicketWorkspace();
                if (_ticketWorkspace != null)
                    SafeFireAndForget(_ticketWorkspace.SelectTicketAsync(ticketId));
            }

            if (_ticketWorkspace != null)
                _ = _ticketWorkspace.LoadDataAsync();
            if (_displayWorkspace != null)
                _ = _displayWorkspace.LoadDataAsync(force: true);
            _dashboardWorkspace?.RefreshData();
        }

        public void OpenEmailDeepLink(
            WpfEmailNotificationDeepLinkTarget target,
            int? deptId = null,
            int? branchId = null,
            string emailLogSearch = null)
        {
            try
            {
                SetActiveNav(_btnNavEmail);
                OpenEmailNotifications();
                _emailWorkspace?.NavigateTo(target, deptId: deptId, branchId: branchId, emailLogSearch: emailLogSearch);
            }
            catch { }
        }

        public void OpenSmtpTest()
        {
            try
            {
                var dlg = new WpfSmtpTestDialog(_callRepo, null)
                {
                    Owner = this
                };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "SMTP Test", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task NavigateToReportsAsync(DateTime fromLocal, DateTime toLocal, string type)
        {
            SetActiveNav(_btnNavReports);
            OpenReports();
            if (_reportsWorkspace != null)
                await _reportsWorkspace.ApplyFilterAsync(fromLocal, toLocal, type);
        }

        public async Task NavigateToTicketAsync(int ticketId)
        {
            SetActiveNav(_btnNavTickets);
            OpenTicketWorkspace();
            if (_ticketWorkspace != null)
                await _ticketWorkspace.SelectTicketAsync(ticketId);
        }

        // ====================================================================
        // DIALOGS
        // ====================================================================

        private void OpenMobileUpdatesDialog()
        {
            try
            {
                var form = new System.Windows.Forms.Form
                {
                    Text = "Mobile Updates",
                    StartPosition = System.Windows.Forms.FormStartPosition.CenterParent,
                    Size = new System.Drawing.Size(1200, 750),
                    MinimumSize = new System.Drawing.Size(1024, 650)
                };
                var page = new Yakult.Inventory.App.Pages.Update.ViewUpdatesPage
                {
                    Dock = System.Windows.Forms.DockStyle.Fill
                };
                form.Controls.Add(page);
                form.ShowDialog();
                _dashboardWorkspace?.RefreshData();
            }
            catch (Exception ex)
            {
                Logger.LogError("[WpfCallMonitoringShell] Unable to open Mobile Updates.", ex);
                System.Windows.MessageBox.Show($"Unable to open Mobile Updates.\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ====================================================================
        // LIFECYCLE
        // ====================================================================

        private void WpfCallMonitoringShell_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppSession.IsLoggedIn)
            {
                var role = AppSession.IsAdmin ? "Admin" :
                    (AppSession.IsDeveloper ? "Developer" : string.Join(",", AppSession.CurrentUserRoles));
                _lblHeaderUser.Text = $"User: {AppSession.CurrentUserName} ({role})";
            }

            SetActiveNav(_btnNavDashboard);
            OpenDashboard();

            if (!_initialDashboardLoadQueued)
            {
                _initialDashboardLoadQueued = true;
                Dispatcher.BeginInvoke(new Action(async () => await LoadInitialStateAsync()));
            }
        }

        private async Task LoadInitialStateAsync()
        {
            await LoadCallMonitoringDataAsync();
            await RefreshNotificationsBadgeAsync();
            StartReminderTimerIfEligible();
            ItcmPresenceReporter.Start();
        }

        private void WpfCallMonitoringShell_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_notificationOverlay.Visibility == Visibility.Visible)
                {
                    CloseNotificationsAnimated();
                    e.Handled = true;
                }
            }
        }

        // ====================================================================
        // NOTIFICATIONS
        // ====================================================================

        private async Task ToggleNotificationsAsync()
        {
            if (_notificationOverlay.Visibility == Visibility.Visible)
            {
                CloseNotificationsAnimated();
                return;
            }
            await OpenNotificationsAsync();
        }

        private async Task OpenNotificationsAsync()
        {
            _drawerTranslate.X = 560;
            _notificationOverlay.Visibility = Visibility.Visible;

            var slideIn = new DoubleAnimation(560, 0,
                new Duration(TimeSpan.FromMilliseconds(260)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            _drawerTranslate.BeginAnimation(TranslateTransform.XProperty, slideIn);

            try
            {
                await _wpfDrawer.RefreshAsync();
            }
            catch { }
        }

        private void CloseNotifications()
        {
            _notificationOverlay.Visibility = Visibility.Collapsed;
            _drawerTranslate.X = 560;
        }

        private void CloseNotificationsAnimated()
        {
            var slideOut = new DoubleAnimation(0, 560,
                new Duration(TimeSpan.FromMilliseconds(220)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            slideOut.Completed += (_, __) =>
            {
                _notificationOverlay.Visibility = Visibility.Collapsed;
                _drawerTranslate.X = 560;
            };
            _drawerTranslate.BeginAnimation(TranslateTransform.XProperty, slideOut);
        }

        private async Task RefreshNotificationsBadgeAsync()
        {
            if (_callRepo == null) return;
            try
            {
                if (!await _callRepo.CallSchemaExistsAsync())
                {
                    SetNotificationsBadge(0);
                    return;
                }
                const int max = 150;
                var rows = await _callRepo.GetRequiresAttentionAsync(maxRows: max);
                var count = rows?.Count ?? 0;
                SetNotificationsBadge(count >= max ? max : count);
            }
            catch
            {
                SetNotificationsBadge(0);
            }
        }

        private void SetNotificationsBadge(int count)
        {
            _notificationsAttentionCount = Math.Max(0, count);
            if (count <= 0)
            {
                _badgeBorder.Visibility = Visibility.Collapsed;
                return;
            }
            _lblNotificationsBadge.Text = count > 99 ? "99+" : count.ToString();
            _badgeBorder.Visibility = Visibility.Visible;
        }

        // ====================================================================
        // DATA LOADING
        // ====================================================================

        public async Task LoadCallMonitoringDataAsync()
        {
            try
            {
                if (!await _callRepo.CallSchemaExistsAsync())
                {
                    if (!_callSchemaMissingShown)
                    {
                        System.Windows.MessageBox.Show(
                            "Call Monitoring Schema (tables) missing. Please run database migration scripts.",
                            "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        _callSchemaMissingShown = true;
                    }
                    return;
                }
                _dashboardWorkspace?.RefreshData();
            }
            catch (Exception ex)
            {
                Logger.LogError("[WpfCallMonitoringShell] Error loading data.", ex);
                System.Windows.MessageBox.Show($"Error loading data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ====================================================================
        // REMINDER TIMER
        // ====================================================================

        private void StartReminderTimerIfEligible()
        {
            try
            {
                // Yakult.ITCM.Server is the single scheduler owner by default.
                // This in-app timer is a FALLBACK only (ItcmServerOwned=false).
                if (AppConfig.ItcmServerOwned) return;
                if (!AppSession.IsLoggedIn) return;
                if (!AppSession.IsAdmin && !AppSession.IsDeveloper) return;
                if (_reminderTimer != null) return;

                var due = TimeSpan.FromMinutes(2);
                var period = TimeSpan.FromMinutes(15);
                CallEmailNotificationService.SetNextBackgroundRunUtc(DateTime.UtcNow.Add(due));
                _reminderTimer = new System.Threading.Timer(
                    _ =>
                    {
                        if (Interlocked.Exchange(ref _reminderRunGate, 1) == 1) return;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                CallEmailNotificationService.SetNextBackgroundRunUtc(DateTime.UtcNow.Add(period));
                                var userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                                await _callEmail.ProcessRemindersAndAutoEscalationsWithGlobalLockAsync(maxTickets: 20, triggeredByUserId: userId);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError("[WpfCallMonitoringShell] Reminder/escalation background run failed.", ex);
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
                Logger.LogError("[WpfCallMonitoringShell] Failed to start reminder timer.", ex);
                StopReminderTimer();
            }
        }

        private void StopReminderTimer()
        {
            try { _reminderTimer?.Dispose(); } catch { }
            _reminderTimer = null;
        }

        // ====================================================================
        // HELPERS
        // ====================================================================

        private static void SafeFireAndForget(Task task)
        {
            if (task == null) return;
            task.ContinueWith(
                t => Logger.LogError("[WpfCallMonitoringShell] Background task failed.", t.Exception?.GetBaseException()),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        private static System.Windows.Controls.Image TryLoadLogo()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var candidates = new[]
                {
                    System.IO.Path.Combine(baseDir, "Images", "yakult_Name.png"),
                    System.IO.Path.Combine(baseDir, "images", "yakult_Name.png"),
                    System.IO.Path.Combine(baseDir, "yakult_Name.png")
                };
                foreach (var path in candidates)
                {
                    if (!System.IO.File.Exists(path)) continue;
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(path);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    return new System.Windows.Controls.Image
                    {
                        Source            = bmp,
                        Width             = 200,
                        Height            = 40,
                        Stretch           = Stretch.Uniform,
                        Margin            = new Thickness(0, 2, 14, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }
            }
            catch { }
            return null;
        }
    }
}
