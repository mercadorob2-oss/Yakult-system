using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WinForms = System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Forms.CallMonitoring;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Employee;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Wpf.RepairPortal.NewTicketIntake.Views;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed class WpfTicketManagementWorkspace : UserControl
    {
        private readonly TextBox _searchBox;
        private readonly ComboBox _statusFilter;
        private readonly ComboBox _assigneeFilter;
        private readonly ComboBox _branchFilter;
        private readonly DataGrid _pendingGrid;
        private readonly DataGrid _solvedGrid;
        private readonly TextBlock _pendingPager;
        private readonly TextBlock _solvedPager;
        private readonly TextBlock _pendingStateText;
        private readonly TextBlock _solvedStateText;
        private readonly Button _pendingPrevButton;
        private readonly Button _pendingNextButton;
        private readonly Button _solvedPrevButton;
        private readonly Button _solvedNextButton;
        private readonly TextBlock _profileStatus;
        private readonly TextBlock _profilePriority;
        private readonly TextBlock _profileAssignee;
        private readonly TextBlock _profileCaller;
        private readonly TextBlock _profileLocation;
        private readonly TextBlock _profileActivity;
        private readonly TextBlock _profileCreated;
        private readonly TextBlock _profileEscalation;
        private readonly TextBlock _profileLastEmail;
        private readonly TextBlock _selectionHeadline;
        private readonly TextBlock _selectionSubhead;
        private TextBlock _inspectorTicketCode;
        private TextBlock _inspectorIssue;
        private TextBlock _inspectorStatus;
        private FrameworkElement _profileEscalationHost;
        private FrameworkElement _profileLastEmailHost;
        private readonly ListBox _activityList;
        private readonly Button _detailsButton;
        private readonly Button _summaryButton;
        private readonly Button _deleteButton;
        private readonly Button _createTicketButton;
        private readonly Button _clearCreateTicketButton;
        private readonly Button _addCreateCallerButton;
        private readonly Button _refreshTicketsButton;
        private readonly ComboBox _createCompany;
        private readonly ComboBox _createDepartment;
        private readonly ComboBox _createBranch;
        private readonly ComboBox _createCaller;
        private readonly ComboBox _createAssignedTo;
        private readonly TextBox _createIssueBox;
        private readonly TextBox _createNotesBox;
        private readonly TextBlock _createStatusText;
        private readonly TextBlock _pendingCaseHeadline;
        private readonly ComboBox _pendingStatusCombo;
        private readonly ComboBox _pendingPriorityCombo;
        private readonly ComboBox _pendingAssigneeCombo;
        private readonly TextBox _pendingProblemBox;
        private readonly TextBox _pendingSolutionBox;
        private readonly Button _pendingAssignToMeButton;
        private readonly Button _pendingReassignButton;
        private readonly Button _pendingUpdateButton;
        private readonly Button _pendingReopenButton;
        private readonly Button _pendingMarkAsButton;
        private readonly Button _pendingFieldWorkButton;
        private readonly Button _pendingReturnTempButton;
        private readonly Button _pendingSyncSetButton;
        private readonly Button _pendingEscalationOverrideButton;

        // Lifecycle browser controls. Portal Intake remains a separate workspace.
        // Ticket Management keeps the selected case visible in a permanent inspector.
        private StackPanel _caseCardsPanel;
        private StackPanel _caseTablePanel;
        private Border _casesViewHost;
        private Border _tableViewHost;
        private Button _casesViewButton;
        private Button _tableViewButton;
        private TextBlock _lifecycleStateText;
        private TextBlock _queueSummaryText;
        private Border _inspectorCard;
        private ContentControl _inspectorContent;
        private readonly List<Button> _inspectorTabButtons = new List<Button>();
        private readonly List<FrameworkElement> _inspectorTabContents = new List<FrameworkElement>();
        private int _inspectorTabIndex;
        private readonly List<Button> _lifecycleChipButtons = new List<Button>();
        private readonly List<LifecycleTicketRow> _lifecycleRows = new List<LifecycleTicketRow>();
        private readonly Dictionary<string, bool> _tableGroupExpanded = new Dictionary<string, bool>();
        private string _lifecycleScope = "All Tickets";
        // Cases is intentionally the entry view; Table is a temporary alternate presentation.
        private string _caseBrowserView = "Cases";
        private string _tableSortColumn = "Needs Attention";
        private bool _tableSortDescending = true;

        // Backdate controls for backlog ticket creation
        private readonly CheckBox _backdateCheck;
        private readonly DatePicker _backdateDatePicker;
        private readonly TextBox _backdateTimeText;

        // Escalation override controls for create ticket
        private readonly TextBlock _createEscalationSummary;
        private readonly Button _createEscalationButton;
        private readonly Button _createEscalationClearButton;

        private ICallMonitoringRepository _repository;
        private bool _initialized;
        private int _loadingFlag; // 0 = idle, 1 = loading (thread-safe via Interlocked)
        private bool _loadingCreateLookups;
        private bool _createWindowOpen; // Guard: only one Create Ticket window at a time
        private int _pendingPageIndex = 1;
        private int _solvedPageIndex = 1;
        private bool _pendingHasNext;
        private bool _solvedHasNext;
        private int _pendingTotalCount;
        private int _solvedTotalCount;
        private int _overdueDays = 3;
        private List<LookupItem> _companies = new List<LookupItem>();
        private List<LookupItem> _departments = new List<LookupItem>();
        private List<LookupItem> _assignees = new List<LookupItem>();
        private List<LookupItem> _branches = new List<LookupItem>();
        private CallEscalationSettingsItem _escalationSettings;
        private CallTicketListItem _selectedTicket;
        private List<PendingTicketRow> _pendingRows = new List<PendingTicketRow>();
        private List<SolvedTicketRow> _solvedRows = new List<SolvedTicketRow>();

        private System.Windows.Threading.DispatcherTimer _callerSearchDebounceTimer;
        private bool _newTicketOrgLockedFromEmployeeSearch;
        private bool _suppressCallerSearchTextChanged;
        private TextBox _callerEditBox;

        // Create-ticket escalation override state (mirrors WinForms TicketManagementControl)
        private int? _createTicketEscDaysToSupervisor;
        private int? _createTicketEscDaysToManager;
        private string _createTicketEscReason;

        public WpfTicketManagementWorkspace()
        {
            Background = BrushFromRgb(241, 244, 247);

            // Apply the shared slim scrollbar style
            Resources.MergedDictionaries.Add(WpfThemeResources.GetScrollBarStyle());

            // Initialize the create ticket panel first so readonly fields are populated
            var createTicketPanel = BuildCreateTicketSection(
                out _createCompany,
                out _createDepartment,
                out _createBranch,
                out _createCaller,
                out _createAssignedTo,
                out _createIssueBox,
                out _createNotesBox,
                out _createStatusText,
                out _addCreateCallerButton,
                out _createTicketButton, // This is the actual submit button
                out _clearCreateTicketButton,
                out _backdateCheck,
                out _backdateDatePicker,
                out _backdateTimeText,
                out _createEscalationSummary,
                out _createEscalationButton,
                out _createEscalationClearButton);

            UpdateCreateTicketEscalationUi();

            var createTicketContainer = new Border
            {
                Padding = new Thickness(20),
                Background = BrushFromRgb(241, 244, 247),
                Child = createTicketPanel
            };

            // 1. Setup ScrollViewer and Root Grid
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var root = new Grid { Margin = new Thickness(28, 24, 28, 28) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Hero & Filters
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Main Body
            scroll.Content = root;

            // ==========================================
            // TOP SECTION: Hero & Filters
            // ==========================================
            var topSection = new StackPanel();
            Grid.SetRow(topSection, 0);
            root.Children.Add(topSection);

            // Add Hero header
            topSection.Children.Add(BuildHero());

            // Filters & Global Actions Card
            var filtersCard = CreateGlassCard();
            filtersCard.Margin = new Thickness(0, 16, 0, 18);
            filtersCard.Padding = new Thickness(22, 14, 22, 14);
            topSection.Children.Add(filtersCard);

            var filtersGrid = new Grid();
            filtersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); // Search
            filtersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Status
            filtersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Assignee
            filtersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Branch
            filtersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Actions

            _searchBox = CreateTextBox("Search issue, branch, caller...");
            AddFilterField(filtersGrid, 0, "Search", _searchBox);

            _statusFilter = CreateTextComboBox();
            _statusFilter.Items.Add("All");
            _statusFilter.Items.Add("Pending");
            _statusFilter.Items.Add("In Progress");
            _statusFilter.Items.Add("Escalated");
            _statusFilter.Items.Add("Forwarded to Repair");
            _statusFilter.Items.Add("Resolved (Temporary)");
            _statusFilter.Items.Add("Overdue");
            _statusFilter.Items.Add("Closed");
            _statusFilter.Items.Add("Solved");
            _statusFilter.Items.Add("Reopened");
            _statusFilter.SelectedIndex = 0;
            AddFilterField(filtersGrid, 1, "Status", _statusFilter);

            _assigneeFilter = CreateLookupComboBox();
            AddFilterField(filtersGrid, 2, "Assignee", _assigneeFilter);

            _branchFilter = CreateLookupComboBox();
            AddFilterField(filtersGrid, 3, "Branch", _branchFilter);

            // Grouping Top Buttons (Refresh + Create Ticket)
            var topActionWrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(18, 22, 0, 0) };
            _refreshTicketsButton = CreatePrimaryButton("Refresh", BrushFromRgb(13, 148, 136));
            
            var openCreateTicketButton = CreatePrimaryButton("+ New Ticket", BrushFromRgb(13, 148, 136)); // Standout color
            openCreateTicketButton.Margin = new Thickness(10, 0, 0, 8);
            openCreateTicketButton.Click += (_, __) =>
            {
                // Guard: prevent opening a second instance while one is already open
                if (_createWindowOpen) return;
                _createWindowOpen = true;
                var win = new Window
                {
                    Title = "Create Ticket",
                    Width = 750,
                    Height = 720,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = new ScrollViewer
                    {
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        Content = createTicketContainer
                    },
                    Background = BrushFromRgb(241, 244, 247)
                };
                win.Closed += (s, e) =>
                {
                    win.Content = null;
                    _createWindowOpen = false;
                };
                win.ShowDialog();
            };
            
            topActionWrap.Children.Add(_refreshTicketsButton);
            topActionWrap.Children.Add(openCreateTicketButton);
            Grid.SetColumn(topActionWrap, 4);
            filtersGrid.Children.Add(topActionWrap);
            
            filtersCard.Child = filtersGrid;

            // ==========================================
            // MAIN BODY: true 2-col × 3-row grid so left/right cards share row heights
            // ==========================================
            var bodyGrid = new Grid();
            Grid.SetRow(bodyGrid, 1);
            root.Children.Add(bodyGrid);

            // Columns: 60% Left | 40% Right
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Rows: Row 0 = Pending | Profile
            //       Row 1 = [Solved + Notes|History stacked] | Workflow
            bodyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            bodyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ── Row 0, Col 0: Pending Tickets card ──
            var pendingCard = CreateGlassCard();
            pendingCard.Margin = new Thickness(0, 0, 16, 16);
            pendingCard.VerticalAlignment = VerticalAlignment.Stretch;
            pendingCard.Child = BuildPendingSection(out _pendingGrid, out _pendingPager, out _pendingPrevButton, out _pendingNextButton, out _pendingStateText);
            Grid.SetRow(pendingCard, 0);
            Grid.SetColumn(pendingCard, 0);
            bodyGrid.Children.Add(pendingCard);

            // ── Row 0, Col 1: Ticket Profile card ──
            var profileCard = CreateGlassCard();
            profileCard.Margin = new Thickness(0, 0, 0, 16);
            profileCard.VerticalAlignment = VerticalAlignment.Stretch;
            profileCard.Child = BuildSelectionDetails(
                out _selectionHeadline, out _selectionSubhead, out _profileStatus,
                out _profilePriority, out _profileAssignee, out _profileCaller,
                out _profileLocation, out _profileActivity, out _profileCreated, out _profileEscalation,
                out _profileLastEmail, out _profileEscalationHost, out _profileLastEmailHost, out _detailsButton, out _summaryButton, out _deleteButton);
            Grid.SetRow(profileCard, 0);
            Grid.SetColumn(profileCard, 1);
            bodyGrid.Children.Add(profileCard);

            // ── Row 1, Col 0: sub-stack containing Solved card + Notes|History row ──
            // Using a StackPanel so Notes/History sit immediately below the Solved card
            // regardless of how tall the Workflow card is on the right.
            var leftBottomStack = new StackPanel();
            Grid.SetRow(leftBottomStack, 1);
            Grid.SetColumn(leftBottomStack, 0);
            bodyGrid.Children.Add(leftBottomStack);

            var solvedCard = CreateGlassCard();
            solvedCard.Margin = new Thickness(0, 0, 16, 16);
            solvedCard.Child = BuildSolvedSection(out _solvedGrid, out _solvedPager, out _solvedPrevButton, out _solvedNextButton, out _solvedStateText);
            leftBottomStack.Children.Add(solvedCard);

            var activityCard = CreateGlassCard();
            activityCard.Margin = new Thickness(0, 0, 16, 16);
            activityCard.Child = BuildActivityTimeline(out _activityList);
            leftBottomStack.Children.Add(activityCard);

            // ── Row 1, Col 1: Pending Case Details card ──
            var workflowCard = CreateGlassCard();
            workflowCard.Child = BuildPendingCaseDetailsSection(
                out _pendingCaseHeadline, out _pendingStatusCombo, out _pendingPriorityCombo,
                out _pendingAssigneeCombo, out _pendingProblemBox, out _pendingSolutionBox,
                out _pendingAssignToMeButton, out _pendingReassignButton, out _pendingUpdateButton,
                out _pendingReopenButton, out _pendingMarkAsButton, out _pendingFieldWorkButton, out _pendingReturnTempButton,
                out _pendingSyncSetButton, out _pendingEscalationOverrideButton);
            Grid.SetRow(workflowCard, 1);
            Grid.SetColumn(workflowCard, 1);
            bodyGrid.Children.Add(workflowCard);

            // Retain the established composer/workflow controls, but re-parent the visible
            // inspector surfaces into the focused three-pane team workbench.
            bodyGrid.Children.Remove(profileCard);
            bodyGrid.Children.Remove(workflowCard);
            leftBottomStack.Children.Remove(activityCard);
            DetachFromParent(_searchBox);
            DetachFromParent(_statusFilter);
            DetachFromParent(_assigneeFilter);
            DetachFromParent(_branchFilter);
            DetachFromParent(_refreshTicketsButton);

            Content = BuildTeamWorkbench(profileCard, activityCard, workflowCard, createTicketContainer);

            HookEvents();
            HookWorkbenchEvents();
            UpdateSelection(null);
        }

        public void Initialize(ICallMonitoringRepository repository)
        {
            _repository = repository;
            if (_initialized)
            {
                return;
            }

            _initialized = true;
        }

        public async Task LoadDataAsync()
        {
            if (_repository == null)
                return;

            // Thread-safe guard: only one load at a time
            if (System.Threading.Interlocked.CompareExchange(ref _loadingFlag, 1, 0) != 0)
                return;

            try
            {
                if (!await _repository.CallSchemaExistsAsync())
                {
                    SetGridState(_pendingGrid, _pendingStateText, "Call Monitoring schema is not installed. Install the ITCM database scripts, then refresh.", isError: true, showGrid: false);
                    SetGridState(_solvedGrid, _solvedStateText, "Call Monitoring schema is not installed. Reports and ticket history are unavailable until the schema exists.", isError: true, showGrid: false);
                    SetLifecycleState("Call Monitoring schema is not installed. Install the ITCM database scripts, then refresh.", isError: true);
                    UpdateSelection(null);
                    return;
                }

                _departments = await _repository.GetDepartmentsAsync() ?? new List<LookupItem>();
                var itDeptName = GuessItDepartmentName(_departments);
                _companies = await _repository.GetCompaniesAsync() ?? new List<LookupItem>();
                _assignees = await _repository.GetCallAssignmentCandidatesByDepartmentNameAsync(itDeptName) ?? new List<LookupItem>();
                _branches = await _repository.GetBranchesAsync() ?? new List<LookupItem>();
                _escalationSettings = await _repository.GetEscalationSettingsAsync();
                _overdueDays = await _repository.GetOverdueDaysAsync(3);

                PopulateLookupFilter(_assigneeFilter, _assignees, "(All)", includeUnassigned: true);
                PopulateLookupFilter(_branchFilter, _branches, "(All)", includeUnassigned: false);
                PopulateLookupCombo(_createCompany, _companies, "(Select company)", includeBlank: true);
                PopulateLookupCombo(_createDepartment, _departments, "(Select department)", includeBlank: true);
                PopulateLookupCombo(_createBranch, _branches, "(Select branch)", includeBlank: true);
                PopulateLookupCombo(_createAssignedTo, _assignees, "(Unassigned)", includeBlank: true);
                PopulateLookupCombo(_pendingAssigneeCombo, _assignees, "(Unassigned)", includeBlank: true);
                await ReloadCreateCallersAsync();

                await ReloadTicketsAsync(resetPending: false, resetSolved: false);
                ClearCreateTicketForm();
                UpdatePendingCaseDetails(null);
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _loadingFlag, 0);
            }
        }

        public async Task SelectTicketAsync(int ticketId)
        {
            if (ticketId <= 0)
            {
                return;
            }

            _searchBox.Text = string.Empty;
            _statusFilter.SelectedItem = "All";
            _pendingPageIndex = 1;
            _solvedPageIndex = 1;
            await ReloadTicketsAsync(resetPending: false, resetSolved: false, preferredTicketId: ticketId);
        }

        private void HookEvents()
        {
            _searchBox.TextChanged += async (_, __) =>
            {
                _pendingPageIndex = 1;
                _solvedPageIndex = 1;
                await ReloadTicketsAsync(resetPending: false, resetSolved: false);
            };

            _statusFilter.SelectionChanged += async (_, __) =>
            {
                _pendingPageIndex = 1;
                await ReloadTicketsAsync(resetPending: false, resetSolved: false);
            };

            _assigneeFilter.SelectionChanged += async (_, __) =>
            {
                _pendingPageIndex = 1;
                _solvedPageIndex = 1;
                await ReloadTicketsAsync(resetPending: false, resetSolved: false);
            };

            _branchFilter.SelectionChanged += async (_, __) =>
            {
                _pendingPageIndex = 1;
                _solvedPageIndex = 1;
                await ReloadTicketsAsync(resetPending: false, resetSolved: false);
            };

            _pendingGrid.SelectionChanged += async (_, __) =>
            {
                if (_pendingGrid.SelectedItem is PendingTicketRow row)
                {
                    _solvedGrid.SelectedItem = null;
                    await SelectTicketInternalAsync(row.Source);
                }
            };

            _solvedGrid.SelectionChanged += async (_, __) =>
            {
                if (_solvedGrid.SelectedItem is SolvedTicketRow row)
                {
                    _pendingGrid.SelectedItem = null;
                    await SelectTicketInternalAsync(row.Source);
                }
            };

            _pendingGrid.MouseDoubleClick += (_, __) => OpenDetails();
            _solvedGrid.MouseDoubleClick += (_, __) => OpenDetails();

            _pendingPrevButton.Click += async (_, __) =>
            {
                if (_pendingPageIndex <= 1) return;
                _pendingPageIndex--;
                await ReloadTicketsAsync(resetPending: false, resetSolved: false);
            };
            _pendingNextButton.Click += async (_, __) =>
            {
                if (!_pendingHasNext) return;
                _pendingPageIndex++;
                await ReloadTicketsAsync(resetPending: false, resetSolved: false);
            };
            _solvedPrevButton.Click += async (_, __) =>
            {
                if (_solvedPageIndex <= 1) return;
                _solvedPageIndex--;
                await ReloadTicketsAsync(resetPending: false, resetSolved: false);
            };
            _solvedNextButton.Click += async (_, __) =>
            {
                if (!_solvedHasNext) return;
                _solvedPageIndex++;
                await ReloadTicketsAsync(resetPending: false, resetSolved: false);
            };

            _detailsButton.Click += (_, __) => OpenDetails();
            _summaryButton.Click += (_, __) => OpenSummary();
            _deleteButton.Click += async (_, __) => await DeleteSelectedTicketAsync();
            _createTicketButton.Click += async (_, __) => await CreateTicketFromWorkspaceAsync();
            _clearCreateTicketButton.Click += (_, __) => ClearCreateTicketForm();
            _createEscalationButton.Click += async (_, __) => await EditCreateTicketEscalationAsync();
            _createEscalationClearButton.Click += (_, __) => ClearCreateTicketEscalation();
            _addCreateCallerButton.Click += async (_, __) => await QuickAddCreateCallerAsync();
            _refreshTicketsButton.Click += async (_, __) => await ReloadTicketsAsync(resetPending: false, resetSolved: false);
            _createCompany.SelectionChanged += async (_, __) =>
            {
                if (_newTicketOrgLockedFromEmployeeSearch) { UnlockNewTicketOrgFromEmployeeSearch(); return; }
                await ReloadCreateDepartmentsBranchesAndCallersAsync();
            };
            _createDepartment.SelectionChanged += async (_, __) =>
            {
                if (_newTicketOrgLockedFromEmployeeSearch) { UnlockNewTicketOrgFromEmployeeSearch(); return; }
                await ReloadCreateBranchesAndCallersAsync();
            };
            _createBranch.SelectionChanged += async (_, __) =>
            {
                if (_newTicketOrgLockedFromEmployeeSearch) { UnlockNewTicketOrgFromEmployeeSearch(); return; }
                await ReloadCreateCallersAsync();
            };

            // Caller search debounce + autocomplete
            _callerSearchDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _callerSearchDebounceTimer.Tick += async (_, __) =>
            {
                _callerSearchDebounceTimer.Stop();
                await SearchCallerEmployeeAsync();
            };

            _createCaller.Loaded += (_, __) =>
                _callerEditBox = _createCaller.Template?.FindName("PART_EditableTextBox", _createCaller) as TextBox;

            _createCaller.AddHandler(
                TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler(OnCallerTextChanged),
                handledEventsToo: true);

            _createCaller.SelectionChanged += async (_, __) =>
            {
                if (_loadingCreateLookups || _suppressCallerSearchTextChanged) return;
                var selected = _createCaller.SelectedItem as LookupItem;
                if (selected == null || selected.Id <= 0) return;
                await HandleCallerEmployeeSelectedAsync(selected.Id);
            };
            _pendingAssignToMeButton.Click += async (_, __) => await AssignSelectedTicketToMeAsync();
            _pendingReassignButton.Click += async (_, __) => await ReassignSelectedTicketAsync();
            _pendingUpdateButton.Click += async (_, __) => await UpdateSelectedTicketWorkflowAsync();
            _pendingReopenButton.Click += async (_, __) => await ReopenSelectedTicketAsync();
            _pendingMarkAsButton.Click += async (_, __) => await MarkSelectedTicketAsync();
            _pendingFieldWorkButton.Click += async (_, __) => await OpenFieldWorkForSelectedTicketAsync();
            _pendingReturnTempButton.Click += async (_, __) => await ReturnTemporaryReplacementAsync();
            _pendingSyncSetButton.Click += async (_, __) => await SyncSelectedTicketSetAsync();
            _pendingEscalationOverrideButton.Click += async (_, __) => await EditSelectedTicketEscalationOverrideAsync();
        }

        private async Task ReloadTicketsAsync(bool resetPending, bool resetSolved, int? preferredTicketId = null)
        {
            if (_repository == null)
            {
                return;
            }

            // The workbench uses one all-ticket lifecycle queue. The existing all-ticket
            // repository query is sufficient, so this does not require a schema migration.
            if (_caseCardsPanel != null)
            {
                await ReloadLifecycleQueueAsync(preferredTicketId);
                return;
            }

            if (resetPending) _pendingPageIndex = 1;
            if (resetSolved) _solvedPageIndex = 1;

            SetGridState(_pendingGrid, _pendingStateText, "Loading pending tickets...", isError: false, showGrid: true);
            SetGridState(_solvedGrid, _solvedStateText, "Loading resolved tickets...", isError: false, showGrid: true);

            var statusFilter = (_statusFilter.SelectedItem as string) ?? "All";
            var search = (_searchBox.Text ?? string.Empty).Trim();
            var assignee = _assigneeFilter.SelectedItem as LookupItem;
            var branch = _branchFilter.SelectedItem as LookupItem;

            string assigneeName = null;
            var unassignedOnly = false;
            if (assignee != null && assignee.Id != -1)
            {
                if (assignee.Id == 0) unassignedOnly = true;
                else assigneeName = assignee.Name?.Trim();
            }

            int? branchId = branch != null && branch.Id > 0 ? (int?)branch.Id : null;

            try
            {
                var pending = await _repository.GetPendingTicketsPageResultAsync(
                    statusFilter,
                    search,
                    assigneeName,
                    companyId: null,
                    branchId: branchId,
                    unassignedOnly: unassignedOnly,
                    overdueDays: _overdueDays,
                    pageIndex: _pendingPageIndex,
                    pageSize: 15);

                var solved = await _repository.GetRecentlyResolvedTicketsPageResultAsync(
                    search,
                    assigneeName,
                    companyId: null,
                    branchId: branchId,
                    unassignedOnly: unassignedOnly,
                    pageIndex: _solvedPageIndex,
                    pageSize: 5);

                _pendingHasNext = pending.HasNext;
                _solvedHasNext = solved.HasNext;
                _pendingTotalCount = pending.TotalCount;
                _solvedTotalCount = solved.TotalCount;

                // Fetch all escalation overrides for pending tickets in ONE query to avoid
                // N async awaits mid-loop that can cause ItemsControl inconsistency exceptions.
                var pendingItems = pending.Items ?? new List<CallTicketListItem>();
                var overrideMap = await _repository.GetEscalationOverridesForTicketsAsync(
                    pendingItems.Select(x => x.TicketId));

                _pendingRows = pendingItems
                    .Select(t => { overrideMap.TryGetValue(t.TicketId, out var ov); return CreatePendingRow(t, ov); })
                    .ToList();
                _solvedRows = (solved.Items ?? new List<CallTicketListItem>()).Select(CreateSolvedRow).ToList();

                _pendingGrid.ItemsSource = _pendingRows;
                _solvedGrid.ItemsSource = _solvedRows;

                UpdatePager(_pendingPager, _pendingPrevButton, _pendingNextButton, _pendingPageIndex, 15, _pendingTotalCount, _pendingHasNext);
                UpdatePager(_solvedPager, _solvedPrevButton, _solvedNextButton, _solvedPageIndex, 5, _solvedTotalCount, _solvedHasNext);
                SetGridState(
                    _pendingGrid,
                    _pendingStateText,
                    HasActiveTicketFilter(statusFilter, search, assignee, branch)
                        ? "No pending tickets match the current filters."
                        : "No pending tickets need attention right now.",
                    isError: false,
                    showGrid: _pendingRows.Count > 0);
                SetGridState(
                    _solvedGrid,
                    _solvedStateText,
                    HasActiveTicketFilter(statusFilter, search, assignee, branch)
                        ? "No resolved tickets match the current filters."
                        : "No recently resolved tickets to show.",
                    isError: false,
                    showGrid: _solvedRows.Count > 0);
            }
            catch
            {
                _pendingRows = new List<PendingTicketRow>();
                _solvedRows = new List<SolvedTicketRow>();
                _pendingGrid.ItemsSource = _pendingRows;
                _solvedGrid.ItemsSource = _solvedRows;
                _pendingHasNext = false;
                _solvedHasNext = false;
                _pendingTotalCount = 0;
                _solvedTotalCount = 0;
                UpdatePager(_pendingPager, _pendingPrevButton, _pendingNextButton, _pendingPageIndex, 15, 0, false);
                UpdatePager(_solvedPager, _solvedPrevButton, _solvedNextButton, _solvedPageIndex, 5, 0, false);
                SetGridState(_pendingGrid, _pendingStateText, "Unable to load pending tickets. Refresh and try again.", isError: true, showGrid: false);
                SetGridState(_solvedGrid, _solvedStateText, "Unable to load resolved tickets. Refresh and try again.", isError: true, showGrid: false);
                UpdateSelection(null);
                return;
            }

            if (preferredTicketId.HasValue)
            {
                var pendingMatch = _pendingRows.FirstOrDefault(x => x.Source.TicketId == preferredTicketId.Value);
                if (pendingMatch != null)
                {
                    _pendingGrid.SelectedItem = pendingMatch;
                    _pendingGrid.ScrollIntoView(pendingMatch);
                    return;
                }

                var solvedMatch = _solvedRows.FirstOrDefault(x => x.Source.TicketId == preferredTicketId.Value);
                if (solvedMatch != null)
                {
                    _solvedGrid.SelectedItem = solvedMatch;
                    _solvedGrid.ScrollIntoView(solvedMatch);
                    return;
                }
            }

            if (_selectedTicket != null)
            {
                var existing = _pendingRows.FirstOrDefault(x => x.Source.TicketId == _selectedTicket.TicketId);
                if (existing != null)
                {
                    _pendingGrid.SelectedItem = existing;
                    return;
                }

                var solvedExisting = _solvedRows.FirstOrDefault(x => x.Source.TicketId == _selectedTicket.TicketId);
                if (solvedExisting != null)
                {
                    _solvedGrid.SelectedItem = solvedExisting;
                    return;
                }
            }

            if (_pendingRows.Count > 0)
            {
                _pendingGrid.SelectedItem = _pendingRows[0];
                return;
            }

            if (_solvedRows.Count > 0)
            {
                _solvedGrid.SelectedItem = _solvedRows[0];
                return;
            }

            UpdateSelection(null);
        }

        private async Task SelectTicketInternalAsync(CallTicketListItem ticket)
        {
            _selectedTicket = ticket;
            UpdateSelection(ticket);

            if (ticket == null || _repository == null)
            {
                return;
            }

            try
            {
                _activityList.ItemsSource = new[]
                {
                    ActivityTimelineItem.SystemMessage("Loading activity", "Loading notes and ticket history...")
                };
                var notes = await _repository.GetTicketNotesAsync(ticket.TicketId);
                var history = await _repository.GetTicketHistoryAsync(ticket.TicketId);
                var escalation = await _repository.GetTicketEscalationOverrideAsync(ticket.TicketId);
                var lastEmail = await _repository.GetLastEmailLogForTicketAsync(ticket.TicketId);

                _activityList.ItemsSource = BuildActivityItems(notes, history);

                _profileEscalation.Text = BuildEscalationLabel(ticket, _escalationSettings, escalation);
                _profileEscalationHost.Visibility = IsUsefulInspectorValue(_profileEscalation.Text) ? Visibility.Visible : Visibility.Collapsed;
                _profileLastEmail.Text = FormatLastEmail(lastEmail);
                _profileLastEmailHost.Visibility = IsUsefulInspectorValue(_profileLastEmail.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
            catch
            {
                _activityList.ItemsSource = new[]
                {
                    ActivityTimelineItem.SystemMessage("Activity unavailable", "Unable to load notes and history for this ticket.")
                };
            }
        }

        private void UpdateSelection(CallTicketListItem ticket)
        {
            if (ticket == null)
            {
                _selectedTicket = null;
                _selectionHeadline.Text = "No ticket selected";
                _selectionSubhead.Text = "Select a lifecycle ticket to load its summary, activity, and workflow.";
                _inspectorTicketCode.Text = "No ticket selected";
                _inspectorIssue.Text = "Select a case from the list to inspect it.";
                _inspectorStatus.Text = "-";
                _profileStatus.Text = "-";
                _profilePriority.Text = "-";
                ApplyProfileStatusStyle(_profileStatus, "-");
                ApplyProfilePriorityStyle(_profilePriority, "-");
                _profileAssignee.Text = "-";
                _profileCaller.Text = "-";
                _profileLocation.Text = "-";
                _profileActivity.Text = "-";
                _profileCreated.Text = "-";
                _profileActivity.Foreground = BrushFromRgb(15, 23, 42);
                _profileEscalation.Text = "-";
                _profileLastEmail.Text = "-";
                _profileEscalationHost.Visibility = Visibility.Collapsed;
                _profileLastEmailHost.Visibility = Visibility.Collapsed;
                _activityList.ItemsSource = new[]
                {
                    ActivityTimelineItem.SystemMessage("Select a ticket", "Activity, notes, and status changes will appear here.")
                };
                _detailsButton.IsEnabled = false;
                _summaryButton.IsEnabled = false;
                _deleteButton.IsEnabled = false;
                UpdatePendingCaseDetails(null);
                RefreshCaseCards();
                return;
            }

            var overdue = IsTicketOverdue(ticket);
            var status = overdue ? "Overdue" : NormalizeLegacyTicketStatus(ticket.Status);
            var priority = string.IsNullOrWhiteSpace(ticket.Priority) ? "-" : ticket.Priority.Trim();
            var activityUtc = GetLastActivityUtc(ticket, out var activitySource);
            var idle = DateTime.UtcNow - activityUtc;
            if (idle < TimeSpan.Zero) idle = TimeSpan.Zero;

            _selectionHeadline.Text = ticket.TicketCode ?? ticket.TicketId.ToString();
            _selectionSubhead.Text = ticket.Issue ?? "No issue provided";
            _inspectorTicketCode.Text = ticket.TicketCode ?? ticket.TicketId.ToString();
            _inspectorIssue.Text = ticket.Issue ?? "No issue provided";
            _inspectorStatus.Text = status;
            _profileStatus.Text = status;
            _profilePriority.Text = priority;
            ApplyProfileStatusStyle(_profileStatus, status);
            ApplyProfilePriorityStyle(_profilePriority, priority);
            _profileAssignee.Text = string.IsNullOrWhiteSpace(ticket.ResponsiblePerson) ? "(Unassigned)" : ticket.ResponsiblePerson.Trim();
            _profileCaller.Text = string.IsNullOrWhiteSpace(ticket.CallerName) ? "-" : ticket.CallerName.Trim();
            _profileLocation.Text = (ticket.Department ?? "-") + " • " + (ticket.Branch ?? "-");
            _profileActivity.Text = AppTime.ToLocalString(activityUtc, "g") + " • " + FormatShortDuration(idle) + " idle (" + activitySource + ")";
            _profileActivity.Foreground = GetMetricForeground(GetIdleState(idle));
            _profileCreated.Text = ticket.CreatedAt == default(DateTime) ? "-" : AppTime.ToLocalString(ticket.CreatedAt, "g");
            _profileEscalation.Text = BuildEscalationLabel(ticket, _escalationSettings, null);
            _profileEscalationHost.Visibility = IsUsefulInspectorValue(_profileEscalation.Text) ? Visibility.Visible : Visibility.Collapsed;
            _profileLastEmail.Text = "Loading...";
            _profileLastEmailHost.Visibility = Visibility.Collapsed;
            _detailsButton.IsEnabled = true;
            _summaryButton.IsEnabled = true;
            _deleteButton.IsEnabled = AppSession.IsLoggedIn && (AppSession.IsAdmin || AppSession.IsDeveloper);
            UpdatePendingCaseDetails(ticket);
            RefreshCaseCards();
        }

        private static bool IsUsefulInspectorValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = value.Trim();
            return normalized != "-"
                && !normalized.Equals("None", StringComparison.OrdinalIgnoreCase)
                && !normalized.Equals("No email logged", StringComparison.OrdinalIgnoreCase)
                && !normalized.Equals("No escalation", StringComparison.OrdinalIgnoreCase)
                && !normalized.Equals("Not configured", StringComparison.OrdinalIgnoreCase);
        }

        private void OpenDetails()
        {
            if (_selectedTicket == null || _selectedTicket.TicketId <= 0 || _repository == null)
            {
                return;
            }

            var dlg = new WpfTicketDetailsDialog(_repository, _selectedTicket.TicketId)
            {
                Owner = Window.GetWindow(this)
            };
            dlg.ShowDialog();
            HandleTicketDetailsAction(dlg.RequestedAction);

            ReloadTicketsAsync(false, false, _selectedTicket.TicketId).FireAndForget(ex => System.Diagnostics.Debug.WriteLine("[Tickets] Reload after details failed: " + ex));
        }

        private void HandleTicketDetailsAction(TicketDetailsDialog.TicketDetailAction action)
        {
            if (action == TicketDetailsDialog.TicketDetailAction.None || _selectedTicket == null)
                return;

            if (action == TicketDetailsDialog.TicketDetailAction.OpenEmailLog)
            {
                var dialog = new WpfEmailLogShortcutDialog(_repository, _selectedTicket.TicketId, _selectedTicket.TicketCode)
                {
                    Owner = Window.GetWindow(this)
                };
                dialog.ShowDialog();
                return;
            }

            if (action == TicketDetailsDialog.TicketDetailAction.Reassign)
            {
                ReassignSelectedTicketWithPickerAsync().FireAndForget(ex => WpfItcmDialogService.ShowError(this, ex.Message, "Reassign"));
                return;
            }

            if (action == TicketDetailsDialog.TicketDetailAction.MarkAs)
            {
                MarkSelectedTicketAsync().FireAndForget(ex => WpfItcmDialogService.ShowError(this, ex.Message, "Mark As"));
                return;
            }

            if (action == TicketDetailsDialog.TicketDetailAction.Reopen)
            {
                ReopenSelectedTicketAsync().FireAndForget(ex => WpfItcmDialogService.ShowError(this, ex.Message, "Reopen"));
            }
        }

        private void OpenSummary()
        {
            if (_selectedTicket == null)
            {
                return;
            }

            var sla = BuildSlaCell(_selectedTicket);
            var activityUtc = GetLastActivityUtc(_selectedTicket, out var source);
            var idle = DateTime.UtcNow - activityUtc;
            if (idle < TimeSpan.Zero) idle = TimeSpan.Zero;

            var dlg = new WpfTicketSummaryDialog(
                _selectedTicket,
                sla.Label,
                sla.Tooltip,
                _profileEscalation.Text,
                AppTime.ToLocalString(activityUtc, "g") + " (" + source + ")",
                FormatShortDuration(idle))
            {
                Owner = Window.GetWindow(this)
            };
            dlg.ShowDialog();
        }

        private async Task DeleteSelectedTicketAsync()
        {
            if (_selectedTicket == null || _selectedTicket.TicketId <= 0 || _repository == null)
            {
                return;
            }

            if (!AppSession.IsLoggedIn || !(AppSession.IsAdmin || AppSession.IsDeveloper))
            {
                WpfItcmDialogService.ShowWarning(this, "You do not have permission to delete tickets.", "Delete Ticket");
                return;
            }

            var ticketLabel = string.IsNullOrWhiteSpace(_selectedTicket.TicketCode) ? _selectedTicket.TicketId.ToString() : _selectedTicket.TicketCode.Trim();
            var confirmation = WpfItcmDialogService.Confirm(
                this,
                "Delete ticket " + ticketLabel + " permanently?",
                "Delete Ticket",
                "Delete",
                destructive: true);
            if (!confirmation)
            {
                return;
            }

            await _repository.DeleteTicketAsync(_selectedTicket.TicketId, AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null);
            _selectedTicket = null;
            await ReloadTicketsAsync(false, false);
        }

        private void OpenLegacyTicketComposer()
        {
            if (_repository == null)
            {
                return;
            }

            using (var form = new WinForms.Form
            {
                Text = "Create or Manage Tickets",
                StartPosition = WinForms.FormStartPosition.CenterParent,
                Size = new System.Drawing.Size(1480, 920),
                MinimumSize = new System.Drawing.Size(1320, 820)
            })
            {
                var control = new TicketManagementControl(_repository, new CallEmailNotificationService(_repository))
                {
                    Dock = WinForms.DockStyle.Fill
                };
                form.Controls.Add(control);
                form.Shown += async (_, __) => await control.LoadDataAsync();
                form.ShowDialog(WinForms.Form.ActiveForm);
            }

            ReloadTicketsAsync(resetPending: false, resetSolved: false).FireAndForget(ex => System.Diagnostics.Debug.WriteLine("[Tickets] Reload after composer failed: " + ex));
        }

        private async Task ReloadCreateDepartmentsBranchesAndCallersAsync()
        {
            if (_repository == null || _loadingCreateLookups)
            {
                return;
            }

            try
            {
                _loadingCreateLookups = true;
                var companyId = GetLookupId(_createCompany);
                var departments = companyId.HasValue && companyId.Value > 0
                    ? await _repository.GetDepartmentsAsync(companyId)
                    : _departments;
                PopulateLookupCombo(_createDepartment, departments, "(Select department)", includeBlank: true);

                var branches = await _repository.GetBranchesAsync(companyId, null);
                PopulateLookupCombo(_createBranch, branches, "(Select branch)", includeBlank: true);
            }
            finally
            {
                _loadingCreateLookups = false;
            }

            await ReloadCreateCallersAsync();
        }

        private async Task ReloadCreateBranchesAndCallersAsync()
        {
            if (_repository == null || _loadingCreateLookups)
            {
                return;
            }

            try
            {
                _loadingCreateLookups = true;
                var branches = await _repository.GetBranchesAsync(GetLookupId(_createCompany), GetLookupId(_createDepartment));
                PopulateLookupCombo(_createBranch, branches, "(Select branch)", includeBlank: true);
            }
            finally
            {
                _loadingCreateLookups = false;
            }

            await ReloadCreateCallersAsync();
        }

        private async Task ReloadCreateCallersAsync()
        {
            if (_loadingCreateLookups || _newTicketOrgLockedFromEmployeeSearch || _repository == null)
                return;

            var callers = await _repository.GetEmployeesByDeptAndBranchAsync(
                GetLookupId(_createCompany),
                GetLookupId(_createDepartment),
                GetLookupId(_createBranch)) ?? new List<LookupItem>();

            _suppressCallerSearchTextChanged = true;
            _createCaller.ItemsSource = callers;
            _createCaller.SelectedIndex = -1;
            _createCaller.IsDropDownOpen = false;
            _suppressCallerSearchTextChanged = false;
        }

        private void OnCallerTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_createCaller == null || _suppressCallerSearchTextChanged)
                return;

            var text = _createCaller.Text ?? string.Empty;
            if (text.Length < 2)
            {
                _callerSearchDebounceTimer.Stop();
                _createCaller.IsDropDownOpen = false;
                if (_newTicketOrgLockedFromEmployeeSearch)
                    UnlockNewTicketOrgFromEmployeeSearch();
                return;
            }

            _createStatusText.Text = "Searching...";
            _createStatusText.Foreground = BrushFromRgb(100, 116, 139);
            _callerSearchDebounceTimer.Stop();
            _callerSearchDebounceTimer.Start();
        }

        private async Task SearchCallerEmployeeAsync()
        {
            if (_repository == null || _createCaller == null || _newTicketOrgLockedFromEmployeeSearch)
                return;

            var text = (_createCaller.Text ?? string.Empty).Trim();
            if (text.Length < 2)
                return;

            try
            {
                var employees = await _repository.SearchEmployeesByNameAsync(text);
                _suppressCallerSearchTextChanged = true;
                _createCaller.ItemsSource = employees;
                _createCaller.IsDropDownOpen = employees.Count > 0;
                _ = Dispatcher.BeginInvoke(new Action(() =>
                {
                    var eb = _callerEditBox
                          ?? _createCaller.Template?.FindName("PART_EditableTextBox", _createCaller) as TextBox;
                    if (eb != null) { eb.CaretIndex = eb.Text.Length; eb.SelectionLength = 0; }
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
                _createStatusText.Text = employees.Count > 0
                    ? $"{employees.Count} employee(s) found."
                    : "No employees matched the search.";
                _createStatusText.Foreground = BrushFromRgb(100, 116, 139);
            }
            catch
            {
                // Silently fail; user can still type freely
            }
            finally
            {
                _suppressCallerSearchTextChanged = false;
            }
        }

        private async Task HandleCallerEmployeeSelectedAsync(int empId)
        {
            if (empId <= 0 || _repository == null)
                return;

            try
            {
                _loadingCreateLookups = true;

                var org = await _repository.GetEmployeeOrgInfoByEmpIdAsync(empId);

                if (org?.ComId.HasValue == true && org.ComId.Value > 0)
                    SelectLookupItem(_createCompany, org.ComId.Value);

                await ReloadCreateDepartmentsBranchesAndCallersAsync();
                if (org?.DeptId.HasValue == true && org.DeptId.Value > 0)
                    SelectLookupItem(_createDepartment, org.DeptId.Value);

                await ReloadCreateBranchesAndCallersAsync();
                if (org?.BranchId.HasValue == true && org.BranchId.Value > 0)
                    SelectLookupItem(_createBranch, org.BranchId.Value);

                _newTicketOrgLockedFromEmployeeSearch = true;
                SetNewTicketOrgDropdownsEnabled(false);
                _callerSearchDebounceTimer.Stop();

                _suppressCallerSearchTextChanged = true;
                SelectLookupItem(_createCaller, empId);
                _createCaller.IsDropDownOpen = false;
                _suppressCallerSearchTextChanged = false;
            }
            catch { }
            finally
            {
                _loadingCreateLookups = false;
            }
        }

        private void UnlockNewTicketOrgFromEmployeeSearch()
        {
            if (!_newTicketOrgLockedFromEmployeeSearch)
                return;

            _newTicketOrgLockedFromEmployeeSearch = false;
            SetNewTicketOrgDropdownsEnabled(true);
            _callerSearchDebounceTimer.Stop();

            _suppressCallerSearchTextChanged = true;
            _createCaller.ItemsSource = null;
            _createCaller.SelectedIndex = -1;
            _createCaller.Text = string.Empty;
            _createCaller.IsDropDownOpen = false;
            _suppressCallerSearchTextChanged = false;
        }

        private void SetNewTicketOrgDropdownsEnabled(bool enabled)
        {
            if (_createCompany != null) _createCompany.IsEnabled = enabled;
            if (_createDepartment != null) _createDepartment.IsEnabled = enabled;
            if (_createBranch != null) _createBranch.IsEnabled = enabled;
        }

        private async Task QuickAddCreateCallerAsync()
        {
            using (var dialog = new QuickAddEmployeeDialog())
            {
                var owner = WinForms.Form.ActiveForm;
                var result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
                if (result != WinForms.DialogResult.OK)
                {
                    return;
                }

                if (dialog.NewCompanyId.HasValue && dialog.NewCompanyId.Value > 0)
                {
                    SelectLookupItem(_createCompany, dialog.NewCompanyId.Value);
                    await ReloadCreateDepartmentsBranchesAndCallersAsync();
                }

                if (dialog.NewDepartmentId.HasValue && dialog.NewDepartmentId.Value > 0)
                {
                    SelectLookupItem(_createDepartment, dialog.NewDepartmentId.Value);
                    await ReloadCreateBranchesAndCallersAsync();
                }

                if (dialog.NewBranchId.HasValue && dialog.NewBranchId.Value > 0)
                {
                    SelectLookupItem(_createBranch, dialog.NewBranchId.Value);
                }

                await ReloadCreateCallersAsync();

                if (dialog.NewEmployeeId.HasValue && dialog.NewEmployeeId.Value > 0)
                {
                    _suppressCallerSearchTextChanged = true;
                    SelectLookupItem(_createCaller, dialog.NewEmployeeId.Value);
                    _suppressCallerSearchTextChanged = false;
                }
                else if (_createCaller.SelectedItem == null && !string.IsNullOrWhiteSpace(dialog.NewEmployeeName))
                {
                    _suppressCallerSearchTextChanged = true;
                    _createCaller.Text = dialog.NewEmployeeName.Trim();
                    _suppressCallerSearchTextChanged = false;
                }

                _createStatusText.Text = "Employee added. Review the caller details, then continue creating the ticket.";
                _createStatusText.Foreground = BrushFromRgb(22, 101, 52);
            }
        }

        private async Task CreateTicketFromWorkspaceAsync()
        {
            if (_repository == null)
            {
                return;
            }

            var companyId    = GetLookupId(_createCompany);
            var departmentId = GetLookupId(_createDepartment);
            var branchId     = GetLookupId(_createBranch);
            var assignedToId = GetLookupId(_createAssignedTo);
            var callerEmployeeId = GetLookupId(_createCaller);
            var caller       = ResolveCallerText();
            var issue        = (_createIssueBox.Text ?? string.Empty).Trim();
            var notes        = (_createNotesBox.Text ?? string.Empty).Trim();

            var hasDepartment = departmentId.HasValue && departmentId.Value > 0;
            var hasBranch = branchId.HasValue && branchId.Value > 0;
            if (!companyId.HasValue || (!hasDepartment && !hasBranch) || string.IsNullOrWhiteSpace(caller) || string.IsNullOrWhiteSpace(issue))
            {
                _createStatusText.Text = "Company, a department or branch, caller, and technical problem are required.";
                _createStatusText.Foreground = BrushFromRgb(220, 38, 38);
                return;
            }

            if (!AppSession.IsLoggedIn || AppSession.CurrentUserId <= 0)
            {
                _createStatusText.Text = "You must be logged in to create a ticket.";
                _createStatusText.Foreground = BrushFromRgb(220, 38, 38);
                return;
            }

            if (!await _repository.CanStoreTicketContactEmailAsync())
            {
                _createStatusText.Text = "Ticket contact email storage is not installed. Apply the ContactEmail migration before creating tickets.";
                _createStatusText.Foreground = BrushFromRgb(220, 38, 38);
                return;
            }

            var ticketContact = await _repository.ResolveTicketContactEmailAsync(
                callerEmployeeId,
                companyId,
                departmentId,
                branchId);
            var requiresCallerEmail = callerEmployeeId.HasValue
                && !string.Equals(ticketContact?.Source, "Employee personal email", StringComparison.Ordinal);

            // --- Resolve backdate (if enabled) ---
            DateTime? backlogUtc = null;
            if (_backdateCheck.IsChecked == true)
            {
                var date = _backdateDatePicker.SelectedDate ?? DateTime.Today;
                if (!TimeSpan.TryParse(_backdateTimeText.Text, out var time))
                {
                    _createStatusText.Text = "Invalid backdate time. Use HH:mm format (e.g., 14:30).";
                    _createStatusText.Foreground = BrushFromRgb(220, 38, 38);
                    return;
                }
                var local = date.Date + time;
                if (local > DateTime.Now.AddMinutes(1))
                {
                    _createStatusText.Text = "Backdate cannot be in the future.";
                    _createStatusText.Foreground = BrushFromRgb(220, 38, 38);
                    return;
                }
                backlogUtc = DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
            }
            // ---

            var companyName  = (_createCompany.SelectedItem   as LookupItem)?.Name ?? "-";
            var deptName     = (_createDepartment.SelectedItem as LookupItem)?.Name ?? "-";
            var branchName   = (_createBranch.SelectedItem    as LookupItem)?.Name ?? "(None)";
            var assignedName = (_createAssignedTo.SelectedItem as LookupItem)?.Name ?? "(Unassigned)";
            var suggestedPriority = WpfConfirmTicketCreationDialog.SuggestPriority(issue, out var priorityReason);
            var recentWarning = await BuildRecentOpenTicketWarningAsync(caller);
            WpfConfirmTicketCreationDialog.CreationAction createAction;
            string selectedPriority;

            var dlg = new WpfConfirmTicketCreationDialog(new WpfConfirmTicketCreationDialog.TicketCreationModel
            {
                Company = companyName,
                Department = deptName,
                Branch = branchName,
                Caller = caller,
                AssignedTo = assignedName,
                TicketContactEmail = ticketContact?.Email,
                TicketContactEmailSource = ticketContact?.Source,
                RequiresCallerEmail = requiresCallerEmail,
                CanLinkTicketContactEmailToCaller = requiresCallerEmail && callerEmployeeId.HasValue,
                RequiresTemporaryTicketContactEmail = !requiresCallerEmail && (ticketContact == null || !ticketContact.HasEmail),
                Priority = "Medium",
                SuggestedPriority = suggestedPriority,
                PrioritySuggestionReason = priorityReason,
                RecentOpenTicketWarning = recentWarning,
                EscalationSummary = GetCreateTicketEscalationSummaryForConfirm(),
                EscalationReason = _createTicketEscReason,
                Issue = issue,
                InitialNotes = string.IsNullOrWhiteSpace(notes) ? "(None)" : notes,
                BackdateDisplay = backlogUtc.HasValue ? backlogUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : null
            })
            {
                Owner = Window.GetWindow(this)
            };
            if (dlg.ShowDialog() != true)
                return;
            createAction = dlg.SelectedAction;
            selectedPriority = dlg.SelectedPriority;
            var selectedTicketContactEmail = dlg.SelectedTicketContactEmail;
            var shouldLinkTicketContactEmailToCaller = dlg.ShouldLinkTicketContactEmailToCaller;

            try
            {
                _createTicketButton.IsEnabled = false;
                _createStatusText.Text = "Creating ticket...";
                _createStatusText.Foreground = BrushFromRgb(13, 148, 136);

                var created = await _repository.CreateTicketAsync(
                    companyId,
                    departmentId,
                    branchId,
                    caller,
                    issue,
                    string.IsNullOrWhiteSpace(notes) ? null : notes,
                    null,
                    selectedPriority,
                    assignedToId,
                    AppSession.CurrentUserId,
                    backlogUtc);

                if (created == null || created.TicketId <= 0)
                    throw new InvalidOperationException("Ticket creation did not return a valid ticket.");

                try
                {
                    await _repository.SetTicketContactEmailAsync(created.TicketId, selectedTicketContactEmail);
                }
                catch (Exception ex)
                {
                    var ticketLabel = string.IsNullOrWhiteSpace(created.TicketCode)
                        ? $"#{created.TicketId}"
                        : created.TicketCode.Trim();
                    throw new InvalidOperationException(
                        $"Ticket {ticketLabel} was created, but its required ticket contact email could not be saved. No lifecycle notification was sent.",
                        ex);
                }

                if (shouldLinkTicketContactEmailToCaller)
                {
                    if (!callerEmployeeId.HasValue || callerEmployeeId.Value <= 0)
                        throw new InvalidOperationException("The ticket contact was saved, but no caller employee profile was selected to link the email.");

                    try
                    {
                        await _repository.SaveEmployeeEmailBindingAsync(
                            callerEmployeeId.Value,
                            selectedTicketContactEmail,
                            AppSession.CurrentUserId);
                    }
                    catch (Exception ex)
                    {
                        var ticketLabel = string.IsNullOrWhiteSpace(created.TicketCode)
                            ? $"#{created.TicketId}"
                            : created.TicketCode.Trim();
                        throw new InvalidOperationException(
                            $"Ticket {ticketLabel} was created and its contact email was saved, but the caller's employee email could not be linked. No lifecycle notification was sent.",
                            ex);
                    }
                }

                // Apply escalation override if configured for this new ticket
                if (created != null && created.TicketId > 0
                    && _createTicketEscDaysToSupervisor.HasValue && _createTicketEscDaysToManager.HasValue)
                {
            try
            {
                // Overrides are meaningless on final tickets (the proc throws
                // 50023) - stop before opening the form.
                var currentStatus = (_selectedTicket.Status ?? string.Empty).Trim();
                if (currentStatus.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                    || currentStatus.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                    || currentStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase))
                {
                    WpfItcmDialogService.ShowInfo(this, "Escalation overrides cannot be changed after a ticket is solved.", "Escalation Override");
                    return;
                }

                if (!await _repository.EscalationOverridesEnabledAsync())
                        {
                            _createStatusText.Text = "Ticket created, but escalation overrides are not installed.";
                            _createStatusText.Foreground = BrushFromRgb(230, 126, 34);
                        }
                        else
                        {
                            await _repository.SetTicketEscalationOverrideAsync(
                                created.TicketId,
                                _createTicketEscDaysToSupervisor,
                                _createTicketEscDaysToManager,
                                _createTicketEscReason,
                                AppSession.CurrentUserId,
                                clear: false);
                        }
                    }
                    catch (Exception exEsc)
                    {
                        _createStatusText.Text = "Ticket created, but failed to set escalation override.\n" + exEsc.Message;
                        _createStatusText.Foreground = BrushFromRgb(230, 126, 34);
                    }
                }

                ClearCreateTicketForm();
                _createStatusText.Text = created != null && !string.IsNullOrWhiteSpace(created.TicketCode)
                    ? "Created " + created.TicketCode + " successfully."
                    : "Ticket created successfully.";
                _createStatusText.Foreground = BrushFromRgb(22, 101, 52);
                await ReloadTicketsAsync(true, false, created?.TicketId);
                if (created != null && created.TicketId > 0 && createAction == WpfConfirmTicketCreationDialog.CreationAction.CreateAndOpen)
                {
                    OpenDetails();
                }
                else if (createAction == WpfConfirmTicketCreationDialog.CreationAction.CreateAnother)
                {
                    _createCaller.Focus();
                }
            }
            catch (Exception ex)
            {
                _createStatusText.Text = ex.Message;
                _createStatusText.Foreground = BrushFromRgb(220, 38, 38);
            }
            finally
            {
                _createTicketButton.IsEnabled = true;
            }
        }

        private async Task<string> BuildRecentOpenTicketWarningAsync(string caller)
        {
            if (_repository == null || string.IsNullOrWhiteSpace(caller))
                return null;

            try
            {
                var trimmedCaller = caller.Trim();
                var rows = await _repository.GetPendingTicketsPageAsync(
                    "All",
                    trimmedCaller,
                    null,
                    null,
                    null,
                    false,
                    _overdueDays,
                    1,
                    5);

                rows = (rows ?? new List<CallTicketListItem>())
                    .Where(t => t != null
                        && !string.IsNullOrWhiteSpace(t.CallerName)
                        && t.CallerName.IndexOf(trimmedCaller, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(3)
                    .ToList();

                if (rows.Count == 0)
                    return null;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Caller has {rows.Count} recent/open matching ticket(s). Review before creating a duplicate:");
                foreach (var t in rows)
                    sb.AppendLine("- " + (t.TicketCode ?? t.TicketId.ToString()) + ": " + (t.Issue ?? "-"));
                return sb.ToString();
            }
            catch
            {
                return null;
            }
        }

        private void ClearCreateTicketForm()
        {
            if (_newTicketOrgLockedFromEmployeeSearch)
                UnlockNewTicketOrgFromEmployeeSearch();

            if (_createCompany.Items.Count > 0) _createCompany.SelectedIndex = 0;
            if (_createDepartment.Items.Count > 0) _createDepartment.SelectedIndex = 0;
            if (_createBranch.Items.Count > 0) _createBranch.SelectedIndex = 0;
            if (_createAssignedTo.Items.Count > 0) _createAssignedTo.SelectedIndex = 0;
            _suppressCallerSearchTextChanged = true;
            _createCaller.ItemsSource = null;
            _createCaller.SelectedIndex = -1;
            _createCaller.Text = string.Empty;
            _createCaller.IsDropDownOpen = false;
            _suppressCallerSearchTextChanged = false;
            _createIssueBox.Text = string.Empty;
            _createNotesBox.Text = string.Empty;
            _createStatusText.Text = "Ready to create a new ticket.";
            _createStatusText.Foreground = BrushFromRgb(100, 116, 139);

            // Reset backdate controls
            _backdateCheck.IsChecked = false;
            _backdateDatePicker.SelectedDate = DateTime.Today;
            _backdateTimeText.Text = DateTime.Now.ToString("HH:mm");
            _backdateDatePicker.IsEnabled = false;
            _backdateTimeText.IsEnabled = false;

            // Reset escalation override
            ClearCreateTicketEscalation();
        }

        private void ClearCreateTicketEscalation()
        {
            _createTicketEscDaysToSupervisor = null;
            _createTicketEscDaysToManager = null;
            _createTicketEscReason = null;
            UpdateCreateTicketEscalationUi();
        }

        private void UpdateCreateTicketEscalationUi()
        {
            if (_createEscalationSummary == null || _createEscalationButton == null || _createEscalationClearButton == null)
                return;

            var defSup = Math.Max(1, _escalationSettings?.DaysToSupervisor ?? 2);
            var defMgr = Math.Max(defSup, _escalationSettings?.DaysToManager ?? 3);

            var hasCustom = _createTicketEscDaysToSupervisor.HasValue && _createTicketEscDaysToManager.HasValue;
            if (hasCustom)
            {
                _createEscalationSummary.Text = $"Custom: Supervisor {_createTicketEscDaysToSupervisor.Value} day(s), Manager {_createTicketEscDaysToManager.Value} day(s)";
                _createEscalationButton.Content = "Edit escalation";
                _createEscalationClearButton.Visibility = Visibility.Visible;
            }
            else
            {
                _createEscalationSummary.Text = $"Default: Supervisor {defSup} day(s), Manager {defMgr} day(s)";
                _createEscalationButton.Content = "Set escalation";
                _createEscalationClearButton.Visibility = Visibility.Collapsed;
            }
        }

        private string GetCreateTicketEscalationSummaryForConfirm()
        {
            var defSup = Math.Max(1, _escalationSettings?.DaysToSupervisor ?? 2);
            var defMgr = Math.Max(defSup, _escalationSettings?.DaysToManager ?? 3);

            if (_createTicketEscDaysToSupervisor.HasValue && _createTicketEscDaysToManager.HasValue)
                return $"Custom (Sup {_createTicketEscDaysToSupervisor.Value}d, Mgr {_createTicketEscDaysToManager.Value}d)";

            return $"Default (Sup {defSup}d, Mgr {defMgr}d)";
        }

        private async Task EditCreateTicketEscalationAsync()
        {
            try
            {
                if (!await _repository.EscalationOverridesEnabledAsync())
                {
                    WpfItcmDialogService.ShowInfo(
                        this,
                        "Escalation overrides are not installed in this database yet.",
                        "Set Escalation");
                    return;
                }

                var settings = _escalationSettings ?? await _repository.GetEscalationSettingsAsync();

                CallTicketEscalationOverrideItem existing = null;
                if (_createTicketEscDaysToSupervisor.HasValue && _createTicketEscDaysToManager.HasValue)
                {
                    existing = new CallTicketEscalationOverrideItem
                    {
                        TicketId = 0,
                        DaysToSupervisor = _createTicketEscDaysToSupervisor.Value,
                        DaysToManager = _createTicketEscDaysToManager.Value,
                        Reason = _createTicketEscReason ?? string.Empty
                    };
                }

                var dlg = new WpfEscalationOverrideDialog(settings, existing, allowClear: false, headerText: "Set Escalation", windowTitle: "Set Escalation")
                {
                    Owner = Window.GetWindow(this)
                };
                if (dlg.ShowDialog() != true)
                    return;

                _createTicketEscDaysToSupervisor = dlg.DaysToSupervisor;
                _createTicketEscDaysToManager = dlg.DaysToManager;
                _createTicketEscReason = dlg.Reason;

                if (!_createTicketEscDaysToSupervisor.HasValue || !_createTicketEscDaysToManager.HasValue)
                {
                    ClearCreateTicketEscalation();
                    return;
                }

                UpdateCreateTicketEscalationUi();
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, ex.Message, "Set Escalation Failed");
            }
        }

        private async Task AssignSelectedTicketToMeAsync()
        {
            var me = FindBestEmployeeMatchForCurrentUser();
            if (me == null || me.Id <= 0)
            {
                WpfItcmDialogService.ShowInfo(this, "Your account is not linked to an IT employee in the assignment list.", "Assign to me");
                return;
            }

            SelectLookupItem(_pendingAssigneeCombo, me.Id);
            await ReassignSelectedTicketAsync();
        }

        private async Task ReassignSelectedTicketAsync()
        {
            if (_selectedTicket == null || _repository == null)
            {
                return;
            }

            if (!AppSession.IsLoggedIn || AppSession.CurrentUserId <= 0)
            {
                WpfItcmDialogService.ShowWarning(this, "You must be logged in to reassign tickets.", "Reassign");
                return;
            }

            try
            {
                await new CallEmailNotificationService(_repository).AssignTicketAndNotifyAsync(_selectedTicket.TicketId, GetLookupId(_pendingAssigneeCombo), AppSession.CurrentUserId);
                await ReloadTicketsAsync(false, false, _selectedTicket.TicketId);
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, ex.Message, "Reassign");
            }
        }

        private async Task ReassignSelectedTicketWithPickerAsync()
        {
            if (_selectedTicket == null || _repository == null)
            {
                return;
            }

            if (!AppSession.IsLoggedIn || AppSession.CurrentUserId <= 0)
            {
                WpfItcmDialogService.ShowWarning(this, "You must be logged in to reassign tickets.", "Reassign");
                return;
            }

            await EnsureAssigneesLoadedForPickerAsync();
            var assignees = BuildAssigneePickerItems();
            if (assignees.Count <= 1)
            {
                WpfItcmDialogService.ShowInfo(this, "No IT assignees are available for reassignment.", "Reassign");
                return;
            }

            var selected = ShowAssigneePickerDialog(assignees, _selectedTicket);
            if (selected == null)
            {
                return;
            }

            try
            {
                var assigneeId = selected.Id > 0 ? (int?)selected.Id : null;
                await new CallEmailNotificationService(_repository).AssignTicketAndNotifyAsync(_selectedTicket.TicketId, assigneeId, AppSession.CurrentUserId);
                SelectLookupItem(_pendingAssigneeCombo, selected.Id);
                await ReloadTicketsAsync(false, false, _selectedTicket.TicketId);
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, ex.Message, "Reassign");
            }
        }

        private async Task EnsureAssigneesLoadedForPickerAsync()
        {
            if (_repository == null || (_assignees != null && _assignees.Count > 0))
            {
                return;
            }

            if (_departments == null || _departments.Count == 0)
            {
                _departments = await _repository.GetDepartmentsAsync() ?? new List<LookupItem>();
            }

            var itDeptName = GuessItDepartmentName(_departments);
            _assignees = await _repository.GetCallAssignmentCandidatesByDepartmentNameAsync(itDeptName) ?? new List<LookupItem>();

            PopulateLookupFilter(_assigneeFilter, _assignees, "(All)", includeUnassigned: true);
            PopulateLookupCombo(_createAssignedTo, _assignees, "(Unassigned)", includeBlank: true);
            PopulateLookupCombo(_pendingAssigneeCombo, _assignees, "(Unassigned)", includeBlank: true);
        }

        private List<LookupItem> BuildAssigneePickerItems()
        {
            var list = new List<LookupItem>
            {
                new LookupItem { Id = 0, Name = "(Unassigned)" }
            };

            list.AddRange((_assignees ?? new List<LookupItem>())
                .Where(x => x != null && x.Id > 0)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .OrderBy(x => x.Name));

            return list;
        }

        private LookupItem ShowAssigneePickerDialog(List<LookupItem> assignees, CallTicketListItem ticket)
        {
            LookupItem selected = null;
            var currentName = (ticket.ResponsiblePerson ?? string.Empty).Trim();

            var dialog = new Window
            {
                Title = "Reassign Ticket",
                Width = 480,
                Height = 560,
                MinWidth = 420,
                MinHeight = 430,
                ResizeMode = ResizeMode.CanResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                Background = Brushes.White
            };

            var owner = Window.GetWindow(this);
            if (owner != null)
            {
                dialog.Owner = owner;
            }

            var root = new Grid
            {
                Margin = new Thickness(18)
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 14)
            };
            header.Children.Add(new TextBlock
            {
                Text = ticket.TicketCode ?? ticket.TicketId.ToString(),
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42)
            });
            header.Children.Add(new TextBlock
            {
                Text = "Current assignee: " + (string.IsNullOrWhiteSpace(currentName) ? "(Unassigned)" : currentName),
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 12,
                Foreground = BrushFromRgb(100, 116, 139)
            });
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var searchBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 12,
                Tag = "Search assignees"
            };
            ApplyModernTextBoxStyle(searchBox);
            Grid.SetRow(searchBox, 1);
            root.Children.Add(searchBox);

            var listBox = new ListBox
            {
                DisplayMemberPath = "Name",
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                FontSize = 13,
                Padding = new Thickness(6)
            };
            Grid.SetRow(listBox, 2);
            root.Children.Add(listBox);

            var footer = new DockPanel
            {
                Margin = new Thickness(0, 14, 0, 0),
                LastChildFill = false
            };

            var cancelButton = CreatePrimaryButton("Cancel", BrushFromRgb(100, 116, 139));
            cancelButton.Margin = new Thickness(0, 0, 10, 0);
            cancelButton.MinWidth = 100;
            cancelButton.Click += (_, __) => dialog.DialogResult = false;

            var assignButton = CreatePrimaryButton("Assign", BrushFromRgb(37, 99, 235));
            assignButton.Margin = new Thickness(0);
            assignButton.MinWidth = 110;
            assignButton.IsEnabled = false;
            assignButton.Click += (_, __) =>
            {
                selected = listBox.SelectedItem as LookupItem;
                if (selected != null)
                {
                    dialog.DialogResult = true;
                }
            };

            DockPanel.SetDock(assignButton, Dock.Right);
            DockPanel.SetDock(cancelButton, Dock.Right);
            footer.Children.Add(assignButton);
            footer.Children.Add(cancelButton);
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            void ApplyFilter()
            {
                var query = (searchBox.Text ?? string.Empty).Trim();
                var filtered = string.IsNullOrWhiteSpace(query)
                    ? assignees
                    : assignees.Where(x => (x.Name ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                listBox.ItemsSource = filtered;

                var preferred = filtered.FirstOrDefault(x => !string.IsNullOrWhiteSpace(currentName) && string.Equals((x.Name ?? string.Empty).Trim(), currentName, StringComparison.OrdinalIgnoreCase))
                    ?? filtered.FirstOrDefault(x => x.Id == 0);
                listBox.SelectedItem = preferred;
            }

            searchBox.TextChanged += (_, __) => ApplyFilter();
            listBox.SelectionChanged += (_, __) => assignButton.IsEnabled = listBox.SelectedItem is LookupItem;
            listBox.MouseDoubleClick += (_, __) =>
            {
                if (listBox.SelectedItem is LookupItem item)
                {
                    selected = item;
                    dialog.DialogResult = true;
                }
            };
            dialog.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    dialog.DialogResult = false;
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && listBox.SelectedItem is LookupItem item)
                {
                    selected = item;
                    dialog.DialogResult = true;
                    e.Handled = true;
                }
            };

            dialog.Content = root;
            ApplyFilter();
            dialog.Loaded += (_, __) =>
            {
                searchBox.Focus();
                searchBox.SelectAll();
            };

            return dialog.ShowDialog() == true ? selected : null;
        }

        private async Task UpdateSelectedTicketWorkflowAsync()
        {
            if (_selectedTicket == null || _repository == null)
            {
                return;
            }

            if (!AppSession.IsLoggedIn || AppSession.CurrentUserId <= 0)
            {
                WpfItcmDialogService.ShowWarning(this, "You must be logged in to update tickets.", "Update Ticket");
                return;
            }

            var status   = (_pendingStatusCombo.SelectedItem   as string) ?? NormalizeLegacyTicketStatus(_selectedTicket.Status);
            var priority = (_pendingPriorityCombo.SelectedItem as string) ?? (_selectedTicket.Priority ?? "Medium");
            var noteText = (_pendingSolutionBox.Text ?? string.Empty).Trim();

            try
            {
                if (!ValidateDirectStatusSelection(this, _selectedTicket, status, noteText))
                    return;

                var currentStatus   = NormalizeLegacyTicketStatus(_selectedTicket.Status);
                var currentPriority = _selectedTicket.Priority ?? "Medium";
                var statusChanged   = !string.Equals(status,   currentStatus,   StringComparison.OrdinalIgnoreCase);
                var priorityChanged = !string.Equals(priority, currentPriority, StringComparison.OrdinalIgnoreCase);
                var hasNote         = !string.IsNullOrWhiteSpace(noteText);

                // Nothing to save?
                if (!statusChanged && !priorityChanged && !hasNote)
                {
                    WpfItcmDialogService.ShowInfo(
                        this,
                        "No changes detected. Adjust the status, priority, or add a note before saving.",
                        "Nothing to Update");
                    return;
                }

                // Workflow guard
                if (statusChanged && !TicketWorkflow.IsStatusTransitionAllowed(_selectedTicket.Status, status))
                {
                    WpfItcmDialogService.ShowWarning(
                        this,
                        "This workflow blocks moving the ticket backward. Use Reopen for final tickets.",
                        "Invalid Status Change");
                    return;
                }

                var dialog = new WpfTicketStatusActionDialog(
                    _selectedTicket,
                    statusChanged ? status : null,
                    priority,
                    priorityChanged,
                    noteText)
                {
                    Owner = Window.GetWindow(this)
                };
                if (dialog.ShowDialog() != true)
                    return;

                noteText = dialog.NoteText;
                hasNote = !string.IsNullOrWhiteSpace(noteText);

                if (statusChanged)
                {
                    // Notify with the raw stored status so the mail matches
                    // CallTicketHistory.OldValue (normalization is display-only).
                    var rawOldStatus = _selectedTicket.Status ?? currentStatus;
                    await _repository.SetTicketStatusAsync(_selectedTicket.TicketId, status, AppSession.CurrentUserId, noteText);
                    new CallEmailNotificationService(_repository)
                        .NotifyStatusChangeAsync(_selectedTicket.TicketId, rawOldStatus, status, noteText, AppSession.CurrentUserId)
                        .FireAndForget(ex => System.Diagnostics.Debug.WriteLine("[Tickets] Status update notification failed: " + ex));
                }

                if (priorityChanged)
                    await _repository.SetTicketPriorityAsync(_selectedTicket.TicketId, priority, AppSession.CurrentUserId);

                if (hasNote)
                {
                    await _repository.AddTicketNoteAsync(_selectedTicket.TicketId, "Solution", noteText, AppSession.CurrentUserId);
                    _pendingSolutionBox.Text = string.Empty;
                }

                await ReloadTicketsAsync(false, false, _selectedTicket.TicketId);
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, ex.Message, "Update Ticket");
            }
        }

        private async Task ReopenSelectedTicketAsync()
        {
            if (_selectedTicket == null || _repository == null)
            {
                return;
            }

            if (!AppSession.IsLoggedIn || AppSession.CurrentUserId <= 0)
            {
                WpfItcmDialogService.ShowWarning(this, "You must be logged in to reopen tickets.", "Reopen");
                return;
            }

            try
            {
                var dialog = new WpfTicketStatusActionDialog(
                    _selectedTicket,
                    "Reopened",
                    _selectedTicket.Priority ?? "Medium",
                    priorityChanged: false,
                    initialNote: (_pendingSolutionBox.Text ?? string.Empty).Trim())
                {
                    Owner = Window.GetWindow(this)
                };
                if (dialog.ShowDialog() != true)
                    return;

                var noteText = dialog.NoteText;
                await _repository.SetTicketStatusAsync(_selectedTicket.TicketId, "Reopened", AppSession.CurrentUserId, noteText);
                _pendingSolutionBox.Text = string.Empty;
                await ReloadTicketsAsync(false, false, _selectedTicket.TicketId);
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, ex.Message, "Reopen");
            }
        }

        private async Task ReturnTemporaryReplacementAsync()
        {
            if (_selectedTicket == null || _repository == null)
            {
                return;
            }

            if (!AppSession.IsLoggedIn || AppSession.CurrentUserId <= 0)
            {
                WpfItcmDialogService.ShowWarning(this, "You must be logged in to return temporary replacement items.", "Return Temp Item");
                return;
            }

            try
            {
                var preview = await _repository.GetTemporaryReplacementReturnPreviewAsync(_selectedTicket.TicketId);
                if (preview.NewItemId <= 0 || preview.Quantity <= 0)
                {
                    // Service-Only temporary: no parts were ever issued, so offer
                    // a one-click close-out instead of a dead end.
                    var closeOut = WpfItcmDialogService.Confirm(
                        this,
                        "No temporary parts were issued for this ticket (Service Only). Mark it as Solved instead?",
                        "Return Temp Item",
                        "Mark Solved");
                    if (closeOut)
                    {
                        var oldStatus = _selectedTicket.Status;
                        await _repository.SetTicketStatusAsync(_selectedTicket.TicketId, "Solved", AppSession.CurrentUserId, "Temporary service confirmed permanent.");
                        new CallEmailNotificationService(_repository)
                            .NotifyStatusChangeAsync(_selectedTicket.TicketId, oldStatus, "Solved", "Temporary service confirmed permanent.", AppSession.CurrentUserId)
                            .FireAndForget(ex => System.Diagnostics.Debug.WriteLine("[Tickets] Temp close-out notification failed: " + ex));
                        await ReloadTicketsAsync(false, false, _selectedTicket.TicketId);
                    }
                    return;
                }

                if (preview.AlreadyReturned)
                {
                    WpfItcmDialogService.ShowInfo(this, "The temporary replacement item for this ticket was already returned.", "Return Temp Item");
                    return;
                }

                var confirm = WpfItcmDialogService.Confirm(
                    this,
                    "Return " + preview.NewItemText + " (Qty: " + preview.Quantity + ")?",
                    "Return Temp Item",
                    "Return Item");

                if (!confirm)
                {
                    return;
                }

                await _repository.ReturnTemporaryReplacementAsync(_selectedTicket.TicketId, AppSession.CurrentUserId);
                await ReloadTicketsAsync(false, false, _selectedTicket.TicketId);
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, ex.Message, "Return Temp Item");
            }
        }

        private async Task SyncSelectedTicketSetAsync()
        {
            if (_selectedTicket == null || _repository == null)
            {
                return;
            }

            if (!AppSession.IsLoggedIn || AppSession.CurrentUserId <= 0)
            {
                WpfItcmDialogService.ShowWarning(this, "You must be logged in to sync ticket-linked sets.", "Sync Set");
                return;
            }

            try
            {
                var sync = await _repository.SyncSetToLatestReplacementWithResultAsync(_selectedTicket.TicketId, AppSession.CurrentUserId);
                var setLabel = sync != null && sync.SetId.HasValue && sync.SetId.Value > 0
                    ? (!string.IsNullOrWhiteSpace(sync.SetCode) ? sync.SetCode + " (SetId " + sync.SetId.Value + ")" : "SetId " + sync.SetId.Value)
                    : "N/A";
                var outcome = (sync?.Outcome ?? "Unknown").Trim();
                var detailsText =
                    "Outcome: " + outcome + "\n" +
                    "Set: " + setLabel + "\n" +
                    "Requests updated: " + (sync?.UpdatedRequests ?? 0) + "\n" +
                    "SetItems updated: " + (sync?.UpdatedSetItems ?? 0) + "\n\n" +
                    "Open the related set details and refresh to confirm the serial number.";

                if (outcome.Equals("Swapped", StringComparison.OrdinalIgnoreCase))
                    WpfItcmDialogService.ShowInfo(this, detailsText, "Sync Set");
                else
                    WpfItcmDialogService.ShowWarning(this, detailsText, "Sync Set");

                await ReloadTicketsAsync(false, false, _selectedTicket.TicketId);
            }
            catch (Exception ex)
            {
                // Service-Only tickets have no replacement swap to sync - explain,
                // don't alarm.
                if (ex is InvalidOperationException && (ex.Message ?? string.Empty).IndexOf("No replacement history", StringComparison.OrdinalIgnoreCase) >= 0)
                    WpfItcmDialogService.ShowInfo(this, "This ticket has no replacement history (Service Only), so there is no set to sync.", "Sync Set");
                else
                    WpfItcmDialogService.ShowError(this, ex.Message, "Sync Set");
            }
        }

        private async Task MarkSelectedTicketAsync()
        {
            if (_selectedTicket == null || _repository == null)
            {
                return;
            }

            if (!AppSession.IsLoggedIn || AppSession.CurrentUserId <= 0)
            {
                WpfItcmDialogService.ShowWarning(this, "You must be logged in to mark tickets as solved or resolved.", "Mark As");
                return;
            }

            try
            {
                var ticketId = _selectedTicket.TicketId;
                var oldStatus = _selectedTicket.Status;

                var itemRepo = new ItemRepository();
                var outHardware = await itemRepo.GetHardwareOutItemsLookupAsync();
                var stockHardware = await itemRepo.GetHardwareStockItemsLookupAsync();
                var categoryRepo = new CategoryRepository();
                var categories = (await categoryRepo.GetAllAsync()).Where(c => c.Active).OrderBy(c => c.Name).ToList();
                var conditionRepo = new ItemConditionRepository();
                var conditions = conditionRepo.GetAll().OrderBy(c => c.ConditionId).ToList();

                var dlg = new WpfMarkAsResolutionDialog(outHardware, stockHardware, categories, conditions)
                {
                    Owner = Window.GetWindow(this)
                };
                if (dlg.ShowDialog() != true)
                {
                    return;
                }

                int? oldItemId = null;
                if (dlg.ResolutionType == "Replacement" && !dlg.UseUnlistedOldItem)
                    oldItemId = dlg.OldItemId;

                var forwardDialog = new WpfForwardToRepairDialog(
                    outHardware,
                    oldItemId,
                    isReplacement: dlg.ResolutionType == "Replacement",
                    usesUnlistedOldItem: dlg.UseUnlistedOldItem)
                {
                    Owner = Window.GetWindow(this)
                };
                // Repair forwarding is gated off until WinForms supports it:
                // WinForms Mark As has no forward branch, so a WPF-forwarded
                // ticket lands in a status the other client cannot manage or
                // report on. Flip back to true together with the WinForms
                // forward implementation (then delete this gate).
                var repairForwardingEnabled = false;
                var forwardToRepair = repairForwardingEnabled && forwardDialog.ShowDialog() == true;
                Yakult.Inventory.App.Models.RepairPortal.RepairTicketListItem linkedRepairTicket = null;
                var postForwardStatus = "Forwarded to Repair";

                // Build the linked Repair Ticket before mutating the ITCM resolution/inventory
                // state, then let the Repair operator review the prefilled standard intake form.
                // The parent IT Call changes only after that form returns a created ticket.
                if (forwardToRepair)
                {
                    if (dlg.ResolutionType == "Replacement" && !oldItemId.HasValue)
                    {
                        oldItemId = itemRepo.AddItem(new ItemDto
                        {
                            Name = dlg.UnlistedOldItemName,
                            Description = dlg.UnlistedOldItemDescription,
                            ModelNumber = dlg.UnlistedOldItemModelNumber,
                            SerialNumber = dlg.UnlistedOldItemSerialNumber,
                            UnitOfMeasure = dlg.UnlistedOldItemUnitOfMeasure,
                            CreatedByUserId = AppSession.CurrentUserId,
                            CreatedByName = AppSession.CurrentUserName,
                            Active = true,
                            DateCreated = DateTime.Now,
                            ItemType = "Hardware",
                            CategoryId = dlg.UnlistedOldItemCategoryId,
                            Category = dlg.UnlistedOldItemCategoryName,
                            StockOnHand = 0,
                            ConditionId = dlg.OldItemConditionId,
                            Remarks = dlg.OldItemConditionRemarks,
                            AffectsInventory = true,
                            IsTrackedAsset = false
                        });
                    }

                    var repairItemId = dlg.ResolutionType == "Replacement"
                        ? (oldItemId ?? 0)
                        : (forwardDialog.RepairItemId ?? 0);
                    var prefilledRepairRequest = await new CallTicketRepairForwardingService(_repository)
                        .CreatePrefilledRequestAsync(
                            ticketId,
                            repairItemId,
                            dlg.ResolutionType,
                            dlg.Remarks,
                            AppSession.CurrentEmployeeId);
                    var repairDialog = new NewRepairTicketDialog(new RepairTicketRepository())
                    {
                        Owner = Window.GetWindow(this)
                    };
                    await repairDialog.PrefillForCallTicketForwardingAsync(prefilledRepairRequest);
                    if (repairDialog.ShowDialog() != true || repairDialog.CreatedTicket == null)
                    {
                        return;
                    }

                    linkedRepairTicket = repairDialog.CreatedTicket;

                    var resolutionDialog = new WpfRepairForwardResolutionDialog(linkedRepairTicket.TicketCode)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    if (resolutionDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(resolutionDialog.SelectedStatus))
                    {
                        postForwardStatus = resolutionDialog.SelectedStatus;
                    }
                    // Closing this final prompt after the Repair Ticket exists leaves the IT Call
                    // active, rather than silently resolving it.
                }

                var markAsStatus = forwardToRepair
                    ? postForwardStatus
                    : "Solved";
                if (dlg.ResolutionType == "Replacement")
                {
                    if (!oldItemId.HasValue)
                    {
                        oldItemId = itemRepo.AddItem(new ItemDto
                        {
                            Name = dlg.UnlistedOldItemName,
                            Description = dlg.UnlistedOldItemDescription,
                            ModelNumber = dlg.UnlistedOldItemModelNumber,
                            SerialNumber = dlg.UnlistedOldItemSerialNumber,
                            UnitOfMeasure = dlg.UnlistedOldItemUnitOfMeasure,
                            CreatedByUserId = AppSession.CurrentUserId,
                            CreatedByName = AppSession.CurrentUserName,
                            Active = true,
                            DateCreated = DateTime.Now,
                            ItemType = "Hardware",
                            CategoryId = dlg.UnlistedOldItemCategoryId,
                            Category = dlg.UnlistedOldItemCategoryName,
                            StockOnHand = 0,
                            ConditionId = dlg.OldItemConditionId,
                            Remarks = dlg.OldItemConditionRemarks,
                            AffectsInventory = true,
                            IsTrackedAsset = false
                        });
                    }

                    // A forwarded old unit must stay out of available stock while Repair assesses it.
                    var oldItemAction = forwardToRepair ? "Unrepaired" : dlg.OldItemRepairAction;
                    await _repository.ApplyTicketReplacementAsync(
                        ticketId,
                        oldItemId.Value,
                        dlg.NewItemId ?? 0,
                        dlg.Quantity,
                        dlg.Remarks,
                        AppSession.CurrentUserId,
                        dlg.OldItemConditionId,
                        dlg.OldItemConditionRemarks,
                        oldItemAction);

                    if (!forwardToRepair)
                        markAsStatus = dlg.IsTemporaryReplacement ? "Resolved (Temporary)" : "Solved";
                }
                else
                {
                    // Canonical resolution type: the repair link (if any) travels in
                    // the history note, never in a suffixed type value, so
                    // resolution reports match on exact 'Service Only'.
                    var resolutionType = "Service Only";
                    await _repository.LogTicketResolutionAsync(ticketId, resolutionType, dlg.Remarks, AppSession.CurrentUserId);
                    if (!forwardToRepair)
                        markAsStatus = dlg.IsTemporaryReplacement ? "Resolved (Temporary)" : "Solved";
                }

                var isTemporaryMark = !forwardToRepair && dlg.IsTemporaryReplacement;
                var resolutionLabel = dlg.ResolutionType + (isTemporaryMark ? " (Temporary)" : string.Empty);
                var note = string.IsNullOrWhiteSpace(dlg.Remarks)
                    ? "Marked as: " + resolutionLabel
                    : "Marked as: " + resolutionLabel + " | " + dlg.Remarks;
                if (linkedRepairTicket != null)
                {
                    note += " | Forwarded to Repair Ticket " + linkedRepairTicket.TicketCode;
                    if (!string.Equals(markAsStatus, "Forwarded to Repair", StringComparison.OrdinalIgnoreCase))
                        note += " | IT Call outcome: " + markAsStatus;
                }

                await _repository.SetTicketStatusAsync(ticketId, markAsStatus, AppSession.CurrentUserId, note);
                new CallEmailNotificationService(_repository)
                    .NotifyStatusChangeAsync(ticketId, oldStatus, markAsStatus, dlg.Remarks, AppSession.CurrentUserId)
                    .FireAndForget(ex => System.Diagnostics.Debug.WriteLine("[Tickets] Mark As notification failed: " + ex));
                await ReloadTicketsAsync(false, false, ticketId);
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, ex.Message, "Mark As");
            }
        }

        private async Task OpenFieldWorkForSelectedTicketAsync()
        {
            if (_selectedTicket == null || _repository == null)
            {
                WpfItcmDialogService.ShowWarning(this, "Select a ticket first to manage its field visit.", "Field Work");
                return;
            }
            if (!AppSession.IsLoggedIn || AppSession.CurrentUserId <= 0)
            {
                WpfItcmDialogService.ShowWarning(this, "You must be logged in to manage field work.", "Field Work");
                return;
            }
            try
            {
                var dlg = new WpfCallFieldWorkDialog(_repository, _selectedTicket.TicketId, _selectedTicket.TicketCode)
                {
                    Owner = Window.GetWindow(this)
                };
                dlg.ShowDialog();
                // Refresh workflow/history after field work (it logs to CallTicketHistory)
                await ReloadTicketsAsync(false, false, _selectedTicket.TicketId);
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, ex.Message, "Field Work");
            }
        }

        private async Task EditSelectedTicketEscalationOverrideAsync()
        {
            if (_selectedTicket == null || _repository == null)
            {
                return;
            }

            if (!AppSession.IsLoggedIn || AppSession.CurrentUserId <= 0)
            {
                WpfItcmDialogService.ShowWarning(this, "You must be logged in to edit escalation overrides.", "Escalation Override");
                return;
            }

            try
            {
                if (!await _repository.EscalationOverridesEnabledAsync())
                {
                    WpfItcmDialogService.ShowInfo(this, "Escalation overrides are not installed in this database yet.", "Escalation Override");
                    return;
                }

                var settings = _escalationSettings ?? await _repository.GetEscalationSettingsAsync();
                var existing = await _repository.GetTicketEscalationOverrideAsync(_selectedTicket.TicketId);

                var dlg = new WpfEscalationOverrideDialog(settings, existing)
                {
                    Owner = Window.GetWindow(this)
                };
                if (dlg.ShowDialog() != true)
                {
                    return;
                }

                await _repository.SetTicketEscalationOverrideAsync(
                    _selectedTicket.TicketId,
                    dlg.ClearRequested ? null : (int?)dlg.DaysToSupervisor,
                    dlg.ClearRequested ? null : (int?)dlg.DaysToManager,
                    dlg.Reason,
                    AppSession.CurrentUserId,
                    dlg.ClearRequested);

                await ReloadTicketsAsync(false, false, _selectedTicket.TicketId);
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, ex.Message, "Escalation Override");
            }
        }

        private void UpdatePendingCaseDetails(CallTicketListItem ticket)
        {
            if (ticket == null)
            {
                _pendingCaseHeadline.Text = "Select a lifecycle ticket to edit status, priority, assignment, and notes.";
                _pendingProblemBox.Text = string.Empty;
                _pendingSolutionBox.Text = string.Empty;
                _pendingStatusCombo.Items.Clear();
                _pendingStatusCombo.SelectedIndex = -1;
                _pendingPriorityCombo.SelectedIndex = -1;
                if (_pendingAssigneeCombo.Items.Count > 0) _pendingAssigneeCombo.SelectedIndex = 0;
                SetPendingCaseActionsEnabled(false);
                return;
            }

            _pendingCaseHeadline.Text = (ticket.TicketCode ?? ticket.TicketId.ToString()) + " • " + (ticket.Issue ?? "No issue provided");
            _pendingProblemBox.Text = ticket.Issue ?? string.Empty;
            ConfigurePendingStatusDropdownForTicket(ticket.Status);
            _pendingPriorityCombo.SelectedItem = string.IsNullOrWhiteSpace(ticket.Priority) ? "Medium" : ticket.Priority.Trim();
            SelectLookupItemByName(_pendingAssigneeCombo, ticket.ResponsiblePerson);
            SetPendingCaseActionsEnabled(!IsFinalStatus(ticket.Status));
        }

        private void SetPendingCaseActionsEnabled(bool enabled)
        {
            var hasTicket = _selectedTicket != null;
            var isResolvedTemp = hasTicket && string.Equals((_selectedTicket.Status ?? string.Empty).Trim(), "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase);
            _pendingStatusCombo.IsEnabled = enabled;
            _pendingPriorityCombo.IsEnabled = enabled;
            _pendingAssigneeCombo.IsEnabled = enabled;
            _pendingSolutionBox.IsEnabled = enabled;
            _pendingAssignToMeButton.IsEnabled = enabled;
            _pendingReassignButton.IsEnabled = enabled;
            _pendingUpdateButton.IsEnabled = enabled;
            _pendingMarkAsButton.IsEnabled = enabled;
            _pendingFieldWorkButton.IsEnabled = enabled;
            _pendingEscalationOverrideButton.IsEnabled = enabled;
            _pendingSyncSetButton.IsEnabled = hasTicket;
            _pendingReturnTempButton.IsEnabled = isResolvedTemp;
            _pendingReturnTempButton.Visibility = isResolvedTemp ? Visibility.Visible : Visibility.Collapsed;
            _pendingReopenButton.IsEnabled = hasTicket;
            _pendingReopenButton.Visibility = hasTicket && IsFinalStatus(_selectedTicket.Status)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ConfigurePendingStatusDropdownForTicket(string currentStatus)
        {
            var current = NormalizeLegacyTicketStatus(currentStatus);
            var currentRank = TicketWorkflow.GetStatusRank(current);

            _pendingStatusCombo.Items.Clear();

            // Single catalog (TicketWorkflow.DirectPickStatuses). Forwarded to
            // Repair is forward-flow only; current value is retained below.
            var statuses = TicketWorkflow.DirectPickStatuses;

            if (currentRank < 0)
            {
                foreach (var status in statuses)
                    _pendingStatusCombo.Items.Add(status);
            }
            else
            {
                foreach (var status in statuses)
                {
                    if (status.Equals("Reopened", StringComparison.OrdinalIgnoreCase))
                    {
                        if (current.Equals("Reopened", StringComparison.OrdinalIgnoreCase))
                            _pendingStatusCombo.Items.Add(status);
                        continue;
                    }

                    if (TicketWorkflow.GetStatusRank(status) >= currentRank)
                        _pendingStatusCombo.Items.Add(status);
                }
            }

            if (!string.IsNullOrWhiteSpace(current) && !_pendingStatusCombo.Items.Contains(current))
                _pendingStatusCombo.Items.Insert(0, current);

            _pendingStatusCombo.SelectedItem = current;
        }

        private static bool ValidateDirectStatusSelection(DependencyObject owner, CallTicketListItem ticket, string normalizedStatus, string noteText)
        {
            if (ticket == null || string.IsNullOrWhiteSpace(normalizedStatus))
                return true;

            var willChangeStatus = !string.Equals((ticket.Status ?? string.Empty).Trim(), normalizedStatus.Trim(), StringComparison.OrdinalIgnoreCase);

            if (normalizedStatus.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                || normalizedStatus.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase))
            {
                WpfItcmDialogService.ShowInfo(
                    owner,
                    "To mark a ticket as Solved/Resolved (Temporary), use the 'Mark As...' flow.",
                    "Status Change");
                return false;
            }

            if (normalizedStatus.Equals("Reopened", StringComparison.OrdinalIgnoreCase))
            {
                WpfItcmDialogService.ShowInfo(
                    owner,
                    "To reopen a ticket, use the 'Reopen' button. The status dropdown does not allow setting 'Reopened' directly.",
                    "Status Change");
                return false;
            }

            if (willChangeStatus
                && normalizedStatus.Equals("Escalated", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(noteText))
            {
                WpfItcmDialogService.ShowWarning(
                    owner,
                    "Please enter a short note (in the Solutions box) before setting status to Escalated.\n\n" +
                    "Example: reason for escalation / what's needed / who to contact.",
                    "Escalation Note Required");
                return false;
            }

            return true;
        }

        private void HookWorkbenchEvents()
        {
            // Card interactions are wired when lifecycle cards are created.
        }

        private FrameworkElement BuildTeamWorkbench(Border summaryCard, Border activityCard, Border workflowCard, Border createTicketContainer)
        {
            var viewport = new Grid { Background = BrushFromRgb(241, 244, 247) };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            scroll.Resources.MergedDictionaries.Add(WpfThemeResources.GetScrollBarStyle());
            viewport.Children.Add(scroll);
            var root = new StackPanel { Margin = new Thickness(30, 22, 30, 30) };
            scroll.Content = root;

            var masthead = new Border { Background = Brushes.White, BorderBrush = BrushFromRgb(226, 232, 240), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(22, 17, 22, 18), Margin = new Thickness(0, 0, 0, 12) };
            var mastheadGrid = new Grid();
            mastheadGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            mastheadGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mastheadGrid.Children.Add(new Border { Background = BrushFromRgb(13, 148, 136), CornerRadius = new CornerRadius(3) });
            var copy = new StackPanel { Margin = new Thickness(15, 0, 0, 0) };
            copy.Children.Add(new TextBlock { Text = "CASE MANAGEMENT", FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(15, 118, 110) });
            copy.Children.Add(new TextBlock { Text = "Ticket lifecycle", FontSize = 25, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(15, 23, 42), Margin = new Thickness(0, 3, 0, 0) });
            copy.Children.Add(new TextBlock { Text = "Browse team cases from active work through closure. Portal Intake stays a separate triage workspace.", FontSize = 12, Foreground = BrushFromRgb(71, 85, 105), Margin = new Thickness(0, 5, 0, 0), TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(copy, 1); mastheadGrid.Children.Add(copy); masthead.Child = mastheadGrid; root.Children.Add(masthead);

            var commandBar = new Border { Background = Brushes.White, BorderBrush = BrushFromRgb(226, 232, 240), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(15, 12, 15, 12), Margin = new Thickness(0, 0, 0, 11) };
            var command = new Grid();
            command.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); command.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); command.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); command.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); command.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); command.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            AddFilterField(command, 0, "Find a case", _searchBox); AddFilterField(command, 1, "Status", _statusFilter); AddFilterField(command, 2, "Assignee", _assigneeFilter); AddFilterField(command, 3, "Branch", _branchFilter);
            _refreshTicketsButton.Content = "Refresh"; _refreshTicketsButton.Background = BrushFromRgb(71, 85, 105); _refreshTicketsButton.Margin = new Thickness(13, 22, 0, 0); Grid.SetColumn(_refreshTicketsButton, 4); command.Children.Add(_refreshTicketsButton);
            var newTicket = CreatePrimaryButton("+ New Ticket", BrushFromRgb(13, 148, 136)); newTicket.Margin = new Thickness(8, 22, 0, 0); newTicket.Click += (_, __) => ShowCreateTicketModal(createTicketContainer); Grid.SetColumn(newTicket, 5); command.Children.Add(newTicket); commandBar.Child = command; root.Children.Add(commandBar);

            var lifeBar = new Border { Background = Brushes.White, BorderBrush = BrushFromRgb(226, 232, 240), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 14) };
            var life = new Grid(); life.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); life.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            life.Children.Add(new TextBlock { Text = "LIFECYCLE", FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(100, 116, 139), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 13, 0) });
            var chips = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            foreach (var scope in new[] { "All Tickets", "Active", "Waiting", "Temporary", "Resolved", "Closed" }) { var chip = CreateLifecycleChip(scope); chip.Click += async (_, __) => await SetLifecycleScopeAsync(scope); _lifecycleChipButtons.Add(chip); chips.Children.Add(chip); }
            Grid.SetColumn(chips, 1); life.Children.Add(chips); lifeBar.Child = life; root.Children.Add(lifeBar); UpdateLifecycleChipButtons();

            var browser = new Border { Background = Brushes.White, BorderBrush = BrushFromRgb(226, 232, 240), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(20, 18, 20, 20), MinHeight = 460 };
            var content = new StackPanel();
            var browserHeader = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            browserHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); browserHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); browserHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = new StackPanel(); title.Children.Add(new TextBlock { Text = "Cases", FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(15, 23, 42) }); _queueSummaryText = new TextBlock { Text = "Loading cases…", FontSize = 11.5, Foreground = BrushFromRgb(100, 116, 139), Margin = new Thickness(0, 3, 0, 0) }; title.Children.Add(_queueSummaryText); browserHeader.Children.Add(title);
            var switcher = BuildCaseViewSwitcher(); Grid.SetColumn(switcher, 1); browserHeader.Children.Add(switcher);
            var hint = new TextBlock { Text = "Table is a temporary alternate view", FontSize = 11, Foreground = BrushFromRgb(100, 116, 139), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) }; Grid.SetColumn(hint, 2); browserHeader.Children.Add(hint); content.Children.Add(browserHeader);

            var views = new Grid();
            _caseCardsPanel = new StackPanel();
            _casesViewHost = new Border { Child = _caseCardsPanel };
            _caseTablePanel = new StackPanel { MinWidth = 1120 };
            _tableViewHost = new Border { Visibility = Visibility.Collapsed, Child = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _caseTablePanel } };
            views.Children.Add(_casesViewHost); views.Children.Add(_tableViewHost); content.Children.Add(views);
            _lifecycleStateText = CreateGridStateText(); _lifecycleStateText.Margin = new Thickness(0, 12, 0, 0); content.Children.Add(_lifecycleStateText); browser.Child = content;

            var body = new Grid { Height = 620 };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            var leftScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 14, 0)
            };
            leftScroll.Content = browser;
            Grid.SetColumn(leftScroll, 0);
            body.Children.Add(leftScroll);

            _inspectorCard = BuildPersistentInspector(summaryCard, activityCard, workflowCard);
            Grid.SetColumn(_inspectorCard, 1);
            body.Children.Add(_inspectorCard);
            root.Children.Add(body);
            UpdateCaseBrowserViewToggle();
            return viewport;
        }

        private FrameworkElement BuildCaseViewSwitcher()
        {
            var wrap = new Border { Background = BrushFromRgb(240, 253, 250), BorderBrush = BrushFromRgb(167, 243, 208), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(3) };
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            _casesViewButton = CreatePrimaryButton("Cases", BrushFromRgb(13, 148, 136));
            _tableViewButton = CreatePrimaryButton("Table", Brushes.White);
            foreach (var button in new[] { _casesViewButton, _tableViewButton }) { button.FontSize = 11.5; button.FontWeight = FontWeights.SemiBold; button.Padding = new Thickness(13, 6, 13, 6); button.Margin = new Thickness(0); }
            _casesViewButton.Click += (_, __) => SetCaseBrowserView("Cases");
            _tableViewButton.Click += (_, __) => SetCaseBrowserView("Table");
            panel.Children.Add(_casesViewButton); panel.Children.Add(_tableViewButton); wrap.Child = panel;
            return wrap;
        }

        private void SetCaseBrowserView(string view)
        {
            _caseBrowserView = string.Equals(view, "Table", StringComparison.OrdinalIgnoreCase) ? "Table" : "Cases";
            if (_casesViewHost != null) _casesViewHost.Visibility = _caseBrowserView == "Cases" ? Visibility.Visible : Visibility.Collapsed;
            if (_tableViewHost != null) _tableViewHost.Visibility = _caseBrowserView == "Table" ? Visibility.Visible : Visibility.Collapsed;
            UpdateCaseBrowserViewToggle();
            RefreshCaseCards();
        }

        private void UpdateCaseBrowserViewToggle()
        {
            if (_casesViewButton == null || _tableViewButton == null) return;
            var cases = _caseBrowserView == "Cases";
            _casesViewButton.Background = cases ? BrushFromRgb(13, 148, 136) : Brushes.White;
            _casesViewButton.Foreground = cases ? Brushes.White : BrushFromRgb(15, 118, 110);
            _tableViewButton.Background = cases ? Brushes.White : BrushFromRgb(13, 148, 136);
            _tableViewButton.Foreground = cases ? BrushFromRgb(15, 118, 110) : Brushes.White;
        }        private Button CreateLifecycleChip(string scope)
        {
            var chip = new Button { Content = scope == "All Tickets" ? "All" : scope, Tag = scope, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand, BorderThickness = new Thickness(1), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 7, 0) };
            ApplyModernButtonTemplate(chip, new Thickness(12, 6, 12, 6)); return chip;
        }
        private async Task SetLifecycleScopeAsync(string scope) { _lifecycleScope = scope ?? "All Tickets"; UpdateLifecycleChipButtons(); await ReloadTicketsAsync(false, false); }
        private void UpdateLifecycleChipButtons()
        {
            foreach (var chip in _lifecycleChipButtons) { var active = string.Equals(chip.Tag as string, _lifecycleScope, StringComparison.OrdinalIgnoreCase); chip.Background = active ? BrushFromRgb(13, 148, 136) : Brushes.White; chip.Foreground = active ? Brushes.White : BrushFromRgb(15, 118, 110); chip.BorderBrush = active ? BrushFromRgb(13, 148, 136) : BrushFromRgb(167, 243, 208); }
        }

        private Border BuildPersistentInspector(Border summaryCard, Border activityCard, Border workflowCard)
        {
            var inspector = new Border { Background = Brushes.White, BorderBrush = BrushFromRgb(226, 232, 240), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), ClipToBounds = true };
            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new Border { Padding = new Thickness(16, 14, 16, 12), Background = new LinearGradientBrush(BrushFromRgb(15, 118, 110).Color, BrushFromRgb(13, 148, 136).Color, new Point(0, 0), new Point(1, 1)) };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock { Text = "SELECTED TICKET", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromArgb(205, 255, 255, 255)) });
            _inspectorTicketCode = new TextBlock { Text = "No ticket selected", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 3, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            _inspectorIssue = new TextBlock { Text = "Select a case from the list to inspect it.", FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(225, 255, 255, 255)), Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap, MaxHeight = 32 };
            headerStack.Children.Add(_inspectorTicketCode); headerStack.Children.Add(_inspectorIssue);
            headerStack.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)), Height = 1, Margin = new Thickness(0, 9, 0, 8) });
            var statusRow = new Grid(); statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusRow.Children.Add(new TextBlock { Text = "Status", FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255)) });
            _inspectorStatus = new TextBlock { Text = "-", FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Right }; Grid.SetColumn(_inspectorStatus, 1); statusRow.Children.Add(_inspectorStatus); headerStack.Children.Add(statusRow);
            header.Child = headerStack; layout.Children.Add(header);

            var tabBar = new Border { Background = BrushFromRgb(240, 253, 250), BorderBrush = BrushFromRgb(204, 251, 241), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(3), Margin = new Thickness(12, 10, 12, 8) };
            var tabPanel = new UniformGrid { Columns = 3 };
            _inspectorTabButtons.Clear();
            _inspectorTabContents.Clear();
            var pairs = new[] { Tuple.Create("Overview", summaryCard), Tuple.Create("Activity", activityCard), Tuple.Create("Workflow", workflowCard) };
            for (var i = 0; i < pairs.Length; i++)
            {
                pairs[i].Item2.Margin = new Thickness(0); pairs[i].Item2.Padding = new Thickness(0); pairs[i].Item2.Background = Brushes.Transparent; pairs[i].Item2.BorderThickness = new Thickness(0); pairs[i].Item2.Effect = null;
                _inspectorTabContents.Add(pairs[i].Item2);
                var button = CreateInspectorTabButton(pairs[i].Item1, i);
                _inspectorTabButtons.Add(button); tabPanel.Children.Add(button);
            }
            tabBar.Child = tabPanel; Grid.SetRow(tabBar, 1); layout.Children.Add(tabBar);

            _inspectorContent = new ContentControl { Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top };
            var contentScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _inspectorContent, Padding = new Thickness(12, 0, 12, 12) };
            Grid.SetRow(contentScroll, 2); layout.Children.Add(contentScroll);
            inspector.Child = layout; SetInspectorTab(0); return inspector;
        }

        private Button CreateInspectorTabButton(string label, int index)
        {
            var button = new Button { Content = label, Tag = index, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(51, 65, 85), Background = Brushes.White, BorderBrush = BrushFromRgb(203, 213, 225), BorderThickness = new Thickness(1), Padding = new Thickness(7, 6, 7, 6), Margin = new Thickness(index == 0 ? 0 : 3, 0, 0, 0), Focusable = true, IsTabStop = true, HorizontalContentAlignment = HorizontalAlignment.Center, Cursor = Cursors.Hand, ToolTip = "Show " + label };
            ApplyInspectorTabTemplate(button);
            button.Click += (_, __) => SetInspectorTab(index);
            button.PreviewKeyDown += (_, e) =>
            {
                var target = -1;
                if (e.Key == Key.Left || e.Key == Key.Up) target = Math.Max(0, index - 1);
                else if (e.Key == Key.Right || e.Key == Key.Down) target = Math.Min(_inspectorTabButtons.Count - 1, index + 1);
                else if (e.Key == Key.Home) target = 0;
                else if (e.Key == Key.End) target = _inspectorTabButtons.Count - 1;
                if (target >= 0 && target != index) { SetInspectorTab(target); _inspectorTabButtons[target].Focus(); e.Handled = true; }
            };
            return button;
        }

        private void SetInspectorTab(int index)
        {
            if (_inspectorContent == null || _inspectorTabContents.Count == 0) return;
            _inspectorTabIndex = Math.Max(0, Math.Min(index, _inspectorTabContents.Count - 1));
            _inspectorContent.Content = _inspectorTabContents[_inspectorTabIndex];
            for (var i = 0; i < _inspectorTabButtons.Count; i++)
            {
                var active = i == _inspectorTabIndex;
                _inspectorTabButtons[i].Background = active ? BrushFromRgb(13, 148, 136) : Brushes.White;
                _inspectorTabButtons[i].Foreground = active ? Brushes.White : BrushFromRgb(51, 65, 85);
                _inspectorTabButtons[i].BorderBrush = active ? BrushFromRgb(13, 148, 136) : BrushFromRgb(203, 213, 225);
            }
        }

        private static void ApplyInspectorTabTemplate(Button button)
        {
            var templateXaml = @"
                <ControlTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" TargetType=""Button"">
                    <Border Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}"" BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""7"" Padding=""{TemplateBinding Padding}"">
                        <ContentPresenter HorizontalAlignment=""{TemplateBinding HorizontalContentAlignment}"" VerticalAlignment=""Center"" />
                    </Border>
                </ControlTemplate>";
            var ctx = new ParserContext(); ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            button.Template = (ControlTemplate)XamlReader.Parse(templateXaml, ctx);
        }

        private async Task SelectCaseAsync(CallTicketListItem ticket) { if (ticket == null || ticket.TicketId <= 0) return; await SelectTicketInternalAsync(ticket); }


        private void RefreshCaseCards()
        {
            if (_caseCardsPanel != null)
            {
                _caseCardsPanel.Children.Clear();
                foreach (var group in new[] { "Active", "Waiting", "Temporary", "Resolved", "Closed" })
                {
                    var rows = _lifecycleRows.Where(row => row.LifecycleGroup == group).ToList();
                    if (rows.Count == 0) continue;
                    var section = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
                    var sectionHeader = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                    sectionHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); sectionHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    sectionHeader.Children.Add(new TextBlock { Text = group, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(15, 118, 110) });
                    var count = new TextBlock { Text = rows.Count + " case" + (rows.Count == 1 ? string.Empty : "s"), FontSize = 10.5, Foreground = BrushFromRgb(100, 116, 139) }; Grid.SetColumn(count, 1); sectionHeader.Children.Add(count); section.Children.Add(sectionHeader);
                    var cards = new WrapPanel { Margin = new Thickness(-3, 0, -3, 0) }; foreach (var row in rows) cards.Children.Add(CreateCaseCard(row)); section.Children.Add(cards); _caseCardsPanel.Children.Add(section);
                }
            }
            RefreshCaseTable();
        }

        private void RefreshCaseTable()
        {
            if (_caseTablePanel == null) return;
            _caseTablePanel.Children.Clear();
            _caseTablePanel.Children.Add(CreateCaseTableHeader());
            foreach (var group in new[] { "Active", "Waiting", "Temporary", "Resolved", "Closed" })
            {
                var rows = SortTableRows(_lifecycleRows.Where(row => row.LifecycleGroup == group)).ToList();
                if (rows.Count > 0) _caseTablePanel.Children.Add(CreateCaseTableGroup(group, rows));
            }
        }

        private FrameworkElement CreateCaseTableHeader()
        {
            var border = new Border { Background = BrushFromRgb(248, 250, 252), BorderBrush = BrushFromRgb(226, 232, 240), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8, 8, 0, 0), Padding = new Thickness(0, 4, 0, 4) };
            var grid = CreateCaseTableGrid();
            var headers = new[] { "Status / SLA risk", "Ticket", "Case / Requester", "Location", "Owner", "Priority", "Age / Idle" };
            for (var i = 0; i < headers.Length; i++) { var header = CreateCaseTableHeaderButton(headers[i]); Grid.SetColumn(header, i); grid.Children.Add(header); }
            var action = new TextBlock { Text = "ACTION", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(100, 116, 139), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(action, 7); grid.Children.Add(action); border.Child = grid; return border;
        }

        private Button CreateCaseTableHeaderButton(string column)
        {
            var active = string.Equals(_tableSortColumn, column, StringComparison.OrdinalIgnoreCase) || (string.Equals(column, "Status / SLA risk", StringComparison.OrdinalIgnoreCase) && string.Equals(_tableSortColumn, "Needs Attention", StringComparison.OrdinalIgnoreCase));
            var button = new Button { Content = active ? column + (_tableSortDescending ? "  ↓" : "  ↑") : column, Tag = column, HorizontalContentAlignment = HorizontalAlignment.Left, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = active ? BrushFromRgb(15, 118, 110) : BrushFromRgb(100, 116, 139), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(12, 7, 8, 7), Cursor = Cursors.Hand };
            ApplyModernButtonTemplate(button, new Thickness(12, 7, 8, 7));
            button.Click += (_, __) => SetTableSort(column);
            return button;
        }

        private FrameworkElement CreateCaseTableGroup(string group, IList<LifecycleTicketRow> rows)
        {
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            var expanded = !_tableGroupExpanded.TryGetValue(group, out var value) || value;
            var toggle = new Button { Background = Brushes.White, BorderBrush = BrushFromRgb(226, 232, 240), BorderThickness = new Thickness(1), Padding = new Thickness(12, 8, 12, 8), HorizontalContentAlignment = HorizontalAlignment.Left, Cursor = Cursors.Hand };
            ApplyModernButtonTemplate(toggle, new Thickness(12, 8, 12, 8));
            var header = new Grid(); header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock { Text = (expanded ? "⌄  " : "›  ") + group, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(15, 118, 110), VerticalAlignment = VerticalAlignment.Center });
            var count = new TextBlock { Text = rows.Count + " case" + (rows.Count == 1 ? string.Empty : "s") + (expanded ? "" : " • collapsed"), FontSize = 10.5, Foreground = BrushFromRgb(100, 116, 139), VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(count, 1); header.Children.Add(count); toggle.Content = header;
            toggle.Click += (_, __) => { _tableGroupExpanded[group] = !expanded; RefreshCaseTable(); };
            section.Children.Add(toggle);
            if (expanded) foreach (var row in rows) section.Children.Add(CreateCaseTableRow(row));
            return section;
        }

        private Border CreateCaseTableRow(LifecycleTicketRow row)
        {
            var ticket = row.Source;
            var selected = _selectedTicket != null && _selectedTicket.TicketId == ticket.TicketId;
            var border = new Border { Height = 64, Background = selected ? BrushFromRgb(240, 253, 250) : Brushes.White, BorderBrush = selected ? BrushFromRgb(13, 148, 136) : BrushFromRgb(226, 232, 240), BorderThickness = new Thickness(selected ? 2 : 1), Padding = new Thickness(0), Cursor = Cursors.Hand, ToolTip = "Open this case in the persistent ticket inspector." };
            border.MouseLeftButtonUp += async (_, e) => { if (!IsClickFromButton(e.OriginalSource as DependencyObject)) await SelectCaseAsync(ticket); };
            var grid = CreateCaseTableGrid();
            var status = CreateCaseStatusSlaCell(row); Grid.SetColumn(status, 0); grid.Children.Add(status);
            var code = CreateCaseTableText(ticket.TicketCode ?? ticket.TicketId.ToString(), 11, FontWeights.Bold, BrushFromRgb(15, 118, 110)); Grid.SetColumn(code, 1); grid.Children.Add(code);
            var caseRequester = CreateCaseTableDetail(ticket.Issue ?? "No issue provided", string.IsNullOrWhiteSpace(ticket.CallerName) ? "No requester" : ticket.CallerName.Trim()); Grid.SetColumn(caseRequester, 2); grid.Children.Add(caseRequester);
            var location = CreateCaseTableDetail(ticket.Branch ?? "No branch", ticket.Department ?? "No department"); Grid.SetColumn(location, 3); grid.Children.Add(location);
            var owner = CreateCaseTableDetail(row.Owner, ticket.AssignedToEmpId.HasValue ? "Assigned" : "Unassigned"); Grid.SetColumn(owner, 4); grid.Children.Add(owner);
            var priority = new Border { Background = GetPriorityBackground(row.Priority), CornerRadius = new CornerRadius(999), Padding = new Thickness(8, 4, 8, 4), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = row.Priority, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = GetPriorityForeground(row.Priority) } }; Grid.SetColumn(priority, 5); grid.Children.Add(priority);
            var idle = DateTime.UtcNow - row.LastActivityUtc; if (idle < TimeSpan.Zero) idle = TimeSpan.Zero;
            var ageIdle = CreateCaseTableDetail(row.Age + " old", FormatShortDuration(idle) + " idle"); Grid.SetColumn(ageIdle, 6); grid.Children.Add(ageIdle);
            var open = CreatePrimaryButton("Open case", BrushFromRgb(13, 148, 136)); open.FontSize = 10; open.Padding = new Thickness(9, 4, 9, 4); open.HorizontalAlignment = HorizontalAlignment.Center; open.VerticalAlignment = VerticalAlignment.Center; open.Click += async (_, __) => await SelectCaseAsync(ticket); Grid.SetColumn(open, 7); grid.Children.Add(open);
            border.Child = grid; return border;
        }

        private FrameworkElement CreateCaseStatusSlaCell(LifecycleTicketRow row)
        {
            var sla = BuildSlaCell(row.Source);
            var stack = new StackPanel { Margin = new Thickness(12, 5, 8, 5), VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = row.ResolutionBadge, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = GetStatusForeground(row.ResolutionBadge), TextTrimming = TextTrimming.CharacterEllipsis });
            if (row.IsForwardedToRepair)
                stack.Children.Add(new TextBlock { Text = "Forwarded to Repair", FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(30, 64, 175), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 0), ToolTip = "Linked Repair Ticket: " + (row.Source.LinkedRepairTicketCode ?? "-") });
            stack.Children.Add(new TextBlock { Text = "SLA · " + sla.Label, FontSize = 9.5, Foreground = GetMetricForeground(sla.State), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = sla.Tooltip, Margin = new Thickness(0, 2, 0, 0) });
            return stack;
        }

        private static FrameworkElement CreateCaseTableText(string text, double fontSize, FontWeight weight, Brush foreground)
        {
            return new TextBlock { Text = text ?? "-", Margin = new Thickness(12, 0, 8, 0), FontSize = fontSize, FontWeight = weight, Foreground = foreground, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        }

        private static FrameworkElement CreateCaseTableDetail(string primary, string secondary)
        {
            var stack = new StackPanel { Margin = new Thickness(12, 5, 8, 5), VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = primary ?? "-", FontSize = 10.8, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(15, 23, 42), TextTrimming = TextTrimming.CharacterEllipsis });
            stack.Children.Add(new TextBlock { Text = secondary ?? "-", FontSize = 9.6, Foreground = BrushFromRgb(100, 116, 139), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 0, 0) });
            return stack;
        }

        private static Grid CreateCaseTableGrid()
        {
            var grid = new Grid { MinWidth = 1120 };
            foreach (var width in new[] { 165d, 100d, 255d, 150d, 145d, 95d, 120d }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            return grid;
        }

        private void SetTableSort(string column)
        {
            var sortColumn = string.Equals(column, "Status / SLA risk", StringComparison.OrdinalIgnoreCase) ? "Needs Attention" : column;
            if (string.Equals(_tableSortColumn, sortColumn, StringComparison.OrdinalIgnoreCase)) _tableSortDescending = !_tableSortDescending;
            else { _tableSortColumn = sortColumn; _tableSortDescending = false; }
            RefreshCaseTable();
        }

        private IEnumerable<LifecycleTicketRow> SortTableRows(IEnumerable<LifecycleTicketRow> rows)
        {
            var source = rows ?? Enumerable.Empty<LifecycleTicketRow>();
            switch (_tableSortColumn)
            {
                case "Ticket": return _tableSortDescending ? source.OrderByDescending(row => row.TicketCode).ThenByDescending(row => row.Source.TicketId) : source.OrderBy(row => row.TicketCode).ThenBy(row => row.Source.TicketId);
                case "Case / Requester": return _tableSortDescending ? source.OrderByDescending(row => row.IssueAndCaller).ThenByDescending(row => row.Source.TicketId) : source.OrderBy(row => row.IssueAndCaller).ThenBy(row => row.Source.TicketId);
                case "Location": return _tableSortDescending ? source.OrderByDescending(row => (row.Source.Branch ?? string.Empty) + " " + (row.Source.Department ?? string.Empty)) : source.OrderBy(row => (row.Source.Branch ?? string.Empty) + " " + (row.Source.Department ?? string.Empty));
                case "Owner": return _tableSortDescending ? source.OrderByDescending(row => row.Owner) : source.OrderBy(row => row.Owner);
                case "Priority": return _tableSortDescending ? source.OrderBy(row => row.PriorityOrder) : source.OrderByDescending(row => row.PriorityOrder);
                case "Age / Idle": return _tableSortDescending ? source.OrderByDescending(GetTableAgeIdleScore) : source.OrderBy(GetTableAgeIdleScore);
                default: return _tableSortDescending ? source.OrderByDescending(GetTableAttentionScore).ThenBy(row => row.PriorityOrder).ThenByDescending(row => row.Source.TicketId) : source.OrderBy(GetTableAttentionScore).ThenByDescending(row => row.PriorityOrder).ThenBy(row => row.Source.TicketId);
            }
        }

        private static double GetTableAgeIdleScore(LifecycleTicketRow row)
        {
            var idle = DateTime.UtcNow - row.LastActivityUtc; if (idle < TimeSpan.Zero) idle = TimeSpan.Zero;
            return Math.Max(0, row.Source.TicketAgeDays) * 10000d + idle.TotalHours;
        }

        private static int GetTableAttentionScore(LifecycleTicketRow row)
        {
            var sla = BuildSlaCell(row.Source).State;
            var risk = string.Equals(sla, "Bad", StringComparison.OrdinalIgnoreCase) ? 4 : string.Equals(sla, "Warn", StringComparison.OrdinalIgnoreCase) ? 3 : string.Equals(sla, "Watch", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
            return risk * 1000000 + (4 - row.PriorityOrder) * 10000 + Math.Min(9999, Math.Max(0, row.Source.TicketAgeDays) * 100 + row.Source.IdleDays);
        }

        private static bool IsClickFromButton(DependencyObject source)
        {
            for (var current = source; current != null; current = VisualTreeHelper.GetParent(current)) if (current is Button) return true;
            return false;
        }        private Border CreateCaseCard(LifecycleTicketRow row)
        {
            var ticket = row.Source; var compact = row.LifecycleGroup == "Resolved" || row.LifecycleGroup == "Closed"; var selected = _selectedTicket != null && _selectedTicket.TicketId == ticket.TicketId;
            var card = new Border { Width = 330, Height = compact ? 82 : 124, Margin = new Thickness(3, 0, 9, 9), Padding = new Thickness(13, compact ? 10 : 11, 13, compact ? 10 : 11), Background = selected ? BrushFromRgb(240, 253, 250) : Brushes.White, BorderBrush = selected ? BrushFromRgb(13, 148, 136) : BrushFromRgb(226, 232, 240), BorderThickness = new Thickness(selected ? 2 : 1), CornerRadius = new CornerRadius(9), Cursor = Cursors.Hand, ToolTip = "Select this case in the persistent ticket inspector." };
            card.MouseLeftButtonUp += async (_, __) => await SelectTicketInternalAsync(ticket);
            var layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.Children.Add(new TextBlock { Text = ticket.TicketCode ?? ticket.TicketId.ToString(), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(15, 118, 110), VerticalAlignment = VerticalAlignment.Center });
            int nextCol = 1;
            if (row.IsFieldVisitCompleted)
            {
                var fieldBadge = new Border { Background = BrushFromRgb(220, 252, 231), CornerRadius = new CornerRadius(8), Padding = new Thickness(7, 3, 7, 3), Margin = new Thickness(0, 0, 5, 0), ToolTip = "Field visit completed for this ticket", Child = new TextBlock { Text = "Field Visit ✓", FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(22, 101, 52) } };
                Grid.SetColumn(fieldBadge, nextCol++);
                top.Children.Add(fieldBadge);
            }
            if (row.IsForwardedToRepair)
            {
                var forwarded = new Border { Background = BrushFromRgb(219, 234, 254), CornerRadius = new CornerRadius(8), Padding = new Thickness(7, 3, 7, 3), Margin = new Thickness(0, 0, 5, 0), ToolTip = "Linked Repair Ticket: " + (ticket.LinkedRepairTicketCode ?? "-"), Child = new TextBlock { Text = "Forwarded to Repair", FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(30, 64, 175) } };
                Grid.SetColumn(forwarded, nextCol++);
                top.Children.Add(forwarded);
            }
            var status = new Border { Background = GetStatusBackground(row.ResolutionBadge), CornerRadius = new CornerRadius(8), Padding = new Thickness(7, 3, 7, 3), Child = new TextBlock { Text = row.ResolutionBadge, FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = GetStatusForeground(row.ResolutionBadge) } };
            Grid.SetColumn(status, nextCol);
            top.Children.Add(status);
            layout.Children.Add(top);
            var issue = new TextBlock { Text = ticket.Issue ?? "No issue provided", FontSize = compact ? 12 : 13, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(15, 23, 42), TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = compact ? 32 : 38, Margin = new Thickness(0, compact ? 5 : 7, 0, 3) }; Grid.SetRow(issue, 1); layout.Children.Add(issue);
            var footer = new Grid { VerticalAlignment = VerticalAlignment.Bottom }; footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); var meta = compact ? (ticket.Branch ?? "No branch") + " • " + FormatRelativeTime(ticket.UpdatedAt) : (string.IsNullOrWhiteSpace(ticket.CallerName) ? "No caller" : ticket.CallerName.Trim()) + " • " + (string.IsNullOrWhiteSpace(ticket.ResponsiblePerson) ? "Unassigned" : ticket.ResponsiblePerson.Trim()) + " • " + row.Age; footer.Children.Add(new TextBlock { Text = meta, FontSize = 10, Foreground = BrushFromRgb(100, 116, 139), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }); var open = CreatePrimaryButton("Open case", BrushFromRgb(13, 148, 136)); open.FontSize = 10.5; open.Padding = new Thickness(9, 4, 9, 4); open.Margin = new Thickness(6, 0, 0, 0); open.Click += async (_, __) => await SelectCaseAsync(ticket); Grid.SetColumn(open, 1); footer.Children.Add(open); Grid.SetRow(footer, 2); layout.Children.Add(footer); card.Child = layout; return card;
        }

        private void ShowCreateTicketModal(Border createTicketContainer)
        {
            if (_createWindowOpen)
            {
                return;
            }

            // The form is reused for each New Ticket dialog. Remove it from the prior
            // dialog's ScrollViewer before assigning it to the new modal host.
            DetachFromParent(createTicketContainer);
            _createWindowOpen = true;

            var win = new Window
            {
                Title = "Create Ticket",
                Width = 750,
                Height = 720,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = BrushFromRgb(241, 244, 247),
                Content = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = createTicketContainer
                }
            };
            win.Closed += (_, __) =>
            {
                DetachFromParent(createTicketContainer);
                win.Content = null;
                _createWindowOpen = false;
            };
            win.ShowDialog();
        }
        private async Task ReloadLifecycleQueueAsync(int? preferredTicketId)
        {
            SetLifecycleState("Loading cases…", false);
            try
            {
                var tickets = await _repository.GetTicketsAsync("All", (_searchBox.Text ?? string.Empty).Trim(), maxRows: 5000) ?? new List<CallTicketListItem>();
                try
                {
                    var linkedRepairCodes = await new RepairTicketRepository()
                        .GetLinkedRepairTicketCodesByCallTicketIdsAsync(tickets.Where(ticket => ticket != null).Select(ticket => ticket.TicketId));
                    foreach (var ticket in tickets.Where(ticket => ticket != null))
                    {
                        if (linkedRepairCodes.TryGetValue(ticket.TicketId, out var repairTicketCode))
                            ticket.LinkedRepairTicketCode = string.IsNullOrWhiteSpace(repairTicketCode) ? "Linked Repair Ticket" : repairTicketCode;
                    }
                }
                catch
                {
                    // The link migration may not yet be deployed. Ticket loading remains
                    // available; the forwarding badge appears once the link is readable.
                }

                var fieldVisitStatusMap = new Dictionary<int, string>();
                try
                {
                    var allIds = tickets.Where(t => t != null).Select(t => t.TicketId).Distinct().ToList();
                    if (allIds.Count > 0)
                    {
                        using (var conn = new System.Data.SqlClient.SqlConnection(_repository.ConnectionString))
                        {
                            await conn.OpenAsync();
                            const int chunkSize = 1000;
                            for (int i = 0; i < allIds.Count; i += chunkSize)
                            {
                                var chunk = allIds.Skip(i).Take(chunkSize).ToList();
                                var paramNames = chunk.Select((_, idx) => "@id" + (i + idx)).ToArray();
                                var sql = "SELECT TicketId, Status FROM dbo.CallFieldVisit WHERE TicketId IN (" + string.Join(",", paramNames) + ")";
                                using (var cmd = new System.Data.SqlClient.SqlCommand(sql, conn))
                                {
                                    for (int j = 0; j < chunk.Count; j++) cmd.Parameters.AddWithValue(paramNames[j], chunk[j]);
                                    using (var reader = await cmd.ExecuteReaderAsync())
                                    {
                                        while (await reader.ReadAsync())
                                        {
                                            int tid = reader.GetInt32(0);
                                            string st = reader.IsDBNull(1) ? null : reader.GetString(1);
                                            if (!fieldVisitStatusMap.ContainsKey(tid))
                                                fieldVisitStatusMap[tid] = st;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Field visit badge is optional; ignore failures
                }

                IEnumerable<CallTicketListItem> filtered = tickets.Where(ticket => ticket != null && !IsUntriagedPortalTicket(ticket));
                var status = (_statusFilter.SelectedItem as string) ?? "All";
                var assignee = _assigneeFilter.SelectedItem as LookupItem;
                var branch = _branchFilter.SelectedItem as LookupItem;
                if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase)) filtered = filtered.Where(ticket => MatchesLifecycleStatusFilter(ticket, status));
                if (assignee != null && assignee.Id == 0) filtered = filtered.Where(ticket => !ticket.AssignedToEmpId.HasValue && string.IsNullOrWhiteSpace(ticket.ResponsiblePerson));
                else if (assignee != null && assignee.Id > 0) filtered = filtered.Where(ticket => ticket.AssignedToEmpId == assignee.Id || string.Equals(ticket.ResponsiblePerson, assignee.Name, StringComparison.OrdinalIgnoreCase));
                if (branch != null && branch.Id > 0) filtered = filtered.Where(ticket => ticket.BranchId == branch.Id);
                _lifecycleRows.Clear(); _lifecycleRows.AddRange(ApplyLifecycleScope(filtered).Select(t => CreateLifecycleRow(t, fieldVisitStatusMap)).OrderBy(row => row.LifecycleOrder).ThenBy(row => row.PriorityOrder).ThenByDescending(row => row.LastActivityUtc).ThenByDescending(row => row.Source.TicketId)); _queueSummaryText.Text = _lifecycleRows.Count + " case" + (_lifecycleRows.Count == 1 ? string.Empty : "s") + " • Active → Waiting → Temporary → Resolved → Closed"; SetLifecycleState(_lifecycleRows.Count == 0 ? "No cases match the current filters." : string.Empty, false); RefreshCaseCards();
                var target = preferredTicketId.HasValue ? _lifecycleRows.FirstOrDefault(row => row.Source.TicketId == preferredTicketId.Value) : _selectedTicket == null ? null : _lifecycleRows.FirstOrDefault(row => row.Source.TicketId == _selectedTicket.TicketId); if (target != null) await SelectTicketInternalAsync(target.Source); else if (_lifecycleRows.Count > 0) await SelectTicketInternalAsync(_lifecycleRows[0].Source); else UpdateSelection(null);
            }
            catch (Exception ex) { _lifecycleRows.Clear(); RefreshCaseCards(); _queueSummaryText.Text = "Cases unavailable"; SetLifecycleState("Unable to load cases. " + ex.GetBaseException().Message, true); UpdateSelection(null); }
        }
        private IEnumerable<CallTicketListItem> ApplyLifecycleScope(IEnumerable<CallTicketListItem> tickets)
        {
            switch (_lifecycleScope) { case "Active": return tickets.Where(ticket => GetLifecycleGroup(ticket) == "Active"); case "Waiting": return tickets.Where(ticket => GetLifecycleGroup(ticket) == "Waiting"); case "Temporary": return tickets.Where(ticket => GetLifecycleGroup(ticket) == "Temporary"); case "Resolved": return tickets.Where(ticket => GetLifecycleGroup(ticket) == "Resolved"); case "Closed": return tickets.Where(ticket => GetLifecycleGroup(ticket) == "Closed"); default: return tickets; }
        }
        private bool MatchesLifecycleStatusFilter(CallTicketListItem ticket, string status)
        {
            if (ticket == null || string.IsNullOrWhiteSpace(status) || string.Equals(status, "All", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(status, "Overdue", StringComparison.OrdinalIgnoreCase)) return IsTicketOverdue(ticket);
            return string.Equals(NormalizeLegacyTicketStatus(ticket.Status), status.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        private static bool IsUntriagedPortalTicket(CallTicketListItem ticket) => ticket != null && string.Equals(ticket.TicketSource, "Portal", StringComparison.OrdinalIgnoreCase) && string.Equals(ticket.Status, "Pending", StringComparison.OrdinalIgnoreCase) && !ticket.AssignedToEmpId.HasValue;
        private static string GetLifecycleGroup(CallTicketListItem ticket) { var status = (ticket?.Status ?? string.Empty).Trim(); if (status.Equals("Closed", StringComparison.OrdinalIgnoreCase)) return "Closed"; if (status.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)) return "Temporary"; if (status.Equals("Solved", StringComparison.OrdinalIgnoreCase)) return "Resolved"; if (status.StartsWith("Waiting", StringComparison.OrdinalIgnoreCase)) return "Waiting"; return "Active"; }
        private static int GetLifecycleOrder(string group) => group == "Active" ? 1 : group == "Waiting" ? 2 : group == "Temporary" ? 3 : group == "Resolved" ? 4 : 5;
        private static int GetPriorityOrder(string priority) => string.Equals(priority, "Critical", StringComparison.OrdinalIgnoreCase) ? 0 : string.Equals(priority, "High", StringComparison.OrdinalIgnoreCase) ? 1 : string.Equals(priority, "Medium", StringComparison.OrdinalIgnoreCase) ? 2 : string.Equals(priority, "Low", StringComparison.OrdinalIgnoreCase) ? 3 : 4;
        private static string GetLifecycleResolutionBadge(CallTicketListItem ticket, bool isForwardedToRepair)
        {
            var status = NormalizeLegacyTicketStatus(ticket?.Status);
            if (!isForwardedToRepair)
                return status;

            if (string.Equals(status, "Solved", StringComparison.OrdinalIgnoreCase))
                return "Solved";
            if (string.Equals(status, "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase))
                return "Temporarily Resolved";
            return "Not Solved";
        }

        private LifecycleTicketRow CreateLifecycleRow(CallTicketListItem ticket, Dictionary<int, string> fieldVisitStatusMap = null)
        {
            var group = GetLifecycleGroup(ticket);
            var status = IsTicketOverdue(ticket) ? "Overdue" : NormalizeLegacyTicketStatus(ticket.Status);
            var isForwardedToRepair = !string.IsNullOrWhiteSpace(ticket.LinkedRepairTicketCode);
            string fieldStatus = null;
            if (fieldVisitStatusMap != null && fieldVisitStatusMap.TryGetValue(ticket.TicketId, out var fv)) fieldStatus = fv;
            return new LifecycleTicketRow
            {
                Source = ticket,
                LifecycleGroup = group,
                LifecycleOrder = GetLifecycleOrder(group),
                PriorityOrder = GetPriorityOrder(ticket.Priority),
                TicketCode = ticket.TicketCode ?? ticket.TicketId.ToString(),
                IssueAndCaller = (ticket.Issue ?? "No issue provided") + " " + (ticket.CallerName ?? string.Empty),
                Priority = string.IsNullOrWhiteSpace(ticket.Priority) ? "Medium" : ticket.Priority.Trim(),
                Owner = string.IsNullOrWhiteSpace(ticket.ResponsiblePerson) ? "Unassigned" : ticket.ResponsiblePerson.Trim(),
                Age = Math.Max(0, ticket.TicketAgeDays) + "d",
                Status = status,
                ResolutionBadge = GetLifecycleResolutionBadge(ticket, isForwardedToRepair),
                IsForwardedToRepair = isForwardedToRepair,
                FieldVisitStatus = fieldStatus,
                IsFieldVisitCompleted = string.Equals(fieldStatus, "Completed", StringComparison.OrdinalIgnoreCase),
                LastActivityUtc = GetLastActivityUtc(ticket, out _)
            };
        }
        private void SetLifecycleState(string message, bool isError) { if (_lifecycleStateText == null) return; _lifecycleStateText.Text = message ?? string.Empty; _lifecycleStateText.Foreground = isError ? BrushFromRgb(153, 27, 27) : BrushFromRgb(100, 116, 139); _lifecycleStateText.Background = isError ? BrushFromRgb(254, 242, 242) : Brushes.White; _lifecycleStateText.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible; }
        private static void DetachFromParent(FrameworkElement element)
        {
            if (element == null) return;
            if (element.Parent is Panel panel) panel.Children.Remove(element);
            else if (element.Parent is ContentControl content && ReferenceEquals(content.Content, element)) content.Content = null;
        }

        private static Border BuildHero()
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(28),
                Padding = new Thickness(28),
                Background = new LinearGradientBrush(
                    Color.FromRgb(14, 116, 144),
                    Color.FromRgb(30, 64, 175),
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

            var stack = new StackPanel();
            card.Child = stack;
            stack.Children.Add(new TextBlock
            {
                Text = "Ticket Management",
                FontSize = 30,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });
            stack.Children.Add(new TextBlock
            {
                Text = "A WPF ticket queue workspace for filtering, reviewing, and opening live call tickets without losing the working actions from your original system.",
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(230, 238, 242, 255))
            });
            return card;
        }

        private static FrameworkElement BuildPendingSection(out DataGrid grid, out TextBlock pager, out Button prev, out Button next, out TextBlock stateText)
        {
            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = CreateSectionHeader("Pending Tickets", "Working queue with overdue and SLA visibility.");
            Grid.SetRow(header, 0);
            layout.Children.Add(header);

            grid = CreateTicketGrid();
            grid.MaxHeight = 520;
            AddPendingColumns(grid);
            Grid.SetRow(grid, 1);
            layout.Children.Add(grid);

            stateText = CreateGridStateText();
            Grid.SetRow(stateText, 2);
            layout.Children.Add(stateText);

            var pagerWrap = BuildPager(out pager, out prev, out next);
            Grid.SetRow(pagerWrap, 3);
            layout.Children.Add(pagerWrap);

            return layout;
        }

        private static FrameworkElement BuildSolvedSection(out DataGrid grid, out TextBlock pager, out Button prev, out Button next, out TextBlock stateText)
        {
            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // grid — Auto so it only takes what it needs
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // state
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // pager

            var header = CreateSectionHeader("Recently Resolved Tickets", "Closed or resolved items for quick review.");
            Grid.SetRow(header, 0);
            layout.Children.Add(header);

            grid = CreateTicketGrid();
            grid.MaxHeight = 220; // cap height so it doesn't push Notes/History down
            AddSolvedColumns(grid);
            Grid.SetRow(grid, 1);
            layout.Children.Add(grid);

            stateText = CreateGridStateText();
            Grid.SetRow(stateText, 2);
            layout.Children.Add(stateText);

            var pagerWrap = BuildPager(out pager, out prev, out next);
            Grid.SetRow(pagerWrap, 3);
            layout.Children.Add(pagerWrap);

            return layout;
        }

        private static FrameworkElement BuildSelectionDetails(
            out TextBlock headline, out TextBlock subhead, out TextBlock status, out TextBlock priority,
            out TextBlock assignee, out TextBlock caller, out TextBlock location, out TextBlock activity,
            out TextBlock created, out TextBlock escalation, out TextBlock lastEmail, out FrameworkElement escalationHost,
            out FrameworkElement lastEmailHost, out Button detailsButton, out Button summaryButton,
            out Button deleteButton)
        {
            var stack = new StackPanel { Margin = new Thickness(12, 10, 12, 12) };
            var title = new StackPanel { Margin = new Thickness(0, 0, 0, 9) };
            title.Children.Add(new TextBlock { Text = "OVERVIEW", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(15, 118, 110) });
            headline = new TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(15, 23, 42), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 3, 0, 0) };
            subhead = new TextBlock { FontSize = 10.8, Foreground = BrushFromRgb(100, 116, 139), TextWrapping = TextWrapping.Wrap, MaxHeight = 34, Margin = new Thickness(0, 2, 0, 0) };
            title.Children.Add(headline); title.Children.Add(subhead); stack.Children.Add(title);

            stack.Children.Add(CreateCompactInspectorSection("Requester / location", out caller, "Requester", out location, "Location"));
            stack.Children.Add(CreateCompactInspectorSection("Ownership / timing", out assignee, "Owner", out created, "Created"));
            stack.Children.Add(CreateCompactInspectorFact("Last activity", out activity));
            escalationHost = CreateCompactInspectorFact("Escalation", out escalation);
            escalationHost.Visibility = Visibility.Collapsed;
            stack.Children.Add(escalationHost);
            lastEmailHost = CreateCompactInspectorFact("Last email", out lastEmail);
            lastEmailHost.Visibility = Visibility.Collapsed;
            stack.Children.Add(lastEmailHost);

            var priorityRow = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            priorityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            priorityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            priorityRow.Children.Add(CreateCompactInspectorChip("STATUS", out status));
            var priorityChip = CreateCompactInspectorChip("PRIORITY", out priority);
            Grid.SetColumn(priorityChip, 1); priorityRow.Children.Add(priorityChip); stack.Children.Add(priorityRow);

            var actions = new WrapPanel { Margin = new Thickness(0, 1, 0, 0) };
            detailsButton = CreateCompactActionButton("Full details", BrushFromRgb(13, 148, 136));
            summaryButton = CreateCompactActionButton("Summary", BrushFromRgb(71, 85, 105));
            deleteButton = CreateCompactActionButton("Delete", BrushFromRgb(220, 38, 38));
            actions.Children.Add(detailsButton); actions.Children.Add(summaryButton); actions.Children.Add(deleteButton); stack.Children.Add(actions);
            return stack;
        }
        private static FrameworkElement BuildCreateTicketSection(
            out ComboBox companyCombo,
            out ComboBox departmentCombo,
            out ComboBox branchCombo,
            out ComboBox callerCombo,
            out ComboBox assignedToCombo,
            out TextBox issueBox,
            out TextBox notesBox,
            out TextBlock statusText,
            out Button addCallerButton,
            out Button createButton,
            out Button clearButton,
            out CheckBox backdateCheck,
            out DatePicker backdateDatePicker,
            out TextBox backdateTimeText,
            out TextBlock escalationSummary,
            out Button escalationButton,
            out Button escalationClearButton)
        {
            var layout = new Grid
            {
                VerticalAlignment = VerticalAlignment.Stretch
            };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = CreateSectionHeader("Create Ticket", "Create live call tickets directly from the WPF ticket list workspace.");
            Grid.SetRow(header, 0);
            layout.Children.Add(header);

            var topFieldsGrid = CreateFormGrid(2, 3);
            topFieldsGrid.Margin = new Thickness(0, 4, 0, 0);
            Grid.SetRow(topFieldsGrid, 1);
            layout.Children.Add(topFieldsGrid);

            companyCombo = CreateLookupComboBox();
            departmentCombo = CreateLookupComboBox();
            branchCombo = CreateLookupComboBox();
            assignedToCombo = CreateLookupComboBox();
            var caller = CreateLookupComboBox();
            caller.IsEditable = true;
            caller.IsTextSearchEnabled = false;
            caller.StaysOpenOnEdit = true;
            caller.DisplayMemberPath = "Name";
            callerCombo = caller;

            addCallerButton = CreateInlineActionButton("+ Add Employee", BrushFromRgb(13, 148, 136));
            var callerField = new Grid();
            callerField.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            callerField.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            callerField.Children.Add(caller);
            Grid.SetColumn(addCallerButton, 1);
            addCallerButton.Margin = new Thickness(10, 0, 0, 0);
            callerField.Children.Add(addCallerButton);

            AddFormField(topFieldsGrid, 0, 0, "Company", companyCombo);
            AddFormField(topFieldsGrid, 0, 1, "Caller Name", callerField);
            AddFormField(topFieldsGrid, 1, 0, "Department", departmentCombo);
            AddFormField(topFieldsGrid, 1, 1, "Branch", branchCombo);
            AddFormField(topFieldsGrid, 2, 0, "Assigned To", assignedToCombo);

            // Backdate (backlog) controls — styled as a subtle card for visual grouping
            backdateCheck = new CheckBox
            {
                Content = "Backdate from logbook",
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2),
                FontSize = 11.5,
                Foreground = BrushFromRgb(71, 85, 105)
            };

            var backdateHint = new TextBlock
            {
                Text = "Set the date and time the issue was originally reported.",
                FontSize = 10.5,
                Foreground = BrushFromRgb(148, 163, 184),
                Margin = new Thickness(0, 2, 0, 6),
                TextWrapping = TextWrapping.Wrap
            };

            backdateDatePicker = new DatePicker
            {
                SelectedDate = DateTime.Today,
                Margin = new Thickness(0, 0, 10, 0),
                Width = 150,
                IsEnabled = false
            };

            backdateTimeText = new TextBox
            {
                Text = DateTime.Now.ToString("HH:mm"),
                Width = 90,
                Margin = new Thickness(0),
                IsEnabled = false,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 4, 6, 4)
            };

            var dateTimeLabel = new TextBlock
            {
                Text = "Date & Time",
                FontSize = 11,
                Foreground = BrushFromRgb(100, 116, 139),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var dateTimeRow = new StackPanel { Orientation = Orientation.Horizontal };
            dateTimeRow.Children.Add(backdateDatePicker);
            dateTimeRow.Children.Add(backdateTimeText);

            // Escalation controls — placed top-right inside the backdate card
            escalationSummary = new TextBlock
            {
                FontSize = 11,
                Foreground = BrushFromRgb(100, 116, 139),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            escalationButton = CreateInlineActionButton("Set escalation", BrushFromRgb(230, 126, 34));
            escalationClearButton = CreateInlineActionButton("Clear", BrushFromRgb(149, 165, 166));
            escalationClearButton.Visibility = Visibility.Collapsed;
            escalationClearButton.Margin = new Thickness(6, 0, 0, 0);

            var escalationRow = new StackPanel { Orientation = Orientation.Horizontal };
            escalationRow.Children.Add(escalationSummary);
            escalationRow.Children.Add(escalationButton);
            escalationRow.Children.Add(escalationClearButton);

            // Top row: checkbox left | escalation right
            var topRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(backdateCheck, 0);
            topRow.Children.Add(backdateCheck);
            Grid.SetColumn(escalationRow, 1);
            topRow.Children.Add(escalationRow);

            var backdateInner = new StackPanel { Orientation = Orientation.Vertical };
            backdateInner.Children.Add(topRow);
            backdateInner.Children.Add(backdateHint);
            backdateInner.Children.Add(dateTimeLabel);
            backdateInner.Children.Add(dateTimeRow);

            var backdatePanel = new Border
            {
                Background = BrushFromRgb(241, 245, 249),
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 10, 0, 6)
            };
            backdatePanel.Child = backdateInner;

            // Local copies required because C# does not allow out parameters inside lambdas
            var datePickerLocal = backdateDatePicker;
            var timeTextLocal = backdateTimeText;
            backdateCheck.Checked += (s, e) =>
            {
                datePickerLocal.IsEnabled = true;
                timeTextLocal.IsEnabled = true;
            };
            backdateCheck.Unchecked += (s, e) =>
            {
                datePickerLocal.IsEnabled = false;
                timeTextLocal.IsEnabled = false;
            };

            Grid.SetRow(backdatePanel, 2);
            layout.Children.Add(backdatePanel);

            statusText = new TextBlock
            {
                Margin = new Thickness(0),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = BrushFromRgb(100, 116, 139)
            };
            var statusPanel = CreateMutedInfoPanel(statusText);
            Grid.SetRow(statusPanel, 3);
            layout.Children.Add(statusPanel);

            issueBox = CreateMultilineTextBox("Technical Problem");
            issueBox.MinHeight = 128;
            notesBox = CreateMultilineTextBox("Initial Troubleshooting / Notes");
            notesBox.MinHeight = 128;

            var textAreasGrid = CreateFormGrid(2, 1);
            textAreasGrid.Margin = new Thickness(0, 18, 0, 0);
            AddFormField(textAreasGrid, 0, 0, "Technical Problem", issueBox);
            AddFormField(textAreasGrid, 0, 1, "Initial Troubleshooting / Notes", notesBox);
            Grid.SetRow(textAreasGrid, 4);
            layout.Children.Add(textAreasGrid);

            var actions = new WrapPanel
            {
                Margin = new Thickness(0, 18, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            createButton = CreatePrimaryButton("Create Ticket", BrushFromRgb(37, 99, 235));
            clearButton = CreatePrimaryButton("Clear Form", BrushFromRgb(71, 85, 105));
            actions.Children.Add(createButton);
            actions.Children.Add(clearButton);
            Grid.SetRow(actions, 6);
            layout.Children.Add(actions);
            return layout;
        }

        private static FrameworkElement BuildPendingCaseDetailsSection(
            out TextBlock headline, out ComboBox statusCombo, out ComboBox priorityCombo, out ComboBox assigneeCombo,
            out TextBox problemBox, out TextBox solutionBox, out Button assignToMeButton, out Button reassignButton,
            out Button updateButton, out Button reopenButton, out Button markAsButton, out Button fieldWorkButton, out Button returnTempButton,
            out Button syncSetButton, out Button escalationOverrideButton)
        {
            var stack = new StackPanel { Margin = new Thickness(14, 12, 14, 14) };
            stack.Children.Add(new TextBlock { Text = "WORKFLOW", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(15, 118, 110) });
            headline = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(51, 65, 85), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 10) };
            stack.Children.Add(headline);

            var fields = CreateFormGrid(2, 2);
            statusCombo = CreateTextComboBox();
            foreach (var item in new[] { "Pending", "In Progress", "Escalated", "Forwarded to Repair", "Resolved (Temporary)", "Solved", "Reopened" }) statusCombo.Items.Add(item);
            priorityCombo = CreateTextComboBox();
            foreach (var item in new[] { "Low", "Medium", "High", "Critical" }) priorityCombo.Items.Add(item);
            assigneeCombo = CreateLookupComboBox();
            AddCompactFormField(fields, 0, 0, "Status", statusCombo);
            AddCompactFormField(fields, 0, 1, "Priority", priorityCombo);
            AddCompactFormField(fields, 1, 0, "Assignment", assigneeCombo);
            var assignmentActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(10, 16, 0, 0) };
            assignToMeButton = CreateCompactActionButton("Assign to me", BrushFromRgb(13, 148, 136));
            reassignButton = CreateCompactActionButton("Reassign", BrushFromRgb(124, 58, 237));
            assignmentActions.Children.Add(assignToMeButton); assignmentActions.Children.Add(reassignButton);
            Grid.SetRow(assignmentActions, 1); Grid.SetColumn(assignmentActions, 1); fields.Children.Add(assignmentActions);
            stack.Children.Add(fields);

            problemBox = CreateMultilineTextBox("Problem description"); problemBox.IsReadOnly = true; problemBox.MinHeight = 58; problemBox.MaxHeight = 82; problemBox.Padding = new Thickness(8, 6, 8, 6);
            solutionBox = CreateMultilineTextBox("Add a note or update summary"); solutionBox.MinHeight = 58; solutionBox.MaxHeight = 82; solutionBox.Padding = new Thickness(8, 6, 8, 6);
            var notes = CreateFormGrid(1, 2);
            AddCompactFormField(notes, 0, 0, "Problem", problemBox);
            AddCompactFormField(notes, 1, 0, "Solution / update note", solutionBox);
            stack.Children.Add(notes);

            var core = new WrapPanel { Margin = new Thickness(0, 12, 0, 8) };
            updateButton = CreateCompactActionButton("Update", BrushFromRgb(37, 99, 235));
            markAsButton = CreateCompactActionButton("Mark as…", BrushFromRgb(71, 85, 105));
            // Field Work — borrows Repair Portal identity (blue accent + card shadow) for visual consistency across portals
            fieldWorkButton = CreateCompactActionButton("Field Work", BrushFromRgb(14, 116, 144));
            fieldWorkButton.ToolTip = "Schedule or manage the on-site visit for the selected ticket (one visit per ticket)";
            core.Children.Add(updateButton); core.Children.Add(markAsButton); core.Children.Add(fieldWorkButton); stack.Children.Add(core);

            var advanced = new Expander { Header = "Advanced actions", IsExpanded = false, Foreground = BrushFromRgb(15, 118, 110), FontSize = 11.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 0) };
            var advancedPanel = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            reopenButton = CreateCompactActionButton("Reopen", BrushFromRgb(220, 38, 38));
            returnTempButton = CreateCompactActionButton("Return temp", BrushFromRgb(124, 58, 237));
            syncSetButton = CreateCompactActionButton("Sync set", BrushFromRgb(22, 163, 74));
            escalationOverrideButton = CreateCompactActionButton("Escalation override", BrushFromRgb(234, 88, 12));
            advancedPanel.Children.Add(reopenButton); advancedPanel.Children.Add(returnTempButton); advancedPanel.Children.Add(syncSetButton); advancedPanel.Children.Add(escalationOverrideButton);
            advanced.Content = advancedPanel; stack.Children.Add(advanced);
            return stack;
        }
        private static FrameworkElement BuildActivityTimeline(out ListBox list)
        {
            var stack = new StackPanel { Margin = new Thickness(14, 12, 14, 14) };
            stack.Children.Add(new TextBlock { Text = "ACTIVITY", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(15, 118, 110) });
            stack.Children.Add(new TextBlock { Text = "Chronological notes and workflow history", FontSize = 11, Foreground = BrushFromRgb(100, 116, 139), Margin = new Thickness(0, 3, 0, 10) });
            list = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent, ItemContainerStyle = BuildListBoxItemStyle(), ItemTemplate = CreateCompactActivityTemplate() };
            // The inspector tab owns the only scroll viewer; this list is deliberately non-scrolling.
            ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            stack.Children.Add(list);
            return stack;
        }
        private static Border CreateCompactInspectorSection(string title, out TextBlock firstValue, string firstLabel, out TextBlock secondValue, string secondLabel)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) }; grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
            var card = new Border { Background = BrushFromRgb(248, 250, 252), BorderBrush = BrushFromRgb(226, 232, 240), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(10), Child = grid };
            firstValue = AddCompactValue(grid, 0, firstLabel); secondValue = AddCompactValue(grid, 1, secondLabel); return card;
        }
        private static Border CreateCompactInspectorFact(string title, out TextBlock value)
        {
            var stack = new StackPanel(); stack.Children.Add(new TextBlock { Text = title.ToUpperInvariant(), FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(100, 116, 139) });
            value = new TextBlock { Text = "-", FontSize = 11, Foreground = BrushFromRgb(51, 65, 85), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) }; stack.Children.Add(value);
            return new Border { Background = BrushFromRgb(248, 250, 252), BorderBrush = BrushFromRgb(226, 232, 240), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 8), Child = stack };
        }
        private static Border CreateCompactInspectorChip(string label, out TextBlock value)
        {
            var stack = new StackPanel(); stack.Children.Add(new TextBlock { Text = label, FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(100, 116, 139) });
            var chip = new Border { Background = BrushFromRgb(241, 245, 249), CornerRadius = new CornerRadius(999), Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 4, 8, 0) };
            value = new TextBlock { Text = "-", FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(71, 85, 105) }; chip.Child = value; stack.Children.Add(chip);
            return new Border { Child = stack, Margin = new Thickness(0, 0, 8, 0) };
        }
        private static TextBlock AddCompactValue(Grid grid, int column, string label)
        {
            var stack = new StackPanel { Margin = new Thickness(column == 0 ? 0 : 10, 0, 0, 0) }; stack.Children.Add(new TextBlock { Text = label.ToUpperInvariant(), FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(100, 116, 139) });
            var value = new TextBlock { Text = "-", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(15, 23, 42), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) }; stack.Children.Add(value); Grid.SetColumn(stack, column); grid.Children.Add(stack); return value;
        }
        private static void AddCompactFormField(Grid grid, int row, int column, string label, FrameworkElement control)
        {
            var stack = new StackPanel { Margin = new Thickness(column > 0 ? 8 : 0, row > 0 ? 8 : 0, 0, 0) }; stack.Children.Add(new TextBlock { Text = label, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(71, 85, 105), Margin = new Thickness(0, 0, 0, 4) }); stack.Children.Add(control); Grid.SetRow(stack, row); Grid.SetColumn(stack, column); grid.Children.Add(stack);
        }
        private static Button CreateCompactActionButton(string text, Brush background)
        {
            var button = CreatePrimaryButton(text, background); button.FontSize = 10.5; button.Padding = new Thickness(10, 6, 10, 6); button.Margin = new Thickness(0, 0, 6, 6); return button;
        }
        private static DataTemplate CreateCompactActivityTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.PaddingProperty, new Thickness(9)); border.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 7)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8)); border.SetValue(Border.BackgroundProperty, Brushes.White); border.SetValue(Border.BorderBrushProperty, BrushFromRgb(226, 232, 240)); border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            var stack = new FrameworkElementFactory(typeof(StackPanel)); var top = new FrameworkElementFactory(typeof(DockPanel)); top.SetValue(DockPanel.LastChildFillProperty, true); var time = CreateBoundText("TimestampText", 9.5, FontWeights.Normal, null, BrushFromRgb(100, 116, 139)); time.SetValue(DockPanel.DockProperty, Dock.Right); top.AppendChild(time); var badge = CreateBoundText("TypeLabel", 10, FontWeights.SemiBold, "BadgeForeground"); top.AppendChild(badge); stack.AppendChild(top); stack.AppendChild(CreateBoundText("Title", 11, FontWeights.SemiBold, null, BrushFromRgb(15, 23, 42))); stack.AppendChild(CreateBoundText("Body", 10.5, FontWeights.Normal, null, BrushFromRgb(71, 85, 105))); border.AppendChild(stack); return new DataTemplate { VisualTree = border };
        }

        private static DataTemplate CreateActivityTimelineTemplate()
        {
            const string xaml = @"
                <DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
                    <Border Padding=""12"" Margin=""0,0,0,10"" CornerRadius=""8"" Background=""#ffffff"" BorderBrush=""#e2e8f0"" BorderThickness=""1"">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""Auto"" />
                                <ColumnDefinition Width=""*"" />
                            </Grid.ColumnDefinitions>
                            <Border Width=""3"" CornerRadius=""2"" Background=""{Binding AccentBrush}"" Margin=""0,2,12,2"" />
                            <StackPanel Grid.Column=""1"">
                                <DockPanel LastChildFill=""True"">
                                    <StackPanel DockPanel.Dock=""Right"" HorizontalAlignment=""Right"">
                                        <TextBlock Text=""{Binding TimestampText}"" FontSize=""11"" Foreground=""#64748b"" TextAlignment=""Right"" />
                                        <TextBlock Text=""{Binding RelativeTimeText}"" Margin=""0,2,0,0"" FontSize=""10.5"" Foreground=""#94a3b8"" TextAlignment=""Right"" />
                                    </StackPanel>
                                    <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"">
                                        <Border CornerRadius=""999"" Padding=""8,3,8,3"" Background=""{Binding BadgeBackground}"" VerticalAlignment=""Center"">
                                            <TextBlock Text=""{Binding TypeLabel}"" FontSize=""10.5"" FontWeight=""SemiBold"" Foreground=""{Binding BadgeForeground}"" />
                                        </Border>
                                        <TextBlock Text=""{Binding Actor}"" Margin=""8,1,0,0"" FontSize=""11.5"" FontWeight=""SemiBold"" Foreground=""#475569"" />
                                    </StackPanel>
                                </DockPanel>
                                <TextBlock Text=""{Binding Title}"" Margin=""0,8,0,0"" FontSize=""13"" FontWeight=""SemiBold"" Foreground=""#0f172a"" TextWrapping=""Wrap"" />
                                <TextBlock Text=""{Binding Body}"" Margin=""0,5,0,0"" FontSize=""12"" Foreground=""#475569"" TextWrapping=""Wrap"" />
                            </StackPanel>
                        </Grid>
                    </Border>
                </DataTemplate>";

            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            return (DataTemplate)XamlReader.Parse(xaml, ctx);
        }

        private static FrameworkElement CreateSectionHeader(string title, string subtitle)
        {
            var stack = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 14)
            };
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(32, 42, 55)
            });
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                Margin = new Thickness(0, 6, 0, 0),
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = BrushFromRgb(98, 113, 128)
            });
            return stack;
        }

        private static TextBlock CreateGridStateText()
        {
            return new TextBlock
            {
                Margin = new Thickness(0, 6, 0, 12),
                Padding = new Thickness(14, 10, 14, 10),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Background = BrushFromRgb(248, 250, 252),
                Foreground = BrushFromRgb(100, 116, 139),
                Visibility = Visibility.Collapsed
            };
        }

        private static void SetGridState(DataGrid grid, TextBlock stateText, string message, bool isError, bool showGrid)
        {
            if (grid != null)
            {
                grid.Visibility = showGrid ? Visibility.Visible : Visibility.Collapsed;
            }

            if (stateText == null)
            {
                return;
            }

            stateText.Text = message ?? string.Empty;
            stateText.Foreground = isError ? BrushFromRgb(185, 28, 28) : BrushFromRgb(100, 116, 139);
            stateText.Background = isError ? BrushFromRgb(254, 242, 242) : BrushFromRgb(248, 250, 252);
            var showLoading = !string.IsNullOrWhiteSpace(message)
                              && message.TrimStart().StartsWith("Loading", StringComparison.OrdinalIgnoreCase);
            stateText.Visibility = string.IsNullOrWhiteSpace(message) || (showGrid && !showLoading)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private static bool HasActiveTicketFilter(string statusFilter, string search, LookupItem assignee, LookupItem branch)
        {
            return !string.IsNullOrWhiteSpace(search)
                   || !string.Equals((statusFilter ?? string.Empty).Trim(), "All", StringComparison.OrdinalIgnoreCase)
                   || (assignee != null && assignee.Id != -1)
                   || (branch != null && branch.Id > 0);
        }

        private static DataGrid CreateTicketGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = false,
                HeadersVisibility = DataGridHeadersVisibility.None,
                GridLinesVisibility = DataGridGridLinesVisibility.None,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                RowHeaderWidth = 0,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                MinHeight = 0,
                Margin = new Thickness(0, 0, 0, 10)
            };
            ScrollViewer.SetVerticalScrollBarVisibility(grid, ScrollBarVisibility.Auto);
            ScrollViewer.SetHorizontalScrollBarVisibility(grid, ScrollBarVisibility.Auto);

            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(100, 116, 139)));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 10, 14, 10)));
            headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(226, 232, 240))));
            grid.ColumnHeaderStyle = headerStyle;

            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            rowStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            rowStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 0.0));
            rowStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 10)));
            rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Select this ticket to view profile and workflow actions."));
            rowStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));

            var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            trigger.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            rowStyle.Triggers.Add(trigger);
            
            var selectedTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            rowStyle.Triggers.Add(selectedTrigger);

            grid.RowStyle = rowStyle;

            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            cellStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            
            var cellSelectedTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
            cellSelectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(15, 23, 42)));
            cellSelectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            cellSelectedTrigger.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Triggers.Add(cellSelectedTrigger);
            
            grid.CellStyle = cellStyle;

            return grid;
        }

        private static FrameworkElement BuildPager(out TextBlock pager, out Button prev, out Button next)
        {
            var panel = new DockPanel();
            pager = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11.5,
                Foreground = BrushFromRgb(71, 85, 105)
            };
            DockPanel.SetDock(pager, Dock.Left);
            panel.Children.Add(pager);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            prev = CreatePagerButton("Prev");
            next = CreatePagerButton("Next");
            buttons.Children.Add(prev);
            buttons.Children.Add(next);
            DockPanel.SetDock(buttons, Dock.Right);
            panel.Children.Add(buttons);
            return panel;
        }

        private static void AddPendingColumns(DataGrid grid)
        {
            grid.Columns.Clear();
            grid.Columns.Add(CreatePendingTicketCardColumn());
        }

        private static void AddSolvedColumns(DataGrid grid)
        {
            grid.Columns.Clear();
            grid.Columns.Add(CreateSolvedTicketCardColumn());
        }

        private static DataGridTemplateColumn CreatePendingTicketCardColumn()
        {
            return new DataGridTemplateColumn
            {
                Header = "Ticket",
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                CellTemplate = BuildPendingTicketCardTemplate()
            };
        }

        private static DataGridTemplateColumn CreateSolvedTicketCardColumn()
        {
            return new DataGridTemplateColumn
            {
                Header = "Ticket",
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                CellTemplate = BuildSolvedTicketCardTemplate()
            };
        }

        private static DataTemplate BuildPendingTicketCardTemplate()
        {
            var outer = new FrameworkElementFactory(typeof(Border));
            outer.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            outer.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            outer.SetValue(Border.BorderBrushProperty, BrushFromRgb(226, 232, 240));
            outer.SetValue(Border.BackgroundProperty, Brushes.White);
            outer.SetBinding(FrameworkElement.ToolTipProperty, new Binding("Issue"));

            var dock = new FrameworkElementFactory(typeof(DockPanel));
            outer.AppendChild(dock);

            var rail = new FrameworkElementFactory(typeof(Border));
            rail.SetValue(FrameworkElement.WidthProperty, 5.0);
            rail.SetValue(Border.CornerRadiusProperty, new CornerRadius(10, 0, 0, 10));
            rail.SetBinding(Border.BackgroundProperty, new Binding("AccentBrush"));
            rail.SetValue(DockPanel.DockProperty, Dock.Left);
            dock.AppendChild(rail);

            var content = new FrameworkElementFactory(typeof(StackPanel));
            content.SetValue(FrameworkElement.MarginProperty, new Thickness(14, 12, 14, 12));
            dock.AppendChild(content);

            var top = new FrameworkElementFactory(typeof(DockPanel));
            top.SetValue(DockPanel.LastChildFillProperty, true);
            content.AppendChild(top);

            var statusChip = new FrameworkElementFactory(typeof(Border));
            statusChip.SetValue(Border.CornerRadiusProperty, new CornerRadius(999));
            statusChip.SetValue(Border.PaddingProperty, new Thickness(10, 4, 10, 4));
            statusChip.SetValue(FrameworkElement.MarginProperty, new Thickness(12, 0, 0, 0));
            statusChip.SetValue(DockPanel.DockProperty, Dock.Right);
            statusChip.SetBinding(Border.BackgroundProperty, new Binding("StatusBackground"));
            var statusText = CreateBoundText("Status", 11, FontWeights.SemiBold, "StatusForeground");
            statusChip.AppendChild(statusText);
            top.AppendChild(statusChip);

            var titleStack = new FrameworkElementFactory(typeof(StackPanel));
            top.AppendChild(titleStack);
            titleStack.AppendChild(CreateBoundText("TicketCode", 13, FontWeights.Bold, null, BrushFromRgb(2, 132, 199)));
            var issue = CreateBoundText("Issue", 13, FontWeights.SemiBold, null, BrushFromRgb(30, 41, 59));
            issue.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            issue.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 0));
            titleStack.AppendChild(issue);

            var branch = CreateBoundText("Branch", 11.5, FontWeights.Normal, null, BrushFromRgb(100, 116, 139));
            branch.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            branch.SetBinding(FrameworkElement.ToolTipProperty, new Binding("Branch"));
            branch.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 8, 0, 0));
            content.AppendChild(branch);

            var metrics = new FrameworkElementFactory(typeof(WrapPanel));
            metrics.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 10, 0, 0));
            content.AppendChild(metrics);
            AppendLabelValue(metrics, "Caller: ", "Caller", BrushFromRgb(51, 65, 85), tooltipPath: "Caller");
            AppendLabelValue(metrics, "Assigned: ", "Responsible", BrushFromRgb(51, 65, 85));
            AppendLabelValue(metrics, "Age ", "Age", null, "MetricForeground");
            AppendLabelValue(metrics, "Idle ", "Idle", null, "MetricForeground");
            AppendLabelValue(metrics, "SLA ", "Sla", null, "MetricForeground", "SlaTooltip");

            var priorityChip = new FrameworkElementFactory(typeof(Border));
            priorityChip.SetValue(Border.CornerRadiusProperty, new CornerRadius(999));
            priorityChip.SetValue(Border.PaddingProperty, new Thickness(8, 3, 8, 3));
            priorityChip.SetBinding(Border.BackgroundProperty, new Binding("PriorityBackground"));
            var priorityText = CreateBoundText("Priority", 11, FontWeights.SemiBold, "PriorityForeground");
            priorityChip.AppendChild(priorityText);
            metrics.AppendChild(priorityChip);

            return new DataTemplate { VisualTree = outer };
        }

        private static DataTemplate BuildSolvedTicketCardTemplate()
        {
            var outer = new FrameworkElementFactory(typeof(Border));
            outer.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            outer.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            outer.SetValue(Border.BorderBrushProperty, BrushFromRgb(226, 232, 240));
            outer.SetValue(Border.BackgroundProperty, Brushes.White);
            outer.SetValue(Border.PaddingProperty, new Thickness(12, 10, 12, 10));
            outer.SetBinding(FrameworkElement.ToolTipProperty, new Binding("Issue"));

            var dock = new FrameworkElementFactory(typeof(DockPanel));
            dock.SetValue(DockPanel.LastChildFillProperty, true);
            outer.AppendChild(dock);

            var statusChip = new FrameworkElementFactory(typeof(Border));
            statusChip.SetValue(Border.CornerRadiusProperty, new CornerRadius(999));
            statusChip.SetValue(Border.PaddingProperty, new Thickness(10, 4, 10, 4));
            statusChip.SetValue(FrameworkElement.MarginProperty, new Thickness(12, 0, 0, 0));
            statusChip.SetValue(DockPanel.DockProperty, Dock.Right);
            statusChip.SetBinding(Border.BackgroundProperty, new Binding("StatusBackground"));
            statusChip.AppendChild(CreateBoundText("Status", 11, FontWeights.SemiBold, "StatusForeground"));
            dock.AppendChild(statusChip);

            var stack = new FrameworkElementFactory(typeof(StackPanel));
            dock.AppendChild(stack);
            stack.AppendChild(CreateBoundText("TicketCode", 12.5, FontWeights.Bold, null, BrushFromRgb(2, 132, 199)));
            var issue = CreateBoundText("Issue", 12.5, FontWeights.SemiBold, null, BrushFromRgb(30, 41, 59));
            issue.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            issue.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 0));
            stack.AppendChild(issue);
            var meta = CreateBoundText("Summary", 11.5, FontWeights.Normal, null, BrushFromRgb(100, 116, 139));
            meta.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 6, 0, 0));
            stack.AppendChild(meta);

            return new DataTemplate { VisualTree = outer };
        }

        private static FrameworkElementFactory CreateBoundText(string path, double fontSize, FontWeight weight, string foregroundPath, Brush foreground = null)
        {
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new Binding(path));
            text.SetValue(TextBlock.FontSizeProperty, fontSize);
            text.SetValue(TextBlock.FontWeightProperty, weight);
            text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            if (!string.IsNullOrWhiteSpace(foregroundPath))
            {
                text.SetBinding(TextBlock.ForegroundProperty, new Binding(foregroundPath));
            }
            else if (foreground != null)
            {
                text.SetValue(TextBlock.ForegroundProperty, foreground);
            }
            return text;
        }

        private static void AppendLabelValue(
            FrameworkElementFactory panel,
            string label,
            string valuePath,
            Brush valueBrush = null,
            string valueBrushPath = null,
            string tooltipPath = null)
        {
            var labelText = new FrameworkElementFactory(typeof(TextBlock));
            labelText.SetValue(TextBlock.TextProperty, label);
            labelText.SetValue(TextBlock.FontSizeProperty, 11.5);
            labelText.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(100, 116, 139));
            panel.AppendChild(labelText);

            var value = CreateBoundText(valuePath, 11.5, FontWeights.SemiBold, valueBrushPath, valueBrush ?? BrushFromRgb(51, 65, 85));
            value.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 14, 0));
            if (!string.IsNullOrWhiteSpace(tooltipPath))
            {
                value.SetBinding(FrameworkElement.ToolTipProperty, new Binding(tooltipPath));
            }
            panel.AppendChild(value);
        }

        private static FrameworkElement CreateFact(string label, out TextBlock value)
        {
            var border = new Border
            {
                Margin = new Thickness(0, 0, 10, 10),
                Padding = new Thickness(16),
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 4,
                    ShadowDepth = 1,
                    Color = Color.FromArgb(40, 15, 23, 42),
                    Opacity = 0.05
                }
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
            border.Style = (Style)XamlReader.Parse(styleXaml, ctx);

            var stack = new StackPanel();
            border.Child = stack;
            stack.Children.Add(new TextBlock
            {
                Text = label.ToUpperInvariant(),
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(100, 116, 139)
            });
            value = new TextBlock
            {
                Margin = new Thickness(0, 6, 0, 0),
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                TextWrapping = TextWrapping.Wrap,
                Foreground = BrushFromRgb(15, 23, 42)
            };
            stack.Children.Add(value);
            return border;
        }

        private static FrameworkElement CreateIndicatorFact(string label, out TextBlock value)
        {
            var outer = new Border
            {
                Margin = new Thickness(0, 0, 10, 10),
                Padding = new Thickness(14),
                CornerRadius = new CornerRadius(8),
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel();
            outer.Child = stack;
            stack.Children.Add(new TextBlock
            {
                Text = label.ToUpperInvariant(),
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(100, 116, 139)
            });

            var chip = new Border
            {
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(10, 5, 10, 5),
                CornerRadius = new CornerRadius(999),
                Background = BrushFromRgb(241, 245, 249),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            value = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105)
            };
            chip.Child = value;
            stack.Children.Add(chip);
            return outer;
        }

        private static void ApplyProfileStatusStyle(TextBlock target, string status)
        {
            var s = (status ?? string.Empty).Trim();
            if (s.Equals("Overdue", StringComparison.OrdinalIgnoreCase))
            {
                ApplyProfileChipStyle(target, BrushFromRgb(255, 228, 230), BrushFromRgb(159, 18, 57));
            }
            else if (s.Equals("Escalated", StringComparison.OrdinalIgnoreCase))
            {
                ApplyProfileChipStyle(target, BrushFromRgb(255, 237, 213), BrushFromRgb(154, 52, 18));
            }
            else if (s.Equals("In Progress", StringComparison.OrdinalIgnoreCase))
            {
                ApplyProfileChipStyle(target, BrushFromRgb(219, 234, 254), BrushFromRgb(30, 64, 175));
            }
            else if (s.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                ApplyProfileChipStyle(target, BrushFromRgb(254, 249, 195), BrushFromRgb(133, 77, 14));
            }
            else if (s.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase))
            {
                ApplyProfileChipStyle(target, BrushFromRgb(224, 231, 255), BrushFromRgb(55, 48, 163));
            }
            else if (s.Equals("Solved", StringComparison.OrdinalIgnoreCase) || s.Equals("Closed", StringComparison.OrdinalIgnoreCase))
            {
                ApplyProfileChipStyle(target, BrushFromRgb(220, 252, 231), BrushFromRgb(22, 101, 52));
            }
            else if (s.Equals("Reopened", StringComparison.OrdinalIgnoreCase))
            {
                ApplyProfileChipStyle(target, BrushFromRgb(254, 226, 226), BrushFromRgb(153, 27, 27));
            }
            else
            {
                ApplyProfileChipStyle(target, BrushFromRgb(241, 245, 249), BrushFromRgb(71, 85, 105));
            }
        }

        private static void ApplyProfilePriorityStyle(TextBlock target, string priority)
        {
            var p = (priority ?? string.Empty).Trim();
            if (p.Equals("Critical", StringComparison.OrdinalIgnoreCase))
            {
                ApplyProfileChipStyle(target, BrushFromRgb(254, 226, 226), BrushFromRgb(153, 27, 27));
            }
            else if (p.Equals("High", StringComparison.OrdinalIgnoreCase))
            {
                ApplyProfileChipStyle(target, BrushFromRgb(255, 237, 213), BrushFromRgb(154, 52, 18));
            }
            else if (p.Equals("Medium", StringComparison.OrdinalIgnoreCase))
            {
                ApplyProfileChipStyle(target, BrushFromRgb(219, 234, 254), BrushFromRgb(30, 64, 175));
            }
            else
            {
                ApplyProfileChipStyle(target, BrushFromRgb(241, 245, 249), BrushFromRgb(71, 85, 105));
            }
        }

        private static void ApplyProfileChipStyle(TextBlock target, Brush background, Brush foreground)
        {
            if (target == null)
                return;

            target.Foreground = foreground;
            if (target.Parent is Border chip)
                chip.Background = background;
        }

        private static Grid CreateFormGrid(int columns, int rows)
        {
            var grid = new Grid();
            for (var columnIndex = 0; columnIndex < columns; columnIndex++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            for (var rowIndex = 0; rowIndex < rows; rowIndex++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            return grid;
        }

        private static Border CreateMutedInfoPanel(UIElement content)
        {
            return new Border
            {
                Margin = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(12, 10, 12, 10),
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                Child = content
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

        private static void ApplyModernButtonTemplate(Button btn, Thickness padding)
        {
            var templateXaml = @"
                <ControlTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" TargetType=""Button"">
                    <Border CornerRadius=""6"" Background=""{TemplateBinding Background}"" Padding=""" + padding.Left + "," + padding.Top + "," + padding.Right + "," + padding.Bottom + @""">
                        <Border.Effect>
                            <DropShadowEffect BlurRadius=""4"" ShadowDepth=""1"" Color=""#1e293b"" Opacity=""0.15"" />
                        </Border.Effect>
                        <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter Property=""Opacity"" Value=""0.9"" />
                        </Trigger>
                        <Trigger Property=""IsPressed"" Value=""True"">
                            <Setter Property=""Opacity"" Value=""0.8"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>";

            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            btn.Template = (ControlTemplate)XamlReader.Parse(templateXaml, ctx);
            btn.Cursor = Cursors.Hand;
        }

        private static Button CreatePrimaryButton(string text, Brush background)
        {
            var btn = new Button
            {
                Content = text,
                Margin = new Thickness(0, 0, 10, 10),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = background
            };
            ApplyModernButtonTemplate(btn, new Thickness(16, 10, 16, 10));
            return btn;
        }

        private static Button CreateInlineActionButton(string text, Brush background)
        {
            var btn = new Button
            {
                Content = text,
                MinWidth = 118,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = background,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            ApplyModernButtonTemplate(btn, new Thickness(14, 8, 14, 8));
            return btn;
        }

        private static Button CreatePagerButton(string text)
        {
            var btn = new Button
            {
                Content = text,
                Margin = new Thickness(8, 0, 0, 0),
                Foreground = BrushFromRgb(51, 65, 85),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            
            var templateXaml = @"
                <ControlTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" TargetType=""Button"">
                    <Border CornerRadius=""16"" Padding=""16,6,16,6"" BorderThickness=""1"">
                        <Border.Style>
                            <Style TargetType=""Border"">
                                <Setter Property=""Background"" Value=""White"" />
                                <Setter Property=""BorderBrush"" Value=""#cbd5e1"" />
                                <Style.Triggers>
                                    <Trigger Property=""IsMouseOver"" Value=""True"">
                                        <Setter Property=""Background"" Value=""#f8fafc"" />
                                        <Setter Property=""BorderBrush"" Value=""#94a3b8"" />
                                    </Trigger>
                                </Style.Triggers>
                            </Style>
                        </Border.Style>
                        <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" />
                    </Border>
                </ControlTemplate>";

            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            btn.Template = (ControlTemplate)XamlReader.Parse(templateXaml, ctx);

            return btn;
        }

        private static void ApplyModernTextBoxStyle(TextBox box)
        {
            var templateXaml = @"
                <ControlTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" TargetType=""TextBox"">
                    <Border x:Name=""border"" CornerRadius=""6"" Background=""White"" BorderBrush=""#cbd5e1"" BorderThickness=""1"" Padding=""{TemplateBinding Padding}"">
                        <ScrollViewer x:Name=""PART_ContentHost"" Focusable=""false"" HorizontalScrollBarVisibility=""Hidden"" VerticalScrollBarVisibility=""Hidden""/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsKeyboardFocused"" Value=""true"">
                            <Setter TargetName=""border"" Property=""BorderBrush"" Value=""#3b82f6""/>
                            <Setter TargetName=""border"" Property=""BorderThickness"" Value=""2""/>
                        </Trigger>
                        <Trigger Property=""IsMouseOver"" Value=""true"">
                            <Setter TargetName=""border"" Property=""BorderBrush"" Value=""#94a3b8""/>
                        </Trigger>
                        <Trigger Property=""IsReadOnly"" Value=""true"">
                            <Setter TargetName=""border"" Property=""Background"" Value=""#f8fafc""/>
                            <Setter TargetName=""border"" Property=""BorderBrush"" Value=""#e2e8f0""/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>";
            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            box.Template = (ControlTemplate)XamlReader.Parse(templateXaml, ctx);
            box.Foreground = BrushFromRgb(30, 41, 59);
        }

        private static TextBox CreateTextBox(string placeholder)
        {
            var box = new TextBox
            {
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 12
            };
            box.Text = string.Empty;
            box.Tag = placeholder;
            ApplyModernTextBoxStyle(box);
            return box;
        }

        private static TextBox CreateMultilineTextBox(string placeholder)
        {
            var box = new TextBox
            {
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 120,
                Tag = placeholder
            };
            ApplyModernTextBoxStyle(box);
            return box;
        }

        private static void ApplyModernComboBoxStyle(ComboBox box)
        {
            box.Background = Brushes.White;
            box.BorderBrush = BrushFromRgb(203, 213, 225);
            box.BorderThickness = new Thickness(1);
            box.Foreground = BrushFromRgb(30, 41, 59);
        }

        private static ComboBox CreateLookupComboBox()
        {
            var box = new ComboBox
            {
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 12,
                DisplayMemberPath = "Name"
            };
            ApplyModernComboBoxStyle(box);
            return box;
        }

        private static ComboBox CreateTextComboBox()
        {
            var box = new ComboBox
            {
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 12
            };
            ApplyModernComboBoxStyle(box);
            return box;
        }

        private static void AddFilterField(Grid grid, int column, string label, FrameworkElement control)
        {
            var stack = new StackPanel
            {
                Margin = column == 0 ? new Thickness(0) : new Thickness(18, 0, 0, 0)
            };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                Margin = new Thickness(0, 0, 0, 8),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105)
            });
            stack.Children.Add(control);
            Grid.SetColumn(stack, column);
            grid.Children.Add(stack);
        }

        private static void AddFormField(Grid grid, int row, int column, string label, FrameworkElement control, Thickness? margin = null)
        {
            var stack = new StackPanel
            {
                Margin = margin ?? new Thickness(column > 0 ? 18 : 0, row > 0 ? 14 : 0, 0, 0)
            };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                Margin = new Thickness(0, 0, 0, 8),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105)
            });
            stack.Children.Add(control);
            Grid.SetRow(stack, row);
            Grid.SetColumn(stack, column);
            grid.Children.Add(stack);
        }

        private static void AddStandaloneField(Panel host, string label, FrameworkElement control, Thickness margin)
        {
            var stack = new StackPanel
            {
                Margin = margin
            };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                Margin = new Thickness(0, 0, 0, 8),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105)
            });
            stack.Children.Add(control);
            host.Children.Add(stack);
        }

        private static Style BuildListBoxItemStyle()
        {
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0, 0, 0, 8)));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            return style;
        }

        private static void PopulateLookupFilter(ComboBox combo, IEnumerable<LookupItem> items, string allLabel, bool includeUnassigned)
        {
            var list = new List<LookupItem> { new LookupItem { Id = -1, Name = allLabel } };
            if (includeUnassigned)
            {
                list.Add(new LookupItem { Id = 0, Name = "(Unassigned)" });
            }
            list.AddRange((items ?? Enumerable.Empty<LookupItem>())
                .Where(x => x != null && x.Id > 0)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .OrderBy(x => x.Name));
            combo.ItemsSource = list;
            combo.SelectedIndex = 0;
        }

        private static void PopulateLookupCombo(ComboBox combo, IEnumerable<LookupItem> items, string blankLabel, bool includeBlank)
        {
            var list = new List<LookupItem>();
            if (includeBlank)
            {
                list.Add(new LookupItem { Id = 0, Name = blankLabel });
            }

            list.AddRange((items ?? Enumerable.Empty<LookupItem>())
                .Where(x => x != null)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .OrderBy(x => x.Name));

            combo.ItemsSource = list;
            combo.SelectedIndex = list.Count > 0 ? 0 : -1;
        }

        private static int? GetLookupId(ComboBox combo)
        {
            var item = combo?.SelectedItem as LookupItem;
            return item != null && item.Id > 0 ? (int?)item.Id : null;
        }

        private string ResolveCallerText()
        {
            if (_createCaller.SelectedItem is LookupItem item && !string.IsNullOrWhiteSpace(item.Name))
                return item.Name.Trim();

            return (_createCaller.Text ?? string.Empty).Trim();
        }

        private LookupItem FindBestEmployeeMatchForCurrentUser()
        {
            if (AppSession.CurrentEmployeeId.HasValue && AppSession.CurrentEmployeeId.Value > 0)
            {
                var byId = _assignees.FirstOrDefault(x => x != null && x.Id == AppSession.CurrentEmployeeId.Value);
                if (byId != null)
                {
                    return byId;
                }
            }

            var currentName = (AppSession.CurrentEmployeeName ?? AppSession.CurrentUserName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(currentName))
            {
                return null;
            }

            var normalizedCurrent = currentName.ToUpperInvariant();
            return _assignees.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x?.Name) && x.Name.Trim().ToUpperInvariant() == normalizedCurrent)
                ?? _assignees.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x?.Name) && x.Name.Trim().ToUpperInvariant().Contains(normalizedCurrent));
        }

        private static void SelectLookupItem(ComboBox combo, int id)
        {
            if (combo == null)
            {
                return;
            }

            foreach (var obj in combo.Items)
            {
                if (obj is LookupItem item && item.Id == id)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
        }

        private static void SelectLookupItemByName(ComboBox combo, string name)
        {
            var target = (name ?? string.Empty).Trim();
            if (combo == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                if (combo.Items.Count > 0)
                {
                    combo.SelectedIndex = 0;
                }
                return;
            }

            foreach (var obj in combo.Items)
            {
                if (obj is LookupItem item && string.Equals((item.Name ?? string.Empty).Trim(), target, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }

            if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
        }

        private static void UpdatePager(TextBlock label, Button prev, Button next, int pageIndex, int pageSize, int totalCount, bool hasNext)
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling(Math.Max(0, totalCount) / (double)Math.Max(1, pageSize)));
            label.Text = "Page " + Math.Max(1, pageIndex) + " of " + totalPages + " (" + Math.Max(0, totalCount) + ")";
            prev.IsEnabled = pageIndex > 1;
            next.IsEnabled = hasNext;
        }

        private PendingTicketRow CreatePendingRow(CallTicketListItem ticket, CallTicketEscalationOverrideItem overrideItem = null)
        {
            var overdue = IsTicketOverdue(ticket);
            var status = overdue ? "Overdue" : NormalizeLegacyTicketStatus(ticket.Status);
            var activityUtc = GetLastActivityUtc(ticket, out _);
            var idle = DateTime.UtcNow - activityUtc;
            if (idle < TimeSpan.Zero) idle = TimeSpan.Zero;
            var sla = BuildSlaCell(ticket, overrideItem);

            return new PendingTicketRow
            {
                Source = ticket,
                TicketCode = ticket.TicketCode ?? ticket.TicketId.ToString(),
                Issue = ticket.Issue,
                Branch = ticket.Branch ?? "-",
                Caller = string.IsNullOrWhiteSpace(ticket.CallerName) ? "-" : ticket.CallerName.Trim(),
                Responsible = string.IsNullOrWhiteSpace(ticket.ResponsiblePerson) ? "Unassigned" : ticket.ResponsiblePerson,
                Priority = string.IsNullOrWhiteSpace(ticket.Priority) ? "Medium" : ticket.Priority.Trim(),
                Age = Math.Max(0, ticket.TicketAgeDays) + "d",
                AgeState = GetAgeState(ticket.TicketAgeDays),
                Idle = FormatShortDuration(idle),
                IdleState = GetIdleState(idle),
                Sla = sla.Label,
                SlaState = sla.State,
                SlaTooltip = sla.Tooltip,
                Status = status,
                AccentBrush = GetTicketAccentBrush(status),
                StatusBackground = GetStatusBackground(status),
                StatusForeground = GetStatusForeground(status),
                PriorityBackground = GetPriorityBackground(string.IsNullOrWhiteSpace(ticket.Priority) ? "Medium" : ticket.Priority.Trim()),
                PriorityForeground = GetPriorityForeground(string.IsNullOrWhiteSpace(ticket.Priority) ? "Medium" : ticket.Priority.Trim()),
                MetricForeground = GetMetricForeground(sla.State == "Bad" ? "Bad" : GetIdleState(idle))
            };
        }

        private SolvedTicketRow CreateSolvedRow(CallTicketListItem ticket)
        {
            return new SolvedTicketRow
            {
                Source = ticket,
                TicketCode = ticket.TicketCode ?? ticket.TicketId.ToString(),
                Issue = ticket.Issue,
                Branch = ticket.Branch ?? "-",
                Responsible = ticket.ResponsiblePerson ?? "-",
                Priority = string.IsNullOrWhiteSpace(ticket.Priority) ? "Medium" : ticket.Priority.Trim(),
                Status = NormalizeLegacyTicketStatus(ticket.Status),
                ResolvedOn = ticket.SolvedAt.HasValue ? AppTime.ToLocalString(ticket.SolvedAt.Value, "g") : AppTime.ToLocalString(ticket.UpdatedAt, "g"),
                Summary = "Solved by " + (string.IsNullOrWhiteSpace(ticket.ResponsiblePerson) ? "-" : ticket.ResponsiblePerson.Trim())
                    + " - " + (ticket.Branch ?? "-")
                    + " - " + (ticket.SolvedAt.HasValue ? AppTime.ToLocalString(ticket.SolvedAt.Value, "g") : AppTime.ToLocalString(ticket.UpdatedAt, "g")),
                StatusBackground = GetStatusBackground(NormalizeLegacyTicketStatus(ticket.Status)),
                StatusForeground = GetStatusForeground(NormalizeLegacyTicketStatus(ticket.Status))
            };
        }

        private static string GuessItDepartmentName(IEnumerable<LookupItem> departments)
        {
            var match = (departments ?? Enumerable.Empty<LookupItem>())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Name) && x.Name.IndexOf("IT", StringComparison.OrdinalIgnoreCase) >= 0);
            return match?.Name ?? "IT";
        }

        private static List<ActivityTimelineItem> BuildActivityItems(
            IEnumerable<CallTicketNoteItem> notes,
            IEnumerable<CallTicketHistoryItem> history)
        {
            var rows = new List<ActivityTimelineItem>();

            foreach (var note in notes ?? Enumerable.Empty<CallTicketNoteItem>())
            {
                var noteType = NormalizeActivityType(note?.NoteType);
                var createdAt = note?.CreatedAt;
                rows.Add(new ActivityTimelineItem
                {
                    TypeLabel = noteType,
                    Actor = FormatActor(note?.CreatedByName),
                    Title = noteType + " added",
                    Body = string.IsNullOrWhiteSpace(note?.NoteText) ? "(No note text.)" : note.NoteText.Trim(),
                    TimestampUtc = NormalizeTimestampUtc(createdAt),
                    TimestampText = FormatActivityTimestamp(createdAt),
                    RelativeTimeText = FormatRelativeTime(createdAt),
                    AccentBrush = GetActivityAccentBrush(noteType),
                    BadgeBackground = GetActivityBadgeBackground(noteType),
                    BadgeForeground = GetActivityBadgeForeground(noteType)
                });
            }

            foreach (var item in history ?? Enumerable.Empty<CallTicketHistoryItem>())
            {
                var type = NormalizeActivityType(item?.FieldName);
                var oldValue = string.IsNullOrWhiteSpace(item?.OldValue) ? "-" : item.OldValue.Trim();
                var newValue = string.IsNullOrWhiteSpace(item?.NewValue) ? "-" : item.NewValue.Trim();
                var changedAt = item?.ChangedAt;
                rows.Add(new ActivityTimelineItem
                {
                    TypeLabel = type,
                    Actor = FormatActor(item?.ChangedByName),
                    Title = type + ": " + oldValue + " -> " + newValue,
                    Body = string.IsNullOrWhiteSpace(item?.Note) ? "No action note recorded." : item.Note.Trim(),
                    TimestampUtc = NormalizeTimestampUtc(changedAt),
                    TimestampText = FormatActivityTimestamp(changedAt),
                    RelativeTimeText = FormatRelativeTime(changedAt),
                    AccentBrush = GetActivityAccentBrush(type),
                    BadgeBackground = GetActivityBadgeBackground(type),
                    BadgeForeground = GetActivityBadgeForeground(type)
                });
            }

            if (rows.Count == 0)
            {
                rows.Add(ActivityTimelineItem.SystemMessage("No activity yet", "Notes and ticket changes will appear here after the ticket is updated."));
            }

            return rows
                .OrderByDescending(x => x.TimestampUtc ?? DateTime.MinValue)
                .ToList();
        }

        private static string NormalizeActivityType(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text)) return "Activity";
            if (text.Equals("AssignedToEmpId", StringComparison.OrdinalIgnoreCase)) return "Assignment";
            if (text.Equals("ResponsiblePerson", StringComparison.OrdinalIgnoreCase)) return "Assignment";
            if (text.Equals("Note", StringComparison.OrdinalIgnoreCase)) return "Note";
            if (text.Equals("Solution", StringComparison.OrdinalIgnoreCase)) return "Solution";
            if (text.Equals("Status", StringComparison.OrdinalIgnoreCase)) return "Status";
            if (text.Equals("Priority", StringComparison.OrdinalIgnoreCase)) return "Priority";
            if (text.Equals("TemporaryReturn", StringComparison.OrdinalIgnoreCase)) return "Return";
            return text;
        }

        private static string FormatActor(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "System" : value.Trim();
        }

        private static string FormatActivityTimestamp(DateTime? value)
        {
            return value.HasValue ? AppTime.ToLocalString(value.Value, "MMM d, yyyy h:mm tt") : "Time unavailable";
        }

        private static DateTime? NormalizeTimestampUtc(DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            var when = value.Value;
            if (when.Kind == DateTimeKind.Utc)
            {
                return when;
            }

            if (when.Kind == DateTimeKind.Local)
            {
                return when.ToUniversalTime();
            }

            return DateTime.SpecifyKind(when, DateTimeKind.Utc);
        }

        private static string FormatRelativeTime(DateTime? value)
        {
            var whenUtc = NormalizeTimestampUtc(value);
            if (!whenUtc.HasValue)
            {
                return "-";
            }

            var elapsed = DateTime.UtcNow - whenUtc.Value;
            if (elapsed < TimeSpan.Zero)
            {
                elapsed = TimeSpan.Zero;
            }

            if (elapsed.TotalDays >= 30)
            {
                var months = Math.Max(1, (int)Math.Floor(elapsed.TotalDays / 30));
                return months == 1 ? "1 month ago" : months + " months ago";
            }

            if (elapsed.TotalDays >= 1)
            {
                var days = Math.Max(1, (int)Math.Floor(elapsed.TotalDays));
                return days == 1 ? "1 day ago" : days + " days ago";
            }

            if (elapsed.TotalHours >= 1)
            {
                var hours = Math.Max(1, (int)Math.Floor(elapsed.TotalHours));
                return hours == 1 ? "1 hour ago" : hours + " hours ago";
            }

            var minutes = Math.Max(1, (int)Math.Floor(elapsed.TotalMinutes));
            return minutes == 1 ? "1 minute ago" : minutes + " minutes ago";
        }

        private static Brush GetActivityAccentBrush(string type)
        {
            if (type.Equals("Status", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(37, 99, 235);
            if (type.Equals("Solution", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(22, 163, 74);
            if (type.Equals("Priority", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(234, 88, 12);
            if (type.Equals("Assignment", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(13, 148, 136);
            if (type.Equals("Return", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(124, 58, 237);
            return BrushFromRgb(100, 116, 139);
        }

        private static Brush GetActivityBadgeBackground(string type)
        {
            if (type.Equals("Status", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(219, 234, 254);
            if (type.Equals("Solution", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(220, 252, 231);
            if (type.Equals("Priority", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(255, 237, 213);
            if (type.Equals("Assignment", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(204, 251, 241);
            if (type.Equals("Return", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(237, 233, 254);
            return BrushFromRgb(241, 245, 249);
        }

        private static Brush GetActivityBadgeForeground(string type)
        {
            if (type.Equals("Status", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(30, 64, 175);
            if (type.Equals("Solution", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(22, 101, 52);
            if (type.Equals("Priority", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(154, 52, 18);
            if (type.Equals("Assignment", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(17, 94, 89);
            if (type.Equals("Return", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(91, 33, 182);
            return BrushFromRgb(71, 85, 105);
        }

        private static string FormatLastEmail(CallEmailLogItem item)
        {
            if (item == null)
            {
                return "None";
            }

            var when = AppTime.ToLocalString(item.DateSent, "g");
            var type = string.IsNullOrWhiteSpace(item.EmailType) ? "-" : item.EmailType.Trim();
            var status = string.IsNullOrWhiteSpace(item.Status) ? "-" : item.Status.Trim();
            return type + " • " + status + " • " + when;
        }

        private bool IsTicketOverdue(CallTicketListItem ticket)
        {
            if (ticket == null || IsFinalStatus(ticket.Status) || _overdueDays <= 0)
            {
                return false;
            }

            var lastActivityUtc = GetLastActivityUtc(ticket, out _);
            var idle = DateTime.UtcNow - lastActivityUtc;
            if (idle < TimeSpan.Zero) idle = TimeSpan.Zero;
            return idle >= TimeSpan.FromDays(_overdueDays);
        }

        private static bool IsFinalStatus(string status)
        {
            var s = (status ?? string.Empty).Trim();
            return s.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                   || s.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                   || s.Equals("Closed", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeLegacyTicketStatus(string status)
        {
            var s = (status ?? string.Empty).Trim();
            if (s.Equals("Waiting on Vendor", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Waiting on Department", StringComparison.OrdinalIgnoreCase))
            {
                return "In Progress";
            }

            return string.IsNullOrWhiteSpace(s) ? "-" : s;
        }

        private static DateTime GetLastActivityUtc(CallTicketListItem ticket, out string source)
        {
            if (ticket?.LastContactAt.HasValue == true)
            {
                source = "LastContactAt";
                return ticket.LastContactAt.Value.Kind == DateTimeKind.Utc
                    ? ticket.LastContactAt.Value
                    : DateTime.SpecifyKind(ticket.LastContactAt.Value, DateTimeKind.Utc);
            }

            source = "UpdatedAt";
            var updated = ticket?.UpdatedAt ?? DateTime.UtcNow;
            return updated.Kind == DateTimeKind.Utc ? updated : DateTime.SpecifyKind(updated, DateTimeKind.Utc);
        }

        private static (string Label, string Tooltip, string State) BuildSlaCell(CallTicketListItem ticket, CallTicketEscalationOverrideItem overrideItem = null)
        {
            if (ticket == null)
            {
                return ("-", "SLA unavailable.", string.Empty);
            }

            var targetHours = GetResolutionSlaTarget(ticket.Priority).TargetHours;
            if (overrideItem != null && overrideItem.DaysToManager > 0)
            {
                targetHours = overrideItem.DaysToManager * 24;
            }

            if (targetHours <= 0)
            {
                return ("-", "SLA unavailable.", string.Empty);
            }

            if (IsFinalStatus(ticket.Status))
            {
                return ("Closed", "Ticket is already closed.", "Good");
            }

            var createdUtc = DateTime.SpecifyKind(ticket.CreatedAt, DateTimeKind.Utc);
            var dueUtc = createdUtc.AddHours(targetHours);
            var remaining = dueUtc - DateTime.UtcNow;
            var dueLocal = dueUtc.ToLocalTime();

            if (remaining <= TimeSpan.Zero)
            {
                var overdue = remaining.Duration();
                return ("Breached " + FormatShortDuration(overdue), "Due " + dueLocal.ToString("yyyy-MM-dd HH:mm"), "Bad");
            }

            var state = remaining.TotalHours <= Math.Max(4, targetHours * 0.25)
                ? "Warn"
                : remaining.TotalHours <= Math.Max(8, targetHours * 0.5)
                    ? "Watch"
                    : "Good";
            return ("Due " + FormatShortDuration(remaining), "Due " + dueLocal.ToString("yyyy-MM-dd HH:mm"), state);
        }

        private static string GetAgeState(int ageDays)
        {
            if (ageDays >= 5) return "Bad";
            if (ageDays >= 3) return "Warn";
            if (ageDays >= 1) return "Watch";
            return "Good";
        }

        private static string GetIdleState(TimeSpan idle)
        {
            if (idle.TotalDays >= 2) return "Bad";
            if (idle.TotalDays >= 1) return "Warn";
            if (idle.TotalHours >= 8) return "Watch";
            return "Good";
        }

        private static Brush GetMetricForeground(string state)
        {
            if (string.Equals(state, "Bad", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(153, 27, 27);
            if (string.Equals(state, "Warn", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(154, 52, 18);
            if (string.Equals(state, "Watch", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(133, 77, 14);
            if (string.Equals(state, "Good", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(22, 101, 52);
            return BrushFromRgb(15, 23, 42);
        }

        private static Brush GetTicketAccentBrush(string status)
        {
            var s = (status ?? string.Empty).Trim();
            if (s.Equals("Overdue", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(225, 29, 72);
            if (s.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(249, 115, 22);
            if (s.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(37, 99, 235);
            if (s.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(202, 138, 4);
            if (s.Equals("Reopened", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(220, 38, 38);
            return BrushFromRgb(148, 163, 184);
        }

        private static Brush GetStatusBackground(string status)
        {
            var s = (status ?? string.Empty).Trim();
            if (s.Equals("Solved", StringComparison.OrdinalIgnoreCase) || s.Equals("Closed", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(220, 252, 231);
            if (s.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(224, 231, 255);
            if (s.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(254, 249, 195);
            if (s.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(219, 234, 254);
            if (s.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(255, 237, 213);
            if (s.Equals("Overdue", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(255, 228, 230);
            if (s.Equals("Reopened", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(254, 226, 226);
            return BrushFromRgb(241, 245, 249);
        }

        private static Brush GetStatusForeground(string status)
        {
            var s = (status ?? string.Empty).Trim();
            if (s.Equals("Solved", StringComparison.OrdinalIgnoreCase) || s.Equals("Closed", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(22, 101, 52);
            if (s.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(55, 48, 163);
            if (s.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(133, 77, 14);
            if (s.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(30, 64, 175);
            if (s.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(154, 52, 18);
            if (s.Equals("Overdue", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(159, 18, 57);
            if (s.Equals("Reopened", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(153, 27, 27);
            return BrushFromRgb(71, 85, 105);
        }

        private static Brush GetPriorityBackground(string priority)
        {
            var p = (priority ?? string.Empty).Trim();
            if (p.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(254, 226, 226);
            if (p.Equals("High", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(255, 237, 213);
            if (p.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(219, 234, 254);
            return BrushFromRgb(241, 245, 249);
        }

        private static Brush GetPriorityForeground(string priority)
        {
            var p = (priority ?? string.Empty).Trim();
            if (p.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(153, 27, 27);
            if (p.Equals("High", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(154, 52, 18);
            if (p.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(30, 64, 175);
            return BrushFromRgb(71, 85, 105);
        }

        private static (int TargetHours, string Name) GetResolutionSlaTarget(string priority)
        {
            var p = (priority ?? string.Empty).Trim();
            if (p.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return (24, "Critical 24h");
            if (p.Equals("High", StringComparison.OrdinalIgnoreCase)) return (48, "High 48h");
            if (p.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return (72, "Medium 72h");
            if (p.Equals("Low", StringComparison.OrdinalIgnoreCase)) return (120, "Low 120h");
            return (72, "Default 72h");
        }

        private static string BuildEscalationLabel(CallTicketListItem ticket, CallEscalationSettingsItem settings, CallTicketEscalationOverrideItem overrideItem)
        {
            if (ticket == null) return "-";
            var ageDays = Math.Max(0, ticket.TicketAgeDays);
            var supDays = overrideItem != null ? overrideItem.DaysToSupervisor : (settings?.DaysToSupervisor ?? 2);
            var mgrDays = overrideItem != null ? overrideItem.DaysToManager : (settings?.DaysToManager ?? 3);

            if (ageDays >= mgrDays)
            {
                var overBy = ageDays - mgrDays;
                return overBy > 0 ? "Escalated: Manager • " + overBy + "d past threshold" : "Escalated: Manager";
            }

            if (ageDays >= supDays)
            {
                var mgrIn = mgrDays - ageDays;
                return mgrIn > 0 ? "Escalated: Supervisor • manager in " + mgrIn + "d" : "Escalated: Supervisor";
            }

            return "Next: sup in " + (supDays - ageDays) + "d, mgr in " + (mgrDays - ageDays) + "d" + (overrideItem != null ? " (override)" : string.Empty);
        }

        private static string FormatShortDuration(TimeSpan value)
        {
            if (value < TimeSpan.Zero)
            {
                value = value.Duration();
            }

            if (value.Days > 0)
            {
                return value.Hours > 0 ? value.Days + "d " + value.Hours + "h" : value.Days + "d";
            }

            if (value.Hours > 0)
            {
                return value.Minutes > 0 ? value.Hours + "h " + value.Minutes + "m" : value.Hours + "h";
            }

            return Math.Max(1, (int)Math.Round(value.TotalMinutes)) + "m";
        }

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
        {
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private sealed class LifecycleTicketRow
        {
            public CallTicketListItem Source { get; set; }
            public string LifecycleGroup { get; set; }
            public int LifecycleOrder { get; set; }
            public int PriorityOrder { get; set; }
            public string TicketCode { get; set; }
            public string IssueAndCaller { get; set; }
            public string Priority { get; set; }
            public string Owner { get; set; }
            public string Age { get; set; }
            public string Status { get; set; }
            public string ResolutionBadge { get; set; }
            public bool IsForwardedToRepair { get; set; }
            public string FieldVisitStatus { get; set; }
            public bool IsFieldVisitCompleted { get; set; }
            public DateTime LastActivityUtc { get; set; }
        }

        private sealed class PendingTicketRow
        {
            public CallTicketListItem Source { get; set; }
            public string TicketCode { get; set; }
            public string Issue { get; set; }
            public string Branch { get; set; }
            public string Caller { get; set; }
            public string Responsible { get; set; }
            public string Priority { get; set; }
            public string Age { get; set; }
            public string AgeState { get; set; }
            public string Idle { get; set; }
            public string IdleState { get; set; }
            public string Sla { get; set; }
            public string SlaState { get; set; }
            public string SlaTooltip { get; set; }
            public string Status { get; set; }
            public Brush AccentBrush { get; set; }
            public Brush StatusBackground { get; set; }
            public Brush StatusForeground { get; set; }
            public Brush PriorityBackground { get; set; }
            public Brush PriorityForeground { get; set; }
            public Brush MetricForeground { get; set; }
        }

        private sealed class SolvedTicketRow
        {
            public CallTicketListItem Source { get; set; }
            public string TicketCode { get; set; }
            public string Issue { get; set; }
            public string Branch { get; set; }
            public string Responsible { get; set; }
            public string Priority { get; set; }
            public string Status { get; set; }
            public string ResolvedOn { get; set; }
            public string Summary { get; set; }
            public Brush StatusBackground { get; set; }
            public Brush StatusForeground { get; set; }
        }

        private sealed class ActivityTimelineItem
        {
            public string TypeLabel { get; set; }
            public string Actor { get; set; }
            public string Title { get; set; }
            public string Body { get; set; }
            public DateTime? TimestampUtc { get; set; }
            public string TimestampText { get; set; }
            public string RelativeTimeText { get; set; }
            public Brush AccentBrush { get; set; }
            public Brush BadgeBackground { get; set; }
            public Brush BadgeForeground { get; set; }

            public static ActivityTimelineItem SystemMessage(string title, string body)
            {
                return new ActivityTimelineItem
                {
                    TypeLabel = "Info",
                    Actor = "System",
                    Title = title,
                    Body = body,
                    TimestampUtc = null,
                    TimestampText = "No timestamp",
                    RelativeTimeText = "-",
                    AccentBrush = BrushFromRgb(100, 116, 139),
                    BadgeBackground = BrushFromRgb(241, 245, 249),
                    BadgeForeground = BrushFromRgb(71, 85, 105)
                };
            }
        }
    }
}
