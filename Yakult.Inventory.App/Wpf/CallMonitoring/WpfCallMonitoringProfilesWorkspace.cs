using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using WinForms = System.Windows.Forms;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Yakult.Inventory.App.Forms.CallMonitoring;
using Yakult.Inventory.App.Pages.Admin.AccountManagement;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed class WpfCallMonitoringProfilesWorkspace : UserControl
    {
        // Filter – row 0
        private readonly ComboBox _employeeCombo;
        private readonly DatePicker _fromDatePicker;
        private readonly DatePicker _toDatePicker;
        private readonly ComboBox _quickRangeCombo;
        private readonly Button _refreshButton;
        private readonly Button _escalationAssigneeButton;
        private readonly Button _viewProfileCardButton;
        private readonly Button _toggleFiltersButton;
        private readonly TextBlock _statusText;
        // Filter – row 1 (expandable)
        private readonly ComboBox _departmentCombo;
        private readonly ComboBox _locationModeCombo;
        private readonly ComboBox _priorityCombo;
        private readonly ComboBox _statusFilterCombo;
        private readonly TextBox _searchBox;
        private readonly Button _applyFiltersButton;
        private readonly Button _clearFiltersButton;
        // Metrics
        private readonly TextBlock _openValue;
        private readonly TextBlock _pendingValue;
        private readonly TextBlock _inProgressValue;
        private readonly TextBlock _escalatedValue;
        private readonly TextBlock _handledAssigneeValue;
        private readonly TextBlock _handledSolverValue;
        // Grids
        private readonly TabControl _tabControl;
        private readonly DataGrid _openGrid;
        private readonly DataGrid _handledGrid;
        // Profile panel
        private readonly TextBlock _profileTicketText;
        private readonly TextBlock _profileStatusText;
        private readonly TextBlock _profilePriorityText;
        private readonly TextBlock _profileLocationText;
        private readonly TextBlock _profileAssignedToText;
        private readonly TextBlock _profileCallerText;
        private readonly TextBlock _profileUpdatedText;
        private readonly TextBlock _profileCreatedText;
        private readonly TextBlock _profileLastEmailText;
        private readonly Button _profileOpenButton;
        private readonly Button _profileSummaryButton;
        // State
        private ICallMonitoringRepository _repository;
        private string _itDepartmentName;
        private ICallMonitoringNavigator _navigator;
        private CallEmailNotificationService _email;
        private bool _initialized;
        private int _profileSelectedTicketId;
        private int _profileSelectionVersion;
        private bool _suppressSelectionChanged;
        private CancellationTokenSource _profileUpdateCts;
        private CancellationTokenSource _applyFiltersCts;
        private int _applyFiltersVersion;
        private enum HandledRoleFilter { All, Assignee, Solver }
        private HandledRoleFilter _handledRoleFilter = HandledRoleFilter.All;
        private List<CallEmployeeProfileLookup> _employees = new List<CallEmployeeProfileLookup>();
        private List<LookupItem> _departments = new List<LookupItem>();
        private List<CallTechOpenTicketRow> _openTicketsAll = new List<CallTechOpenTicketRow>();
        private List<CallTechHandledTicketRow> _handledTicketsAll = new List<CallTechHandledTicketRow>();
        private List<CallTechOpenTicketRow> _openTicketsFiltered = new List<CallTechOpenTicketRow>();
        private List<CallTechHandledTicketRow> _handledTicketsFiltered = new List<CallTechHandledTicketRow>();
        private readonly Dictionary<int, (CallTicketListItem Ticket, DateTime FetchedUtc)> _ticketCache
            = new Dictionary<int, (CallTicketListItem, DateTime)>();
        private readonly Dictionary<int, (CallEmailLogItem Email, DateTime FetchedUtc)> _emailCache
            = new Dictionary<int, (CallEmailLogItem, DateTime)>();
        private static readonly TimeSpan ProfileCacheTtl = TimeSpan.FromSeconds(30);

        public WpfCallMonitoringProfilesWorkspace()
        {
            Background = BrushFromRgb(241, 244, 247);
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var root = new StackPanel { Margin = new Thickness(28, 24, 28, 28) };
            scroll.Content = root;
            Content = scroll;

            root.Children.Add(BuildHero());

            // ── Metric cards ─────────────────────────────────────────────────────────
            var metricGrid = new Grid { Margin = new Thickness(0, 22, 0, 0) };
            for (var i = 0; i < 6; i++) metricGrid.ColumnDefinitions.Add(new ColumnDefinition());
            _openValue = CreateMetricValue();
            _pendingValue = CreateMetricValue();
            _inProgressValue = CreateMetricValue();
            _escalatedValue = CreateMetricValue();
            _handledAssigneeValue = CreateMetricValue();
            _handledSolverValue = CreateMetricValue();
            var cOpen = BuildClickableMetricCard(0, "Open (Assigned)", "Active assigned tickets", _openValue, Color.FromRgb(37, 99, 235));
            var cPending = BuildClickableMetricCard(1, "Pending", "Awaiting action", _pendingValue, Color.FromRgb(245, 158, 11));
            var cInProg = BuildClickableMetricCard(2, "In Progress", "Being actively worked on", _inProgressValue, Color.FromRgb(124, 58, 237));
            var cEsc = BuildClickableMetricCard(3, "Escalated", "Escalated / overdue", _escalatedValue, Color.FromRgb(239, 68, 68));
            var cHandledA = BuildClickableMetricCard(4, "Handled (Assignee)", "Solved/closed as assignee", _handledAssigneeValue, Color.FromRgb(16, 185, 129));
            var cHandledS = BuildClickableMetricCard(5, "Handled (Solver)", "Solved/closed as solver", _handledSolverValue, Color.FromRgb(16, 185, 129));
            metricGrid.Children.Add(cOpen);
            metricGrid.Children.Add(cPending);
            metricGrid.Children.Add(cInProg);
            metricGrid.Children.Add(cEsc);
            metricGrid.Children.Add(cHandledA);
            metricGrid.Children.Add(cHandledS);
            cOpen.MouseLeftButtonDown += (_, __) => ApplyCardFilter(null, null);
            cPending.MouseLeftButtonDown += (_, __) => ApplyCardFilter("Pending", null);
            cInProg.MouseLeftButtonDown += (_, __) => ApplyCardFilter("In Progress", null);
            cEsc.MouseLeftButtonDown += (_, __) => ApplyCardFilter("Escalated", null);
            cHandledA.MouseLeftButtonDown += (_, __) => ApplyCardFilter(null, HandledRoleFilter.Assignee);
            cHandledS.MouseLeftButtonDown += (_, __) => ApplyCardFilter(null, HandledRoleFilter.Solver);
            root.Children.Add(metricGrid);

            // ── Filter card ───────────────────────────────────────────────────────────
            var filterCard = CreateGlassCard();
            filterCard.Margin = new Thickness(0, 22, 0, 0);
            root.Children.Add(filterCard);

            _employeeCombo = new ComboBox { Padding = new Thickness(8, 6, 8, 6), FontSize = 12, MinWidth = 260 };
            _fromDatePicker = CreateDatePicker();
            _toDatePicker = CreateDatePicker();
            _quickRangeCombo = CreateTextComboBox();
            foreach (var r in new[] { "Last 30 days", "Today", "Last 7 days", "This month", "This year" })
                _quickRangeCombo.Items.Add(r);
            _quickRangeCombo.SelectedIndex = 0;
            _departmentCombo = CreateTextComboBox();
            _locationModeCombo = CreateTextComboBox();
            foreach (var l in new[] { "All", "Department only", "Branch only" })
                _locationModeCombo.Items.Add(l);
            _locationModeCombo.SelectedIndex = 0;
            _priorityCombo = CreateTextComboBox();
            foreach (var p in new[] { "All Priorities", "Critical", "High", "Medium", "Low" })
                _priorityCombo.Items.Add(p);
            _priorityCombo.SelectedIndex = 0;
            _statusFilterCombo = CreateTextComboBox();
            foreach (var s in new[] { "All Statuses", "Pending", "In Progress", "Escalated", "Forwarded to Repair", "Reopened" })
                _statusFilterCombo.Items.Add(s);
            _statusFilterCombo.SelectedIndex = 0;
            _searchBox = new TextBox { Padding = new Thickness(10, 8, 10, 8), FontSize = 12, MinWidth = 220 };
            _refreshButton = CreatePrimaryButton("Refresh Data", BrushFromRgb(37, 99, 235));
            _escalationAssigneeButton = new Button
            {
                Content = "Manage Assignees…", Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(14, 9, 14, 9), FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(37, 99, 235), Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
                Visibility = (AppSession.IsAdmin || AppSession.IsDeveloper) ? Visibility.Visible : Visibility.Collapsed
            };
            _viewProfileCardButton = new Button
            {
                Content = "View Profile Card", Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(14, 9, 14, 9), FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42), Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand
            };
            _toggleFiltersButton = new Button
            {
                Content = "Filters \u25be", Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(14, 9, 14, 9), FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105), Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand
            };
            _statusText = new TextBlock
            {
                FontSize = 11.5, Foreground = BrushFromRgb(100, 116, 139),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), Text = "Ready"
            };
            _applyFiltersButton = CreatePrimaryButton("Apply Filters", BrushFromRgb(37, 99, 235));
            _clearFiltersButton = CreatePrimaryButton("Clear", BrushFromRgb(71, 85, 105));
            _refreshButton.Margin = new Thickness(0, 0, 8, 0);

            var filterOuterGrid = new Grid();
            filterOuterGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            filterOuterGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            filterCard.Child = filterOuterGrid;

            var topStrip = new Grid();
            topStrip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topStrip.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(topStrip, 0);
            filterOuterGrid.Children.Add(topStrip);
            var topLeft = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
            topLeft.Children.Add(MakeCompactField("Employee", _employeeCombo));
            topLeft.Children.Add(MakeCompactField("From", _fromDatePicker));
            topLeft.Children.Add(MakeCompactField("To", _toDatePicker));
            topLeft.Children.Add(MakeCompactField("Quick Range", _quickRangeCombo));
            Grid.SetColumn(topLeft, 0);
            topStrip.Children.Add(topLeft);
            var topRight = new WrapPanel
            {
                Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(14, 0, 0, 0)
            };
            topRight.Children.Add(_refreshButton);
            topRight.Children.Add(_escalationAssigneeButton);
            topRight.Children.Add(_viewProfileCardButton);
            topRight.Children.Add(_toggleFiltersButton);
            topRight.Children.Add(_statusText);
            Grid.SetColumn(topRight, 1);
            topStrip.Children.Add(topRight);

            var expandedStrip = new Border
            {
                Margin = new Thickness(0, 14, 0, 0), Padding = new Thickness(0, 12, 0, 0),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0, 1, 0, 0), Visibility = Visibility.Collapsed
            };
            Grid.SetRow(expandedStrip, 1);
            filterOuterGrid.Children.Add(expandedStrip);
            var expandedFlow = new WrapPanel { Orientation = Orientation.Horizontal };
            expandedStrip.Child = expandedFlow;
            expandedFlow.Children.Add(MakeCompactField("Dept", _departmentCombo));
            expandedFlow.Children.Add(MakeCompactField("Show", _locationModeCombo));
            expandedFlow.Children.Add(MakeCompactField("Priority", _priorityCombo));
            expandedFlow.Children.Add(MakeCompactField("Status", _statusFilterCombo));
            expandedFlow.Children.Add(MakeCompactField("Search", _searchBox));
            var expandedRight = new StackPanel
            {
                Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(18, 0, 0, 8)
            };
            expandedRight.Children.Add(_applyFiltersButton);
            expandedRight.Children.Add(_clearFiltersButton);
            expandedFlow.Children.Add(expandedRight);

            var filtersVisible = false;
            _toggleFiltersButton.Click += (_, __) =>
            {
                filtersVisible = !filtersVisible;
                expandedStrip.Visibility = filtersVisible ? Visibility.Visible : Visibility.Collapsed;
                _toggleFiltersButton.Content = filtersVisible ? "Filters \u25b4" : "Filters \u25be";
                _toggleFiltersButton.Foreground = filtersVisible ? BrushFromRgb(37, 99, 235) : BrushFromRgb(71, 85, 105);
                _toggleFiltersButton.BorderBrush = filtersVisible
                    ? new SolidColorBrush(Color.FromRgb(147, 197, 253))
                    : new SolidColorBrush(Color.FromRgb(203, 213, 225));
            };

            // ── Content card (grids + profile panel) ──────────────────────────────────
            var contentCard = CreateGlassCard();
            contentCard.Margin = new Thickness(0, 18, 0, 0);
            contentCard.Padding = new Thickness(0);
            root.Children.Add(contentCard);

            var contentGrid = new Grid { MinHeight = 620 };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
            contentCard.Child = contentGrid;

            _tabControl = new TabControl
            {
                Background = Brushes.Transparent, BorderThickness = new Thickness(0)
            };
            _tabControl.ItemContainerStyle = BuildTabItemStyle();
            Grid.SetColumn(_tabControl, 0);
            contentGrid.Children.Add(_tabControl);

            _openGrid = CreateReportGrid();
            SetupOpenColumns(_openGrid);
            var openTab = new TabItem { Header = "Assigned (Open)" };
            openTab.Content = new Border { Padding = new Thickness(10), Child = _openGrid };
            _tabControl.Items.Add(openTab);

            _handledGrid = CreateReportGrid();
            SetupHandledColumns(_handledGrid);
            var handledTab = new TabItem { Header = "Handled (Solved / Closed)" };
            handledTab.Content = new Border { Padding = new Thickness(10), Child = _handledGrid };
            _tabControl.Items.Add(handledTab);

            var splitter = new GridSplitter
            {
                Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Cursor = Cursors.SizeWE
            };
            Grid.SetColumn(splitter, 1);
            contentGrid.Children.Add(splitter);

            var profileCard = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 249, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1, 0, 0, 0), Padding = new Thickness(16, 14, 16, 14)
            };
            Grid.SetColumn(profileCard, 2);
            contentGrid.Children.Add(profileCard);
            var profileScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            profileCard.Child = profileScroll;
            var profileStack = new StackPanel();
            profileScroll.Content = profileStack;
            profileStack.Children.Add(new TextBlock
            {
                Text = "TICKET PROFILE", FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(100, 116, 139), Margin = new Thickness(0, 0, 0, 8)
            });
            _profileTicketText = new TextBlock
            {
                Text = "Select a ticket", FontSize = 16, FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(15, 23, 42), TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14)
            };
            profileStack.Children.Add(_profileTicketText);
            _profileStatusText = new TextBlock { Text = "-", FontSize = 13, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            _profilePriorityText = new TextBlock { Text = "-", FontSize = 13, FontWeight = FontWeights.SemiBold };
            _profileLocationText = new TextBlock { Text = "-", FontSize = 12, TextWrapping = TextWrapping.Wrap };
            _profileAssignedToText = new TextBlock { Text = "-", FontSize = 12, TextWrapping = TextWrapping.Wrap };
            _profileCallerText = new TextBlock { Text = "-", FontSize = 12, TextWrapping = TextWrapping.Wrap };
            _profileUpdatedText = new TextBlock { Text = "-", FontSize = 12 };
            _profileCreatedText = new TextBlock { Text = "-", FontSize = 12 };
            _profileLastEmailText = new TextBlock { Text = "-", FontSize = 12, TextWrapping = TextWrapping.Wrap };
            profileStack.Children.Add(BuildProfileField("STATUS", _profileStatusText));
            profileStack.Children.Add(BuildProfileField("PRIORITY", _profilePriorityText));
            profileStack.Children.Add(BuildProfileField("LOCATION", _profileLocationText));
            profileStack.Children.Add(BuildProfileField("ASSIGNED TO", _profileAssignedToText));
            profileStack.Children.Add(BuildProfileField("CALLER", _profileCallerText));
            profileStack.Children.Add(BuildProfileField("UPDATED", _profileUpdatedText));
            profileStack.Children.Add(BuildProfileField("CREATED", _profileCreatedText));
            profileStack.Children.Add(BuildProfileField("LAST EMAIL", _profileLastEmailText));
            var profileActions = new StackPanel
            {
                Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _profileOpenButton = CreatePrimaryButton("Profile", BrushFromRgb(37, 99, 235));
            _profileOpenButton.IsEnabled = false;
            _profileOpenButton.Margin = new Thickness(0, 0, 8, 0);
            _profileSummaryButton = new Button
            {
                Content = "Summary", Padding = new Thickness(14, 9, 14, 9), FontSize = 12,
                FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(37, 99, 235),
                Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                BorderThickness = new Thickness(1), IsEnabled = false, Cursor = Cursors.Hand
            };
            profileActions.Children.Add(_profileOpenButton);
            profileActions.Children.Add(_profileSummaryButton);
            profileStack.Children.Add(profileActions);

            HookEvents();
            ApplyQuickRange();
        }

        public void Initialize(ICallMonitoringRepository repository, string itDepartmentName, ICallMonitoringNavigator navigator = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _itDepartmentName = string.IsNullOrWhiteSpace(itDepartmentName) ? null : itDepartmentName.Trim();
            _navigator = navigator;
            _email = new CallEmailNotificationService(repository);
            if (!_initialized) { _initialized = true; InitializeAsync().FireAndForget(ex => System.Diagnostics.Debug.WriteLine("[Profiles] Initialize failed: " + ex)); }
        }

        public void RefreshData() { RefreshAsync().FireAndForget(ex => System.Diagnostics.Debug.WriteLine("[Profiles] Refresh failed: " + ex)); }

        private async Task InitializeAsync()
        {
            await LoadEmployeesAsync();
            await LoadDepartmentsAsync();
            await RefreshAsync();
        }

        private void HookEvents()
        {
            _quickRangeCombo.SelectionChanged += (_, __) => ApplyQuickRange();
            _refreshButton.Click += async (_, __) => await RefreshAsync();
            _viewProfileCardButton.Click += (_, __) => OpenProfileCard();
            _escalationAssigneeButton.Click += async (_, __) => await OpenAutoEscalationAssigneeDialogAsync();
            _applyFiltersButton.Click += (_, __) => ApplyFilters();
            _clearFiltersButton.Click += (_, __) => ClearFilters();
            _locationModeCombo.SelectionChanged += (_, __) => UpdateDeptFilterState();
            _searchBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) ApplyFilters(); };
            _profileOpenButton.Click += (_, __) =>
            {
                if (_profileSelectedTicketId <= 0) return;
                if (_navigator != null) _navigator.OpenTicket(_profileSelectedTicketId);
                else MessageBox.Show("Ticket navigation is not available from this view.", "Ticket Profile", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            _profileSummaryButton.Click += async (_, __) => await OpenSelectedTicketSummaryAsync();
            _openGrid.SelectionChanged += async (_, __) => { if (!_suppressSelectionChanged) await QueueProfileUpdateAsync(_openGrid); };
            _handledGrid.SelectionChanged += async (_, __) => { if (!_suppressSelectionChanged) await QueueProfileUpdateAsync(_handledGrid); };
            _tabControl.SelectionChanged += async (_, __) =>
            {
                if (_suppressSelectionChanged) return;
                await QueueProfileUpdateAsync(_tabControl.SelectedIndex == 0 ? _openGrid : _handledGrid);
            };
            _openGrid.MouseDoubleClick += HandleGridDoubleClick;
            _handledGrid.MouseDoubleClick += HandleGridDoubleClick;
            _openGrid.ContextMenu = BuildGridContextMenu(true);
            _handledGrid.ContextMenu = BuildGridContextMenu(false);
        }
        // ── Context menus ────────────────────────────────────────────────────────
        private void HandleGridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid == null) return;
            int ticketId = 0; string ticketCode = null;
            if (grid.SelectedItem is OpenTicketVm ov) { ticketId = ov.TicketId; ticketCode = ov.TicketCode; }
            else if (grid.SelectedItem is HandledTicketVm hv) { ticketId = hv.TicketId; ticketCode = hv.TicketCode; }
            if (_navigator != null && ticketId > 0) { _navigator.OpenTicket(ticketId); return; }
            if (!string.IsNullOrWhiteSpace(ticketCode))
                try { Clipboard.SetText(ticketCode); SetStatus($"Copied ticket: {ticketCode}"); } catch { }
        }

        private ContextMenu BuildGridContextMenu(bool isOpenGrid)
        {
            var menu = new ContextMenu();
            var miOpen = new MenuItem { Header = "Open Ticket" };
            miOpen.Click += (_, __) => OpenSelectedTicket(isOpenGrid);
            var miCopyCode = new MenuItem { Header = "Copy Ticket Code" };
            miCopyCode.Click += (_, __) => CopySelectedTicket(isOpenGrid, copyCode: true);
            var miCopyId = new MenuItem { Header = "Copy Ticket ID" };
            miCopyId.Click += (_, __) => CopySelectedTicket(isOpenGrid, copyCode: false);
            menu.Items.Add(miOpen); menu.Items.Add(miCopyCode); menu.Items.Add(miCopyId);
            if (isOpenGrid)
            {
                menu.Items.Add(new Separator());
                var miStatus = new MenuItem { Header = "Set Status" };
                foreach (var s in new[] { "Pending", "In Progress", "Escalated" })
                    miStatus.Items.Add(MakeStatusMenuItem(s, isOpenGrid));
                miStatus.Items.Add(new Separator());
                foreach (var s in new[] { "Solved", "Resolved (Temporary)", "Closed" })
                    miStatus.Items.Add(MakeStatusMenuItem(s, isOpenGrid));
                menu.Items.Add(miStatus);
            }
            else { menu.Items.Add(new Separator()); menu.Items.Add(MakeStatusMenuItem("Reopened", isOpenGrid)); }
            menu.Opened += (_, __) =>
            {
                var info = GetSelectedTicketInfo(isOpenGrid);
                miOpen.IsEnabled = info.TicketId > 0;
                miCopyCode.IsEnabled = !string.IsNullOrWhiteSpace(info.TicketCode);
                miCopyId.IsEnabled = info.TicketId > 0;
            };
            return menu;
        }

        private MenuItem MakeStatusMenuItem(string newStatus, bool isOpenGrid)
        {
            var mi = new MenuItem { Header = newStatus };
            mi.Click += async (_, __) => await SetSelectedTicketStatusAsync(isOpenGrid, newStatus);
            return mi;
        }

        private (int TicketId, string TicketCode, string Status) GetSelectedTicketInfo(bool isOpenGrid)
        {
            try
            {
                var item = (isOpenGrid ? _openGrid : _handledGrid).SelectedItem;
                if (item is OpenTicketVm o) return (o.TicketId, o.TicketCode, o.Status);
                if (item is HandledTicketVm h) return (h.TicketId, h.TicketCode, h.Status);
            }
            catch { }
            return (0, null, null);
        }

        private void OpenSelectedTicket(bool isOpenGrid)
        {
            var info = GetSelectedTicketInfo(isOpenGrid);
            if (info.TicketId <= 0) return;
            if (_navigator != null) { _navigator.OpenTicket(info.TicketId); return; }
            if (!string.IsNullOrWhiteSpace(info.TicketCode))
                try { Clipboard.SetText(info.TicketCode); SetStatus($"Copied ticket: {info.TicketCode}"); } catch { }
        }

        private void CopySelectedTicket(bool isOpenGrid, bool copyCode)
        {
            var info = GetSelectedTicketInfo(isOpenGrid);
            try
            {
                Clipboard.SetText(copyCode ? (info.TicketCode ?? string.Empty) : info.TicketId.ToString());
                SetStatus(copyCode ? $"Copied: {info.TicketCode}" : $"Copied ID: {info.TicketId}");
            }
            catch { }
        }

        private async Task SetSelectedTicketStatusAsync(bool isOpenGrid, string newStatus)
        {
            var info = GetSelectedTicketInfo(isOpenGrid);
            if (info.TicketId <= 0 || string.IsNullOrWhiteSpace(newStatus)) return;
            var changedByUserId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
            if (changedByUserId == null)
            {
                MessageBox.Show("You must be logged in to change ticket status.", "Call Monitoring Profiles", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var oldStatus = info.Status ?? string.Empty;
            if (string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase)) return;
            // Final states require resolution details: route through the
            // Mark As flow instead of setting them directly.
            if (newStatus.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                || newStatus.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                || newStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "To mark a ticket as Solved/Resolved (Temporary)/Closed, use the 'Mark As...' flow on the ticket.\n\n" +
                    "This ensures resolution details are captured properly.",
                    "Status Change",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
            var msg = $"Change status for {info.TicketCode ?? info.TicketId.ToString()}?\n\nFrom: {oldStatus}\nTo:   {newStatus}";
            if (MessageBox.Show(msg, "Confirm Status Change", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                await _repository.SetTicketStatusAsync(info.TicketId, newStatus, changedByUserId, note: null);
                _ = Task.Run(async () =>
                {
                    try { await _email.NotifyStatusChangeAsync(info.TicketId, oldStatus, newStatus, string.Empty, changedByUserId); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                });
                await RefreshAsync();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Status Change Failed", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // ── Data loading ──────────────────────────────────────────────────────────
        private async Task LoadEmployeesAsync()
        {
            if (_repository == null) return;
            try { _employees = await _repository.GetItEmployeeProfilesAsync(_itDepartmentName); }
            catch { _employees = new List<CallEmployeeProfileLookup>(); }
            var ordered = (_employees ?? new List<CallEmployeeProfileLookup>())
                .Where(x => x != null && x.EmpId > 0).OrderBy(x => x.EmployeeName).ToList();
            _employeeCombo.ItemsSource = null;
            _employeeCombo.Items.Clear();
            _employeeCombo.DisplayMemberPath = "EmployeeName";
            _employeeCombo.ItemsSource = ordered;
            if (_employeeCombo.Items.Count > 0) _employeeCombo.SelectedIndex = 0;
        }

        private async Task LoadDepartmentsAsync()
        {
            if (_repository == null) return;
            try { _departments = await _repository.GetDepartmentsAsync(); }
            catch { _departments = new List<LookupItem>(); }
            var items = new List<LookupItem> { new LookupItem { Id = 0, Name = "All Departments" } };
            items.AddRange((_departments ?? new List<LookupItem>()).Where(d => d != null).OrderBy(d => d.Name));
            _departmentCombo.ItemsSource = null;
            _departmentCombo.Items.Clear();
            _departmentCombo.DisplayMemberPath = "Name";
            _departmentCombo.SelectedValuePath = "Id";
            _departmentCombo.ItemsSource = items;
            if (_departmentCombo.Items.Count > 0) _departmentCombo.SelectedIndex = 0;
        }

        private async Task RefreshAsync()
        {
            if (_repository == null) return;
            try
            {
                _refreshButton.IsEnabled = false;
                SetStatus("Loading...");
                if (!await _repository.CallSchemaExistsAsync())
                {
                    SetStatus("Call Monitoring schema not installed.");
                    _openGrid.ItemsSource = null; _handledGrid.ItemsSource = null;
                    SetTicketProfileEmpty("Schema unavailable");
                    return;
                }
                var emp = _employeeCombo.SelectedItem as CallEmployeeProfileLookup;
                if (emp == null || emp.EmpId <= 0)
                {
                    SetStatus("Select an employee.");
                    _openGrid.ItemsSource = null; _handledGrid.ItemsSource = null;
                    SetTicketProfileEmpty("Select an employee");
                    return;
                }
                var from = _fromDatePicker.SelectedDate;
                var to = _toDatePicker.SelectedDate;
                if (!from.HasValue || !to.HasValue || from.Value.Date > to.Value.Date)
                {
                    MessageBox.Show("'From' date must be earlier than or equal to 'To' date.", "Call Monitoring Profiles", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SetStatus("Invalid date range"); return;
                }
                var fromUtc = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Local).ToUniversalTime();
                var toUtc = DateTime.SpecifyKind(to.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime();
                var summaryTask = _repository.GetTechProfileSummaryAsync(emp.EmpId, fromUtc, toUtc);
                var openTask = _repository.GetOpenTicketsAssignedToEmployeeAsync(emp.EmpId, maxRows: 300);
                var handledTask = _repository.GetTechHandledTicketsAsync(emp.EmpId, fromUtc, toUtc, maxRows: 300);
                await Task.WhenAll(summaryTask, openTask, handledTask);
                var summary = summaryTask.Result;
                _openTicketsAll = openTask.Result ?? new List<CallTechOpenTicketRow>();
                _handledTicketsAll = handledTask.Result ?? new List<CallTechHandledTicketRow>();
                _openValue.Text = summary?.AssignedOpenTotal.ToString() ?? "0";
                _pendingValue.Text = summary?.AssignedPending.ToString() ?? "0";
                _inProgressValue.Text = summary?.AssignedInProgress.ToString() ?? "0";
                _escalatedValue.Text = summary?.AssignedEscalated.ToString() ?? "0";
                _handledAssigneeValue.Text = summary?.HandledAsAssigneeSolvedClosed.ToString() ?? "0";
                _handledSolverValue.Text = summary?.HandledAsSolverSolvedClosed.ToString() ?? "0";
                ApplyFilters();
            }
            catch (Exception ex) { SetStatus("Error"); MessageBox.Show(ex.Message, "Call Monitoring Profiles", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { _refreshButton.IsEnabled = true; }
        }

        // ── Filter ────────────────────────────────────────────────────────────────
        private void ApplyQuickRange()
        {
            try
            {
                var now = DateTime.Today;
                var choice = _quickRangeCombo.SelectedItem?.ToString() ?? string.Empty;
                if (choice == "Today") { _fromDatePicker.SelectedDate = now; _toDatePicker.SelectedDate = now; }
                else if (choice == "Last 7 days") { _fromDatePicker.SelectedDate = now.AddDays(-6); _toDatePicker.SelectedDate = now; }
                else if (choice == "This month") { _fromDatePicker.SelectedDate = new DateTime(now.Year, now.Month, 1); _toDatePicker.SelectedDate = now; }
                else if (choice == "This year") { _fromDatePicker.SelectedDate = new DateTime(now.Year, 1, 1); _toDatePicker.SelectedDate = now; }
                else { _fromDatePicker.SelectedDate = now.AddDays(-30); _toDatePicker.SelectedDate = now; }
            }
            catch { }
        }

        private void ClearFilters()
        {
            try
            {
                if (_departmentCombo.Items.Count > 0) _departmentCombo.SelectedIndex = 0;
                if (_locationModeCombo.Items.Count > 0) _locationModeCombo.SelectedIndex = 0;
                if (_priorityCombo.Items.Count > 0) _priorityCombo.SelectedIndex = 0;
                if (_statusFilterCombo.Items.Count > 0) _statusFilterCombo.SelectedIndex = 0;
                _handledRoleFilter = HandledRoleFilter.All;
                _searchBox.Text = string.Empty;
                ApplyFilters();
            }
            catch { }
        }

        private void ApplyCardFilter(string openStatus, HandledRoleFilter? handledRole)
        {
            _suppressSelectionChanged = true;
            if (!string.IsNullOrWhiteSpace(openStatus))
            {
                _tabControl.SelectedIndex = 0;
                var idx = _statusFilterCombo.Items.IndexOf(openStatus);
                _statusFilterCombo.SelectedIndex = idx >= 0 ? idx : 0;
                _handledRoleFilter = HandledRoleFilter.All;
            }
            else if (handledRole.HasValue)
            {
                _tabControl.SelectedIndex = 1;
                _statusFilterCombo.SelectedIndex = 0;
                _handledRoleFilter = handledRole.Value;
            }
            else { _statusFilterCombo.SelectedIndex = 0; _handledRoleFilter = HandledRoleFilter.All; }
            ApplyFilters();
        }

        private void ApplyFilters() { ApplyFiltersAsync().FireAndForget(ex => System.Diagnostics.Debug.WriteLine("[Profiles] Filter failed: " + ex)); }

        private async Task ApplyFiltersAsync()
        {
            CancellationTokenSource cts = null;
            var version = 0;
            try
            {
                UpdateDeptFilterState();
                var locationMode = _locationModeCombo.SelectedIndex;
                var departmentName = (_departmentCombo.SelectedItem as LookupItem)?.Name;
                if (string.Equals(departmentName, "All Departments", StringComparison.OrdinalIgnoreCase)) departmentName = null;
                if (locationMode == 2) departmentName = null;
                departmentName = string.IsNullOrWhiteSpace(departmentName) ? null : departmentName.Trim();
                var priority = _priorityCombo.SelectedItem?.ToString();
                if (!string.IsNullOrWhiteSpace(priority) && priority.StartsWith("All", StringComparison.OrdinalIgnoreCase)) priority = null;
                priority = string.IsNullOrWhiteSpace(priority) ? null : priority.Trim();
                var openStatus = _statusFilterCombo.SelectedItem?.ToString();
                if (!string.IsNullOrWhiteSpace(openStatus) && openStatus.StartsWith("All", StringComparison.OrdinalIgnoreCase)) openStatus = null;
                openStatus = string.IsNullOrWhiteSpace(openStatus) ? null : openStatus.Trim();
                var handledRole = _handledRoleFilter;
                var q = (_searchBox.Text ?? string.Empty).Trim();
                var hasQuery = !string.IsNullOrWhiteSpace(q);
                var query = hasQuery ? q : null;
                var emp = _employeeCombo.SelectedItem as CallEmployeeProfileLookup;
                var empId = emp?.EmpId ?? 0;
                var empUserId = emp?.UserId;
                var openAll = _openTicketsAll ?? new List<CallTechOpenTicketRow>();
                var handledAll = _handledTicketsAll ?? new List<CallTechHandledTicketRow>();
                cts = new CancellationTokenSource();
                var token = cts.Token;
                var prev = Interlocked.Exchange(ref _applyFiltersCts, cts);
                if (prev != null) { try { prev.Cancel(); } catch { } try { prev.Dispose(); } catch { } }
                version = Interlocked.Increment(ref _applyFiltersVersion);
                SetStatus("Filtering...");
                var result = await Task.Run(() =>
                {
                    var openFiltered = new List<CallTechOpenTicketRow>();
                    foreach (var t in openAll)
                    {
                        token.ThrowIfCancellationRequested();
                        if (t == null) continue;
                        var hasDept = !string.IsNullOrWhiteSpace(t.Department);
                        var hasBranch = !string.IsNullOrWhiteSpace(t.Branch);
                        if (locationMode == 1 && !hasDept) continue;
                        if (locationMode == 2 && (hasDept || !hasBranch)) continue;
                        if (!string.IsNullOrWhiteSpace(departmentName) && !string.Equals((t.Department ?? string.Empty).Trim(), departmentName, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!string.IsNullOrWhiteSpace(priority) && !string.Equals((t.Priority ?? string.Empty).Trim(), priority, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!string.IsNullOrWhiteSpace(openStatus) && !string.Equals((t.Status ?? string.Empty).Trim(), openStatus, StringComparison.OrdinalIgnoreCase)) continue;
                        if (query != null && !MatchesOpenQuery(t, query)) continue;
                        openFiltered.Add(t);
                    }
                    var handledFiltered = new List<CallTechHandledTicketRow>();
                    foreach (var t in handledAll)
                    {
                        token.ThrowIfCancellationRequested();
                        if (t == null) continue;
                        var hasDept = !string.IsNullOrWhiteSpace(t.Department);
                        var hasBranch = !string.IsNullOrWhiteSpace(t.Branch);
                        if (locationMode == 1 && !hasDept) continue;
                        if (locationMode == 2 && (hasDept || !hasBranch)) continue;
                        if (!string.IsNullOrWhiteSpace(departmentName) && !string.Equals((t.Department ?? string.Empty).Trim(), departmentName, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!string.IsNullOrWhiteSpace(priority) && !string.Equals((t.Priority ?? string.Empty).Trim(), priority, StringComparison.OrdinalIgnoreCase)) continue;
                        if (handledRole == HandledRoleFilter.Assignee && empId > 0 && t.AssignedToEmpId != empId) continue;
                        if (handledRole == HandledRoleFilter.Solver && empUserId.HasValue && t.CompletedByUserId != empUserId.Value) continue;
                        if (query != null && !MatchesHandledQuery(t, query)) continue;
                        handledFiltered.Add(t);
                    }
                    var parts = new List<string> { $"{openFiltered.Count} Open", $"{handledFiltered.Count} Handled" };
                    if (!string.IsNullOrWhiteSpace(openStatus)) parts.Add($"Status={openStatus}");
                    if (!string.IsNullOrWhiteSpace(departmentName)) parts.Add($"Dept={departmentName}");
                    if (locationMode == 1) parts.Add("Show=Dept"); else if (locationMode == 2) parts.Add("Show=Branch");
                    if (!string.IsNullOrWhiteSpace(priority)) parts.Add($"Priority={priority}");
                    if (handledRole != HandledRoleFilter.All) parts.Add(handledRole == HandledRoleFilter.Assignee ? "Handled=Assignee" : "Handled=Solver");
                    if (hasQuery) parts.Add($"Search=\"{q}\"");
                    var statusText = openFiltered.Count == 0 && handledFiltered.Count == 0
                        ? "No profile tickets match the current filters. Clear filters or widen the date range."
                        : string.Join(" | ", parts);
                    return (Open: openFiltered, Handled: handledFiltered, StatusText: statusText);
                }, token).ConfigureAwait(true);
                if (token.IsCancellationRequested || version != _applyFiltersVersion) return;
                _openTicketsFiltered = result.Open;
                _handledTicketsFiltered = result.Handled;
                _suppressSelectionChanged = true;
                try
                {
                    _openGrid.ItemsSource = result.Open.Select(r => new OpenTicketVm(r)).ToList();
                    _handledGrid.ItemsSource = result.Handled.Select(r => new HandledTicketVm(r)).ToList();
                    SetTicketProfileEmpty("Select a ticket");
                }
                finally { _suppressSelectionChanged = false; }
                SetStatus(result.StatusText);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { SetStatus("Filtering failed: " + ex.Message); }
        }

        private void UpdateDeptFilterState()
        {
            try
            {
                var branchOnly = _locationModeCombo.SelectedIndex == 2;
                _departmentCombo.IsEnabled = !branchOnly;
                if (branchOnly && _departmentCombo.Items.Count > 0) _departmentCombo.SelectedIndex = 0;
            }
            catch { }
        }

        // ── Ticket profile panel ──────────────────────────────────────────────────
        private async Task QueueProfileUpdateAsync(DataGrid grid)
        {
            CancellationTokenSource cts = null;
            try
            {
                cts = new CancellationTokenSource();
                var prev = Interlocked.Exchange(ref _profileUpdateCts, cts);
                if (prev != null) { try { prev.Cancel(); } catch { } try { prev.Dispose(); } catch { } }
                await Task.Delay(150, cts.Token);
                if (cts.Token.IsCancellationRequested || _suppressSelectionChanged) return;
                await UpdateTicketProfileAsync(grid);
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        private async Task UpdateTicketProfileAsync(DataGrid grid)
        {
            if (grid == null) { SetTicketProfileEmpty("Select a ticket"); return; }
            var item = grid.SelectedItem;
            if (item == null) { SetTicketProfileEmpty("Select a ticket"); return; }
            int ticketId = 0; string ticketCode = null, status = null, priority = null, location = null;
            string assignedTo = "-", caller = "-"; DateTime? createdAt = null, updatedAt = null;
            if (item is OpenTicketVm ov)
            {
                ticketId = ov.TicketId; ticketCode = ov.TicketCode; status = ov.Status;
                priority = ov.Priority; location = ov.Location;
                createdAt = ov.Source.CreatedAt; updatedAt = ov.Source.UpdatedAt;
            }
            else if (item is HandledTicketVm hv)
            {
                ticketId = hv.TicketId; ticketCode = hv.TicketCode; status = hv.Status;
                priority = hv.Priority; location = hv.Location;
                assignedTo = string.IsNullOrWhiteSpace(hv.Source.AssignedToName) ? "-" : hv.Source.AssignedToName.Trim();
                if (hv.Source.CompletedAtUtc.HasValue)
                    updatedAt = DateTime.SpecifyKind(hv.Source.CompletedAtUtc.Value, DateTimeKind.Utc);
            }
            if (ticketId <= 0) { SetTicketProfileEmpty("Select a ticket"); return; }
            _profileSelectedTicketId = ticketId;
            var version = Interlocked.Increment(ref _profileSelectionVersion);
            _profileTicketText.Text = string.IsNullOrWhiteSpace(ticketCode) ? $"Ticket #{ticketId}" : ticketCode.Trim();
            _profileStatusText.Text = string.IsNullOrWhiteSpace(status) ? "-" : status.Trim();
            _profileStatusText.Foreground = GetStatusBrush(status);
            _profilePriorityText.Text = string.IsNullOrWhiteSpace(priority) ? "-" : priority.Trim();
            _profilePriorityText.Foreground = GetPriorityBrush(priority);
            _profileLocationText.Text = string.IsNullOrWhiteSpace(location) ? "-" : location.Trim();
            _profileAssignedToText.Text = assignedTo;
            _profileCallerText.Text = caller;
            _profileUpdatedText.Text = updatedAt.HasValue ? ToLocalString(updatedAt.Value, "g") : "-";
            _profileCreatedText.Text = createdAt.HasValue ? ToLocalString(createdAt.Value, "g") : "-";
            _profileLastEmailText.Text = "Loading...";
            _profileOpenButton.IsEnabled = true;
            _profileSummaryButton.IsEnabled = true;
            try
            {
                var nowUtc = DateTime.UtcNow;
                CallTicketListItem full = null; CallEmailLogItem lastEmail = null;
                Task<CallTicketListItem> fullTask = null; Task<CallEmailLogItem> emailTask = null;
                if (_ticketCache.TryGetValue(ticketId, out var ct) && (nowUtc - ct.FetchedUtc) <= ProfileCacheTtl)
                    full = ct.Ticket;
                else fullTask = _repository.GetTicketByIdAsync(ticketId);
                if (_emailCache.TryGetValue(ticketId, out var ce) && (nowUtc - ce.FetchedUtc) <= ProfileCacheTtl)
                    lastEmail = ce.Email;
                else emailTask = _repository.GetLastEmailLogForTicketAsync(ticketId);
                if (fullTask != null && emailTask != null) await Task.WhenAll(fullTask, emailTask);
                else if (fullTask != null) full = await fullTask;
                else if (emailTask != null) lastEmail = await emailTask;
                if (version != _profileSelectionVersion) return;
                if (full == null && fullTask != null) full = fullTask.Result;
                if (lastEmail == null && emailTask != null) lastEmail = emailTask.Result;
                if (fullTask != null && full != null) _ticketCache[ticketId] = (full, nowUtc);
                if (emailTask != null) _emailCache[ticketId] = (lastEmail, nowUtc);
                if (full != null)
                {
                    _profileAssignedToText.Text = string.IsNullOrWhiteSpace(full.ResponsiblePerson) ? "(Unassigned)" : full.ResponsiblePerson.Trim();
                    _profileCallerText.Text = string.IsNullOrWhiteSpace(full.CallerName) ? "-" : full.CallerName.Trim();
                    _profileUpdatedText.Text = ToLocalString(GetLastActivityUtc(full, out _), "g");
                    _profileCreatedText.Text = ToLocalString(full.CreatedAt, "g");
                }
                _profileLastEmailText.Text = lastEmail == null ? "None"
                    : $"{(string.IsNullOrWhiteSpace(lastEmail.EmailType) ? "-" : lastEmail.EmailType.Trim())} \u2022 {ToLocalString(lastEmail.DateSent, "g")}";
            }
            catch { if (version != _profileSelectionVersion) return; if (_profileLastEmailText.Text == "Loading...") _profileLastEmailText.Text = "-"; }
        }

        private void SetTicketProfileEmpty(string title)
        {
            _profileSelectedTicketId = 0; Interlocked.Increment(ref _profileSelectionVersion);
            _profileTicketText.Text = title ?? "Select a ticket";
            _profileStatusText.Text = "-"; _profileStatusText.Foreground = BrushFromRgb(15, 23, 42);
            _profilePriorityText.Text = "-"; _profilePriorityText.Foreground = BrushFromRgb(15, 23, 42);
            _profileLocationText.Text = "-"; _profileAssignedToText.Text = "-"; _profileCallerText.Text = "-";
            _profileUpdatedText.Text = "-"; _profileCreatedText.Text = "-"; _profileLastEmailText.Text = "-";
            _profileOpenButton.IsEnabled = false; _profileSummaryButton.IsEnabled = false;
        }

        // ── Dialogs ───────────────────────────────────────────────────────────────
        private void OpenProfileCard()
        {
            var emp = _employeeCombo.SelectedItem as CallEmployeeProfileLookup;
            if (emp != null)
            {
                var dlg = new WpfEmployeeProfileDetailsDialog(emp, _repository)
                {
                    Owner = Window.GetWindow(this)
                };
                dlg.ShowDialog();
            }
            else MessageBox.Show("Please select an employee first.", "Profile", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task OpenSelectedTicketSummaryAsync()
        {
            var ticketId = _profileSelectedTicketId;
            if (ticketId <= 0) return;
            try
            {
                var ticket = await _repository.GetTicketByIdAsync(ticketId);
                if (ticket == null) { MessageBox.Show("Unable to load ticket details.", "Ticket Summary", MessageBoxButton.OK, MessageBoxImage.Information); return; }
                var settings = await _repository.GetEscalationSettingsAsync();
                var ovr = await _repository.GetTicketEscalationOverrideAsync(ticketId);
                var slaLabel = BuildSlaShortLabel(ticket.Priority, ticket.CreatedAt, ticket.Status);
                var slaTooltip = BuildSlaTooltip(ticket);
                var escalationText = BuildEscalationLabel(ticket, settings, ovr);
                var lastActivityUtc = GetLastActivityUtc(ticket, out var lastActivitySource);
                var idle = DateTime.UtcNow - lastActivityUtc;
                if (idle < TimeSpan.Zero) idle = idle.Duration();
                var dlg = new WpfTicketSummaryDialog(ticket: ticket, slaLabel: slaLabel, slaTooltip: slaTooltip,
                    escalationText: escalationText,
                    lastActivityText: $"{ToLocalString(lastActivityUtc, "g")} ({lastActivitySource})",
                    idleText: FormatShortDuration(idle))
                {
                    Owner = Window.GetWindow(this)
                };
                dlg.ShowDialog();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Ticket Summary", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private async Task OpenAutoEscalationAssigneeDialogAsync()
        {
            try
            {
                if (!AppSession.IsLoggedIn) { MessageBox.Show("You must be logged in.", "Call Monitoring Profiles", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                var userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                if (userId == null) { MessageBox.Show("No current user ID found.", "Call Monitoring Profiles", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

                bool reopen;
                do
                {
                    reopen = false;
                    var assignmentEligibilitySupported = await _repository.AssignmentEligibilitySettingsEnabledAsync();
                    var escalationSettingsSupported = await _repository.EscalationSettingsEnabledAsync();
                    var autoEscalationSupported = await _repository.AutoEscalationAssigneeSettingsEnabledAsync();
                    if (!assignmentEligibilitySupported && !escalationSettingsSupported && !autoEscalationSupported)
                    {
                        MessageBox.Show("No assignee or escalation settings schema is installed yet.\r\nInstall the required Call Monitoring settings scripts first.", "Manage Assignees", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var items = await _repository.GetCallAssignmentEligibilityAsync(_itDepartmentName);
                    var escalationSettings = await _repository.GetEscalationSettingsAsync();
                    var currentAssigneeEmpId = autoEscalationSupported ? await _repository.GetAutoEscalationAssigneeEmpIdAsync() : null;

                    var owner = WinForms.Form.ActiveForm;
                    var dlg = new WpfAssigneeEligibilityDialog(
                        items,
                        escalationSettings,
                        currentAssigneeEmpId,
                        assignmentEligibilitySupported,
                        autoEscalationSupported,
                        escalationSettingsSupported,
                        async (item) =>
                        {
                            var existingUser = item.HasActiveAccount
                                ? new LegacyUserAccountDto
                                {
                                    UserId = item.UserId ?? 0,
                                    Username = item.UserName,
                                    IsActive = true,
                                    EmpId = item.EmpId,
                                    EmployeeName = item.EmployeeName
                                }
                                : null;
                            var accountDlg = existingUser != null
                                ? new UserAccountEditDialog(_repository.ConnectionString, existingUser)
                                : new UserAccountEditDialog(_repository.ConnectionString, preselectedEmpId: item.EmpId);
                            var result = accountDlg.ShowDialog(owner);
                            if (result == WinForms.DialogResult.OK)
                            {
                                reopen = true;
                                return true;
                            }
                            return false;
                        },
                        AppSession.IsDeveloper);
                    if (owner != null)
                        new System.Windows.Interop.WindowInteropHelper(dlg).Owner = owner.Handle;
                    var result = dlg.ShowDialog();
                    if (result == true)
                    {
                        if (assignmentEligibilitySupported)
                            await _repository.SaveCallAssignmentEligibilityAsync(dlg.Items, updatedByUserId: userId);

                        if (autoEscalationSupported)
                            await _repository.SaveAutoEscalationAssigneeEmpIdAsync(dlg.SelectedAutoEscalationEmpId, updatedByUserId: userId);

                        if (escalationSettingsSupported)
                            await _repository.SaveEscalationSettingsAsync(dlg.EscalationSettings);

                        SetStatus("Assignee settings updated.");
                        MessageBox.Show(
                            "Assignee and escalation settings updated.",
                            "Manage Assignees",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        reopen = false;
                    }
                } while (reopen);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Manage Assignees", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void SetStatus(string text) { if (_statusText != null) _statusText.Text = text ?? string.Empty; }

        // ── Static utilities ──────────────────────────────────────────────────────
        private static bool MatchesOpenQuery(CallTechOpenTicketRow t, string q) =>
            Ci(t.TicketCode, q) || Ci(t.Issue, q) || Ci(t.Department, q) || Ci(t.Branch, q) || Ci(t.Status, q) || Ci(t.Priority, q);

        private static bool MatchesHandledQuery(CallTechHandledTicketRow t, string q) =>
            Ci(t.TicketCode, q) || Ci(t.Issue, q) || Ci(t.Department, q) || Ci(t.Branch, q) ||
            Ci(t.Status, q) || Ci(t.Priority, q) || Ci(t.AssignedToName, q) || Ci(t.CompletedBy, q);

        private static bool Ci(string h, string n) =>
            !string.IsNullOrWhiteSpace(h) && !string.IsNullOrWhiteSpace(n) && h.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string FormatAgo(DateTime utc, DateTime nowUtc)
        {
            var diff = nowUtc - utc; if (diff < TimeSpan.Zero) diff = diff.Duration();
            if (diff.TotalDays >= 1) return $"{(int)Math.Floor(diff.TotalDays)}d ago";
            if (diff.TotalHours >= 1) return $"{(int)Math.Floor(diff.TotalHours)}h ago";
            return $"{Math.Max(1, (int)Math.Round(diff.TotalMinutes))}m ago";
        }

        private static string BuildAgeLabel(DateTime createdAtUtc, DateTime nowUtc)
        {
            var c = createdAtUtc.Kind == DateTimeKind.Utc ? createdAtUtc : DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
            var age = nowUtc - c; if (age < TimeSpan.Zero) age = age.Duration();
            return age.TotalDays >= 1 ? $"{(int)Math.Floor(age.TotalDays)}d" : $"{Math.Max(1, (int)Math.Round(age.TotalHours))}h";
        }

        private static string BuildSlaShortLabel(string priority, DateTime createdAtUtc, string status) =>
            BuildSlaShortLabel(priority, createdAtUtc, status, DateTime.UtcNow);

        private static string BuildSlaShortLabel(string priority, DateTime createdAtUtc, string status, DateTime nowUtc)
        {
            if (IsFinalStatus(status)) return "Closed";
            var hours = GetSlaTargetHours(priority); if (hours <= 0) return "-";
            var c = createdAtUtc.Kind == DateTimeKind.Utc ? createdAtUtc : DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
            var remaining = c.AddHours(hours) - nowUtc;
            if (remaining <= TimeSpan.Zero) { var over = remaining.Duration(); return over.TotalDays >= 1 ? $"Breached {over.Days}d" : $"Breached {Math.Max(1, (int)Math.Round(over.TotalHours))}h"; }
            return remaining.TotalDays >= 1 ? $"Due {(int)Math.Floor(remaining.TotalDays)}d" : $"Due {Math.Max(1, (int)Math.Round(remaining.TotalHours))}h";
        }

        private static string BuildSlaTooltip(CallTicketListItem t)
        {
            if (t == null) return "SLA: -";
            var hours = GetSlaTargetHours(t.Priority); if (hours <= 0) return "SLA: -";
            var due = DateTime.SpecifyKind(t.CreatedAt, DateTimeKind.Utc).AddHours(hours);
            var remaining = due - DateTime.UtcNow;
            if (IsFinalStatus(t.Status)) return $"SLA: (closed) \u2022 due {due.ToLocalTime():yyyy-MM-dd HH:mm}";
            if (remaining <= TimeSpan.Zero) return $"SLA: Breached by {FormatShortDuration(remaining.Duration())} \u2022 due {due.ToLocalTime():yyyy-MM-dd HH:mm}";
            return $"SLA: Due in {FormatShortDuration(remaining)} \u2022 due {due.ToLocalTime():yyyy-MM-dd HH:mm}";
        }

        private static bool IsFinalStatus(string s) { var t = (s ?? string.Empty).Trim(); return t.Equals("Solved", StringComparison.OrdinalIgnoreCase) || t.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase) || t.Equals("Closed", StringComparison.OrdinalIgnoreCase); }

        private static DateTime GetLastActivityUtc(CallTicketListItem t, out string source)
        {
            if (t == null) { source = "-"; return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc); }
            if (t.LastContactAt.HasValue) { source = "LastContactAt"; var v = t.LastContactAt.Value; return v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc); }
            source = "UpdatedAt"; var u = t.UpdatedAt; return u.Kind == DateTimeKind.Utc ? u : DateTime.SpecifyKind(u, DateTimeKind.Utc);
        }

        private static string FormatShortDuration(TimeSpan v)
        {
            if (v < TimeSpan.Zero) v = v.Duration();
            if (v.Days > 0) return v.Hours > 0 ? $"{v.Days}d {v.Hours}h" : $"{v.Days}d";
            if (v.Hours > 0) return v.Minutes > 0 ? $"{v.Hours}h {v.Minutes}m" : $"{v.Hours}h";
            return $"{Math.Max(1, (int)Math.Round(v.TotalMinutes))}m";
        }

        private static string ToLocalString(DateTime value, string format)
        {
            var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return utc.ToLocalTime().ToString(string.IsNullOrWhiteSpace(format) ? "g" : format);
        }

        private string BuildEscalationLabel(CallTicketListItem t, CallEscalationSettingsItem settings, CallTicketEscalationOverrideItem ovr)
        {
            if (t == null) return "-";
            var ageDays = Math.Max(0, t.TicketAgeDays);
            var supDays = ovr != null ? ovr.DaysToSupervisor : (settings?.DaysToSupervisor ?? 2);
            var mgrDays = ovr != null ? ovr.DaysToManager : (settings?.DaysToManager ?? 3);
            if (supDays < 1) supDays = 1; if (mgrDays < supDays) mgrDays = supDays;
            if (ageDays >= mgrDays) { var ob = ageDays - mgrDays; return ob > 0 ? $"Escalated: Manager \u2022 {ob}d past threshold" : "Escalated: Manager"; }
            if (ageDays >= supDays) { var mi = mgrDays - ageDays; return mi > 0 ? $"Escalated: Supervisor \u2022 manager in {mi}d" : "Escalated: Supervisor"; }
            var suffix = ovr != null ? " (override)" : string.Empty;
            return $"{(supDays - ageDays <= 1 ? "Warning" : "Next")}: sup in {supDays - ageDays}d, mgr in {mgrDays - ageDays}d{suffix}";
        }

        private static int GetSlaTargetHours(string priority)
        {
            var p = (priority ?? string.Empty).Trim();
            if (p.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return 24;
            if (p.Equals("High", StringComparison.OrdinalIgnoreCase)) return 48;
            if (p.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return 72;
            if (p.Equals("Low", StringComparison.OrdinalIgnoreCase)) return 120;
            return 72;
        }

        private static Brush GetStatusBrush(string s)
        {
            var t = (s ?? string.Empty).Trim();
            if (t.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(245, 158, 11);
            if (t.Equals("Overdue", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(239, 68, 68);
            if (t.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(124, 58, 237);
            if (t.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(37, 99, 235);
            if (t.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(99, 102, 241);
            if (t.Equals("Solved", StringComparison.OrdinalIgnoreCase) || t.Equals("Closed", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(16, 185, 129);
            if (t.Equals("Reopened", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(239, 68, 68);
            return BrushFromRgb(15, 23, 42);
        }

        private static Brush GetPriorityBrush(string p)
        {
            var t = (p ?? string.Empty).Trim();
            if (t.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(239, 68, 68);
            if (t.Equals("High", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(245, 158, 11);
            if (t.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(37, 99, 235);
            if (t.Equals("Low", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(100, 116, 139);
            return BrushFromRgb(15, 23, 42);
        }

        // ── UI Builders ───────────────────────────────────────────────────────────
        private static Border BuildHero()
        {
            var card = new Border
            {
                Background = new LinearGradientBrush(Color.FromRgb(15, 23, 42), Color.FromRgb(30, 41, 59), new Point(0, 0), new Point(1, 1)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(28), Padding = new Thickness(26, 24, 26, 24),
                Effect = new DropShadowEffect { BlurRadius = 24, Color = Color.FromArgb(32, 15, 23, 42), ShadowDepth = 0, Opacity = 0.25 }
            };
            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            card.Child = layout;
            var textStack = new StackPanel();
            textStack.Children.Add(new TextBlock { Text = "Tech Profiles", FontSize = 30, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            textStack.Children.Add(new TextBlock
            {
                Text = "Per-employee view of open and handled tickets, SLA status, and ticket profile drill-down — same logic as the WinForms profiles form, modernised in WPF.",
                Margin = new Thickness(0, 8, 0, 0), FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(191, 219, 254)), TextWrapping = TextWrapping.Wrap
            });
            layout.Children.Add(textStack);
            var chips = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            chips.Children.Add(CreateHeroChip("Per-employee", BrushFromRgb(14, 165, 233)));
            chips.Children.Add(CreateHeroChip("Open + Handled", BrushFromRgb(16, 185, 129)));
            chips.Children.Add(CreateHeroChip("Ticket profile", BrushFromRgb(245, 158, 11)));
            Grid.SetColumn(chips, 1);
            layout.Children.Add(chips);
            return card;
        }

        private static Border CreateHeroChip(string text, Brush background) =>
            new Border
            {
                Margin = new Thickness(8, 0, 0, 8), Padding = new Thickness(12, 7, 12, 7),
                CornerRadius = new CornerRadius(999), Background = background,
                Child = new TextBlock { Text = text, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White }
            };

        private Border BuildClickableMetricCard(int column, string title, string subtitle, TextBlock valueBlock, Color accentColor)
        {
            var card = CreateGlassCard();
            card.Padding = new Thickness(16, 14, 16, 14);
            card.Cursor = Cursors.Hand;
            if (column > 0) card.Margin = new Thickness(14, 0, 0, 0);
            var stack = new StackPanel();
            card.Child = stack;
            stack.Children.Add(new Border { Width = 36, Height = 4, CornerRadius = new CornerRadius(999), Background = new SolidColorBrush(accentColor), HorizontalAlignment = HorizontalAlignment.Left });
            stack.Children.Add(new TextBlock { Text = title, Margin = new Thickness(0, 12, 0, 0), FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(71, 85, 105) });
            valueBlock.Margin = new Thickness(0, 6, 0, 0); valueBlock.Text = "0";
            stack.Children.Add(valueBlock);
            stack.Children.Add(new TextBlock { Text = subtitle, Margin = new Thickness(0, 5, 0, 0), FontSize = 11, Foreground = BrushFromRgb(100, 116, 139), TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(card, column);
            return card;
        }

        private static Border CreateGlassCard() =>
            new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(248, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(223, 229, 236)), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(24), Padding = new Thickness(22),
                Effect = new DropShadowEffect { BlurRadius = 18, Color = Color.FromArgb(28, 15, 23, 42), ShadowDepth = 0, Opacity = 0.2 }
            };

        private static TextBlock CreateMetricValue() =>
            new TextBlock { FontSize = 28, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(15, 23, 42), Text = "0" };

        private static StackPanel BuildProfileField(string label, TextBlock valueBlock)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            stack.Children.Add(new TextBlock { Text = label, FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(100, 116, 139), Margin = new Thickness(0, 0, 0, 3) });
            stack.Children.Add(valueBlock);
            return stack;
        }

        private static FrameworkElement MakeCompactField(string label, FrameworkElement control)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 18, 8), VerticalAlignment = VerticalAlignment.Bottom };
            stack.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 5), FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(71, 85, 105) });
            stack.Children.Add(control);
            return stack;
        }

        private static Button CreatePrimaryButton(string text, Brush background) =>
            new Button
            {
                Content = text, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(14, 9, 14, 9),
                FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
                Background = background, BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };

        private static ComboBox CreateTextComboBox() =>
            new ComboBox { Padding = new Thickness(8, 6, 8, 6), FontSize = 12, MinWidth = 130 };

        private static DatePicker CreateDatePicker() => new DatePicker { FontSize = 12 };

        private static DataGrid CreateReportGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false,
                CanUserResizeRows = false,
                IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = BrushFromRgb(241, 245, 249),
                Background = Brushes.White,
                RowBackground = Brushes.White,
                AlternatingRowBackground = BrushFromRgb(249, 250, 251),
                BorderThickness = new Thickness(0),
                RowHeaderWidth = 0,
                RowHeight = 44,
                Margin = new Thickness(20, 0, 20, 16),
                MinHeight = 520
            };

            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(100, 116, 139)));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 12, 8, 12)));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFromRgb(226, 232, 240)));
            headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            grid.ColumnHeaderStyle = headerStyle;

            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 10, 8, 10)));
            cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(Control.VerticalAlignmentProperty, VerticalAlignment.Center));
            var cellSelectedTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
            cellSelectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            cellSelectedTrigger.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Triggers.Add(cellSelectedTrigger);
            grid.CellStyle = cellStyle;

            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(15, 23, 42)));
            rowStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
            rowStyle.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFromRgb(241, 245, 249)));
            rowStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            rowStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, BrushFromRgb(248, 250, 252)));
            rowStyle.Triggers.Add(hoverTrigger);

            var selectedTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, BrushFromRgb(239, 246, 255)));
            selectedTrigger.Setters.Add(new Setter(DataGridRow.BorderBrushProperty, BrushFromRgb(59, 130, 246)));
            selectedTrigger.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0, 0, 0, 2)));
            rowStyle.Triggers.Add(selectedTrigger);

            grid.RowStyle = rowStyle;
            return grid;
        }

        private static void SetupOpenColumns(DataGrid g)
        {
            g.Columns.Add(CreateTemplateCol("TICKET", 90, "<DataTemplate><TextBlock Text='{Binding TicketCode}' FontWeight='Bold' Foreground='#0284c7' VerticalAlignment='Center'/></DataTemplate>"));
            g.Columns.Add(CreateCol("Location", "LOCATION", 150));
            g.Columns.Add(CreateCol("Issue", "ISSUE", 240));
            g.Columns.Add(CreateTemplateCol("STATUS", 110, "<DataTemplate><TextBlock Text='{Binding Status}' FontWeight='Bold' VerticalAlignment='Center'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Foreground' Value='#374151'/><Style.Triggers><DataTrigger Binding='{Binding Status}' Value='Solved'><Setter Property='Foreground' Value='#10b981'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Closed'><Setter Property='Foreground' Value='#10b981'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Resolved (Temporary)'><Setter Property='Foreground' Value='#14b8a6'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Escalated'><Setter Property='Foreground' Value='#ef4444'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='In Progress'><Setter Property='Foreground' Value='#7c3aed'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Pending'><Setter Property='Foreground' Value='#2563eb'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></DataTemplate>"));
            g.Columns.Add(CreateTemplateCol("PRIORITY", 85, "<DataTemplate><TextBlock Text='{Binding Priority}' FontWeight='Bold' VerticalAlignment='Center'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Foreground' Value='#374151'/><Style.Triggers><DataTrigger Binding='{Binding Priority}' Value='Critical'><Setter Property='Foreground' Value='#ef4444'/></DataTrigger><DataTrigger Binding='{Binding Priority}' Value='High'><Setter Property='Foreground' Value='#ef4444'/></DataTrigger><DataTrigger Binding='{Binding Priority}' Value='Medium'><Setter Property='Foreground' Value='#f59e0b'/></DataTrigger><DataTrigger Binding='{Binding Priority}' Value='Low'><Setter Property='Foreground' Value='#6b7280'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></DataTemplate>"));
            g.Columns.Add(CreateCol("AgeLabel", "AGE", 65));
            g.Columns.Add(CreateCol("SlaLabel", "SLA", 110));
            g.Columns.Add(CreateCol("UpdatedAtDisplay", "UPDATED", 120));
            g.Columns.Add(CreateCol("UpdatedAgoLabel", "UPDATED (AGO)", 110));
            g.Columns.Add(CreateCol("CreatedAtDisplay", "CREATED", 120));
        }

        private static void SetupHandledColumns(DataGrid g)
        {
            g.Columns.Add(CreateTemplateCol("TICKET", 90, "<DataTemplate><TextBlock Text='{Binding TicketCode}' FontWeight='Bold' Foreground='#0284c7' VerticalAlignment='Center'/></DataTemplate>"));
            g.Columns.Add(CreateCol("Location", "LOCATION", 150));
            g.Columns.Add(CreateCol("Issue", "ISSUE", 220));
            g.Columns.Add(CreateTemplateCol("STATUS", 130, "<DataTemplate><TextBlock Text='{Binding Status}' FontWeight='Bold' VerticalAlignment='Center'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Foreground' Value='#374151'/><Style.Triggers><DataTrigger Binding='{Binding Status}' Value='Solved'><Setter Property='Foreground' Value='#10b981'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Closed'><Setter Property='Foreground' Value='#10b981'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Resolved (Temporary)'><Setter Property='Foreground' Value='#14b8a6'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Escalated'><Setter Property='Foreground' Value='#ef4444'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='In Progress'><Setter Property='Foreground' Value='#7c3aed'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Pending'><Setter Property='Foreground' Value='#2563eb'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></DataTemplate>"));
            g.Columns.Add(CreateTemplateCol("PRIORITY", 85, "<DataTemplate><TextBlock Text='{Binding Priority}' FontWeight='Bold' VerticalAlignment='Center'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Foreground' Value='#374151'/><Style.Triggers><DataTrigger Binding='{Binding Priority}' Value='Critical'><Setter Property='Foreground' Value='#ef4444'/></DataTrigger><DataTrigger Binding='{Binding Priority}' Value='High'><Setter Property='Foreground' Value='#ef4444'/></DataTrigger><DataTrigger Binding='{Binding Priority}' Value='Medium'><Setter Property='Foreground' Value='#f59e0b'/></DataTrigger><DataTrigger Binding='{Binding Priority}' Value='Low'><Setter Property='Foreground' Value='#6b7280'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></DataTemplate>"));
            g.Columns.Add(CreateCol("AssignedToName", "ASSIGNED TO", 130));
            g.Columns.Add(CreateCol("CompletedBy", "COMPLETED BY", 130));
            g.Columns.Add(CreateCol("CompletedAtDisplay", "COMPLETED", 120));
            g.Columns.Add(CreateCol("CompletedAgoLabel", "COMPLETED (AGO)", 120));
        }

        private static DataGridTextColumn CreateCol(string path, string header, double width) =>
            new DataGridTextColumn { Header = header, Binding = new Binding(path), Width = new DataGridLength(width) };

        private static DataGridTemplateColumn CreateTemplateCol(string header, double width, string xaml)
        {
            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            ctx.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
            return new DataGridTemplateColumn
            {
                Header = header,
                CellTemplate = (DataTemplate)XamlReader.Parse(xaml, ctx),
                Width = new DataGridLength(width)
            };
        }

        private static Style BuildTabItemStyle()
        {
            const string xaml = @"<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" TargetType=""TabItem""><Setter Property=""Foreground"" Value=""#64748b""/><Setter Property=""FontSize"" Value=""13""/><Setter Property=""Template""><Setter.Value><ControlTemplate TargetType=""TabItem""><Border Name=""Bd"" Padding=""16,8"" Margin=""0,0,4,0"" CornerRadius=""8,8,0,0"" Background=""#e2e8f0""><ContentPresenter x:Name=""Cs"" VerticalAlignment=""Center"" HorizontalAlignment=""Center"" ContentSource=""Header""/></Border><ControlTemplate.Triggers><Trigger Property=""IsSelected"" Value=""True""><Setter TargetName=""Bd"" Property=""Background"" Value=""White""/><Setter Property=""Foreground"" Value=""#0f172a""/><Setter Property=""FontWeight"" Value=""SemiBold""/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>";
            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            ctx.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
            return (Style)XamlReader.Parse(xaml, ctx);
        }

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b) =>
            new SolidColorBrush(Color.FromRgb(r, g, b));

        // ── Row ViewModels ────────────────────────────────────────────────────────
        private sealed class OpenTicketVm
        {
            private readonly DateTime _now = DateTime.UtcNow;
            public OpenTicketVm(CallTechOpenTicketRow source) { Source = source; }
            public CallTechOpenTicketRow Source { get; }
            public int TicketId => Source.TicketId;
            public string TicketCode => Source.TicketCode ?? string.Empty;
            public string Location => !string.IsNullOrWhiteSpace(Source.Department) ? Source.Department : (Source.Branch ?? string.Empty);
            public string Issue => Source.Issue ?? string.Empty;
            public string Status => Source.Status ?? string.Empty;
            public string Priority => Source.Priority ?? string.Empty;
            public string AgeLabel => BuildAgeLabel(Source.CreatedAt, _now);
            public string SlaLabel => BuildSlaShortLabel(Source.Priority, Source.CreatedAt, Source.Status, _now);
            public string UpdatedAtDisplay => ToLocalString(Source.UpdatedAt, "g");
            public string UpdatedAgoLabel => FormatAgo(DateTime.SpecifyKind(Source.UpdatedAt, DateTimeKind.Utc), _now);
            public string CreatedAtDisplay => ToLocalString(Source.CreatedAt, "g");
        }

        private sealed class HandledTicketVm
        {
            private readonly DateTime _now = DateTime.UtcNow;
            public HandledTicketVm(CallTechHandledTicketRow source) { Source = source; }
            public CallTechHandledTicketRow Source { get; }
            public int TicketId => Source.TicketId;
            public string TicketCode => Source.TicketCode ?? string.Empty;
            public string Location => !string.IsNullOrWhiteSpace(Source.Department) ? Source.Department : (Source.Branch ?? string.Empty);
            public string Issue => Source.Issue ?? string.Empty;
            public string Status => Source.Status ?? string.Empty;
            public string Priority => Source.Priority ?? string.Empty;
            public string AssignedToName => Source.AssignedToName ?? string.Empty;
            public string CompletedBy => Source.CompletedBy ?? string.Empty;
            public string CompletedAtDisplay => Source.CompletedAtUtc.HasValue ? ToLocalString(Source.CompletedAtUtc.Value, "g") : "-";
            public string CompletedAgoLabel => Source.CompletedAtUtc.HasValue ? FormatAgo(DateTime.SpecifyKind(Source.CompletedAtUtc.Value, DateTimeKind.Utc), _now) : "-";
        }
    }
}
