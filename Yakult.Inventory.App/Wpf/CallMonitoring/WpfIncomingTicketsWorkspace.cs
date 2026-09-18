using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed class WpfIncomingTicketsWorkspace : UserControl
    {
        private ICallMonitoringRepository _repository;
        private ICallMonitoringNavigator _navigator;
        private int _loading;
        private bool _initialized;

        // --- Data ---
        private ObservableCollection<CallTicketListItem> _items = new ObservableCollection<CallTicketListItem>();
        private CallMonitoringRepository.IncomingTicketsSummary _summary;

        // --- Controls ---
        private ScrollViewer _rootScroll;
        private StackPanel _root;

        // Hero
        private Border _heroBorder;
        private TextBlock _heroTitle;
        private TextBlock _heroSummary;
        private TextBlock _heroLastRefreshed;
        private TextBlock _heroTotalCount;
        private TextBlock _heroCriticalCount;
        private TextBlock _heroHighCount;
        private TextBlock _heroOldTicketsCount;

        // Filter bar
        private TextBox _searchBox;
        private ComboBox _statusFilter;
        private ComboBox _priorityFilter;
        private CheckBox _chkUnassigned;
        private WrapPanel _quickFilterPanel;
        private Border _activeQuickFilter;
        private TextBlock _activeFilterSummary;
        private bool _suppressFilterEvents;

        // DataGrid
        private DataGrid _grid;
        private Border _gridState;
        private TextBlock _gridStateTitle;
        private TextBlock _gridStateDetail;
        private Button _gridStateRetry;
        private Border _pagerBar;

        // Persistent inspector
        private Border _inspectorCard;
        private Border _inspectorEmptyState;
        private ScrollViewer _inspectorScroll;
        private TextBlock _inspectorTicketCode;
        private TextBlock _inspectorIssue;
        private TextBlock _inspectorStatus;
        private TextBlock _inspectorRequester;
        private TextBlock _inspectorLocation;
        private TextBlock _inspectorOwner;
        private TextBlock _inspectorCreated;
        private TextBlock _inspectorActivity;
        private TextBlock _inspectorTimeline;
        private ComboBox _inspectorPriority;
        private Button _inspectorAssignButton;
        private Button _inspectorStartButton;
        private Button _inspectorResolveButton;
        private Button _inspectorPriorityButton;
        private CallTicketListItem _selectedTicket;
        private int _inspectorLoadVersion;

        // Pager
        private Button _prevBtn;
        private Button _nextBtn;
        private TextBlock _pageInfo;
        private ComboBox _pageSizeCombo;
        private int _currentPage = 1;
        private int _pageSize = 25;
        private int _totalCount;
        private bool _hasNext;

        // Bulk action bar
        private Border _bulkBar;

        // State for quick filters
        private string _quickFilterStatus;
        private string _quickFilterPriority;
        private bool _quickFilterUnassigned;

        // Colors
        private static readonly SolidColorBrush ColorTeal = new SolidColorBrush(Color.FromRgb(0, 150, 136));
        private static readonly SolidColorBrush ColorTealDark = new SolidColorBrush(Color.FromRgb(15, 118, 110));
        private static readonly SolidColorBrush ColorWhite = Brushes.White;
        private static readonly SolidColorBrush ColorTextPrimary = new SolidColorBrush(Color.FromRgb(30, 41, 59));
        private static readonly SolidColorBrush ColorTextSecondary = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        private static readonly SolidColorBrush ColorBackground = new SolidColorBrush(Color.FromRgb(241, 244, 247));
        private static readonly SolidColorBrush ColorCardBg = Brushes.White;
        private static readonly SolidColorBrush ColorAccentBlue = new SolidColorBrush(Color.FromRgb(52, 152, 219));
        private static readonly SolidColorBrush ColorAccentGreen = new SolidColorBrush(Color.FromRgb(46, 204, 113));
        private static readonly SolidColorBrush ColorAccentRed = new SolidColorBrush(Color.FromRgb(231, 76, 60));
        private static readonly SolidColorBrush ColorAccentOrange = new SolidColorBrush(Color.FromRgb(230, 126, 34));
        private static readonly SolidColorBrush ColorAccentPurple = new SolidColorBrush(Color.FromRgb(155, 89, 182));
        private static readonly SolidColorBrush ColorBorder = new SolidColorBrush(Color.FromRgb(222, 226, 230));
        private static readonly SolidColorBrush ColorRowAlt = new SolidColorBrush(Color.FromRgb(248, 249, 250));

        private static readonly FontWeight FwBold = FontWeights.Bold;
        private static readonly FontWeight FwSemiBold = FontWeights.SemiBold;
        private static readonly FontWeight FwNormal = FontWeights.Normal;

        public WpfIncomingTicketsWorkspace()
        {
            Background = ColorBackground;

            _rootScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            _root = new StackPanel { Margin = new Thickness(24, 16, 24, 24) };
            _rootScroll.Content = _root;

            // The hero remains the visual entry point. Everything below it is a focused
            // queue-and-inspector workspace instead of a collection of default controls.
            _heroBorder = BuildHero();
            _root.Children.Add(_heroBorder);
            _quickFilterPanel = new WrapPanel(); // retained for filter-state compatibility

            _root.Children.Add(BuildTriageToolbar());
            _root.Children.Add(BuildTriageWorkspace());

            Content = _rootScroll;

            // Hero controls are assigned inside BuildHero()
        }

        public void Initialize(ICallMonitoringRepository repository, ICallMonitoringNavigator navigator)
        {
            _repository = repository;
            _navigator = navigator;

            if (_initialized) return;
            _initialized = true;

            RefreshData();
        }

        public async Task LoadDataAsync(bool force = false)
        {
            if (Interlocked.CompareExchange(ref _loading, 1, 0) != 0)
                return;

            try
            {
                SetGridState("Loading portal tickets…", "Connecting to the ticket service.", false, false);

                var searchText = _searchBox?.Text?.Trim() ?? string.Empty;
                var statusFilter = _quickFilterStatus ?? (_statusFilter?.SelectedValue as string) ?? "All";
                var priorityFilter = _quickFilterPriority ?? (_priorityFilter?.SelectedValue as string) ?? "All";
                var unassignedOnly = _quickFilterUnassigned || (_chkUnassigned?.IsChecked == true);
                var minAgeDays = 0;

                var result = await _repository.GetIncomingPortalTicketsPageResultAsync(
                    searchText, statusFilter, priorityFilter, unassignedOnly, minAgeDays,
                    _currentPage, _pageSize);

                CallMonitoringRepository.IncomingTicketsSummary summary;
                try
                {
                    summary = await _repository.GetIncomingPortalTicketsSummaryAsync();
                }
                catch (Exception summaryException)
                {
                    Logger.LogError("[WpfIncomingTicketsWorkspace] Summary load failed; keeping ticket page.", summaryException);
                    summary = new CallMonitoringRepository.IncomingTicketsSummary
                    {
                        TotalCount = result.TotalCount
                    };
                }

                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    _items.Clear();
                    foreach (var item in result.Items)
                        _items.Add(item);

                    _grid.ItemsSource = _items;

                    _totalCount = result.TotalCount;
                    _hasNext = result.HasNext;
                    _summary = summary;

                    UpdateHeroSummary();
                    UpdateQuickFilterBadges();
                    UpdateActiveFilterSummary();
                    UpdatePagerState();
                    UpdateBulkBar();
                    SetGridState(
                        _items.Count == 0 ? "No portal tickets found" : string.Empty,
                        _items.Count == 0
                            ? "Try clearing the filters or refresh to check for new submissions."
                            : string.Empty,
                        false,
                        _items.Count > 0);
                }));
            }
            catch (Exception ex)
            {
                Logger.LogError("[WpfIncomingTicketsWorkspace] LoadDataAsync failed.", ex);
                var rootException = ex.GetBaseException();
                var detail = rootException == null ? "Unknown error." : rootException.Message;
                if (detail.Length > 320) detail = detail.Substring(0, 320) + "…";

                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    _items.Clear();
                    _grid.ItemsSource = _items;
                    _heroSummary.Text = "The portal ticket feed needs attention.";
                    _heroLastRefreshed.Text = "Last attempt: " + DateTime.Now.ToString("MMM dd, h:mm tt");
                    SetGridState(
                        "We couldn’t load portal tickets",
                        "Check the database connection or required ticket-view migration. Details: " + detail,
                        true,
                        false);
                }));
            }
            finally
            {
                Interlocked.Exchange(ref _loading, 0);
            }
        }

        public void RefreshData()
        {
            SafeFireAndForget(LoadDataAsync(force: true));
        }

        private async Task ResetFiltersAsync()
        {
            _suppressFilterEvents = true;
            try
            {
                _quickFilterStatus = null;
                _quickFilterPriority = null;
                _quickFilterUnassigned = false;
                _searchBox.Text = string.Empty;
                _statusFilter.SelectedIndex = 0;
                _priorityFilter.SelectedIndex = 0;
                _chkUnassigned.IsChecked = false;
                SetAllQuickFiltersActive();
            }
            finally
            {
                _suppressFilterEvents = false;
            }

            _currentPage = 1;
            await LoadDataAsync(force: true);
        }

        private void SetAllQuickFiltersActive()
        {
            foreach (var child in _quickFilterPanel.Children)
            {
                if (!(child is Border chip)) continue;
                var status = chip.Tag as string ?? string.Empty;
                var accent = status switch
                {
                    "Pending" => Color.FromRgb(245, 158, 11),
                    "In Progress" => Color.FromRgb(52, 152, 219),
                    "Escalated" => Color.FromRgb(231, 76, 60),
                    "Solved" => Color.FromRgb(149, 165, 166),
                    "Critical" => Color.FromRgb(231, 76, 60),
                    "Unassigned" => Color.FromRgb(155, 89, 182),
                    _ => ColorTeal.Color
                };
                chip.Background = new SolidColorBrush(Color.FromArgb(30, accent.R, accent.G, accent.B));
                if (chip.Child is TextBlock text)
                    text.Foreground = new SolidColorBrush(accent);
            }

            if (_quickFilterPanel.Children.Count > 0 && _quickFilterPanel.Children[0] is Border all)
            {
                all.Background = new SolidColorBrush(ColorTeal.Color);
                if (all.Child is TextBlock text)
                    text.Foreground = ColorWhite;
                _activeQuickFilter = all;
            }
        }

        // =================================================================
        //  HERO SECTION
        // =================================================================
        private Border BuildHero()
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(24),
                Margin = new Thickness(0, 0, 0, 16),
                Background = new LinearGradientBrush(
                    Color.FromRgb(15, 118, 110),
                    Color.FromRgb(21, 94, 117),
                    new Point(0, 0),
                    new Point(1, 1)),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 20,
                    Color = Color.FromArgb(60, 15, 23, 42),
                    ShadowDepth = 0,
                    Opacity = 0.3
                }
            };

            var root = new StackPanel();
            border.Child = root;

            var heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            root.Children.Add(heading);

            var headingCopy = new StackPanel();
            heading.Children.Add(headingCopy);

            headingCopy.Children.Add(new TextBlock
            {
                Text = "PORTAL INTAKE",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255))
            });

            var title = new TextBlock
            {
                Text = "Incoming portal requests",
                FontSize = 26,
                FontWeight = FontWeights.SemiBold,
                Foreground = ColorWhite,
                Margin = new Thickness(0, 3, 0, 0)
            };
            headingCopy.Children.Add(title);

            var summary = new TextBlock
            {
                Text = "Loading the triage queue…",
                Margin = new Thickness(0, 7, 0, 2),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                TextWrapping = TextWrapping.Wrap
            };
            headingCopy.Children.Add(summary);

            var refreshed = new TextBlock
            {
                Text = string.Empty,
                Margin = new Thickness(0, 2, 0, 0),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
            };
            headingCopy.Children.Add(refreshed);

            var refreshButton = new Button
            {
                Content = "↻  Refresh queue",
                Height = 34,
                Padding = new Thickness(14, 0, 14, 0),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                Foreground = ColorTealDark,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(20, 3, 0, 0)
            };
            refreshButton.Click += async (s, e) => await LoadDataAsync(force: true);
            heading.Children.Add(refreshButton);
            Grid.SetColumn(refreshButton, 1);

            var metrics = new UniformGrid
            {
                Columns = 4,
                Margin = new Thickness(0, 20, 0, 0)
            };
            metrics.Children.Add(CreateQueueMetricCard("WAITING", "new portal requests", Color.FromRgb(96, 165, 250), out _heroTotalCount));
            metrics.Children.Add(CreateQueueMetricCard("CRITICAL", "needs immediate review", Color.FromRgb(248, 113, 113), out _heroCriticalCount));
            metrics.Children.Add(CreateQueueMetricCard("HIGH PRIORITY", "requires prompt action", Color.FromRgb(251, 191, 36), out _heroHighCount));
            metrics.Children.Add(CreateQueueMetricCard("7+ DAYS", "requires follow-up", Color.FromRgb(251, 146, 60), out _heroOldTicketsCount));
            root.Children.Add(metrics);

            _heroTitle = title;
            _heroSummary = summary;
            _heroLastRefreshed = refreshed;

            return border;
        }

        private static Border CreateQueueMetricCard(string label, string detail, Color accent, out TextBlock valueText)
        {
            var card = new Border
            {
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(14, 11, 14, 11),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(54, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(accent),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            valueText = new TextBlock
            {
                Text = "--",
                FontSize = 25,
                FontWeight = FontWeights.Bold,
                Foreground = ColorWhite,
                Margin = new Thickness(0, 2, 0, 0)
            };
            stack.Children.Add(valueText);
            stack.Children.Add(new TextBlock
            {
                Text = detail,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromArgb(185, 255, 255, 255)),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            card.Child = stack;
            return card;
        }
        // =================================================================
        //  MODERN TRIAGE WORKSPACE
        // =================================================================
        private Border BuildTriageToolbar()
        {
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brush(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 14, 16, 12),
                Margin = new Thickness(0, 0, 0, 14)
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var controls = new WrapPanel { VerticalAlignment = VerticalAlignment.Top };
            _searchBox = CreateModernSearchBox();
            _searchBox.KeyDown += async (_, e) =>
            {
                if (e.Key != Key.Enter) return;
                _currentPage = 1;
                await LoadDataAsync(true);
            };
            controls.Children.Add(CreateToolbarField("Search", CreateSearchSurface(_searchBox)));

            _priorityFilter = CreateModernComboBox(132);
            _priorityFilter.Items.Add("All");
            _priorityFilter.Items.Add("Critical");
            _priorityFilter.Items.Add("High");
            _priorityFilter.Items.Add("Medium");
            _priorityFilter.Items.Add("Low");
            _priorityFilter.SelectedIndex = 0;
            _priorityFilter.SelectionChanged += async (_, __) =>
            {
                if (_suppressFilterEvents) return;
                _currentPage = 1;
                await LoadDataAsync(true);
            };
            controls.Children.Add(CreateToolbarField("Priority", _priorityFilter));

            _statusFilter = new ComboBox { Visibility = Visibility.Collapsed };
            _statusFilter.Items.Add("All");
            _statusFilter.SelectedIndex = 0;
            _chkUnassigned = new CheckBox { IsChecked = true, Visibility = Visibility.Collapsed };

            var clearButton = CreateToolbarButton("Clear filters", Brushes.White, Brush(71, 85, 105), Brush(203, 213, 225));
            clearButton.Margin = new Thickness(12, 16, 0, 0);
            clearButton.Click += async (_, __) => await ResetFiltersAsync();
            controls.Children.Add(clearButton);
            root.Children.Add(controls);

            var refreshButton = CreateToolbarButton("↻  Refresh", Brush(13, 148, 136), Brushes.White, Brush(13, 148, 136));
            refreshButton.VerticalAlignment = VerticalAlignment.Bottom;
            refreshButton.Click += async (_, __) => await LoadDataAsync(true);
            Grid.SetColumn(refreshButton, 1);
            root.Children.Add(refreshButton);

            _activeFilterSummary = new TextBlock
            {
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 11.5,
                Foreground = Brush(100, 116, 139),
                Text = "Queue scope: unassigned portal requests."
            };
            Grid.SetColumnSpan(_activeFilterSummary, 2);
            Grid.SetRow(_activeFilterSummary, 1);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.Children.Add(_activeFilterSummary);

            border.Child = root;
            return border;
        }

        private Grid BuildTriageWorkspace()
        {
            var workspace = new Grid { MinHeight = 540 };
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            var queueStack = new StackPanel();
            var queueHeader = new Grid { Margin = new Thickness(2, 0, 2, 10) };
            queueHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            queueHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = "Triage queue",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(15, 23, 42)
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "Review the request, then assign ownership or continue it into active work.",
                Margin = new Thickness(0, 3, 0, 0),
                FontSize = 11.5,
                Foreground = Brush(100, 116, 139)
            });
            queueHeader.Children.Add(titleStack);
            var selectionHint = new TextBlock
            {
                Text = "Use Ctrl+click to select multiple requests",
                FontSize = 11,
                Foreground = Brush(100, 116, 139),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(selectionHint, 1);
            queueHeader.Children.Add(selectionHint);
            queueStack.Children.Add(queueHeader);

            _grid = BuildModernQueueGrid();
            _grid.Visibility = Visibility.Collapsed;
            _gridState = BuildGridState();
            var gridHost = new Grid();
            gridHost.Children.Add(_grid);
            gridHost.Children.Add(_gridState);
            var queueCard = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brush(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                MinHeight = 470,
                Child = gridHost
            };
            queueStack.Children.Add(queueCard);

            _bulkBar = BuildModernBulkActionBar();
            queueStack.Children.Add(_bulkBar);
            _pagerBar = BuildModernPager();
            queueStack.Children.Add(_pagerBar);
            Grid.SetColumn(queueStack, 0);
            workspace.Children.Add(queueStack);

            _inspectorCard = BuildInspector();
            Grid.SetColumn(_inspectorCard, 2);
            workspace.Children.Add(_inspectorCard);
            return workspace;
        }

        private DataGrid BuildModernQueueGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Extended,
                RowHeight = 74,
                Background = Brushes.White,
                BorderThickness = new Thickness(0),
                HeadersVisibility = DataGridHeadersVisibility.Column,
                FontSize = 12.5,
                EnableRowVirtualization = true,
                EnableColumnVirtualization = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                GridLinesVisibility = DataGridGridLinesVisibility.None
            };
            grid.Resources.MergedDictionaries.Add(WpfThemeResources.GetScrollBarStyle());

            var header = new Style(typeof(DataGridColumnHeader));
            header.Setters.Add(new Setter(Control.BackgroundProperty, Brush(248, 250, 252)));
            header.Setters.Add(new Setter(Control.ForegroundProperty, Brush(100, 116, 139)));
            header.Setters.Add(new Setter(Control.FontSizeProperty, 10.5));
            header.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            header.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 11, 12, 11)));
            header.Setters.Add(new Setter(Control.BorderBrushProperty, Brush(226, 232, 240)));
            header.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            grid.ColumnHeaderStyle = header;

            grid.Columns.Add(new DataGridTemplateColumn { Header = "REQUEST", Width = 106, CellTemplate = CreateQueueTicketTemplate() });
            grid.Columns.Add(new DataGridTemplateColumn { Header = "REQUESTER / ISSUE", Width = new DataGridLength(1.7, DataGridLengthUnitType.Star), CellTemplate = CreateQueueIssueTemplate() });
            grid.Columns.Add(new DataGridTemplateColumn { Header = "PRIORITY", Width = 90, CellTemplate = CreatePriorityTemplate() });
            grid.Columns.Add(new DataGridTemplateColumn { Header = "AGE", Width = 72, CellTemplate = CreateQueueAgeTemplate() });
            grid.Columns.Add(new DataGridTemplateColumn { Header = "OWNER", Width = 108, CellTemplate = CreateQueueOwnerTemplate() });
            grid.Columns.Add(new DataGridTemplateColumn { Header = "STATUS", Width = 98, CellTemplate = CreateStatusTemplate() });

            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(DataGridRow.BackgroundProperty, Brushes.White));
            rowStyle.Setters.Add(new Setter(DataGridRow.BorderBrushProperty, Brush(241, 245, 249)));
            rowStyle.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            rowStyle.Setters.Add(new Setter(DataGridRow.CursorProperty, Cursors.Hand));
            var selected = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(DataGridRow.BackgroundProperty, Brush(240, 253, 250)));
            selected.Setters.Add(new Setter(DataGridRow.ForegroundProperty, Brush(15, 23, 42)));
            rowStyle.Triggers.Add(selected);
            grid.RowStyle = rowStyle;

            grid.SelectionChanged += async (_, __) =>
            {
                UpdateBulkBar();
                await SelectInspectorTicketAsync(grid.SelectedItem as CallTicketListItem);
            };
            grid.MouseDoubleClick += (_, __) =>
            {
                var item = grid.SelectedItem as CallTicketListItem;
                if (item != null) _navigator?.OpenTicket(item.TicketId);
            };
            return grid;
        }

        private DataTemplate CreateQueueTicketTemplate()
        {
            var root = new FrameworkElementFactory(typeof(StackPanel));
            root.SetValue(StackPanel.MarginProperty, new Thickness(14, 8, 6, 6));
            var code = new FrameworkElementFactory(typeof(TextBlock));
            code.SetBinding(TextBlock.TextProperty, new Binding("TicketCode"));
            code.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            code.SetValue(TextBlock.ForegroundProperty, Brush(15, 118, 110));
            code.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            root.AppendChild(code);
            var source = new FrameworkElementFactory(typeof(TextBlock));
            source.SetValue(TextBlock.TextProperty, "PORTAL");
            source.SetValue(TextBlock.MarginProperty, new Thickness(0, 4, 0, 0));
            source.SetValue(TextBlock.FontSizeProperty, 10.0);
            source.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            source.SetValue(TextBlock.ForegroundProperty, Brush(100, 116, 139));
            root.AppendChild(source);
            return new DataTemplate { VisualTree = root };
        }

        private DataTemplate CreateQueueIssueTemplate()
        {
            var root = new FrameworkElementFactory(typeof(StackPanel));
            root.SetValue(StackPanel.MarginProperty, new Thickness(4, 8, 10, 6));
            var issue = new FrameworkElementFactory(typeof(TextBlock));
            issue.SetBinding(TextBlock.TextProperty, new Binding("Issue"));
            issue.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            issue.SetValue(TextBlock.ForegroundProperty, Brush(15, 23, 42));
            issue.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            root.AppendChild(issue);
            var metadata = new FrameworkElementFactory(typeof(TextBlock));
            metadata.SetBinding(TextBlock.TextProperty, new Binding { Path = new PropertyPath("CallerName"), StringFormat = "Requester: {0}" });
            metadata.SetValue(TextBlock.MarginProperty, new Thickness(0, 4, 0, 0));
            metadata.SetValue(TextBlock.FontSizeProperty, 11.0);
            metadata.SetValue(TextBlock.ForegroundProperty, Brush(100, 116, 139));
            metadata.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            root.AppendChild(metadata);
            return new DataTemplate { VisualTree = root };
        }

        private DataTemplate CreateQueueAgeTemplate()
        {
            var root = new FrameworkElementFactory(typeof(StackPanel));
            root.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            root.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            var value = new FrameworkElementFactory(typeof(TextBlock));
            value.SetBinding(TextBlock.TextProperty, new Binding("TicketAgeDays"));
            value.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            value.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            root.AppendChild(value);
            var label = new FrameworkElementFactory(typeof(TextBlock));
            label.SetValue(TextBlock.TextProperty, "days");
            label.SetValue(TextBlock.FontSizeProperty, 10.0);
            label.SetValue(TextBlock.ForegroundProperty, Brush(100, 116, 139));
            label.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            root.AppendChild(label);
            return new DataTemplate { VisualTree = root };
        }

        private DataTemplate CreateQueueOwnerTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            border.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.SetValue(Border.BackgroundProperty, Brush(255, 247, 237));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.PaddingProperty, new Thickness(8, 4, 8, 4));
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetValue(TextBlock.TextProperty, "Unassigned");
            text.SetValue(TextBlock.FontSizeProperty, 10.5);
            text.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            text.SetValue(TextBlock.ForegroundProperty, Brush(194, 65, 12));
            border.AppendChild(text);
            return new DataTemplate { VisualTree = border };
        }

        private Border BuildInspector()
        {
            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brush(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(0),
                MinHeight = 560
            };
            var host = new Grid();
            card.Child = host;

            _inspectorEmptyState = new Border
            {
                Padding = new Thickness(30),
                Child = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = "◈", FontSize = 34, Foreground = Brush(13, 148, 136), HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock { Text = "Ticket inspector", FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = Brush(15, 23, 42), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) },
                        new TextBlock { Text = "Select a portal request from the queue to review the full context and take action.", FontSize = 12.5, Foreground = Brush(100, 116, 139), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) }
                    }
                }
            };
            host.Children.Add(_inspectorEmptyState);

            _inspectorScroll = new ScrollViewer
            {
                Visibility = Visibility.Collapsed,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _inspectorScroll.Resources.MergedDictionaries.Add(WpfThemeResources.GetScrollBarStyle());
            var content = new StackPanel();
            _inspectorScroll.Content = content;

            var head = new Border { Background = Brush(15, 118, 110), Padding = new Thickness(20, 18, 20, 18) };
            var headGrid = new Grid();
            headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var headerCopy = new StackPanel();
            _inspectorTicketCode = new TextBlock { Text = "Loading…", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
            _inspectorIssue = new TextBlock { Margin = new Thickness(0, 5, 0, 0), FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), TextWrapping = TextWrapping.Wrap, MaxHeight = 38 };
            headerCopy.Children.Add(_inspectorTicketCode);
            headerCopy.Children.Add(_inspectorIssue);
            headGrid.Children.Add(headerCopy);
            _inspectorStatus = new TextBlock { FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = Brush(13, 148, 136), Background = Brushes.White, Padding = new Thickness(8, 5, 8, 5), VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(_inspectorStatus, 1);
            headGrid.Children.Add(_inspectorStatus);
            head.Child = headGrid;
            content.Children.Add(head);

            var body = new StackPanel { Margin = new Thickness(18) };
            content.Children.Add(body);
            body.Children.Add(CreateInspectorSection("Request context", out _inspectorRequester, "Requester", out _inspectorLocation, "Location"));
            body.Children.Add(CreateInspectorSection("Ownership & timing", out _inspectorOwner, "Owner", out _inspectorCreated, "Created"));
            _inspectorActivity = new TextBlock { FontSize = 12, Foreground = Brush(71, 85, 105), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            body.Children.Add(CreateInspectorCard("Last activity", _inspectorActivity));

            var priorityCard = new Border { Background = Brush(248, 250, 252), BorderBrush = Brush(226, 232, 240), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(13), Margin = new Thickness(0, 0, 0, 12) };
            var priorityGrid = new Grid();
            priorityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            priorityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var priorityStack = new StackPanel();
            priorityStack.Children.Add(new TextBlock { Text = "Priority", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brush(100, 116, 139) });
            _inspectorPriority = CreateModernComboBox(140);
            _inspectorPriority.Margin = new Thickness(0, 7, 0, 0);
            _inspectorPriority.Items.Add("Critical"); _inspectorPriority.Items.Add("High"); _inspectorPriority.Items.Add("Medium"); _inspectorPriority.Items.Add("Low");
            priorityStack.Children.Add(_inspectorPriority);
            priorityGrid.Children.Add(priorityStack);
            _inspectorPriorityButton = CreateToolbarButton("Save", Brush(37, 99, 235), Brushes.White, Brush(37, 99, 235));
            _inspectorPriorityButton.VerticalAlignment = VerticalAlignment.Bottom;
            _inspectorPriorityButton.Click += async (_, __) => await SaveInspectorPriorityAsync();
            Grid.SetColumn(_inspectorPriorityButton, 1);
            priorityGrid.Children.Add(_inspectorPriorityButton);
            priorityCard.Child = priorityGrid;
            body.Children.Add(priorityCard);

            var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            _inspectorAssignButton = CreateInspectorAction("Assign owner", Brush(13, 148, 136));
            _inspectorAssignButton.Click += async (_, __) => await AssignSelectedInspectorTicketAsync();
            _inspectorStartButton = CreateInspectorAction("Assign & start", Brushes.White, Brush(15, 118, 110), Brush(94, 234, 212));
            _inspectorStartButton.Click += async (_, __) => await StartSelectedInspectorTicketAsync();
            _inspectorResolveButton = CreateInspectorAction("Assign & resolve", Brushes.White, Brush(21, 128, 61), Brush(134, 239, 172));
            _inspectorResolveButton.Click += async (_, __) => await ResolveSelectedInspectorTicketAsync();
            var fullDetails = CreateInspectorAction("Full details", Brushes.White, Brush(71, 85, 105), Brush(203, 213, 225));
            fullDetails.Click += (_, __) => { if (_selectedTicket != null) _navigator?.OpenTicket(_selectedTicket.TicketId); };
            actions.Children.Add(_inspectorAssignButton); actions.Children.Add(_inspectorStartButton); actions.Children.Add(_inspectorResolveButton); actions.Children.Add(fullDetails);
            body.Children.Add(actions);

            _inspectorTimeline = new TextBlock { FontSize = 11.5, Foreground = Brush(71, 85, 105), TextWrapping = TextWrapping.Wrap, LineHeight = 18 };
            body.Children.Add(CreateInspectorCard("Recent activity", _inspectorTimeline));
            host.Children.Add(_inspectorScroll);
            return card;
        }

        private async Task SelectInspectorTicketAsync(CallTicketListItem ticket)
        {
            _selectedTicket = ticket;
            var version = Interlocked.Increment(ref _inspectorLoadVersion);
            if (ticket == null)
            {
                _inspectorEmptyState.Visibility = Visibility.Visible;
                _inspectorScroll.Visibility = Visibility.Collapsed;
                return;
            }

            _inspectorEmptyState.Visibility = Visibility.Collapsed;
            _inspectorScroll.Visibility = Visibility.Visible;
            _inspectorTicketCode.Text = ticket.TicketCode ?? ticket.TicketId.ToString();
            _inspectorIssue.Text = "Loading request context…";
            _inspectorStatus.Text = "LOADING";
            SetInspectorActionsEnabled(false);

            try
            {
                var detailsTask = _repository.GetTicketByIdAsync(ticket.TicketId);
                var notesTask = _repository.GetTicketNotesAsync(ticket.TicketId, 4);
                var historyTask = _repository.GetTicketHistoryAsync(ticket.TicketId, 4);
                await Task.WhenAll(detailsTask, notesTask, historyTask);
                if (version != _inspectorLoadVersion) return;

                var detail = detailsTask.Result ?? ticket;
                _selectedTicket = detail;
                _inspectorTicketCode.Text = detail.TicketCode ?? detail.TicketId.ToString();
                _inspectorIssue.Text = string.IsNullOrWhiteSpace(detail.Issue) ? "No issue description supplied." : detail.Issue.Trim();
                _inspectorStatus.Text = string.IsNullOrWhiteSpace(detail.Status) ? "PENDING" : detail.Status.Trim().ToUpperInvariant();
                _inspectorRequester.Text = string.IsNullOrWhiteSpace(detail.CallerName) ? "Not provided" : detail.CallerName.Trim();
                _inspectorLocation.Text = string.Join(" • ", new[] { detail.Department, detail.Branch }.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (string.IsNullOrWhiteSpace(_inspectorLocation.Text)) _inspectorLocation.Text = "Location not provided";
                _inspectorOwner.Text = string.IsNullOrWhiteSpace(detail.ResponsiblePerson) ? "Unassigned" : detail.ResponsiblePerson.Trim();
                _inspectorCreated.Text = detail.CreatedAt == default(DateTime) ? "Not available" : detail.CreatedAt.ToString("MMM dd, yyyy h:mm tt");
                _inspectorActivity.Text = BuildInspectorActivity(detail);
                _inspectorPriority.SelectedItem = string.IsNullOrWhiteSpace(detail.Priority) ? "Medium" : detail.Priority.Trim();
                _inspectorTimeline.Text = BuildInspectorTimeline(notesTask.Result, historyTask.Result);
                SetInspectorActionsEnabled(true);
            }
            catch (Exception ex)
            {
                if (version != _inspectorLoadVersion) return;
                _inspectorIssue.Text = "Unable to load the selected request.";
                _inspectorTimeline.Text = ex.GetBaseException()?.Message ?? "Unknown error.";
                _inspectorStatus.Text = "RETRY";
                SetInspectorActionsEnabled(false);
            }
        }

        private async Task AssignSelectedInspectorTicketAsync()
        {
            if (_selectedTicket == null) return;
            await AssignTicketsWithStaffPickerAsync(new[] { _selectedTicket });
        }

        private async Task StartSelectedInspectorTicketAsync()
        {
            if (_selectedTicket == null) return;
            await AssignTicketsAndSetStatusAsync(new[] { _selectedTicket }, "In Progress", "Started work from portal triage.");
        }

        private async Task ResolveSelectedInspectorTicketAsync()
        {
            if (_selectedTicket == null) return;
            await AssignTicketsAndSetStatusAsync(new[] { _selectedTicket }, "Resolved (Temporary)", "Resolved from portal triage.");
        }

        private async Task SaveInspectorPriorityAsync()
        {
            if (_selectedTicket == null || !(_inspectorPriority.SelectedItem is string priority)) return;
            if (string.Equals(priority, _selectedTicket.Priority, StringComparison.OrdinalIgnoreCase)) return;
            await _repository.SetTicketPriorityAsync(_selectedTicket.TicketId, priority, AppSession.CurrentUserId);
            await SelectInspectorTicketAsync(_selectedTicket);
            await LoadDataAsync(true);
        }

        private async Task AssignTicketsWithStaffPickerAsync(IEnumerable<CallTicketListItem> tickets)
        {
            var targets = (tickets ?? Enumerable.Empty<CallTicketListItem>()).Where(x => x != null && x.TicketId > 0).GroupBy(x => x.TicketId).Select(x => x.First()).ToList();
            if (targets.Count == 0 || _repository == null) return;
            var candidates = await LoadAssignmentCandidatesAsync();
            if (candidates.Count == 0)
            {
                WpfItcmDialogService.ShowWarning(this, "No eligible IT staff are configured for assignment. Verify IT department membership, active user accounts, and assignee eligibility settings.", "No assignees available");
                return;
            }

            var workloads = await _repository.GetOpenTicketCountsByAssigneeAsync(candidates.Select(x => x.Id));
            var dialog = new WpfIncomingTicketAssigneePickerDialog(candidates, workloads, targets.Count) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true || dialog.SelectedAssignee == null) return;
            foreach (var ticket in targets)
                await new CallEmailNotificationService(_repository).AssignTicketAndNotifyAsync(ticket.TicketId, dialog.SelectedAssignee.Id, AppSession.CurrentUserId);
            await LoadDataAsync(true);
        }

        private async Task AssignTicketsAndSetStatusAsync(IEnumerable<CallTicketListItem> tickets, string targetStatus, string initialNote)
        {
            var targets = (tickets ?? Enumerable.Empty<CallTicketListItem>())
                .Where(x => x != null && x.TicketId > 0)
                .GroupBy(x => x.TicketId)
                .Select(x => x.First())
                .ToList();
            if (targets.Count == 0 || _repository == null) return;

            var candidates = await LoadAssignmentCandidatesAsync();
            if (candidates.Count == 0)
            {
                WpfItcmDialogService.ShowWarning(this, "An owner is required before work can start or a request can be resolved. No eligible IT staff are currently configured for assignment.", "Owner required");
                return;
            }

            var workloads = await _repository.GetOpenTicketCountsByAssigneeAsync(candidates.Select(x => x.Id));
            var picker = new WpfIncomingTicketAssigneePickerDialog(candidates, workloads, targets.Count) { Owner = Window.GetWindow(this) };
            if (picker.ShowDialog() != true || picker.SelectedAssignee == null) return;

            var note = initialNote;
            if (targets.Count == 1)
            {
                var statusDialog = new WpfTicketStatusActionDialog(targets[0], targetStatus, null, false, initialNote)
                {
                    Owner = Window.GetWindow(this)
                };
                if (statusDialog.ShowDialog() != true) return;
                note = statusDialog.NoteText;
            }
            else if (!WpfItcmDialogService.Confirm(
                this,
                "Assign " + picker.SelectedAssignee.Name + " and set " + targets.Count + " selected portal requests to " + targetStatus + "?",
                "Confirm bulk triage",
                "Assign and continue"))
            {
                return;
            }

            foreach (var ticket in targets)
            {
                await new CallEmailNotificationService(_repository).AssignTicketAndNotifyAsync(ticket.TicketId, picker.SelectedAssignee.Id, AppSession.CurrentUserId);
                // Triage fast path: keep a Service Only resolution row so
                // reports stay consistent even without a Mark As dialog.
                if (string.Equals(targetStatus, "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(targetStatus, "Solved", StringComparison.OrdinalIgnoreCase))
                {
                    await _repository.LogTicketResolutionAsync(ticket.TicketId, "Service Only", note, AppSession.CurrentUserId);
                }
                await _repository.SetTicketStatusAsync(ticket.TicketId, targetStatus, AppSession.CurrentUserId, note);
            }

            await LoadDataAsync(true);
        }

        private Border BuildModernBulkActionBar()
        {
            var border = new Border
            {
                Margin = new Thickness(0, 12, 0, 0),
                Padding = new Thickness(14, 10, 14, 10),
                Background = Brush(236, 253, 245),
                BorderBrush = Brush(167, 243, 208),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Visibility = Visibility.Collapsed
            };
            var panel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brush(6, 95, 70), Margin = new Thickness(0, 7, 14, 0) });
            var assign = CreateInspectorAction("Assign selected", Brush(13, 148, 136));
            assign.Click += async (_, __) => await AssignTicketsWithStaffPickerAsync(_grid.SelectedItems.Cast<CallTicketListItem>());
            var start = CreateInspectorAction("Assign & start", Brushes.White, Brush(15, 118, 110), Brush(94, 234, 212));
            start.Click += async (_, __) => await AssignTicketsAndSetStatusAsync(_grid.SelectedItems.Cast<CallTicketListItem>(), "In Progress", "Started work from portal triage.");
            panel.Children.Add(assign); panel.Children.Add(start);
            border.Child = panel;
            return border;
        }

        private Border BuildModernPager()
        {
            var border = new Border { Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(10), Background = Brushes.White, BorderBrush = Brush(226, 232, 240), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _prevBtn = CreateToolbarButton("← Prev", Brushes.White, Brush(71, 85, 105), Brush(203, 213, 225));
            _prevBtn.Click += async (_, __) => { if (_currentPage > 1) { _currentPage--; await LoadDataAsync(true); } };
            _nextBtn = CreateToolbarButton("Next →", Brushes.White, Brush(71, 85, 105), Brush(203, 213, 225));
            _nextBtn.Click += async (_, __) => { if (_hasNext) { _currentPage++; await LoadDataAsync(true); } };
            _pageInfo = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 11.5, Foreground = Brush(100, 116, 139) };
            grid.Children.Add(_prevBtn); Grid.SetColumn(_pageInfo, 1); grid.Children.Add(_pageInfo); Grid.SetColumn(_nextBtn, 2); grid.Children.Add(_nextBtn);
            border.Child = grid;
            return border;
        }

        private static Border CreateInspectorSection(string title, out TextBlock firstValue, string firstLabel, out TextBlock secondValue, string secondLabel)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
            var card = new Border { Background = Brush(248, 250, 252), BorderBrush = Brush(226, 232, 240), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(13), Child = grid };
            var first = new StackPanel();
            first.Children.Add(new TextBlock { Text = firstLabel.ToUpperInvariant(), FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brush(100, 116, 139) });
            firstValue = new TextBlock { FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Brush(15, 23, 42), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 8, 0) };
            first.Children.Add(firstValue); grid.Children.Add(first);
            var second = new StackPanel();
            second.Children.Add(new TextBlock { Text = secondLabel.ToUpperInvariant(), FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brush(100, 116, 139) });
            secondValue = new TextBlock { FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Brush(15, 23, 42), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) };
            second.Children.Add(secondValue); Grid.SetColumn(second, 1); grid.Children.Add(second);
            return card;
        }

        private static Border CreateInspectorCard(string title, UIElement child)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = title.ToUpperInvariant(), FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brush(100, 116, 139), Margin = new Thickness(0, 0, 0, 4) });
            stack.Children.Add(child);
            return new Border { Background = Brush(248, 250, 252), BorderBrush = Brush(226, 232, 240), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(13), Margin = new Thickness(0, 0, 0, 12), Child = stack };
        }

        private static Button CreateInspectorAction(string text, Brush background, Brush foreground = null, Brush border = null)
        {
            return CreateFlatButton(text, background, foreground ?? Brushes.White, border ?? background, 34, new Thickness(12, 0, 12, 0), new Thickness(0, 0, 8, 8));
        }

        private static TextBox CreateModernSearchBox()
        {
            return new TextBox
            {
                Height = 34,
                Padding = new Thickness(31, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 12.5,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ToolTip = "Search ticket, requester, department, or branch"
            };
        }

        private static Border CreateSearchSurface(TextBox searchBox)
        {
            var hint = new TextBlock
            {
                Text = "Search requests",
                FontSize = 12.5,
                Foreground = Brush(148, 163, 184),
                Margin = new Thickness(31, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            searchBox.TextChanged += (_, __) => hint.Visibility = string.IsNullOrWhiteSpace(searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;

            var grid = new Grid();
            grid.Children.Add(new TextBlock
            {
                Text = "⌕",
                FontSize = 18,
                Foreground = Brush(100, 116, 139),
                Margin = new Thickness(10, 0, 0, 1),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            });
            grid.Children.Add(hint);
            grid.Children.Add(searchBox);
            return new Border
            {
                Width = 272,
                Height = 36,
                Background = Brush(248, 250, 252),
                BorderBrush = Brush(203, 213, 225),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = grid
            };
        }

        private static ComboBox CreateModernComboBox(double width)
        {
            var combo = new ComboBox
            {
                Width = width,
                Height = 36,
                Padding = new Thickness(10, 0, 30, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 12,
                Background = Brushes.White,
                BorderBrush = Brush(203, 213, 225),
                BorderThickness = new Thickness(1),
                Foreground = Brush(15, 23, 42),
                FocusVisualStyle = null
            };
            combo.Template = CreateModernComboBoxTemplate();
            return combo;
        }

        private static ControlTemplate CreateModernComboBoxTemplate()
        {
            var root = new FrameworkElementFactory(typeof(Grid));
            var field = new FrameworkElementFactory(typeof(Border));
            field.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            field.SetValue(Border.SnapsToDevicePixelsProperty, true);
            field.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            field.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            field.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemProperty));
            content.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemTemplateProperty));
            content.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            field.AppendChild(content);
            root.AppendChild(field);

            var arrow = new FrameworkElementFactory(typeof(TextBlock));
            arrow.SetValue(TextBlock.TextProperty, "⌄");
            arrow.SetValue(TextBlock.FontSizeProperty, 15.0);
            arrow.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            arrow.SetValue(TextBlock.ForegroundProperty, Brush(71, 85, 105));
            arrow.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            arrow.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrow.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 10, 2));
            arrow.SetValue(UIElement.IsHitTestVisibleProperty, false);
            root.AppendChild(arrow);

            var popup = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Popup));
            popup.SetValue(FrameworkElement.NameProperty, "PART_Popup");
            popup.SetValue(System.Windows.Controls.Primitives.Popup.PlacementProperty, System.Windows.Controls.Primitives.PlacementMode.Bottom);
            popup.SetValue(System.Windows.Controls.Primitives.Popup.AllowsTransparencyProperty, true);
            popup.SetValue(System.Windows.Controls.Primitives.Popup.PopupAnimationProperty, System.Windows.Controls.Primitives.PopupAnimation.Slide);
            popup.SetValue(System.Windows.Controls.Primitives.Popup.IsOpenProperty, new TemplateBindingExtension(ComboBox.IsDropDownOpenProperty));
            var popupBorder = new FrameworkElementFactory(typeof(Border));
            popupBorder.SetValue(Border.MarginProperty, new Thickness(0, 4, 0, 0));
            popupBorder.SetValue(Border.BackgroundProperty, Brushes.White);
            popupBorder.SetValue(Border.BorderBrushProperty, Brush(203, 213, 225));
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            popupBorder.SetValue(FrameworkElement.MinWidthProperty, new TemplateBindingExtension(FrameworkElement.ActualWidthProperty));
            popupBorder.SetValue(FrameworkElement.MaxHeightProperty, 240.0);
            var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
            scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
            scroll.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
            popupBorder.AppendChild(scroll);
            popup.AppendChild(popupBorder);
            root.AppendChild(popup);
            return new ControlTemplate(typeof(ComboBox)) { VisualTree = root };
        }

        private static FrameworkElement CreateToolbarField(string label, FrameworkElement control)
        {
            var panel = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            panel.Children.Add(new TextBlock { Text = label.ToUpperInvariant(), FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = Brush(100, 116, 139), Margin = new Thickness(2, 0, 0, 3) });
            panel.Children.Add(control);
            return panel;
        }

        private static Button CreateToolbarButton(string text, Brush background, Brush foreground, Brush border)
        {
            return CreateFlatButton(text, background, foreground, border, 36, new Thickness(12, 0, 12, 0), new Thickness(0));
        }

        private static Button CreateFlatButton(string text, Brush background, Brush foreground, Brush border, double height, Thickness padding, Thickness margin)
        {
            var button = new Button
            {
                Content = text,
                Height = height,
                Padding = padding,
                Margin = margin,
                Background = background,
                Foreground = foreground,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                FocusVisualStyle = null
            };
            var style = new Style(typeof(Button));
            var template = new ControlTemplate(typeof(Button));
            var surface = new FrameworkElementFactory(typeof(Border));
            surface.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            surface.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            surface.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            surface.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            surface.AppendChild(presenter);
            template.VisualTree = surface;
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.9));
            style.Triggers.Add(hover);
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
            style.Triggers.Add(disabled);
            button.Style = style;
            return button;
        }

        private void SetInspectorActionsEnabled(bool enabled)
        {
            if (_inspectorAssignButton != null) _inspectorAssignButton.IsEnabled = enabled;
            if (_inspectorStartButton != null) _inspectorStartButton.IsEnabled = enabled;
            if (_inspectorResolveButton != null) _inspectorResolveButton.IsEnabled = enabled;
            if (_inspectorPriorityButton != null) _inspectorPriorityButton.IsEnabled = enabled;
            if (_inspectorPriority != null) _inspectorPriority.IsEnabled = enabled;
        }

        private static string BuildInspectorActivity(CallTicketListItem ticket)
        {
            var updated = ticket.UpdatedAt == default(DateTime) ? ticket.CreatedAt : ticket.UpdatedAt;
            var idle = ticket.IdleDays > 0 ? ticket.IdleDays + " day(s) idle" : "active today";
            return "Updated " + (updated == default(DateTime) ? "recently" : updated.ToString("MMM dd, h:mm tt")) + " • " + idle;
        }

        private static string BuildInspectorTimeline(IEnumerable<CallTicketNoteItem> notes, IEnumerable<CallTicketHistoryItem> history)
        {
            var entries = new List<string>();
            entries.AddRange((notes ?? Enumerable.Empty<CallTicketNoteItem>()).Select(n => "• " + (n.CreatedAt?.ToString("MMM dd, h:mm tt") ?? "Recent") + "  " + (string.IsNullOrWhiteSpace(n.NoteText) ? "Note added" : n.NoteText.Trim())));
            entries.AddRange((history ?? Enumerable.Empty<CallTicketHistoryItem>()).Select(h => "• " + (h.ChangedAt?.ToString("MMM dd, h:mm tt") ?? "Recent") + "  " + (string.IsNullOrWhiteSpace(h.FieldName) ? "Ticket updated" : h.FieldName + ": " + (h.NewValue ?? "updated"))));
            return entries.Count == 0 ? "No recent notes or workflow changes are recorded for this request." : string.Join(Environment.NewLine, entries.Take(6));
        }

        private static SolidColorBrush Brush(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));

        // =================================================================
        //  QUICK FILTER CHIPS
        // =================================================================
        private WrapPanel BuildQuickFilters()
        {
            var panel = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };

            var filters = new[]
            {
                ("All", "", Color.FromRgb(52, 152, 219)),
                ("Pending", "Pending", Color.FromRgb(245, 158, 11)),
                ("In Progress", "In Progress", Color.FromRgb(52, 152, 219)),
                ("Escalated", "Escalated", Color.FromRgb(231, 76, 60)),
                ("Solved", "Solved", Color.FromRgb(149, 165, 166)),
                ("Critical", "Critical", Color.FromRgb(231, 76, 60)),
                ("Unassigned", "Unassigned", Color.FromRgb(155, 89, 182)),
            };

            foreach (var (label, status, accent) in filters)
            {
                var border = CreateQuickFilterChip(label, status, accent);
                panel.Children.Add(border);
                if (status == "")
                {
                    _activeQuickFilter = border;
                    border.Background = new SolidColorBrush(accent);
                    border.Child = new TextBlock
                    {
                        Text = "All",
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = ColorWhite,
                        Margin = new Thickness(12, 6, 12, 6)
                    };
                }
            }

            return panel;
        }

        private Border CreateQuickFilterChip(string label, string status, Color accent)
        {
            var border = new Border
            {
                Margin = new Thickness(0, 0, 8, 4),
                CornerRadius = new CornerRadius(999),
                Background = new SolidColorBrush(Color.FromArgb(30, accent.R, accent.G, accent.B)),
                Cursor = Cursors.Hand,
                Tag = status,
                Child = new TextBlock
                {
                    Text = label,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(accent),
                    Margin = new Thickness(12, 6, 12, 6)
                }
            };

            border.MouseLeftButtonDown += async (s, e) =>
            {
                var chip = s as Border;
                if (chip == null) return;

                var newStatus = chip.Tag as string ?? "";

                // Deactivate previous
                if (_activeQuickFilter != null)
                {
                    var oldText = (_activeQuickFilter.Child as TextBlock)?.Text ?? "";
                    var oldStatus = _activeQuickFilter.Tag as string ?? "";
                    var oldAccent = oldStatus switch
                    {
                        "Pending" => Color.FromRgb(245, 158, 11),
                        "In Progress" => Color.FromRgb(52, 152, 219),
                        "Escalated" => Color.FromRgb(231, 76, 60),
                        "Solved" => Color.FromRgb(149, 165, 166),
                        "Critical" => Color.FromRgb(231, 76, 60),
                        "Unassigned" => Color.FromRgb(155, 89, 182),
                        _ => Color.FromRgb(52, 152, 219)
                    };
                    _activeQuickFilter.Background = new SolidColorBrush(Color.FromArgb(30, oldAccent.R, oldAccent.G, oldAccent.B));
                    if (_activeQuickFilter.Child is TextBlock tb)
                    {
                        tb.Foreground = new SolidColorBrush(oldAccent);
                    }
                }

                // Activate this
                chip.Background = new SolidColorBrush(accent);
                if (chip.Child is TextBlock tb2)
                {
                    tb2.Foreground = ColorWhite;
                }
                _activeQuickFilter = chip;

                _quickFilterStatus = null;
                _quickFilterPriority = null;
                _quickFilterUnassigned = false;

                if (status == "Critical")
                {
                    _quickFilterPriority = "Critical";
                }
                else if (status == "Unassigned")
                {
                    _quickFilterUnassigned = true;
                }
                else if (!string.IsNullOrEmpty(status))
                {
                    _quickFilterStatus = status;
                }

                _currentPage = 1;
                await LoadDataAsync();
            };

            return border;
        }

        // =================================================================
        //  FILTER BAR (search + status/priority combos + unassigned checkbox)
        // =================================================================
        private Border BuildFilterBar()
        {
            var border = new Border
            {
                Background = ColorCardBg,
                BorderBrush = ColorBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var panel = new WrapPanel();

            _searchBox = new TextBox
            {
                Width = 240,
                Height = 30,
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _searchBox.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    _currentPage = 1;
                    await LoadDataAsync();
                }
            };

            var searchBtn = new Button
            {
                Content = "  Search  ",
                Height = 30,
                Margin = new Thickness(6, 0, 12, 0),
                Cursor = Cursors.Hand,
                Background = ColorAccentBlue,
                Foreground = ColorWhite,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0)
            };
            searchBtn.Click += async (s, e) =>
            {
                _currentPage = 1;
                await LoadDataAsync();
            };

            panel.Children.Add(_searchBox);
            panel.Children.Add(searchBtn);

            // Status filter
            panel.Children.Add(new TextBlock
            {
                Text = "Status:",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = ColorTextSecondary,
                Margin = new Thickness(0, 0, 6, 0)
            });
            _statusFilter = new ComboBox
            {
                Width = 130,
                Height = 30,
                FontSize = 13,
                Margin = new Thickness(0, 0, 12, 0)
            };
            _statusFilter.Items.Add("All");
            _statusFilter.Items.Add("Pending");
            _statusFilter.Items.Add("In Progress");
            _statusFilter.Items.Add("Escalated");
            _statusFilter.Items.Add("Forwarded to Repair");
            _statusFilter.Items.Add("Resolved (Temporary)");
            _statusFilter.Items.Add("Solved");
            _statusFilter.Items.Add("Reopened");
            _statusFilter.SelectedIndex = 0;
            _statusFilter.SelectionChanged += async (s, e) =>
            {
                if (_suppressFilterEvents) return;
                _quickFilterStatus = null;
                _quickFilterPriority = null;
                _quickFilterUnassigned = false;
                _currentPage = 1;
                await LoadDataAsync();
            };
            panel.Children.Add(_statusFilter);

            // Priority filter
            panel.Children.Add(new TextBlock
            {
                Text = "Priority:",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = ColorTextSecondary,
                Margin = new Thickness(0, 0, 6, 0)
            });
            _priorityFilter = new ComboBox
            {
                Width = 120,
                Height = 30,
                FontSize = 13,
                Margin = new Thickness(0, 0, 12, 0)
            };
            _priorityFilter.Items.Add("All");
            _priorityFilter.Items.Add("Critical");
            _priorityFilter.Items.Add("High");
            _priorityFilter.Items.Add("Medium");
            _priorityFilter.Items.Add("Low");
            _priorityFilter.SelectedIndex = 0;
            _priorityFilter.SelectionChanged += async (s, e) =>
            {
                if (_suppressFilterEvents) return;
                _quickFilterStatus = null;
                _quickFilterPriority = null;
                _quickFilterUnassigned = false;
                _currentPage = 1;
                await LoadDataAsync();
            };
            panel.Children.Add(_priorityFilter);

            // Unassigned only
            _chkUnassigned = new CheckBox
            {
                Content = "Unassigned only",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = ColorTextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            _chkUnassigned.Checked += async (s, e) =>
            {
                if (_suppressFilterEvents) return;
                _quickFilterStatus = null;
                _quickFilterPriority = null;
                _quickFilterUnassigned = false;
                _currentPage = 1;
                await LoadDataAsync();
            };
            _chkUnassigned.Unchecked += async (s, e) =>
            {
                if (_suppressFilterEvents) return;
                _quickFilterStatus = null;
                _quickFilterPriority = null;
                _quickFilterUnassigned = false;
                _currentPage = 1;
                await LoadDataAsync();
            };
            panel.Children.Add(_chkUnassigned);

            var resetButton = new Button
            {
                Content = "Reset",
                Height = 30,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                Foreground = ColorTextPrimary,
                FontWeight = FontWeights.SemiBold,
                BorderBrush = ColorBorder
            };
            resetButton.Click += async (s, e) => await ResetFiltersAsync();
            panel.Children.Add(resetButton);

            var refreshButton = new Button
            {
                Content = "Refresh",
                Height = 30,
                Padding = new Thickness(12, 0, 12, 0),
                Cursor = Cursors.Hand,
                Background = ColorAccentBlue,
                Foreground = ColorWhite,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0)
            };
            refreshButton.Click += async (s, e) => await LoadDataAsync(force: true);
            panel.Children.Add(refreshButton);

            _activeFilterSummary = new TextBlock
            {
                Text = "Showing all portal submissions",
                FontSize = 11.5,
                Foreground = ColorTextSecondary,
                Margin = new Thickness(0, 10, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var wrapper = new StackPanel();
            wrapper.Children.Add(panel);
            wrapper.Children.Add(_activeFilterSummary);
            border.Child = wrapper;
            return border;
        }

        // =================================================================
        //  DATAGRID
        // =================================================================
        private DataGrid BuildGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Extended,
                RowHeight = 38,
                AlternatingRowBackground = ColorRowAlt,
                Background = ColorCardBg,
                BorderBrush = ColorBorder,
                BorderThickness = new Thickness(0),
                HeadersVisibility = DataGridHeadersVisibility.Column,
                FontSize = 13,
                EnableRowVirtualization = true,
                EnableColumnVirtualization = true,
                RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                GridLinesVisibility = DataGridGridLinesVisibility.None
            };

            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(248, 250, 252))));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, ColorTextSecondary));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 9, 10, 9)));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, ColorBorder));
            headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            headerStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            grid.ColumnHeaderStyle = headerStyle;

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Ticket",
                Binding = new Binding("TicketCode"),
                Width = 110
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Issue",
                Binding = new Binding("Issue"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star),
                ElementStyle = CreateCellStyle(TextTrimming.CharacterEllipsis)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Caller",
                Binding = new Binding("CallerName"),
                Width = 130
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Company",
                Binding = new Binding("Company"),
                Width = 120
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Dept",
                Binding = new Binding("Department"),
                Width = 110
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Branch",
                Binding = new Binding("Branch"),
                Width = 130
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Assigned To",
                Binding = new Binding("ResponsiblePerson"),
                Width = 120
            });
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Priority",
                Width = 85,
                CellTemplate = CreatePriorityTemplate()
            });
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Status",
                Width = 110,
                CellTemplate = CreateStatusTemplate()
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Created",
                Binding = new Binding("CreatedAt") { StringFormat = "MMM dd, yyyy" },
                Width = 110
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Age",
                Binding = new Binding("TicketAgeDays"),
                Width = 55
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Idle",
                Binding = new Binding("IdleDays"),
                Width = 50
            });
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Actions",
                Width = 140,
                CellTemplate = CreateActionsTemplate()
            });

            // Priority accent row style
            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(DataGridRow.BackgroundProperty, ColorCardBg));
            var trigger = new DataTrigger { Binding = new Binding("Priority"), Value = "Critical" };
            trigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 245, 245))));
            rowStyle.Triggers.Add(trigger);
            var highTrigger = new DataTrigger { Binding = new Binding("Priority"), Value = "High" };
            highTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 248, 240))));
            rowStyle.Triggers.Add(highTrigger);
            var medTrigger = new DataTrigger { Binding = new Binding("Priority"), Value = "Medium" };
            medTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromRgb(248, 250, 255))));
            rowStyle.Triggers.Add(medTrigger);
            grid.RowStyle = rowStyle;

            grid.SelectionChanged += (s, e) => UpdateBulkBar();
            grid.MouseDoubleClick += (s, e) =>
            {
                var item = grid.SelectedItem as CallTicketListItem;
                if (item != null)
                    _navigator?.OpenTicket(item.TicketId);
            };

            return grid;
        }

        private static Style CreateCellStyle(TextTrimming trimming)
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, trimming));
            style.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 0, 4, 0)));
            style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            return style;
        }

        private Border BuildGridState()
        {
            var icon = new TextBlock
            {
                Text = "!",
                Width = 42,
                Height = 42,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Foreground = ColorAccentBlue,
                Background = new SolidColorBrush(Color.FromRgb(239, 246, 255)),
                Margin = new Thickness(0, 0, 0, 12)
            };

            _gridStateTitle = new TextBlock
            {
                Text = "Loading portal tickets…",
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = ColorTextPrimary,
                TextAlignment = TextAlignment.Center
            };
            _gridStateDetail = new TextBlock
            {
                Text = "Connecting to the ticket service.",
                FontSize = 12.5,
                Foreground = ColorTextSecondary,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                MaxWidth = 620,
                Margin = new Thickness(0, 7, 0, 14)
            };
            _gridStateRetry = new Button
            {
                Content = "Retry",
                Width = 88,
                Height = 32,
                Visibility = Visibility.Collapsed,
                Cursor = Cursors.Hand,
                Background = ColorAccentBlue,
                Foreground = ColorWhite,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0)
            };
            _gridStateRetry.Click += async (s, e) => await LoadDataAsync(force: true);

            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(icon);
            panel.Children.Add(_gridStateTitle);
            panel.Children.Add(_gridStateDetail);
            panel.Children.Add(_gridStateRetry);

            return new Border
            {
                MinHeight = 430,
                Background = ColorCardBg,
                Padding = new Thickness(24),
                Child = panel
            };
        }

        // =================================================================
        //  BULK ACTION BAR
        // =================================================================
        private Border BuildBulkActionBar()
        {
            var border = new Border
            {
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(12, 10, 12, 10),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(245, 247, 250)),
                BorderBrush = ColorBorder,
                BorderThickness = new Thickness(1),
                Visibility = Visibility.Collapsed
            };

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var selCount = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = ColorTextPrimary,
                Margin = new Thickness(0, 0, 16, 0)
            };

            var assignBtn = new Button
            {
                Content = "  Assign Selected  ",
                Height = 30,
                Cursor = Cursors.Hand,
                Background = ColorAccentGreen,
                Foreground = ColorWhite,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 6, 0)
            };
            assignBtn.Click += async (s, e) => await BulkAssignAsync();

            var inProgressBtn = new Button
            {
                Content = "  Set In Progress  ",
                Height = 30,
                Cursor = Cursors.Hand,
                Background = ColorAccentBlue,
                Foreground = ColorWhite,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 6, 0)
            };
            inProgressBtn.Click += async (s, e) => await BulkSetStatusAsync("In Progress");

            var solvedBtn = new Button
            {
                Content = "  Mark Solved  ",
                Height = 30,
                Cursor = Cursors.Hand,
                Background = ColorAccentGreen,
                Foreground = ColorWhite,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 6, 0)
            };
            solvedBtn.Click += async (s, e) => await BulkSetStatusAsync("Solved");

            panel.Children.Add(selCount);
            panel.Children.Add(assignBtn);
            panel.Children.Add(inProgressBtn);
            panel.Children.Add(solvedBtn);

            border.Child = panel;
            return border;
        }

        private void UpdateBulkBar()
        {
            var count = _grid.SelectedItems.Count;
            _bulkBar.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;

            var panel = _bulkBar?.Child as Panel;
            var tb = panel?.Children.OfType<TextBlock>().FirstOrDefault();
            if (tb != null)
                tb.Text = count == 1
                    ? "1 request selected"
                    : count + " requests selected";
        }

        private async Task<List<LookupItem>> LoadAssignmentCandidatesAsync()
        {
            var departments = await _repository.GetDepartmentsAsync() ?? new List<LookupItem>();
            var itDepartment = (departments ?? Enumerable.Empty<LookupItem>())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Name) &&
                                     x.Name.IndexOf("IT", StringComparison.OrdinalIgnoreCase) >= 0)
                ?.Name ?? "IT";

            return await _repository.GetCallAssignmentCandidatesByDepartmentNameAsync(itDepartment)
                   ?? new List<LookupItem>();
        }

        private async Task BulkAssignAsync()
        {
            var selected = _grid.SelectedItems.Cast<CallTicketListItem>().ToList();
            if (selected.Count == 0) return;

            var employees = await LoadAssignmentCandidatesAsync();
            if (employees == null || employees.Count == 0)
            {
                MessageBox.Show("No employees found to assign.", "Assign Tickets", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Window
            {
                Title = $"Assign {selected.Count} Ticket{(selected.Count != 1 ? "s" : "")}",
                Width = 400,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ShowInTaskbar = false,
                Background = new SolidColorBrush(Color.FromRgb(245, 247, 250))
            };

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(new TextBlock
            {
                Text = $"Select employee to assign ({selected.Count} ticket{(selected.Count != 1 ? "s" : "")}):",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = ColorTextPrimary
            });

            var listBox = new ListBox
            {
                DisplayMemberPath = "DisplayName",
                FontSize = 13,
                BorderBrush = ColorBorder
            };
            foreach (var emp in employees)
                listBox.Items.Add(emp);
            Grid.SetRow(listBox, 1);
            root.Children.Add(listBox);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var cancelBtn = new Button { Content = "Cancel", Width = 80, Height = 32, Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand };
            cancelBtn.Click += (_, __) => dialog.DialogResult = false;
            var okBtn = new Button
            {
                Content = "Assign",
                Width = 80,
                Height = 32,
                IsEnabled = false,
                Background = ColorAccentGreen,
                Foreground = ColorWhite,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            okBtn.Click += async (_, __) =>
            {
                var emp = listBox.SelectedItem as LookupItem;
                if (emp == null) return;
                okBtn.IsEnabled = false;
                foreach (var ticket in selected)
                {
                    await new CallEmailNotificationService(_repository).AssignTicketAndNotifyAsync(
                        ticket.TicketId, emp.Id, AppSession.CurrentUserId);
                }
                dialog.DialogResult = true;
            };
            listBox.SelectionChanged += (_, __) => okBtn.IsEnabled = listBox.SelectedItem != null;
            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(okBtn);
            Grid.SetRow(btnPanel, 2);
            root.Children.Add(btnPanel);

            dialog.Content = root;
            if (dialog.ShowDialog() == true)
                await LoadDataAsync();
        }

        private async Task BulkSetStatusAsync(string status)
        {
            var selected = _grid.SelectedItems.Cast<CallTicketListItem>().ToList();
            if (selected.Count == 0) return;

            var msg = $"Set {selected.Count} ticket{(selected.Count != 1 ? "s" : "")} to \"{status}\"?";
            if (status == "Solved")
            {
                msg += "\n\nThis marks them Resolved (Temporary) as a Service Only triage resolution.";
            }

            var result = MessageBox.Show(msg, "Bulk Status Update",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            foreach (var ticket in selected)
            {
                if (status == "In Progress")
                {
                    await _repository.SetTicketStatusAsync(
                        ticket.TicketId, "In Progress", AppSession.CurrentUserId);
                }
                else if (status == "Solved")
                {
                    // Triage fast path keeps no Mark As dialog, but still logs
                    // a Service Only resolution row so reports stay consistent.
                    await _repository.LogTicketResolutionAsync(
                        ticket.TicketId, "Service Only",
                        "Bulk resolved from Incoming Portal Tickets page.",
                        AppSession.CurrentUserId);
                    await _repository.SetTicketStatusAsync(
                        ticket.TicketId, "Resolved (Temporary)", AppSession.CurrentUserId,
                        "Bulk resolved from Incoming Portal Tickets page.");
                }
            }

            await LoadDataAsync();
        }

        // =================================================================
        //  PAGER
        // =================================================================
        private Border BuildPager()
        {
            var border = new Border
            {
                Margin = new Thickness(0, 12, 0, 0),
                Padding = new Thickness(12, 8, 12, 8),
                Background = ColorCardBg,
                BorderBrush = ColorBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _prevBtn = new Button
            {
                Content = "  \u2190 Prev  ",
                Height = 30,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0),
                IsEnabled = false,
                Background = ColorAccentBlue,
                Foreground = ColorWhite,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0)
            };
            _prevBtn.Click += async (s, e) =>
            {
                if (_currentPage > 1) { _currentPage--; await LoadDataAsync(); }
            };

            _pageInfo = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13,
                Margin = new Thickness(8, 0, 8, 0),
                Foreground = ColorTextSecondary
            };

            _nextBtn = new Button
            {
                Content = "  Next \u2192  ",
                Height = 30,
                Cursor = Cursors.Hand,
                IsEnabled = false,
                Background = ColorAccentBlue,
                Foreground = ColorWhite,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0)
            };
            _nextBtn.Click += async (s, e) =>
            {
                if (_hasNext) { _currentPage++; await LoadDataAsync(); }
            };

            // Page size combo
            _pageSizeCombo = new ComboBox
            {
                Width = 70,
                Height = 30,
                FontSize = 13,
                Margin = new Thickness(12, 0, 0, 0)
            };
            _pageSizeCombo.Items.Add("25");
            _pageSizeCombo.Items.Add("50");
            _pageSizeCombo.Items.Add("100");
            _pageSizeCombo.SelectedIndex = 0;
            _pageSizeCombo.SelectionChanged += async (s, e) =>
            {
                if (int.TryParse(_pageSizeCombo.SelectedItem as string, out var size))
                {
                    _pageSize = size;
                    _currentPage = 1;
                    await LoadDataAsync();
                }
            };

            var sizeLabel = new TextBlock
            {
                Text = "Per page:",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = ColorTextSecondary,
                Margin = new Thickness(0, 0, 6, 0)
            };

            panel.Children.Add(_prevBtn);
            panel.Children.Add(_pageInfo);
            panel.Children.Add(_nextBtn);
            panel.Children.Add(sizeLabel);
            panel.Children.Add(_pageSizeCombo);

            border.Child = panel;
            return border;
        }

        // =================================================================
        //  TEMPLATES
        // =================================================================
        private DataTemplate CreatePriorityTemplate()
        {
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            factory.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));
            factory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            factory.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);

            var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
            tbFactory.SetValue(TextBlock.TextProperty, new Binding("Priority"));
            tbFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            tbFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            tbFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            factory.AppendChild(tbFactory);

            var template = new DataTemplate { VisualTree = factory };
            var bgBinding = new Binding("Priority")
            {
                Converter = new PriorityToColorConverter()
            };
            factory.SetBinding(Border.BackgroundProperty, bgBinding);
            return template;
        }

        private DataTemplate CreateStatusTemplate()
        {
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            factory.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));
            factory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            factory.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);

            var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
            tbFactory.SetValue(TextBlock.TextProperty, new Binding("Status"));
            tbFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            tbFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            tbFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            factory.AppendChild(tbFactory);

            var template = new DataTemplate { VisualTree = factory };
            var bgBinding = new Binding("Status")
            {
                Converter = new StatusToColorConverter()
            };
            factory.SetBinding(Border.BackgroundProperty, bgBinding);
            return template;
        }

        private DataTemplate CreateActionsTemplate()
        {
            var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackFactory.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            var assignBtnFactory = new FrameworkElementFactory(typeof(Button));
            assignBtnFactory.SetValue(Button.ContentProperty, "Assign");
            assignBtnFactory.SetValue(Button.HeightProperty, 26.0);
            assignBtnFactory.SetValue(Button.CursorProperty, Cursors.Hand);
            assignBtnFactory.SetValue(Button.MarginProperty, new Thickness(0, 0, 4, 0));
            assignBtnFactory.SetValue(Button.BackgroundProperty, ColorAccentGreen);
            assignBtnFactory.SetValue(Button.ForegroundProperty, ColorWhite);
            assignBtnFactory.SetValue(Button.FontWeightProperty, FontWeights.SemiBold);
            assignBtnFactory.SetValue(Button.BorderThicknessProperty, new Thickness(0));
            assignBtnFactory.SetValue(Button.FontSizeProperty, 11.0);
            assignBtnFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnAssignClick));

            var statusBtnFactory = new FrameworkElementFactory(typeof(Button));
            statusBtnFactory.SetValue(Button.ContentProperty, "Status");
            statusBtnFactory.SetValue(Button.HeightProperty, 26.0);
            statusBtnFactory.SetValue(Button.CursorProperty, Cursors.Hand);
            statusBtnFactory.SetValue(Button.BackgroundProperty, ColorAccentBlue);
            statusBtnFactory.SetValue(Button.ForegroundProperty, ColorWhite);
            statusBtnFactory.SetValue(Button.FontWeightProperty, FontWeights.SemiBold);
            statusBtnFactory.SetValue(Button.BorderThicknessProperty, new Thickness(0));
            statusBtnFactory.SetValue(Button.FontSizeProperty, 11.0);
            statusBtnFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnStatusClick));

            stackFactory.AppendChild(assignBtnFactory);
            stackFactory.AppendChild(statusBtnFactory);
            return new DataTemplate { VisualTree = stackFactory };
        }

        // =================================================================
        //  EVENT HANDLERS
        // =================================================================
        private async void OnAssignClick(object sender, RoutedEventArgs e)
        {
            var item = GetRowItemFromChild(sender as DependencyObject);
            if (item == null || _repository == null) return;

            var employees = await LoadAssignmentCandidatesAsync();
            if (employees == null || employees.Count == 0)
            {
                MessageBox.Show("No employees found to assign.", "Assign Ticket", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Window
            {
                Title = "Assign Ticket - " + item.TicketCode,
                Width = 400,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ShowInTaskbar = false,
                Background = new SolidColorBrush(Color.FromRgb(245, 247, 250))
            };

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(new TextBlock
            {
                Text = "Select employee to assign:",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = ColorTextPrimary
            });

            var listBox = new ListBox
            {
                DisplayMemberPath = "DisplayName",
                FontSize = 13,
                BorderBrush = ColorBorder
            };
            foreach (var emp in employees)
                listBox.Items.Add(emp);
            Grid.SetRow(listBox, 1);
            root.Children.Add(listBox);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var cancelBtn = new Button { Content = "Cancel", Width = 80, Height = 32, Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand };
            cancelBtn.Click += (_, __) => dialog.DialogResult = false;
            var okBtn = new Button
            {
                Content = "Assign",
                Width = 80,
                Height = 32,
                IsEnabled = false,
                Background = ColorAccentGreen,
                Foreground = ColorWhite,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            okBtn.Click += async (_, __) =>
            {
                var selected = listBox.SelectedItem as LookupItem;
                if (selected != null)
                {
                    okBtn.IsEnabled = false;
                    await new CallEmailNotificationService(_repository).AssignTicketAndNotifyAsync(
                        item.TicketId, selected.Id, AppSession.CurrentUserId);
                    dialog.DialogResult = true;
                }
            };
            listBox.SelectionChanged += (_, __) => okBtn.IsEnabled = listBox.SelectedItem != null;
            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(okBtn);
            Grid.SetRow(btnPanel, 2);
            root.Children.Add(btnPanel);

            dialog.Content = root;
            if (dialog.ShowDialog() == true)
                await LoadDataAsync();
        }

        private async void OnStatusClick(object sender, RoutedEventArgs e)
        {
            var item = GetRowItemFromChild(sender as DependencyObject);
            if (item == null || _repository == null) return;

            var dialog = new WpfTicketStatusActionDialog(
                item,
                targetStatus: null,
                targetPriority: null,
                priorityChanged: false,
                initialNote: null);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                await LoadDataAsync();
            }
        }

        // =================================================================
        //  HERO UPDATE HELPERS
        // =================================================================
        private void UpdateHeroSummary()
        {
            if (_summary == null) return;

            var total = _summary.TotalCount;
            _heroSummary.Text = total == 0
                ? "No untriaged portal requests right now. New submissions will appear here automatically."
                : total == 1
                    ? "1 unassigned portal request is waiting for IT triage. Assign an owner, or assign and start work, to move it into active handling."
                    : $"{total:N0} unassigned portal requests are waiting for IT triage. Assign an owner or start work to move them to Pending.";
            _heroLastRefreshed.Text = "Last refreshed: " + DateTime.Now.ToString("MMM dd, h:mm tt");

            if (_heroTotalCount != null) _heroTotalCount.Text = total.ToString("N0");
            if (_heroCriticalCount != null) _heroCriticalCount.Text = _summary.Critical.ToString("N0");
            if (_heroHighCount != null) _heroHighCount.Text = _summary.High.ToString("N0");
            if (_heroOldTicketsCount != null) _heroOldTicketsCount.Text = _summary.OldTickets.ToString("N0");
        }
        private void UpdateQuickFilterBadges()
        {
            if (_summary == null) return;

            foreach (var child in _quickFilterPanel.Children)
            {
                var border = child as Border;
                if (border == null) continue;
                var tb = border.Child as TextBlock;
                if (tb == null) continue;

                var status = border.Tag as string ?? "";
                var label = string.IsNullOrEmpty(status) ? "All" : status;
                var count = status switch
                {
                    "" => _summary.TotalCount,
                    "Pending" => _summary.Pending,
                    "In Progress" => _summary.InProgress,
                    "Escalated" => _summary.Escalated,
                    "Solved" => _summary.Solved,
                    "Critical" => _summary.Critical,
                    "Unassigned" => _summary.Unassigned,
                    _ => 0
                };

                tb.Text = $"{label} ({count})";
            }
        }

        private void UpdateActiveFilterSummary()
        {
            if (_activeFilterSummary == null) return;

            var filters = new System.Collections.Generic.List<string>();
            var search = _searchBox?.Text?.Trim();
            var status = _quickFilterStatus ?? (_statusFilter?.SelectedValue as string);
            var priority = _quickFilterPriority ?? (_priorityFilter?.SelectedValue as string);
            var unassigned = _quickFilterUnassigned || (_chkUnassigned?.IsChecked == true);

            if (!string.IsNullOrWhiteSpace(search)) filters.Add($"Search: {search}");
            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", System.StringComparison.OrdinalIgnoreCase))
                filters.Add($"Status: {status}");
            if (!string.IsNullOrWhiteSpace(priority) && !string.Equals(priority, "All", System.StringComparison.OrdinalIgnoreCase))
                filters.Add($"Priority: {priority}");
            // The repository permanently scopes this workspace to unassigned portal requests.

            _activeFilterSummary.Text = filters.Count == 0
                ? "Queue scope: unassigned portal requests"
                : "Queue scope: unassigned portal requests  •  Filters: " + string.Join("  •  ", filters);
        }

        // =================================================================
        //  HELPERS
        // =================================================================
        private void SetGridState(string title, string detail, bool isError, bool showGrid)
        {
            if (_grid == null || _gridState == null) return;

            _grid.Visibility = showGrid ? Visibility.Visible : Visibility.Collapsed;
            _gridState.Visibility = showGrid ? Visibility.Collapsed : Visibility.Visible;
            if (_pagerBar != null)
                _pagerBar.Visibility = showGrid ? Visibility.Visible : Visibility.Collapsed;
            _gridStateTitle.Text = string.IsNullOrWhiteSpace(title) ? string.Empty : title;
            _gridStateDetail.Text = string.IsNullOrWhiteSpace(detail) ? string.Empty : detail;
            _gridStateRetry.Visibility = isError ? Visibility.Visible : Visibility.Collapsed;

            _gridState.BorderBrush = isError
                ? new SolidColorBrush(Color.FromRgb(254, 202, 202))
                : ColorBorder;
            _gridState.BorderThickness = new Thickness(0);
        }

        private void UpdatePagerState()
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalCount / _pageSize));
            _pageInfo.Text = $"Page {_currentPage} of {totalPages} ({_totalCount} total)";
            _prevBtn.IsEnabled = _currentPage > 1;
            _nextBtn.IsEnabled = _hasNext;
        }

        private static CallTicketListItem GetRowItemFromChild(DependencyObject child)
        {
            while (child != null && !(child is DataGridRow))
                child = VisualTreeHelper.GetParent(child);

            if (child is DataGridRow row)
                return row.Item as CallTicketListItem;

            return null;
        }

        private static void SafeFireAndForget(Task task)
        {
            if (task == null) return;
            task.ContinueWith(
                t => Logger.LogError("[WpfIncomingTicketsWorkspace] Background task failed.", t.Exception?.GetBaseException()),
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    #region Value Converters

    public sealed class PriorityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var p = value as string;
            if (string.IsNullOrEmpty(p)) return new SolidColorBrush(Color.FromRgb(149, 165, 166));
            return p switch
            {
                "Critical" => new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                "High" => new SolidColorBrush(Color.FromRgb(230, 126, 34)),
                "Medium" => new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                "Low" => new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var s = value as string;
            if (string.IsNullOrEmpty(s)) return new SolidColorBrush(Color.FromRgb(149, 165, 166));
            return s switch
            {
                "Pending" => new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                "In Progress" => new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                "Escalated" => new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                "Solved" => new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                "Resolved (Temporary)" => new SolidColorBrush(Color.FromRgb(155, 89, 182)),
                "Reopened" => new SolidColorBrush(Color.FromRgb(230, 126, 34)),
                _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}
