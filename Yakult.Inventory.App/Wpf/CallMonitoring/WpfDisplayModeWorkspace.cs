using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.Win32;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public enum BoardGroupMode { Status, Branch, Department, Employee }

    public sealed class WpfDisplayModeWorkspace : UserControl
    {
        private ICallMonitoringRepository _repository;
        private ICallMonitoringNavigator _navigator;

        private readonly TextBlock _clockText;
        private readonly TextBlock _refreshInfoText;
        private readonly Button _refreshBtn;

        private readonly TextBlock _openCountText;
        private readonly TextBlock _overdueCountText;
        private readonly TextBlock _escalatedCountText;
        private readonly TextBlock _unassignedCountText;

        private readonly StackPanel _pendingBoard;
        private readonly StackPanel _progressBoard;
        private readonly StackPanel _escalatedBoard;
        private readonly StackPanel _overdueBoard;

        private readonly TextBlock _pendingHeader;
        private readonly TextBlock _progressHeader;
        private readonly TextBlock _escalatedHeader;
        private readonly TextBlock _overdueHeader;

        private BoardGroupMode _groupMode = BoardGroupMode.Status;
        private Button _btnGroupStatus, _btnGroupBranch, _btnGroupDept, _btnGroupEmp;
        private string _boardSearchText = string.Empty;
        private Grid _statusGrid;
        private Grid _boardCanvas;

        private readonly DataGrid _tableGrid;
        private readonly Border _tableEmptyState;
        private readonly TextBlock _tableEmptyStateText;
        private TextBlock _tableCountText;

        private readonly TextBlock _viewSummaryText;
        private readonly TextBlock _overdueRuleText;

        private readonly TabControl _tabControl;

        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _clockTimer;

        private bool _isLoading;
        private int _overdueDays = 3;
        private int _lastTicketCount;
        private int _lastOverdueCount;
        private int _lastEscalatedCount;
        private int _lastUnassignedCount;
        private List<CallTicketListItem> _currentTickets;

        public WpfDisplayModeWorkspace()
        {
            Background = BrushFromRgb(239, 243, 247);

            var scrollbarXaml = @"
                <ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                    <Style TargetType=""ScrollBar"">
                        <Setter Property=""Background"" Value=""Transparent""/>
                        <Setter Property=""Width"" Value=""10""/>
                        <Setter Property=""Template"">
                            <Setter.Value>
                                <ControlTemplate TargetType=""ScrollBar"">
                                    <Border Background=""Transparent"">
                                        <Track x:Name=""PART_Track"" IsDirectionReversed=""true"">
                                            <Track.Thumb>
                                                <Thumb>
                                                    <Thumb.Template>
                                                        <ControlTemplate TargetType=""Thumb"">
                                                            <Border Background=""#cbd5e1"" CornerRadius=""3"" Margin=""2,0,2,0""/>
                                                        </ControlTemplate>
                                                    </Thumb.Template>
                                                </Thumb>
                                            </Track.Thumb>
                                        </Track>
                                    </Border>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>
                </ResourceDictionary>";
            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            ctx.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
            Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(scrollbarXaml, ctx));

            var root = new Grid { Margin = new Thickness(28, 24, 28, 28) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Content = root;

            var topPanel = new StackPanel();
            topPanel.Children.Add(BuildHeader(out _clockText, out _refreshInfoText, out _refreshBtn));
            
            var summaryGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            for(int i = 0; i < 4; i++) summaryGrid.ColumnDefinitions.Add(new ColumnDefinition());
            
            summaryGrid.Children.Add(SetGridColumn(BuildSummaryCard("Open Tickets", "All active work on screen", out _openCountText, BrushFromRgb(52, 152, 219)), 0));
            summaryGrid.Children.Add(SetGridColumn(BuildSummaryCard("Overdue", "Needs immediate attention", out _overdueCountText, BrushFromRgb(231, 76, 60)), 1));
            summaryGrid.Children.Add(SetGridColumn(BuildSummaryCard("Escalated", "Already pushed for action", out _escalatedCountText, BrushFromRgb(230, 126, 34)), 2));
            summaryGrid.Children.Add(SetGridColumn(BuildSummaryCard("Unassigned", "Owner still missing", out _unassignedCountText, BrushFromRgb(108, 117, 125)), 3));
            topPanel.Children.Add(summaryGrid);

            var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 16) };
            var exportExcelBtn = CreateActionButton("Export Excel", BrushFromRgb(46, 125, 50));
            exportExcelBtn.Click += ExportExcelBtn_Click;
            var exportPdfBtn = CreateActionButton("Export PDF", BrushFromRgb(211, 47, 47));
            exportPdfBtn.Click += ExportPdfBtn_Click;
            actionsPanel.Children.Add(exportExcelBtn);
            actionsPanel.Children.Add(exportPdfBtn);
            topPanel.Children.Add(actionsPanel);

            var infoPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
            infoPanel.Children.Add(CreateInfoChip("Auto refresh every 30s", BrushFromRgb(227, 242, 253), BrushFromRgb(30, 87, 153)));
            
            var overdueRuleChip = CreateInfoChip("Overdue rule: 3d idle", BrushFromRgb(255, 243, 224), BrushFromRgb(166, 95, 26));
            _overdueRuleText = (TextBlock)((Border)overdueRuleChip).Child;
            infoPanel.Children.Add(overdueRuleChip);
            
            infoPanel.Children.Add(CreateInfoChip("Double-click any card or row to open details", BrushFromRgb(237, 247, 237), BrushFromRgb(39, 94, 54)));
            
            _viewSummaryText = new TextBlock { Text = "Board | waiting for ticket data...", FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(73, 80, 87), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            infoPanel.Children.Add(_viewSummaryText);
            topPanel.Children.Add(infoPanel);

            Grid.SetRow(topPanel, 0);
            root.Children.Add(topPanel);

            _tabControl = new TabControl { Margin = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            var tabStyleXaml = @"
                <Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" TargetType=""TabItem"">
                    <Setter Property=""Template"">
                        <Setter.Value>
                            <ControlTemplate TargetType=""TabItem"">
                                <Border Name=""Border"" Padding=""16,8"" Margin=""0,0,4,0"" CornerRadius=""6,6,0,0"" Background=""#e2e8f0"">
                                    <ContentPresenter x:Name=""ContentSite"" VerticalAlignment=""Center"" HorizontalAlignment=""Center"" ContentSource=""Header""/>
                                </Border>
                                <ControlTemplate.Triggers>
                                    <Trigger Property=""IsSelected"" Value=""True"">
                                        <Setter TargetName=""Border"" Property=""Background"" Value=""White""/>
                                        <Setter Property=""Foreground"" Value=""#0f172a""/>
                                        <Setter Property=""FontWeight"" Value=""SemiBold""/>
                                    </Trigger>
                                </ControlTemplate.Triggers>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                    <Setter Property=""Foreground"" Value=""#64748b""/>
                    <Setter Property=""FontSize"" Value=""14""/>
                </Style>";
            _tabControl.ItemContainerStyle = (Style)XamlReader.Parse(tabStyleXaml, ctx);
            _tabControl.SelectionChanged += (s, e) => { UpdateViewSummary(); };

            var boardItem = new TabItem { Header = "Board View" };
            var boardWrapper = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            boardWrapper.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            boardWrapper.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var groupBar = BuildBoardGroupBar();
            Grid.SetRow(groupBar, 0);
            boardWrapper.Children.Add(groupBar);
            _statusGrid = new Grid { Background = Brushes.Transparent, Margin = new Thickness(0, 8, 0, 0) };
            for (int i = 0; i < 4; i++) _statusGrid.ColumnDefinitions.Add(new ColumnDefinition());
            _statusGrid.Children.Add(SetGridColumn(BuildColumn("Pending", BrushFromRgb(52, 152, 219), BrushFromRgb(248, 251, 254), out _pendingHeader, out _pendingBoard), 0));
            _statusGrid.Children.Add(SetGridColumn(BuildColumn("In Progress", BrushFromRgb(155, 89, 182), BrushFromRgb(249, 247, 252), out _progressHeader, out _progressBoard), 1));
            _statusGrid.Children.Add(SetGridColumn(BuildColumn("Escalated", BrushFromRgb(230, 126, 34), BrushFromRgb(255, 249, 242), out _escalatedHeader, out _escalatedBoard), 2));
            _statusGrid.Children.Add(SetGridColumn(BuildColumn("Overdue", BrushFromRgb(231, 76, 60), BrushFromRgb(255, 247, 247), out _overdueHeader, out _overdueBoard), 3));
            _boardCanvas = new Grid();
            _boardCanvas.Children.Add(_statusGrid);
            Grid.SetRow(_boardCanvas, 1);
            boardWrapper.Children.Add(_boardCanvas);
            boardItem.Content = boardWrapper;

            var tableItem = new TabItem { Header = "Table View" };
            var tableCard = new Border
            {
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 16, 0, 0),
                Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 1, Color = Color.FromArgb(255, 15, 23, 42), Opacity = 0.05 }
            };
            var tableCardInner = new Grid();
            tableCardInner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            tableCardInner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var tableTopBorder = new Border { BorderBrush = BrushFromRgb(241, 245, 249), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(20, 14, 20, 14) };
            var tableTopGrid = new Grid();
            tableTopGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tableTopGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleRow.Children.Add(new TextBlock { Text = "Ticket List", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(15, 23, 42) });
            _tableCountText = new TextBlock { Text = "0 tickets", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(100, 116, 139) };
            titleRow.Children.Add(new Border { CornerRadius = new CornerRadius(999), Background = BrushFromRgb(241, 245, 249), Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(12, 0, 0, 0), Child = _tableCountText });
            Grid.SetColumn(titleRow, 0);
            tableTopGrid.Children.Add(titleRow);
            var hintText = new TextBlock { Text = "Double-click any row to open ticket details", FontSize = 11, Foreground = BrushFromRgb(148, 163, 184), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(hintText, 1);
            tableTopGrid.Children.Add(hintText);
            tableTopBorder.Child = tableTopGrid;
            Grid.SetRow(tableTopBorder, 0);
            tableCardInner.Children.Add(tableTopBorder);

            var tableGridHost = new Grid();
            _tableGrid = CreateDataGrid();
            _tableEmptyState = new Border { Background = Brushes.White, Visibility = Visibility.Collapsed };
            _tableEmptyStateText = new TextBlock { Text = "No open tickets to show.", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = BrushFromRgb(108, 117, 125), FontWeight = FontWeights.SemiBold, FontSize = 14 };
            _tableEmptyState.Child = _tableEmptyStateText;
            tableGridHost.Children.Add(_tableGrid);
            tableGridHost.Children.Add(_tableEmptyState);
            Grid.SetRow(tableGridHost, 1);
            tableCardInner.Children.Add(tableGridHost);
            tableCard.Child = tableCardInner;
            tableItem.Content = tableCard;

            _tabControl.Items.Add(boardItem);
            _tabControl.Items.Add(tableItem);
            Grid.SetRow(_tabControl, 1);
            root.Children.Add(_tabControl);

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _refreshTimer.Tick += async (s, e) => await LoadDataAsync();
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => _clockText.Text = DateTime.Now.ToString("hh:mm:ss tt");

            this.IsVisibleChanged += async (s, e) =>
            {
                if ((bool)e.NewValue)
                {
                    _clockTimer.Start();
                    _refreshTimer.Start();
                    _clockText.Text = DateTime.Now.ToString("hh:mm:ss tt");
                    await LoadDataAsync();
                }
                else
                {
                    _clockTimer.Stop();
                    _refreshTimer.Stop();
                }
            };
        }

        public void Initialize(ICallMonitoringRepository repository, ICallMonitoringNavigator navigator)
        {
            _repository = repository;
            _navigator = navigator;
        }

        public async Task LoadDataAsync(bool force = false)
        {
            if (_isLoading && !force) return;
            _isLoading = true;
            _refreshBtn.IsEnabled = false;
            _refreshInfoText.Text = "Refreshing...";

            try
            {
                if (_repository != null)
                {
                    _overdueDays = await _repository.GetOverdueDaysAsync(3);
                    var tickets = await _repository.GetTicketsAsync("All", null, 250) ?? new List<CallTicketListItem>();
                    _currentTickets = tickets;
                    RenderTickets(tickets);
                    _refreshInfoText.Text = $"Last refresh {DateTime.Now:MMM dd, yyyy hh:mm:ss tt}  |  {tickets.Count} active tickets";
                    _overdueRuleText.Text = $"Overdue rule: {Math.Max(1, _overdueDays)}d idle";
                }
            }
            catch (Exception ex)
            {
                ShowLoadError(ex.Message);
            }
            finally
            {
                _isLoading = false;
                _refreshBtn.IsEnabled = true;
            }
        }

        private void RenderTickets(List<CallTicketListItem> tickets)
        {
            var pending = new List<CallTicketListItem>();
            var progress = new List<CallTicketListItem>();
            var escalated = new List<CallTicketListItem>();
            var overdue = new List<CallTicketListItem>();
            var orderedTableRows = new List<CallTicketListItem>();

            foreach (var t in tickets.Where(t => t != null))
            {
                if (IsOverdue(t)) { overdue.Add(t); continue; }
                var s = NormalizeStatus(t.Status);
                if (s.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) escalated.Add(t);
                else if (s.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) progress.Add(t);
                else pending.Add(t);
            }

            _openCountText.Text = tickets.Count.ToString();
            _overdueCountText.Text = overdue.Count.ToString();
            _overdueCountText.Foreground = overdue.Count > 0 ? BrushFromRgb(192, 57, 43) : BrushFromRgb(22, 52, 86);
            _escalatedCountText.Text = escalated.Count.ToString();
            _escalatedCountText.Foreground = escalated.Count > 0 ? BrushFromRgb(211, 84, 0) : BrushFromRgb(22, 52, 86);
            
            var unassigned = tickets.Count(t => string.IsNullOrWhiteSpace(t.ResponsiblePerson));
            _unassignedCountText.Text = unassigned.ToString();

            _lastTicketCount = tickets.Count;
            _lastOverdueCount = overdue.Count;
            _lastEscalatedCount = escalated.Count;
            _lastUnassignedCount = unassigned;

            BindColumn(_pendingBoard, _pendingHeader, "Pending", pending);
            BindColumn(_progressBoard, _progressHeader, "In Progress", progress);
            BindColumn(_escalatedBoard, _escalatedHeader, "Escalated", escalated);
            BindColumn(_overdueBoard, _overdueHeader, "Overdue", overdue);

            orderedTableRows.AddRange(overdue.OrderByDescending(GetPriorityRank).ThenByDescending(t => Math.Max(t.IdleDays, t.TicketAgeDays)).ThenByDescending(t => t.UpdatedAt));
            orderedTableRows.AddRange(escalated.OrderByDescending(GetPriorityRank).ThenByDescending(t => Math.Max(t.IdleDays, t.TicketAgeDays)).ThenByDescending(t => t.UpdatedAt));
            orderedTableRows.AddRange(progress.OrderByDescending(GetPriorityRank).ThenByDescending(t => Math.Max(t.IdleDays, t.TicketAgeDays)).ThenByDescending(t => t.UpdatedAt));
            orderedTableRows.AddRange(pending.OrderByDescending(GetPriorityRank).ThenByDescending(t => Math.Max(t.IdleDays, t.TicketAgeDays)).ThenByDescending(t => t.UpdatedAt));
            
            _tableGrid.ItemsSource = orderedTableRows;
            if (_tableCountText != null)
                _tableCountText.Text = $"{orderedTableRows.Count} ticket{(orderedTableRows.Count != 1 ? "s" : "")}";

            if (orderedTableRows.Count == 0)
            {
                _tableEmptyState.Visibility = Visibility.Visible;
                _tableEmptyStateText.Text = "No open tickets to show in table view.";
            }
            else
            {
                _tableEmptyState.Visibility = Visibility.Collapsed;
            }

            UpdateViewSummary();
            if (_groupMode != BoardGroupMode.Status)
                RebuildBoardLayout();
        }

        private void BindColumn(StackPanel host, TextBlock header, string title, List<CallTicketListItem> tickets)
        {
            host.Children.Clear();
            var ordered = tickets.OrderByDescending(GetPriorityRank).ThenByDescending(t => Math.Max(t.IdleDays, t.TicketAgeDays)).ThenByDescending(t => t.UpdatedAt).ToList();
            header.Text = $"{title} ({ordered.Count})";
            
            if (ordered.Count == 0)
            {
                host.Children.Add(CreateEmptyCard(EmptyText(title)));
            }
            else
            {
                foreach (var t in ordered)
                {
                    host.Children.Add(CreateTicketCard(t));
                }
            }
        }

        private Border CreateTicketCard(CallTicketListItem ticket)
        {
            var accent = Accent(ticket);
            
            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(16, 14, 14, 14),
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect { BlurRadius = 4, ShadowDepth = 1, Color = Color.FromArgb(255, 15, 23, 42), Opacity = 0.05 }
            };

            var styleXaml = @"
                <Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" TargetType=""Border"">
                    <Style.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter Property=""Background"" Value=""#f8fafc"" />
                        </Trigger>
                    </Style.Triggers>
                </Style>";
            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            card.Style = (Style)XamlReader.Parse(styleXaml, ctx);

            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2 && _navigator != null)
                {
                    _navigator.OpenTicket(ticket.TicketId);
                }
            };

            var rootStack = new StackPanel();

            var topPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var codeText = new TextBlock
            {
                Text = ticket.TicketCode ?? $"#{ticket.TicketId}",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = BrushFromRgb(22, 52, 86),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(codeText, Dock.Left);
            topPanel.Children.Add(codeText);

            var priorityBadge = CreateBadge(Safe(ticket.Priority, "Unspecified").ToUpperInvariant(), accent, Brushes.White);
            DockPanel.SetDock(priorityBadge, Dock.Right);
            priorityBadge.HorizontalAlignment = HorizontalAlignment.Right;
            topPanel.Children.Add(priorityBadge);
            rootStack.Children.Add(topPanel);

            var issueText = new TextBlock
            {
                Text = Safe(ticket.Issue, "(No issue summary)"),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = BrushFromRgb(44, 62, 80),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 4)
            };
            rootStack.Children.Add(issueText);

            var contextText = new TextBlock
            {
                Text = $"{Safe(ticket.Branch, "-")} | {Safe(ticket.Department, "-")}",
                FontSize = 12,
                Foreground = BrushFromRgb(93, 109, 126),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 4)
            };
            rootStack.Children.Add(contextText);

            var ownerText = new TextBlock
            {
                Text = $"Owner: {Safe(ticket.ResponsiblePerson, "Unassigned")}",
                FontSize = 12,
                Foreground = BrushFromRgb(73, 80, 87),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 12)
            };
            rootStack.Children.Add(ownerText);

            var metricsWrap = new WrapPanel { Orientation = Orientation.Horizontal };
            metricsWrap.Children.Add(CreatePill($"Age {ShortDuration(TimeSpan.FromDays(Math.Max(0, ticket.TicketAgeDays)))}", BrushFromRgb(235, 245, 255), BrushFromRgb(30, 87, 153)));
            metricsWrap.Children.Add(CreatePill($"Idle {ShortDuration(TimeSpan.FromDays(Math.Max(0, ticket.IdleDays)))}", BrushFromRgb(255, 244, 230), BrushFromRgb(166, 95, 26)));
            if (!string.IsNullOrWhiteSpace(ticket.CallerName))
            {
                metricsWrap.Children.Add(CreatePill(FitChipLabelText("Caller", ticket.CallerName, 18), BrushFromRgb(240, 247, 240), BrushFromRgb(39, 94, 54)));
            }
            rootStack.Children.Add(metricsWrap);

            var accentBar = new Border { Width = 4, Background = accent, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(4, 0, 0, 4) };
            
            var grid = new Grid();
            grid.Children.Add(rootStack);
            grid.Children.Add(accentBar);
            
            // Adjust margin to give room for accent bar
            rootStack.Margin = new Thickness(8, 0, 0, 0);
            
            card.Child = grid;
            return card;
        }

        // ── Group-by bar ───────────────────────────────────────────────────────
        private FrameworkElement BuildBoardGroupBar()
        {
            var bar = new Border
            {
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 8, 0, 0)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var groupStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            groupStack.Children.Add(new TextBlock
            {
                Text = "Group by:",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(100, 116, 139),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            _btnGroupStatus = CreateGroupByButton("Status");
            _btnGroupBranch = CreateGroupByButton("Branch");
            _btnGroupDept = CreateGroupByButton("Department");
            _btnGroupEmp = CreateGroupByButton("Employee");

            _btnGroupStatus.Click += (_, __) => SetBoardGroupMode(BoardGroupMode.Status);
            _btnGroupBranch.Click += (_, __) => SetBoardGroupMode(BoardGroupMode.Branch);
            _btnGroupDept.Click += (_, __) => SetBoardGroupMode(BoardGroupMode.Department);
            _btnGroupEmp.Click += (_, __) => SetBoardGroupMode(BoardGroupMode.Employee);

            groupStack.Children.Add(_btnGroupStatus);
            groupStack.Children.Add(_btnGroupBranch);
            groupStack.Children.Add(_btnGroupDept);
            groupStack.Children.Add(_btnGroupEmp);
            Grid.SetColumn(groupStack, 0);
            grid.Children.Add(groupStack);

            var searchBorder = new Border
            {
                Background = BrushFromRgb(248, 250, 252),
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Height = 32, Width = 240,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var searchBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0)
            };
            searchBox.TextChanged += (_, __) =>
            {
                _boardSearchText = (searchBox.Text ?? string.Empty).Trim().ToLowerInvariant();
                if (_groupMode != BoardGroupMode.Status)
                    RebuildBoardLayout();
            };
            searchBorder.Child = searchBox;
            Grid.SetColumn(searchBorder, 2);
            grid.Children.Add(searchBorder);

            bar.Child = grid;
            UpdateGroupButtonStyles();
            return bar;
        }

        private static Button CreateGroupByButton(string text)
        {
            return new Button
            {
                Content = text,
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11.5, FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(1),
                BorderBrush = BrushFromRgb(210, 219, 230),
                Foreground = BrushFromRgb(71, 85, 105),
                Background = Brushes.Transparent
            };
        }

        private void SetBoardGroupMode(BoardGroupMode mode)
        {
            _groupMode = mode;
            UpdateGroupButtonStyles();
            _boardCanvas.Children.Clear();
            if (_groupMode == BoardGroupMode.Status)
                _boardCanvas.Children.Add(_statusGrid);
            else
                RebuildBoardLayout();
        }

        private void UpdateGroupButtonStyles()
        {
            if (_btnGroupStatus == null) return;
            void SetActive(Button btn, bool active)
            {
                btn.Background = active ? BrushFromRgb(14, 165, 233) : Brushes.Transparent;
                btn.Foreground = active ? Brushes.White : BrushFromRgb(71, 85, 105);
                btn.BorderBrush = active ? BrushFromRgb(14, 165, 233) : BrushFromRgb(210, 219, 230);
                btn.FontWeight = active ? FontWeights.Bold : FontWeights.SemiBold;
            }
            SetActive(_btnGroupStatus, _groupMode == BoardGroupMode.Status);
            SetActive(_btnGroupBranch, _groupMode == BoardGroupMode.Branch);
            SetActive(_btnGroupDept, _groupMode == BoardGroupMode.Department);
            SetActive(_btnGroupEmp, _groupMode == BoardGroupMode.Employee);
        }

        private void RebuildBoardLayout()
        {
            if (_groupMode == BoardGroupMode.Status || _currentTickets == null) return;

            var filtered = string.IsNullOrWhiteSpace(_boardSearchText)
                ? _currentTickets
                : _currentTickets.Where(t =>
                    (t.TicketCode ?? "").ToLowerInvariant().Contains(_boardSearchText) ||
                    (t.Issue ?? "").ToLowerInvariant().Contains(_boardSearchText) ||
                    (t.Branch ?? "").ToLowerInvariant().Contains(_boardSearchText) ||
                    (t.Department ?? "").ToLowerInvariant().Contains(_boardSearchText) ||
                    (t.ResponsiblePerson ?? "").ToLowerInvariant().Contains(_boardSearchText)
                ).ToList();

            _boardCanvas.Children.Clear();

            if (filtered.Count == 0)
            {
                _boardCanvas.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(_boardSearchText) ? "No tickets to display." : "No tickets match your search.",
                    FontSize = 15, Foreground = BrushFromRgb(148, 163, 184),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(60, 80, 60, 80)
                });
                return;
            }

            var scroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _groupMode == BoardGroupMode.Employee
                    ? BuildEmployeeLayout(filtered)
                    : BuildGroupLayout(filtered)
            };
            _boardCanvas.Children.Add(scroll);
        }

        // ── Branch / Department card layout ────────────────────────────────────
        private UIElement BuildGroupLayout(List<CallTicketListItem> tickets)
        {
            Func<CallTicketListItem, string> key = _groupMode == BoardGroupMode.Branch
                ? (t => string.IsNullOrWhiteSpace(t.Branch) ? "No Branch" : t.Branch.Trim())
                : (t => string.IsNullOrWhiteSpace(t.Department) ? "No Department" : t.Department.Trim());

            var wrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
            foreach (var g in tickets.GroupBy(key).OrderBy(g => g.Key))
                wrap.Children.Add(BuildGroupCard(g.Key, g.ToList()));
            return wrap;
        }

        private UIElement BuildGroupCard(string groupName, List<CallTicketListItem> tickets)
        {
            var overdueCount = tickets.Count(IsOverdue);
            var escalatedCount = tickets.Count(t => !IsOverdue(t) && NormalizeStatus(t.Status).Equals("Escalated", StringComparison.OrdinalIgnoreCase));
            var critHighCount = tickets.Count(t =>
            {
                var p = (t.Priority ?? "").Trim();
                return p.Equals("Critical", StringComparison.OrdinalIgnoreCase) || p.Equals("High", StringComparison.OrdinalIgnoreCase);
            });

            var headerColor = overdueCount > 0 ? Color.FromRgb(192, 57, 43)
                            : escalatedCount > 0 ? Color.FromRgb(230, 126, 34)
                            : Color.FromRgb(30, 41, 59);

            var card = new Border
            {
                Width = 420,
                Margin = new Thickness(0, 0, 16, 16),
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10)
            };

            var inner = new Grid();
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(220, GridUnitType.Pixel) });

            var headerBorder = new Border
            {
                Background = new SolidColorBrush(headerColor),
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                Padding = new Thickness(16, 12, 16, 12)
            };
            var hGrid = new Grid();
            hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new Border
            {
                Width = 32, Height = 32, CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                Margin = new Thickness(0, 0, 10, 0),
                Child = new TextBlock
                {
                    Text = groupName.Length > 0 ? groupName[0].ToString().ToUpper() : "?",
                    FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
            });
            nameStack.Children.Add(new TextBlock
            {
                Text = groupName, FontSize = 14, FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(nameStack, 0);
            hGrid.Children.Add(nameStack);

            var countBadge = new Border
            {
                CornerRadius = new CornerRadius(999),
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                Padding = new Thickness(10, 4, 10, 4), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = $"{tickets.Count} ticket{(tickets.Count != 1 ? "s" : "")}", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White }
            };
            Grid.SetColumn(countBadge, 1);
            hGrid.Children.Add(countBadge);
            headerBorder.Child = hGrid;
            Grid.SetRow(headerBorder, 0);
            inner.Children.Add(headerBorder);

            var chipsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 8, 12, 4) };
            void AddChip(string label, int count, Color bg, Color fg)
            {
                if (count <= 0) return;
                chipsPanel.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(999),
                    Background = new SolidColorBrush(Color.FromArgb(35, bg.R, bg.G, bg.B)),
                    Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0),
                    Child = new TextBlock { Text = $"{count} {label}", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(fg) }
                });
            }
            AddChip("overdue", overdueCount, Color.FromRgb(192, 57, 43), Color.FromRgb(192, 57, 43));
            AddChip("escalated", escalatedCount, Color.FromRgb(230, 126, 34), Color.FromRgb(230, 126, 34));
            AddChip("crit/high", critHighCount, Color.FromRgb(231, 76, 60), Color.FromRgb(231, 76, 60));
            Grid.SetRow(chipsPanel, 1);
            inner.Children.Add(chipsPanel);

            var itemStack = new StackPanel { Margin = new Thickness(10, 4, 10, 8) };
            foreach (var t in tickets
                .OrderByDescending(t => IsOverdue(t) ? 2 : NormalizeStatus(t.Status).Equals("Escalated", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenByDescending(GetPriorityRank)
                .ThenByDescending(t => Math.Max(t.IdleDays, t.TicketAgeDays)))
                itemStack.Children.Add(BuildGroupCardRow(t));

            var itemScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = itemStack };
            Grid.SetRow(itemScroll, 2);
            inner.Children.Add(itemScroll);

            card.Child = inner;
            return card;
        }

        private UIElement BuildGroupCardRow(CallTicketListItem t)
        {
            var isOd = IsOverdue(t);
            var status = NormalizeStatus(t.Status);
            var rowColor = isOd ? Color.FromRgb(192, 57, 43)
                : status.Equals("Escalated", StringComparison.OrdinalIgnoreCase) ? Color.FromRgb(230, 126, 34)
                : status.Equals("In Progress", StringComparison.OrdinalIgnoreCase) ? Color.FromRgb(155, 89, 182)
                : Color.FromRgb(52, 152, 219);

            var rowBorder = new Border
            {
                Tag = t,
                Background = BrushFromRgb(248, 250, 252), BorderBrush = BrushFromRgb(241, 245, 249),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(10, 7, 10, 7),
                Cursor = Cursors.Hand
            };
            rowBorder.MouseEnter += (_, __) => rowBorder.Background = BrushFromRgb(241, 245, 249);
            rowBorder.MouseLeave += (_, __) => rowBorder.Background = BrushFromRgb(248, 250, 252);
            rowBorder.MouseDown += (s, e) =>
            {
                if (e.ClickCount == 2 && ((Border)s).Tag is CallTicketListItem ticket && _navigator != null)
                    _navigator.OpenTicket(ticket.TicketId);
            };

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoStack = new StackPanel();
            infoStack.Children.Add(new TextBlock { Text = t.TicketCode ?? $"#{t.TicketId}", FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(15, 23, 42), TextTrimming = TextTrimming.CharacterEllipsis });
            infoStack.Children.Add(new TextBlock { Text = t.Issue ?? "(No summary)", FontSize = 10.5, Foreground = BrushFromRgb(100, 116, 139), TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(infoStack, 0);
            g.Children.Add(infoStack);

            var badge = new Border
            {
                CornerRadius = new CornerRadius(999),
                Background = new SolidColorBrush(Color.FromArgb(35, rowColor.R, rowColor.G, rowColor.B)),
                Padding = new Thickness(7, 2, 7, 2), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Child = new TextBlock { Text = isOd ? "Overdue" : status, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(rowColor) }
            };
            Grid.SetColumn(badge, 1);
            g.Children.Add(badge);

            rowBorder.Child = g;
            return rowBorder;
        }

        // ── Employee mosaic layout ─────────────────────────────────────────────
        private UIElement BuildEmployeeLayout(List<CallTicketListItem> tickets)
        {
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
            foreach (var g in tickets
                .GroupBy(t => string.IsNullOrWhiteSpace(t.ResponsiblePerson) ? "Unassigned" : t.ResponsiblePerson.Trim())
                .OrderBy(g => g.Key))
                wrap.Children.Add(BuildEmployeeTile(g.Key, g.ToList()));
            return wrap;
        }

        private UIElement BuildEmployeeTile(string empName, List<CallTicketListItem> tiles)
        {
            var hasOverdue = tiles.Any(IsOverdue);
            var hasEscalated = tiles.Any(t => !IsOverdue(t) && NormalizeStatus(t.Status).Equals("Escalated", StringComparison.OrdinalIgnoreCase));
            var hasInProgress = tiles.Any(t => NormalizeStatus(t.Status).Equals("In Progress", StringComparison.OrdinalIgnoreCase));
            var accentColor = hasOverdue ? Color.FromRgb(192, 57, 43)
                            : hasEscalated ? Color.FromRgb(230, 126, 34)
                            : hasInProgress ? Color.FromRgb(155, 89, 182)
                            : Color.FromRgb(52, 152, 219);
            var deptName = tiles.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Department))?.Department ?? "";

            var tile = new Border
            {
                Width = 210, Margin = new Thickness(0, 0, 16, 16),
                Background = Brushes.White, BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Cursor = Cursors.Hand
            };
            tile.MouseEnter += (_, __) => tile.Background = BrushFromRgb(248, 250, 252);
            tile.MouseLeave += (_, __) => tile.Background = Brushes.White;

            var inner = new Grid();
            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var strip = new Border { Background = new SolidColorBrush(accentColor), CornerRadius = new CornerRadius(10, 10, 0, 0) };
            Grid.SetRow(strip, 0);
            inner.Children.Add(strip);

            var avatarSection = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(12, 16, 12, 8) };
            avatarSection.Children.Add(new Border
            {
                Width = 58, Height = 58, CornerRadius = new CornerRadius(29),
                Background = new SolidColorBrush(accentColor),
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10),
                Child = new TextBlock { Text = GetBoardInitials(empName), FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            });
            avatarSection.Children.Add(new TextBlock { Text = empName, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(15, 23, 42), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrWhiteSpace(deptName))
                avatarSection.Children.Add(new TextBlock { Text = deptName, FontSize = 11, Foreground = BrushFromRgb(100, 116, 139), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetRow(avatarSection, 1);
            inner.Children.Add(avatarSection);

            var countBadge = new Border
            {
                CornerRadius = new CornerRadius(999),
                Background = new SolidColorBrush(Color.FromArgb(30, accentColor.R, accentColor.G, accentColor.B)),
                Padding = new Thickness(12, 4, 12, 4), HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
                Child = new TextBlock { Text = $"{tiles.Count} ticket{(tiles.Count != 1 ? "s" : "")} assigned", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(accentColor) }
            };
            Grid.SetRow(countBadge, 2);
            inner.Children.Add(countBadge);

            var itemStack = new StackPanel { Margin = new Thickness(10, 0, 10, 12) };
            itemStack.Children.Add(new Border { Height = 1, Background = BrushFromRgb(241, 245, 249), Margin = new Thickness(0, 0, 0, 8) });
            foreach (var t in tiles
                .OrderByDescending(t => IsOverdue(t) ? 2 : NormalizeStatus(t.Status).Equals("Escalated", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenByDescending(GetPriorityRank).Take(3))
            {
                var isOd = IsOverdue(t);
                var status = NormalizeStatus(t.Status);
                var tColor = isOd ? Color.FromRgb(192, 57, 43)
                    : status.Equals("Escalated", StringComparison.OrdinalIgnoreCase) ? Color.FromRgb(230, 126, 34)
                    : status.Equals("In Progress", StringComparison.OrdinalIgnoreCase) ? Color.FromRgb(155, 89, 182)
                    : Color.FromRgb(52, 152, 219);
                var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                rowGrid.Children.Add(new TextBlock { Text = t.Issue ?? t.TicketCode ?? "--", FontSize = 10.5, Foreground = BrushFromRgb(71, 85, 105), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
                var lbl = new TextBlock { Text = isOd ? "Overdue" : status, FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(tColor), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(lbl, 1);
                rowGrid.Children.Add(lbl);
                itemStack.Children.Add(rowGrid);
            }
            if (tiles.Count > 3)
                itemStack.Children.Add(new TextBlock { Text = $"+{tiles.Count - 3} more", FontSize = 10, Foreground = BrushFromRgb(148, 163, 184), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetRow(itemStack, 3);
            inner.Children.Add(itemStack);

            tile.Child = inner;
            return tile;
        }

        private static string GetBoardInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0][0].ToString().ToUpper();
            return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
        }

        private Border CreateEmptyCard(string text)
        {
            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(16, 24, 16, 24),
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1)
            };

            card.Child = new TextBlock
            {
                Text = text,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = BrushFromRgb(108, 117, 125),
                TextWrapping = TextWrapping.Wrap
            };
            return card;
        }

        private void ShowLoadError(string message)
        {
            _openCountText.Text = _overdueCountText.Text = _escalatedCountText.Text = _unassignedCountText.Text = "-";
            _pendingBoard.Children.Clear();
            _progressBoard.Children.Clear();
            _escalatedBoard.Children.Clear();
            _overdueBoard.Children.Clear();
            
            _pendingBoard.Children.Add(CreateErrorCard(message));
            _progressBoard.Children.Add(CreateErrorCard(message));
            _escalatedBoard.Children.Add(CreateErrorCard(message));
            _overdueBoard.Children.Add(CreateErrorCard(message));

            _tableGrid.ItemsSource = null;
            _tableEmptyState.Visibility = Visibility.Visible;
            _tableEmptyStateText.Text = $"Unable to load tickets.\r\n{Safe(message, "Refresh again to retry.")}";
            
            _refreshInfoText.Text = "Refresh failed: " + message;
            UpdateViewSummary();
        }

        private Border CreateErrorCard(string message)
        {
            var card = new Border
            {
                Background = BrushFromRgb(255, 244, 244),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(16, 24, 16, 24),
                BorderBrush = BrushFromRgb(241, 196, 196),
                BorderThickness = new Thickness(1)
            };

            card.Child = new TextBlock
            {
                Text = Safe(message, "Unable to load tickets."),
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = BrushFromRgb(192, 57, 43),
                TextWrapping = TextWrapping.Wrap
            };
            return card;
        }

        private void UpdateViewSummary()
        {
            var mode = _tabControl != null && _tabControl.SelectedIndex == 1 ? "Table" : "Board";
            if (_viewSummaryText != null)
            {
                _viewSummaryText.Text = $"{mode} | {_lastTicketCount} open | {_lastOverdueCount} overdue | {_lastEscalatedCount} escalated | {_lastUnassignedCount} unassigned";
            }
        }

        private FrameworkElement BuildHeader(out TextBlock clock, out TextBlock refreshInfo, out Button refreshBtn)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 16),
                Padding = new Thickness(28, 20, 24, 20),
                Background = new SolidColorBrush(Color.FromRgb(18, 52, 86)),
                Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Color = Color.FromArgb(255, 18, 52, 86), Opacity = 0.2 }
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock { Text = "Open Tickets Display", FontSize = 28, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            titleStack.Children.Add(new TextBlock { Text = "Live ITCM wallboard for active tickets and immediate action", FontSize = 14, Foreground = BrushFromRgb(213, 232, 250), Margin = new Thickness(0, 4, 0, 0) });
            grid.Children.Add(titleStack);

            var rightStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            clock = new TextBlock { Text = "--:--:--", FontSize = 28, FontWeight = FontWeights.Bold, Foreground = Brushes.White, TextAlignment = TextAlignment.Right };
            refreshInfo = new TextBlock { Text = "Refreshing...", FontSize = 12, Foreground = BrushFromRgb(205, 226, 246), TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 4, 0, 8) };
            
            refreshBtn = new Button
            {
                Content = "Refresh Now",
                Padding = new Thickness(16, 8, 16, 8),
                Background = BrushFromRgb(0, 160, 148),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var btnTemplateXaml = @"
                <ControlTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" TargetType=""Button"">
                    <Border CornerRadius=""6"" Background=""{TemplateBinding Background}"" Padding=""{TemplateBinding Padding}"">
                        <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter Property=""Opacity"" Value=""0.9"" />
                        </Trigger>
                        <Trigger Property=""IsPressed"" Value=""True"">
                            <Setter Property=""Opacity"" Value=""0.8"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>";
            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            refreshBtn.Template = (ControlTemplate)XamlReader.Parse(btnTemplateXaml, ctx);
            refreshBtn.Click += async (s, e) => await LoadDataAsync(true);

            rightStack.Children.Add(clock);
            rightStack.Children.Add(refreshInfo);
            rightStack.Children.Add(refreshBtn);

            Grid.SetColumn(rightStack, 1);
            grid.Children.Add(rightStack);
            
            border.Child = grid;
            return border;
        }

        private Border BuildSummaryCard(string title, string caption, out TextBlock valueBlock, Brush accent)
        {
            var border = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 16, 0),
                Padding = new Thickness(20, 16, 20, 16),
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 1, Color = Color.FromArgb(255, 15, 23, 42), Opacity = 0.05 }
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(73, 80, 87) });

            valueBlock = new TextBlock { Text = "0", FontSize = 36, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(22, 52, 86), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 0, 8) };
            Grid.SetRow(valueBlock, 1);
            grid.Children.Add(valueBlock);

            var capBlock = new TextBlock { Text = caption, FontSize = 12, Foreground = BrushFromRgb(108, 117, 125) };
            Grid.SetRow(capBlock, 2);
            grid.Children.Add(capBlock);

            var topAccent = new Border { Height = 4, Background = accent, VerticalAlignment = VerticalAlignment.Top, CornerRadius = new CornerRadius(12, 12, 0, 0), Margin = new Thickness(-20, -16, -20, 0) };
            
            var mainContainer = new Grid();
            mainContainer.Children.Add(grid);
            mainContainer.Children.Add(topAccent);

            border.Child = mainContainer;
            return border;
        }

        private Border BuildColumn(string title, Brush accent, Brush surface, out TextBlock headerText, out StackPanel board)
        {
            var columnBorder = new Border
            {
                Background = surface,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(6, 0, 6, 0),
                BorderBrush = BrushFromRgb(221, 227, 234),
                BorderThickness = new Thickness(1)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var headerBorder = new Border { Background = accent, Padding = new Thickness(16, 12, 16, 12), CornerRadius = new CornerRadius(8, 8, 0, 0) };
            headerText = new TextBlock { Text = title, FontWeight = FontWeights.Bold, FontSize = 14, Foreground = Brushes.White };
            headerBorder.Child = headerText;
            grid.Children.Add(headerBorder);

            board = new StackPanel { Margin = new Thickness(12, 12, 12, 0) };
            var scroll = new ScrollViewer { Content = board, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, 0, 12) };
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);

            columnBorder.Child = grid;
            return columnBorder;
        }

        private DataGrid CreateDataGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = BrushFromRgb(241, 245, 249),
                Background = Brushes.White,
                AlternatingRowBackground = BrushFromRgb(249, 250, 251),
                BorderThickness = new Thickness(0),
                RowHeight = 44,
                Margin = new Thickness(20, 0, 20, 16)
            };

            grid.MouseDoubleClick += (s, e) =>
            {
                if (grid.SelectedItem is CallTicketListItem ticket && _navigator != null)
                {
                    _navigator.OpenTicket(ticket.TicketId);
                }
            };

            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(100, 116, 139)));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 12, 8, 12)));
            headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFromRgb(226, 232, 240)));
            grid.ColumnHeaderStyle = headerStyle;

            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
            rowStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            rowStyle.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, BrushFromRgb(248, 250, 252)));
            rowStyle.Triggers.Add(hoverTrigger);
            
            var selectedTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, BrushFromRgb(239, 246, 255)));
            rowStyle.Triggers.Add(selectedTrigger);
            grid.RowStyle = rowStyle;

            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 12, 8, 12)));
            cellStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            
            var cellSelectedTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
            cellSelectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(15, 23, 42)));
            cellSelectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            cellSelectedTrigger.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Triggers.Add(cellSelectedTrigger);
            grid.CellStyle = cellStyle;

            grid.Columns.Add(CreateTemplateColumn("Ticket", "<DataTemplate><TextBlock Text=\"{Binding TicketCode}\" FontWeight=\"Bold\" Foreground=\"#0284c7\" VerticalAlignment=\"Center\" /></DataTemplate>", 90, DataGridLengthUnitType.Pixel));
            grid.Columns.Add(CreateTemplateColumn("Issue", "<DataTemplate><TextBlock Text=\"{Binding Issue}\" TextTrimming=\"CharacterEllipsis\" Foreground=\"#334155\" VerticalAlignment=\"Center\" /></DataTemplate>", 2, DataGridLengthUnitType.Star));
            grid.Columns.Add(CreateTemplateColumn("Status", "<DataTemplate><Border CornerRadius=\"999\" Padding=\"10,4,10,4\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Center\"><Border.Style><Style TargetType=\"Border\"><Setter Property=\"Background\" Value=\"#f1f5f9\" /><Style.Triggers><DataTrigger Binding=\"{Binding Status}\" Value=\"Solved\"><Setter Property=\"Background\" Value=\"#dcfce7\" /></DataTrigger><DataTrigger Binding=\"{Binding Status}\" Value=\"Resolved (Temporary)\"><Setter Property=\"Background\" Value=\"#dcfce7\" /></DataTrigger><DataTrigger Binding=\"{Binding Status}\" Value=\"Pending\"><Setter Property=\"Background\" Value=\"#fef9c3\" /></DataTrigger><DataTrigger Binding=\"{Binding Status}\" Value=\"In Progress\"><Setter Property=\"Background\" Value=\"#dbeafe\" /></DataTrigger><DataTrigger Binding=\"{Binding Status}\" Value=\"Escalated\"><Setter Property=\"Background\" Value=\"#fee2e2\" /></DataTrigger><DataTrigger Binding=\"{Binding Status}\" Value=\"Overdue\"><Setter Property=\"Background\" Value=\"#ffe4e6\" /></DataTrigger></Style.Triggers></Style></Border.Style><TextBlock Text=\"{Binding Status}\" FontSize=\"11\" FontWeight=\"SemiBold\"><TextBlock.Style><Style TargetType=\"TextBlock\"><Setter Property=\"Foreground\" Value=\"#475569\" /><Style.Triggers><DataTrigger Binding=\"{Binding Status}\" Value=\"Solved\"><Setter Property=\"Foreground\" Value=\"#166534\" /></DataTrigger><DataTrigger Binding=\"{Binding Status}\" Value=\"Resolved (Temporary)\"><Setter Property=\"Foreground\" Value=\"#166534\" /></DataTrigger><DataTrigger Binding=\"{Binding Status}\" Value=\"Pending\"><Setter Property=\"Foreground\" Value=\"#854d0e\" /></DataTrigger><DataTrigger Binding=\"{Binding Status}\" Value=\"In Progress\"><Setter Property=\"Foreground\" Value=\"#1e40af\" /></DataTrigger><DataTrigger Binding=\"{Binding Status}\" Value=\"Escalated\"><Setter Property=\"Foreground\" Value=\"#991b1b\" /></DataTrigger><DataTrigger Binding=\"{Binding Status}\" Value=\"Overdue\"><Setter Property=\"Foreground\" Value=\"#9f1239\" /></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></Border></DataTemplate>", 115, DataGridLengthUnitType.Pixel));
            grid.Columns.Add(CreateTemplateColumn("Priority", "<DataTemplate><TextBlock Text=\"{Binding Priority}\" FontWeight=\"SemiBold\" Foreground=\"#475569\" VerticalAlignment=\"Center\" /></DataTemplate>", 85, DataGridLengthUnitType.Pixel));
            grid.Columns.Add(CreateTemplateColumn("Branch", "<DataTemplate><TextBlock Text=\"{Binding Branch}\" TextTrimming=\"CharacterEllipsis\" Foreground=\"#475569\" VerticalAlignment=\"Center\" /></DataTemplate>", 1.5, DataGridLengthUnitType.Star));
            grid.Columns.Add(CreateTemplateColumn("Department", "<DataTemplate><TextBlock Text=\"{Binding Department}\" TextTrimming=\"CharacterEllipsis\" Foreground=\"#475569\" VerticalAlignment=\"Center\" /></DataTemplate>", 1.5, DataGridLengthUnitType.Star));
            grid.Columns.Add(CreateTemplateColumn("Assigned To", "<DataTemplate><StackPanel Orientation=\"Horizontal\" VerticalAlignment=\"Center\"><Border Width=\"8\" Height=\"8\" CornerRadius=\"4\" Background=\"#94a3b8\" Margin=\"0,0,8,0\" /><TextBlock Text=\"{Binding ResponsiblePerson}\" TextTrimming=\"CharacterEllipsis\" Foreground=\"#334155\" VerticalAlignment=\"Center\" /></StackPanel></DataTemplate>", 1.5, DataGridLengthUnitType.Star));
            grid.Columns.Add(CreateTemplateColumn("Age (Days)", "<DataTemplate><TextBlock Text=\"{Binding TicketAgeDays}\" Foreground=\"#475569\" VerticalAlignment=\"Center\" /></DataTemplate>", 85, DataGridLengthUnitType.Pixel));
            grid.Columns.Add(CreateTemplateColumn("Idle (Days)", "<DataTemplate><TextBlock Text=\"{Binding IdleDays}\" Foreground=\"#475569\" VerticalAlignment=\"Center\" /></DataTemplate>", 85, DataGridLengthUnitType.Pixel));
            
            return grid;
        }

        private static DataGridTemplateColumn CreateTemplateColumn(string header, string xamlTemplate, double width = double.NaN, DataGridLengthUnitType unit = DataGridLengthUnitType.Auto)
        {
            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            ctx.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");

            var column = new DataGridTemplateColumn
            {
                Header = header,
                CellTemplate = (DataTemplate)XamlReader.Parse(xamlTemplate, ctx)
            };

            if (!double.IsNaN(width)) column.Width = new DataGridLength(width, unit);
            return column;
        }

        private Button CreateActionButton(string text, Brush bg)
        {
            var btn = new Button
            {
                Content = text,
                Padding = new Thickness(14, 8, 14, 8),
                Background = bg,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(8, 0, 0, 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            var btnTemplateXaml = @"
                <ControlTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" TargetType=""Button"">
                    <Border CornerRadius=""6"" Background=""{TemplateBinding Background}"" Padding=""{TemplateBinding Padding}"">
                        <Border.Effect><DropShadowEffect BlurRadius=""4"" ShadowDepth=""1"" Color=""#1e293b"" Opacity=""0.15"" /></Border.Effect>
                        <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True""><Setter Property=""Opacity"" Value=""0.9"" /></Trigger>
                        <Trigger Property=""IsPressed"" Value=""True""><Setter Property=""Opacity"" Value=""0.8"" /></Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>";
            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            btn.Template = (ControlTemplate)XamlReader.Parse(btnTemplateXaml, ctx);
            return btn;
        }

        private Border CreateInfoChip(string text, Brush bg, Brush fg)
        {
            var border = new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 10, 0)
            };
            border.Child = new TextBlock { Text = text, Foreground = fg, FontWeight = FontWeights.SemiBold, FontSize = 12 };
            return border;
        }

        private Border CreatePill(string text, Brush bg, Brush fg)
        {
            var border = new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 8, 8)
            };
            border.Child = new TextBlock { Text = text, Foreground = fg, FontWeight = FontWeights.SemiBold, FontSize = 11 };
            return border;
        }

        private Border CreateBadge(string text, Brush bg, Brush fg)
        {
            var border = new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2)
            };
            border.Child = new TextBlock { Text = text, Foreground = fg, FontWeight = FontWeights.Bold, FontSize = 11 };
            return border;
        }

        private void ExportExcelBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTickets == null || _currentTickets.Count == 0)
            {
                WpfItcmDialogService.ShowInfo(this, "No ticket data to export. Please refresh the data first.", "Export");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"ITCM_Tickets_{DateTime.Now:yyyyMMdd_HHmmss}",
                Title = "Export Tickets to CSV"
            };

            if (saveDialog.ShowDialog(Window.GetWindow(this)) != true) return;

            try
            {
                var csv = new StringBuilder();
                csv.AppendLine("Ticket Code,Issue,Status,Priority,Branch,Department,Assigned To,Caller,Age Days,Idle Days");

                foreach (var ticket in _currentTickets)
                {
                    var status = IsOverdue(ticket) ? "Overdue" : NormalizeStatus(ticket.Status);
                    var line = $"\"{EscapeCsv(ticket.TicketCode ?? "#" + ticket.TicketId)}\",\"{EscapeCsv(ticket.Issue)}\",\"{EscapeCsv(status)}\",\"{EscapeCsv(ticket.Priority)}\",\"{EscapeCsv(ticket.Branch ?? "-")}\",\"{EscapeCsv(ticket.Department ?? "-")}\",\"{EscapeCsv(ticket.ResponsiblePerson ?? "Unassigned")}\",\"{EscapeCsv(ticket.CallerName ?? "-")}\",{ticket.TicketAgeDays},{ticket.IdleDays}";
                    csv.AppendLine(line);
                }

                System.IO.File.WriteAllText(saveDialog.FileName, csv.ToString(), Encoding.UTF8);
                WpfItcmDialogService.ShowInfo(this, "Export completed successfully.", "Export");
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, "Export failed: " + ex.Message, "Export");
            }
        }

        private void ExportPdfBtn_Click(object sender, RoutedEventArgs e)
        {
            WpfItcmDialogService.ShowInfo(this, "PDF Export is currently handled by the legacy provider. CSV Export is recommended.", "Not Supported");
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
        }

        private bool IsOverdue(CallTicketListItem ticket)
        {
            if (ticket == null) return false;
            var lastActivity = GetLastActivityUtc(ticket);
            var idle = DateTime.UtcNow - lastActivity;
            if (idle < TimeSpan.Zero) idle = TimeSpan.Zero;
            return idle >= TimeSpan.FromDays(Math.Max(1, _overdueDays));
        }

        private static DateTime GetLastActivityUtc(CallTicketListItem ticket)
        {
            if (ticket == null) return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
            if (ticket.LastContactAt.HasValue) return ticket.LastContactAt.Value.Kind == DateTimeKind.Utc ? ticket.LastContactAt.Value : DateTime.SpecifyKind(ticket.LastContactAt.Value, DateTimeKind.Local).ToUniversalTime();
            var updated = ticket.UpdatedAt;
            if (updated.Kind == DateTimeKind.Utc) return updated;
            if (updated.Kind == DateTimeKind.Unspecified) updated = DateTime.SpecifyKind(updated, DateTimeKind.Local);
            return updated.ToUniversalTime();
        }

        private static string NormalizeStatus(string status)
        {
            var value = (status ?? string.Empty).Trim();
            if (value.Equals("Waiting on Vendor", StringComparison.OrdinalIgnoreCase) || value.Equals("Waiting on Department", StringComparison.OrdinalIgnoreCase)) return "In Progress";
            return string.IsNullOrWhiteSpace(value) ? "Pending" : value;
        }

        private static string ShortDuration(TimeSpan value)
        {
            if (value < TimeSpan.Zero) value = value.Duration();
            var d = value.Days; var h = value.Hours; var m = value.Minutes;
            if (d > 0) return h > 0 ? $"{d}d {h}h" : $"{d}d";
            if (h > 0) return m > 0 ? $"{h}h {m}m" : $"{h}h";
            return $"{Math.Max(1, m)}m";
        }

        private static int GetPriorityRank(CallTicketListItem ticket)
        {
            var p = (ticket != null ? ticket.Priority : null) ?? string.Empty;
            if (p.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return 4;
            if (p.Equals("High", StringComparison.OrdinalIgnoreCase)) return 3;
            if (p.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return 2;
            if (p.Equals("Low", StringComparison.OrdinalIgnoreCase)) return 1;
            return 0;
        }

        private static string FitChipLabelText(string prefix, string value, int maxLength)
        {
            var text = Safe(value, string.Empty);
            if (text.Length > maxLength) text = text.Substring(0, Math.Max(0, maxLength - 3)).TrimEnd() + "...";
            return string.IsNullOrWhiteSpace(text) ? prefix : prefix + " " + text;
        }

        private static string Safe(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private static Brush Accent(CallTicketListItem ticket)
        {
            if (ticket == null) return BrushFromRgb(52, 73, 94);
            var p = (ticket.Priority ?? string.Empty).Trim();
            if (p.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(192, 57, 43);
            if (p.Equals("High", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(230, 126, 34);
            if (p.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(52, 152, 219);
            if (p.Equals("Low", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(46, 204, 113);
            return BrushFromRgb(108, 117, 125);
        }

        private static string EmptyText(string title)
        {
            if (title.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return "Nothing is waiting in Pending.";
            if (title.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) return "No active work is marked In Progress.";
            if (title.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return "No escalated tickets right now.";
            if (title.Equals("Overdue", StringComparison.OrdinalIgnoreCase)) return "No overdue tickets at the moment.";
            return "No tickets in this column.";
        }

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));
        private static UIElement SetGridColumn(UIElement e, int c) { Grid.SetColumn(e, c); return e; }
    }
}
