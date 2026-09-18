using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Collections.Generic;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Forms.CallMonitoring;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed class WpfCallMonitoringDiagnosticsWorkspace : UserControl
    {
        private ICallMonitoringRepository _repo;
        private ICallMonitoringNavigator _navigator;
        
        private CallMonitoringRepository.ItcmDiagnosticsSnapshot _lastSnapshot;
        private int? _lastResolvedTicketId;
        private int? _lastResolvedDeptId;
        private int? _lastResolvedBranchId;
        private ItcmServerPing _lastServerPing;
        private DateTime _lastServerPingAtUtc = DateTime.MinValue;

        // Short-timeout probe client for the ITCM web server (mirrors MainForm pattern).
        private static readonly HttpClient _itcmPingClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        // Result of GET {ItcmServerUrl}/api/itcm/ping. Connected=false means the
        // server did not answer (down, wrong URL, or network blocked), never a throw.
        private sealed class ItcmServerPing
        {
            public bool Connected { get; set; }
            public string Url { get; set; }
            public long LatencyMs { get; set; }
            public string Version { get; set; }
            public bool? SchedulerEnabled { get; set; }
            public bool? Paused { get; set; }
            public bool? Running { get; set; }
            public DateTimeOffset? NextRunAt { get; set; }
            public DateTimeOffset? LastStartedAt { get; set; }
            public DateTimeOffset? LastCompletedAt { get; set; }
            public bool? LastRunSucceeded { get; set; }
            public int? TotalRuns { get; set; }
            public DateTimeOffset? ServerTimeUtc { get; set; }
            public string Error { get; set; }
        }

        // UI Elements
        private TextBlock _lblHealthBadge;
        private TextBlock _lblLastRefreshedValue;
        
        private TextBlock _lblServerValue;
        private TextBlock _lblDbValue;
        private TextBlock _lblWebServerValue;
        private TextBlock _lblSchedulerTaskStateValue;
        private TextBlock _lblSchedulerEnabledValue;
        private TextBlock _lblSchedulerStatusValue;
        private TextBlock _lblSchedulerLastActivityValue;
        private TextBlock _lblSchedulerSignalsValue;
        private TextBlock _lblSchemaValue;
        private TextBlock _lblCountsValue;
        private TextBlock _lblLastEscalationValue;
        private TextBlock _lblVersionValue;
        private TextBlock _lblTrendValue;

        private TextBlock _lblOpenValue;
        private TextBlock _lblPendingValue;
        private TextBlock _lblCriticalValue;
        private TextBlock _lblTodayValue;

        private TextBlock _lblLastEmailFailedValue;
        private TextBox _txtLastEmailFailedDetails;
        private TextBlock _lblLastEmailSkippedValue;
        private TextBox _txtLastEmailSkippedDetails;
        private TextBlock _lblLastReminderEmailValue;
        private TextBlock _lblLastEscalationEmailValue;

        private Button _lnkTemplates;

        private TextBlock _lblJobLastStartValue;
        private TextBlock _lblJobLastEndValue;
        private TextBlock _lblJobNextRunValue;
        private TextBlock _lblJobLockProbeValue;
        private TextBlock _lblJobCountsValue;

        private DataGrid _schemaGrid;
        private StackPanel _topIssuesPanel;

        private Button _btnOpenDashboard;
        private Button _btnConnect;
        private TextBlock _lblHeartbeatLastStartedValue;
        private TextBlock _lblHeartbeatLastFinishedValue;
        private TextBlock _lblHeartbeatLastSuccessValue;
        private TextBlock _lblHeartbeatMachineValue;
        private TextBlock _lblHeartbeatVersionValue;
        private TextBlock _lblLiveStateValue;
        private TextBlock _lblNextRunValue;
        private TextBlock _lblServerUrlValue;
        private TextBlock _lblLatencyValue;
        private TextBlock _lblSchedulerActiveValue;
        private TextBlock _lblLastResultValue;
        private TextBlock _lblServerTimeValue;
        private TextBlock _lblDevicesCountValue;
        private StackPanel _devicesPanel;
        private List<CallClientPresenceItem> _lastConnectedClients;
        
        private Button _btnRefresh;

        public void Initialize(
            ICallMonitoringRepository repo,
            ICallMonitoringNavigator navigator)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _navigator = navigator;
            
            BuildUi();

            // Apply the shared slim scrollbar style
            Resources.MergedDictionaries.Add(WpfThemeResources.GetScrollBarStyle());

            Loaded += async (_, __) => await RefreshAsync();
        }

        private void BuildUi()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = new SolidColorBrush(Color.FromArgb(255, 241, 244, 247))
            };

            var rootGrid = new Grid { Margin = new Thickness(28, 24, 28, 28) };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Hero
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Content (Summary + Email)
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Lower Content (Schema + Scheduler)

            rootGrid.Children.Add(BuildHero(0));

            _lblOpenValue = CreateMetricValue();
            _lblPendingValue = CreateMetricValue();
            _lblCriticalValue = CreateMetricValue();
            _lblTodayValue = CreateMetricValue();



            var contentGrid = new Grid { Margin = new Thickness(0, 22, 0, 0) };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48, GridUnitType.Star) });
            
            var leftContainer = BuildTabContainer(BuildSystemSummaryCard(0), "System Summary", BuildSchemaCard(0), "Health & Schema Checks");
            var rightContainer = BuildTabContainer(BuildEmailSignalsCard(1), "Email / Background", BuildSchedulerStatusCard(1), "Scheduler Status");
            
            var leftWrapper = new Border { Margin = new Thickness(0, 0, 11, 0), Child = leftContainer };
            Grid.SetColumn(leftWrapper, 0);
            contentGrid.Children.Add(leftWrapper);
            
            var rightWrapper = new Border { Margin = new Thickness(11, 0, 0, 0), Child = rightContainer };
            Grid.SetColumn(rightWrapper, 1);
            contentGrid.Children.Add(rightWrapper);
            
            Grid.SetRow(contentGrid, 1);
            Grid.SetRowSpan(contentGrid, 2);
            rootGrid.Children.Add(contentGrid);

            scroll.Content = rootGrid;
            Content = scroll;
        }

        private Border BuildHero(int row)
        {
            var card = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(15, 23, 42),
                    Color.FromRgb(30, 41, 59),
                    new Point(0, 0),
                    new Point(1, 1)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(28),
                Padding = new Thickness(26, 24, 26, 24),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 24,
                    Color = Color.FromArgb(32, 15, 23, 42),
                    ShadowDepth = 0,
                    Opacity = 0.25
                }
            };

            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            card.Child = layout;

            var textStack = new StackPanel();
            textStack.Children.Add(new TextBlock
            {
                Text = "Diagnostics",
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            textStack.Children.Add(new TextBlock
            {
                Text = "IT Call Monitoring • Health & email pipeline status",
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                TextWrapping = TextWrapping.Wrap
            });
            
            var badgeStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            _lblHealthBadge = new TextBlock
            {
                Text = "Loading...",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                Padding = new Thickness(10, 4, 10, 4)
            };
            var badgeBorder = new Border { CornerRadius = new CornerRadius(12), Background = _lblHealthBadge.Background, Child = _lblHealthBadge };
            badgeStack.Children.Add(badgeBorder);

            _lblLastRefreshedValue = new TextBlock
            {
                Text = "Last refreshed: -",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeStack.Children.Add(_lblLastRefreshedValue);
            textStack.Children.Add(badgeStack);
            layout.Children.Add(textStack);

            // ── Action bar ───────────────────────────────────────────────
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            // PRIMARY GROUP: Refresh
            _btnRefresh = CreateHeroButton("↻  Refresh", Color.FromRgb(59, 130, 246), isPrimary: true);
            _btnRefresh.Click += async (_, __) => await RefreshAsync();
            actions.Children.Add(_btnRefresh);

            // PRIMARY GROUP: Run SMTP Test
            var btnSmtp = CreateHeroButton("✉  Run SMTP Test", Color.FromRgb(16, 185, 129), isPrimary: true);
            btnSmtp.Click += (_, __) => _navigator?.OpenSmtpTest();
            actions.Children.Add(btnSmtp);

            // PRIMARY GROUP: Copy
            var btnCopy = CreateHeroButton("⎘  Copy", Color.FromRgb(71, 85, 105), isPrimary: false);
            btnCopy.Click += (_, __) => TryCopyToClipboard(BuildDiagnosticsText(_lastSnapshot, redact: true));
            actions.Children.Add(btnCopy);

            // ── Divider ──────────────────────────────────────────────────
            actions.Children.Add(new Border
            {
                Width = 1,
                Background = new SolidColorBrush(Color.FromArgb(80, 148, 163, 184)),
                Margin = new Thickness(6, 4, 6, 4),
                VerticalAlignment = VerticalAlignment.Stretch
            });

            // SECONDARY GROUP (⋯ More dropdown): Export TXT, Export JSON, Open Logs, Test Ticket, Cleanup Tests
            var morePopup = new Popup
            {
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true
            };

            var moreMenu = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(6),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 20,
                    Color = Colors.Black,
                    ShadowDepth = 0,
                    Opacity = 0.45
                }
            };

            var moreStack = new StackPanel { MinWidth = 180 };

            var menuExportTxt = CreateDropdownMenuItem("💾  Export TXT", Color.FromRgb(148, 163, 184));
            menuExportTxt.Click += (_, __) => { morePopup.IsOpen = false; ExportDiagnosticsText(_lastSnapshot); };
            moreStack.Children.Add(menuExportTxt);

            var menuExportJson = CreateDropdownMenuItem("🗂  Export JSON", Color.FromRgb(148, 163, 184));
            menuExportJson.Click += (_, __) => { morePopup.IsOpen = false; ExportDiagnosticsJson(_lastSnapshot); };
            moreStack.Children.Add(menuExportJson);

            var menuOpenLogs = CreateDropdownMenuItem("📁  Open Logs", Color.FromRgb(148, 163, 184));
            menuOpenLogs.Click += (_, __) => { morePopup.IsOpen = false; TryOpenLogFolder(); };
            moreStack.Children.Add(menuOpenLogs);

            // Divider inside dropdown
            moreStack.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromArgb(60, 148, 163, 184)),
                Margin = new Thickness(6, 4, 6, 4)
            });

            // Warning-style actions
            var menuTestTicket = CreateDropdownMenuItem("⚠  Test Ticket", Color.FromRgb(251, 191, 36));
            menuTestTicket.Click += async (_, __) => { morePopup.IsOpen = false; await CreateTestTicketAsync(); };
            moreStack.Children.Add(menuTestTicket);

            var menuCleanup = CreateDropdownMenuItem("⚠  Cleanup Tests", Color.FromRgb(251, 191, 36));
            menuCleanup.Click += async (_, __) => { morePopup.IsOpen = false; await CleanupTestTicketsAsync(); };
            moreStack.Children.Add(menuCleanup);

            moreMenu.Child = moreStack;
            morePopup.Child = moreMenu;

            var btnMore = CreateHeroButton("⋯  More", Color.FromRgb(71, 85, 105), isPrimary: false);
            btnMore.Margin = new Thickness(0);
            btnMore.Click += (s, __) =>
            {
                morePopup.PlacementTarget = btnMore;
                morePopup.IsOpen = !morePopup.IsOpen;
            };
            actions.Children.Add(btnMore);

            Grid.SetColumn(actions, 1);
            layout.Children.Add(actions);

            Grid.SetRow(card, row);
            return card;
        }

        private static Button CreateHeroButton(string label, Color bg, bool isPrimary)
        {
            var btn = new Button
            {
                Content = label,
                MinWidth = 110,
                Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 12.5,
                FontWeight = isPrimary ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(bg),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            return btn;
        }

        private static Button CreateDropdownMenuItem(string label, Color fg)
        {
            var btn = new Button
            {
                Content = label,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(14, 9, 14, 9),
                Margin = new Thickness(0, 1, 0, 1),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(fg),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            return btn;
        }

        private Border BuildSystemSummaryCard(int col)
        {
            var card = CreateGlassCard();

            var stack = new StackPanel();
            stack.Children.Add(CreateCardHeader("System Summary", Color.FromRgb(59, 130, 246)));

            var grid = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for(int i=0; i<10; i++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });

            _lblServerValue = CreateValueText();
            _lblDbValue = CreateValueText();
            _lblWebServerValue = CreateValueText();
            _lblSchedulerTaskStateValue = CreateValueText();
            _lblSchedulerEnabledValue = CreateValueText();
            _lblSchedulerStatusValue = CreateValueText();
            _lblSchedulerLastActivityValue = CreateValueText();
            _lblSchedulerSignalsValue = CreateValueText();
            _lblSchemaValue = CreateValueText();
            _lblCountsValue = CreateValueText();
            _lblLastEscalationValue = CreateValueText();
            _lblVersionValue = CreateValueText();
            _lblTrendValue = CreateValueText();

            grid.RowDefinitions.Clear();
            for(int i=0; i<13; i++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });

            AddRow(grid, 0, "Server:", _lblServerValue);
            AddRow(grid, 1, "Database:", _lblDbValue);
            AddRow(grid, 2, "Web Server:", _lblWebServerValue);
            AddRow(grid, 3, "Task Scheduler:", _lblSchedulerTaskStateValue);
            AddRow(grid, 4, "Scheduler Enabled:", _lblSchedulerEnabledValue);
            AddRow(grid, 5, "Processing:", _lblSchedulerStatusValue);
            AddRow(grid, 6, "Last activity:", _lblSchedulerLastActivityValue);
            AddRow(grid, 7, "Signals (24h):", _lblSchedulerSignalsValue);
            AddRow(grid, 8, "Schema:", _lblSchemaValue);
            AddRow(grid, 9, "Avg resolve:", _lblCountsValue);
            AddRow(grid, 10, "Last escalation:", _lblLastEscalationValue);
            AddRow(grid, 11, "Versions:", _lblVersionValue);
            AddRow(grid, 12, "14-day trend:", _lblTrendValue);

            stack.Children.Add(grid);
            
            var metricGrid = new Grid { Margin = new Thickness(0, 24, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AddMetricPill(metricGrid, 0, "Open", _lblOpenValue, Color.FromRgb(59, 130, 246));
            AddMetricPill(metricGrid, 1, "Pending", _lblPendingValue, Color.FromRgb(245, 158, 11));
            AddMetricPill(metricGrid, 2, "Critical", _lblCriticalValue, Color.FromRgb(239, 68, 68));
            AddMetricPill(metricGrid, 3, "Today", _lblTodayValue, Color.FromRgb(16, 185, 129));
            stack.Children.Add(metricGrid);
            


            stack.Children.Add(new TextBlock
            {
                Text = "Tip: Copy and paste this into support tickets for faster triage.",
                Margin = new Thickness(0, 16, 0, 0),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            });

            card.Child = stack;
            return card;
        }

        private Border BuildEmailSignalsCard(int col)
        {
            var card = CreateGlassCard();

            var stack = new StackPanel();
            stack.Children.Add(CreateCardHeader("Email / Background Signals", Color.FromRgb(16, 185, 129)));

            var jobGrid = new Grid { Margin = new Thickness(0, 16, 0, 16) };
            jobGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            jobGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for(int i=0; i<5; i++) jobGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });

            _lblJobLastStartValue = CreateValueText();
            _lblJobLastEndValue = CreateValueText();
            _lblJobNextRunValue = CreateValueText();
            _lblJobLockProbeValue = CreateValueText();
            _lblJobCountsValue = CreateValueText();

            AddRow(jobGrid, 0, "Last run start:", _lblJobLastStartValue);
            AddRow(jobGrid, 1, "Last run end:", _lblJobLastEndValue);
            AddRow(jobGrid, 2, "Next run:", _lblJobNextRunValue);
            AddRow(jobGrid, 3, "Lock (probe):", _lblJobLockProbeValue);
            AddRow(jobGrid, 4, "Counts:", _lblJobCountsValue);
            stack.Children.Add(jobGrid);

            // Quick Links
            stack.Children.Add(CreateKeyLabel("Quick links:"));
            var linksPanel = new WrapPanel { Margin = new Thickness(0, 6, 0, 16) };
            
            var btnSettings = CreateLinkBtn("Email Settings"); btnSettings.Click += (_,__) => _navigator?.OpenEmailDeepLink(WpfEmailNotificationDeepLinkTarget.SetupSmtp, null, null, null);
            var btnRules = CreateLinkBtn("Notification Rules"); btnRules.Click += (_,__) => _navigator?.OpenEmailDeepLink(WpfEmailNotificationDeepLinkTarget.SetupNotificationRules, null, null, null);
            _lnkTemplates = CreateLinkBtn("Templates"); _lnkTemplates.Click += (_,__) => OpenTemplatesDeepLink();
            var btnDept = CreateLinkBtn("Dept Recipients"); btnDept.Click += (_,__) => _navigator?.OpenEmailDeepLink(WpfEmailNotificationDeepLinkTarget.DepartmentRecipients, _lastResolvedDeptId, null, null);
            var btnBranch = CreateLinkBtn("Branch Recipients"); btnBranch.Click += (_,__) => _navigator?.OpenEmailDeepLink(WpfEmailNotificationDeepLinkTarget.BranchRecipients, null, _lastResolvedBranchId, null);
            var btnEmailLog = CreateLinkBtn("Email Log"); btnEmailLog.Click += (_,__) => _navigator?.OpenEmailDeepLink(WpfEmailNotificationDeepLinkTarget.EmailLog, null, null, _lastResolvedTicketId?.ToString());

            linksPanel.Children.Add(btnSettings);
            linksPanel.Children.Add(btnRules);
            linksPanel.Children.Add(_lnkTemplates);
            linksPanel.Children.Add(btnDept);
            linksPanel.Children.Add(btnBranch);
            linksPanel.Children.Add(btnEmailLog);
            stack.Children.Add(linksPanel);

            // Email status
            _lblLastEmailFailedValue = CreateValueText();
            _txtLastEmailFailedDetails = CreateTextBoxMulti();
            _lblLastEmailSkippedValue = CreateValueText();
            _txtLastEmailSkippedDetails = CreateTextBoxMulti();
            _lblLastReminderEmailValue = CreateValueText();
            _lblLastEscalationEmailValue = CreateValueText();

            stack.Children.Add(CreateKeyLabel("Last FAILED email:"));
            stack.Children.Add(_lblLastEmailFailedValue);
            stack.Children.Add(_txtLastEmailFailedDetails);

            stack.Children.Add(CreateKeyLabel("Last SKIPPED email:"));
            stack.Children.Add(_lblLastEmailSkippedValue);
            stack.Children.Add(_txtLastEmailSkippedDetails);

            stack.Children.Add(CreateKeyLabel("Last reminder email:"));
            stack.Children.Add(_lblLastReminderEmailValue);
            stack.Children.Add(CreateKeyLabel("Last escalation email:"));
            stack.Children.Add(_lblLastEscalationEmailValue);

            card.Child = stack;
            return card;
        }

        private Border BuildSchemaCard(int col)
        {
            var card = CreateGlassCard();

            var stack = new StackPanel();
            stack.Children.Add(CreateCardHeader("Health & Schema Checks", Color.FromRgb(168, 85, 247)));

            _schemaGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.White,
                RowBackground = Brushes.White,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 16, 0, 0),
                Height = 350,
                EnableRowVirtualization = true,
                EnableColumnVirtualization = true
            };
            VirtualizingPanel.SetIsVirtualizing(_schemaGrid, true);
            VirtualizingPanel.SetVirtualizationMode(_schemaGrid, VirtualizationMode.Recycling);
            ScrollViewer.SetCanContentScroll(_schemaGrid, true);

            _schemaGrid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader));
            _schemaGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(248, 250, 252))));
            _schemaGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(71, 85, 105))));
            _schemaGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            _schemaGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 8, 10, 8)));
            _schemaGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(226, 232, 240))));
            _schemaGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));

            _schemaGrid.RowStyle = new Style(typeof(DataGridRow));
            _schemaGrid.RowStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12d));
            _schemaGrid.RowStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(241, 245, 249))));
            _schemaGrid.RowStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            _schemaGrid.RowStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0, 4, 0, 4)));
            var criticalTrigger = new DataTrigger { Binding = new System.Windows.Data.Binding("Severity"), Value = "Critical" };
            criticalTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(254, 226, 226))));
            criticalTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(153, 27, 27))));
            _schemaGrid.RowStyle.Triggers.Add(criticalTrigger);
            var warningTrigger = new DataTrigger { Binding = new System.Windows.Data.Binding("Severity"), Value = "Warning" };
            warningTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(254, 243, 199))));
            warningTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(180, 83, 9))));
            _schemaGrid.RowStyle.Triggers.Add(warningTrigger);

            var wrapTextStyle = new Style(typeof(TextBlock));
            wrapTextStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            wrapTextStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));

            _schemaGrid.Columns.Add(new DataGridTextColumn { Header = "Area", Binding = new System.Windows.Data.Binding("Area"), Width = 100, ElementStyle = wrapTextStyle });
            _schemaGrid.Columns.Add(new DataGridTextColumn { Header = "Object", Binding = new System.Windows.Data.Binding("ObjectName"), Width = 180, ElementStyle = wrapTextStyle });
            _schemaGrid.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new System.Windows.Data.Binding("StatusText"), Width = 90, ElementStyle = wrapTextStyle });
            _schemaGrid.Columns.Add(new DataGridTextColumn { Header = "Details", Binding = new System.Windows.Data.Binding("Detail"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = wrapTextStyle });
            _schemaGrid.Columns.Add(new DataGridTextColumn { Header = "Suggested Fix", Binding = new System.Windows.Data.Binding("SuggestedFix"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = wrapTextStyle });

            // Top Issues summary widget
            _topIssuesPanel = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            stack.Children.Add(_topIssuesPanel);

            stack.Children.Add(_schemaGrid);

            card.Child = stack;
            return card;
        }

        private Border BuildSchedulerStatusCard(int col)
        {
            var card = CreateGlassCard();

            var stack = new StackPanel();
            stack.Children.Add(CreateCardHeader("Scheduler Status", Color.FromRgb(249, 115, 22)));

            // Connect + Open Web Dashboard buttons
            _btnConnect = CreateSecondaryButton("Connect");
            _btnConnect.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
            _btnConnect.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231));
            _btnConnect.BorderBrush = new SolidColorBrush(Color.FromRgb(134, 239, 172));
            _btnConnect.ToolTip = "Test the connection to the ITCM web server.";
            _btnConnect.Click += async (_, __) => await ConnectToServerOnDemandAsync();

            _btnOpenDashboard = CreateSecondaryButton("Open Web Dashboard");
            _btnOpenDashboard.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            _btnOpenDashboard.Background = new SolidColorBrush(Color.FromRgb(219, 234, 254));
            _btnOpenDashboard.BorderBrush = new SolidColorBrush(Color.FromRgb(147, 197, 253));
            _btnOpenDashboard.Click += (_, __) => OpenWebDashboard();

            var serverButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 16) };
            serverButtons.Children.Add(_btnConnect);
            serverButtons.Children.Add(_btnOpenDashboard);
            stack.Children.Add(serverButtons);

            // Heartbeat status grid
            _lblHeartbeatLastStartedValue = CreateValueText();
            _lblHeartbeatLastFinishedValue = CreateValueText();
            _lblHeartbeatLastSuccessValue = CreateValueText();
            _lblHeartbeatMachineValue = CreateValueText();
            _lblHeartbeatVersionValue = CreateValueText();
            _lblLiveStateValue = CreateValueText();
            _lblNextRunValue = CreateValueText();
            _lblServerUrlValue = CreateValueText();
            _lblLatencyValue = CreateValueText();
            _lblSchedulerActiveValue = CreateValueText();
            _lblLastResultValue = CreateValueText();
            _lblServerTimeValue = CreateValueText();

            var grid = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 12; i++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });

            AddRow(grid, 0, "Last started:", _lblHeartbeatLastStartedValue);
            AddRow(grid, 1, "Last finished:", _lblHeartbeatLastFinishedValue);
            AddRow(grid, 2, "Last success:", _lblHeartbeatLastSuccessValue);
            AddRow(grid, 3, "Machine:", _lblHeartbeatMachineValue);
            AddRow(grid, 4, "Version:", _lblHeartbeatVersionValue);
            AddRow(grid, 5, "Live state:", _lblLiveStateValue);
            AddRow(grid, 6, "Next run:", _lblNextRunValue);
            AddRow(grid, 7, "Server URL:", _lblServerUrlValue);
            AddRow(grid, 8, "Latency:", _lblLatencyValue);
            AddRow(grid, 9, "Scheduler:", _lblSchedulerActiveValue);
            AddRow(grid, 10, "Last result:", _lblLastResultValue);
            AddRow(grid, 11, "Server time:", _lblServerTimeValue);
            stack.Children.Add(grid);

            // Connected devices (desktops calling the ITCM server)
            _lblDevicesCountValue = CreateValueText();
            _devicesPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            var devicesHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
            var devicesTitle = CreateKeyLabel("Connected devices:");
            devicesTitle.Margin = new Thickness(0, 4, 8, 4);
            devicesHeader.Children.Add(devicesTitle);
            devicesHeader.Children.Add(_lblDevicesCountValue);
            stack.Children.Add(devicesHeader);
            stack.Children.Add(_devicesPanel);

            var tipBlock = new TextBlock
            {
                Text = "The ITCM Scheduler runs as a centralized web service. Use the dashboard above to view run history, settings, and logs.",
                Margin = new Thickness(0, 16, 0, 0),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(tipBlock);

            card.Child = stack;
            return card;
        }

        // Helpers

        private UIElement BuildTabContainer(Border card1, string tab1Text, Border card2, string tab2Text)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            
            var btnTab1 = new Button
            {
                Content = tab1Text,
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            
            var btnTab2 = new Button
            {
                Content = tab2Text,
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            headerPanel.Children.Add(btnTab1);
            headerPanel.Children.Add(btnTab2);
            Grid.SetRow(headerPanel, 0);
            root.Children.Add(headerPanel);

            var contentGrid = new Grid();
            contentGrid.Children.Add(card2);
            contentGrid.Children.Add(card1);
            Grid.SetRow(contentGrid, 1);
            root.Children.Add(contentGrid);

            Action<bool> setActiveTab = (showTab1) =>
            {
                card1.Visibility = showTab1 ? Visibility.Visible : Visibility.Collapsed;
                card2.Visibility = showTab1 ? Visibility.Collapsed : Visibility.Visible;
                
                btnTab1.Background = new SolidColorBrush(showTab1 ? Color.FromRgb(59, 130, 246) : Color.FromRgb(241, 245, 249));
                btnTab1.Foreground = showTab1 ? Brushes.White : new SolidColorBrush(Color.FromRgb(100, 116, 139));
                
                btnTab2.Background = new SolidColorBrush(!showTab1 ? Color.FromRgb(59, 130, 246) : Color.FromRgb(241, 245, 249));
                btnTab2.Foreground = !showTab1 ? Brushes.White : new SolidColorBrush(Color.FromRgb(100, 116, 139));
            };

            btnTab1.Click += (_, __) => setActiveTab(true);
            btnTab2.Click += (_, __) => setActiveTab(false);

            setActiveTab(true);
            
            return root;
        }

        private void AddRow(Grid grid, int row, string label, UIElement value)
        {
            var lbl = CreateKeyLabel(label);
            Grid.SetRow(lbl, row);
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            Grid.SetRow(value, row);
            Grid.SetColumn(value, 1);
            grid.Children.Add(value);

            if (row < grid.RowDefinitions.Count - 1)
            {
                var divider = new Border { BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = new SolidColorBrush(Color.FromRgb(241, 245, 249)), Margin = new Thickness(0, 0, 0, -2), VerticalAlignment = VerticalAlignment.Bottom };
                Grid.SetRow(divider, row);
                Grid.SetColumnSpan(divider, 2);
                grid.Children.Add(divider);
            }
        }

        private TextBlock CreateKeyLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4)
            };
        }

        private TextBlock CreateValueText()
        {
            return new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 4)
            };
        }

        private TextBox CreateTextBoxMulti()
        {
            return new TextBox
            {
                Height = 60,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11.5,
                Margin = new Thickness(0, 4, 0, 12)
            };
        }

        private Button CreatePrimaryButton(string text, Color bg)
        {
            return new Button
            {
                Content = text,
                Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(0, 0, 10, 0),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(bg),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
        }

        private Button CreateSecondaryButton(string text)
        {
            return new Button
            {
                Content = text,
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
        }

        private Button CreateLinkBtn(string text)
        {
            return new Button
            {
                Content = text,
                Margin = new Thickness(0, 0, 12, 4),
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(0)
            };
        }

        private Border CreateGlassCard()
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(248, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(223, 229, 236)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(24),
                Padding = new Thickness(22),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 18,
                    Color = Color.FromArgb(28, 15, 23, 42),
                    ShadowDepth = 0,
                    Opacity = 0.2
                }
            };
        }

        private StackPanel CreateCardHeader(string title, Color accent)
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new Border
            {
                Width = 4,
                Height = 24,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(accent),
                Margin = new Thickness(0, 0, 12, 0)
            });
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                VerticalAlignment = VerticalAlignment.Center
            });
            return stack;
        }

        private TextBlock CreateMetricValue()
        {
            return new TextBlock
            {
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            };
        }

        private void AddMetricPill(Grid parent, int column, string title, TextBlock valueBlock, Color accentColor)
        {
            double leftMargin = column > 0 ? 16 : 0;
            var pill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(248, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(223, 229, 236)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(leftMargin, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 12,
                    Color = Color.FromArgb(25, 15, 23, 42),
                    ShadowDepth = 0,
                    Opacity = 0.15
                }
            };

            var accentBar = new Border
            {
                Width = 5,
                Height = 44,
                CornerRadius = new CornerRadius(999),
                Background = new SolidColorBrush(accentColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            });
            valueBlock.FontSize = 32;
            valueBlock.FontWeight = FontWeights.Bold;
            valueBlock.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            valueBlock.Margin = new Thickness(0, 2, 0, 0);
            textStack.Children.Add(valueBlock);

            var innerGrid = new Grid();
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            Grid.SetColumn(accentBar, 0);
            Grid.SetColumn(textStack, 1);
            innerGrid.Children.Add(accentBar);
            innerGrid.Children.Add(textStack);
            
            pill.Child = innerGrid;
            Grid.SetColumn(pill, column);
            parent.Children.Add(pill);
        }

        // Logic

        private async Task RefreshAsync()
        {
            if (_btnRefresh != null) _btnRefresh.IsEnabled = false;
            try
            {
                // Phase 1: core snapshot (scheduler probe runs in parallel with DB inside).
                // The web-server ping runs alongside so a down server never blocks refresh.
                var snapTask = _repo.GetDiagnosticsSnapshotAsync();
                var pingTask = ProbeItcmServerAsync();
                var clientsTask = _repo.GetConnectedClientsAsync(15);
                var snap = await snapTask;
                _lastSnapshot = snap;
                ApplySnapshotToUi(snap);

                try
                {
                    var ping = await pingTask;
                    _lastServerPing = ping;
                    _lastServerPingAtUtc = DateTime.UtcNow;
                    ApplyServerPingToUi(ping, snap);
                }
                catch { }

                try
                {
                    var clients = await clientsTask;
                    _lastConnectedClients = clients;
                    ApplyConnectedClientsToUi(clients);
                }
                catch { }

                try { await ApplyEnhancedSignalsAsync(snap); }
                catch { }

                if (_lblLastRefreshedValue != null)
                    _lblLastRefreshedValue.Text = "Last refreshed: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // Phase 2: load 14-day trends in background so first paint is not blocked
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var trends = await _repo.LoadSnapshotTrendsAsync(snap);
                        snap.Trends = trends;
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (_lastSnapshot == snap && _lblTrendValue != null)
                                _lblTrendValue.Text = FormatTrendSummary(snap);
                        });
                    }
                    catch { /* trends are non-critical */ }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("ITCM Diagnostics refresh failed.", ex);
                WpfItcmDialogService.ShowWarning(this, "Diagnostics failed to load.\n\n" + ex.Message, "Diagnostics");
            }
            finally
            {
                if (_btnRefresh != null) _btnRefresh.IsEnabled = true;
            }
        }

        private async Task ConnectToServerOnDemandAsync()
        {
            // Already connected and fresh (5 min): just show Connected, no network call.
            if (_lastServerPing != null && _lastServerPing.Connected
                && (DateTime.UtcNow - _lastServerPingAtUtc) < TimeSpan.FromMinutes(5))
            {
                ApplyServerPingToUi(_lastServerPing, _lastSnapshot);
                return;
            }

            if (_btnConnect != null) _btnConnect.IsEnabled = false;
            try
            {
                var ping = await ProbeItcmServerAsync();
                _lastServerPing = ping;
                _lastServerPingAtUtc = DateTime.UtcNow;
                ApplyServerPingToUi(ping, _lastSnapshot);
            }
            catch { }
            finally
            {
                if (_btnConnect != null) _btnConnect.IsEnabled = true;
            }
        }

        private async Task<ItcmServerPing> ProbeItcmServerAsync()
        {            var url = (AppConfig.ItcmServerUrl ?? string.Empty).Trim().TrimEnd('/');
            var ping = new ItcmServerPing { Connected = false, Url = url };
            if (string.IsNullOrWhiteSpace(url))
            {
                ping.Error = "ItcmServerUrl is not configured";
                return ping;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                using (var response = await _itcmPingClient.GetAsync(url + "/api/itcm/ping"))
                {
                    sw.Stop();
                    ping.LatencyMs = sw.ElapsedMilliseconds;
                    if (!response.IsSuccessStatusCode)
                    {
                        ping.Error = "HTTP " + (int)response.StatusCode + " " + response.ReasonPhrase;
                        return ping;
                    }

                    var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                    var sched = json["scheduler"];
                    ping.Connected = true;
                    ping.Version = ((string)json["version"] ?? string.Empty).Trim();
                    ping.SchedulerEnabled = (bool?)sched?["enabled"];
                    ping.Paused = (bool?)sched?["paused"];
                    ping.Running = (bool?)sched?["running"];
                    ping.NextRunAt = (DateTimeOffset?)sched?["nextRunAt"];
                    ping.LastStartedAt = (DateTimeOffset?)sched?["lastStartedAt"];
                    ping.LastCompletedAt = (DateTimeOffset?)sched?["lastCompletedAt"];
                    ping.LastRunSucceeded = (bool?)sched?["lastRunSucceeded"];
                    ping.TotalRuns = (int?)sched?["totalRuns"];
                    ping.ServerTimeUtc = (DateTimeOffset?)json["timeUtc"];
                    return ping;
                }
            }
            catch (TaskCanceledException)
            {
                sw.Stop();
                ping.LatencyMs = sw.ElapsedMilliseconds;
                ping.Error = "Timed out after 5s";
                return ping;
            }
            catch (Exception ex)
            {
                sw.Stop();
                ping.LatencyMs = sw.ElapsedMilliseconds;
                var msg = (ex.GetBaseException().Message ?? ex.Message ?? "unreachable").Trim();
                if (msg.Length > 90) msg = msg.Substring(0, 90) + "...";
                ping.Error = msg;
                return ping;
            }
        }

        private void ApplyServerPingToUi(ItcmServerPing ping, CallMonitoringRepository.ItcmDiagnosticsSnapshot snap)
        {
            if (ping == null) return;

            // The button itself shows the state: Connected while live, Connect otherwise.
            if (_btnConnect != null)
                _btnConnect.Content = ping.Connected ? "Connected" : "Connect";

            if (ping.Connected)
            {
                var detail = string.IsNullOrWhiteSpace(ping.Version) ? "" : " • v" + ping.Version;
                _lblWebServerValue.Text = $"Connected ({ping.LatencyMs}ms){detail}";
                _lblWebServerValue.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
                _lblWebServerValue.ToolTip = ping.Url;

                string live;
                if (ping.Running == true) live = "● RUNNING";
                else if (ping.Paused == true) live = "❚❚ PAUSED";
                else if (ping.SchedulerEnabled == false) live = "○ DISABLED";
                else live = "● READY";
                if (ping.TotalRuns.HasValue) live += $" • {ping.TotalRuns.Value} run(s)";
                _lblLiveStateValue.Text = live;
                _lblLiveStateValue.Foreground = new SolidColorBrush(
                    ping.Paused == true || ping.SchedulerEnabled == false
                        ? Color.FromRgb(245, 158, 11)
                        : Color.FromRgb(39, 174, 96));
                _lblNextRunValue.Text = ping.NextRunAt.HasValue
                    ? ping.NextRunAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                    : "-";
                _lblServerUrlValue.Text = string.IsNullOrWhiteSpace(ping.Url) ? "-" : ping.Url;
                _lblServerUrlValue.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                _lblLatencyValue.Text = ping.LatencyMs + "ms";

                string schedText;
                Color schedColor;
                if (ping.SchedulerEnabled == false)
                {
                    schedText = "DISABLED in server settings";
                    schedColor = Color.FromRgb(192, 57, 43);
                }
                else if (ping.Paused == true)
                {
                    schedText = "ENABLED • paused";
                    schedColor = Color.FromRgb(245, 158, 11);
                }
                else if (ping.SchedulerEnabled == true)
                {
                    schedText = "ENABLED • automatic";
                    schedColor = Color.FromRgb(39, 174, 96);
                }
                else
                {
                    schedText = "-";
                    schedColor = Color.FromRgb(120, 130, 140);
                }
                _lblSchedulerActiveValue.Text = schedText;
                _lblSchedulerActiveValue.Foreground = new SolidColorBrush(schedColor);

                if (ping.LastRunSucceeded == true)
                {
                    _lblLastResultValue.Text = "Succeeded";
                    _lblLastResultValue.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
                }
                else if (ping.LastRunSucceeded == false)
                {
                    _lblLastResultValue.Text = "Failed (see server logs)";
                    _lblLastResultValue.Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43));
                }
                else
                {
                    _lblLastResultValue.Text = ping.TotalRuns > 0 ? "No runs yet" : "-";
                    _lblLastResultValue.Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 140));
                }

                _lblServerTimeValue.Text = ping.ServerTimeUtc.HasValue
                    ? ping.ServerTimeUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                    : "-";

                // Prefer the live server version over the DB-derived one.
                if (!string.IsNullOrWhiteSpace(ping.Version) && snap?.VersionInfo != null)
                    _lblVersionValue.Text = $"Desktop {NullDash(snap.VersionInfo.DesktopVersion)} • Scheduler {ping.Version} • DB {NullDash(snap.VersionInfo.DatabaseMigrationVersion)}";
            }
            else
            {
                _lblWebServerValue.Text = $"Unreachable • {ping.Error}";
                _lblWebServerValue.Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43));
                _lblWebServerValue.ToolTip = ping.Url;
                _lblLiveStateValue.Text = "(unknown — server unreachable)";
                _lblLiveStateValue.Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 140));
                _lblNextRunValue.Text = "-";
                _lblServerUrlValue.Text = string.IsNullOrWhiteSpace(ping.Url) ? "-" : ping.Url;
                _lblServerUrlValue.Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 140));
                _lblLatencyValue.Text = "-";
                _lblSchedulerActiveValue.Text = "-";
                _lblSchedulerActiveValue.Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 140));
                _lblLastResultValue.Text = "-";
                _lblLastResultValue.Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 140));
                _lblServerTimeValue.Text = "-";

                // Downgrade an otherwise-healthy badge so a down server is visible
                // at a glance. More severe states (schema/email) keep precedence.
                if (_lblHealthBadge != null && (_lblHealthBadge.Text ?? "").StartsWith("Healthy"))
                {
                    _lblHealthBadge.Text = "Web server unreachable";
                    _lblHealthBadge.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
                    _lblHealthBadge.Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9));
                    ((Border)_lblHealthBadge.Parent).Background = _lblHealthBadge.Background;
                }
            }
        }

        private void ApplyConnectedClientsToUi(List<CallClientPresenceItem> clients)
        {
            if (_devicesPanel == null) return;
            _devicesPanel.Children.Clear();

            var list = (clients ?? new List<CallClientPresenceItem>())
                .OrderByDescending(c => c.Online)
                .ThenByDescending(c => c.LastSeenUtc)
                .Take(8)
                .ToList();
            var online = (clients ?? new List<CallClientPresenceItem>()).Count(c => c.Online);

            if (_lblDevicesCountValue != null)
            {
                _lblDevicesCountValue.Text = online + " online";
                _lblDevicesCountValue.Foreground = new SolidColorBrush(
                    online > 0 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(120, 130, 140));
            }

            if (list.Count == 0)
            {
                _devicesPanel.Children.Add(new TextBlock
                {
                    Text = "(none seen yet)",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Margin = new Thickness(0, 2, 0, 2)
                });
                return;
            }

            string selfMachine;
            try { selfMachine = (Environment.MachineName ?? string.Empty).Trim(); }
            catch { selfMachine = string.Empty; }

            foreach (var c in list)
            {
                var seen = ToSeenAgo(c.LastSeenUtc);
                var self = !string.IsNullOrWhiteSpace(selfMachine)
                    && string.Equals((c.MachineName ?? string.Empty).Trim(), selfMachine, StringComparison.OrdinalIgnoreCase);
                var dot = c.Online ? "● " : "○ ";
                _devicesPanel.Children.Add(new TextBlock
                {
                    Text = dot + (c.MachineName ?? "-").Trim()
                        + " • " + (c.UserName ?? "-").Trim()
                        + " • " + seen + (self ? " (this device)" : ""),
                    FontSize = 12,
                    FontWeight = self ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(c.Online
                        ? Color.FromRgb(39, 174, 96)
                        : Color.FromRgb(120, 130, 140)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 2)
                });
            }
        }

        private static string ToSeenAgo(DateTime lastSeenUtc)
        {
            DateTime utc;
            try
            {
                utc = lastSeenUtc.Kind == DateTimeKind.Utc
                    ? lastSeenUtc
                    : DateTime.SpecifyKind(lastSeenUtc, DateTimeKind.Utc);
                var age = DateTime.UtcNow - utc;
                if (age.TotalSeconds < 0) return "just now";
                if (age.TotalMinutes < 1) return ((int)age.TotalSeconds) + "s ago";
                if (age.TotalHours < 1) return ((int)age.TotalMinutes) + " min ago";
                return ToLocalDisplayStatic(utc);
            }
            catch
            {
                return "-";
            }
        }

        private static string ToLocalDisplayStatic(DateTime utcFromDb)
        {
            var utc = utcFromDb.Kind == DateTimeKind.Utc ? utcFromDb : DateTime.SpecifyKind(utcFromDb, DateTimeKind.Utc);
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        private void ApplySnapshotToUi(CallMonitoringRepository.ItcmDiagnosticsSnapshot snap)
        {
            if (snap == null) return;

            _lblServerValue.Text = string.IsNullOrWhiteSpace(snap.ServerName) ? "-" : snap.ServerName;
            _lblDbValue.Text = string.IsNullOrWhiteSpace(snap.DatabaseName) ? "-" : snap.DatabaseName;

            var taskStateText = snap.SchedulerTaskState;
            if (snap.SchedulerTaskEnabled == true && snap.SchedulerTaskNextRunLocal.HasValue)
                taskStateText = $"{taskStateText} • next {snap.SchedulerTaskNextRunLocal.Value:yyyy-MM-dd HH:mm:ss}";
            else if (snap.SchedulerTaskEnabled == false)
                taskStateText = string.IsNullOrWhiteSpace(taskStateText) ? "DISABLED" : taskStateText;

            _lblSchedulerTaskStateValue.Text = string.IsNullOrWhiteSpace(taskStateText) ? "UNKNOWN" : taskStateText;
            _lblSchedulerTaskStateValue.Foreground = new SolidColorBrush(snap.SchedulerTaskEnabled == true ? Color.FromRgb(39, 174, 96) : snap.SchedulerTaskEnabled == false ? Color.FromRgb(192, 57, 43) : Color.FromRgb(120, 130, 140));

            if (snap.SchedulerTaskEnabled == true)
            {
                _lblSchedulerEnabledValue.Text = "YES";
                _lblSchedulerEnabledValue.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
            }
            else if (snap.SchedulerTaskEnabled == false)
            {
                _lblSchedulerEnabledValue.Text = "NO";
                _lblSchedulerEnabledValue.Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43));
            }
            else
            {
                _lblSchedulerEnabledValue.Text = "UNKNOWN";
                _lblSchedulerEnabledValue.Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 140));
            }

            if (snap.SchedulerIsRunning == true)
            {
                _lblSchedulerStatusValue.Text = "● RUNNING";
                _lblSchedulerStatusValue.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
            }
            else if (snap.SchedulerIsRunning == false)
            {
                _lblSchedulerStatusValue.Text = "● IDLE";
                _lblSchedulerStatusValue.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            }
            else
            {
                _lblSchedulerStatusValue.Text = "● UNKNOWN";
                _lblSchedulerStatusValue.Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 140));
            }

            _lblSchedulerLastActivityValue.Text = snap.SchedulerLastActivityUtc.HasValue ? ToLocalDisplay(snap.SchedulerLastActivityUtc.Value) : "(none)";
            _lblSchedulerSignalsValue.Text = snap.SchedulerSignalCountLast24Hours.ToString();

            // Heartbeat
            var hb = snap.SchedulerHeartbeat;
            if (hb != null && hb.TableExists)
            {
                _lblHeartbeatLastStartedValue.Text = hb.LastStartedUtc.HasValue ? ToLocalDisplay(hb.LastStartedUtc.Value) : "(none)";
                _lblHeartbeatLastFinishedValue.Text = hb.LastFinishedUtc.HasValue ? ToLocalDisplay(hb.LastFinishedUtc.Value) : "(none)";
                _lblHeartbeatLastSuccessValue.Text = hb.LastSuccessUtc.HasValue ? ToLocalDisplay(hb.LastSuccessUtc.Value) : "(none)";
                _lblHeartbeatMachineValue.Text = string.IsNullOrWhiteSpace(hb.MachineName) ? "-" : hb.MachineName;
                _lblHeartbeatVersionValue.Text = string.IsNullOrWhiteSpace(hb.Version) ? "-" : hb.Version;
            }
            else
            {
                _lblHeartbeatLastStartedValue.Text = hb == null ? "Heartbeat table not found" : "No heartbeat rows yet";
                _lblHeartbeatLastFinishedValue.Text = "-";
                _lblHeartbeatLastSuccessValue.Text = "-";
                _lblHeartbeatMachineValue.Text = "-";
                _lblHeartbeatVersionValue.Text = "-";
            }

            var coreOk = snap.SchemaChecks.Any(c => c.Area == "Core" && c.ObjectName == "dbo.CallTicket" && c.Exists);
            var emailOk = snap.SchemaChecks.Any(c => c.Area == "Email" && c.ObjectName == "dbo.CallEmailSettings" && c.Exists) && snap.SchemaChecks.Any(c => c.Area == "Email" && c.ObjectName == "dbo.CallEmailTemplate" && c.Exists);

            _lblSchemaValue.Text = coreOk ? (emailOk ? "OK (Core + Email)" : "OK (Core) / Email partial") : "Missing core schema";
            _lblSchemaValue.Foreground = new SolidColorBrush(coreOk ? Color.FromRgb(39, 174, 96) : Color.FromRgb(192, 57, 43));

            var lastFailedUtc = snap.LastEmailFailed?.DateSent;
            var hasRecentFailure = lastFailedUtc.HasValue && (DateTime.UtcNow - DateTime.SpecifyKind(lastFailedUtc.Value, DateTimeKind.Utc)).TotalHours <= 24;

            var heartbeatAlive = hb?.LastSuccessUtc.HasValue == true && (DateTime.UtcNow - hb.LastSuccessUtc.Value).TotalMinutes < 10;

            if (!coreOk)
            {
                _lblHealthBadge.Text = "Schema missing";
                _lblHealthBadge.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226));
                _lblHealthBadge.Foreground = new SolidColorBrush(Color.FromRgb(153, 27, 27));
            }
            else if (snap.SchedulerTaskEnabled == false)
            {
                _lblHealthBadge.Text = "Scheduler disabled";
                _lblHealthBadge.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226));
                _lblHealthBadge.Foreground = new SolidColorBrush(Color.FromRgb(153, 27, 27));
            }
            else if (!emailOk)
            {
                _lblHealthBadge.Text = "Email partial";
                _lblHealthBadge.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
                _lblHealthBadge.Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9));
            }
            else if (hasRecentFailure)
            {
                _lblHealthBadge.Text = "Email failing (last 24h)";
                _lblHealthBadge.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
                _lblHealthBadge.Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9));
            }
            else if (heartbeatAlive)
            {
                _lblHealthBadge.Text = "Healthy • Scheduler running (heartbeat OK)";
                _lblHealthBadge.Background = new SolidColorBrush(Color.FromRgb(209, 250, 229));
                _lblHealthBadge.Foreground = new SolidColorBrush(Color.FromRgb(6, 95, 70));
            }
            else if (snap.SchedulerIsRunning == true)
            {
                _lblHealthBadge.Text = "Healthy • Scheduler running";
                _lblHealthBadge.Background = new SolidColorBrush(Color.FromRgb(209, 250, 229));
                _lblHealthBadge.Foreground = new SolidColorBrush(Color.FromRgb(6, 95, 70));
            }
            else if (snap.SchedulerTaskEnabled == true)
            {
                _lblHealthBadge.Text = "Healthy • Scheduler ready";
                _lblHealthBadge.Background = new SolidColorBrush(Color.FromRgb(209, 250, 229));
                _lblHealthBadge.Foreground = new SolidColorBrush(Color.FromRgb(6, 95, 70));
            }
            else
            {
                _lblHealthBadge.Text = "Healthy • Scheduler idle";
                _lblHealthBadge.Background = new SolidColorBrush(Color.FromRgb(209, 250, 229));
                _lblHealthBadge.Foreground = new SolidColorBrush(Color.FromRgb(6, 95, 70));
            }
            ((Border)_lblHealthBadge.Parent).Background = _lblHealthBadge.Background;

            _lblOpenValue.Text = (snap.DashboardMetrics?.OpenTickets ?? 0).ToString();
            _lblPendingValue.Text = (snap.PendingTickets).ToString();
            _lblCriticalValue.Text = (snap.DashboardMetrics?.CriticalTickets ?? 0).ToString();
            _lblTodayValue.Text = (snap.DashboardMetrics?.TodaysVolume ?? 0).ToString();

            _lblCountsValue.Text = snap.DashboardMetrics?.AvgResolutionMinutes.HasValue == true ? $"{snap.DashboardMetrics.AvgResolutionMinutes.Value} min" : "-";
            _lblLastEscalationValue.Text = snap.LastAutoEscalationUtc.HasValue ? ToLocalDisplay(snap.LastAutoEscalationUtc.Value) + " (Auto)" : "-";
            _lblVersionValue.Text = FormatVersionInfo(snap);
            _lblTrendValue.Text = FormatTrendSummary(snap);

            ApplyEmailSection(_lblLastEmailFailedValue, _txtLastEmailFailedDetails, snap.LastEmailFailed, "(none)");
            ApplyEmailSection(_lblLastEmailSkippedValue, _txtLastEmailSkippedDetails, snap.LastEmailSkipped, "(none)");

            _lblLastReminderEmailValue.Text = snap.LastReminderEmail != null ? $"{ToLocalDisplay(snap.LastReminderEmail.DateSent)} • {snap.LastReminderEmail.Status}" : "(none)";
            _lblLastEscalationEmailValue.Text = snap.LastEscalationEmail != null ? $"{ToLocalDisplay(snap.LastEscalationEmail.DateSent)} • {snap.LastEscalationEmail.Status}" : "(none)";

            var gridItems = (snap.HealthChecks != null && snap.HealthChecks.Count > 0
                ? snap.HealthChecks.Select(c => new
                {
                    Area = c.Area ?? "",
                    ObjectName = c.Name ?? "",
                    Severity = c.Severity ?? "Healthy",
                    StatusText = c.Severity ?? "Healthy",
                    Detail = (c.Message ?? "").Trim(),
                    SuggestedFix = (c.SuggestedFix ?? "").Trim()
                })
                : snap.SchemaChecks.Select(c => new
                {
                    Area = c.Area ?? "",
                    ObjectName = c.ObjectName ?? "",
                    Severity = c.Severity ?? (c.Exists ? "Healthy" : "Critical"),
                    StatusText = c.Exists ? "Healthy" : "Critical",
                    Detail = (c.Detail ?? "").Trim(),
                    SuggestedFix = (c.SuggestedFix ?? "").Trim()
                }))
                .OrderBy(c => c.Area)
                .ThenBy(c => SeveritySort(c.Severity))
                .ThenBy(c => c.ObjectName)
                .ToList();
            _schemaGrid.ItemsSource = gridItems;

            // Top issues summary widget
            BuildTopIssuesPanel(snap);
        }

        private void BuildTopIssuesPanel(CallMonitoringRepository.ItcmDiagnosticsSnapshot snap)
        {
            if (_topIssuesPanel == null) return;
            _topIssuesPanel.Children.Clear();

            var issues = (snap?.HealthChecks ?? new List<CallMonitoringRepository.ItcmDiagnosticCheckResult>())
                .Where(c => !string.Equals(c.Severity, "Healthy", StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => SeveritySort(c.Severity))
                .ThenBy(c => c.Area)
                .Take(5)
                .ToList();

            if (issues.Count == 0)
            {
                _topIssuesPanel.Children.Add(new TextBlock
                {
                    Text = "All checks passed — no top issues",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 4, 0, 4)
                });
                return;
            }

            var header = new TextBlock
            {
                Text = $"Top issues ({issues.Count})",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            _topIssuesPanel.Children.Add(header);

            foreach (var issue in issues)
            {
                var color = string.Equals(issue.Severity, "Critical", StringComparison.OrdinalIgnoreCase)
                    ? Color.FromRgb(239, 68, 68)
                    : Color.FromRgb(245, 158, 11);

                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

                var dot = new Border
                {
                    Width = 8,
                    Height = 8,
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(color),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };

                var text = new TextBlock
                {
                    Text = $"[{issue.Severity}] {issue.Area} / {issue.Name}: {issue.Message}",
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(color),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 580
                };

                row.Children.Add(dot);
                row.Children.Add(text);
                _topIssuesPanel.Children.Add(row);

                if (!string.IsNullOrWhiteSpace(issue.SuggestedFix))
                {
                    var fixText = new TextBlock
                    {
                        Text = "  Fix: " + issue.SuggestedFix,
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 580,
                        Margin = new Thickness(16, 0, 0, 4)
                    };
                    _topIssuesPanel.Children.Add(fixText);
                }

                // Runbook link
                var runbookUrl = GetRunbookUrl(issue.Area, issue.Name);
                if (!string.IsNullOrWhiteSpace(runbookUrl))
                {
                    var link = new TextBlock
                    {
                        Text = "  Runbook",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                        TextDecorations = TextDecorations.Underline,
                        Cursor = Cursors.Hand,
                        Margin = new Thickness(16, 0, 0, 6)
                    };
                    link.MouseLeftButtonDown += (_, __) =>
                    {
                        try { Process.Start(new ProcessStartInfo(runbookUrl) { UseShellExecute = true }); }
                        catch { }
                    };
                    _topIssuesPanel.Children.Add(link);
                }
            }
        }

        private static string GetRunbookUrl(string area, string name)
        {
            // Return internal runbook/help links based on area/name
            if (string.Equals(area, "Schema", StringComparison.OrdinalIgnoreCase))
                return "https://yakult-inventory-help.runbook/itcm/schema-setup";
            if (string.Equals(area, "Email", StringComparison.OrdinalIgnoreCase))
                return "https://yakult-inventory-help.runbook/itcm/email-troubleshooting";
            if (string.Equals(area, "Scheduler", StringComparison.OrdinalIgnoreCase))
                return "https://yakult-inventory-help.runbook/itcm/scheduler-setup";
            if (string.Equals(area, "Background", StringComparison.OrdinalIgnoreCase))
                return "https://yakult-inventory-help.runbook/itcm/background-jobs";
            return null;
        }

        private async Task ApplyEnhancedSignalsAsync(CallMonitoringRepository.ItcmDiagnosticsSnapshot snap)
        {
            var status = CallEmailNotificationService.GetBackgroundJobStatusSnapshot();

            _lblJobLastStartValue.Text = status.LastRunStartUtc.HasValue ? status.LastRunStartUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "(none)";
            _lblJobLastEndValue.Text = status.LastRunEndUtc.HasValue ? status.LastRunEndUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "(none)";
            _lblJobNextRunValue.Text = status.NextRunUtc.HasValue ? status.NextRunUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "(unknown)";

            var p = snap?.BackgroundLockProbe;
            if (p?.IsFree == true) _lblJobLockProbeValue.Text = p.ResultCode.HasValue ? $"Free (code {p.ResultCode.Value})" : "Free";
            else if (p?.IsFree == false) _lblJobLockProbeValue.Text = p.ResultCode.HasValue ? $"Blocked (code {p.ResultCode.Value})" : "Blocked (another client running)";
            else _lblJobLockProbeValue.Text = "Unknown";

            _lblJobCountsValue.Text = $"Reminders: {status.RemindersSent}/{status.ReminderCandidates} (skipped/failed {status.ReminderSkippedOrFailed}) • Escalations: {status.EscalationsApplied}/{status.EscalationCandidates} (skipped/failed {status.EscalationSkippedOrFailed})";

            await EnrichEmailDetailsAsync(snap?.LastEmailFailed, _txtLastEmailFailedDetails, isPrimary: true);
            await EnrichEmailDetailsAsync(snap?.LastEmailSkipped, _txtLastEmailSkippedDetails, isPrimary: false);
        }

        private async Task EnrichEmailDetailsAsync(Yakult.Inventory.App.Models.CallMonitoring.CallEmailLogItem log, TextBox detailsBox, bool isPrimary)
        {
            if (log == null || detailsBox == null || !log.TicketId.HasValue || log.TicketId.Value <= 0) return;

            var ticketId = log.TicketId.Value;
            var baseText = (detailsBox.Text ?? string.Empty).Trim();

            var ticket = await _repo.GetTicketNotificationDataAsync(ticketId);
            _lastResolvedTicketId = ticketId;
            _lastResolvedDeptId = ticket?.DeptId;
            _lastResolvedBranchId = ticket?.BranchId;

            var senderResolution = await _repo.GetSmtpSenderResolutionForTicketAsync(ticket?.BranchId, ticket?.DeptId);
            var email = new CallEmailNotificationService(_repo);
            var recipientTrace = await email.GetRecipientResolutionTraceForTicketAsync(ticketId, log.EmailType);

            if (senderResolution == null && recipientTrace == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("Resolution trace:");

            if (senderResolution != null)
            {
                var sender = senderResolution.Sender;
                var authMode = sender != null && !string.IsNullOrWhiteSpace(sender.SmtpUsername) ? "Username+Password" : "DefaultCredentials";
                var from = sender == null ? "" : $"{(sender.FromEmail ?? "").Trim()} {(string.IsNullOrWhiteSpace(sender.FromName) ? "" : $"({sender.FromName.Trim()})")}".Trim();
                sb.AppendLine($"- SMTP profile: {senderResolution.Source}{(senderResolution.ProfileId.HasValue ? $" (ProfileId={senderResolution.ProfileId.Value})" : "")}{(!string.IsNullOrWhiteSpace(senderResolution.ProfileName) ? $" • {senderResolution.ProfileName}" : "")}");
                if (sender != null)
                {
                    var userLabel = string.IsNullOrWhiteSpace(sender.SmtpUsername) ? "(none)" : "(set)";
                    sb.AppendLine($"- SMTP host: {sender.SmtpServer}:{sender.SmtpPort} • SSL={sender.UseSsl} • Auth={authMode} • User={userLabel}");
                    if (!string.IsNullOrWhiteSpace(from)) sb.AppendLine($"- From: {from}");
                }
            }

            if (recipientTrace != null)
            {
                sb.AppendLine($"- Recipients resolved: {recipientTrace.FinalRecipients.Count}");
                if (recipientTrace.FinalRecipients.Count > 0) sb.AppendLine("  " + string.Join(", ", recipientTrace.FinalRecipients));
                sb.AppendLine($"- GroupEmail: {(recipientTrace.RulesGroupEmail ?? "").Trim()}");
                sb.AppendLine($"- Dept recipients: {(recipientTrace.DepartmentRecipients ?? "").Trim()}");
                sb.AppendLine($"- Branch recipients: {(recipientTrace.BranchRecipients ?? "").Trim()}");
            }

            sb.AppendLine();
            detailsBox.Text = sb.ToString().TrimEnd() + (string.IsNullOrWhiteSpace(baseText) ? "" : ("\r\n\r\n" + baseText));

            if (isPrimary && _lnkTemplates != null)
            {
                var normalized = (log.EmailType ?? string.Empty).Trim();
                if (normalized.Equals("Escalation", StringComparison.OrdinalIgnoreCase)) _lnkTemplates.Content = "Templates (Escalation)";
                else if (normalized.Equals("StatusUpdate", StringComparison.OrdinalIgnoreCase)) _lnkTemplates.Content = "Templates (Status Update)";
                else if (normalized.Equals("NewTicket", StringComparison.OrdinalIgnoreCase)) _lnkTemplates.Content = "Templates (New Ticket)";
                else _lnkTemplates.Content = "Templates (Reminder)";
            }
        }

        private void ApplyEmailSection(TextBlock label, TextBox details, Yakult.Inventory.App.Models.CallMonitoring.CallEmailLogItem log, string emptyMessage)
        {
            if (log == null)
            {
                label.Text = "No recent issues (" + emptyMessage + ")";
                label.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                details.Text = string.Empty;
                details.Visibility = Visibility.Collapsed;
                return;
            }

            var dateSent = ToLocalDisplay(log.DateSent);
            var ticket = log.TicketId.HasValue && log.TicketId.Value > 0 ? $"Ticket #{log.TicketId.Value}" : "No Ticket";
            label.Text = $"{dateSent} • {log.EmailType} • {ticket}";
            
            if (string.Equals(log.Status, "Failed", StringComparison.OrdinalIgnoreCase)) label.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            else if (string.Equals(log.Status, "Skipped", StringComparison.OrdinalIgnoreCase)) label.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            else label.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));

            var formatted = FormatActionableText(log.ErrorMessage);
            details.Text = formatted;
            details.Visibility = string.IsNullOrWhiteSpace(formatted) ? Visibility.Collapsed : Visibility.Visible;
        }

        private string FormatActionableText(string message)
        {
            var text = (message ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            try
            {
                var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Select(l => (l ?? string.Empty).Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                var problem = lines.FirstOrDefault(l => l.StartsWith("Problem:", StringComparison.OrdinalIgnoreCase));
                var fix = lines.FirstOrDefault(l => l.StartsWith("Fix:", StringComparison.OrdinalIgnoreCase));
                var info = lines.FirstOrDefault(l => l.StartsWith("Info:", StringComparison.OrdinalIgnoreCase));

                if (problem != null || fix != null || info != null)
                {
                    var sb = new StringBuilder();
                    if (problem != null) sb.AppendLine(problem);
                    if (fix != null) { if (sb.Length > 0) sb.AppendLine(); sb.AppendLine(fix); }
                    if (info != null) { if (sb.Length > 0) sb.AppendLine(); sb.AppendLine(info); }
                    return sb.ToString().Trim();
                }
            }
            catch { }
            return text;
        }

        private string ToLocalDisplay(DateTime utcFromDb)
        {
            var utc = utcFromDb.Kind == DateTimeKind.Utc ? utcFromDb : DateTime.SpecifyKind(utcFromDb, DateTimeKind.Utc);
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        private void OpenTemplatesDeepLink()
        {
            var type = (_lastSnapshot?.LastEmailFailed?.EmailType ?? string.Empty).Trim();
            if (type.Equals("Escalation", StringComparison.OrdinalIgnoreCase)) _navigator?.OpenEmailDeepLink(WpfEmailNotificationDeepLinkTarget.TemplatesEscalation, null, null, null);
            else if (type.Equals("StatusUpdate", StringComparison.OrdinalIgnoreCase)) _navigator?.OpenEmailDeepLink(WpfEmailNotificationDeepLinkTarget.TemplatesStatusUpdate, null, null, null);
            else if (type.Equals("Assignment", StringComparison.OrdinalIgnoreCase)) _navigator?.OpenEmailDeepLink(WpfEmailNotificationDeepLinkTarget.TemplatesAssignment, null, null, null);
            else if (type.Equals("Reassignment", StringComparison.OrdinalIgnoreCase)) _navigator?.OpenEmailDeepLink(WpfEmailNotificationDeepLinkTarget.TemplatesReassignment, null, null, null);
            else if (type.Equals("NewTicket", StringComparison.OrdinalIgnoreCase)) _navigator?.OpenEmailDeepLink(WpfEmailNotificationDeepLinkTarget.TemplatesNewTicket, null, null, null);
            else _navigator?.OpenEmailDeepLink(WpfEmailNotificationDeepLinkTarget.TemplatesReminder, null, null, null);
        }

        private void TryCopyToClipboard(string text)
        {
            try { if (!string.IsNullOrWhiteSpace(text)) Clipboard.SetText(text); } catch { }
        }

        private void TryOpenLogFolder()
        {
            try
            {
                var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (System.IO.Directory.Exists(path)) System.Diagnostics.Process.Start(path);
                else MessageBox.Show("Log folder not found.", "Logs", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("Failed to open log folder: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void ExportDiagnosticsText(CallMonitoringRepository.ItcmDiagnosticsSnapshot snap)
        {
            try
            {
                if (snap == null) return;
                var dlg = new SaveFileDialog
                {
                    Title = "Export ITCM diagnostics",
                    Filter = "Text file (*.txt)|*.txt",
                    FileName = "itcm-diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"
                };
                if (dlg.ShowDialog() != true) return;
                File.WriteAllText(dlg.FileName, BuildDiagnosticsText(snap, redact: true), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to export diagnostics: " + ex.Message, "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExportDiagnosticsJson(CallMonitoringRepository.ItcmDiagnosticsSnapshot snap)
        {
            try
            {
                if (snap == null) return;
                var dlg = new SaveFileDialog
                {
                    Title = "Export ITCM diagnostics JSON",
                    Filter = "JSON file (*.json)|*.json",
                    FileName = "itcm-diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json"
                };
                if (dlg.ShowDialog() != true) return;
                var json = BuildDiagnosticsJson(snap);
                File.WriteAllText(dlg.FileName, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to export diagnostics JSON: " + ex.Message, "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenWebDashboard()
        {
            try
            {
                var url = AppConfig.ItcmServerUrl;
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open ITCM Server dashboard.\n\n" + ex.Message, "Dashboard", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CreateTestTicketAsync()
        {
            try
            {
                var result = MessageBox.Show(
                    "This will create a synthetic test ticket in the database.\n\n" +
                    "The ticket will be clearly marked as [TEST] and can be cleaned up later.\n\n" +
                    "Continue?",
                    "Create Test Ticket",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                var ticketId = await _repo.CreateSyntheticTestTicketAsync(null);
                if (ticketId.HasValue)
                {
                    MessageBox.Show($"Test ticket #{ticketId.Value} created successfully.", "Test Ticket", MessageBoxButton.OK, MessageBoxImage.Information);
                    await RefreshAsync();
                }
                else
                {
                    MessageBox.Show("Failed to create test ticket.", "Test Ticket", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to create test ticket:\n" + ex.Message, "Test Ticket", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CleanupTestTicketsAsync()
        {
            try
            {
                var hasAny = await _repo.AnySyntheticTestTicketsAsync();
                if (!hasAny)
                {
                    MessageBox.Show("No test tickets found to clean up.", "Cleanup", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    "This will permanently delete all synthetic test tickets from the database.\n\n" +
                    "This action cannot be undone.\n\n" +
                    "Continue?",
                    "Cleanup Test Tickets",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                var deleted = await _repo.CleanupSyntheticTestTicketsAsync();
                MessageBox.Show($"{deleted} test ticket(s) cleaned up.", "Cleanup", MessageBoxButton.OK, MessageBoxImage.Information);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to clean up test tickets:\n" + ex.Message, "Cleanup", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string BuildDiagnosticsText(CallMonitoringRepository.ItcmDiagnosticsSnapshot snap, bool redact)
        {
            if (snap == null) return "No data";
            var sb = new StringBuilder();
            sb.AppendLine("ITCM Diagnostics Snapshot");
            sb.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Server: " + SafeDiagnosticValue(snap.ServerName, redact));
            sb.AppendLine("Database: " + SafeDiagnosticValue(snap.DatabaseName, redact));
            sb.AppendLine("Web Server: " + FormatServerPingSummary(_lastServerPing));
            sb.AppendLine("Connected devices: " + FormatConnectedClientsSummary(_lastConnectedClients));
            sb.AppendLine("Health: " + (_lblHealthBadge?.Text ?? "-"));
            sb.AppendLine("Scheduler: " + (snap.SchedulerTaskState ?? "-") + " / enabled=" + (snap.SchedulerTaskEnabled.HasValue ? snap.SchedulerTaskEnabled.Value.ToString() : "unknown"));
            sb.AppendLine("Processing: " + (snap.SchedulerIsRunning.HasValue ? (snap.SchedulerIsRunning.Value ? "running" : "idle") : "unknown"));
            sb.AppendLine("Last Activity: " + (snap.SchedulerLastActivityUtc.HasValue ? ToLocalDisplay(snap.SchedulerLastActivityUtc.Value) : "(none)"));
            sb.AppendLine("Versions: " + FormatVersionInfo(snap));
            sb.AppendLine("Trend: " + FormatTrendSummary(snap));
            sb.AppendLine();
            sb.AppendLine("Health Checks");
            foreach (var check in (snap.HealthChecks ?? new List<CallMonitoringRepository.ItcmDiagnosticCheckResult>()).OrderBy(c => SeveritySort(c.Severity)).ThenBy(c => c.Area).ThenBy(c => c.Name))
            {
                sb.AppendLine($"- [{check.Severity}] {check.Area} / {check.Name}: {RedactDiagnosticText(check.Message, redact)}");
                if (!string.IsNullOrWhiteSpace(check.SuggestedFix))
                    sb.AppendLine("  Fix: " + RedactDiagnosticText(check.SuggestedFix, redact));
            }
            sb.AppendLine();
            sb.AppendLine("Metrics");
            sb.AppendLine("- Open: " + (snap.DashboardMetrics?.OpenTickets ?? 0));
            sb.AppendLine("- Pending: " + snap.PendingTickets);
            sb.AppendLine("- Critical: " + (snap.DashboardMetrics?.CriticalTickets ?? 0));
            sb.AppendLine("- Today: " + (snap.DashboardMetrics?.TodaysVolume ?? 0));
            sb.AppendLine("- Avg Resolution: " + (snap.DashboardMetrics?.AvgResolutionMinutes.HasValue == true ? snap.DashboardMetrics.AvgResolutionMinutes.Value + " min" : "-"));
            sb.AppendLine();
            sb.AppendLine("Recent Email Signals");
            AppendEmailLog(sb, "Last failed", snap.LastEmailFailed, redact);
            AppendEmailLog(sb, "Last skipped", snap.LastEmailSkipped, redact);
            AppendEmailLog(sb, "Last reminder", snap.LastReminderEmail, redact);
            AppendEmailLog(sb, "Last escalation", snap.LastEscalationEmail, redact);
            return sb.ToString().TrimEnd();
        }

        private string BuildDiagnosticsJson(CallMonitoringRepository.ItcmDiagnosticsSnapshot snap)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"generatedLocal\": \"" + EscapeJson(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")) + "\",");
            sb.AppendLine("  \"server\": \"" + EscapeJson(SafeDiagnosticValue(snap.ServerName, true)) + "\",");
            sb.AppendLine("  \"database\": \"" + EscapeJson(SafeDiagnosticValue(snap.DatabaseName, true)) + "\",");
            sb.AppendLine("  \"webServer\": \"" + EscapeJson(FormatServerPingSummary(_lastServerPing)) + "\",");
            sb.AppendLine("  \"connectedDevices\": \"" + EscapeJson(FormatConnectedClientsSummary(_lastConnectedClients)) + "\",");
            sb.AppendLine("  \"health\": \"" + EscapeJson(_lblHealthBadge?.Text ?? "-") + "\",");
            sb.AppendLine("  \"versions\": \"" + EscapeJson(FormatVersionInfo(snap)) + "\",");
            sb.AppendLine("  \"trend\": \"" + EscapeJson(FormatTrendSummary(snap)) + "\",");
            sb.AppendLine("  \"checks\": [");
            var checks = (snap.HealthChecks ?? new List<CallMonitoringRepository.ItcmDiagnosticCheckResult>()).OrderBy(c => SeveritySort(c.Severity)).ThenBy(c => c.Area).ThenBy(c => c.Name).ToList();
            for (var i = 0; i < checks.Count; i++)
            {
                var c = checks[i];
                sb.Append("    {");
                sb.Append("\"area\":\"" + EscapeJson(c.Area) + "\",");
                sb.Append("\"name\":\"" + EscapeJson(c.Name) + "\",");
                sb.Append("\"severity\":\"" + EscapeJson(c.Severity) + "\",");
                sb.Append("\"message\":\"" + EscapeJson(RedactDiagnosticText(c.Message, true)) + "\",");
                sb.Append("\"suggestedFix\":\"" + EscapeJson(RedactDiagnosticText(c.SuggestedFix, true)) + "\"");
                sb.Append("}");
                if (i < checks.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string FormatServerPingSummary(ItcmServerPing ping)
        {
            if (ping == null) return "-";
            if (!ping.Connected) return $"Unreachable ({ping.Error}) • {ping.Url}";
            var state = ping.Running == true ? "running"
                : ping.Paused == true ? "paused"
                : ping.SchedulerEnabled == false ? "disabled" : "ready";
            return $"Connected ({ping.LatencyMs}ms) • v{(string.IsNullOrWhiteSpace(ping.Version) ? "-" : ping.Version)} • {state} • {ping.Url}";
        }

        private static string FormatConnectedClientsSummary(List<CallClientPresenceItem> clients)
        {
            if (clients == null || clients.Count == 0) return "-";
            var online = clients.Count(c => c.Online);
            var parts = clients
                .OrderByDescending(c => c.Online)
                .ThenByDescending(c => c.LastSeenUtc)
                .Take(8)
                .Select(c => (c.Online ? "" : "[stale] ")
                    + (c.MachineName ?? "-").Trim()
                    + " • " + (c.UserName ?? "-").Trim());
            return online + " online: " + string.Join("; ", parts);
        }

        private static string FormatVersionInfo(CallMonitoringRepository.ItcmDiagnosticsSnapshot snap)
        {
            var v = snap?.VersionInfo;
            if (v == null) return "-";
            return $"Desktop {NullDash(v.DesktopVersion)} • Scheduler {NullDash(v.SchedulerVersion)} • DB {NullDash(v.DatabaseMigrationVersion)}";
        }

        private static string FormatTrendSummary(CallMonitoringRepository.ItcmDiagnosticsSnapshot snap)
        {
            var trends = snap?.Trends;
            if (trends == null || trends.Count == 0) return "-";
            var failed = trends.Sum(t => t.FailedEmails);
            var escalations = trends.Sum(t => t.Escalations);
            var signals = trends.Sum(t => t.SchedulerSignals);
            var tickets = trends.Sum(t => t.TicketVolume);
            var avg = trends.Where(t => t.AvgResolutionMinutes.HasValue).Select(t => t.AvgResolutionMinutes.Value).DefaultIfEmpty(0).Average();
            return $"Tickets {tickets} • Failed emails {failed} • Escalations {escalations} • Signals {signals} • Avg resolve {(avg <= 0 ? "-" : Math.Round(avg) + " min")}";
        }

        private static int SeveritySort(string severity)
        {
            if (string.Equals(severity, "Critical", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(severity, "Warning", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(severity, "Unknown", StringComparison.OrdinalIgnoreCase)) return 2;
            return 3;
        }

        private static string NullDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        private static string SafeDiagnosticValue(string value, bool redact)
        {
            var text = value ?? string.Empty;
            return redact ? RedactDiagnosticText(text, true) : text;
        }

        private static string RedactDiagnosticText(string text, bool redact)
        {
            if (!redact || string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;
            var output = text;
            output = Regex.Replace(output, @"(?i)(password|pwd|secret|token|key)\s*=\s*[^;\s]+", "$1=***");
            output = Regex.Replace(output, @"(?i)(User ID|UID)\s*=\s*[^;]+", "$1=***");
            output = Regex.Replace(output, @"(?i)(Data Source|Server)\s*=\s*[^;]+", "$1=***");
            output = Regex.Replace(output, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", "***@***", RegexOptions.IgnoreCase);
            return output;
        }

        private static string EscapeJson(string text)
        {
            return (text ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private void AppendEmailLog(StringBuilder sb, string label, Yakult.Inventory.App.Models.CallMonitoring.CallEmailLogItem log, bool redact)
        {
            if (log == null)
            {
                sb.AppendLine("- " + label + ": (none)");
                return;
            }
            sb.AppendLine("- " + label + ": " + ToLocalDisplay(log.DateSent) + " • " + NullDash(log.EmailType) + " • " + NullDash(log.Status) + " • Ticket " + (log.TicketId.HasValue ? log.TicketId.Value.ToString() : "-"));
            if (!string.IsNullOrWhiteSpace(log.ErrorMessage))
                sb.AppendLine("  " + RedactDiagnosticText(log.ErrorMessage, redact));
        }
    }
}
