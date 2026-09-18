using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Yakult.Inventory.App.Models.BorrowItems;
using Yakult.Inventory.App.Wpf.CallMonitoring;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Wpf.BorrowItems
{
    public sealed class WpfBorrowWorkspace : UserControl
    {
        // Events wired back to WinForms Host
        public event EventHandler ReturnSelectedClicked;
        public event EventHandler DeleteBorrowClicked;
        public event EventHandler ExportCsvClicked;
        public event EventHandler PrevPageClicked;
        public event EventHandler NextPageClicked;
        public event EventHandler RefreshClicked;
        public event EventHandler BackToPortalClicked;
        public event EventHandler<BorrowLogRow> BorrowRowDoubleClicked;
        public event EventHandler<BorrowLogRow> BorrowRowSelected;

        private readonly TextBlock _kpiOpenCount;
        private readonly TextBlock _kpiTodayCount;
        private readonly TextBlock _kpiOverdueCount;
        private readonly TextBlock _kpiReturnedTodayCount;
        
        private readonly TextBlock _heroSummaryText;
        private readonly TextBlock _heroRefreshedText;

        private readonly ItemsControl _attentionQueue;

        private readonly Button _openPrevButton;
        private readonly Button _openNextButton;
        private readonly TextBlock _openPageText;
        private readonly TextBlock _openEmptyText;

        private readonly Button _historyPrevButton;
        private readonly Button _historyNextButton;
        private readonly TextBlock _historyPageText;
        private readonly TextBlock _historyEmptyText;

        private readonly TabControl _tabControl;
        private readonly DataGrid _openGrid;
        private readonly DataGrid _historyGrid;
        private readonly Button _openTabButton;
        private readonly Button _historyTabButton;

        public WpfBorrowWorkspace()
        {
            Background = BrushFromRgb(241, 244, 247);
            Resources.MergedDictionaries.Add(WpfThemeResources.GetScrollBarStyle());

            var root = new Grid { Margin = new Thickness(28, 24, 28, 28) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Hero
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // KPIs
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Main Tabs & Queue

            // 1. HERO SECTION
            var heroStack = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            heroStack.Children.Add(BuildHero(out _heroSummaryText, out _heroRefreshedText));
            Grid.SetRow(heroStack, 0);
            root.Children.Add(heroStack);

            // 2. KPI ROW
            var kpiGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            for (int i = 0; i < 4; i++) kpiGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            var kpi1 = BuildKpiCard("Open Borrows", out _kpiOpenCount, BrushFromRgb(14, 116, 144)); // Teal
            var kpi2 = BuildKpiCard("Borrowed Today", out _kpiTodayCount, BrushFromRgb(59, 130, 246)); // Blue
            var kpi3 = BuildKpiCard("Overdue (8+ days)", out _kpiOverdueCount, BrushFromRgb(239, 68, 68)); // Red
            var kpi4 = BuildKpiCard("Returned Today", out _kpiReturnedTodayCount, BrushFromRgb(34, 197, 94)); // Green
            
            Grid.SetColumn(kpi1, 0); Grid.SetColumn(kpi2, 1); Grid.SetColumn(kpi3, 2); Grid.SetColumn(kpi4, 3);
            kpiGrid.Children.Add(kpi1); kpiGrid.Children.Add(kpi2); kpiGrid.Children.Add(kpi3); kpiGrid.Children.Add(kpi4);
            
            Grid.SetRow(kpiGrid, 1);
            root.Children.Add(kpiGrid);

            // 3. MAIN BODY (Queue + Tabs)
            var bodyGrid = new Grid();
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.2, GridUnitType.Star) });
            Grid.SetRow(bodyGrid, 2);
            root.Children.Add(bodyGrid);

            // Left: Attention Queue
            var queueCard = BuildGlassCard();
            queueCard.Margin = new Thickness(0, 0, 16, 0);
            queueCard.Child = BuildAttentionQueue(out _attentionQueue);
            Grid.SetColumn(queueCard, 0);
            bodyGrid.Children.Add(queueCard);

            // Right: Tabs
            var tabsCard = BuildGlassCard();
            tabsCard.Child = BuildTabs(
                out _tabControl,
                out _openGrid,
                out _historyGrid,
                out _openTabButton,
                out _historyTabButton,
                out _openPrevButton,
                out _openNextButton,
                out _openPageText,
                out _openEmptyText,
                out _historyPrevButton,
                out _historyNextButton,
                out _historyPageText,
                out _historyEmptyText);
            Grid.SetColumn(tabsCard, 1);
            bodyGrid.Children.Add(tabsCard);

            Content = root;
        }

        public int SelectedTabIndex
        {
            get => _tabControl.SelectedIndex;
            set => _tabControl.SelectedIndex = value;
        }

        public BorrowLogRow SelectedOpenRow => _openGrid.SelectedItem as BorrowLogRow;
        public BorrowLogRow SelectedHistoryRow => _historyGrid.SelectedItem as BorrowLogRow;

        public void BindData(IEnumerable<BorrowLogRow> open, IEnumerable<BorrowLogRow> history)
        {
            var openList = open?.ToList() ?? new List<BorrowLogRow>();
            var historyList = history?.ToList() ?? new List<BorrowLogRow>();

            _openGrid.ItemsSource = openList;
            _historyGrid.ItemsSource = historyList;
            if (_openEmptyText != null) _openEmptyText.Visibility = openList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_historyEmptyText != null) _historyEmptyText.Visibility = historyList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // Sort open borrows by age (oldest first) for the queue
            var queueItems = openList.OrderBy(x => x.BorrowedAtUtc).ToList();
            _attentionQueue.ItemsSource = queueItems;
        }

        public void SetPaging(int openPageIndex, int openTotalCount, int historyPageIndex, int historyTotalCount, int pageSize)
        {
            var openTotalPages = pageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(openTotalCount / (double)pageSize));
            var historyTotalPages = pageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(historyTotalCount / (double)pageSize));

            var openPage = Math.Max(1, openPageIndex + 1);
            var historyPage = Math.Max(1, historyPageIndex + 1);

            if (_openPrevButton != null) _openPrevButton.IsEnabled = openPage > 1;
            if (_openNextButton != null) _openNextButton.IsEnabled = openPage < openTotalPages;
            if (_openPageText != null) _openPageText.Text = $"Page {openPage} of {openTotalPages}";

            if (_historyPrevButton != null) _historyPrevButton.IsEnabled = historyPage > 1;
            if (_historyNextButton != null) _historyNextButton.IsEnabled = historyPage < historyTotalPages;
            if (_historyPageText != null) _historyPageText.Text = $"Page {historyPage} of {historyTotalPages}";
        }

        public void SetKpis(int openCount, int todayBorrow, int overdue, int returnedToday, string oldestElapsed, string lastRefresh)
        {
            _kpiOpenCount.Text = openCount.ToString();
            _kpiTodayCount.Text = todayBorrow.ToString();
            _kpiOverdueCount.Text = overdue.ToString();
            _kpiReturnedTodayCount.Text = returnedToday.ToString();

            _heroSummaryText.Text = $"{openCount} items currently borrowed • Oldest: {oldestElapsed}";
            _heroRefreshedText.Text = $"Last refreshed: {lastRefresh}";
        }

        private UIElement BuildHero(out TextBlock summaryText, out TextBlock refreshedText)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(24, 20, 24, 20),
                Background = new LinearGradientBrush(
                    Color.FromRgb(14, 116, 144), // Teal-700
                    Color.FromRgb(30, 64, 175),  // Blue-800
                    45.0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left side
            var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var title = new TextBlock
            {
                Text = "Borrow Items Dashboard",
                FontSize = 26,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 4)
            };
            summaryText = new TextBlock
            {
                Text = "Loading...",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                Margin = new Thickness(0, 0, 0, 2)
            };
            refreshedText = new TextBlock
            {
                Text = "Last refreshed: --",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
            };
            leftStack.Children.Add(title);
            leftStack.Children.Add(summaryText);
            leftStack.Children.Add(refreshedText);
            Grid.SetColumn(leftStack, 0);
            grid.Children.Add(leftStack);

            // Right side (Actions)
            var rightWrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Orientation = Orientation.Horizontal };
            
            var btnReturn = CreateHeroAction("Return Selected", BrushFromRgb(255, 255, 255), true);
            btnReturn.Click += (s, e) => ReturnSelectedClicked?.Invoke(this, EventArgs.Empty);
            
            var btnDelete = CreateHeroAction("Delete Borrow", BrushFromRgb(255, 255, 255), false);
            btnDelete.Click += (s, e) => DeleteBorrowClicked?.Invoke(this, EventArgs.Empty);

            var btnCsv = CreateHeroAction("CSV Export", BrushFromRgb(255, 255, 255), false);
            btnCsv.Click += (s, e) => ExportCsvClicked?.Invoke(this, EventArgs.Empty);

            var btnRefresh = CreateHeroAction("Refresh", BrushFromRgb(255, 255, 255), false);
            btnRefresh.Click += (s, e) => RefreshClicked?.Invoke(this, EventArgs.Empty);

            rightWrap.Children.Add(btnReturn);
            rightWrap.Children.Add(btnDelete);
            rightWrap.Children.Add(btnCsv);
            rightWrap.Children.Add(btnRefresh);

            Grid.SetColumn(rightWrap, 1);
            grid.Children.Add(rightWrap);

            border.Child = grid;
            return border;
        }

        private Border BuildKpiCard(string title, out TextBlock valueBlock, SolidColorBrush accentColor)
        {
            var card = BuildGlassCard();
            card.Margin = new Thickness(0, 0, 16, 0);
            
            card.Padding = new Thickness(0);
            var grid = new Grid { Margin = new Thickness(20, 16, 20, 16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(100, 116, 139),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(titleBlock, 0);
            
            valueBlock = new TextBlock
            {
                Text = "0",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = accentColor
            };
            Grid.SetRow(valueBlock, 1);

            grid.Children.Add(titleBlock);
            grid.Children.Add(valueBlock);
            card.Child = grid;
            return card;
        }

        private UIElement BuildAttentionQueue(out ItemsControl queue)
        {
            var grid = new Grid { Margin = new Thickness(0) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new Border
            {
                Background = BrushFromRgb(248, 250, 252),
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 12, 16, 12),
                CornerRadius = new CornerRadius(8, 8, 0, 0)
            };
            header.Child = new TextBlock
            {
                Text = "Active Borrows (Oldest First)",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(51, 65, 85)
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            queue = new ItemsControl
            {
                Margin = new Thickness(8),
                ItemTemplate = CreateQueueItemTemplate()
            };
            
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = queue
            };
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);

            return grid;
        }

        private DataTemplate CreateQueueItemTemplate()
        {
            var template = new DataTemplate(typeof(BorrowLogRow));
            
            var cardFactory = new FrameworkElementFactory(typeof(Border));
            cardFactory.SetValue(Border.BackgroundProperty, Brushes.White);
            cardFactory.SetValue(Border.BorderBrushProperty, BrushFromRgb(226, 232, 240));
            cardFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            cardFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            cardFactory.SetValue(Border.MarginProperty, new Thickness(8, 4, 8, 8));
            cardFactory.SetValue(Border.PaddingProperty, new Thickness(12));
            cardFactory.SetValue(Border.CursorProperty, Cursors.Hand);

            // Double Click logic handled via EventSetter inside Style, but since FrameworkElementFactory
            // is limited with events, we will wire it up in the code behind when items are clicked.
            cardFactory.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(QueueItem_MouseDown));

            var stack = new FrameworkElementFactory(typeof(StackPanel));
            stack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);

            var topDock = new FrameworkElementFactory(typeof(DockPanel));
            topDock.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 2));

            var elapsedText = new FrameworkElementFactory(typeof(TextBlock));
            elapsedText.SetBinding(TextBlock.TextProperty, new Binding("ElapsedText"));
            elapsedText.SetValue(TextBlock.FontSizeProperty, 12.0);
            elapsedText.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            elapsedText.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(239, 68, 68)); // Red for emphasis
            elapsedText.SetValue(DockPanel.DockProperty, Dock.Right);
            topDock.AppendChild(elapsedText);

            var serialText = new FrameworkElementFactory(typeof(TextBlock));
            serialText.SetBinding(TextBlock.TextProperty, new Binding("SerialNumber"));
            serialText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            serialText.SetValue(TextBlock.FontSizeProperty, 14.0);
            topDock.AppendChild(serialText);

            stack.AppendChild(topDock);

            var itemText = new FrameworkElementFactory(typeof(TextBlock));
            itemText.SetBinding(TextBlock.TextProperty, new Binding("ItemDisplay"));
            itemText.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            itemText.SetValue(TextBlock.FontSizeProperty, 13.0);
            itemText.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(71, 85, 105));
            itemText.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 4));
            stack.AppendChild(itemText);

            var borrowerText = new FrameworkElementFactory(typeof(TextBlock));
            borrowerText.SetBinding(TextBlock.TextProperty, new Binding("BorrowedByEmpName"));
            borrowerText.SetValue(TextBlock.FontSizeProperty, 12.0);
            borrowerText.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(100, 116, 139));
            stack.AppendChild(borrowerText);

            cardFactory.AppendChild(stack);
            template.VisualTree = cardFactory;
            return template;
        }

        private void QueueItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is BorrowLogRow row)
            {
                BorrowRowSelected?.Invoke(this, row);
                if (e.ClickCount == 2)
                {
                    BorrowRowDoubleClicked?.Invoke(this, row);
                }
            }
        }

        private UIElement BuildTabs(
            out TabControl tabControl,
            out DataGrid openGrid,
            out DataGrid historyGrid,
            out Button openTabButton,
            out Button historyTabButton,
            out Button openPrev,
            out Button openNext,
            out TextBlock openPageText,
            out TextBlock openEmptyText,
            out Button historyPrev,
            out Button historyNext,
            out TextBlock historyPageText,
            out TextBlock historyEmptyText)
        {
            var shell = new Grid();
            shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var tabButtonsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 4, 8, 0)
            };

            openTabButton = CreateTabPillButton("Open Borrows");
            historyTabButton = CreateTabPillButton("Returned History");
            historyTabButton.Margin = new Thickness(10, 0, 0, 0);
            tabButtonsRow.Children.Add(openTabButton);
            tabButtonsRow.Children.Add(historyTabButton);
            Grid.SetRow(tabButtonsRow, 0);
            shell.Children.Add(tabButtonsRow);

            tabControl = new TabControl
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(8, 6, 8, 0)
            };
            tabControl.Style = CreateTabControlStyle();
            var tabControlLocal = tabControl;
            var openTabButtonLocal = openTabButton;
            var historyTabButtonLocal = historyTabButton;
            tabControlLocal.SelectionChanged += (s, e) =>
            {
                if (!ReferenceEquals(e.Source, tabControlLocal))
                    return;

                UpdateTabPillState(openTabButtonLocal, tabControlLocal.SelectedIndex == 0);
                UpdateTabPillState(historyTabButtonLocal, tabControlLocal.SelectedIndex == 1);
            };
            openTabButtonLocal.Click += (s, e) => tabControlLocal.SelectedIndex = 0;
            historyTabButtonLocal.Click += (s, e) => tabControlLocal.SelectedIndex = 1;

            // Open Borrows Tab
            var openTab = new TabItem
            {
                Header = "Open Borrows",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            };
            openGrid = CreateDataGrid();
            var openGridLocal = openGrid;
            openGrid.Columns.Add(CreateTemplateColumn("Serial", "<DataTemplate><TextBlock Text=\"{Binding SerialNumber}\" FontWeight=\"Bold\" Foreground=\"#0f172a\" VerticalAlignment=\"Center\" /></DataTemplate>", 1.05, DataGridLengthUnitType.Star));
            openGrid.Columns.Add(CreateTemplateColumn("Item", "<DataTemplate><StackPanel VerticalAlignment=\"Center\"><TextBlock Text=\"{Binding ItemName}\" FontWeight=\"SemiBold\" Foreground=\"#1e293b\" TextTrimming=\"CharacterEllipsis\" /><TextBlock Text=\"{Binding ModelNumber}\" FontSize=\"11\" Foreground=\"#64748b\" TextTrimming=\"CharacterEllipsis\" /></StackPanel></DataTemplate>", 1.9, DataGridLengthUnitType.Star));
            openGrid.Columns.Add(CreateTemplateColumn("Borrower", "<DataTemplate><StackPanel VerticalAlignment=\"Center\"><TextBlock Text=\"{Binding BorrowedByEmpName}\" FontWeight=\"SemiBold\" Foreground=\"#334155\" TextTrimming=\"CharacterEllipsis\" /><TextBlock Text=\"{Binding BorrowedByDeptName}\" FontSize=\"11\" Foreground=\"#94a3b8\" TextTrimming=\"CharacterEllipsis\" /></StackPanel></DataTemplate>", 1.45, DataGridLengthUnitType.Star));
            openGrid.Columns.Add(CreateTemplateColumn("Elapsed", "<DataTemplate><Border CornerRadius=\"999\" Padding=\"10,4,10,4\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Center\" Background=\"#eff6ff\"><Border.Style><Style TargetType=\"Border\"><Setter Property=\"Background\" Value=\"#eff6ff\" /><Style.Triggers><DataTrigger Binding=\"{Binding ElapsedText}\" Value=\"\"><Setter Property=\"Background\" Value=\"#f8fafc\" /></DataTrigger></Style.Triggers></Style></Border.Style><TextBlock Text=\"{Binding ElapsedText}\" FontSize=\"11\" FontWeight=\"SemiBold\" Foreground=\"#1d4ed8\" VerticalAlignment=\"Center\" /></Border></DataTemplate>", 110, DataGridLengthUnitType.Pixel));
            openGrid.Columns.Add(CreateTemplateColumn("Borrowed", "<DataTemplate><StackPanel VerticalAlignment=\"Center\"><TextBlock Text=\"{Binding BorrowedAtLocal}\" FontWeight=\"SemiBold\" Foreground=\"#334155\" /><TextBlock Text=\"Open borrow\" FontSize=\"11\" Foreground=\"#10b981\" /></StackPanel></DataTemplate>", 145, DataGridLengthUnitType.Pixel));
            openGridLocal.SelectionChanged += (s, e) => { if (openGridLocal.SelectedItem is BorrowLogRow row) BorrowRowSelected?.Invoke(this, row); };
            openGridLocal.MouseDoubleClick += (s, e) => { if (openGridLocal.SelectedItem is BorrowLogRow row) BorrowRowDoubleClicked?.Invoke(this, row); };
            openTab.Content = BuildGridWithPaging(openGrid, "No open borrows.", out openPrev, out openNext, out openPageText, out openEmptyText, isOpenTab: true);
            tabControl.Items.Add(openTab);

            // History Tab
            var historyTab = new TabItem
            {
                Header = "Returned History",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            };
            historyGrid = CreateDataGrid();
            var historyGridLocal = historyGrid;
            historyGrid.Columns.Add(CreateTemplateColumn("Serial", "<DataTemplate><TextBlock Text=\"{Binding SerialNumber}\" FontWeight=\"Bold\" Foreground=\"#0f172a\" VerticalAlignment=\"Center\" /></DataTemplate>", 1.05, DataGridLengthUnitType.Star));
            historyGrid.Columns.Add(CreateTemplateColumn("Item", "<DataTemplate><TextBlock Text=\"{Binding ItemDisplay}\" FontWeight=\"SemiBold\" Foreground=\"#1e293b\" TextTrimming=\"CharacterEllipsis\" VerticalAlignment=\"Center\" /></DataTemplate>", 1.95, DataGridLengthUnitType.Star));
            historyGrid.Columns.Add(CreateTemplateColumn("Borrower", "<DataTemplate><TextBlock Text=\"{Binding BorrowedByEmpName}\" FontWeight=\"SemiBold\" Foreground=\"#334155\" TextTrimming=\"CharacterEllipsis\" VerticalAlignment=\"Center\" /></DataTemplate>", 1.2, DataGridLengthUnitType.Star));
            historyGrid.Columns.Add(CreateTemplateColumn("Borrowed", "<DataTemplate><TextBlock Text=\"{Binding BorrowedAtLocal}\" Foreground=\"#475569\" VerticalAlignment=\"Center\" HorizontalAlignment=\"Left\" /></DataTemplate>", 150, DataGridLengthUnitType.Pixel));
            historyGrid.Columns.Add(CreateTemplateColumn("Returned", "<DataTemplate><TextBlock Text=\"{Binding ReturnedAtLocal}\" Foreground=\"#475569\" VerticalAlignment=\"Center\" HorizontalAlignment=\"Left\" /></DataTemplate>", 150, DataGridLengthUnitType.Pixel));
            historyGrid.Columns.Add(CreateTemplateColumn("Returned By", "<DataTemplate><StackPanel Orientation=\"Horizontal\" VerticalAlignment=\"Center\"><Border Width=\"8\" Height=\"8\" CornerRadius=\"4\" Background=\"#10b981\" Margin=\"0,0,8,0\" VerticalAlignment=\"Center\" /><TextBlock Text=\"{Binding ReturnedByEmpName}\" FontWeight=\"SemiBold\" Foreground=\"#334155\" TextTrimming=\"CharacterEllipsis\" VerticalAlignment=\"Center\" /></StackPanel></DataTemplate>", 1.45, DataGridLengthUnitType.Star));
            historyGridLocal.MouseDoubleClick += (s, e) => { if (historyGridLocal.SelectedItem is BorrowLogRow row) BorrowRowDoubleClicked?.Invoke(this, row); };
            historyTab.Content = BuildGridWithPaging(historyGrid, "No history rows.", out historyPrev, out historyNext, out historyPageText, out historyEmptyText, isOpenTab: false);
            tabControl.Items.Add(historyTab);

            Grid.SetRow(tabControl, 1);
            shell.Children.Add(tabControl);

            UpdateTabPillState(openTabButton, true);
            UpdateTabPillState(historyTabButton, false);

            return shell;
        }

        private UIElement BuildGridWithPaging(DataGrid grid, string emptyText, out Button prevButton, out Button nextButton, out TextBlock pageText, out TextBlock emptyStateText, bool isOpenTab)
        {
            var container = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var gridHost = new Grid
            {
                Background = Brushes.White,
                Margin = new Thickness(0, 6, 0, 0)
            };
            Grid.SetRow(gridHost, 0);
            container.Children.Add(gridHost);

            gridHost.Children.Add(grid);

            emptyStateText = new TextBlock
            {
                Text = emptyText,
                Visibility = Visibility.Collapsed,
                Foreground = BrushFromRgb(100, 116, 139),
                FontSize = 12.5,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(18),
                IsHitTestVisible = false
            };
            gridHost.Children.Add(emptyStateText);

            var pagerWrap = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            pagerWrap.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pagerWrap.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pagerWrap.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pagerWrap.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            pageText = new TextBlock
            {
                Text = "Page 1 of 1",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = BrushFromRgb(100, 116, 139),
                FontSize = 11.5
            };
            Grid.SetColumn(pageText, 0);
            pagerWrap.Children.Add(pageText);
             
            prevButton = CreatePagerButton("Prev");
            prevButton.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(prevButton, 1);
            pagerWrap.Children.Add(prevButton);

            nextButton = CreatePagerButton("Next");
            nextButton.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(nextButton, 2);
            pagerWrap.Children.Add(nextButton);

            var csvBtn = CreatePagerButton("CSV");
            csvBtn.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(csvBtn, 3);
            pagerWrap.Children.Add(csvBtn);

            prevButton.Click += (s, e) => PrevPageClicked?.Invoke(this, EventArgs.Empty);
            nextButton.Click += (s, e) => NextPageClicked?.Invoke(this, EventArgs.Empty);
            csvBtn.Click += (s, e) => ExportCsvClicked?.Invoke(this, EventArgs.Empty);

            Grid.SetRow(pagerWrap, 1);
            container.Children.Add(pagerWrap);

            return container;
        }

        private DataGrid CreateDataGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.None,
                Background = Brushes.White,
                BorderThickness = new Thickness(0),
                RowHeight = 46,
                Margin = new Thickness(0, 10, 0, 0),
                CanUserResizeRows = false,
                CanUserResizeColumns = false,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                SelectionUnit = DataGridSelectionUnit.FullRow
            };

            grid.ColumnHeaderStyle = CreateDataGridHeaderStyle();
            grid.RowStyle = CreateDataGridRowStyle();
            grid.CellStyle = CreateDataGridCellStyle();

            var baseCellText = CreateTextBlockCellStyle(BrushFromRgb(51, 65, 85));
            grid.Resources[typeof(TextBlock)] = baseCellText;

            return grid;
        }

        private Button CreatePagerButton(string text)
        {
            return new Button
            {
                Content = text,
                Padding = new Thickness(12, 7, 12, 7),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(210, 219, 230)),
                BorderThickness = new Thickness(1),
                Foreground = BrushFromRgb(51, 65, 85),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
        }

        private static Button CreateTabPillButton(string text)
        {
            var button = new Button
            {
                Content = text,
                Padding = new Thickness(18, 10, 18, 10),
                MinWidth = 116,
                Background = BrushFromRgb(241, 245, 249),
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                Foreground = BrushFromRgb(71, 85, 105),
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            button.Template = CreateTabPillButtonTemplate();
            return button;
        }

        private static void UpdateTabPillState(Button button, bool isSelected)
        {
            if (button == null)
                return;

            button.Background = isSelected ? BrushFromRgb(14, 165, 233) : BrushFromRgb(241, 245, 249);
            button.BorderBrush = isSelected ? BrushFromRgb(14, 165, 233) : BrushFromRgb(226, 232, 240);
            button.Foreground = isSelected ? Brushes.White : BrushFromRgb(71, 85, 105);
        }

        private static ControlTemplate CreateTabPillButtonTemplate()
        {
            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            ctx.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");

            return (ControlTemplate)XamlReader.Parse(@"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='Button'>
  <Border x:Name='PillBorder'
          Background='{TemplateBinding Background}'
          BorderBrush='{TemplateBinding BorderBrush}'
          BorderThickness='{TemplateBinding BorderThickness}'
          CornerRadius='18'
          SnapsToDevicePixels='True'>
    <ContentPresenter Margin='{TemplateBinding Padding}'
                      HorizontalAlignment='Center'
                      VerticalAlignment='Center'
                      RecognizesAccessKey='True'/>
  </Border>
  <ControlTemplate.Triggers>
    <Trigger Property='IsMouseOver' Value='True'>
      <Setter TargetName='PillBorder' Property='Opacity' Value='0.95'/>
    </Trigger>
    <Trigger Property='IsPressed' Value='True'>
      <Setter TargetName='PillBorder' Property='Opacity' Value='0.88'/>
    </Trigger>
    <Trigger Property='IsEnabled' Value='False'>
      <Setter TargetName='PillBorder' Property='Opacity' Value='0.55'/>
    </Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>", ctx);
        }

        private Button CreateHeroAction(string text, SolidColorBrush fg, bool isPrimary)
        {
            var btn = new Button
            {
                Content = text,
                Foreground = fg,
                Background = isPrimary ? new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)) : Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 8, 0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            return btn;
        }

        private Border BuildGlassCard()
        {
            return WpfThemeResources.CreateGlassCard();
        }

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
        {
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        public void SelectOpenRowBySerial(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial) || _openGrid.ItemsSource == null) return;
            
            var items = _openGrid.ItemsSource.Cast<BorrowLogRow>().ToList();
            var row = items.FirstOrDefault(x => string.Equals(x.SerialNumber, serial, StringComparison.OrdinalIgnoreCase));
            if (row != null)
            {
                _openGrid.SelectedItem = row;
                _openGrid.ScrollIntoView(row);
                _tabControl.SelectedIndex = 0; // Switch to open tab
            }
        }

        private static Style CreateDataGridHeaderStyle()
        {
            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(100, 116, 139)));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 12, 14, 12)));
            headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFromRgb(226, 232, 240)));
            headerStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            headerStyle.Setters.Add(new Setter(Control.HeightProperty, 44.0));
            return headerStyle;
        }

        private static Style CreateDataGridRowStyle()
        {
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

            return rowStyle;
        }

        private static Style CreateDataGridCellStyle()
        {
            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 8, 12, 8)));
            cellStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            cellStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));

            var selectedTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(15, 23, 42)));
            selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            selectedTrigger.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Triggers.Add(selectedTrigger);

            return cellStyle;
        }

        private static Style CreateTextBlockCellStyle(SolidColorBrush foreground, FontWeight? weight = null)
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, foreground));
            style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            if (weight.HasValue)
                style.Setters.Add(new Setter(TextBlock.FontWeightProperty, weight.Value));
            return style;
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

            if (!double.IsNaN(width))
                column.Width = new DataGridLength(width, unit);

            return column;
        }

        private static Style CreateTabControlStyle()
        {
            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            ctx.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");

            return (Style)XamlReader.Parse(@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='TabControl'>
  <Setter Property='BorderThickness' Value='0'/>
  <Setter Property='Background' Value='Transparent'/>
  <Setter Property='Padding' Value='2'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='TabControl'>
        <Grid ClipToBounds='False' SnapsToDevicePixels='True'>
          <Grid.RowDefinitions>
            <RowDefinition Height='0'/>
            <RowDefinition Height='*'/>
          </Grid.RowDefinitions>
          <TabPanel Grid.Row='0'
                    Height='0'
                    IsItemsHost='True'
                    Visibility='Collapsed'/>
          <ContentPresenter Grid.Row='1'
                            ContentSource='SelectedContent'
                            Margin='0'
                            SnapsToDevicePixels='{TemplateBinding SnapsToDevicePixels}'/>
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>", ctx);
        }

    }
}
