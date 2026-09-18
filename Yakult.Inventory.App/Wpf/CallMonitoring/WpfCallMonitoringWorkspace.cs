using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
        private readonly TextBlock _openTicketsValue;
        private readonly TextBlock _criticalTicketsValue;
        private readonly TextBlock _todayVolumeValue;
        private readonly TextBlock _avgResolutionValue;
        private readonly TextBlock _mobileUpdatesValue;
        private readonly TextBlock _heroStatusText;
        private readonly TextBlock _lastRefreshedText;
        private readonly TextBlock _attentionSummaryText;
        private readonly TextBlock _lineChartTitle;
        private readonly TextBlock _donutChartTitle;
        private readonly Canvas _lineChartCanvas;
        private readonly StackPanel _lineChartLabels;
        private readonly Canvas _donutCanvas;
        private readonly StackPanel _donutLegendPanel;
        private readonly Button _donutToggleButton;
        private readonly StackPanel _attentionPanel;
        private readonly Button _attentionPrevButton;
        private readonly Button _attentionNextButton;

        private ICallMonitoringRepository _repository;
        private ICallMonitoringNavigator _navigator;
        private bool _initialized;
        private int _loadingFlag; // 0 = idle, 1 = loading (thread-safe via Interlocked)
        private bool _showResolutionDonut = true;
        private int _analyticsRangeDays = 7;
        private int _attentionPageIndex = 1;
        private const int AttentionPageSize = 8;
        private List<CallTicketListItem> _attentionCache = new List<CallTicketListItem>();

        public WpfCallMonitoringWorkspace()
        {
            Background = new SolidColorBrush(Color.FromRgb(241, 244, 247));

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var root = new StackPanel
            {
                Margin = new Thickness(28, 24, 28, 28)
            };
            scroll.Content = root;

            var hero = BuildHeroSection();
            root.Children.Add(hero.Container);
            _heroStatusText = hero.StatusText;
            _lastRefreshedText = hero.LastRefreshedText;

            var metricGrid = new Grid
            {
                Margin = new Thickness(0, 22, 0, 0)
            };
            for (var i = 0; i < 5; i++)
            {
                metricGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            _openTicketsValue = CreateMetricValue();
            _criticalTicketsValue = CreateMetricValue();
            _todayVolumeValue = CreateMetricValue();
            _avgResolutionValue = CreateMetricValue();
            _mobileUpdatesValue = CreateMetricValue();

            AddMetricCard(metricGrid, 0, "Open Tickets", "Active now", _openTicketsValue, Color.FromRgb(14, 116, 144),
                async () => await ShowDrillDownAsync("Open Tickets", () => _repository.GetOpenTicketsListAsync()));
            AddMetricCard(metricGrid, 1, "Critical", "Requires attention", _criticalTicketsValue, Color.FromRgb(190, 24, 93),
                async () => await ShowDrillDownAsync("Critical Tickets", () => _repository.GetCriticalTicketsListAsync()));
            AddMetricCard(metricGrid, 2, "Today's Volume", "Tickets today", _todayVolumeValue, Color.FromRgb(37, 99, 235),
                async () => await ShowDrillDownAsync("Ticket Volume Today", () => _repository.GetTodaysTicketsListAsync()));
            AddMetricCard(metricGrid, 3, "Avg Resolution", "Recent solved tickets", _avgResolutionValue, Color.FromRgb(22, 163, 74),
                async () => await ShowDrillDownAsync("Example Resolved", () => _repository.GetRecentlySolvedAsync()));
            AddMetricCard(metricGrid, 4, "Mobile Updates", "Needs processing", _mobileUpdatesValue, Color.FromRgb(245, 158, 11),
                OpenMobileUpdatesAndRefresh);
            root.Children.Add(metricGrid);

            var analyticsGrid = new Grid
            {
                Margin = new Thickness(0, 22, 0, 0)
            };
            analyticsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            analyticsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lineChartCard = CreateGlassCard();
            Grid.SetColumn(lineChartCard, 0);
            analyticsGrid.Children.Add(lineChartCard);

            var lineChartStack = new StackPanel();
            lineChartCard.Child = lineChartStack;

            var lineHeader = new Grid();
            lineHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            lineHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            lineChartStack.Children.Add(lineHeader);

            _lineChartTitle = new TextBlock
            {
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(32, 42, 55)
            };
            lineHeader.Children.Add(_lineChartTitle);

            var rangeButtons = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right
            };
            rangeButtons.Children.Add(CreateRangeButton("7 Days", 7));
            rangeButtons.Children.Add(CreateRangeButton("30 Days", 30));
            rangeButtons.Children.Add(CreateRangeButton("90 Days", 90));
            Grid.SetColumn(rangeButtons, 1);
            lineHeader.Children.Add(rangeButtons);

            lineChartStack.Children.Add(new TextBlock
            {
                Text = "Daily ticket volume, using the same repository data as the original dashboard.",
                Margin = new Thickness(0, 6, 0, 14),
                FontSize = 12.5,
                Foreground = BrushFromRgb(98, 113, 128)
            });
            _lineChartCanvas = new Canvas
            {
                Height = 240,
                Background = new SolidColorBrush(Color.FromRgb(250, 252, 255))
            };
            lineChartStack.Children.Add(_lineChartCanvas);
            _lineChartLabels = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 12, 0, 0)
            };
            lineChartStack.Children.Add(_lineChartLabels);

            var donutCard = CreateGlassCard();
            donutCard.Margin = new Thickness(18, 0, 0, 0);
            Grid.SetColumn(donutCard, 1);
            analyticsGrid.Children.Add(donutCard);

            var donutStack = new StackPanel();
            donutCard.Child = donutStack;

            var donutHeader = new Grid();
            donutHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            donutHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            donutStack.Children.Add(donutHeader);

            _donutChartTitle = new TextBlock
            {
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(32, 42, 55)
            };
            donutHeader.Children.Add(_donutChartTitle);

            _donutToggleButton = new Button
            {
                Padding = new Thickness(12, 7, 12, 7),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(210, 219, 230)),
                BorderThickness = new Thickness(1),
                Foreground = BrushFromRgb(37, 99, 235),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            _donutToggleButton.Click += async (_, __) =>
            {
                _showResolutionDonut = !_showResolutionDonut;
                await RefreshDonutChartAsync();
            };
            Grid.SetColumn(_donutToggleButton, 1);
            donutHeader.Children.Add(_donutToggleButton);

            donutStack.Children.Add(new TextBlock
            {
                Text = "Click a legend row or chart slice to open the same kind of drill-down the original dashboard provided.",
                Margin = new Thickness(0, 6, 0, 14),
                FontSize = 12.5,
                Foreground = BrushFromRgb(98, 113, 128)
            });
            _donutCanvas = new Canvas
            {
                Width = 240,
                Height = 240,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            donutStack.Children.Add(_donutCanvas);
            _donutLegendPanel = new StackPanel
            {
                Margin = new Thickness(0, 14, 0, 0)
            };
            donutStack.Children.Add(_donutLegendPanel);

            root.Children.Add(analyticsGrid);

            var lowerGrid = new Grid
            {
                Margin = new Thickness(0, 22, 0, 0)
            };
            lowerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.45, GridUnitType.Star) });
            lowerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var attentionCard = CreateGlassCard();
            Grid.SetColumn(attentionCard, 0);
            lowerGrid.Children.Add(attentionCard);

            var attentionLayout = new DockPanel();
            attentionCard.Child = attentionLayout;

            var attentionHeaderGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 14)
            };
            attentionHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            attentionHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            DockPanel.SetDock(attentionHeaderGrid, Dock.Top);
            attentionLayout.Children.Add(attentionHeaderGrid);

            var attentionHeader = new StackPanel();
            attentionHeader.Children.Add(new TextBlock
            {
                Text = "Attention Queue",
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(32, 42, 55)
            });
            attentionHeader.Children.Add(new TextBlock
            {
                Text = "These are real tickets that need action. Click any card to open the ticket details dialog.",
                Margin = new Thickness(0, 6, 0, 0),
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = BrushFromRgb(98, 113, 128)
            });
            _attentionSummaryText = new TextBlock
            {
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 11.5,
                Foreground = BrushFromRgb(17, 94, 89)
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

            var sideRail = CreateGlassCard();
            sideRail.Margin = new Thickness(18, 0, 0, 0);
            Grid.SetColumn(sideRail, 1);
            lowerGrid.Children.Add(sideRail);

            var sideLayout = new StackPanel();
            sideRail.Child = sideLayout;

            var pillRow = new WrapPanel
            {
                Margin = new Thickness(0, 0, 0, 14)
            };
            pillRow.Children.Add(CreatePill("WPF Dashboard", Color.FromRgb(15, 118, 110), Color.FromRgb(240, 253, 250)));
            pillRow.Children.Add(CreatePill("WinForms Shell", Color.FromRgb(55, 65, 81), Color.FromRgb(243, 244, 246)));
            pillRow.Children.Add(CreatePill("Original Data Restored", Color.FromRgb(29, 78, 216), Color.FromRgb(239, 246, 255)));
            sideLayout.Children.Add(pillRow);

            sideLayout.Children.Add(new TextBlock
            {
                Text = "Quick Navigation",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(32, 42, 55)
            });
            sideLayout.Children.Add(new TextBlock
            {
                Text = "The dashboard keeps the modern shell, but routes back into the working WinForms workflows whenever you need to process or review records.",
                Margin = new Thickness(0, 8, 0, 18),
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = BrushFromRgb(98, 113, 128)
            });

            sideLayout.Children.Add(CreateActionTile(
                "Open Ticket Workspace",
                "Continue into the main ticket processing screen.",
                Color.FromRgb(14, 116, 144),
                () => _navigator?.OpenTicketWorkspace()));
            sideLayout.Children.Add(CreateActionTile(
                "Open Display Mode",
                "Switch to the live operations-focused ticket wall.",
                Color.FromRgb(126, 34, 206),
                () => _navigator?.OpenDisplayMode()));
            sideLayout.Children.Add(CreateActionTile(
                "Open Reports",
                "Jump into analytics and longer-range reporting.",
                Color.FromRgb(22, 101, 52),
                () => _navigator?.OpenReports()));
            sideLayout.Children.Add(CreateActionTile(
                "Open Mobile Updates",
                "Review and process mobile-originated set updates.",
                Color.FromRgb(245, 158, 11),
                OpenMobileUpdatesAndRefresh));

            root.Children.Add(lowerGrid);
            Content = scroll;

            UpdateAnalyticsTitles();
            SetLoadingState("Preparing the WPF dashboard...");
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
            LoadAsync().FireAndForget(ex => Dispatcher.BeginInvoke(new Action(() => _heroStatusText.Text = "Dashboard load failed: " + ex.Message)));
        }

        public void RefreshData()
        {
            if (_repository == null)
            {
                return;
            }

            LoadAsync().FireAndForget(ex => Dispatcher.BeginInvoke(new Action(() => _heroStatusText.Text = "Dashboard refresh failed: " + ex.Message)));
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
                _todayVolumeValue.Text = metrics.TodaysVolume.ToString("N0");
                _avgResolutionValue.Text = FormatResolution(metrics.AvgResolutionMinutes);
                _mobileUpdatesValue.Text = mobileUpdates.ToString("N0");
                _mobileUpdatesValue.Foreground = mobileUpdates > 0 ? BrushFromRgb(220, 38, 38) : BrushFromRgb(15, 23, 42);

                RenderAttentionPage();
                await RefreshLineChartAsync();
                await RefreshDonutChartAsync();

                _heroStatusText.Text = BuildHeroStatus(metrics, _attentionCache, mobileUpdates);
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
                _attentionCache = new List<CallTicketListItem>();
                _attentionPanel.Children.Clear();
                _attentionPanel.Children.Add(new TextBlock
                {
                    Text = "Unable to load attention items right now.",
                    Foreground = BrushFromRgb(185, 28, 28),
                    Margin = new Thickness(0, 2, 0, 0)
                });
                _heroStatusText.Text = "Load failed: " + ex.Message;
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
            _mobileUpdatesValue.Foreground = BrushFromRgb(15, 23, 42);
            _heroStatusText.Text = message;
            _lastRefreshedText.Text = "Last refreshed: pending";
            _attentionSummaryText.Text = message;
            _attentionPanel.Children.Clear();
            _lineChartCanvas.Children.Clear();
            _lineChartLabels.Children.Clear();
            _donutCanvas.Children.Clear();
            _donutLegendPanel.Children.Clear();
            _attentionPanel.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = BrushFromRgb(98, 113, 128),
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
                    Background = new SolidColorBrush(Color.FromRgb(240, 253, 244)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(187, 247, 208)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(16, 14, 16, 14),
                    Child = new TextBlock
                    {
                        Text = "No active alerts. The queue is clear right now.",
                        FontSize = 12.5,
                        Foreground = BrushFromRgb(22, 101, 52)
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

            var button = new Button
            {
                Margin = new Thickness(0, 0, 0, 14),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            var outer = new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(225, 231, 238)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16)
            };
            button.Content = outer;

            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outer.Child = root;

            root.Children.Add(new Border
            {
                Background = priorityTone.Accent,
                CornerRadius = new CornerRadius(999)
            });

            var content = new StackPanel
            {
                Margin = new Thickness(14, 0, 0, 0)
            };
            Grid.SetColumn(content, 1);
            root.Children.Add(content);

            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.Children.Add(topRow);

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(item.TicketCode) ? "Ticket" : item.TicketCode,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(30, 41, 59)
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(item.Branch) ? "No branch specified" : item.Branch,
                Margin = new Thickness(0, 3, 0, 0),
                FontSize = 11.5,
                Foreground = BrushFromRgb(100, 116, 139)
            });
            topRow.Children.Add(titleStack);

            var priorityBadge = new Border
            {
                Background = priorityTone.Background,
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(10, 5, 10, 5),
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = priority.ToUpperInvariant(),
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = priorityTone.Foreground
                }
            };
            Grid.SetColumn(priorityBadge, 1);
            topRow.Children.Add(priorityBadge);

            content.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(item.Issue) ? "No issue summary provided." : item.Issue,
                Margin = new Thickness(0, 12, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = BrushFromRgb(71, 85, 105)
            });

            var metaWrap = new WrapPanel
            {
                Margin = new Thickness(0, 14, 0, 0)
            };
            metaWrap.Children.Add(CreateMetaChip("Status", string.IsNullOrWhiteSpace(item.Status) ? "Unknown" : item.Status));
            metaWrap.Children.Add(CreateMetaChip("Caller", string.IsNullOrWhiteSpace(item.CallerName) ? "N/A" : item.CallerName));
            metaWrap.Children.Add(CreateMetaChip("Issue Type", string.IsNullOrWhiteSpace(item.IssueType) ? "N/A" : item.IssueType));
            metaWrap.Children.Add(CreateMetaChip("Age", item.TicketAgeDays + " day" + (item.TicketAgeDays == 1 ? string.Empty : "s")));
            if (item.IdleDays > 0)
            {
                metaWrap.Children.Add(CreateMetaChip("Idle", item.IdleDays + " day" + (item.IdleDays == 1 ? string.Empty : "s")));
            }
            content.Children.Add(metaWrap);

            button.Click += async (_, __) => await OpenTicketDetailsAsync(item.TicketId);
            return button;
        }

        private FrameworkElement CreateMetaChip(string label, string value)
        {
            return new Border
            {
                Margin = new Thickness(0, 0, 8, 8),
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(10, 6, 10, 6),
                Child = new TextBlock
                {
                    Text = label + ": " + value,
                    FontSize = 11,
                    Foreground = BrushFromRgb(71, 85, 105)
                }
            };
        }

        private HeroBundle BuildHeroSection()
        {
            var heroBorder = new Border
            {
                CornerRadius = new CornerRadius(28),
                Padding = new Thickness(28),
                Background = new LinearGradientBrush(
                    Color.FromRgb(15, 118, 110),
                    Color.FromRgb(21, 94, 117),
                    new Point(0, 0),
                    new Point(1, 1)),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 24,
                    Color = Color.FromArgb(70, 15, 23, 42),
                    ShadowDepth = 0,
                    Opacity = 0.35
                }
            };

            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            heroBorder.Child = layout;

            var left = new StackPanel();
            layout.Children.Add(left);

            left.Children.Add(new TextBlock
            {
                Text = "Call Monitoring Dashboard",
                FontSize = 30,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });
            left.Children.Add(new TextBlock
            {
                Text = "A modern WPF shell that still behaves like the original dashboard instead of losing the working interactions behind the redesign.",
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(228, 236, 253, 250))
            });

            var statusText = new TextBlock
            {
                Margin = new Thickness(0, 18, 0, 0),
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255))
            };
            left.Children.Add(statusText);

            var lastRefreshedText = new TextBlock
            {
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 240, 255, 255))
            };
            left.Children.Add(lastRefreshedText);

            var actionRow = new WrapPanel
            {
                Margin = new Thickness(0, 18, 0, 0)
            };
            actionRow.Children.Add(CreateHeroButton("Tickets", () => _navigator?.OpenTicketWorkspace()));
            actionRow.Children.Add(CreateHeroButton("Display Mode", () => _navigator?.OpenDisplayMode()));
            actionRow.Children.Add(CreateHeroButton("Reports", () => _navigator?.OpenReports()));
            actionRow.Children.Add(CreateHeroButton("Refresh", () => RefreshData()));
            left.Children.Add(actionRow);

            var right = new Border
            {
                Margin = new Thickness(22, 0, 0, 0),
                Padding = new Thickness(18),
                CornerRadius = new CornerRadius(24),
                Background = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(65, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };
            Grid.SetColumn(right, 1);
            layout.Children.Add(right);

            var rightStack = new StackPanel();
            right.Child = rightStack;
            rightStack.Children.Add(new TextBlock
            {
                Text = "Working Features",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });
            rightStack.Children.Add(CreateHeroFact("Chart parity", "Range-aware line chart plus resolution and active-ticket donut views."));
            rightStack.Children.Add(CreateHeroFact("Real drill-downs", "Cards and legend rows open the same data-backed ticket lists."));
            rightStack.Children.Add(CreateHeroFact("Ticket actions", "Attention cards open the existing WinForms ticket details dialog."));

            return new HeroBundle
            {
                Container = heroBorder,
                StatusText = statusText,
                LastRefreshedText = lastRefreshedText
            };
        }

        private FrameworkElement CreateHeroFact(string title, string text)
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(0, 14, 0, 0)
            };
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });
            panel.Children.Add(new TextBlock
            {
                Text = text,
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 240, 255, 255))
            });
            return panel;
        }

        private Button CreateHeroButton(string text, Action onClick)
        {
            var button = new Button
            {
                Content = text,
                Margin = new Thickness(0, 0, 10, 10),
                Padding = new Thickness(16, 10, 16, 10),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(44, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(82, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            button.Click += (_, __) => onClick?.Invoke();
            return button;
        }

        private Button CreateRangeButton(string text, int days)
        {
            var button = new Button
            {
                Content = text,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(37, 99, 235),
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(210, 219, 230)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Tag = days
            };
            button.Click += async (_, __) =>
            {
                if (_analyticsRangeDays == days)
                {
                    return;
                }

                _analyticsRangeDays = days;
                UpdateAnalyticsTitles();
                if (_repository != null)
                {
                    await RefreshLineChartAsync();
                    await RefreshDonutChartAsync();
                }
            };
            return button;
        }

        private Button CreatePagerButton(string text, Func<Task> action)
        {
            var button = new Button
            {
                Content = text,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(12, 7, 12, 7),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(210, 219, 230)),
                BorderThickness = new Thickness(1),
                Foreground = BrushFromRgb(51, 65, 85),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            button.Click += async (_, __) => await action();
            return button;
        }

        private static void AddMetricCard(Grid grid, int column, string title, string subtitle, TextBlock valueBlock, Color accent, Action onClick)
        {
            var button = new Button
            {
                Margin = column == 4 ? new Thickness(18, 0, 0, 0) : new Thickness(0, 0, 18, 0),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            Grid.SetColumn(button, column);
            grid.Children.Add(button);

            var card = CreateGlassCard();
            button.Content = card;

            var stack = new StackPanel();
            card.Child = stack;

            stack.Children.Add(new Border
            {
                Width = 54,
                Height = 5,
                Background = new SolidColorBrush(accent),
                CornerRadius = new CornerRadius(999)
            });
            stack.Children.Add(new TextBlock
            {
                Text = title,
                Margin = new Thickness(0, 16, 0, 4),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(32, 42, 55)
            });
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 11.5,
                Foreground = BrushFromRgb(100, 116, 139)
            });
            stack.Children.Add(valueBlock);

            button.Click += (_, __) => onClick?.Invoke();
        }

        private static TextBlock CreateMetricValue()
        {
            return new TextBlock
            {
                Margin = new Thickness(0, 16, 0, 0),
                FontSize = 30,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42)
            };
        }

        private static Border CreateGlassCard()
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
                    Color = Color.FromArgb(30, 15, 23, 42),
                    ShadowDepth = 0,
                    Opacity = 0.2
                }
            };
        }

        private FrameworkElement CreatePill(string text, Color foreground, Color background)
        {
            return new Border
            {
                Margin = new Thickness(0, 0, 8, 8),
                Background = new SolidColorBrush(background),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(10, 6, 10, 6),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(foreground)
                }
            };
        }

        private FrameworkElement CreateActionTile(string title, string subtitle, Color accent, Action onClick)
        {
            var button = new Button
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(222, 228, 235)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(16)
            };
            button.Content = border;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            border.Child = grid;

            grid.Children.Add(new Border
            {
                Background = new SolidColorBrush(accent),
                CornerRadius = new CornerRadius(999)
            });

            var stack = new StackPanel
            {
                Margin = new Thickness(14, 0, 0, 0)
            };
            Grid.SetColumn(stack, 1);
            grid.Children.Add(stack);

            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(30, 41, 59)
            });
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = BrushFromRgb(100, 116, 139)
            });

            button.Click += (_, __) => onClick?.Invoke();
            return button;
        }

        private static string BuildHeroStatus(CallDashboardMetrics metrics, IReadOnlyCollection<CallTicketListItem> attention, int mobileUpdates)
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

            _lineChartCanvas.Children.Clear();
            _lineChartLabels.Children.Clear();
            UpdateAnalyticsTitles();

            var range = GetAnalyticsRangeUtc();
            var points = await _repository.GetTicketVolumeByDayAsync(range.FromUtc, range.ToUtcExclusive) ?? new List<CallTicketVolumePoint>();

            if (points.Count == 0)
            {
                _lineChartCanvas.Children.Add(new TextBlock
                {
                    Text = "No volume data.",
                    Foreground = BrushFromRgb(100, 116, 139)
                });
                return;
            }

            var width = 620d;
            var height = 220d;
            _lineChartCanvas.Width = width;
            _lineChartCanvas.Height = height;

            var marginLeft = 34d;
            var marginTop = 18d;
            var marginBottom = 30d;
            var marginRight = 18d;
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
                    Stroke = new SolidColorBrush(Color.FromRgb(231, 236, 242)),
                    StrokeThickness = 1
                });
            }

            _lineChartCanvas.Children.Add(new Line
            {
                X1 = marginLeft,
                X2 = marginLeft,
                Y1 = marginTop,
                Y2 = height - marginBottom,
                Stroke = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                StrokeThickness = 1
            });
            _lineChartCanvas.Children.Add(new Line
            {
                X1 = marginLeft,
                X2 = width - marginRight,
                Y1 = height - marginBottom,
                Y2 = height - marginBottom,
                Stroke = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                StrokeThickness = 1
            });

            var polyline = new Polyline
            {
                Stroke = BrushFromRgb(14, 116, 144),
                StrokeThickness = 3,
                StrokeLineJoin = PenLineJoin.Round
            };

            for (var i = 0; i < points.Count; i++)
            {
                var x = marginLeft + (plotWidth * i / Math.Max(1, points.Count - 1));
                var y = marginTop + plotHeight - ((points[i].TicketCount / (double)max) * plotHeight);
                polyline.Points.Add(new Point(x, y));

                var ellipse = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = BrushFromRgb(14, 116, 144),
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    ToolTip = points[i].Day.ToString("ddd") + ": " + points[i].TicketCount
                };
                Canvas.SetLeft(ellipse, x - 5);
                Canvas.SetTop(ellipse, y - 5);
                _lineChartCanvas.Children.Add(ellipse);

                var dayLabel = new Border
                {
                    Margin = new Thickness(i == 0 ? 0 : 10, 0, 0, 0),
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(999),
                    Padding = new Thickness(10, 6, 10, 6),
                    Child = new TextBlock
                    {
                        Text = points[i].Day.ToString("MMM dd") + "  " + points[i].TicketCount,
                        FontSize = 11,
                        Foreground = BrushFromRgb(71, 85, 105)
                    }
                };
                _lineChartLabels.Children.Add(dayLabel);
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
                    Foreground = BrushFromRgb(100, 116, 139)
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
                Text = total.ToString(),
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(30, 41, 59)
            };
            centerLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(centerLabel, centerX - (centerLabel.DesiredSize.Width / 2));
            Canvas.SetTop(centerLabel, centerY - 18);
            _donutCanvas.Children.Add(centerLabel);
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
            var button = new Button
            {
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            button.Content = grid;

            grid.Children.Add(new Border
            {
                Width = 12,
                Height = 12,
                Background = color,
                CornerRadius = new CornerRadius(999),
                Margin = new Thickness(0, 5, 10, 0)
            });

            var name = new TextBlock
            {
                Text = bucket,
                FontSize = 11.5,
                Foreground = BrushFromRgb(71, 85, 105)
            };
            Grid.SetColumn(name, 1);
            grid.Children.Add(name);

            var value = new TextBlock
            {
                Text = count + " (" + pct + "%)",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(30, 41, 59)
            };
            Grid.SetColumn(value, 2);
            grid.Children.Add(value);

            button.Click += async (_, __) => await OpenDonutDrillDownAsync(bucket);
            return button;
        }

        private static SolidColorBrush GetResolutionBrush(string bucket)
        {
            if (string.Equals(bucket, "Repair", StringComparison.OrdinalIgnoreCase))
            {
                return BrushFromRgb(59, 130, 246);
            }

            if (string.Equals(bucket, "Replacement", StringComparison.OrdinalIgnoreCase))
            {
                return BrushFromRgb(139, 92, 246);
            }

            if (string.Equals(bucket, "Temporary Replacement", StringComparison.OrdinalIgnoreCase))
            {
                return BrushFromRgb(245, 158, 11);
            }

            return BrushFromRgb(156, 163, 175);
        }

        private static SolidColorBrush GetActiveStatusBrush(string bucket)
        {
            if (string.Equals(bucket, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                return BrushFromRgb(245, 158, 11);
            }

            if (string.Equals(bucket, "In Progress", StringComparison.OrdinalIgnoreCase))
            {
                return BrushFromRgb(59, 130, 246);
            }

            if (string.Equals(bucket, "Solved", StringComparison.OrdinalIgnoreCase))
            {
                return BrushFromRgb(16, 185, 129);
            }

            return BrushFromRgb(156, 163, 175);
        }

        private static PriorityTone GetPriorityTone(string priority)
        {
            switch ((priority ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "critical":
                    return new PriorityTone(
                        BrushFromRgb(190, 24, 93),
                        new SolidColorBrush(Color.FromRgb(253, 242, 248)),
                        BrushFromRgb(157, 23, 77));
                case "high":
                    return new PriorityTone(
                        BrushFromRgb(234, 88, 12),
                        new SolidColorBrush(Color.FromRgb(255, 247, 237)),
                        BrushFromRgb(154, 52, 18));
                case "medium":
                    return new PriorityTone(
                        BrushFromRgb(37, 99, 235),
                        new SolidColorBrush(Color.FromRgb(239, 246, 255)),
                        BrushFromRgb(29, 78, 216));
                default:
                    return new PriorityTone(
                        BrushFromRgb(22, 163, 74),
                        new SolidColorBrush(Color.FromRgb(240, 253, 244)),
                        BrushFromRgb(21, 128, 61));
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
                _lineChartTitle.Text = "Ticket Volume (" + (_analyticsRangeDays == 1 ? "Last 1 Day" : "Last " + _analyticsRangeDays + " Days") + ")";
            }

            if (_donutChartTitle != null)
            {
                _donutChartTitle.Text = _showResolutionDonut
                    ? "Resolutions (" + (_analyticsRangeDays == 1 ? "Last 1 Day" : "Last " + _analyticsRangeDays + " Days") + ")"
                    : "Active Tickets (Current)";
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

        private sealed class HeroBundle
        {
            public Border Container { get; set; }
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
