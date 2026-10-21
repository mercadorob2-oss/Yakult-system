using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using Newtonsoft.Json;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Forms.Portal;
using Yakult.Inventory.App.Pages.User;
using Yakult.Inventory.App.Security;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Wpf.Portal
{
    /// <summary>
    /// Shared desktop home. Launches only the systems available to the current
    /// account and keeps personal pins and recent launches between sessions.
    /// </summary>
    public sealed class WpfMainDashboardShell : Window
    {
        public PortalSelection SelectedModule { get; private set; } = PortalSelection.None;

        private static readonly Color TextSecondary = Color.FromRgb(103, 112, 129);
        private static readonly Brush Ink = Brush("#202635");
        private static readonly Brush Muted = new SolidColorBrush(TextSecondary);
        private static readonly Brush Accent = Brush("#B92335");
        private static readonly Brush Line = Brush("#E5E6E8");
        private static readonly Brush Tint = Brush("#FCF0F1");
        private static readonly Style ActionStyle = CreateActionStyle();
        private static readonly Style HeaderActionStyle = CreateActionStyle(true);

        private readonly Grid _rootGrid;
        private readonly List<SystemModule> _modules = new List<SystemModule>();
        private WorkspacePreferences _preferences;
        private StackPanel _pinnedPanel;
        private StackPanel _recentPanel;
        private UniformGrid _cardsPanel;
        private TextBox _search;
        private TextBlock _searchHint;
        private TextBlock _resultLabel;
        private TextBlock _emptyResults;
        private TextBlock _supportReference;
        private Grid _workspace;
        private Grid _body;
        private StackPanel _secondary;
        private Border _welcome;
        private StackPanel _keyboardTip;

        private sealed class CardPresentation
        {
            public Button Open;
            public Button Pin;
            public TextBlock Title;
            public TextBlock Description;
            public FrameworkElement Footer;
            public FrameworkElement Icon;
        }

        private sealed class SystemModule
        {
            public string Title;
            public string Description;
            public string Icon;
            public string Shortcut;
            public PortalSelection Selection;
            public PermissionResolver.Portal Permission;
        }

        private sealed class WorkspacePreferences
        {
            public List<string> Pinned { get; set; } = new List<string>();
            public List<string> Recent { get; set; } = new List<string>();
        }

        public WpfMainDashboardShell()
        {
            Title = "Yakult Workspace";
            WindowState = WindowState.Maximized;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            MinWidth = 820;
            MinHeight = 600;
            Background = Brush("#F6F5F2");
            FontFamily = new FontFamily("Segoe UI");
            Foreground = Ink;
            UseLayoutRounding = true;

            RegisterSystems();
            LoadPreferences();
            _rootGrid = new Grid { Background = Background };
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(76) });
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            BuildHeader();
            BuildContent();
            BuildFooter();
            Content = _rootGrid;
            PreviewKeyDown += HandleShortcut;
        }

        private const string EmployeePortalUrl = "http://192.168.100.186:7016";
        private const string CartridgeWebUrl = "http://192.168.100.186:8080";
        private const string TicketAutomationUrl = "http://192.168.100.186:7017";
        private const string MobileApiUrl = "http://192.168.100.186:7014";
        private static System.Windows.Media.MediaPlayer _alertPlayer;

        private void RegisterSystems()
        {
            AddSystem("Inventory System", "Manage assets, stock levels, requests, and inventory movements.", "inventory", "Alt + I", PortalSelection.InventorySystem, PermissionResolver.Portal.InventorySystem);
            AddSystem("IT Call Monitoring", "Track helpdesk tickets, escalations, and issue resolution.", "headset", "Alt + C", PortalSelection.ITCallMonitoring, PermissionResolver.Portal.CallITMonitoring);
            AddSystem("Request/s Portal", "Submit requests and follow their progress and approval status.", "request", "", PortalSelection.RequesterPortal, PermissionResolver.Portal.RequesterPortal);
            AddSystem("Consumable Management", "Manage consumable stock, exchanges, refills, and fulfillment.", "printer", "Alt + M", PortalSelection.CartridgeManagement, PermissionResolver.Portal.CartridgeManagement);
            AddSystem("Borrow Items", "Record borrowed equipment and keep track of returns.", "borrow", "Alt + B", PortalSelection.BorrowItems, PermissionResolver.Portal.BorrowItems);
            AddSystem("Reports", "View reports, inventory analytics, and invoice summaries.", "report", "Alt + R", PortalSelection.Reports, PermissionResolver.Portal.Reports);
            AddSystem("Repair Technician Portal", "Track equipment repairs, service records, and supporting evidence.", "repair", "", PortalSelection.RepairTechnicianPortal, PermissionResolver.Portal.RepairTechnicianPortal);
            // Match the caller's Admin Portal guard as well as the portal permission.
            if (AppSession.IsDeveloper || AppSession.IsSuperAdmin)
                AddSystem("Admin Portal", "Manage accounts, permissions, and system configuration.", "settings", "", PortalSelection.AdminPortal, PermissionResolver.Portal.AdminPortal);
        }

        private void AddSystem(string title, string description, string icon, string shortcut, PortalSelection selection, PermissionResolver.Portal permission)
        {
            if (!PermissionResolver.HasPortalAccess(AppSession.CurrentUserRoles, permission)) return;
            _modules.Add(new SystemModule { Title = title, Description = description, Icon = icon, Shortcut = shortcut, Selection = selection, Permission = permission });
        }

        private void BuildHeader()
        {
            var header = new Border { Background = Brush("#182235"), BorderBrush = Brush("#29364D"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(24, 0, 24, 0) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var logo = TryLoadLogo();
            var logoBadge = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center };
            if (logo != null)
                logoBadge.Child = new Image { Source = logo, Width = 98, Height = 28, Stretch = Stretch.Uniform };
            else
                logoBadge.Child = Label("Yakult", 25, Accent, FontWeights.Bold);
            brand.Children.Add(logoBadge);
            brand.Children.Add(new Border { Width = 1, Height = 28, Background = Brush("#354158"), Margin = new Thickness(0, 0, 18, 0) });
            var titles = new StackPanel();
            titles.Children.Add(Label("WORKSPACE", 12, Brush("#F8FAFC"), FontWeights.SemiBold));
            titles.Children.Add(Label("Internal systems", 11, Brush("#B8C3D4")));
            brand.Children.Add(titles);
            grid.Children.Add(brand);

            if (IsDummyDatabase())
            {
                var badge = new Border { Background = Brush("#FFF5D6"), CornerRadius = new CornerRadius(5), Padding = new Thickness(8, 5, 8, 5), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = Label("DUMMY DB", 10, Brush("#805F08"), FontWeights.SemiBold) };
                Grid.SetColumn(badge, 1);
                grid.Children.Add(badge);
            }

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Button recent = null;
            recent = ActionButton("Recent", null, () => ShowSystemMenu(recent, _preferences.Recent));
            actions.Children.Add(recent);
            actions.Children.Add(ActionButton("Help", "help", ShowHelpDialog));
            var askAi = ActionButton("Ask AI", "chat", ToggleYakumi);
            askAi.ToolTip = "Ask the office AI assistant";
            actions.Children.Add(askAi);
            actions.Children.Add(ActionButton("My account", "person", OpenAccountDialog));
            var logout = ActionButton("Log out", "logout", () => SelectAndClose(PortalSelection.Logout));
            actions.Children.Add(logout);
            foreach (Button action in actions.Children)
            {
                action.Style = HeaderActionStyle;
                action.Foreground = Brush("#F8FAFC");
            }
            logout.Foreground = Brush("#FF9AA8");
            Grid.SetColumn(actions, 2);
            grid.Children.Add(actions);
            header.Child = grid;
            _rootGrid.Children.Add(header);
        }

        private void BuildContent()
        {
            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(184) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(layout, 1);
            _rootGrid.Children.Add(layout);
            BuildSidebar(layout);

            _workspace = new Grid { Margin = new Thickness(28, 20, 28, 20), MaxWidth = 1500 };
            _workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(_workspace, 1);
            layout.Children.Add(_workspace);

            var intro = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            intro.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            intro.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var greeting = new StackPanel();
            greeting.Children.Add(Label("YOUR DAILY WORKSPACE", 10, Accent, FontWeights.SemiBold, new Thickness(0, 0, 0, 8)));
            var hour = DateTime.Now.Hour;
            string salutation = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
            var name = AppSession.CurrentUserName;
            var greetingTitle = Label(string.IsNullOrWhiteSpace(name) ? salutation + "." : salutation + ", " + name + ".", 25, Ink, FontWeights.SemiBold);
            greetingTitle.TextWrapping = TextWrapping.NoWrap;
            greetingTitle.TextTrimming = TextTrimming.CharacterEllipsis;
            greetingTitle.ToolTip = greetingTitle.Text;
            greeting.Children.Add(greetingTitle);
            greeting.Children.Add(Label("Your systems and shortcuts, together in one place.", 12, Muted, FontWeights.Normal, new Thickness(0, 5, 12, 0)));
            intro.Children.Add(greeting);
            var date = Label(DateTime.Today.ToString("dddd\ndd MMMM yyyy"), 12, Muted);
            date.TextAlignment = TextAlignment.Right;
            date.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetColumn(date, 1);
            intro.Children.Add(date);
            _workspace.Children.Add(intro);

            _welcome = new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 20), Visibility = Visibility.Collapsed };
            var welcomeGrid = new Grid();
            welcomeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            welcomeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var message = new StackPanel { Margin = new Thickness(0, 0, 20, 0) };
            message.Children.Add(Label("Make yourself at home", 17, Ink, FontWeights.SemiBold));
            message.Children.Add(Label("Pin the systems you use most. Find everything else in your system library below.", 12, Muted, FontWeights.Normal, new Thickness(0, 6, 0, 0)));
            welcomeGrid.Children.Add(message);
            var library = ActionButton("Explore systems", "arrow", () => { _search.Focus(); _search.BringIntoView(); });
            library.Background = Tint;
            library.Foreground = Accent;
            library.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(library, 1);
            welcomeGrid.Children.Add(library);
            _welcome.Child = welcomeGrid;
            Grid.SetRow(_welcome, 1);
            _workspace.Children.Add(_welcome);

            _body = new Grid();
            _body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(272) });
            Grid.SetRow(_body, 2);
            _workspace.Children.Add(_body);
            var libraryGrid = new Grid();
            for (int row = 0; row < 3; row++) libraryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            libraryGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _body.Children.Add(libraryGrid);
            libraryGrid.Children.Add(Label("Your systems", 18, Ink, FontWeights.SemiBold));
            _resultLabel = Label("", 11, Muted, FontWeights.Normal, new Thickness(0, 4, 0, 10));
            Grid.SetRow(_resultLabel, 1);
            libraryGrid.Children.Add(_resultLabel);
            var searchPanel = new StackPanel();
            Grid.SetRow(searchPanel, 2);
            libraryGrid.Children.Add(searchPanel);
            BuildSearch(searchPanel);
            _cardsPanel = new UniformGrid { Columns = 2, Margin = new Thickness(-6, 0, -6, 0), VerticalAlignment = VerticalAlignment.Top };
            Grid.SetRow(_cardsPanel, 3);
            libraryGrid.Children.Add(_cardsPanel);
            _emptyResults = Label("", 13, Muted, FontWeights.Normal, new Thickness(0, 24, 0, 24));
            Grid.SetRow(_emptyResults, 3);
            libraryGrid.Children.Add(_emptyResults);
            libraryGrid.SizeChanged += (_, e) =>
            {
                _cardsPanel.Columns = e.NewSize.Width >= 840 ? 3 : 2;
                FitCardDensity();
            };
            _cardsPanel.SizeChanged += (_, __) => FitCardDensity();
            BuildSecondary();
            _workspace.SizeChanged += (_, e) =>
            {
                _welcome.Visibility = e.NewSize.Height >= 900 ? Visibility.Visible : Visibility.Collapsed;
                greetingTitle.FontSize = e.NewSize.Width < 650 ? 21 : 25;
                UpdateLayoutForWidth(e.NewSize.Width);
            };
            _body.SizeChanged += (_, __) =>
            {
                UpdateLayoutForWidth(_workspace.ActualWidth);
                _keyboardTip.Visibility = _body.ActualHeight >= 600 ? Visibility.Visible : Visibility.Collapsed;
                RenderRecent();
            };
            RenderSystems();
            RenderPins();
            RenderRecent();
        }

        private void BuildSidebar(Grid layout)
        {
            var rail = new Border { Background = Brush("#202B3E"), Padding = new Thickness(12, 24, 12, 18) };
            layout.Children.Add(rail);
            var dock = new DockPanel { LastChildFill = true };
            rail.Child = dock;
            var identity = new StackPanel { Margin = new Thickness(12, 20, 8, 0) };
            identity.Children.Add(Label("SIGNED IN", 10, Brush("#9DAAC0"), FontWeights.SemiBold));
            identity.Children.Add(Label(AppSession.CurrentUserName ?? "User", 12, Brushes.White, FontWeights.SemiBold, new Thickness(0, 7, 0, 0)));
            var context = string.Join(" · ", new[] { AppSession.CurrentCompanyName, AppSession.CurrentDepartmentName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            identity.Children.Add(Label(context, 10, Brush("#BBC5D5"), FontWeights.Normal, new Thickness(0, 5, 0, 0)));
            DockPanel.SetDock(identity, Dock.Bottom);
            dock.Children.Add(identity);
            var menu = new StackPanel();
            var home = ActionButton("Home", "home", () => { _search.Clear(); _search.Focus(); });
            home.Background = Brush("#354158");
            home.Foreground = Brushes.White;
            home.Margin = new Thickness(0, 0, 0, 6);
            AutomationProperties.SetHelpText(home, "Current page: dashboard home");
            menu.Children.Add(home);
            var systems = ActionButton("All systems", "grid", () => { _search.Focus(); _search.BringIntoView(); });
            systems.Foreground = Brush("#DDE3ED");
            menu.Children.Add(systems);
            menu.Children.Add(Label("PINNED SYSTEMS", 10, Brush("#9DAAC0"), FontWeights.SemiBold, new Thickness(12, 32, 0, 12)));
            _pinnedPanel = new StackPanel();
            menu.Children.Add(_pinnedPanel);
            menu.Children.Add(Label("WEB SYSTEMS", 10, Brush("#9DAAC0"), FontWeights.SemiBold, new Thickness(12, 24, 0, 12)));
            menu.Children.Add(CreateWebNavButton("Employee Portal", "person", EmployeePortalUrl));
            menu.Children.Add(CreateWebNavButton("Request Cartridge Web", "request", CartridgeWebUrl));
            // Admin tools section is rendered only for developer / super-admin / admin
            // accounts (same gate as OpenAdminTool). Non-admins never see the entries.
            if (CanAccessAdminTools())
            {
                menu.Children.Add(Label("ADMIN TOOLS", 10, Brush("#9DAAC0"), FontWeights.SemiBold, new Thickness(12, 24, 0, 12)));
                menu.Children.Add(CreateAdminNavButton("Ticket Automation Handler", "headset", TicketAutomationUrl));
                menu.Children.Add(CreateAdminNavButton("Mobile API", "grid", MobileApiUrl));
            }
            var menuScroll = new ScrollViewer { Content = menu, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            dock.Children.Add(menuScroll);
        }

        private Button CreateWebNavButton(string title, string icon, string url)
        {
            var button = ActionButton(title, icon, () => OpenExternalLink(url, title));
            button.Foreground = Brush("#DDE3ED");
            button.FontSize = 11;
            button.Padding = new Thickness(10, 7, 10, 7);
            button.Margin = new Thickness(0, 0, 0, 4);
            button.ToolTip = title + "\n" + url;
            AutomationProperties.SetName(button, "Open " + title);
            return button;
        }

        private Button CreateAdminNavButton(string title, string icon, string url)
        {
            var button = ActionButton(title, icon, () => OpenAdminTool(url, title));
            button.Foreground = Brush("#DDE3ED");
            button.FontSize = 11;
            button.Padding = new Thickness(10, 7, 10, 7);
            button.Margin = new Thickness(0, 0, 0, 4);
            button.ToolTip = title + "\n" + url;
            AutomationProperties.SetName(button, "Open " + title);
            return button;
        }

        private static bool CanAccessAdminTools() => AppSession.IsDeveloper || AppSession.IsSuperAdmin || AppSession.IsAdmin;

        private void OpenAdminTool(string url, string title)
        {
            if (!CanAccessAdminTools())
            {
                ShowUnauthorizedAccessDialog(title);
                return;
            }
            OpenExternalLink(url, title);
        }

        private void ShowUnauthorizedAccessDialog(string toolName)
        {
            string originIp = GetLocalIpAddress();
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string user = AppSession.CurrentUserName ?? "Unknown";
            Logger.LogError("[Workspace] Unauthorized access attempt by '" + user + "' for '" + toolName + "' from " + originIp + ".");
            PlayUnauthorizedAlertSound();

            var overlay = new Grid { Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)) };
            Grid.SetRowSpan(overlay, 3);
            KeyboardNavigation.SetTabNavigation(overlay, KeyboardNavigationMode.Cycle);
            var stories = new List<Storyboard>();

            var dialog = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(31, 43, 69)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(229, 57, 53)),
                BorderThickness = new Thickness(2),
                CornerRadius    = new CornerRadius(14),
                Width           = 400,
                Padding         = new Thickness(0, 10, 0, 20),
                Effect          = new DropShadowEffect { BlurRadius = 28, ShadowDepth = 0, Opacity = 0.55, Color = Color.FromRgb(229, 57, 53) },
                RenderTransform = new TranslateTransform()
            };

            Action closeDialog = null;
            closeDialog = () =>
            {
                try { foreach (var sb in stories) sb.Stop(); } catch { }
                if (_rootGrid.Children.Contains(overlay)) _rootGrid.Children.Remove(overlay);
            };

            var stack = new StackPanel();

            // Top bar: siren lights left and right, close X at the far right.
            var topBar = new Grid { Margin = new Thickness(20, 0, 14, 0) };
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var leftLight = new System.Windows.Shapes.Ellipse { Width = 44, Height = 10, Fill = new SolidColorBrush(Color.FromRgb(229, 57, 53)), Margin = new Thickness(0, 0, 0, 0) };
            var rightLight = new System.Windows.Shapes.Ellipse { Width = 44, Height = 10, Fill = new SolidColorBrush(Color.FromRgb(229, 57, 53)) };
            var closeX = new Button { Style = ActionStyle, Foreground = Brush("#8A94A8"), Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(10, 0, 0, 0), Cursor = Cursors.Hand, ToolTip = "Close" };
            closeX.Content = new TextBlock { Text = "✕", FontSize = 14, Foreground = Brush("#8A94A8"), HorizontalAlignment = HorizontalAlignment.Center };
            AutomationProperties.SetName(closeX, "Close alert");
            closeX.Click += (_, __) => closeDialog();
            topBar.Children.Add(leftLight);
            Grid.SetColumn(rightLight, 2);
            topBar.Children.Add(rightLight);
            Grid.SetColumn(closeX, 3);
            topBar.Children.Add(closeX);
            stack.Children.Add(topBar);

            // STOP banner with pulsing red border.
            var bannerBorderBrush = new SolidColorBrush(Color.FromRgb(229, 57, 53));
            var banner = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(20, 29, 51)),
                BorderBrush     = bannerBorderBrush,
                BorderThickness = new Thickness(2),
                CornerRadius    = new CornerRadius(10),
                Padding         = new Thickness(12, 8, 12, 8),
                Margin          = new Thickness(24, 8, 24, 0)
            };
            var stopText = new TextBlock { Text = "STOP", FontSize = 56, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(229, 57, 53)), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center };
            stopText.Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 0, Opacity = 0.8, Color = Color.FromRgb(229, 57, 53) };
            banner.Child = stopText;
            stack.Children.Add(banner);

            var heading = new TextBlock { Text = "SYSTEM ALERT: Unauthorized Access Attempt Detected.", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(24, 12, 24, 0) };
            stack.Children.Add(heading);

            var details = new StackPanel { Margin = new Thickness(24, 12, 24, 0) };
            details.Children.Add(MakeAlertRow("Tool:", toolName));
            details.Children.Add(MakeAlertRow("Origin IP:", originIp));
            details.Children.Add(MakeAlertRow("Timestamp:", timestamp));
            stack.Children.Add(details);

            var terminate = new Button { Style = ActionStyle, Background = Brush("#C62828"), BorderBrush = Brush("#C62828"), BorderThickness = new Thickness(1), Foreground = Brushes.White, Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(24, 16, 24, 0), HorizontalContentAlignment = HorizontalAlignment.Center, Cursor = Cursors.Hand };
            terminate.Content = new TextBlock { Text = "TERMINATE SESSION", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center };
            AutomationProperties.SetName(terminate, "Terminate session");
            terminate.Click += (_, __) => closeDialog();
            stack.Children.Add(terminate);

            var review = new Button { Style = ActionStyle, Background = Brushes.Transparent, BorderBrush = Brush("#5A6B8C"), BorderThickness = new Thickness(1), Foreground = Brush("#DDE3ED"), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(24, 8, 24, 0), HorizontalContentAlignment = HorizontalAlignment.Center, Cursor = Cursors.Hand };
            review.Content = new TextBlock { Text = "REVIEW LOGS", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brush("#DDE3ED"), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center };
            AutomationProperties.SetName(review, "Review logs");
            review.Click += (_, __) => closeDialog();
            stack.Children.Add(review);

            dialog.Child = stack;
            var centerPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            centerPanel.Children.Add(dialog);
            overlay.Children.Add(centerPanel);
            overlay.KeyDown += (_, e) => { if (e.Key == Key.Escape) closeDialog(); };
            overlay.Focusable = true;
            _rootGrid.Children.Add(overlay);

            // Siren lights flash alternately.
            var sirenLeft = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            var leftFade = new DoubleAnimation(1, 0.15, new Duration(TimeSpan.FromMilliseconds(500))) { AutoReverse = true };
            Storyboard.SetTarget(leftFade, leftLight);
            Storyboard.SetTargetProperty(leftFade, new PropertyPath(UIElement.OpacityProperty));
            sirenLeft.Children.Add(leftFade);
            stories.Add(sirenLeft);
            sirenLeft.Begin();
            var sirenRight = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            var rightFade = new DoubleAnimation(1, 0.15, new Duration(TimeSpan.FromMilliseconds(500))) { AutoReverse = true, BeginTime = TimeSpan.FromMilliseconds(500) };
            Storyboard.SetTarget(rightFade, rightLight);
            Storyboard.SetTargetProperty(rightFade, new PropertyPath(UIElement.OpacityProperty));
            sirenRight.Children.Add(rightFade);
            stories.Add(sirenRight);
            sirenRight.Begin();

            // STOP text blinks.
            var blink = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            var blinkFade = new DoubleAnimation(1, 0.35, new Duration(TimeSpan.FromMilliseconds(550))) { AutoReverse = true };
            Storyboard.SetTarget(blinkFade, stopText);
            Storyboard.SetTargetProperty(blinkFade, new PropertyPath(UIElement.OpacityProperty));
            blink.Children.Add(blinkFade);
            stories.Add(blink);
            blink.Begin();

            // Banner border pulses between bright and dark red.
            var pulse = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            var glow = new ColorAnimation(Color.FromRgb(229, 57, 53), Color.FromRgb(120, 20, 25), new Duration(TimeSpan.FromMilliseconds(700))) { AutoReverse = true };
            Storyboard.SetTarget(glow, bannerBorderBrush);
            Storyboard.SetTargetProperty(glow, new PropertyPath(SolidColorBrush.ColorProperty));
            pulse.Children.Add(glow);
            stories.Add(pulse);
            pulse.Begin();

            // Quick shake on open.
            var shake = new Storyboard();
            var shakeX = new DoubleAnimationUsingKeyFrames();
            shakeX.KeyFrames.Add(new LinearDoubleKeyFrame(-10, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
            shakeX.KeyFrames.Add(new LinearDoubleKeyFrame(9, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
            shakeX.KeyFrames.Add(new LinearDoubleKeyFrame(-6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
            shakeX.KeyFrames.Add(new LinearDoubleKeyFrame(4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
            shakeX.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));
            Storyboard.SetTarget(shakeX, dialog);
            Storyboard.SetTargetProperty(shakeX, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            shake.Children.Add(shakeX);
            shake.Begin();

            terminate.Focus();
        }

        private static StackPanel MakeAlertRow(string label, string value)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            row.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Brush("#8A94A8"), Width = 90 });
            row.Children.Add(new TextBlock { Text = value, FontSize = 12, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Width = 230 });
            return row;
        }

        private static string GetLocalIpAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip))
                        return ip.ToString();
            }
            catch { }
            return "Unknown";
        }

        private static void PlayUnauthorizedAlertSound()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string soundPath = null;
                foreach (var candidate in new[]
                {
                    Path.Combine(baseDir, "Images", "Danger Alarm Sound Effect.mp3"),
                    Path.Combine(baseDir, "images", "Danger Alarm Sound Effect.mp3"),
                    Path.Combine(baseDir, "Danger Alarm Sound Effect.mp3")
                })
                {
                    if (File.Exists(candidate)) { soundPath = candidate; break; }
                }
                if (soundPath != null)
                {
                    try
                    {
                        if (_alertPlayer != null)
                        {
                            try { _alertPlayer.Stop(); _alertPlayer.Close(); } catch { }
                            _alertPlayer = null;
                        }
                        var player = new System.Windows.Media.MediaPlayer { Volume = 1.0 };
                        _alertPlayer = player;
                        player.MediaEnded += (_, __) =>
                        {
                            try { player.Close(); } catch { }
                            if (ReferenceEquals(_alertPlayer, player)) _alertPlayer = null;
                        };
                        player.MediaFailed += (_, __) => PlayFallbackAlertBeeps();
                        player.Open(new Uri(soundPath));
                        player.Play();
                        return;
                    }
                    catch { }
                }
            }
            catch { }
            PlayFallbackAlertBeeps();
        }

        private static void PlayFallbackAlertBeeps()
        {
            try { System.Media.SystemSounds.Hand.Play(); } catch { }
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < 3; i++)
                    {
                        System.Console.Beep(988, 160);
                        System.Console.Beep(659, 160);
                    }
                }
                catch { }
            });
        }

        private static void OpenExternalLink(string url, string title)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.LogError("[Workspace] Could not open web system '" + title + "'.", ex);
                MessageBox.Show("Could not open " + title + " (" + url + ").\n" + ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildSearch(StackPanel parent)
        {
            var border = new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(12, 2, 4, 2), Margin = new Thickness(0, 0, 0, 12) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(MakeIcon("search", Muted, 17));
            var input = new Grid();
            _search = new TextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Ink, FontSize = 13, Padding = new Thickness(2, 8, 2, 8), VerticalContentAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(_search, "Search your systems");
            _searchHint = Label("Search systems…", 13, Muted);
            _searchHint.Margin = new Thickness(3, 0, 0, 0);
            _searchHint.VerticalAlignment = VerticalAlignment.Center;
            _searchHint.IsHitTestVisible = false;
            input.Children.Add(_search);
            input.Children.Add(_searchHint);
            Grid.SetColumn(input, 1);
            grid.Children.Add(input);
            var clear = ActionButton("Clear", null, () => { _search.Clear(); _search.Focus(); });
            clear.ToolTip = "Clear search";
            clear.Visibility = Visibility.Collapsed;
            Grid.SetColumn(clear, 2);
            grid.Children.Add(clear);
            _search.TextChanged += (_, __) =>
            {
                _searchHint.Visibility = string.IsNullOrEmpty(_search.Text) ? Visibility.Visible : Visibility.Collapsed;
                clear.Visibility = string.IsNullOrEmpty(_search.Text) ? Visibility.Collapsed : Visibility.Visible;
                RenderSystems();
            };
            border.Child = grid;
            parent.Children.Add(border);
        }

        private void BuildSecondary()
        {
            _secondary = new StackPanel { Margin = new Thickness(28, 0, 0, 0) };
            Grid.SetColumn(_secondary, 1);
            _body.Children.Add(_secondary);
            _secondary.Children.Add(Label("Recently opened", 16, Ink, FontWeights.SemiBold));
            _secondary.Children.Add(Label("Pick up where you left off.", 12, Muted, FontWeights.Normal, new Thickness(0, 5, 0, 14)));
            _recentPanel = new StackPanel();
            _secondary.Children.Add(_recentPanel);

            var help = new StackPanel();
            help.Children.Add(MakeIcon("headset", Accent, 23));
            help.Children.Add(Label("Need a hand?", 16, Ink, FontWeights.SemiBold, new Thickness(0, 12, 0, 6)));
            help.Children.Add(Label("Your IT team is here to help with system access and technical concerns.", 12, Muted));
            var email = Label(ContactEmail(), 12, Accent, FontWeights.SemiBold);
            var emailButton = new Button { Style = ActionStyle, Content = email, Padding = new Thickness(0, 10, 0, 10), HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) };
            AutomationProperties.SetName(emailButton, "Email IT support");
            emailButton.Click += (_, __) => ContactSupport();
            help.Children.Add(emailButton);
            _supportReference = Label(Properties.Settings.Default.ITContactReference ?? "IT Helpdesk · Mon–Fri, 8AM–5PM", 11, Muted);
            help.Children.Add(_supportReference);
            if (AppSession.IsAdmin)
                help.Children.Add(ActionButton("Edit contact", null, () => ShowEditITContactDialog(email, _supportReference.Text)));
            _secondary.Children.Add(new Border { Child = help, Background = Brushes.White, CornerRadius = new CornerRadius(9), BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(20), Margin = new Thickness(0, 24, 0, 0) });
            var tip = new StackPanel { Margin = new Thickness(2, 20, 2, 0) };
            _keyboardTip = tip;
            tip.Children.Add(Label("A LITTLE SHORTCUT", 10, Muted, FontWeights.SemiBold));
            tip.Children.Add(Label("Press Ctrl + K to find a system, or use Tab and Enter to navigate.", 12, Muted, FontWeights.Normal, new Thickness(0, 7, 0, 0)));
            _secondary.Children.Add(tip);
        }

        private void RenderSystems()
        {
            if (_cardsPanel == null) return;
            _cardsPanel.Children.Clear();
            var query = (_search.Text ?? "").Trim();
            var matches = _modules.Where(m => (m.Title + " " + m.Description).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            _resultLabel.Text = query.Length == 0
                ? _modules.Count + (_modules.Count == 1 ? " system available to your account" : " systems available to your account")
                : matches.Count + " of " + _modules.Count + " systems match your search";
            foreach (var module in matches)
            {
                var card = new Grid { Margin = new Thickness(6, 0, 6, 10) };
                var content = new Grid();
                content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var top = new Grid { Margin = new Thickness(0, 0, 30, 0) };
                top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var icon = MakeIcon(module.Icon, Accent, 23);
                icon.HorizontalAlignment = HorizontalAlignment.Left;
                icon.Margin = new Thickness(0, 0, 12, 0);
                top.Children.Add(icon);
                var title = Label(module.Title, 15, Ink, FontWeights.SemiBold);
                title.VerticalAlignment = VerticalAlignment.Center;
                title.TextTrimming = TextTrimming.CharacterEllipsis;
                Grid.SetColumn(title, 1);
                top.Children.Add(title);
                content.Children.Add(top);
                var description = Label(module.Description, 12, Muted, FontWeights.Normal, new Thickness(0, 7, 0, 12));
                Grid.SetRow(description, 1);
                content.Children.Add(description);
                var bottom = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
                var shortcut = Label(module.Shortcut, 10, Muted);
                shortcut.HorizontalAlignment = HorizontalAlignment.Right;
                DockPanel.SetDock(shortcut, Dock.Right);
                bottom.Children.Add(shortcut);
                bottom.Children.Add(Label("Open system  →", 11, Accent, FontWeights.SemiBold));
                Grid.SetRow(bottom, 2);
                content.Children.Add(bottom);
                var open = new Button { Style = ActionStyle, Content = content, Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(16), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, ToolTip = module.Title + "\n" + module.Description };
                AutomationProperties.SetName(open, "Open " + module.Title);
                open.Click += (_, __) => SelectAndClose(module.Selection);
                card.Children.Add(open);
                var pinned = IsPinned(module);
                var pin = new Button { Style = ActionStyle, Content = MakeIcon("pin", pinned ? Accent : Muted, 17), Background = pinned ? Tint : Brushes.Transparent, Padding = new Thickness(8), Margin = new Thickness(0, 12, 12, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
                var pinText = (pinned ? "Unpin " : "Pin ") + module.Title;
                pin.ToolTip = pinText;
                AutomationProperties.SetName(pin, pinText);
                AutomationProperties.SetHelpText(pin, pinned ? "Pinned to the home sidebar" : "Add to the home sidebar");
                pin.Click += (_, __) => TogglePin(module);
                card.Children.Add(pin);
                card.Tag = new CardPresentation { Open = open, Pin = pin, Title = title, Description = description, Footer = bottom, Icon = icon };
                _cardsPanel.Children.Add(card);
            }
            _emptyResults.Text = _modules.Count == 0 ? "No systems are assigned to your account. Contact IT support for access." : "No systems found. Try a different search or clear the search field.";
            _emptyResults.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            FitCardDensity();
        }

        private void FitCardDensity()
        {
            if (_cardsPanel == null || _cardsPanel.Children.Count == 0) return;
            int rows = (int)Math.Ceiling(_cardsPanel.Children.Count / (double)_cardsPanel.Columns);
            var library = (Grid)_cardsPanel.Parent;
            double availableHeight = library.RowDefinitions[3].ActualHeight;
            if (availableHeight <= 0) return;
            double panelHeight = Math.Min(availableHeight, rows * 210);
            _cardsPanel.Height = panelHeight;
            double height = panelHeight / rows - 10;
            // Keep every launch and pin action visible. Descriptions remain in tooltips
            // when smaller windows need compact cards.
            foreach (Grid card in _cardsPanel.Children)
            {
                var view = (CardPresentation)card.Tag;
                bool tight = height < 115;
                view.Open.Padding = new Thickness(height < 80 ? 8 : tight ? 12 : 16);
                view.Title.FontSize = tight ? 12 : 14;
                view.Description.Visibility = height >= 155 ? Visibility.Visible : Visibility.Collapsed;
                view.Footer.Visibility = height >= 95 ? Visibility.Visible : Visibility.Collapsed;
                view.Icon.Width = view.Icon.Height = tight ? 18 : 23;
                view.Pin.Padding = new Thickness(tight ? 5 : 7);
                view.Pin.Margin = new Thickness(0, tight ? 5 : 10, tight ? 5 : 10, 0);
            }
        }

        private bool IsPinned(SystemModule module) => _preferences.Pinned.Contains(module.Selection.ToString());

        private void TogglePin(SystemModule module)
        {
            var key = module.Selection.ToString();
            if (!_preferences.Pinned.Remove(key)) _preferences.Pinned.Add(key);
            SavePreferences();
            RenderPins();
            RenderSystems();
            // Restore focus to the replacement pin button after rebuilding the cards.
            foreach (Grid card in _cardsPanel.Children)
            {
                var pin = (Button)card.Children[1];
                if (AutomationProperties.GetName(pin) == (IsPinned(module) ? "Unpin " : "Pin ") + module.Title)
                { pin.Focus(); break; }
            }
        }

        private void RenderPins()
        {
            _pinnedPanel.Children.Clear();
            var pinned = _preferences.Pinned.Select(key => _modules.FirstOrDefault(m => m.Selection.ToString() == key)).Where(m => m != null).ToList();
            foreach (var module in pinned.Take(3))
            {
                var button = ActionButton(module.Title, module.Icon, () => SelectAndClose(module.Selection));
                button.Foreground = Brush("#DDE3ED");
                button.FontSize = 11;
                button.Padding = new Thickness(10, 7, 10, 7);
                button.Margin = new Thickness(0, 0, 0, 4);
                _pinnedPanel.Children.Add(button);
            }
            if (pinned.Count > 3)
            {
                Button more = null;
                more = ActionButton("More pinned…", null, () => ShowSystemMenu(more, pinned.Skip(3).Select(m => m.Selection.ToString())));
                more.Foreground = Brush("#DDE3ED");
                _pinnedPanel.Children.Add(more);
            }
            if (_pinnedPanel.Children.Count == 0)
                _pinnedPanel.Children.Add(Label("Use the pin on any system to add it here.", 11, Brush("#BBC5D5"), FontWeights.Normal, new Thickness(12, 0, 12, 0)));
        }

        private void RenderRecent()
        {
            _recentPanel.Children.Clear();
            foreach (var key in _preferences.Recent.Take(_body.ActualHeight < 600 ? 2 : 4))
            {
                var module = _modules.FirstOrDefault(m => m.Selection.ToString() == key);
                if (module == null) continue;
                var button = ActionButton(module.Title, module.Icon, () => SelectAndClose(module.Selection));
                button.Background = Brushes.White;
                button.BorderBrush = Line;
                button.BorderThickness = new Thickness(1);
                button.Margin = new Thickness(0, 0, 0, 8);
                _recentPanel.Children.Add(button);
            }
            if (_recentPanel.Children.Count == 0)
                _recentPanel.Children.Add(Label("Systems you open will appear here. Choose one from your library to get started.", 12, Muted));
        }

        private void UpdateLayoutForWidth(double width)
        {
            bool compact = width < 900 || _body.ActualHeight < 430;
            _body.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(272);
            _secondary.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ShowSystemMenu(Button anchor, IEnumerable<string> keys)
        {
            var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom };
            foreach (var key in keys)
            {
                var module = _modules.FirstOrDefault(m => m.Selection.ToString() == key);
                if (module == null) continue;
                var item = new MenuItem { Header = module.Title };
                item.Click += (_, __) => SelectAndClose(module.Selection);
                menu.Items.Add(item);
            }
            if (menu.Items.Count == 0) menu.Items.Add(new MenuItem { Header = "No recent systems yet", IsEnabled = false });
            anchor.ContextMenu = menu;
            menu.IsOpen = true;
        }

        private void BuildFooter()
        {
            var footer = new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(24, 10, 24, 10) };
            var dock = new DockPanel();
            var version = Label("v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"), 10, Muted);
            version.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(version, Dock.Right);
            dock.Children.Add(version);
            dock.Children.Add(Label("© " + DateTime.Today.Year + " Yakult · Internal systems", 10, Muted));
            footer.Child = dock;
            Grid.SetRow(footer, 2);
            _rootGrid.Children.Add(footer);
        }

        private void HandleShortcut(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape)
            {
                if (_rootGrid.Children.Count > 3) _rootGrid.Children.RemoveAt(_rootGrid.Children.Count - 1);
                else if (!string.IsNullOrEmpty(_search.Text)) { _search.Clear(); _search.Focus(); }
                else SelectAndClose(PortalSelection.Logout);
                e.Handled = true;
                return;
            }
            // Do not launch another portal while a home dialog is open.
            if (_rootGrid.Children.Count > 3) return;
            var modifiers = e.KeyboardDevice.Modifiers;
            if (modifiers == ModifierKeys.Control && key == Key.K)
            { _search.Focus(); _search.BringIntoView(); _search.SelectAll(); e.Handled = true; }
            else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.D)
            { if (CanAccessDatabaseSwitcher()) HandleDeveloperDatabaseReset(); e.Handled = true; }
            else if (key == Key.F1)
            { ShowHelpDialog(); e.Handled = true; }
            else if (modifiers == ModifierKeys.Alt)
            {
                PortalSelection selection = PortalSelection.None;
                switch (key)
                {
                    case Key.I: selection = PortalSelection.InventorySystem; break;
                    case Key.C: selection = PortalSelection.ITCallMonitoring; break;
                    case Key.M: selection = PortalSelection.CartridgeManagement; break;
                    case Key.R: selection = PortalSelection.Reports; break;
                    case Key.B: selection = PortalSelection.BorrowItems; break;
                }
                if (selection != PortalSelection.None)
                { SelectAndClose(selection); e.Handled = true; }
            }
        }

        private void SelectAndClose(PortalSelection selection)
        {
            if (selection != PortalSelection.Logout)
            {
                var module = _modules.FirstOrDefault(m => m.Selection == selection);
                if (module == null || !PermissionResolver.HasPortalAccess(AppSession.CurrentUserRoles, module.Permission)) return;
                var key = selection.ToString();
                _preferences.Recent.RemoveAll(s => s == key);
                _preferences.Recent.Insert(0, key);
                _preferences.Recent = _preferences.Recent.Take(4).ToList();
                SavePreferences();
            }
            SelectedModule = selection;
            Close();
        }

        private string PreferencesPath()
        {
            string account = AppSession.IsDepartmentAccountSession
                ? "department_" + AppSession.DepartmentAccountId
                : "user_" + AppSession.CurrentUserId;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Yakult", "Workspace", account + ".json");
        }

        private void LoadPreferences()
        {
            _preferences = new WorkspacePreferences();
            try
            {
                var path = PreferencesPath();
                if (File.Exists(path))
                {
                    _preferences = JsonConvert.DeserializeObject<WorkspacePreferences>(File.ReadAllText(path)) ?? new WorkspacePreferences();
                    _preferences.Pinned = (_preferences.Pinned ?? new List<string>()).Distinct().ToList();
                    _preferences.Recent = (_preferences.Recent ?? new List<string>()).Distinct().Take(4).ToList();
                    return;
                }
            }
            catch (Exception ex) { Logger.LogError("[Workspace] Could not load home preferences.", ex); }
            _preferences.Pinned = _modules.Take(3).Select(m => m.Selection.ToString()).ToList();
        }

        private void SavePreferences()
        {
            // Guest/preview sessions must not create shared user_0 preferences.
            if (!AppSession.IsLoggedIn) return;
            try
            {
                var path = PreferencesPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonConvert.SerializeObject(_preferences, Formatting.Indented));
            }
            catch (Exception ex) { Logger.LogError("[Workspace] Could not save home preferences.", ex); }
        }

        private static string ContactEmail() => string.IsNullOrWhiteSpace(Properties.Settings.Default.ITContactEmail) ? "it-support@yakult.com" : Properties.Settings.Default.ITContactEmail.Trim();

        private static void ContactSupport()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("mailto:" + Uri.EscapeDataString(ContactEmail())) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.LogError("[Workspace] Could not open the email application.", ex);
                MessageBox.Show("Please contact IT support at " + ContactEmail(), "IT support", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private static Brush Brush(string hex)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
            brush.Freeze();
            return brush;
        }

        private static TextBlock Label(string text, double size, Brush foreground, FontWeight? weight = null, Thickness? margin = null)
        {
            return new TextBlock { Text = text, FontSize = size, Foreground = foreground, FontWeight = weight ?? FontWeights.Normal, Margin = margin ?? new Thickness(0), TextWrapping = TextWrapping.Wrap };
        }

        private static Button ActionButton(string text, string icon, Action action)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (icon != null)
            {
                var glyph = MakeIcon(icon, null, 17);
                glyph.Margin = new Thickness(0, 0, 10, 0);
                row.Children.Add(glyph);
            }
            var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(label, 1);
            row.Children.Add(label);
            var button = new Button { Style = ActionStyle, Content = row, FontSize = 12, Foreground = Ink, Padding = new Thickness(12, 10, 12, 10), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(button, text);
            button.Click += (_, __) => action();
            return button;
        }

        private static Style CreateActionStyle(bool darkHeader = false)
        {
            var border = new FrameworkElementFactory(typeof(Border), "Surface");
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            border.AppendChild(presenter);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, darkHeader ? Brush("#29364D") : Accent, "Surface"));
            hover.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1), "Surface"));
            if (darkHeader) hover.Setters.Add(new Setter(Border.BackgroundProperty, Brush("#29364D"), "Surface"));
            template.Triggers.Add(hover);
            var press = new Trigger { Property = Button.IsPressedProperty, Value = true };
            press.Setters.Add(new Setter(UIElement.OpacityProperty, 0.72, "Surface"));
            template.Triggers.Add(press);
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focus.Setters.Add(new Setter(Border.BorderBrushProperty, darkHeader ? Brush("#FF9AA8") : Accent, "Surface"));
            focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "Surface"));
            template.Triggers.Add(focus);
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            return style;
        }

        private static FrameworkElement MakeIcon(string name, Brush foreground, double size)
        {
            string data;
            switch (name)
            {
                case "home": data = "M3,10 L12,3 21,10 M5,9 L5,21 10,21 10,14 14,14 14,21 19,21 19,9"; break;
                case "inventory": data = "M3,7 L12,3 21,7 21,18 12,22 3,18 Z M3,7 L12,11 21,7 M12,11 L12,22 M7,5 L16,9"; break;
                case "headset": data = "M4,14 L4,11 A8,8 0 0 1 20,11 L20,14 M4,12 L7,12 7,19 3,19 3,12 Z M17,12 L21,12 21,19 17,19 Z M20,19 Q20,22 14,22"; break;
                case "request": data = "M8,5 L4,5 4,22 20,22 20,5 16,5 M8,3 L16,3 16,7 8,7 Z M8,12 L16,12 M8,17 L14,17"; break;
                case "printer": data = "M6,8 L6,3 18,3 18,8 M6,18 L3,18 3,8 21,8 21,18 18,18 M6,14 L18,14 18,22 6,22 Z M17,11 L18,11"; break;
                case "borrow": data = "M4,7 L20,7 M16,3 L20,7 16,11 M20,17 L4,17 M8,13 L4,17 8,21"; break;
                case "report": data = "M4,3 L4,21 22,21 M8,17 L8,11 M13,17 L13,6 M18,17 L18,9"; break;
                case "repair": data = "M14,3 Q20,1 22,6 L18,10 14,6 M22,6 Q24,14 16,14 L7,23 2,18 11,9 Q9,3 14,3 Z"; break;
                case "settings": data = "M9,3 L15,3 16,7 20,8 23,13 20,17 16,18 15,22 9,22 8,18 4,17 1,12 4,8 8,7 Z M16,12 A4,4 0 1 1 8,12 A4,4 0 1 1 16,12"; break;
                case "grid": data = "M3,3 L10,3 10,10 3,10 Z M14,3 L21,3 21,10 14,10 Z M3,14 L10,14 10,21 3,21 Z M14,14 L21,14 21,21 14,21 Z"; break;
                case "pin": data = "M8,3 L17,3 M9,3 L9,10 5,15 19,15 15,10 15,3 M12,15 L12,23"; break;
                case "search": data = "M17,10 A7,7 0 1 1 3,10 A7,7 0 1 1 17,10 M15,15 L22,22"; break;
                case "person": data = "M16,7 A4,4 0 1 1 8,7 A4,4 0 1 1 16,7 M3,22 Q3,14 12,14 Q21,14 21,22"; break;
                case "logout": data = "M10,3 L3,3 3,21 10,21 M8,12 L22,12 M17,7 L22,12 17,17"; break;
                case "help": data = "M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12 M8,8 Q8,4 13,6 Q18,8 12,12 L12,14 M12,17 L12,18"; break;
                case "chat": data = "M4,4 L20,4 20,14 14,14 10,20 10,14 4,14 Z"; break;
                default: data = "M3,12 L21,12 M15,6 L21,12 15,18"; break;
            }
            var path = new System.Windows.Shapes.Path { Data = Geometry.Parse(data), StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Stretch = Stretch.Uniform, Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
            if (foreground != null) path.Stroke = foreground;
            else path.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Button), 1) });
            return path;
        }

        private void OpenAccountDialog()
        {
            try
            {
                using (var dlg = new System.Windows.Forms.Form
                {
                    Text            = "Account Settings",
                    Size            = new System.Drawing.Size(1020, 700),
                    MinimumSize     = new System.Drawing.Size(880, 620),
                    StartPosition   = System.Windows.Forms.FormStartPosition.CenterParent,
                    MinimizeBox     = false,
                    MaximizeBox     = false,
                    FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable
                })
                {
                    var page = new MyAccountPage { Dock = System.Windows.Forms.DockStyle.Fill };
                    dlg.Controls.Add(page);
                    dlg.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("[WpfMainDashboardShell] Failed to open Account dialog.", ex);
            }
        }

        // ====================================================================
        // Shared read-only assistant (available across all desktop modules).
        private void ToggleYakumi() => YakumiAssistantHost.Open(this);

        // ====================================================================
        // DEVELOPER DATABASE RESET
        // ====================================================================

        // Dummy database is YIMS_PROD. Any Yakult_* name is production (see DatabaseSetupForm).
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

        private static bool CanAccessDatabaseSwitcher()
        {
            if (AppSession.IsDeveloper && AppSession.IsSuperAdmin)
                return true;

            return string.Equals(AppSession.CurrentCompanyName,    "YPI",                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(AppSession.CurrentDepartmentName, "INFORMATION TECHNOLOGY", StringComparison.OrdinalIgnoreCase)
                && string.Equals(AppSession.CurrentBranchName,     "MANILA LIAISON OFFICE",  StringComparison.OrdinalIgnoreCase);
        }

        private void HandleDeveloperDatabaseReset()
        {
            using (var form = new Pages.Admin.DBConn.DatabaseSetupForm())
            {
                if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    System.Windows.Forms.Application.Restart();
                    Environment.Exit(0);
                }
            }
        }

        // ====================================================================
        // EDIT IT CONTACT (Admin only)
        // ====================================================================

        private void ShowEditITContactDialog(TextBlock emailLink, string oldReference)
        {
            if (!AppSession.IsAdmin) return;

            var overlay = new Grid { Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)) };
            Grid.SetRowSpan(overlay, 3);
            KeyboardNavigation.SetTabNavigation(overlay, KeyboardNavigationMode.Cycle);

            var dialog = new Border
            {
                Background     = Brushes.White,
                CornerRadius   = new CornerRadius(12),
                Width          = 400,
                Padding        = new Thickness(24),
                Effect         = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.2, Color = Colors.Black }
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text       = "Edit IT Contact",
                FontSize   = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                Margin     = new Thickness(0, 0, 0, 16)
            });

            stack.Children.Add(new TextBlock { Text = "Email", FontSize = 11, Foreground = new SolidColorBrush(TextSecondary), Margin = new Thickness(0, 0, 0, 4) });
            var txtEmail = new System.Windows.Controls.TextBox
            {
                Text    = Properties.Settings.Default.ITContactEmail ?? "it-support@yakult.com",
                Padding = new Thickness(8),
                Margin  = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(txtEmail);

            stack.Children.Add(new TextBlock { Text = "Reference / Hours", FontSize = 11, Foreground = new SolidColorBrush(TextSecondary), Margin = new Thickness(0, 0, 0, 4) });
            var txtRef = new System.Windows.Controls.TextBox
            {
                Text    = Properties.Settings.Default.ITContactReference ?? "IT Helpdesk - Mon-Fri 8AM-5PM",
                Padding = new Thickness(8),
                Margin  = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(txtRef);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancelBtn = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 32,
                Background = Brushes.White,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            var saveBtn = new Button
            {
                Content = "Save",
                Width = 80,
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0)
            };

            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(saveBtn);
            stack.Children.Add(btnRow);
            dialog.Child = stack;

            var centerPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            centerPanel.Children.Add(dialog);
            overlay.Children.Add(centerPanel);
            overlay.MouseDown += (_, e) => { if (e.Source == overlay) _rootGrid.Children.Remove(overlay); };
            overlay.KeyDown += (_, e) => { if (e.Key == Key.Escape) _rootGrid.Children.Remove(overlay); };
            overlay.Focusable = true;

            cancelBtn.Click += (_, __) => _rootGrid.Children.Remove(overlay);
            saveBtn.Click += (_, __) =>
            {
                try
                {
                    Properties.Settings.Default.ITContactEmail = txtEmail.Text.Trim();
                    Properties.Settings.Default.ITContactReference = txtRef.Text.Trim();
                    Properties.Settings.Default.Save();
                    emailLink.Text = ContactEmail();
                    _supportReference.Text = Properties.Settings.Default.ITContactReference;
                    _rootGrid.Children.Remove(overlay);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to save:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            _rootGrid.Children.Add(overlay);
            overlay.Focus();
            txtEmail.Focus();
        }

        // ====================================================================
        // HELP DIALOG
        // ====================================================================

        private void ShowHelpDialog()
        {
            var overlay = new Grid { Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)) };
            Grid.SetRowSpan(overlay, 3);
            KeyboardNavigation.SetTabNavigation(overlay, KeyboardNavigationMode.Cycle);

            var dialog = new Border
            {
                Background      = Brushes.White,
                CornerRadius    = new CornerRadius(12),
                Width           = 560,
                MaxHeight       = 620,
                Effect          = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.2, Color = Colors.Black }
            };

            var dialogRoot = new Grid();
            dialogRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // ─ Title bar with X ─
            var titleBar = new Grid { Margin = new Thickness(24, 20, 20, 0) };
            var titleText = new TextBlock
            {
                Text       = "Help & Shortcuts",
                FontSize   = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                VerticalAlignment = VerticalAlignment.Center
            };
            var closeX = ActionButton("Close", null, () => _rootGrid.Children.Remove(overlay));
            closeX.HorizontalAlignment = HorizontalAlignment.Right;
            titleBar.Children.Add(titleText);
            titleBar.Children.Add(closeX);
            Grid.SetRow(titleBar, 0);
            dialogRoot.Children.Add(titleBar);

            // ─ Scrollable content ─
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(24, 16, 24, 20) };
            var stack = new StackPanel();

            // ─ Portal Descriptions ─
            stack.Children.Add(MakeHelpSection("Portal Descriptions"));
            foreach (var module in _modules)
                stack.Children.Add(MakeHelpRow(module.Title, module.Description));

            // ─ Keyboard Shortcuts ─
            stack.Children.Add(MakeHelpSection("Keyboard Shortcuts"));
            stack.Children.Add(MakeHelpRow("Ctrl + K", "Search your available systems."));
            stack.Children.Add(MakeHelpRow("Tab / Enter", "Move between controls and activate a selected control."));
            stack.Children.Add(MakeHelpRow("F1", "Open Help & Shortcuts."));
            foreach (var module in _modules.Where(m => !string.IsNullOrEmpty(m.Shortcut)))
                stack.Children.Add(MakeHelpRow(module.Shortcut, "Open " + module.Title + "."));
            stack.Children.Add(MakeHelpRow("Esc", "Close this dialog, clear an active search, or log out from home."));
            if (CanAccessDatabaseSwitcher())
                stack.Children.Add(MakeHelpRow("Ctrl + Shift + D", "Reset database configuration and restart."));

            // ─ User Manual ─
            stack.Children.Add(MakeHelpSection("User Manual"));
            stack.Children.Add(ActionButton("Contact IT support", "headset", ContactSupport));
            stack.Children.Add(new TextBlock
            {
                Text         = "User manual is not yet updated.",
                FontSize     = 11,
                Foreground   = new SolidColorBrush(TextSecondary),
                Margin       = new Thickness(0, 6, 0, 0)
            });

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            dialogRoot.Children.Add(scroll);
            dialog.Child = dialogRoot;

            // Center dialog
            overlay.Children.Add(new Border()); // spacer
            var centerPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            centerPanel.Children.Add(dialog);
            overlay.Children.Add(centerPanel);

            // Close handlers
            overlay.MouseDown += (_, e) => { if (e.Source == overlay) _rootGrid.Children.Remove(overlay); };
            overlay.KeyDown += (_, e) => { if (e.Key == Key.Escape) _rootGrid.Children.Remove(overlay); };
            overlay.Focusable = true;
            _rootGrid.Children.Add(overlay);
            closeX.Focus();
        }

        private static TextBlock MakeHelpSection(string title)
        {
            return new TextBlock
            {
                Text       = title,
                FontSize   = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                Margin     = new Thickness(0, 16, 0, 8)
            };
        }

        private static StackPanel MakeHelpRow(string label, string description)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            row.Children.Add(new TextBlock
            {
                Text       = label,
                FontSize   = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                Width      = 160,
                TextWrapping = TextWrapping.Wrap
            });
            row.Children.Add(new TextBlock
            {
                Text       = description,
                FontSize   = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                TextWrapping = TextWrapping.Wrap,
                Width      = 300
            });
            return row;
        }

        private static BitmapImage TryLoadLogo()
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
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource    = new Uri(path);
                    bmp.CacheOption  = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    return bmp;
                }
            }
            catch { }
            return null;
        }
    }
}
