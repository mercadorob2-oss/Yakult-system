using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Forms.Portal;
using Yakult.Inventory.App.Pages.User;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Security;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Wpf.Portal
{
    /// <summary>
    /// Pure WPF portal shell. Replaces the WinForms <see cref="MainDashboardForm"/>.
    /// Shows module cards filtered by <see cref="PermissionResolver"/> based on
    /// the logged-in user's roles, then returns a <see cref="PortalSelection"/>
    /// to the caller.
    /// </summary>
    public sealed class WpfMainDashboardShell : Window
    {
        // ====================================================================
        // RESULT
        // ====================================================================

        public PortalSelection SelectedModule { get; private set; } = PortalSelection.None;

        // ====================================================================
        // LAYOUT ROOTS
        // ====================================================================

        private readonly Grid _rootGrid;
        private System.Windows.Controls.Primitives.UniformGrid _cardsPanel;

        // Quick Overview stat labels (populated async)
        private TextBlock _statPendingTickets;
        private TextBlock _statUnresolved;

        // ====================================================================
        // COLORS
        // ====================================================================

        private static readonly Color HeaderStart   = Color.FromRgb(15,  23,  42);
        private static readonly Color HeaderEnd     = Color.FromRgb(30,  64, 120);
        private static readonly Color ContentBg     = Color.FromRgb(245, 247, 251);
        private static readonly Color TextPrimary   = Color.FromRgb(15,  23,  42);
        private static readonly Color TextSecondary = Color.FromRgb(100, 116, 139);
        private static readonly Color FooterBg      = Color.FromRgb(241, 243, 247);

        // Module accent colors  (#2563EB #14B8A6 #9333EA #F97316 #EF4444 #64748B #EC4899)
        private static readonly Color AInventory  = Color.FromRgb(37,   99, 235);
        private static readonly Color ACallIT     = Color.FromRgb(20,  184, 166);
        private static readonly Color ARequester  = Color.FromRgb(147,  51, 234);
        private static readonly Color ACartridge  = Color.FromRgb(249, 115,  22);
        private static readonly Color ABorrow     = Color.FromRgb(239,  68,  68);
        private static readonly Color AReports    = Color.FromRgb(100, 116, 139);
        private static readonly Color AAdmin      = Color.FromRgb(236,  72, 153);
        private static readonly Color ARepairPortal = Color.FromRgb(13,  148, 136);

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        public WpfMainDashboardShell()
        {
            Title                  = "Yakult Internal Systems";
            WindowState            = WindowState.Maximized;
            WindowStartupLocation  = WindowStartupLocation.CenterScreen;
            MinWidth               = 1024;
            MinHeight              = 700;
            Background             = new SolidColorBrush(ContentBg);
            FontFamily             = new FontFamily("Segoe UI");
            Focusable              = true;

            _rootGrid = new Grid();
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(90) });              // header
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // content
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                 // footer

            BuildHeader();
            BuildContent();
            BuildFooter();

            Content = _rootGrid;

            Loaded  += async (_, __) => { try { await LoadStatsAsync(); } catch { } };
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    SelectAndClose(PortalSelection.Logout);
                    e.Handled = true;
                    return;
                }
                if ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                    (e.KeyboardDevice.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift &&
                    e.Key == Key.D)
                {
                    if (CanAccessDatabaseSwitcher())
                        HandleDeveloperDatabaseReset();
                    e.Handled = true;
                    return;
                }
                // Alt+ shortcuts for direct portal launch
                var roles = AppSession.CurrentUserRoles;
                if ((e.KeyboardDevice.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                {
                    switch (e.Key)
                    {
                        case Key.I:
                            if (PermissionResolver.HasPortalAccess(roles, PermissionResolver.Portal.InventorySystem))
                            { SelectAndClose(PortalSelection.InventorySystem); e.Handled = true; }
                            return;
                        case Key.C:
                            if (PermissionResolver.HasPortalAccess(roles, PermissionResolver.Portal.CallITMonitoring))
                            { SelectAndClose(PortalSelection.ITCallMonitoring); e.Handled = true; }
                            return;
                        case Key.M:
                            if (PermissionResolver.HasPortalAccess(roles, PermissionResolver.Portal.CartridgeManagement))
                            { SelectAndClose(PortalSelection.CartridgeManagement); e.Handled = true; }
                            return;
                        case Key.R:
                            if (PermissionResolver.HasPortalAccess(roles, PermissionResolver.Portal.Reports))
                            { SelectAndClose(PortalSelection.Reports); e.Handled = true; }
                            return;
                        case Key.B:
                            if (PermissionResolver.HasPortalAccess(roles, PermissionResolver.Portal.BorrowItems))
                            { SelectAndClose(PortalSelection.BorrowItems); e.Handled = true; }
                            else
                            {
                                System.Windows.MessageBox.Show("You do not have access to Borrow Items.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                                e.Handled = true;
                            }
                            return;
                    }
                }
            };
        }

        // ====================================================================
        // HEADER
        // ====================================================================

        private void BuildHeader()
        {
            var gradient = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(HeaderStart, 0.0),
                    new GradientStop(HeaderEnd,   1.0)
                },
                new Point(0, 0), new Point(1, 0));

            var header = new Border { Background = gradient, Padding = new Thickness(28, 0, 28, 0) };
            Grid.SetRow(header, 0);

            // Single row: [logo] [title/sub] [spacer] [?] [avatar+name+role]
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                      // 0 logo
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                      // 1 titles
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 2 spacer
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                      // 3 help
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                      // 4 user

            // ─ Logo
            var logo = TryLoadLogo();
            if (logo != null)
            {
                var img = new System.Windows.Controls.Image
                {
                    Source            = logo,
                    Width             = 150,
                    Height            = 40,
                    Stretch           = Stretch.Uniform,
                    Margin            = new Thickness(0, 0, 16, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(img, 0);
                grid.Children.Add(img);
            }

            // ─ Titles
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text       = "INTERNAL PORTAL",
                FontSize   = 17,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            titleStack.Children.Add(new TextBlock
            {
                Text       = $"Welcome back, {AppSession.CurrentUserName ?? "User"}",
                FontSize   = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 210, 235)),
                Margin     = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(titleStack, 1);
            grid.Children.Add(titleStack);

            // ─ Dummy database indicator (col 2, spacer) — only shown when connected to the dummy DB
            if (IsDummyDatabase())
            {
                var dummyBadge = new Border
                {
                    Background          = new SolidColorBrush(Color.FromArgb(60, 255, 193, 7)),
                    BorderBrush         = new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                    BorderThickness     = new Thickness(1),
                    CornerRadius        = new CornerRadius(4),
                    Padding             = new Thickness(10, 4, 10, 4),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                };
                dummyBadge.Child = new TextBlock
                {
                    Text       = "DUMMY DATABASE",
                    FontSize   = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7))
                };
                Grid.SetColumn(dummyBadge, 2);
                grid.Children.Add(dummyBadge);
            }

            // ─ Help "?" circle (col 3)
            var helpCircle = new Border
            {
                Background          = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
                CornerRadius        = new CornerRadius(19),
                Width               = 38,
                Height              = 38,
                Margin              = new Thickness(0, 0, 10, 0),
                VerticalAlignment   = VerticalAlignment.Center,
                Cursor              = Cursors.Hand
            };
            helpCircle.Child = new TextBlock { Text = "?", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            helpCircle.MouseDown += (_, __) => ShowHelpDialog();
            Grid.SetColumn(helpCircle, 3);
            grid.Children.Add(helpCircle);

            // ─ User: avatar circle + name/role (col 4)
            var userPanel = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(4, 0, 0, 0)
            };
            var initial = (AppSession.CurrentUserName ?? "U").Length > 0
                ? (AppSession.CurrentUserName ?? "U")[0].ToString().ToUpper() : "U";
            var avatarCircle = new Border
            {
                Background   = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                CornerRadius = new CornerRadius(19),
                Width        = 38,
                Height       = 38,
                Margin       = new Thickness(0, 0, 10, 0)
            };
            avatarCircle.Child = new TextBlock
            {
                Text                = initial,
                Foreground          = Brushes.White,
                FontSize            = 16,
                FontWeight          = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            userPanel.Children.Add(avatarCircle);
            var userInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            userInfo.Children.Add(new TextBlock
            {
                Text       = AppSession.CurrentUserName ?? "User",
                Foreground = Brushes.White,
                FontSize   = 12,
                FontWeight = FontWeights.Bold
            });
            // Show employee position if available, otherwise fall back to roles
            var roleText = !string.IsNullOrWhiteSpace(AppSession.CurrentEmployeePosition)
                ? AppSession.CurrentEmployeePosition
                : (AppSession.CurrentUserRoles?.Count > 0
                    ? string.Join(", ", AppSession.CurrentUserRoles)
                    : "User");
            userInfo.Children.Add(new TextBlock
            {
                Text       = roleText,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 210, 235)),
                FontSize   = 10
            });
            userPanel.Children.Add(userInfo);
            Grid.SetColumn(userPanel, 4);
            grid.Children.Add(userPanel);

            header.Child = grid;
            _rootGrid.Children.Add(header);
        }

        // ====================================================================
        // CONTENT
        // ====================================================================

        private void BuildContent()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetRow(scroll, 1);

            var contentStack = new StackPanel { Margin = new Thickness(80, 16, 80, 36) };

            // ── Account / Logout buttons (top-right, white area)
            var actionBar = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            actionBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var btnStrip = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var btnAcc = MakeHeaderButton("My Account", Color.FromRgb(39, 174, 96));
            btnAcc.Click += (_, __) => OpenAccountDialog();
            var btnOut = MakeHeaderButton("Logout", Color.FromRgb(192, 57, 43));
            btnOut.Margin = new Thickness(10, 0, 0, 0);
            btnOut.Click  += (_, __) => SelectAndClose(PortalSelection.Logout);
            btnStrip.Children.Add(btnAcc);
            btnStrip.Children.Add(btnOut);
            Grid.SetColumn(btnStrip, 1);
            actionBar.Children.Add(btnStrip);
            contentStack.Children.Add(actionBar);

            // ── Greeting row ─────────────────────────────────────────────────
            var greetGrid = new Grid { Margin = new Thickness(0, 0, 0, 28) };
            greetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            greetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var greetStack = new StackPanel();
            greetStack.Children.Add(new TextBlock
            {
                Text       = $"Good day, {AppSession.CurrentUserName ?? "User"}! \U0001F44B",
                FontSize   = 26,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(TextPrimary)
            });
            greetStack.Children.Add(new TextBlock
            {
                Text       = "Access your systems and tools quickly and efficiently.",
                FontSize   = 13,
                Foreground = new SolidColorBrush(TextSecondary),
                Margin     = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(greetStack, 0);
            greetGrid.Children.Add(greetStack);

            // "All Systems Operational" badge
            var statusRow = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top,
                Margin            = new Thickness(0, 8, 0, 0)
            };
            statusRow.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width             = 8,
                Height            = 8,
                Fill              = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                VerticalAlignment = VerticalAlignment.Center
            });
            statusRow.Children.Add(new TextBlock
            {
                Text              = "  All Systems Operational",
                FontSize          = 11,
                FontWeight        = FontWeights.Bold,
                Foreground        = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(statusRow, 1);
            greetGrid.Children.Add(statusRow);

            contentStack.Children.Add(greetGrid);

            // ── Module cards ─────────────────────────────────────────────────
            _cardsPanel = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 4,
                Margin  = new Thickness(-8, 0, -8, 4)
            };
            BuildModuleCards();
            contentStack.Children.Add(_cardsPanel);

            // ── Quick Overview ────────────────────────────────────────────────
            contentStack.Children.Add(BuildQuickOverviewPanel());

            scroll.Content = contentStack;
            _rootGrid.Children.Add(scroll);
        }

        private void BuildModuleCards()
        {
            var roles = AppSession.CurrentUserRoles;

            // Admin Portal is pinned to the first slot (top-left) when the user has access.
            if (PermissionResolver.HasPortalAccess(roles, PermissionResolver.Portal.AdminPortal))
                _cardsPanel.Children.Add(MakeCard(
                    "Admin Portal",
                    "Manage user accounts, roles, SMTP settings, and configuration.",
                    "⚙", AAdmin,
                    () => SelectAndClose(PortalSelection.AdminPortal)));

            if (PermissionResolver.HasPortalAccess(roles, PermissionResolver.Portal.InventorySystem))
                _cardsPanel.Children.Add(MakeCard(
                    "Inventory System",
                    "Manage stock levels, requests, and analyze movement data.",
                    "\U0001F4E6", AInventory,
                    () => SelectAndClose(PortalSelection.InventorySystem)));

            if (PermissionResolver.HasPortalAccess(roles, PermissionResolver.Portal.CallITMonitoring))
                _cardsPanel.Children.Add(MakeCard(
                    "IT Call Monitoring",
                    "Track helpdesk tickets, escalations, and issue resolution.",
                    "\U0001F3A7", ACallIT,
                    () => SelectAndClose(PortalSelection.ITCallMonitoring)));

            if (PermissionResolver.HasPortalAccess(roles, PermissionResolver.Portal.RequesterPortal))
                _cardsPanel.Children.Add(MakeCard(
                    "Request/s Portal",
                    "Quick access to submit and track cartridge requests.",
                    "\U0001F4CB", ARequester,
                    () => SelectAndClose(PortalSelection.RequesterPortal)));

            if (PermissionResolver.HasPortalAccess(roles, PermissionResolver.Portal.CartridgeManagement))
                _cardsPanel.Children.Add(MakeCard(
                    "Consumable Management",
                    "Manage cartridges, ink, printhead, and toner cartridge requests and fulfillment.",
                    "\U0001F5A8", ACartridge,
                    () => SelectAndClose(PortalSelection.CartridgeManagement)));

            if (PermissionResolver.HasPortalAccess(roles, PermissionResolver.Portal.BorrowItems))
                _cardsPanel.Children.Add(MakeCard(
                    "Borrow Items",
                    "Log borrowed hardware by serial number and track returns.",
                    "\U0001F501", ABorrow,
                    () => SelectAndClose(PortalSelection.BorrowItems)));

            if (PermissionResolver.HasPortalAccess(roles, PermissionResolver.Portal.Reports))
                _cardsPanel.Children.Add(MakeCard(
                    "Reports",
                    "Generate and view system reports and analytics.",
                    "\U0001F4CA", AReports,
                    () => SelectAndClose(PortalSelection.Reports)));

            // (Admin Portal card is rendered first - see top of this method.)

            if (PermissionResolver.HasPortalAccess(roles, PermissionResolver.Portal.RepairTechnicianPortal))
                _cardsPanel.Children.Add(MakeCard(
                    "Repair Technician Portal",
                    "Log, track, and resolve equipment repairs with photo/video evidence.",
                    "\U0001F527", ARepairPortal,
                    () => SelectAndClose(PortalSelection.RepairTechnicianPortal)));
        }

        // ====================================================================
        // CARD FACTORY
        // ====================================================================

        private Border MakeCard(string title, string desc, string icon, Color accent, Action onClick)
        {
            var normalShadow = new DropShadowEffect { BlurRadius = 8,  ShadowDepth = 2, Opacity = 0.07, Direction = 270, Color = Colors.Black };
            var hoverShadow  = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 4, Opacity = 0.15, Direction = 270, Color = Colors.Black };

            var card = new Border
            {
                Margin          = new Thickness(8, 0, 8, 20),
                Background      = Brushes.White,
                BorderBrush     = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(12),
                Cursor          = Cursors.Hand,
                Effect          = normalShadow
            };

            // Inner grid: 4px accent top bar + body
            var innerGrid = new Grid();
            innerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
            innerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var accentBar = new Border { Background = new SolidColorBrush(accent), CornerRadius = new CornerRadius(10, 10, 0, 0) };
            Grid.SetRow(accentBar, 0);
            innerGrid.Children.Add(accentBar);

            // Body — icon ABOVE title (vertical stack)
            var body = new StackPanel { Margin = new Thickness(22, 20, 22, 20) };

            // Icon square (above title)
            var iconBox = new Border
            {
                Background          = new SolidColorBrush(Color.FromArgb(32, accent.R, accent.G, accent.B)),
                CornerRadius        = new CornerRadius(12),
                Width               = 52,
                Height              = 52,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin              = new Thickness(0, 0, 0, 14)
            };
            iconBox.Child = new TextBlock
            {
                Text                = icon,
                FontSize            = 24,
                FontFamily          = new FontFamily("Segoe UI Emoji"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            body.Children.Add(iconBox);

            // Title
            body.Children.Add(new TextBlock
            {
                Text         = title,
                FontSize     = 15,
                FontWeight   = FontWeights.Bold,
                Foreground   = new SolidColorBrush(TextPrimary),
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 0, 0, 6)
            });

            // Description
            body.Children.Add(new TextBlock
            {
                Text         = desc,
                FontSize     = 12,
                Foreground   = new SolidColorBrush(TextSecondary),
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 0, 0, 16)
            });

            // Open System →
            body.Children.Add(new TextBlock
            {
                Text       = "Open System \u2192",
                FontSize   = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(accent)
            });

            Grid.SetRow(body, 1);
            innerGrid.Children.Add(body);
            card.Child = innerGrid;

            card.MouseEnter += (_, __) => { card.Effect = hoverShadow; card.BorderBrush = new SolidColorBrush(accent); };
            card.MouseLeave += (_, __) => { card.Effect = normalShadow; card.BorderBrush = new SolidColorBrush(Color.FromRgb(225, 230, 236)); };
            WireActivateClick(card, onClick);

            return card;
        }

        /// <summary>
        /// Wires a "click" (press + release on the same element) instead of a bare
        /// <see cref="UIElement.MouseLeftButtonUp"/> handler.
        ///
        /// A lone MouseLeftButtonUp handler fires for ANY left-button release over the element —
        /// including a stale / redelivered WM_LBUTTONUP whose matching WM_LBUTTONDOWN happened on a
        /// different window. That is exactly what occurs when a sub-portal is closed by clicking its
        /// own "Back to Portal" button: MainForm re-shows this dashboard synchronously (via a nested
        /// ShowDialog message pump) while that click is still being routed, so the trailing button-up
        /// lands on whichever card now sits under the cursor and instantly re-launches a portal the
        /// user never chose. Requiring the press to have started on this same element (the same
        /// contract Button.Click uses) discards that fall-through release.
        /// </summary>
        private static void WireActivateClick(FrameworkElement element, Action onClick)
        {
            if (element == null || onClick == null) return;

            bool pressedHere = false;
            element.MouseLeftButtonDown += (_, __) => pressedHere = true;
            element.MouseLeave          += (_, __) => pressedHere = false;
            element.MouseLeftButtonUp   += (_, __) =>
            {
                if (!pressedHere) return;
                pressedHere = false;
                onClick();
            };
        }

        // ====================================================================
        // QUICK OVERVIEW PANEL
        // ====================================================================

        private UIElement BuildQuickOverviewPanel()
        {
            var outer = new Border
            {
                Margin          = new Thickness(0, 28, 0, 0),
                Background      = Brushes.White,
                BorderBrush     = new SolidColorBrush(Color.FromRgb(225, 230, 236)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(10),
                Effect          = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = 0.07, Direction = 270, Color = Colors.Black }
            };

            var outerGrid = new Grid();
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });

            // ── Left: stats ──────────────────────────────────────────────────
            var statsSection = new StackPanel { Margin = new Thickness(26, 22, 26, 22) };
            statsSection.Children.Add(new TextBlock
            {
                Text       = "Quick Overview",
                FontSize   = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(TextPrimary),
                Margin     = new Thickness(0, 0, 0, 16)
            });

            var statsRow = new StackPanel { Orientation = Orientation.Horizontal };

            _statPendingTickets = AddStatTile(statsRow, "—", "Pending Tickets",  ACallIT,    "\U0001F3A7", () => SelectAndClose(PortalSelection.ITCallMonitoring));
            /*_statOpenRequests = */ AddStatTile(statsRow, "—", "Open Requests",   ARequester, "\U0001F4CB", () => SelectAndClose(PortalSelection.RequesterPortal));
            /*_statBorrowedItems= */ AddStatTile(statsRow, "—", "Borrowed Items",  ABorrow,    "\U0001F501", () => SelectAndClose(PortalSelection.BorrowItems));
            _statUnresolved    = AddStatTile(statsRow, "—", "Unresolved Issues", ACartridge, "\U0001F5A8", () => SelectAndClose(PortalSelection.ITCallMonitoring));

            statsSection.Children.Add(statsRow);
            Grid.SetColumn(statsSection, 0);
            outerGrid.Children.Add(statsSection);

            // ── Right: Need Help ─────────────────────────────────────────────
            var helpCard = new Border
            {
                Margin          = new Thickness(0, 18, 22, 18),
                Background      = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(215, 225, 235)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(20, 16, 20, 16),
                VerticalAlignment = VerticalAlignment.Center
            };
            var helpBody = new StackPanel();

            // Headset icon + title row
            var helpTitleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var helpIconCircle = new Border
            {
                Background        = new SolidColorBrush(Color.FromArgb(22, 52, 152, 219)),
                CornerRadius      = new CornerRadius(22),
                Width             = 44,
                Height            = 44,
                Margin            = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            helpIconCircle.Child = new TextBlock
            {
                Text                = "\U0001F3A7",
                FontSize            = 22,
                FontFamily          = new FontFamily("Segoe UI Emoji"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            helpTitleRow.Children.Add(helpIconCircle);
            helpTitleRow.Children.Add(new TextBlock
            {
                Text              = "Need Help?",
                FontSize          = 14,
                FontWeight        = FontWeights.Bold,
                Foreground        = new SolidColorBrush(TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            });
            helpBody.Children.Add(helpTitleRow);

            // ── IT Contact info (dynamic, admin-editable) ──
            var email = Properties.Settings.Default.ITContactEmail ?? "it-support@yakult.com";
            var reference = Properties.Settings.Default.ITContactReference ?? "IT Helpdesk";

            var emailLink = new TextBlock
            {
                Text         = email,
                FontSize     = 11,
                FontWeight   = FontWeights.SemiBold,
                Foreground   = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Cursor       = Cursors.Hand,
                TextWrapping = TextWrapping.Wrap
            };
            emailLink.MouseDown += (_, __) =>
            {
                try { System.Diagnostics.Process.Start("mailto:" + email); }
                catch { }
            };
            helpBody.Children.Add(emailLink);

            helpBody.Children.Add(new TextBlock
            {
                Text         = reference,
                FontSize     = 11,
                Foreground   = new SolidColorBrush(TextSecondary),
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 2, 0, 10)
            });

            var contactRow = new StackPanel { Orientation = Orientation.Horizontal };
            var supportBtn = MakeHeaderButton("Contact Support \u2192", Color.FromRgb(52, 152, 219));
            supportBtn.HorizontalAlignment = HorizontalAlignment.Left;
            supportBtn.Width = double.NaN;
            contactRow.Children.Add(supportBtn);

            // Admin-only edit button
            if (AppSession.IsAdmin)
            {
                var editBtn = new TextBlock
                {
                    Text              = "\u270E  Edit",
                    FontSize          = 10,
                    Foreground        = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                    Cursor            = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(10, 0, 0, 0)
                };
                editBtn.MouseEnter += (_, __) => editBtn.Foreground = new SolidColorBrush(Color.FromRgb(52, 73, 94));
                editBtn.MouseLeave += (_, __) => editBtn.Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141));
                editBtn.MouseDown += (_, __) => ShowEditITContactDialog(emailLink, reference);
                contactRow.Children.Add(editBtn);
            }
            helpBody.Children.Add(contactRow);
            helpCard.Child = helpBody;
            Grid.SetColumn(helpCard, 1);
            outerGrid.Children.Add(helpCard);

            outer.Child = outerGrid;
            return outer;
        }

        private TextBlock AddStatTile(StackPanel parent, string count, string label, Color color, string icon, Action onClick)
        {
            var tile = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin      = new Thickness(0, 0, 36, 0),
                Cursor      = onClick != null ? Cursors.Hand : Cursors.Arrow
            };
            WireActivateClick(tile, onClick);

            // Icon box (matching card icon style)
            var iconBox = new Border
            {
                Background        = new SolidColorBrush(Color.FromArgb(22, color.R, color.G, color.B)),
                CornerRadius      = new CornerRadius(10),
                Width             = 46,
                Height            = 46,
                Margin            = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBox.Child = new TextBlock
            {
                Text                = icon,
                FontSize            = 22,
                FontFamily          = new FontFamily("Segoe UI Emoji"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            tile.Children.Add(iconBox);

            var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var countText = new TextBlock
            {
                Text       = count,
                FontSize   = 26,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(TextPrimary)
            };
            textCol.Children.Add(countText);
            textCol.Children.Add(new TextBlock
            {
                Text       = label,
                FontSize   = 11,
                Foreground = new SolidColorBrush(TextSecondary)
            });
            if (onClick != null)
            {
                textCol.Children.Add(new TextBlock
                {
                    Text       = "View all",
                    FontSize   = 10.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                    Margin     = new Thickness(0, 2, 0, 0)
                });
            }
            tile.Children.Add(textCol);
            parent.Children.Add(tile);
            return countText;
        }

        // ====================================================================
        // FOOTER
        // ====================================================================

        private void BuildFooter()
        {
            var footer = new Border
            {
                Background      = new SolidColorBrush(FooterBg),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(220, 226, 232)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding         = new Thickness(50, 12, 50, 12)
            };
            Grid.SetRow(footer, 2);

            var fg = new Grid();
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            fg.Children.Add(new TextBlock
            {
                Text              = "\u00A9 2026 Yakult Philippines, Inc. All rights reserved.",
                FontSize          = 11,
                Foreground        = new SolidColorBrush(TextSecondary),
                VerticalAlignment = VerticalAlignment.Center
            });
            var ver = new TextBlock
            {
                Text              = "Version 2.0.0",
                FontSize          = 11,
                Foreground        = new SolidColorBrush(TextSecondary),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(ver, 1);
            fg.Children.Add(ver);

            footer.Child = fg;
            _rootGrid.Children.Add(footer);
        }

        // ====================================================================
        // NAVIGATION
        // ====================================================================

        private void SelectAndClose(PortalSelection selection)
        {
            SelectedModule = selection;
            Close();
        }

        private void OpenAccountDialog()
        {
            try
            {
                using (var dlg = new System.Windows.Forms.Form
                {
                    Text            = "Account Settings",
                    Size            = new System.Drawing.Size(880, 660),
                    StartPosition   = System.Windows.Forms.FormStartPosition.CenterParent,
                    MinimizeBox     = false,
                    MaximizeBox     = false,
                    FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
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
        // ASYNC DATA LOADING
        // ====================================================================

        private async Task LoadStatsAsync()
        {
            try
            {
                var repo = new CallMonitoringRepository();
                if (!await repo.CallSchemaExistsAsync()) return;

                var rows  = await repo.GetRequiresAttentionAsync(maxRows: 99);
                var count = rows?.Count ?? 0;
                var text  = count.ToString();

                Dispatcher.Invoke(() =>
                {
                    if (_statPendingTickets != null) _statPendingTickets.Text = text;
                    if (_statUnresolved     != null) _statUnresolved.Text     = text;
                });
            }
            catch
            {
                // Non-fatal: stats stay as "—"
            }
        }

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
        // HELPERS
        // ====================================================================

        private static Button MakeHeaderButton(string text, Color bg)
        {
            var baseBg = new SolidColorBrush(bg);
            var hoverBg = new SolidColorBrush(Color.FromRgb(
                (byte)Math.Min(bg.R + 25, 255),
                (byte)Math.Min(bg.G + 25, 255),
                (byte)Math.Min(bg.B + 25, 255)));
            var pressBg = new SolidColorBrush(Color.FromRgb(
                (byte)Math.Max(bg.R - 15, 0),
                (byte)Math.Max(bg.G - 15, 0),
                (byte)Math.Max(bg.B - 15, 0)));

            var border = new Border
            {
                Background    = baseBg,
                CornerRadius  = new CornerRadius(19),
                Padding       = new Thickness(14, 0, 14, 0),
                SnapsToDevicePixels = true,
                Effect        = new DropShadowEffect
                {
                    BlurRadius = 6, ShadowDepth = 2, Opacity = 0.12, Direction = 270, Color = Colors.Black
                }
            };

            border.Child = new TextBlock
            {
                Text                = text,
                Foreground          = Brushes.White,
                FontWeight          = FontWeights.SemiBold,
                FontSize            = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };

            var btn = new Button
            {
                Content         = border,
                Width           = 120,
                Height          = 38,
                Background      = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor          = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Template        = new ControlTemplate(typeof(Button))
                {
                    VisualTree = new FrameworkElementFactory(typeof(ContentPresenter))
                }
            };

            btn.MouseEnter += (_, __) => border.Background = hoverBg;
            btn.MouseLeave += (_, __) => border.Background = baseBg;
            btn.PreviewMouseDown += (_, __) => border.Background = pressBg;
            btn.PreviewMouseUp += (_, __) => border.Background = hoverBg;

            return btn;
        }

        // ====================================================================
        // EDIT IT CONTACT (Admin only)
        // ====================================================================

        private void ShowEditITContactDialog(TextBlock emailLink, string oldReference)
        {
            if (!AppSession.IsAdmin) return;

            var overlay = new Grid { Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)) };
            Grid.SetRowSpan(overlay, 3);

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
                    emailLink.Text = Properties.Settings.Default.ITContactEmail;
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
            var closeX = new TextBlock
            {
                Text              = "\u2715",
                FontSize          = 18,
                Foreground        = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                Cursor            = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding           = new Thickness(6)
            };
            closeX.MouseEnter += (_, __) => closeX.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
            closeX.MouseLeave += (_, __) => closeX.Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141));
            closeX.MouseDown  += (_, __) => _rootGrid.Children.Remove(overlay);
            titleBar.Children.Add(titleText);
            titleBar.Children.Add(closeX);
            Grid.SetRow(titleBar, 0);
            dialogRoot.Children.Add(titleBar);

            // ─ Scrollable content ─
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(24, 16, 24, 20) };
            var stack = new StackPanel();

            // ─ Portal Descriptions ─
            stack.Children.Add(MakeHelpSection("Portal Descriptions"));
            stack.Children.Add(MakeHelpRow("\U0001F4E6  Inventory System",       "Manage stock levels, requests, item movements, and analyze inventory data."));
            stack.Children.Add(MakeHelpRow("\U0001F3A7  IT Call Monitoring",     "Track helpdesk tickets, escalations, SLA compliance, and issue resolution."));
            stack.Children.Add(MakeHelpRow("\U0001F4CB  Request/s Portal",      "Submit and track cartridge and item requests, view approval status."));
            stack.Children.Add(MakeHelpRow("\U0001F5A8  Consumable Management", "Manage cartridge exchanges, refills, brand-new allocations, and fulfillment for cartridges, ink, printhead, and toner cartridge."));
            stack.Children.Add(MakeHelpRow("\U0001F501  Borrow Items",           "Log borrowed hardware by serial number, track returns, and view due dates."));
            stack.Children.Add(MakeHelpRow("\U0001F4CA  Reports",                "Generate and view system reports, analytics, and invoice summaries."));
            stack.Children.Add(MakeHelpRow("\u2699  Admin Portal",               "Manage user accounts, roles, SMTP settings, portal access, and system configuration."));

            // ─ Keyboard Shortcuts ─
            stack.Children.Add(MakeHelpSection("Keyboard Shortcuts"));
            stack.Children.Add(MakeHelpRow("Alt + I",  "Open Inventory System (if accessible)."));
            stack.Children.Add(MakeHelpRow("Alt + C",  "Open IT Call Monitoring (if accessible)."));
            stack.Children.Add(MakeHelpRow("Alt + M",  "Open Consumable Management (if accessible)."));
            stack.Children.Add(MakeHelpRow("Alt + R",  "Open Reports (if accessible)."));
            stack.Children.Add(MakeHelpRow("Alt + B",  "Open Borrow Items (if accessible)."));
            stack.Children.Add(MakeHelpRow("Esc",      "Logout and return to the login screen."));
            stack.Children.Add(MakeHelpRow("Ctrl + Shift + D", "Developer only: Reset database configuration and restart."));

            // ─ User Manual ─
            stack.Children.Add(MakeHelpSection("User Manual"));
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
            overlay.Focus();

            _rootGrid.Children.Add(overlay);
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
                Width      = 160
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
