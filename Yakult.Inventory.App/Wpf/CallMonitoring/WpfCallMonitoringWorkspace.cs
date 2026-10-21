using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WinForms = System.Windows.Forms;
using Yakult.Inventory.App.Forms.CallMonitoring;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Pages.Update;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed class WpfCallMonitoringWorkspace : System.Windows.Controls.UserControl
    {
        private static readonly Brush InkBrush = BrushFromRgb(32, 38, 53);
        private static readonly Brush MutedBrush = BrushFromRgb(103, 112, 129);
        private static readonly Brush BodyBrush = BrushFromRgb(74, 82, 97);
        private static readonly Brush HairlineBrush = BrushFromRgb(229, 230, 232);
        private static readonly Brush InputBorderBrush = BrushFromRgb(205, 208, 214);
        private static readonly Brush SoftFillBrush = BrushFromRgb(245, 246, 248);
        private static readonly Brush HoverFillBrush = BrushFromRgb(250, 250, 250);
        private static readonly Brush GridLineBrush = BrushFromRgb(240, 240, 242);
        private static readonly Brush AlertRedBrush = BrushFromRgb(185, 35, 53);
        private static readonly Color AccentBlue = Color.FromRgb(59, 126, 161);
        private static readonly Color AccentRed = Color.FromRgb(185, 35, 53);
        private static readonly Color AccentViolet = Color.FromRgb(107, 92, 165);
        private static readonly Color AccentGreen = Color.FromRgb(47, 133, 90);
        private static readonly Color AccentAmber = Color.FromRgb(183, 121, 31);

        private static readonly ResourceDictionary CommandStyles = WpfThemeResources.GetWorkspaceCommandStyles();

        private readonly TextBlock _openTicketsValue;
        private readonly TextBlock _criticalTicketsValue;
        private readonly TextBlock _todayVolumeValue;
        private readonly TextBlock _avgResolutionValue;
        private readonly TextBlock _mobileUpdatesValue;
        private readonly TextBlock _headerStatusText;
        private readonly TextBlock _lastRefreshedText;
        private readonly TextBlock _attentionSummaryText;
        private readonly TextBlock _lineChartTitle;
        private readonly TextBlock _donutChartTitle;
        private readonly Canvas _lineChartCanvas;
        private readonly Canvas _donutCanvas;
        private readonly StackPanel _donutLegendPanel;
        private readonly Button _donutToggleButton;
        private readonly StackPanel _attentionPanel;
        private readonly Button _attentionPrevButton;
        private readonly Button _attentionNextButton;
        private readonly List<(Button Button, int Days)> _rangeButtons = new List<(Button, int)>();

        private ICallMonitoringRepository _repository;
        private ICallMonitoringNavigator _navigator;
        private bool _initialized;
        private int _loadingFlag; // 0 = idle, 1 = loading (thread-safe via Interlocked)
        private bool _showResolutionDonut = true;
        private int _analyticsRangeDays = 7;
        private int _attentionPageIndex = 1;
        private const int AttentionPageSize = 8;
        private List<CallTicketListItem> _attentionCache = new List<CallTicketListItem>();
        private List<CallTicketVolumePoint> _lineChartPoints;
        private double _lineChartDrawnWidth;

        public WpfCallMonitoringWorkspace()
        {
            Background = new SolidColorBrush(WpfThemeResources.PageBackground);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var root = new StackPanel
            {
                Margin = new Thickness(28, 24, 28, 32)
            };
            scroll.Content = root;

            var header = BuildHeaderSection();
            root.Children.Add(header.Container);
            _headerStatusText = header.StatusText;
            _lastRefreshedText = header.LastRefreshedText;

            var metricGrid = new UniformGrid
            {
                Columns = 5,
                Margin = new Thickness(-7, 22, -7, 0)
            };

            _openTicketsValue = CreateMetricValue();
            _criticalTicketsValue = CreateMetricValue();
            _todayVolumeValue = CreateMetricValue();
            _avgResolutionValue = CreateMetricValue();
            _mobileUpdatesValue = CreateMetricValue();

            AddMetricCard(metricGrid, "Open Tickets", "Active now", _openTicketsValue, AccentBlue,
                async () => await ShowDrillDownAsync("Open Tickets", () => _repository.GetOpenTicketsListAsync()));
            AddMetricCard(metricGrid, "Critical", "Requires attention", _criticalTicketsValue, AccentRed,
                async () => await ShowDrillDownAsync("Critical Tickets", () => _repository.GetCriticalTicketsListAsync()));
            AddMetricCard(metricGrid, "Today's Volume", "Tickets today", _todayVolumeValue, AccentViolet,
                async () => await ShowDrillDownAsync("Ticket Volume Today", () => _repository.GetTodaysTicketsListAsync()));
            AddMetricCard(metricGrid, "Avg Resolution", "Recent solved tickets", _avgResolutionValue, AccentGreen,
                async () => await ShowDrillDownAsync("Example Resolved", () => _repository.GetRecentlySolvedAsync()));
            AddMetricCard(metricGrid, "Mobile Updates", "Needs processing", _mobileUpdatesValue, AccentAmber,
                OpenMobileUpdatesAndRefresh);
            root.Children.Add(metricGrid);

            var analyticsGrid = new Grid
            {
                Margin = new Thickness(0, 18, 0, 0)
            };
            analyticsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
            analyticsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lineChartCard = CreateCard();
            Grid.SetColumn(lineChartCard, 0);
            analyticsGrid.Children.Add(lineChartCard);

            var lineChartStack = new StackPanel();
            lineChartCard.Child = lineChartStack;

            var lineHeader = new Grid();
            lineHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            lineHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            lineChartStack.Children.Add(lineHeader);

            _lineChartTitle = CreateMicroLabelText();
            lineHeader.Children.Add(_lineChartTitle);

            var rangeButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            rangeButtons.Children.Add(CreateRangeButton("7 Days", 7));
            rangeButtons.Children.Add(CreateRangeButton("30 Days", 30));
            rangeButtons.Children.Add(CreateRangeButton("90 Days", 90));
            Grid.SetColumn(rangeButtons, 1);
            lineHeader.Children.Add(rangeButtons);

            _lineChartCanvas = new Canvas
            {
                Height = 236,
                Margin = new Thickness(0, 16, 0, 0),
                Background = Brushes.Transparent,
                ClipToBounds = true
            };
            _lineChartCanvas.SizeChanged += (_, __) => RedrawLineChartIfNeeded();
            lineChartStack.Children.Add(_lineChartCanvas);

            var donutCard = CreateCard();
            donutCard.Margin = new Thickness(14, 0, 0, 0);
            Grid.SetColumn(donutCard, 1);
            analyticsGrid.Children.Add(donutCard);

            var donutStack = new StackPanel();
            donutCard.Child = donutStack;

            var donutHeader = new Grid();
            donutHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            donutHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            donutStack.Children.Add(donutHeader);

            _donutChartTitle = CreateMicroLabelText();
            _donutChartTitle.VerticalAlignment = VerticalAlignment.Center;
            donutHeader.Children.Add(_donutChartTitle);

            _donutToggleButton = CreateQuietButton("Active Tickets");
            _donutToggleButton.Padding = new Thickness(10, 5, 10, 5);
            _donutToggleButton.FontSize = 11.5;
            _donutToggleButton.Click += async (_, __) =>
            {
                _showResolutionDonut = !_showResolutionDonut;
                await RefreshDonutChartAsync();
            };
            Grid.SetColumn(_donutToggleButton, 1);
            donutHeader.Children.Add(_donutToggleButton);

            _donutCanvas = new Canvas
            {
                Width = 240,
                Height = 240,
                Margin = new Thickness(0, 16, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            donutStack.Children.Add(_donutCanvas);
            _donutLegendPanel = new StackPanel
            {
                Margin = new Thickness(0, 10, 0, 0)
            };
            donutStack.Children.Add(_donutLegendPanel);

            root.Children.Add(analyticsGrid);

            var lowerGrid = new Grid
            {
                Margin = new Thickness(0, 18, 0, 0)
            };
            lowerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.45, GridUnitType.Star) });
            lowerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var attentionCard = CreateCard();
            Grid.SetColumn(attentionCard, 0);
            lowerGrid.Children.Add(attentionCard);

            var attentionLayout = new DockPanel();
            attentionCard.Child = attentionLayout;

            var attentionHeaderGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 12)
            };
            attentionHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            attentionHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            DockPanel.SetDock(attentionHeaderGrid, Dock.Top);
            attentionLayout.Children.Add(attentionHeaderGrid);

            var attentionHeader = new StackPanel();
            attentionHeader.Children.Add(CreateMicroLabelText("ATTENTION QUEUE"));
            _attentionSummaryText = new TextBlock
            {
                Margin = new Thickness(0, 6, 0, 0),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = MutedBrush
            };
            attentionHeader.Children.Add(_attentionSummaryText);
            attentionHeaderGrid.Children.Add(attentionHeader);

            var attentionPager = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top
            };
            _attentionPrevButton = CreatePagerButton("Prev", async () =>
            {
                if (_attentionPageIndex <= 1)
                {
                    return;
                }

                _attentionPageIndex--;
                RenderAttentionPage();
                await Task.CompletedTask;
            });
            _attentionNextButton = CreatePagerButton("Next", async () =>
            {
                var totalPages = GetAttentionTotalPages();
                if (_attentionPageIndex >= totalPages)
                {
                    return;
                }

                _attentionPageIndex++;
                RenderAttentionPage();
                await Task.CompletedTask;
            });
            attentionPager.Children.Add(_attentionPrevButton);
            attentionPager.Children.Add(_attentionNextButton);
            Grid.SetColumn(attentionPager, 1);
            attentionHeaderGrid.Children.Add(attentionPager);

            var attentionScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            _attentionPanel = new StackPanel();
            attentionScroll.Content = _attentionPanel;
            attentionLayout.Children.Add(attentionScroll);

            var sideRail = CreateCard();
            sideRail.Margin = new Thickness(14, 0, 0, 0);
            Grid.SetColumn(sideRail, 1);
            lowerGrid.Children.Add(sideRail);

            var sideLayout = new StackPanel();
            sideRail.Child = sideLayout;

            sideLayout.Children.Add(CreateMicroLabelText("QUICK ACTIONS"));
            sideLayout.Children.Add(new TextBlock
            {
                Text = "Jump straight into the other ITCM screens.",
                Margin = new Thickness(0, 6, 0, 12),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = MutedBrush
            });

            sideLayout.Children.Add(CreateQuickActionRow(
                "Ticket Workspace",
                () => _navigator?.OpenTicketWorkspace()));
            sideLayout.Children.Add(CreateQuickActionRow(
                "Display Mode",
                () => _navigator?.OpenDisplayMode()));
            sideLayout.Children.Add(CreateQuickActionRow(
                "Reports",
                () => _navigator?.OpenReports()));
            sideLayout.Children.Add(CreateQuickActionRow(
                "Mobile Updates",
                OpenMobileUpdatesAndRefresh));

            root.Children.Add(lowerGrid);
            Content = scroll;

            UpdateAnalyticsTitles();
            UpdateRangeButtonStates();
            SetLoadingState("Loading dashboard…");
        }

        public void Initialize(
            ICallMonitoringRepository repository,
            ICallMonitoringNavigator navigator)
        {
            _repository = repository;
            _navigator = navigator;

            if (_initialized)
            {
                return;
            }

            _initialized = true;
            LoadAsync().FireAndForget(ex => Dispatcher.BeginInvoke(new Action(() => _headerStatusText.Text = "Dashboard load failed: " + ex.Message)));
        }

        public void RefreshData()
        {
            if (_repository == null)
            {
                return;
            }

            LoadAsync().FireAndForget(ex => Dispatcher.BeginInvoke(new Action(() => _headerStatusText.Text = "Dashboard refresh failed: " + ex.Message)));
        }

        private async Task LoadAsync()
        {
            if (_repository == null)
                return;

            // Thread-safe guard: only one load at a time
            if (System.Threading.Interlocked.CompareExchange(ref _loadingFlag, 1, 0) != 0)
                return;

            SetLoadingState("Loading live call monitoring data...");

            try
            {
                if (!await _repository.CallSchemaExistsAsync())
                {
                    SetLoadingState("Call monitoring tables are not available yet.");
                    return;
                }

                var metrics = await _repository.GetDashboardMetricsAsync();
                _attentionCache = await _repository.GetRequiresAttentionAsync(100) ?? new List<CallTicketListItem>();
                _attentionPageIndex = Math.Min(Math.Max(1, _attentionPageIndex), GetAttentionTotalPages());

                var updatesRepo = new SetItemUpdateRepository();
                var mobileUpdates = await updatesRepo.GetUnprocessedCountAsync();

                _openTicketsValue.Text = metrics.OpenTickets.ToString("N0");
                _criticalTicketsValue.Text = metrics.CriticalTickets.ToString("N0");
                _criticalTicketsValue.Foreground = metrics.CriticalTickets > 0 ? AlertRedBrush : InkBrush;
                _todayVolumeValue.Text = metrics.TodaysVolume.ToString("N0");
                _avgResolutionValue.Text = FormatResolution(metrics.AvgResolutionMinutes);
                _mobileUpdatesValue.Text = mobileUpdates.ToString("N0");
                _mobileUpdatesValue.Foreground = mobileUpdates > 0 ? AlertRedBrush : InkBrush;

                RenderAttentionPage();
                await RefreshLineChartAsync();
                await RefreshDonutChartAsync();

                _headerStatusText.Text = BuildHeaderStatus(metrics, _attentionCache, mobileUpdates);
                _lastRefreshedText.Text = "Last refreshed: " + DateTime.Now.ToString("MMM dd, h:mm tt");
                _attentionSummaryText.Text = BuildAttentionSummary();
            }
            catch (Exception ex)
            {
                _openTicketsValue.Text = "--";
                _criticalTicketsValue.Text = "--";
                _todayVolumeValue.Text = "--";
                _avgResolutionValue.Text = "--";
                _mobileUpdatesValue.Text = "--";
                _criticalTicketsValue.Foreground = InkBrush;
                _mobileUpdatesValue.Foreground = InkBrush;
                _attentionCache = new List<CallTicketListItem>();
                _attentionPanel.Children.Clear();
                _attentionPanel.Children.Add(new TextBlock
                {
                    Text = "Unable to load attention items right now.",
                    Foreground = BrushFromRgb(185, 28, 28),
                    Margin = new Thickness(0, 2, 0, 0)
                });
                _headerStatusText.Text = "Load failed: " + ex.Message;
                _attentionSummaryText.Text = "Dashboard data could not be refreshed.";
                _lastRefreshedText.Text = "Last refreshed: failed";
                UpdatePagerState();
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _loadingFlag, 0);
            }
        }

        private void SetLoadingState(string message)
        {
            _openTicketsValue.Text = "--";
            _criticalTicketsValue.Text = "--";
            _todayVolumeValue.Text = "--";
            _avgResolutionValue.Text = "--";
            _mobileUpdatesValue.Text = "--";
            _criticalTicketsValue.Foreground = InkBrush;
            _mobileUpdatesValue.Foreground = InkBrush;
            _headerStatusText.Text = message;
            _lastRefreshedText.Text = "Last refreshed: pending";
            _attentionSummaryText.Text = message;
            _lineChartPoints = null;
            _attentionPanel.Children.Clear();
            _lineChartCanvas.Children.Clear();
            _donutCanvas.Children.Clear();
            _donutLegendPanel.Children.Clear();
            _attentionPanel.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 4, 0, 0)
            });
            UpdatePagerState();
        }

        private void RenderAttentionPage()
        {
            _attentionPanel.Children.Clear();

            if (_attentionCache == null || _attentionCache.Count == 0)
            {
                _attentionPanel.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(241, 247, 243)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(214, 231, 219)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14, 12, 14, 12),
                    Child = new TextBlock
                    {
                        Text = "Queue is clear — no tickets need attention right now.",
                        FontSize = 12.5,
                        Foreground = BrushFromRgb(47, 133, 90)
                    }
                });
                UpdatePagerState();
                return;
            }

            var pageItems = _attentionCache
                .Skip((_attentionPageIndex - 1) * AttentionPageSize)
                .Take(AttentionPageSize)
                .ToList();

            foreach (var item in pageItems)
            {
                _attentionPanel.Children.Add(CreateAttentionTile(item));
            }

            UpdatePagerState();
        }

        private FrameworkElement CreateAttentionTile(CallTicketListItem item)
        {
            var priority = string.IsNullOrWhiteSpace(item.Priority) ? "Unspecified" : item.Priority.Trim();
            var priorityTone = GetPriorityTone(priority);

            var card = new Border
            {
                Margin = new Thickness(0, 0, 0, 8),
                Background = Brushes.White,
                BorderBrush = HairlineBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 12, 14, 12),
                Cursor = Cursors.Hand,
                ToolTip = "Open ticket details"
            };

            var content = new StackPanel();
            card.Child = content;

            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.Children.Add(topRow);

            var titleRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleRow.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(item.TicketCode) ? "Ticket" : item.TicketCode,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = InkBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            titleRow.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(item.Branch) ? "No branch specified" : item.Branch,
                Margin = new Thickness(8, 0, 8, 0),
                FontSize = 11.5,
                Foreground = MutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            topRow.Children.Add(titleRow);

            var chipRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            chipRow.Children.Add(new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = priorityTone.Accent,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            chipRow.Children.Add(new TextBlock
            {
                Text = priority.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = priorityTone.Foreground,
                VerticalAlignment = VerticalAlignment.Center
            });

            var priorityBadge = new Border
            {
                Background = priorityTone.Background,
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(9, 3, 9, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = chipRow
            };
            Grid.SetColumn(priorityBadge, 1);
            topRow.Children.Add(priorityBadge);

            content.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(item.Issue) ? "No issue summary provided." : item.Issue,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5,
                Foreground = BodyBrush
            });

            content.Children.Add(new TextBlock
            {
                Text = BuildAttentionMetaLine(item),
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = MutedBrush
            });

            card.MouseEnter += (_, __) =>
            {
                card.Background = HoverFillBrush;
                card.BorderBrush = InputBorderBrush;
            };
            card.MouseLeave += (_, __) =>
            {
                card.Background = Brushes.White;
                card.BorderBrush = HairlineBrush;
            };
            card.MouseLeftButtonUp += async (_, __) => await OpenTicketDetailsAsync(item.TicketId);
            return card;
        }

        private static string BuildAttentionMetaLine(CallTicketListItem item)
        {
            var status = string.IsNullOrWhiteSpace(item.Status) ? "Unknown" : item.Status.Trim();
            var issueType = string.IsNullOrWhiteSpace(item.IssueType) ? "N/A" : item.IssueType.Trim();
            var caller = string.IsNullOrWhiteSpace(item.CallerName) ? "N/A" : item.CallerName.Trim();
            var age = item.TicketAgeDays + (item.TicketAgeDays == 1 ? " day old" : " days old");

            var parts = new List<string> { status, issueType, caller, age };
            if (item.IdleDays > 0)
            {
                parts.Add("idle " + item.IdleDays + (item.IdleDays == 1 ? " day" : " days"));
            }

            return string.Join("  ·  ", parts);
        }

        private HeaderBundle BuildHeaderSection()
        {
            var container = new Grid();
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel
            {
                Margin = new Thickness(0, 0, 24, 0)
            };
            container.Children.Add(left);

            left.Children.Add(CreateMicroLabelText("IT CALL MONITORING"));
            left.Children.Add(new TextBlock
            {
                Text = "Dashboard",
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 26,
                FontWeight = FontWeights.SemiBold,
                Foreground = InkBrush
            });

            var statusText = new TextBlock
            {
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = MutedBrush
            };
            left.Children.Add(statusText);

            var lastRefreshedText = new TextBlock
            {
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 11.5,
                Foreground = MutedBrush
            };
            left.Children.Add(lastRefreshedText);

            var actionRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0)
            };
            Grid.SetColumn(actionRow, 1);
            container.Children.Add(actionRow);

            var ticketsButton = CreateAccentButton("Tickets");
            ticketsButton.Click += (_, __) => _navigator?.OpenTicketWorkspace();
            actionRow.Children.Add(ticketsButton);

            var displayModeButton = CreateQuietButton("Display Mode");
            displayModeButton.Margin = new Thickness(8, 0, 0, 0);
            displayModeButton.Click += (_, __) => _navigator?.OpenDisplayMode();
            actionRow.Children.Add(displayModeButton);

            var reportsButton = CreateQuietButton("Reports");
            reportsButton.Margin = new Thickness(8, 0, 0, 0);
            reportsButton.Click += (_, __) => _navigator?.OpenReports();
            actionRow.Children.Add(reportsButton);

            var refreshButton = CreateQuietButton(string.Empty);
            refreshButton.Margin = new Thickness(8, 0, 0, 0);
            refreshButton.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock
                    {
                        Text = "\u21BB",
                        FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, -1, 7, 0)
                    },
                    new TextBlock
                    {
                        Text = "Refresh",
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            };
            refreshButton.Click += (_, __) => RefreshData();
            actionRow.Children.Add(refreshButton);

            return new HeaderBundle
            {
                Container = container,
                StatusText = statusText,
                LastRefreshedText = lastRefreshedText
            };
        }

        private static TextBlock CreateMicroLabelText(string text = null)
        {
            return new TextBlock
            {
                Text = text ?? string.Empty,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = MutedBrush,
                TextWrapping = TextWrapping.Wrap
            };
        }

        private static Button CreateQuietButton(string text)
        {
            return new Button
            {
                Content = text,
                Style = (Style)CommandStyles["ItcmQuietButton"]
            };
        }

        private static Button CreateAccentButton(string text)
        {
            return new Button
            {
                Content = text,
                Style = (Style)CommandStyles["ItcmAccentButton"]
            };
        }

        private Button CreateRangeButton(string text, int days)
        {
            var button = new Button
            {
                Content = text,
                Margin = new Thickness(8, 0, 0, 0),
                Tag = days,
                Style = (Style)CommandStyles["ItcmRangeButton"]
            };
            _rangeButtons.Add((button, days));
            button.Click += async (_, __) =>
            {
                if (_analyticsRangeDays == days)
                {
                    return;
                }

                _analyticsRangeDays = days;
                UpdateRangeButtonStates();
                UpdateAnalyticsTitles();
                if (_repository != null)
                {
                    await RefreshLineChartAsync();
                    await RefreshDonutChartAsync();
                }
            };
            return button;
        }

        private void UpdateRangeButtonStates()
        {
            foreach (var entry in _rangeButtons)
            {
                entry.Button.Style = (Style)CommandStyles[entry.Days == _analyticsRangeDays ? "ItcmRangeButtonActive" : "ItcmRangeButton"];
            }
        }

        private Button CreatePagerButton(string text, Func<Task> action)
        {
            var button = new Button
            {
                Content = text,
                Margin = new Thickness(8, 0, 0, 0),
                Style = (Style)CommandStyles["ItcmQuietButton"],
                Padding = new Thickness(10, 5, 10, 5),
                FontSize = 11.5
            };
            button.Click += async (_, __) => await action();
            return button;
        }

        private static void AddMetricCard(UniformGrid grid, string title, string subtitle, TextBlock valueBlock, Color accent, Action onClick)
        {
            var accentBrush = new SolidColorBrush(accent);

            var card = CreateCard(16);
            card.Margin = new Thickness(7);
            card.MinHeight = 112;
            card.Cursor = Cursors.Hand;
            card.ToolTip = "Click to view " + title + " details";

            var stack = new StackPanel();
            card.Child = stack;

            var labelRow = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            labelRow.Children.Add(new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = accentBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            labelRow.Children.Add(new TextBlock
            {
                Text = (title ?? string.Empty).ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = MutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            stack.Children.Add(labelRow);

            stack.Children.Add(valueBlock);

            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                Margin = new Thickness(0, 6, 0, 0),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = MutedBrush
            });

            card.MouseEnter += (_, __) =>
            {
                card.Background = HoverFillBrush;
                card.BorderBrush = accentBrush;
            };
            card.MouseLeave += (_, __) =>
            {
                card.Background = Brushes.White;
                card.BorderBrush = HairlineBrush;
            };
            card.MouseLeftButtonUp += (_, __) => onClick?.Invoke();

            grid.Children.Add(card);
        }

        private static TextBlock CreateMetricValue()
        {
            return new TextBlock
            {
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 26,
                FontWeight = FontWeights.SemiBold,
                Foreground = InkBrush
            };
        }

        private static Border CreateCard(double padding = 18)
        {
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = HairlineBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(padding)
            };
        }

        private FrameworkElement CreateQuickActionRow(string title, Action onClick)
        {
            var row = new Border
            {
                Background = Brushes.White,
                BorderBrush = HairlineBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 10, 10),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Child = grid;

            grid.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = InkBrush,
                VerticalAlignment = VerticalAlignment.Center
            });

            var chevron = new TextBlock
            {
                Text = "\u203A",
                FontSize = 16,
                Foreground = MutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, -1, 0, 0)
            };
            Grid.SetColumn(chevron, 1);
            grid.Children.Add(chevron);

            row.MouseEnter += (_, __) =>
            {
                row.Background = SoftFillBrush;
                row.BorderBrush = InputBorderBrush;
            };
            row.MouseLeave += (_, __) =>
            {
                row.Background = Brushes.White;
                row.BorderBrush = HairlineBrush;
            };
            row.MouseLeftButtonUp += (_, __) => onClick?.Invoke();
            return row;
        }

        private static string BuildHeaderStatus(CallDashboardMetrics metrics, IReadOnlyCollection<CallTicketListItem> attention, int mobileUpdates)
        {
            var busiest = attention != null && attention.Count > 0
                ? attention.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Branch))?.Branch
                : null;

            var avg = metrics.AvgResolutionMinutes.HasValue
                ? FormatResolution(metrics.AvgResolutionMinutes)
                : "Average resolution is currently unavailable.";

            if (!string.IsNullOrWhiteSpace(busiest))
            {
                return metrics.OpenTickets + " open tickets with " + metrics.CriticalTickets + " critical. " +
                       busiest + " appears in the attention queue. " +
                       mobileUpdates + " mobile updates are still pending. " + avg;
            }

            return metrics.OpenTickets + " open tickets with " + metrics.CriticalTickets + " critical. " +
                   mobileUpdates + " mobile updates are still pending. " + avg;
        }

        private async Task RefreshLineChartAsync()
        {
            if (_repository == null)
            {
                return;
            }

            _lineChartPoints = null;
            _lineChartCanvas.Children.Clear();
            UpdateAnalyticsTitles();

            var range = GetAnalyticsRangeUtc();
            var points = await _repository.GetTicketVolumeByDayAsync(range.FromUtc, range.ToUtcExclusive) ?? new List<CallTicketVolumePoint>();

            if (points.Count == 0)
            {
                _lineChartCanvas.Children.Add(new TextBlock
                {
                    Text = "No volume data for this range.",
                    FontSize = 12.5,
                    Foreground = MutedBrush,
                    Margin = new Thickness(4)
                });
                return;
            }

            _lineChartPoints = points;
            DrawLineChart();
        }

        private void RedrawLineChartIfNeeded()
        {
            if (_lineChartPoints == null || _lineChartPoints.Count == 0)
            {
                return;
            }

            if (Math.Abs(_lineChartCanvas.ActualWidth - _lineChartDrawnWidth) > 40)
            {
                DrawLineChart();
            }
        }

        private void DrawLineChart()
        {
            var points = _lineChartPoints;
            _lineChartCanvas.Children.Clear();
            if (points == null || points.Count == 0)
            {
                return;
            }

            var width = _lineChartCanvas.ActualWidth;
            if (width < 240)
            {
                width = 620;
            }

            var height = 236d;
            _lineChartCanvas.Height = height;
            _lineChartDrawnWidth = width;

            var marginLeft = 30d;
            var marginTop = 16d;
            var marginBottom = 36d;
            var marginRight = 14d;
            var plotWidth = width - marginLeft - marginRight;
            var plotHeight = height - marginTop - marginBottom;

            var max = Math.Max(1, points.Max(p => p.TicketCount));
            for (var i = 0; i < 4; i++)
            {
                var y = marginTop + (plotHeight / 3d) * i;
                _lineChartCanvas.Children.Add(new Line
                {
                    X1 = marginLeft,
                    X2 = width - marginRight,
                    Y1 = y,
                    Y2 = y,
                    Stroke = GridLineBrush,
                    StrokeThickness = 1
                });
            }

            var polyline = new Polyline
            {
                Stroke = AlertRedBrush,
                StrokeThickness = 2.4,
                StrokeLineJoin = PenLineJoin.Round
            };

            var labelStep = Math.Max(1, (int)Math.Ceiling(points.Count / 7d));
            for (var i = 0; i < points.Count; i++)
            {
                var x = marginLeft + (plotWidth * i / Math.Max(1, points.Count - 1));
                var y = marginTop + plotHeight - ((points[i].TicketCount / (double)max) * plotHeight);
                polyline.Points.Add(new Point(x, y));

                var dot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = Brushes.White,
                    Stroke = AlertRedBrush,
                    StrokeThickness = 2,
                    ToolTip = points[i].Day.ToString("ddd, MMM d") + ": " + points[i].TicketCount + " ticket" + (points[i].TicketCount == 1 ? string.Empty : "s")
                };
                Canvas.SetLeft(dot, x - 4);
                Canvas.SetTop(dot, y - 4);
                _lineChartCanvas.Children.Add(dot);

                if (i % labelStep == 0 || i == points.Count - 1)
                {
                    var label = new TextBlock
                    {
                        Text = points[i].Day.ToString("MMM d"),
                        FontSize = 10,
                        Foreground = MutedBrush
                    };
                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(label, x - (label.DesiredSize.Width / 2));
                    Canvas.SetTop(label, height - marginBottom + 11);
                    _lineChartCanvas.Children.Add(label);
                }
            }

            _lineChartCanvas.Children.Add(polyline);
        }

        private async Task RefreshDonutChartAsync()
        {
            if (_repository == null)
            {
                return;
            }

            _donutCanvas.Children.Clear();
            _donutLegendPanel.Children.Clear();
            UpdateAnalyticsTitles();

            List<(string Bucket, int TicketCount)> rows;
            if (_showResolutionDonut)
            {
                _donutToggleButton.Content = "Active Tickets";
                var range = GetAnalyticsRangeUtc();
                rows = await _repository.GetResolutionBucketDistributionAsync(range.FromUtc, range.ToUtcInclusive);
            }
            else
            {
                _donutToggleButton.Content = "Resolutions";
                rows = await _repository.GetActiveTicketStatusDistributionAsync();
            }

            rows = rows ?? new List<(string Bucket, int TicketCount)>();
            var total = Math.Max(1, rows.Sum(r => r.TicketCount));

            if (rows.Count == 0)
            {
                _donutLegendPanel.Children.Add(new TextBlock
                {
                    Text = "No data available.",
                    FontSize = 12.5,
                    Foreground = MutedBrush
                });
                return;
            }

            var centerX = 120d;
            var centerY = 120d;
            var radius = 84d;
            var innerRadius = 48d;
            var startAngle = -90d;

            foreach (var row in rows)
            {
                var sweep = 360d * row.TicketCount / total;
                var endAngle = startAngle + sweep;
                var color = _showResolutionDonut
                    ? GetResolutionBrush(row.Bucket)
                    : GetActiveStatusBrush(row.Bucket);

                var bucket = row.Bucket;
                var segment = CreateDonutSegment(centerX, centerY, radius, innerRadius, startAngle, endAngle, color);
                segment.Cursor = Cursors.Hand;
                segment.ToolTip = bucket + ": " + row.TicketCount;
                segment.MouseLeftButtonUp += async (_, __) => await OpenDonutDrillDownAsync(bucket);
                _donutCanvas.Children.Add(segment);

                var pct = (int)Math.Round((row.TicketCount * 100d) / total);
                _donutLegendPanel.Children.Add(CreateLegendRow(bucket, row.TicketCount, pct, color));
                startAngle = endAngle;
            }

            var centerCircle = new Ellipse
            {
                Width = innerRadius * 2,
                Height = innerRadius * 2,
                Fill = Brushes.White
            };
            _donutCanvas.Children.Add(centerCircle);
            Canvas.SetLeft(centerCircle, centerX - innerRadius);
            Canvas.SetTop(centerCircle, centerY - innerRadius);

            var centerLabel = new TextBlock
            {
                Text = total.ToString("N0"),
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = InkBrush
            };
            centerLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(centerLabel, centerX - (centerLabel.DesiredSize.Width / 2));
            Canvas.SetTop(centerLabel, centerY - 24);
            _donutCanvas.Children.Add(centerLabel);

            var centerCaption = new TextBlock
            {
                Text = "TOTAL",
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = MutedBrush
            };
            centerCaption.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(centerCaption, centerX - (centerCaption.DesiredSize.Width / 2));
            Canvas.SetTop(centerCaption, centerY + 8);
            _donutCanvas.Children.Add(centerCaption);
        }

        private static Polygon CreateDonutSegment(double centerX, double centerY, double radius, double innerRadius, double startAngle, double endAngle, Brush fill)
        {
            const int arcSteps = 40;
            var points = new PointCollection();

            for (var i = 0; i <= arcSteps; i++)
            {
                var t = i / (double)arcSteps;
                var a = startAngle + (endAngle - startAngle) * t;
                points.Add(PointOnCircle(centerX, centerY, radius, a));
            }

            for (var i = arcSteps; i >= 0; i--)
            {
                var t = i / (double)arcSteps;
                var a = startAngle + (endAngle - startAngle) * t;
                points.Add(PointOnCircle(centerX, centerY, innerRadius, a));
            }

            return new Polygon
            {
                Points = points,
                Fill = fill,
                Stroke = Brushes.White,
                StrokeThickness = 2
            };
        }

        private static Point PointOnCircle(double cx, double cy, double r, double angleDegrees)
        {
            var radians = angleDegrees * Math.PI / 180d;
            return new Point(cx + (r * Math.Cos(radians)), cy + (r * Math.Sin(radians)));
        }

        private FrameworkElement CreateLegendRow(string bucket, int count, int pct, Brush color)
        {
            var row = new Border
            {
                Margin = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(8, 7, 8, 7),
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(7),
                Cursor = Cursors.Hand,
                ToolTip = "Click to view " + bucket + " tickets"
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Child = grid;

            grid.Children.Add(new Border
            {
                Width = 10,
                Height = 10,
                Background = color,
                CornerRadius = new CornerRadius(999),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            var name = new TextBlock
            {
                Text = bucket,
                FontSize = 12,
                Foreground = BodyBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(name, 1);
            grid.Children.Add(name);

            var value = new TextBlock
            {
                Text = count + "  ·  " + pct + "%",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = InkBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            Grid.SetColumn(value, 2);
            grid.Children.Add(value);

            row.MouseEnter += (_, __) => row.Background = SoftFillBrush;
            row.MouseLeave += (_, __) => row.Background = Brushes.Transparent;
            row.MouseLeftButtonUp += async (_, __) => await OpenDonutDrillDownAsync(bucket);
            return row;
        }

        private static SolidColorBrush GetResolutionBrush(string bucket)
        {
            if (string.Equals(bucket, "Repair", StringComparison.OrdinalIgnoreCase))
            {
                return BrushFromRgb(59, 126, 161);
            }

            if (string.Equals(bucket, "Replacement", StringComparison.OrdinalIgnoreCase))
            {
                return BrushFromRgb(107, 92, 165);
            }

            if (string.Equals(bucket, "Temporary Replacement", StringComparison.OrdinalIgnoreCase))
            {
                return BrushFromRgb(199, 154, 46);
            }

            return BrushFromRgb(154, 161, 172);
        }

        private static SolidColorBrush GetActiveStatusBrush(string bucket)
        {
            if (string.Equals(bucket, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                return BrushFromRgb(199, 154, 46);
            }

            if (string.Equals(bucket, "In Progress", StringComparison.OrdinalIgnoreCase))
            {
                return BrushFromRgb(59, 126, 161);
            }

            if (string.Equals(bucket, "Solved", StringComparison.OrdinalIgnoreCase))
            {
                return BrushFromRgb(47, 133, 90);
            }

            return BrushFromRgb(154, 161, 172);
        }

        private static PriorityTone GetPriorityTone(string priority)
        {
            switch ((priority ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "critical":
                    return new PriorityTone(
                        BrushFromRgb(185, 35, 53),
                        BrushFromRgb(252, 235, 238),
                        BrushFromRgb(185, 35, 53));
                case "high":
                    return new PriorityTone(
                        BrushFromRgb(183, 121, 31),
                        BrushFromRgb(251, 243, 228),
                        BrushFromRgb(183, 121, 31));
                case "medium":
                    return new PriorityTone(
                        BrushFromRgb(59, 126, 161),
                        BrushFromRgb(236, 243, 248),
                        BrushFromRgb(59, 126, 161));
                default:
                    return new PriorityTone(
                        BrushFromRgb(47, 133, 90),
                        BrushFromRgb(234, 244, 238),
                        BrushFromRgb(47, 133, 90));
            }
        }

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
        {
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private async Task ShowDrillDownAsync(string title, Func<Task<List<CallTicketListItem>>> fetchAction)
        {
            if (_repository == null || fetchAction == null)
            {
                return;
            }

            try
            {
                var list = await fetchAction() ?? new List<CallTicketListItem>();
                ShowDrillDownForm(title, list);
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, "Unable to open drill-down.\n\n" + ex.Message, "Dashboard");
            }
        }

        private async Task OpenDonutDrillDownAsync(string bucket)
        {
            if (string.IsNullOrWhiteSpace(bucket) || _repository == null)
            {
                return;
            }

            try
            {
                if (_showResolutionDonut)
                {
                    var range = GetAnalyticsRangeUtc();
                    var title = bucket.Trim() + " Resolutions (" + GetAnalyticsRangeTitleText() + ")";
                    var list = await _repository.GetResolvedTicketsByResolutionBucketAsync(range.FromUtc, range.ToUtcInclusive, bucket.Trim(), maxRows: 500);
                    ShowDrillDownForm(title, list ?? new List<CallTicketListItem>());
                    return;
                }

                var activeTitle = bucket.Trim() + " Tickets (Current)";
                var activeList = await _repository.GetTicketsByActiveStatusBucketAsync(bucket.Trim(), maxRows: 500);
                ShowDrillDownForm(activeTitle, activeList ?? new List<CallTicketListItem>());
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, "Unable to open ticket drill-down.\n\n" + ex.Message, "Dashboard");
            }
        }

        private async Task OpenTicketDetailsAsync(int ticketId)
        {
            if (ticketId <= 0 || _repository == null)
                return;

            try
            {
                var dialog = new WpfTicketDetailsDialog(_repository, ticketId)
                {
                    Owner = Window.GetWindow(this)
                };
                var result = dialog.ShowDialog();
                var changed = result == true && (dialog.IsChanged || dialog.RequestedAction != TicketDetailsDialog.TicketDetailAction.None);

                if (changed)
                    await LoadAsync();
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, "Unable to open ticket details.\n\n" + ex.Message, "Dashboard");
            }
        }

        private void OpenMobileUpdatesAndRefresh()
        {
            try
            {
                if (_navigator != null)
                {
                    _navigator.OpenMobileUpdates();
                }
                else
                {
                    var owner = WinForms.Form.ActiveForm;
                    using (var form = new WinForms.Form
                    {
                        Text = "Mobile Updates",
                        StartPosition = WinForms.FormStartPosition.CenterParent,
                        Size = new System.Drawing.Size(1200, 750),
                        MinimumSize = new System.Drawing.Size(1024, 650)
                    })
                    {
                        var page = new ViewUpdatesPage { Dock = WinForms.DockStyle.Fill };
                        form.Controls.Add(page);
                        if (owner != null) form.ShowDialog(owner);
                        else form.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, "Unable to open Mobile Updates.\n\n" + ex.Message, "Dashboard");
            }
            finally
            {
                RefreshData();
            }
        }

        private void ShowDrillDownForm(string title, List<CallTicketListItem> list)
        {
            var dialog = new WpfDashboardDrillDownDialog(title, list)
            {
                Owner = Window.GetWindow(this)
            };
            if (dialog.ShowDialog() == true && dialog.RequestedTicketId.HasValue)
            {
                OpenTicketDetailsAsync(dialog.RequestedTicketId.Value)
                    .FireAndForget(ex => WpfItcmDialogService.ShowError(this, ex.Message, "Ticket Details"));
            }
        }

        private (DateTime FromUtc, DateTime ToUtcInclusive, DateTime ToUtcExclusive) GetAnalyticsRangeUtc()
        {
            var toUtcExclusive = DateTime.UtcNow.Date.AddDays(1);
            var fromUtc = toUtcExclusive.AddDays(-_analyticsRangeDays);
            return (fromUtc, toUtcExclusive.AddTicks(-1), toUtcExclusive);
        }

        private string GetAnalyticsRangeTitleText()
        {
            return _analyticsRangeDays == 1 ? "LAST 1 DAY" : "LAST " + _analyticsRangeDays + " DAYS";
        }

        private void UpdateAnalyticsTitles()
        {
            if (_lineChartTitle != null)
            {
                _lineChartTitle.Text = "TICKET VOLUME (" + (_analyticsRangeDays == 1 ? "LAST 1 DAY" : "LAST " + _analyticsRangeDays + " DAYS") + ")";
            }

            if (_donutChartTitle != null)
            {
                _donutChartTitle.Text = _showResolutionDonut
                    ? "RESOLUTIONS (" + (_analyticsRangeDays == 1 ? "LAST 1 DAY" : "LAST " + _analyticsRangeDays + " DAYS") + ")"
                    : "ACTIVE TICKETS (CURRENT)";
            }
        }

        private int GetAttentionTotalPages()
        {
            if (_attentionCache == null || _attentionCache.Count == 0)
            {
                return 1;
            }

            return (int)Math.Ceiling(_attentionCache.Count / (double)AttentionPageSize);
        }

        private void UpdatePagerState()
        {
            var totalPages = GetAttentionTotalPages();
            _attentionPrevButton.IsEnabled = _attentionCache != null && _attentionCache.Count > 0 && _attentionPageIndex > 1;
            _attentionNextButton.IsEnabled = _attentionCache != null && _attentionCache.Count > 0 && _attentionPageIndex < totalPages;
        }

        private string BuildAttentionSummary()
        {
            if (_attentionCache == null || _attentionCache.Count == 0)
            {
                return "No tickets are currently flagged for extra attention.";
            }

            var totalPages = GetAttentionTotalPages();
            return _attentionCache.Count + " attention item" + (_attentionCache.Count == 1 ? string.Empty : "s") +
                   " currently surfaced. Page " + _attentionPageIndex + " of " + totalPages + ".";
        }

        private static string FormatResolution(int? avgResolutionMinutes)
        {
            if (!avgResolutionMinutes.HasValue)
            {
                return "N/A";
            }

            var minutes = avgResolutionMinutes.Value;
            var hours = minutes / 60;
            var mins = minutes % 60;
            if (hours > 0)
            {
                return hours + "h " + mins + "m";
            }

            return mins + "m";
        }

        private sealed class HeaderBundle
        {
            public Grid Container { get; set; }
            public TextBlock StatusText { get; set; }
            public TextBlock LastRefreshedText { get; set; }
        }

        private sealed class PriorityTone
        {
            public PriorityTone(SolidColorBrush accent, SolidColorBrush background, SolidColorBrush foreground)
            {
                Accent = accent;
                Background = background;
                Foreground = foreground;
            }

            public SolidColorBrush Accent { get; }
            public SolidColorBrush Background { get; }
            public SolidColorBrush Foreground { get; }
        }
    }
}
