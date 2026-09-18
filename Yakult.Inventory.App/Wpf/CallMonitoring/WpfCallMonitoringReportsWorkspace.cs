using System;
using System.Collections.Generic;
using System.IO;
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
using Microsoft.Win32;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed class WpfCallMonitoringReportsWorkspace : UserControl
    {
        private const int MaxReportRows = 2000;
        private const int PageSize = 14;

        private readonly DatePicker _fromDatePicker;
        private readonly DatePicker _toDatePicker;
        private readonly ComboBox _quickRangeCombo;
        private readonly ComboBox _typeCombo;
        private readonly ComboBox _departmentCombo;
        private readonly ComboBox _locationModeCombo;
        private readonly ComboBox _responsibleCombo;
        private readonly ComboBox _priorityCombo;
        private readonly ComboBox _companyCombo;
        private readonly TextBox _searchBox;
        private readonly Button _refreshButton;
        private readonly Button _clearFiltersButton;
        private readonly Button _exportCsvButton;
        private readonly Button _exportPdfButton;
        private readonly Button _toggleFiltersButton;
        private readonly TextBlock _statusText;
        private readonly TextBlock _tabSummaryText;
        private readonly TextBlock _totalRowsValue;
        private readonly TextBlock _filteredRowsValue;
        private readonly TextBlock _selectedRowsValue;
        private readonly TextBlock _pageValue;
        private readonly TabControl _tabControl;
        private readonly DataGrid _resolutionGrid;
        private readonly DataGrid _slaGrid;
        private readonly DataGrid _solvedGrid;
        private readonly TextBlock _resolutionSummaryText;
        private readonly TextBlock _slaSummaryText;
        private readonly TextBlock _solvedSummaryText;
        private readonly TextBlock _resolutionPagerText;
        private readonly TextBlock _slaPagerText;
        private readonly TextBlock _solvedPagerText;
        private readonly Button _resolutionPrevButton;
        private readonly Button _resolutionNextButton;
        private readonly Button _slaPrevButton;
        private readonly Button _slaNextButton;
        private readonly Button _solvedPrevButton;
        private readonly Button _solvedNextButton;
        // Field Work — borrows Repair Portal card/pill visual language for consistency
        private readonly DataGrid _fieldWorkGrid;
        private readonly TextBlock _fieldWorkSummaryText;
        private readonly TextBlock _fieldWorkPagerText;
        private readonly Button _fieldWorkPrevButton;
        private readonly Button _fieldWorkNextButton;
        private readonly ComboBox _fieldWorkStatusCombo;
        private readonly ComboBox _fieldWorkTechnicianCombo;

        private ICallMonitoringRepository _repository;
        private ICallMonitoringNavigator _navigator;
        private bool _initialized;
        private bool _loading;
        private System.Windows.Threading.DispatcherTimer _searchDebounceTimer;
        private int _resolutionPageIndex = 1;
        private int _slaPageIndex = 1;
        private int _solvedPageIndex = 1;
        private int _fieldWorkPageIndex = 1;

        private List<CallResolutionReportRow> _resolutionRowsAll = new List<CallResolutionReportRow>();
        private List<CallResolutionReportRow> _resolutionRowsFiltered = new List<CallResolutionReportRow>();
        private List<ResolutionRowVm> _resolutionRowsPage = new List<ResolutionRowVm>();

        private List<SlaComplianceDisplayRow> _slaRowsAll = new List<SlaComplianceDisplayRow>();
        private List<SlaComplianceDisplayRow> _slaRowsFiltered = new List<SlaComplianceDisplayRow>();
        private List<SlaRowVm> _slaRowsPage = new List<SlaRowVm>();

        private List<CallSolvedSummaryReportRow> _solvedRowsAll = new List<CallSolvedSummaryReportRow>();
        private List<CallSolvedSummaryReportRow> _solvedRowsFiltered = new List<CallSolvedSummaryReportRow>();
        private List<SolvedRowVm> _solvedRowsPage = new List<SolvedRowVm>();

        private List<CallFieldVisitReportRow> _fieldWorkRowsAll = new List<CallFieldVisitReportRow>();
        private List<CallFieldVisitReportRow> _fieldWorkRowsFiltered = new List<CallFieldVisitReportRow>();
        private List<FieldWorkRowVm> _fieldWorkRowsPage = new List<FieldWorkRowVm>();

        private readonly HashSet<int> _selectedResolutionTicketIds = new HashSet<int>();
        private readonly HashSet<int> _selectedSlaTicketIds = new HashSet<int>();
        private readonly HashSet<int> _selectedSolvedTicketIds = new HashSet<int>();
        private readonly HashSet<int> _selectedFieldWorkIds = new HashSet<int>();

        public WpfCallMonitoringReportsWorkspace()
        {
            Background = BrushFromRgb(241, 244, 247);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var rootGrid = new Grid { Margin = new Thickness(28, 24, 28, 28) };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // Hero
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // Metrics
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // Filters
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // Summary bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Reports (fills remaining height)
            scroll.Content = rootGrid;
            Content = scroll;

            var heroEl = BuildHero();
            Grid.SetRow(heroEl, 0);
            rootGrid.Children.Add(heroEl);

            var metricGrid = new Grid { Margin = new Thickness(0, 22, 0, 0) };
            for (var i = 0; i < 4; i++) metricGrid.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetRow(metricGrid, 1);
            rootGrid.Children.Add(metricGrid);

            _totalRowsValue = CreateMetricValue();
            _filteredRowsValue = CreateMetricValue();
            _selectedRowsValue = CreateMetricValue();
            _pageValue = CreateMetricValue();

            AddMetricCard(metricGrid, 0, "Loaded Rows",   "Rows pulled from the repository", _totalRowsValue,    Color.FromRgb(14, 116, 144));
            AddMetricCard(metricGrid, 1, "Filtered Rows",  "Rows matching current filters",   _filteredRowsValue, Color.FromRgb(37, 99, 235));
            AddMetricCard(metricGrid, 2, "Selected",        "Rows marked for export",          _selectedRowsValue, Color.FromRgb(22, 163, 74));
            AddMetricCard(metricGrid, 3, "Page View",       "Current page and tab",            _pageValue,         Color.FromRgb(124, 58, 237));

            var filterCard = CreateGlassCard();
            filterCard.Margin = new Thickness(0, 22, 0, 0);
            Grid.SetRow(filterCard, 2);
            rootGrid.Children.Add(filterCard);

            _fromDatePicker = CreateDatePicker();
            _toDatePicker = CreateDatePicker();
            _quickRangeCombo = CreateTextComboBox();
            _quickRangeCombo.Items.Add("Last 30 days");
            _quickRangeCombo.Items.Add("Today");
            _quickRangeCombo.Items.Add("Last 7 days");
            _quickRangeCombo.Items.Add("This month");
            _quickRangeCombo.Items.Add("This year");
            _quickRangeCombo.SelectedIndex = 2;

            _typeCombo = CreateTextComboBox();
            _typeCombo.Items.Add("All");
            _typeCombo.Items.Add("Service Only");
            _typeCombo.Items.Add("Replacement");
            _typeCombo.Items.Add("Temporary Replacement");
            _typeCombo.Items.Add("Temporary Service");
            _typeCombo.SelectedIndex = 0;

            _fieldWorkStatusCombo = CreateTextComboBox();
            _fieldWorkStatusCombo.Items.Add("All");
            foreach (var s in new[] { "Scheduled", "Completed", "Cancelled" }) _fieldWorkStatusCombo.Items.Add(s);
            _fieldWorkStatusCombo.SelectedIndex = 0;
            _fieldWorkTechnicianCombo = CreateTextComboBox();
            _fieldWorkTechnicianCombo.Items.Add("All Technicians");
            _fieldWorkTechnicianCombo.SelectedIndex = 0;

            _departmentCombo = CreateTextComboBox();
            _locationModeCombo = CreateTextComboBox();
            _locationModeCombo.Items.Add("All");
            _locationModeCombo.Items.Add("Department only");
            _locationModeCombo.Items.Add("Branch only");
            _locationModeCombo.SelectedIndex = 0;

            _responsibleCombo = CreateTextComboBox();
            _priorityCombo = CreateTextComboBox();
            _priorityCombo.Items.Add("All Priorities");
            _priorityCombo.Items.Add("Critical");
            _priorityCombo.Items.Add("High");
            _priorityCombo.Items.Add("Medium");
            _priorityCombo.Items.Add("Low");
            _priorityCombo.SelectedIndex = 0;

            _companyCombo = CreateTextComboBox();
            _searchBox = CreateTextBox("Search ticket, person, location, remarks...");
            _refreshButton = CreatePrimaryButton("Refresh Report", BrushFromRgb(37, 99, 235));
            _clearFiltersButton = CreatePrimaryButton("Clear", BrushFromRgb(71, 85, 105));
            _exportCsvButton = CreatePrimaryButton("Export CSV", BrushFromRgb(79, 70, 229));
            _exportPdfButton = CreatePrimaryButton("Export PDF", BrushFromRgb(5, 150, 105));
            _toggleFiltersButton = new Button
            {
                Content = "Filters \u25be",
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(14, 9, 14, 9),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            _statusText = new TextBlock
            {
                FontSize = 11.5,
                Foreground = BrushFromRgb(100, 116, 139),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };

            var filterOuterGrid = new Grid();
            filterOuterGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            filterOuterGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            filterCard.Child = filterOuterGrid;

            var topStrip = new Grid();
            topStrip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topStrip.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(topStrip, 0);
            filterOuterGrid.Children.Add(topStrip);

            var topLeftFlow = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
            topLeftFlow.Children.Add(MakeCompactField("From", _fromDatePicker));
            topLeftFlow.Children.Add(MakeCompactField("To", _toDatePicker));
            topLeftFlow.Children.Add(MakeCompactField("Quick Range", _quickRangeCombo));
            topLeftFlow.Children.Add(MakeCompactField("Type", _typeCombo));
            Grid.SetColumn(topLeftFlow, 0);
            topStrip.Children.Add(topLeftFlow);

            var topRightFlow = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(14, 0, 0, 0)
            };
            _refreshButton.Margin = new Thickness(0, 0, 8, 0);
            _clearFiltersButton.Margin = new Thickness(0, 0, 8, 8);
            topRightFlow.Children.Add(_refreshButton);
            topRightFlow.Children.Add(_toggleFiltersButton);
            topRightFlow.Children.Add(_clearFiltersButton);
            topRightFlow.Children.Add(_statusText);
            Grid.SetColumn(topRightFlow, 1);
            topStrip.Children.Add(topRightFlow);

            var expandedStrip = new Border
            {
                Margin = new Thickness(0, 14, 0, 0),
                Padding = new Thickness(0, 12, 0, 0),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(expandedStrip, 1);
            filterOuterGrid.Children.Add(expandedStrip);

            var expandedFlow = new WrapPanel { Orientation = Orientation.Horizontal };
            expandedStrip.Child = expandedFlow;
            expandedFlow.Children.Add(MakeCompactField("Company", _companyCombo));
            expandedFlow.Children.Add(MakeCompactField("Department", _departmentCombo));
            expandedFlow.Children.Add(MakeCompactField("Show", _locationModeCombo));
            expandedFlow.Children.Add(MakeCompactField("Priority", _priorityCombo));
            expandedFlow.Children.Add(MakeCompactField("Responsible", _responsibleCombo));
            expandedFlow.Children.Add(MakeCompactField("Field Status", _fieldWorkStatusCombo));
            expandedFlow.Children.Add(MakeCompactField("Field Tech", _fieldWorkTechnicianCombo));
            _searchBox.MinWidth = 200;
            expandedFlow.Children.Add(MakeCompactField("Search", _searchBox));

            var filtersVisible = false;
            _toggleFiltersButton.Click += (_, __) =>
            {
                filtersVisible = !filtersVisible;
                expandedStrip.Visibility = filtersVisible ? Visibility.Visible : Visibility.Collapsed;
                _toggleFiltersButton.Content = filtersVisible ? "Filters \u25b4" : "Filters \u25be";
                _toggleFiltersButton.Foreground = filtersVisible
                    ? BrushFromRgb(37, 99, 235)
                    : BrushFromRgb(71, 85, 105);
                _toggleFiltersButton.BorderBrush = filtersVisible
                    ? new SolidColorBrush(Color.FromRgb(147, 197, 253))
                    : new SolidColorBrush(Color.FromRgb(203, 213, 225));
            };

            var infoCard = CreateGlassCard();
            infoCard.Margin = new Thickness(0, 18, 0, 0);
            infoCard.Padding = new Thickness(18, 14, 18, 14);
            Grid.SetRow(infoCard, 3);
            rootGrid.Children.Add(infoCard);

            _tabSummaryText = new TextBlock
            {
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(17, 94, 89),
                TextWrapping = TextWrapping.Wrap
            };
            infoCard.Child = _tabSummaryText;

            var reportsCard = CreateGlassCard();
            reportsCard.Margin = new Thickness(0, 18, 0, 0);
            Grid.SetRow(reportsCard, 4);
            rootGrid.Children.Add(reportsCard);

            var reportsLayout = new Grid();
            reportsLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            reportsLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            reportsCard.Child = reportsLayout;

            var reportsHeader = CreateSectionHeader("Reports Workspace", "Resolution analytics, SLA compliance, and solved summary in the same modern WPF treatment as the rest of IT Call Monitoring.");
            Grid.SetRow(reportsHeader, 0);
            reportsLayout.Children.Add(reportsHeader);

            _tabControl = new TabControl
            {
                Margin = new Thickness(0, 18, 0, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            _tabControl.ItemContainerStyle = BuildTabItemStyle();
            Grid.SetRow(_tabControl, 1);
            reportsLayout.Children.Add(_tabControl);

            _resolutionGrid = CreateReportGrid();
            SetupResolutionColumns(_resolutionGrid);
            _resolutionSummaryText = CreateInlineSummaryText();
            _resolutionPagerText = CreatePagerText();
            _resolutionPrevButton = CreatePagerButton("Prev");
            _resolutionNextButton = CreatePagerButton("Next");
            _tabControl.Items.Add(CreateReportTab("Resolution",    "Replacement/service outcomes and marked actions.",                               _resolutionSummaryText, _resolutionGrid, _resolutionPagerText, _resolutionPrevButton, _resolutionNextButton, _exportCsvButton, _exportPdfButton));

            _slaGrid = CreateReportGrid();
            SetupSlaColumns(_slaGrid);
            _slaSummaryText    = CreateInlineSummaryText();
            _slaPagerText      = CreatePagerText();
            _slaPrevButton     = CreatePagerButton("Prev");
            _slaNextButton     = CreatePagerButton("Next");
            _tabControl.Items.Add(CreateReportTab("SLA Compliance", "Resolution timing against the priority SLA targets.",                           _slaSummaryText,        _slaGrid,        _slaPagerText,        _slaPrevButton,        _slaNextButton,        _exportCsvButton, _exportPdfButton));

            _solvedGrid = CreateReportGrid();
            SetupSolvedColumns(_solvedGrid);
            _solvedSummaryText = CreateInlineSummaryText();
            _solvedPagerText   = CreatePagerText();
            _solvedPrevButton  = CreatePagerButton("Prev");
            _solvedNextButton  = CreatePagerButton("Next");
            _tabControl.Items.Add(CreateReportTab("Solved Summary", "Closed ticket summaries, callers, solutions, and turnaround.",                  _solvedSummaryText,     _solvedGrid,     _solvedPagerText,     _solvedPrevButton,     _solvedNextButton,     _exportCsvButton, _exportPdfButton));

            _fieldWorkGrid = CreateReportGrid();
            SetupFieldWorkColumns(_fieldWorkGrid);
            _fieldWorkSummaryText = CreateInlineSummaryText();
            _fieldWorkPagerText   = CreatePagerText();
            _fieldWorkPrevButton  = CreatePagerButton("Prev");
            _fieldWorkNextButton  = CreatePagerButton("Next");
            _tabControl.Items.Add(CreateReportTab("Field Work",     "Scheduled → Completed / Cancelled (Cancelled reschedulable). Double-click a row to view full details, timeline, notes, photos and signature.", _fieldWorkSummaryText,  _fieldWorkGrid,  _fieldWorkPagerText,  _fieldWorkPrevButton,  _fieldWorkNextButton,  _exportCsvButton, _exportPdfButton));

            HookEvents();
            InitializeFilterLists();
            ApplyQuickRange();
            UpdateFilterStateForActiveTab();
            UpdateWorkspaceSummary();
        }

        public void Initialize(ICallMonitoringRepository repository, ICallMonitoringNavigator navigator)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _navigator = navigator;
            _initialized = true;
        }

        public Task LoadDataAsync(bool force = false)
        {
            if (!_initialized || _repository == null)
            {
                return Task.CompletedTask;
            }

            if (_loading && !force)
            {
                return Task.CompletedTask;
            }

            return RefreshActiveAsync();
        }

        public Task ApplyFilterAsync(DateTime fromLocal, DateTime toLocal, string type)
        {
            if (Dispatcher.CheckAccess())
            {
                _fromDatePicker.SelectedDate = fromLocal.Date;
                _toDatePicker.SelectedDate = toLocal.Date;

                var desired = (type ?? "All").Trim();
                SelectStringComboValue(_typeCombo, desired, defaultIndex: 0);
                _tabControl.SelectedIndex = 0;
                return RefreshActiveAsync();
            }

            return Dispatcher.InvokeAsync(() => ApplyFilterAsync(fromLocal, toLocal, type)).Task.Unwrap();
        }

        private void HookEvents()
        {
            _quickRangeCombo.SelectionChanged += async (_, __) =>
            {
                ApplyQuickRange();
                await RefreshActiveAsync();
            };

            _typeCombo.SelectionChanged += async (_, __) =>
            {
                if ((_tabControl.SelectedIndex == 0) || _resolutionRowsAll.Count == 0)
                {
                    await RefreshActiveAsync();
                }
                else
                {
                    ApplyFilters();
                }
            };

            _refreshButton.Click += async (_, __) => await RefreshActiveAsync();
            _clearFiltersButton.Click += (_, __) => ClearFilters();
            _exportCsvButton.Click += (_, __) => ExportCsv();
            _exportPdfButton.Click += (_, __) => ExportPdf();

            _departmentCombo.SelectionChanged += (_, __) => ApplyFilters();
            _locationModeCombo.SelectionChanged += (_, __) =>
            {
                UpdateDepartmentFilterEnabledState();
                ApplyFilters();
            };
            _responsibleCombo.SelectionChanged += (_, __) => ApplyFilters();
            _priorityCombo.SelectionChanged += (_, __) => ApplyFilters();
            _companyCombo.SelectionChanged += (_, __) => ApplyFilters();
            // Debounce search: wait 300ms after last keystroke before filtering
            _searchDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchDebounceTimer.Tick += (_, __) =>
            {
                _searchDebounceTimer.Stop();
                ApplyFilters();
            };
            _searchBox.TextChanged += (_, __) =>
            {
                _searchDebounceTimer.Stop();
                _searchDebounceTimer.Start();
            };

            _tabControl.SelectionChanged += async (_, __) =>
            {
                UpdateFilterStateForActiveTab();
                await RefreshActiveIfEmptyAsync();
                ApplyFilters();
            };

            _resolutionPrevButton.Click += (_, __) =>
            {
                _resolutionPageIndex--;
                UpdateResolutionPage();
            };
            _resolutionNextButton.Click += (_, __) =>
            {
                _resolutionPageIndex++;
                UpdateResolutionPage();
            };
            _slaPrevButton.Click += (_, __) =>
            {
                _slaPageIndex--;
                UpdateSlaPage();
            };
            _slaNextButton.Click += (_, __) =>
            {
                _slaPageIndex++;
                UpdateSlaPage();
            };
            _solvedPrevButton.Click += (_, __) =>
            {
                _solvedPageIndex--;
                UpdateSolvedPage();
            };
            _solvedNextButton.Click += (_, __) =>
            {
                _solvedPageIndex++;
                UpdateSolvedPage();
            };

            _resolutionGrid.MouseDoubleClick += (_, __) =>
            {
                if (_resolutionGrid.SelectedItem is ResolutionRowVm row)
                {
                    OpenTicket(row.TicketId, row.TicketCode);
                }
            };
            _slaGrid.MouseDoubleClick += (_, __) =>
            {
                if (_slaGrid.SelectedItem is SlaRowVm row)
                {
                    OpenTicket(row.TicketId, row.TicketCode);
                }
            };
            _solvedGrid.MouseDoubleClick += (_, __) =>
            {
                if (_solvedGrid.SelectedItem is SolvedRowVm row)
                {
                    OpenTicket(row.TicketId, row.TicketCode);
                }
            };
            _fieldWorkGrid.MouseDoubleClick += (_, __) =>
            {
                if (_fieldWorkGrid.SelectedItem is FieldWorkRowVm row && row.Source != null)
                {
                    OpenFieldWorkDetail(row.Source);
                }
            };

            // The blue Ticket column is an explicit single-click target. Other cells
            // still select the full row, and the first checkbox column remains reserved
            // for export selection.
            _resolutionGrid.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (TryGetTicketCellItem(_resolutionGrid, e.OriginalSource, out var item) && item is ResolutionRowVm row)
                {
                    e.Handled = true;
                    OpenTicket(row.TicketId, row.TicketCode);
                }
            };
            _slaGrid.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (TryGetTicketCellItem(_slaGrid, e.OriginalSource, out var item) && item is SlaRowVm row)
                {
                    e.Handled = true;
                    OpenTicket(row.TicketId, row.TicketCode);
                }
            };
            _solvedGrid.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (TryGetTicketCellItem(_solvedGrid, e.OriginalSource, out var item) && item is SolvedRowVm row)
                {
                    e.Handled = true;
                    OpenTicket(row.TicketId, row.TicketCode);
                }
            };
            _fieldWorkGrid.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (TryGetTicketCellItem(_fieldWorkGrid, e.OriginalSource, out var item) && item is FieldWorkRowVm row && row.Source != null)
                {
                    e.Handled = true;
                    OpenTicket(row.Source.TicketId, row.Source.TicketCode);
                }
            };

            _resolutionGrid.CellEditEnding += (_, e) => HandleResolutionCheckEdit(e);
            _slaGrid.CellEditEnding += (_, e) => HandleSlaCheckEdit(e);
            _solvedGrid.CellEditEnding += (_, e) => HandleSolvedCheckEdit(e);
            _fieldWorkGrid.CellEditEnding += (_, e) => HandleFieldWorkCheckEdit(e);
        }

        private async Task RefreshActiveIfEmptyAsync()
        {
            if (_tabControl.SelectedIndex == 1)
            {
                if (_slaRowsAll.Count == 0)
                {
                    await RefreshActiveAsync();
                }
            }
            else if (_tabControl.SelectedIndex == 2)
            {
                if (_solvedRowsAll.Count == 0)
                {
                    await RefreshActiveAsync();
                }
            }
            else if (_tabControl.SelectedIndex == 3)
            {
                if (_fieldWorkRowsAll.Count == 0)
                {
                    await RefreshActiveAsync();
                }
            }
            else if (_resolutionRowsAll.Count == 0)
            {
                await RefreshActiveAsync();
            }
        }

        private async Task RefreshActiveAsync()
        {
            if (_repository == null)
            {
                return;
            }

            if (_loading)
            {
                return;
            }

            _loading = true;
            _refreshButton.IsEnabled = false;
            _exportCsvButton.IsEnabled = false;
            _exportPdfButton.IsEnabled = false;
            _statusText.Text = "Loading report data...";

            try
            {
                if (!await _repository.CallSchemaExistsAsync())
                {
                    _statusText.Text = "Call Monitoring schema is not installed.";
                    _resolutionRowsAll.Clear();
                    _resolutionRowsFiltered.Clear();
                    _slaRowsAll.Clear();
                    _slaRowsFiltered.Clear();
                    _solvedRowsAll.Clear();
                    _solvedRowsFiltered.Clear();
                    UpdateResolutionPage();
                    UpdateSlaPage();
                    UpdateSolvedPage();
                    UpdateWorkspaceSummary();
                    return;
                }

                var fromLocal = (_fromDatePicker.SelectedDate ?? DateTime.Today).Date;
                var toLocal = (_toDatePicker.SelectedDate ?? DateTime.Today).Date;
                if (fromLocal > toLocal)
                {
                    WpfItcmDialogService.ShowWarning(this, "'From' date must be earlier than or equal to 'To' date.", "Call Monitoring Reports");
                    _statusText.Text = "Invalid date range.";
                    return;
                }

                var fromUtc = DateTime.SpecifyKind(fromLocal, DateTimeKind.Local).ToUniversalTime();
                var toUtc = DateTime.SpecifyKind(toLocal.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime();

                _selectedResolutionTicketIds.Clear();
                _selectedSlaTicketIds.Clear();
                _selectedSolvedTicketIds.Clear();

                if (_tabControl.SelectedIndex == 1)
                {
                    await RefreshSlaAsync(fromUtc, toUtc);
                }
                else if (_tabControl.SelectedIndex == 2)
                {
                    await RefreshSolvedAsync(fromUtc, toUtc);
                }
                else if (_tabControl.SelectedIndex == 3)
                {
                    await RefreshFieldWorkAsync(fromUtc, toUtc);
                }
                else
                {
                    await RefreshResolutionAsync(fromUtc, toUtc);
                }

                ApplyFilters();
                _statusText.Text = "Report data refreshed.";
            }
            catch (Exception ex)
            {
                _statusText.Text = "Failed to load reports.";
                WpfItcmDialogService.ShowError(this, ex.Message, "Call Monitoring Reports");
            }
            finally
            {
                _loading = false;
                _refreshButton.IsEnabled = true;
                _exportCsvButton.IsEnabled = HasExportableRows();
                _exportPdfButton.IsEnabled = HasExportableRows();
            }
        }

        private async Task RefreshResolutionAsync(DateTime fromUtc, DateTime toUtc)
        {
            var type = GetSelectedComboText(_typeCombo, "All");
            List<CallResolutionReportRow> rows;

            if (string.Equals(type, "Temporary Replacement", StringComparison.OrdinalIgnoreCase))
            {
                rows = await _repository.GetResolutionReportAsync(fromUtc, toUtc, "Replacement", MaxReportRows);
                rows = (rows ?? new List<CallResolutionReportRow>())
                    .Where(r => string.Equals(r.TicketStatus, "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var row in rows)
                {
                    row.ResolutionType = "Temporary Replacement";
                }
            }
            else if (string.Equals(type, "Temporary Service", StringComparison.OrdinalIgnoreCase))
            {
                rows = await _repository.GetResolutionReportAsync(fromUtc, toUtc, "Service Only", MaxReportRows);
                rows = (rows ?? new List<CallResolutionReportRow>())
                    .Where(r => string.Equals(r.TicketStatus, "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var row in rows)
                {
                    row.ResolutionType = "Temporary Service";
                }
            }
            else
            {
                rows = await _repository.GetResolutionReportAsync(fromUtc, toUtc, type, MaxReportRows);
            }

            _resolutionRowsAll = rows ?? new List<CallResolutionReportRow>();
            RebuildDepartmentAndResponsibleLists(_resolutionRowsAll.Select(r => r.Department), _resolutionRowsAll.Select(r => r.ResponsiblePerson));
        }

        private async Task RefreshSlaAsync(DateTime fromUtc, DateTime toUtc)
        {
            var rows = await _repository.GetSlaComplianceReportAsync(fromUtc, toUtc, MaxReportRows) ?? new List<CallSlaComplianceReportRow>();
            _slaRowsAll = rows.Select(BuildSlaDisplayRow).Where(r => r != null).ToList();
            RebuildDepartmentAndResponsibleLists(_slaRowsAll.Select(r => r.Department), _slaRowsAll.Select(r => r.ResponsiblePerson));
        }

        private async Task RefreshSolvedAsync(DateTime fromUtc, DateTime toUtc)
        {
            var rows = await _repository.GetSolvedSummaryReportAsync(fromUtc, toUtc, MaxReportRows);
            _solvedRowsAll = rows ?? new List<CallSolvedSummaryReportRow>();
            RebuildDepartmentAndResponsibleLists(_solvedRowsAll.Select(r => r.Department), _solvedRowsAll.Select(r => r.ResponsiblePerson));
            RebuildCompanyList(_solvedRowsAll.Select(r => r.Company));
        }

        private void InitializeFilterLists()
        {
            ResetComboItems(_departmentCombo, "All Departments");
            ResetComboItems(_responsibleCombo, "All Responsible");
            ResetComboItems(_companyCombo, "All Companies");
        }

        private void ClearFilters()
        {
            SelectStringComboValue(_locationModeCombo, "All", defaultIndex: 0);
            SelectStringComboValue(_departmentCombo, "All Departments", defaultIndex: 0);
            SelectStringComboValue(_responsibleCombo, "All Responsible", defaultIndex: 0);
            SelectStringComboValue(_companyCombo, "All Companies", defaultIndex: 0);
            SelectStringComboValue(_priorityCombo, "All Priorities", defaultIndex: 0);
            _searchBox.Text = string.Empty;
            ApplyFilters();
        }

        private void ApplyQuickRange()
        {
            var today = DateTime.Today;
            var choice = GetSelectedComboText(_quickRangeCombo, "Last 30 days");
            if (string.Equals(choice, "Today", StringComparison.OrdinalIgnoreCase))
            {
                _fromDatePicker.SelectedDate = today;
                _toDatePicker.SelectedDate = today;
            }
            else if (string.Equals(choice, "Last 7 days", StringComparison.OrdinalIgnoreCase))
            {
                _fromDatePicker.SelectedDate = today.AddDays(-6);
                _toDatePicker.SelectedDate = today;
            }
            else if (string.Equals(choice, "This month", StringComparison.OrdinalIgnoreCase))
            {
                _fromDatePicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
                _toDatePicker.SelectedDate = today;
            }
            else if (string.Equals(choice, "This year", StringComparison.OrdinalIgnoreCase))
            {
                _fromDatePicker.SelectedDate = new DateTime(today.Year, 1, 1);
                _toDatePicker.SelectedDate = today;
            }
            else
            {
                _fromDatePicker.SelectedDate = today.AddDays(-30);
                _toDatePicker.SelectedDate = today;
            }
        }

        private void ApplyFilters()
        {
            UpdateDepartmentFilterEnabledState();

            if (_tabControl.SelectedIndex == 1)
            {
                ApplySlaFilters();
            }
            else if (_tabControl.SelectedIndex == 2)
            {
                ApplySolvedFilters();
            }
            else
            {
                ApplyResolutionFilters();
            }

            _exportCsvButton.IsEnabled = HasExportableRows();
            _exportPdfButton.IsEnabled = HasExportableRows();
            UpdateWorkspaceSummary();
        }

        private void ApplyResolutionFilters()
        {
            var locationMode = _locationModeCombo.SelectedIndex;
            var dept = NormalizeAllText(GetSelectedComboText(_departmentCombo, "All Departments"), "All Departments");
            var resp = NormalizeAllText(GetSelectedComboText(_responsibleCombo, "All Responsible"), "All Responsible");
            var prio = NormalizePriorityText(GetSelectedComboText(_priorityCombo, "All Priorities"));
            var query = (_searchBox.Text ?? string.Empty).Trim().ToUpperInvariant();
            var hasQuery = query.Length > 0;

            if (locationMode == 2)
            {
                dept = null;
            }

            _resolutionRowsFiltered = (_resolutionRowsAll ?? new List<CallResolutionReportRow>())
                .Where(r =>
                {
                    if (r == null) return false;

                    var hasDept = !string.IsNullOrWhiteSpace(r.Department);
                    var hasLocation = !string.IsNullOrWhiteSpace(r.Location);
                    if (locationMode == 1 && !hasDept) return false;
                    if (locationMode == 2 && (hasDept || !hasLocation)) return false;
                    if (!string.IsNullOrWhiteSpace(dept) && !string.Equals((r.Department ?? string.Empty).Trim(), dept, StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrWhiteSpace(resp) && !string.Equals((r.ResponsiblePerson ?? string.Empty).Trim(), resp, StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrWhiteSpace(prio) && !string.Equals((r.Priority ?? string.Empty).Trim(), prio, StringComparison.OrdinalIgnoreCase)) return false;
                    if (hasQuery)
                    {
                        var hay = $"{r.TicketCode} {r.TicketStatus} {r.Priority} {r.ResolutionType} {r.Location} {r.Department} {r.ResponsiblePerson} {r.ReplacementOldItem} {r.ReplacementNewItem} {r.MarkedByName} {r.Remarks}".ToUpperInvariant();
                        if (!hay.Contains(query)) return false;
                    }

                    return true;
                })
                .ToList();

            _selectedResolutionTicketIds.IntersectWith(_resolutionRowsFiltered.Select(r => r?.TicketId ?? 0).Where(id => id > 0));
            _resolutionPageIndex = 1;
            UpdateResolutionPage();
        }

        private void ApplySlaFilters()
        {
            var locationMode = _locationModeCombo.SelectedIndex;
            var dept = NormalizeAllText(GetSelectedComboText(_departmentCombo, "All Departments"), "All Departments");
            var resp = NormalizeAllText(GetSelectedComboText(_responsibleCombo, "All Responsible"), "All Responsible");
            var prio = NormalizePriorityText(GetSelectedComboText(_priorityCombo, "All Priorities"));
            var query = (_searchBox.Text ?? string.Empty).Trim().ToUpperInvariant();
            var hasQuery = query.Length > 0;

            if (locationMode == 2)
            {
                dept = null;
            }

            _slaRowsFiltered = (_slaRowsAll ?? new List<SlaComplianceDisplayRow>())
                .Where(r =>
                {
                    if (r == null) return false;

                    var hasDept = !string.IsNullOrWhiteSpace(r.Department);
                    var hasLocation = !string.IsNullOrWhiteSpace(r.Location);
                    if (locationMode == 1 && !hasDept) return false;
                    if (locationMode == 2 && (hasDept || !hasLocation)) return false;
                    if (!string.IsNullOrWhiteSpace(dept) && !string.Equals((r.Department ?? string.Empty).Trim(), dept, StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrWhiteSpace(resp) && !string.Equals((r.ResponsiblePerson ?? string.Empty).Trim(), resp, StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrWhiteSpace(prio) && !string.Equals((r.Priority ?? string.Empty).Trim(), prio, StringComparison.OrdinalIgnoreCase)) return false;
                    if (hasQuery)
                    {
                        var hay = $"{r.TicketCode} {r.Location} {r.Department} {r.Priority} {r.ResponsiblePerson} {r.CompletedByName} {r.CompletedStatus} {r.SlaResult}".ToUpperInvariant();
                        if (!hay.Contains(query)) return false;
                    }

                    return true;
                })
                .ToList();

            _selectedSlaTicketIds.IntersectWith(_slaRowsFiltered.Select(r => r?.TicketId ?? 0).Where(id => id > 0));
            _slaPageIndex = 1;
            UpdateSlaPage();
        }

        private void ApplySolvedFilters()
        {
            var locationMode = _locationModeCombo.SelectedIndex;
            var company = NormalizeAllText(GetSelectedComboText(_companyCombo, "All Companies"), "All Companies");
            var dept = NormalizeAllText(GetSelectedComboText(_departmentCombo, "All Departments"), "All Departments");
            var resp = NormalizeAllText(GetSelectedComboText(_responsibleCombo, "All Responsible"), "All Responsible");
            var prio = NormalizePriorityText(GetSelectedComboText(_priorityCombo, "All Priorities"));
            var query = (_searchBox.Text ?? string.Empty).Trim().ToUpperInvariant();
            var hasQuery = query.Length > 0;

            if (locationMode == 2)
            {
                dept = null;
            }

            _solvedRowsFiltered = (_solvedRowsAll ?? new List<CallSolvedSummaryReportRow>())
                .Where(r =>
                {
                    if (r == null) return false;

                    var hasDept = !string.IsNullOrWhiteSpace(r.Department);
                    var hasLocation = !string.IsNullOrWhiteSpace(r.Location);
                    if (locationMode == 1 && !hasDept) return false;
                    if (locationMode == 2 && (hasDept || !hasLocation)) return false;
                    if (!string.IsNullOrWhiteSpace(company) && !string.Equals((r.Company ?? string.Empty).Trim(), company, StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrWhiteSpace(dept) && !string.Equals((r.Department ?? string.Empty).Trim(), dept, StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrWhiteSpace(resp) && !string.Equals((r.ResponsiblePerson ?? string.Empty).Trim(), resp, StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.IsNullOrWhiteSpace(prio) && !string.Equals((r.Priority ?? string.Empty).Trim(), prio, StringComparison.OrdinalIgnoreCase)) return false;
                    if (hasQuery)
                    {
                        var hay = $"{r.TicketCode} {r.Company} {r.CallerName} {r.Location} {r.Priority} {r.ResponsiblePerson} {r.FinalStatus} {r.Problem} {r.Solution}".ToUpperInvariant();
                        if (!hay.Contains(query)) return false;
                    }

                    return true;
                })
                .ToList();

            _selectedSolvedTicketIds.IntersectWith(_solvedRowsFiltered.Select(r => r?.TicketId ?? 0).Where(id => id > 0));
            _solvedPageIndex = 1;
            UpdateSolvedPage();
        }

        private void UpdateResolutionPage()
        {
            var totalPages = NormalizePageIndex(ref _resolutionPageIndex, _resolutionRowsFiltered.Count);
            _resolutionRowsPage = _resolutionRowsFiltered
                .Skip((_resolutionPageIndex - 1) * PageSize)
                .Take(PageSize)
                .Select(r => new ResolutionRowVm(r, _selectedResolutionTicketIds.Contains(r.TicketId)))
                .ToList();

            _resolutionGrid.ItemsSource = _resolutionRowsPage;
            _resolutionPagerText.Text = $"Page {_resolutionPageIndex} of {totalPages}";
            _resolutionPrevButton.IsEnabled = _resolutionPageIndex > 1;
            _resolutionNextButton.IsEnabled = _resolutionPageIndex < totalPages;
            _resolutionSummaryText.Text = _resolutionRowsFiltered.Count == 0
                ? BuildEmptyReportLabel()
                : BuildRowCountLabel(_resolutionRowsFiltered.Count);
        }

        private void UpdateSlaPage()
        {
            var totalPages = NormalizePageIndex(ref _slaPageIndex, _slaRowsFiltered.Count);
            _slaRowsPage = _slaRowsFiltered
                .Skip((_slaPageIndex - 1) * PageSize)
                .Take(PageSize)
                .Select(r => new SlaRowVm(r, _selectedSlaTicketIds.Contains(r.TicketId)))
                .ToList();

            _slaGrid.ItemsSource = _slaRowsPage;
            _slaPagerText.Text = $"Page {_slaPageIndex} of {totalPages}";
            _slaPrevButton.IsEnabled = _slaPageIndex > 1;
            _slaNextButton.IsEnabled = _slaPageIndex < totalPages;
            UpdateSlaSummary();
        }

        private void UpdateSolvedPage()
        {
            var totalPages = NormalizePageIndex(ref _solvedPageIndex, _solvedRowsFiltered.Count);
            _solvedRowsPage = _solvedRowsFiltered
                .Skip((_solvedPageIndex - 1) * PageSize)
                .Take(PageSize)
                .Select(r => new SolvedRowVm(r, _selectedSolvedTicketIds.Contains(r.TicketId)))
                .ToList();

            _solvedGrid.ItemsSource = _solvedRowsPage;
            _solvedPagerText.Text = $"Page {_solvedPageIndex} of {totalPages}";
            _solvedPrevButton.IsEnabled = _solvedPageIndex > 1;
            _solvedNextButton.IsEnabled = _solvedPageIndex < totalPages;
            UpdateSolvedSummary();
        }

        private void UpdateFieldWorkPage()
        {
            var totalPages = NormalizePageIndex(ref _fieldWorkPageIndex, _fieldWorkRowsFiltered.Count);
            _fieldWorkRowsPage = _fieldWorkRowsFiltered
                .Skip((_fieldWorkPageIndex - 1) * PageSize)
                .Take(PageSize)
                .Select(r => new FieldWorkRowVm(r, _selectedFieldWorkIds.Contains(r.FieldVisitId)))
                .ToList();
            _fieldWorkGrid.ItemsSource = _fieldWorkRowsPage;
            _fieldWorkPagerText.Text = $"Page {_fieldWorkPageIndex} of {totalPages}";
            _fieldWorkPrevButton.IsEnabled = _fieldWorkPageIndex > 1;
            _fieldWorkNextButton.IsEnabled = _fieldWorkPageIndex < totalPages;
            UpdateFieldWorkSummary();
        }

        private void UpdateFieldWorkSummary()
        {
            var total = _fieldWorkRowsFiltered.Count;
            if (total == 0)
            {
                _fieldWorkSummaryText.Text = HasActiveReportFilters() ? "Field Work: no rows match the current filters." : "Field Work: no visits found for the selected date range (by scheduled date, fallback created).";
                _fieldWorkSummaryText.Foreground = BrushFromRgb(100, 116, 139);
                _fieldWorkSummaryText.FontWeight = FontWeights.Normal;
                _fieldWorkSummaryText.FontStyle = FontStyles.Italic;
                return;
            }
            var completed = _fieldWorkRowsFiltered.Count(r => string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase));
            var scheduled = _fieldWorkRowsFiltered.Count(r => string.Equals(r.Status, "Scheduled", StringComparison.OrdinalIgnoreCase));
            var cancelled = _fieldWorkRowsFiltered.Count(r => string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));
            var signed = _fieldWorkRowsFiltered.Count(r => r.HasSignature);
            var isTruncated = _fieldWorkRowsAll != null && _fieldWorkRowsAll.Count >= MaxReportRows;
            var trunc = isTruncated ? $" • {BuildRowCountLabel(_fieldWorkRowsAll.Count)} — narrow dates" : string.Empty;
            _fieldWorkSummaryText.Text = $"Field Work: {total} visits • {completed} completed • {scheduled} scheduled • {cancelled} cancelled • {signed} signed{trunc}";
            _fieldWorkSummaryText.FontStyle = FontStyles.Normal;
            if (isTruncated)
            {
                _fieldWorkSummaryText.Foreground = BrushFromRgb(185, 28, 28);
                _fieldWorkSummaryText.FontWeight = FontWeights.SemiBold;
            }
            else
            {
                _fieldWorkSummaryText.Foreground = BrushFromRgb(100, 116, 139);
                _fieldWorkSummaryText.FontWeight = FontWeights.Normal;
            }
        }

        private async Task RefreshFieldWorkAsync(DateTime fromUtc, DateTime toUtc)
        {
            var status = GetSelectedComboText(_fieldWorkStatusCombo, "All");
            if (string.Equals(status, "All", StringComparison.OrdinalIgnoreCase)) status = null;
            var techText = GetSelectedComboText(_fieldWorkTechnicianCombo, "All Technicians");
            int? techId = null;
            if (!string.Equals(techText, "All Technicians", StringComparison.OrdinalIgnoreCase))
            {
                var m = System.Text.RegularExpressions.Regex.Match(techText, @"\(ID:(\d+)\)");
                if (m.Success && int.TryParse(m.Groups[1].Value, out var id)) techId = id;
            }
            var rows = await _repository.GetFieldWorkReportAsync(fromUtc, toUtc, status, techId, MaxReportRows) ?? new List<CallFieldVisitReportRow>();
            _fieldWorkRowsAll = rows;
            RebuildFieldWorkTechnicianList(rows);
            ApplyFieldWorkFilters();
        }

        private void RebuildFieldWorkTechnicianList(List<CallFieldVisitReportRow> rows)
        {
            var prev = GetSelectedComboText(_fieldWorkTechnicianCombo, "All Technicians");
            var distinct = rows.Where(r => r.TechnicianEmpId.HasValue && !string.IsNullOrWhiteSpace(r.TechnicianName))
                .GroupBy(r => r.TechnicianEmpId.Value).Select(g => g.First()).OrderBy(r => r.TechnicianName).ToList();
            _fieldWorkTechnicianCombo.Items.Clear();
            _fieldWorkTechnicianCombo.Items.Add("All Technicians");
            _fieldWorkTechnicianCombo.Items.Add("Unassigned");
            foreach (var r in distinct) _fieldWorkTechnicianCombo.Items.Add($"{r.TechnicianName} (ID:{r.TechnicianEmpId})");
            SelectStringComboValue(_fieldWorkTechnicianCombo, prev, 0);
        }

        private void ApplyFieldWorkFilters()
        {
            var search = (_searchBox.Text ?? "").Trim();
            var dept = NormalizeAllText(GetSelectedComboText(_departmentCombo, "All Departments"), "All Departments");
            var locMode = _locationModeCombo.SelectedIndex; // 0 All,1 Dept only,2 Branch only
            var status = NormalizeAllText(GetSelectedComboText(_fieldWorkStatusCombo, "All"), "All");
            var techText = GetSelectedComboText(_fieldWorkTechnicianCombo, "All Technicians");
            int? techId = null;
            var unassignedOnly = string.Equals(techText, "Unassigned", StringComparison.OrdinalIgnoreCase);
            var m = System.Text.RegularExpressions.Regex.Match(techText, @"\(ID:(\d+)\)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var id)) techId = id;

            _fieldWorkRowsFiltered = (_fieldWorkRowsAll ?? new List<CallFieldVisitReportRow>()).Where(r =>
            {
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var hay = $"{r.TicketCode} {r.Issue} {r.TechnicianName} {r.Location} {r.Department} {r.Branch} {r.Status} {r.Notes}";
                    if (hay.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) return false;
                }
                if (!string.IsNullOrWhiteSpace(dept) && !string.Equals(dept, "All Departments", StringComparison.OrdinalIgnoreCase))
                    if (!string.Equals(r.Department ?? "", dept, StringComparison.OrdinalIgnoreCase)) return false;
                if (locMode == 1 && string.IsNullOrWhiteSpace(r.Department)) return false;
                if (locMode == 2 && string.IsNullOrWhiteSpace(r.Branch)) return false;
                if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
                    if (!string.Equals(r.Status ?? "", status, StringComparison.OrdinalIgnoreCase)) return false;
                if (techId.HasValue && r.TechnicianEmpId != techId.Value) return false;
                if (unassignedOnly && r.TechnicianEmpId.HasValue) return false;
                return true;
            }).ToList();
            _selectedFieldWorkIds.IntersectWith(_fieldWorkRowsFiltered.Select(r => r.FieldVisitId).Where(id => id > 0));
            _fieldWorkPageIndex = 1;
            UpdateFieldWorkPage();
        }

        private List<CallFieldVisitReportRow> GetSelectedFieldWorkRowsForExport()
        {
            var selected = _fieldWorkRowsFiltered.Where(r => r != null && _selectedFieldWorkIds.Contains(r.FieldVisitId)).ToList();
            if (selected.Count > 0) return selected;
            if (_selectedFieldWorkIds.Count > 0) return new List<CallFieldVisitReportRow>();
            return _fieldWorkRowsFiltered.Where(r => r != null).ToList();
        }

        private void HandleFieldWorkCheckEdit(DataGridCellEditEndingEventArgs e)
        {
            if (!(e.Row.Item is FieldWorkRowVm row) || !(e.EditingElement is CheckBox checkBox)) return;
            var isChecked = checkBox.IsChecked == true;
            if (isChecked) _selectedFieldWorkIds.Add(row.FieldVisitId);
            else _selectedFieldWorkIds.Remove(row.FieldVisitId);
        }

        private void UpdateSlaSummary()
        {
            var total = _slaRowsFiltered.Count;
            if (total == 0)
            {
                _slaSummaryText.Text = "SLA: no rows match the current filters.";
                return;
            }

            var breached = _slaRowsFiltered.Count(r => string.Equals(r.SlaResult, "Breached", StringComparison.OrdinalIgnoreCase));
            var met = total - breached;
            var breachedHours = _slaRowsFiltered.Sum(r => Math.Max(0, r.BreachedByHours));
            _slaSummaryText.Text = $"SLA: {met} met, {breached} breached, {breachedHours:0.0} total breached hours.";
        }

        private string BuildEmptyReportLabel()
        {
            return HasActiveReportFilters()
                ? "No rows match the current filters. Clear filters or widen the date range."
                : "No rows found for the selected date range.";
        }

        private bool HasActiveReportFilters()
        {
            return !string.IsNullOrWhiteSpace(_searchBox.Text)
                   || !string.Equals(GetSelectedComboText(_departmentCombo, "All Departments"), "All Departments", StringComparison.OrdinalIgnoreCase)
                   || !string.Equals(GetSelectedComboText(_companyCombo, "All Companies"), "All Companies", StringComparison.OrdinalIgnoreCase)
                   || !string.Equals(GetSelectedComboText(_responsibleCombo, "All Responsible"), "All Responsible", StringComparison.OrdinalIgnoreCase)
                   || !string.Equals(GetSelectedComboText(_priorityCombo, "All Priorities"), "All Priorities", StringComparison.OrdinalIgnoreCase)
                   || _locationModeCombo.SelectedIndex > 0;
        }

        private void UpdateSolvedSummary()
        {
            var total = _solvedRowsFiltered.Count;
            if (total == 0)
            {
                _solvedSummaryText.Text = "Solved Summary: no rows match the current filters.";
                return;
            }

            var avgHours = _solvedRowsFiltered.Average(r => r.ResolutionHours);
            var critical = _solvedRowsFiltered.Count(r => string.Equals(r.Priority, "Critical", StringComparison.OrdinalIgnoreCase));
            _solvedSummaryText.Text = $"Solved Summary: {total} rows, {avgHours:0.0} average resolution hours, {critical} critical tickets.";
        }

        private void UpdateWorkspaceSummary()
        {
            var filteredCount = GetCurrentFilteredCount();
            var totalCount = GetCurrentTotalCount();
            var selectedCount = GetCurrentSelectedCount();
            var pageIndex = GetCurrentPageIndex();
            var totalPages = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)PageSize));
            var tabName = GetActiveTabName();

            _totalRowsValue.Text = totalCount.ToString();
            _filteredRowsValue.Text = filteredCount.ToString();
            _selectedRowsValue.Text = selectedCount.ToString();
            _pageValue.Text = $"{pageIndex}/{totalPages}";

            var fromText = (_fromDatePicker.SelectedDate ?? DateTime.Today).ToString("MMM dd, yyyy");
            var toText = (_toDatePicker.SelectedDate ?? DateTime.Today).ToString("MMM dd, yyyy");
            _tabSummaryText.Text = $"{tabName} report from {fromText} to {toText}. Click a blue ticket code or double-click any row to open the related ticket.";
        }

        private bool HasExportableRows()
        {
            return GetCurrentFilteredCount() > 0;
        }

        private void ExportCsv()
        {
            if (!HasExportableRows())
            {
                WpfItcmDialogService.ShowInfo(this, "No rows to export.", "Call Monitoring Reports");
                return;
            }

            var fileName = _tabControl.SelectedIndex == 1
                ? $"call_monitoring_sla_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                : _tabControl.SelectedIndex == 2
                    ? $"call_monitoring_solved_summary_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                    : $"call_monitoring_resolution_{DateTime.Now:yyyyMMdd_HHmm}.csv";

            var sfd = new SaveFileDialog
            {
                Title = "Export Call Monitoring Report",
                Filter = "CSV files (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = fileName
            };
            if (sfd.ShowDialog(Window.GetWindow(this)) != true)
            {
                return;
            }

            try
            {
                File.WriteAllText(sfd.FileName, BuildCsvContent(), Encoding.UTF8);
                _statusText.Text = "CSV exported successfully.";
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, ex.Message, "Export CSV Failed");
            }
        }

        private void ExportPdf()
        {
            if (!HasExportableRows())
            {
                WpfItcmDialogService.ShowInfo(this, "No rows to export.", "Call Monitoring Reports");
                return;
            }

            var defaultName = _tabControl.SelectedIndex == 1
                ? $"call_monitoring_sla_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
                : _tabControl.SelectedIndex == 2
                    ? $"call_monitoring_solved_summary_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
                    : $"call_monitoring_resolution_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

            var sfd = new SaveFileDialog
            {
                Title = "Export Call Monitoring Report (PDF)",
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                FileName = defaultName
            };
            if (sfd.ShowDialog(Window.GetWindow(this)) != true)
            {
                return;
            }

            try
            {
                    if (_tabControl.SelectedIndex == 1)
                    {
                        var selected = GetSelectedSlaRowsForExport();
                        var cols = new List<TabularPdfGenerator.Col>
                        {
                            new TabularPdfGenerator.Col("Ticket", 80),
                            new TabularPdfGenerator.Col("Location", 150, wrap: true, maxLines: 2),
                            new TabularPdfGenerator.Col("Priority", 60),
                            new TabularPdfGenerator.Col("Responsible", 110, wrap: true, maxLines: 2),
                            new TabularPdfGenerator.Col("Completed By", 110, wrap: true, maxLines: 2),
                            new TabularPdfGenerator.Col("Final Status", 90),
                            new TabularPdfGenerator.Col("Created", 95),
                            new TabularPdfGenerator.Col("Completed", 95),
                            new TabularPdfGenerator.Col("Target (H)", 65, rightAlign: true),
                            new TabularPdfGenerator.Col("Resolution (H)", 80, rightAlign: true),
                            new TabularPdfGenerator.Col("SLA", 55),
                            new TabularPdfGenerator.Col("Breached (H)", 80, rightAlign: true)
                        };

                        var rows = selected.Select(r => new[]
                        {
                            r.TicketCode,
                            r.Location,
                            r.Priority,
                            r.ResponsiblePerson,
                            r.CompletedByName,
                            r.CompletedStatus,
                            FormatLocalDate(r.CreatedAtUtc),
                            FormatLocalDate(r.CompletedAtUtc),
                            r.TargetHours.ToString(),
                            r.ResolutionHours.ToString("0.0"),
                            r.SlaResult,
                            r.BreachedByHours.ToString("0.0")
                        }).ToList();

                        TabularPdfGenerator.GenerateTablePdf("SLA Compliance Report", "Yakult IT Call Monitoring", cols, rows, sfd.FileName);
                    }
                    else if (_tabControl.SelectedIndex == 2)
                    {
                        var selected = GetSelectedSolvedRowsForExport();
                        var cols = new List<TabularPdfGenerator.Col>
                        {
                            new TabularPdfGenerator.Col("Ticket", 70),
                            new TabularPdfGenerator.Col("Received", 95),
                            new TabularPdfGenerator.Col("Solved", 95),
                            new TabularPdfGenerator.Col("Company", 80, wrap: true, maxLines: 2),
                            new TabularPdfGenerator.Col("Caller", 95, wrap: true, maxLines: 2),
                            new TabularPdfGenerator.Col("Location", 140, wrap: true, maxLines: 2),
                            new TabularPdfGenerator.Col("Priority", 60),
                            new TabularPdfGenerator.Col("Responsible", 110, wrap: true, maxLines: 2),
                            new TabularPdfGenerator.Col("Status", 75),
                            new TabularPdfGenerator.Col("Problem", 170, wrap: true, maxLines: 3),
                            new TabularPdfGenerator.Col("Solution", 170, wrap: true, maxLines: 3),
                            new TabularPdfGenerator.Col("Hours", 55, rightAlign: true)
                        };

                        var rows = selected.Select(r => new[]
                        {
                            r.TicketCode,
                            FormatLocalDate(r.CreatedAtUtc),
                            FormatLocalDate(r.SolvedAtUtc),
                            r.Company,
                            r.CallerName,
                            r.Location,
                            r.Priority,
                            r.ResponsiblePerson,
                            r.FinalStatus,
                            r.Problem,
                            r.Solution,
                            r.ResolutionHours.ToString("0.0")
                        }).ToList();

                        TabularPdfGenerator.GenerateTablePdf("Solved Summary Report", "Yakult IT Call Monitoring", cols, rows, sfd.FileName);
                    }
                    else
                    {
                        var selected = GetSelectedResolutionRowsForExport();
                        var cols = new List<TabularPdfGenerator.Col>
                        {
                            new TabularPdfGenerator.Col("Ticket", 70),
                            new TabularPdfGenerator.Col("Status", 75),
                            new TabularPdfGenerator.Col("Priority", 60),
                            new TabularPdfGenerator.Col("Type", 90),
                            new TabularPdfGenerator.Col("Location", 140, wrap: true, maxLines: 2),
                            new TabularPdfGenerator.Col("Responsible", 110, wrap: true, maxLines: 2),
                            new TabularPdfGenerator.Col("Old Item", 160, wrap: true, maxLines: 2),
                            new TabularPdfGenerator.Col("New Item", 160, wrap: true, maxLines: 2),
                            new TabularPdfGenerator.Col("Qty", 45, rightAlign: true),
                            new TabularPdfGenerator.Col("Marked By", 95, wrap: true, maxLines: 2),
                            new TabularPdfGenerator.Col("Marked At", 95),
                            new TabularPdfGenerator.Col("Remarks", 180, wrap: true, maxLines: 3)
                        };

                        var rows = selected.Select(r => new[]
                        {
                            r.TicketCode,
                            r.TicketStatus,
                            r.Priority,
                            r.ResolutionType,
                            r.Location,
                            r.ResponsiblePerson,
                            r.ReplacementOldItem,
                            r.ReplacementNewItem,
                            r.ReplacementQty?.ToString() ?? string.Empty,
                            r.MarkedByName,
                            FormatLocalDate(r.ResolutionMarkedAt),
                            r.Remarks
                        }).ToList();

                        TabularPdfGenerator.GenerateTablePdf("Resolution Report", "Yakult IT Call Monitoring", cols, rows, sfd.FileName);
                    }

                _statusText.Text = "PDF generated successfully.";
                TabularPdfGenerator.TryOpen(sfd.FileName);
            }
            catch (Exception ex)
            {
                WpfItcmDialogService.ShowError(this, ex.Message, "Export PDF Failed");
            }
        }

        private string BuildCsvContent()
        {
            var builder = new StringBuilder();
            if (_tabControl.SelectedIndex == 1)
            {
                builder.AppendLine("Ticket,Location,Priority,Responsible,Completed By,Final Status,Created,Completed,Target Hours,Resolution Hours,SLA,Breached By Hours");
                foreach (var row in _slaRowsFiltered)
                {
                    builder.AppendLine(string.Join(",",
                        Csv(row.TicketCode),
                        Csv(row.Location),
                        Csv(row.Priority),
                        Csv(row.ResponsiblePerson),
                        Csv(row.CompletedByName),
                        Csv(row.CompletedStatus),
                        Csv(FormatLocalDate(row.CreatedAtUtc)),
                        Csv(FormatLocalDate(row.CompletedAtUtc)),
                        Csv(row.TargetHours.ToString()),
                        Csv(row.ResolutionHours.ToString("0.0")),
                        Csv(row.SlaResult),
                        Csv(row.BreachedByHours.ToString("0.0"))));
                }
            }
            else if (_tabControl.SelectedIndex == 2)
            {
                builder.AppendLine("Ticket,Received,Solved,Company,Caller,Location,Priority,Responsible,Status,Problem,Solution,Hours");
                foreach (var row in _solvedRowsFiltered)
                {
                    builder.AppendLine(string.Join(",",
                        Csv(row.TicketCode),
                        Csv(FormatLocalDate(row.CreatedAtUtc)),
                        Csv(FormatLocalDate(row.SolvedAtUtc)),
                        Csv(row.Company),
                        Csv(row.CallerName),
                        Csv(row.Location),
                        Csv(row.Priority),
                        Csv(row.ResponsiblePerson),
                        Csv(row.FinalStatus),
                        Csv(row.Problem),
                        Csv(row.Solution),
                        Csv(row.ResolutionHours.ToString("0.0"))));
                }
            }
            else
            {
                builder.AppendLine("Ticket,Status,Priority,Type,Location,Responsible,Old Item,New Item,Qty,Marked By,Marked At,Remarks");
                foreach (var row in _resolutionRowsFiltered)
                {
                    builder.AppendLine(string.Join(",",
                        Csv(row.TicketCode),
                        Csv(row.TicketStatus),
                        Csv(row.Priority),
                        Csv(row.ResolutionType),
                        Csv(row.Location),
                        Csv(row.ResponsiblePerson),
                        Csv(row.ReplacementOldItem),
                        Csv(row.ReplacementNewItem),
                        Csv(row.ReplacementQty?.ToString() ?? string.Empty),
                        Csv(row.MarkedByName),
                        Csv(FormatLocalDate(row.ResolutionMarkedAt)),
                        Csv(row.Remarks)));
                }
            }

            return builder.ToString();
        }

        private void OpenTicket(int ticketId, string ticketCode)
        {
            if (_navigator != null && ticketId > 0)
            {
                _navigator.OpenTicket(ticketId);
                return;
            }

            if (string.IsNullOrWhiteSpace(ticketCode))
            {
                return;
            }

            try
            {
                Clipboard.SetText(ticketCode);
                _statusText.Text = $"Copied ticket: {ticketCode}";
            }
            catch
            {
            }
        }

        private void OpenFieldWorkDetail(CallFieldVisitReportRow row)
        {
            if (row == null || _repository == null) return;
            try
            {
                var owner = Window.GetWindow(this);
                var dlg = new WpfFieldWorkReportDetailDialog(_repository, _navigator, row)
                {
                    Owner = owner
                };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                _statusText.Text = "Failed to open visit detail.";
                WpfItcmDialogService.ShowError(this, ex.Message, "Field Work Detail");
            }
        }

        private static bool TryGetTicketCellItem(DataGrid grid, object source, out object item)
        {
            item = null;
            var element = source as DependencyObject;
            DataGridCell cell = null;
            DataGridRow row = null;

            while (element != null)
            {
                if (cell == null) cell = element as DataGridCell;
                if (row == null) row = element as DataGridRow;
                if (cell != null && row != null) break;
                element = VisualTreeHelper.GetParent(element);
            }

            // Column 0 is the export checkbox; column 1 is the blue ticket code.
            if (cell == null || cell.Column == null || cell.Column.DisplayIndex != 1 || row == null)
                return false;

            item = row.Item;
            return item != null;
        }

        private void HandleResolutionCheckEdit(DataGridCellEditEndingEventArgs e)
        {
            if (!(e.Row.Item is ResolutionRowVm row) || !(e.EditingElement is CheckBox checkBox))
            {
                return;
            }

            var isChecked = checkBox.IsChecked == true;
            if (isChecked) _selectedResolutionTicketIds.Add(row.TicketId);
            else _selectedResolutionTicketIds.Remove(row.TicketId);
            UpdateWorkspaceSummary();
        }

        private void HandleSlaCheckEdit(DataGridCellEditEndingEventArgs e)
        {
            if (!(e.Row.Item is SlaRowVm row) || !(e.EditingElement is CheckBox checkBox))
            {
                return;
            }

            var isChecked = checkBox.IsChecked == true;
            if (isChecked) _selectedSlaTicketIds.Add(row.TicketId);
            else _selectedSlaTicketIds.Remove(row.TicketId);
            UpdateWorkspaceSummary();
        }

        private void HandleSolvedCheckEdit(DataGridCellEditEndingEventArgs e)
        {
            if (!(e.Row.Item is SolvedRowVm row) || !(e.EditingElement is CheckBox checkBox))
            {
                return;
            }

            var isChecked = checkBox.IsChecked == true;
            if (isChecked) _selectedSolvedTicketIds.Add(row.TicketId);
            else _selectedSolvedTicketIds.Remove(row.TicketId);
            UpdateWorkspaceSummary();
        }

        private void RebuildDepartmentAndResponsibleLists(IEnumerable<string> departments, IEnumerable<string> responsibles)
        {
            var previousDepartment = GetSelectedComboText(_departmentCombo, "All Departments");
            var previousResponsible = GetSelectedComboText(_responsibleCombo, "All Responsible");

            var departmentList = (departments ?? Enumerable.Empty<string>())
                .Select(v => (v ?? string.Empty).Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var responsibleList = (responsibles ?? Enumerable.Empty<string>())
                .Select(v => (v ?? string.Empty).Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ResetComboItems(_departmentCombo, "All Departments", departmentList);
            ResetComboItems(_responsibleCombo, "All Responsible", responsibleList);
            SelectStringComboValue(_departmentCombo, previousDepartment, 0);
            SelectStringComboValue(_responsibleCombo, previousResponsible, 0);
        }

        private void RebuildCompanyList(IEnumerable<string> companies)
        {
            var previousCompany = GetSelectedComboText(_companyCombo, "All Companies");
            var companyList = (companies ?? Enumerable.Empty<string>())
                .Select(v => (v ?? string.Empty).Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ResetComboItems(_companyCombo, "All Companies", companyList);
            SelectStringComboValue(_companyCombo, previousCompany, 0);
        }

        private void UpdateFilterStateForActiveTab()
        {
            var isResolution = _tabControl.SelectedIndex == 0;
            var isSolved = _tabControl.SelectedIndex == 2;
            var isFieldWork = _tabControl.SelectedIndex == 3;

            _typeCombo.IsEnabled = isResolution;
            _companyCombo.IsEnabled = isSolved;
            _fieldWorkStatusCombo.IsEnabled = isFieldWork;
            _fieldWorkTechnicianCombo.IsEnabled = isFieldWork;
            if (!isSolved)
            {
                SelectStringComboValue(_companyCombo, "All Companies", 0);
            }
            if (!isFieldWork)
            {
                SelectStringComboValue(_fieldWorkStatusCombo, "All", 0);
                SelectStringComboValue(_fieldWorkTechnicianCombo, "All Technicians", 0);
            }
        }

        private void UpdateDepartmentFilterEnabledState()
        {
            var branchOnly = _locationModeCombo.SelectedIndex == 2;
            _departmentCombo.IsEnabled = !branchOnly;
            if (branchOnly)
            {
                SelectStringComboValue(_departmentCombo, "All Departments", 0);
            }
        }

        private int GetCurrentFilteredCount(){ if (_tabControl.SelectedIndex == 1) return _slaRowsFiltered.Count; if (_tabControl.SelectedIndex == 2) return _solvedRowsFiltered.Count; if (_tabControl.SelectedIndex == 3) return _fieldWorkRowsFiltered.Count; return _resolutionRowsFiltered.Count; }

        private int GetCurrentTotalCount(){ if (_tabControl.SelectedIndex == 1) return _slaRowsAll.Count; if (_tabControl.SelectedIndex == 2) return _solvedRowsAll.Count; if (_tabControl.SelectedIndex == 3) return _fieldWorkRowsAll.Count; return _resolutionRowsAll.Count; }

        private int GetCurrentSelectedCount()
        {
            if (_tabControl.SelectedIndex == 1) return _selectedSlaTicketIds.Count;
            if (_tabControl.SelectedIndex == 2) return _selectedSolvedTicketIds.Count;
            return _selectedResolutionTicketIds.Count;
        }

        private int GetCurrentPageIndex(){ if (_tabControl.SelectedIndex == 1) return _slaPageIndex; if (_tabControl.SelectedIndex == 2) return _solvedPageIndex; if (_tabControl.SelectedIndex == 3) return _fieldWorkPageIndex; return _resolutionPageIndex; }

        private string GetActiveTabName()
        {
            if (_tabControl.SelectedIndex == 1) return "SLA Compliance";
            if (_tabControl.SelectedIndex == 2) return "Solved Summary";
            return "Resolution";
        }

        private List<CallResolutionReportRow> GetSelectedResolutionRowsForExport()
        {
            var selected = _resolutionRowsFiltered.Where(r => r != null && _selectedResolutionTicketIds.Contains(r.TicketId)).ToList();
            if (selected.Count > 0)
            {
                return selected;
            }

            if (!WpfItcmDialogService.Confirm(this, "No rows are checked. Export all filtered rows?", "Export PDF", "Export All"))
            {
                return new List<CallResolutionReportRow>();
            }

            return _resolutionRowsFiltered.Where(r => r != null).ToList();
        }

        private List<SlaComplianceDisplayRow> GetSelectedSlaRowsForExport()
        {
            var selected = _slaRowsFiltered.Where(r => r != null && _selectedSlaTicketIds.Contains(r.TicketId)).ToList();
            if (selected.Count > 0)
            {
                return selected;
            }

            if (!WpfItcmDialogService.Confirm(this, "No rows are checked. Export all filtered rows?", "Export PDF", "Export All"))
            {
                return new List<SlaComplianceDisplayRow>();
            }

            return _slaRowsFiltered.Where(r => r != null).ToList();
        }

        private List<CallSolvedSummaryReportRow> GetSelectedSolvedRowsForExport()
        {
            var selected = _solvedRowsFiltered.Where(r => r != null && _selectedSolvedTicketIds.Contains(r.TicketId)).ToList();
            if (selected.Count > 0)
            {
                return selected;
            }

            if (!WpfItcmDialogService.Confirm(this, "No rows are checked. Export all filtered rows?", "Export PDF", "Export All"))
            {
                return new List<CallSolvedSummaryReportRow>();
            }

            return _solvedRowsFiltered.Where(r => r != null).ToList();
        }

        private static SlaComplianceDisplayRow BuildSlaDisplayRow(CallSlaComplianceReportRow row)
        {
            if (row == null || row.TicketId <= 0)
            {
                return null;
            }

            var createdUtc = row.CreatedAtUtc.Kind == DateTimeKind.Utc ? row.CreatedAtUtc : DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc);
            var completedUtc = row.CompletedAtUtc.Kind == DateTimeKind.Utc ? row.CompletedAtUtc : DateTime.SpecifyKind(row.CompletedAtUtc, DateTimeKind.Utc);
            var targetHours = GetResolutionSlaTargetHours(row.Priority);
            var dueUtc = createdUtc.AddHours(targetHours);
            var resolutionHours = Math.Max(0, (completedUtc - createdUtc).TotalHours);
            var breachedByHours = Math.Max(0, (completedUtc - dueUtc).TotalHours);

            return new SlaComplianceDisplayRow
            {
                TicketId = row.TicketId,
                TicketCode = row.TicketCode ?? string.Empty,
                Department = row.Department ?? string.Empty,
                Location = row.Location ?? string.Empty,
                ResponsiblePerson = row.AssignedToName ?? string.Empty,
                Priority = row.Priority ?? string.Empty,
                CompletedByName = row.CompletedByName ?? string.Empty,
                CompletedStatus = row.CompletedStatus ?? string.Empty,
                CreatedAtUtc = createdUtc,
                CompletedAtUtc = completedUtc,
                TargetHours = targetHours,
                ResolutionHours = resolutionHours,
                SlaResult = breachedByHours > 0 ? "Breached" : "Met",
                BreachedByHours = breachedByHours
            };
        }

        private static int GetResolutionSlaTargetHours(string priority)
        {
            var value = (priority ?? string.Empty).Trim();
            if (value.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return 24;
            if (value.Equals("High", StringComparison.OrdinalIgnoreCase)) return 48;
            if (value.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return 72;
            if (value.Equals("Low", StringComparison.OrdinalIgnoreCase)) return 120;
            return 72;
        }

        private static int NormalizePageIndex(ref int pageIndex, int itemCount)
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling(itemCount / (double)PageSize));
            if (pageIndex < 1) pageIndex = 1;
            if (pageIndex > totalPages) pageIndex = totalPages;
            return totalPages;
        }

        private static string BuildRowCountLabel(int rowCount)
        {
            return rowCount >= MaxReportRows
                ? $"{rowCount} row(s) found (showing the first {MaxReportRows} loaded rows)"
                : $"{rowCount} row(s) found";
        }

        private static string NormalizeAllText(string value, string allLabel)
        {
            if (string.Equals((value ?? string.Empty).Trim(), allLabel, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return (value ?? string.Empty).Trim();
        }

        private static string NormalizePriorityText(string value)
        {
            var trimmed = (value ?? string.Empty).Trim();
            return trimmed.StartsWith("All", StringComparison.OrdinalIgnoreCase) ? null : trimmed;
        }

        private static string GetSelectedComboText(ComboBox comboBox, string fallback)
        {
            return comboBox?.SelectedItem as string ?? comboBox?.Text ?? fallback;
        }

        private static void SelectStringComboValue(ComboBox comboBox, string value, int defaultIndex)
        {
            if (comboBox == null)
            {
                return;
            }

            var target = (value ?? string.Empty).Trim();
            for (var i = 0; i < comboBox.Items.Count; i++)
            {
                var item = comboBox.Items[i] as string;
                if (string.Equals((item ?? string.Empty).Trim(), target, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = Math.Max(0, Math.Min(defaultIndex, comboBox.Items.Count - 1));
            }
        }

        private static void ResetComboItems(ComboBox comboBox, string defaultItem, IEnumerable<string> values = null)
        {
            comboBox.Items.Clear();
            comboBox.Items.Add(defaultItem);
            if (values != null)
            {
                foreach (var value in values)
                {
                    comboBox.Items.Add(value);
                }
            }

            comboBox.SelectedIndex = 0;
        }

        private static string FormatLocalDate(DateTime? value)
        {
            if (!value.HasValue)
            {
                return string.Empty;
            }

            return FormatLocalDate(value.Value);
        }

        private static string FormatLocalDate(DateTime value)
        {
            var local = value.Kind == DateTimeKind.Utc
                ? value.ToLocalTime()
                : value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime()
                    : value;

            return local.ToString("g");
        }

        private static string Csv(string value)
        {
            var safe = (value ?? string.Empty).Replace("\"", "\"\"");
            return "\"" + safe + "\"";
        }

        private static Border BuildHero()
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
                Text = "Call Monitoring Reports",
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            textStack.Children.Add(new TextBlock
            {
                Text = "Modernized WPF analytics for resolution actions, SLA compliance, and solved-ticket summaries.",
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                TextWrapping = TextWrapping.Wrap
            });
            layout.Children.Add(textStack);

            var chips = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            chips.Children.Add(CreateHeroChip("Repository-backed", BrushFromRgb(14, 165, 233), Brushes.White));
            chips.Children.Add(CreateHeroChip("CSV + PDF export", BrushFromRgb(16, 185, 129), Brushes.White));
            chips.Children.Add(CreateHeroChip("Ticket drill-down", BrushFromRgb(245, 158, 11), Brushes.White));
            Grid.SetColumn(chips, 1);
            layout.Children.Add(chips);

            return card;
        }

        private static Border CreateHeroChip(string text, Brush background, Brush foreground)
        {
            return new Border
            {
                Margin = new Thickness(8, 0, 0, 8),
                Padding = new Thickness(12, 7, 12, 7),
                CornerRadius = new CornerRadius(999),
                Background = background,
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = foreground
                }
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
                    Color = Color.FromArgb(28, 15, 23, 42),
                    ShadowDepth = 0,
                    Opacity = 0.2
                }
            };
        }

        private static Border CreateMetricCard(string title, string subtitle, TextBlock valueBlock, Color accentColor)
        {
            var card = CreateGlassCard();
            card.Padding = new Thickness(20, 18, 20, 18);

            var stack = new StackPanel();
            card.Child = stack;

            stack.Children.Add(new Border
            {
                Width = 40,
                Height = 5,
                CornerRadius = new CornerRadius(999),
                Background = new SolidColorBrush(accentColor)
            });
            stack.Children.Add(new TextBlock
            {
                Text = title,
                Margin = new Thickness(0, 16, 0, 0),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105)
            });
            stack.Children.Add(valueBlock);
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 11.5,
                Foreground = BrushFromRgb(100, 116, 139),
                TextWrapping = TextWrapping.Wrap
            });

            return card;
        }

        private static TextBlock CreateMetricValue()
        {
            return new TextBlock
            {
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(15, 23, 42)
            };
        }

        private static void AddMetricCard(Grid grid, int column, string title, string subtitle, TextBlock valueBlock, Color accentColor)
        {
            var card = CreateMetricCard(title, subtitle, valueBlock, accentColor);
            if (column > 0)
            {
                card.Margin = new Thickness(18, 0, 0, 0);
            }

            Grid.SetColumn(card, column);
            grid.Children.Add(card);
        }

        private static StackPanel CreateSectionHeader(string title, string subtitle)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42)
            });
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                Margin = new Thickness(0, 6, 0, 0),
                FontSize = 12.5,
                Foreground = BrushFromRgb(100, 116, 139),
                TextWrapping = TextWrapping.Wrap
            });
            return stack;
        }

        private static Grid CreateFormGrid(int columns, int rows)
        {
            var grid = new Grid();
            for (var i = 0; i < columns; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition());
            }
            for (var i = 0; i < rows; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            return grid;
        }

        private static void AddFormField(Grid grid, int row, int column, string label, FrameworkElement control)
        {
            var stack = new StackPanel
            {
                Margin = new Thickness(column > 0 ? 18 : 0, row > 0 ? 14 : 0, 0, 0)
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

        private static Button CreatePrimaryButton(string text, Brush background)
        {
            return new Button
            {
                Content = text,
                Margin = new Thickness(0, 0, 10, 10),
                Padding = new Thickness(16, 10, 16, 10),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = background,
                BorderThickness = new Thickness(0)
            };
        }

        private static ComboBox CreateTextComboBox()
        {
            return new ComboBox
            {
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 12
            };
        }

        private static TextBox CreateTextBox(string placeholder)
        {
            return new TextBox
            {
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 12,
                Tag = placeholder
            };
        }

        private static DatePicker CreateDatePicker()
        {
            return new DatePicker
            {
                FontSize = 12
            };
        }

        private static FrameworkElement MakeCompactField(string label, FrameworkElement control)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 18, 8), VerticalAlignment = VerticalAlignment.Bottom };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                Margin = new Thickness(0, 0, 0, 5),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105)
            });
            stack.Children.Add(control);
            return stack;
        }

        private static TextBlock CreateInlineSummaryText()
        {
            return new TextBlock
            {
                FontSize = 12,
                Foreground = BrushFromRgb(100, 116, 139),
                TextWrapping = TextWrapping.Wrap
            };
        }

        private static TextBlock CreatePagerText()
        {
            return new TextBlock
            {
                Margin = new Thickness(12, 0, 12, 0),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static Button CreatePagerButton(string text)
        {
            var button = new Button
            {
                Content = text,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(14, 6, 14, 6),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(51, 65, 85),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            return button;
        }

        private static TabItem CreateReportTab(
            string header, string subtitle,
            TextBlock summaryText, DataGrid grid,
            TextBlock pagerText, Button prevButton, Button nextButton,
            Button exportCsvButton, Button exportPdfButton)
        {
            var tab = new TabItem { Header = header };
            var layout = new Grid { Margin = new Thickness(0, 18, 0, 0) };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Top: subtitle + summary badge on the LEFT; Export buttons on the RIGHT
            var topPanel = new Grid();
            topPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            topPanel.Children.Add(new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = subtitle,
                        FontSize = 12.5,
                        Foreground = BrushFromRgb(100, 116, 139),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new Border
                    {
                        Margin = new Thickness(0, 10, 0, 0),
                        Padding = new Thickness(12, 10, 12, 10),
                        CornerRadius = new CornerRadius(14),
                        Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                        BorderThickness = new Thickness(1),
                        Child = summaryText
                    }
                }
            });

            // Contextual export buttons — placed in this tab so scope is clear
            var exportRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(12, 0, 0, 0)
            };
            // Clone-style wrapper buttons that delegate to the shared export actions
            var csvBtn = new Button
            {
                Content = "Export CSV",
                Padding = new Thickness(14, 9, 14, 9),
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = BrushFromRgb(79, 70, 229),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            csvBtn.Click += (_, __) => exportCsvButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var pdfBtn = new Button
            {
                Content = "Export PDF",
                Padding = new Thickness(14, 9, 14, 9),
                Margin = new Thickness(0, 0, 0, 0),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = BrushFromRgb(5, 150, 105),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            pdfBtn.Click += (_, __) => exportPdfButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            exportRow.Children.Add(csvBtn);
            exportRow.Children.Add(pdfBtn);
            Grid.SetColumn(exportRow, 1);
            topPanel.Children.Add(exportRow);

            Grid.SetRow(topPanel, 0);
            layout.Children.Add(topPanel);

            Grid.SetRow(grid, 1);
            layout.Children.Add(grid);

            var pagerPanel = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
            var pagerButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            pagerButtons.Children.Add(prevButton);
            pagerButtons.Children.Add(pagerText);
            pagerButtons.Children.Add(nextButton);
            DockPanel.SetDock(pagerButtons, Dock.Right);
            pagerPanel.Children.Add(pagerButtons);
            Grid.SetRow(pagerPanel, 2);
            layout.Children.Add(pagerPanel);

            tab.Content = layout;
            return tab;
        }

        private static Style BuildTabItemStyle()
        {
            var xaml = @"
                <Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" TargetType=""TabItem"">
                    <Setter Property=""Foreground"" Value=""#64748b"" />
                    <Setter Property=""FontSize"" Value=""14"" />
                    <Setter Property=""Template"">
                        <Setter.Value>
                            <ControlTemplate TargetType=""TabItem"">
                                <Border Name=""Border"" Padding=""16,8"" Margin=""0,0,4,0"" CornerRadius=""8,8,0,0"" Background=""#e2e8f0"">
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
                </Style>";

            var parserContext = new ParserContext();
            parserContext.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            parserContext.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
            return (Style)XamlReader.Parse(xaml, parserContext);
        }

        private static DataGrid CreateReportGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = false,
                IsReadOnly = false,
                SelectionMode = DataGridSelectionMode.Single,
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

        private static void SetupResolutionColumns(DataGrid grid)
        {
            grid.Columns.Add(CreateCheckColumn("IsSelected"));
            grid.Columns.Add(CreateReportTemplateColumn("Ticket", 90, "<DataTemplate><TextBlock Text='{Binding TicketCode}' FontWeight='Bold' Foreground='#0284c7' Cursor='Hand' ToolTip='Open ticket' VerticalAlignment='Center'/></DataTemplate>"));
            grid.Columns.Add(CreateReportTemplateColumn("Status", 120, "<DataTemplate><TextBlock Text='{Binding TicketStatus}' FontWeight='Bold' VerticalAlignment='Center'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Foreground' Value='#374151'/><Style.Triggers><DataTrigger Binding='{Binding TicketStatus}' Value='Solved'><Setter Property='Foreground' Value='#10b981'/></DataTrigger><DataTrigger Binding='{Binding TicketStatus}' Value='Closed'><Setter Property='Foreground' Value='#10b981'/></DataTrigger><DataTrigger Binding='{Binding TicketStatus}' Value='Resolved (Temporary)'><Setter Property='Foreground' Value='#14b8a6'/></DataTrigger><DataTrigger Binding='{Binding TicketStatus}' Value='Escalated'><Setter Property='Foreground' Value='#ef4444'/></DataTrigger><DataTrigger Binding='{Binding TicketStatus}' Value='In Progress'><Setter Property='Foreground' Value='#7c3aed'/></DataTrigger><DataTrigger Binding='{Binding TicketStatus}' Value='Pending'><Setter Property='Foreground' Value='#2563eb'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></DataTemplate>"));
            grid.Columns.Add(CreateReportTemplateColumn("Priority", 85, "<DataTemplate><TextBlock Text='{Binding Priority}' FontWeight='Bold' VerticalAlignment='Center'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Foreground' Value='#374151'/><Style.Triggers><DataTrigger Binding='{Binding Priority}' Value='Critical'><Setter Property='Foreground' Value='#ef4444'/></DataTrigger><DataTrigger Binding='{Binding Priority}' Value='High'><Setter Property='Foreground' Value='#ef4444'/></DataTrigger><DataTrigger Binding='{Binding Priority}' Value='Medium'><Setter Property='Foreground' Value='#f59e0b'/></DataTrigger><DataTrigger Binding='{Binding Priority}' Value='Low'><Setter Property='Foreground' Value='#6b7280'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></DataTemplate>"));
            grid.Columns.Add(CreateTextColumn("ResolutionType", "Type", 120));
            grid.Columns.Add(CreateTextColumn("Location", "Location", 180));
            grid.Columns.Add(CreateTextColumn("ResponsiblePerson", "Responsible", 150));
            grid.Columns.Add(CreateTextColumn("ReplacementOldItem", "Old Item", 180));
            grid.Columns.Add(CreateTextColumn("ReplacementNewItem", "New Item", 180));
            grid.Columns.Add(CreateTextColumn("ReplacementQtyDisplay", "Qty", 60));
            grid.Columns.Add(CreateTextColumn("MarkedByName", "Marked By", 120));
            grid.Columns.Add(CreateTextColumn("ResolutionMarkedAtDisplay", "Marked At", 130));
            grid.Columns.Add(CreateTextColumn("Remarks", "Remarks", 220));
        }

        private static void SetupFieldWorkColumns(DataGrid grid)
        {
            grid.Columns.Add(CreateCheckColumn("IsSelected"));
            grid.Columns.Add(CreateReportTemplateColumn("Ticket", 95, "<DataTemplate><TextBlock Text='{Binding TicketCode}' FontWeight='Bold' Foreground='#0284c7' Cursor='Hand' ToolTip='Open ticket' VerticalAlignment='Center'/></DataTemplate>"));
            grid.Columns.Add(CreateTextColumn("TechnicianName", "Technician", 140));
            grid.Columns.Add(CreateTextColumn("Location", "Location", 160));
            grid.Columns.Add(CreateReportTemplateColumn("Status", 115, "<DataTemplate><Border CornerRadius='10' Padding='8,3' HorizontalAlignment='Left'><Border.Style><Style TargetType='Border'><Setter Property='Background' Value='#E2E8F0'/><Style.Triggers><DataTrigger Binding='{Binding Status}' Value='Completed'><Setter Property='Background' Value='#DCFCE7'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Scheduled'><Setter Property='Background' Value='#DBEAFE'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Cancelled'><Setter Property='Background' Value='#FEE2E2'/></DataTrigger></Style.Triggers></Style></Border.Style><TextBlock Text='{Binding Status}' FontWeight='Bold' FontSize='11' VerticalAlignment='Center'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Foreground' Value='#475569'/><Style.Triggers><DataTrigger Binding='{Binding Status}' Value='Completed'><Setter Property='Foreground' Value='#166534'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Scheduled'><Setter Property='Foreground' Value='#1D4ED8'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Cancelled'><Setter Property='Foreground' Value='#B91C1C'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></Border></DataTemplate>"));
            grid.Columns.Add(CreateTextColumn("ScheduledAtDisplay", "Scheduled Date", 140));
            grid.Columns.Add(CreateTextColumn("CompletedAtDisplay", "Completed Date", 140));
            grid.Columns.Add(CreateReportTemplateColumn("Signature", 90, "<DataTemplate><TextBlock Text='{Binding HasSignatureDisplay}' FontWeight='SemiBold' VerticalAlignment='Center'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Foreground' Value='#94A3B8'/><Style.Triggers><DataTrigger Binding='{Binding HasSignatureDisplay}' Value='✓'><Setter Property='Foreground' Value='#16A34A'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></DataTemplate>"));
            grid.Columns.Add(CreateTextColumn("AttachmentCountDisplay", "Photos", 70));
            grid.Columns.Add(CreateTextColumn("Issue", "Issue", 200));
            grid.Columns.Add(CreateTextColumn("Notes", "Notes", 220));
        }

        private static void SetupSlaColumns(DataGrid grid)
        {
            grid.Columns.Add(CreateCheckColumn("IsSelected"));
            grid.Columns.Add(CreateReportTemplateColumn("Ticket", 90, "<DataTemplate><TextBlock Text='{Binding TicketCode}' FontWeight='Bold' Foreground='#0284c7' Cursor='Hand' ToolTip='Open ticket' VerticalAlignment='Center'/></DataTemplate>"));
            grid.Columns.Add(CreateTextColumn("Location", "Location", 180));
            grid.Columns.Add(CreateReportTemplateColumn("Priority", 85, "<DataTemplate><TextBlock Text='{Binding Priority}' FontWeight='Bold' VerticalAlignment='Center'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Foreground' Value='#374151'/><Style.Triggers><DataTrigger Binding='{Binding Priority}' Value='Critical'><Setter Property='Foreground' Value='#ef4444'/></DataTrigger><DataTrigger Binding='{Binding Priority}' Value='High'><Setter Property='Foreground' Value='#ef4444'/></DataTrigger><DataTrigger Binding='{Binding Priority}' Value='Medium'><Setter Property='Foreground' Value='#f59e0b'/></DataTrigger><DataTrigger Binding='{Binding Priority}' Value='Low'><Setter Property='Foreground' Value='#6b7280'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></DataTemplate>"));
            grid.Columns.Add(CreateTextColumn("ResponsiblePerson", "Responsible", 150));
            grid.Columns.Add(CreateTextColumn("CompletedByName", "Completed By", 140));
            grid.Columns.Add(CreateReportTemplateColumn("Final Status", 130, "<DataTemplate><TextBlock Text='{Binding CompletedStatus}' FontWeight='Bold' VerticalAlignment='Center'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Foreground' Value='#374151'/><Style.Triggers><DataTrigger Binding='{Binding CompletedStatus}' Value='Solved'><Setter Property='Foreground' Value='#10b981'/></DataTrigger><DataTrigger Binding='{Binding CompletedStatus}' Value='Closed'><Setter Property='Foreground' Value='#10b981'/></DataTrigger><DataTrigger Binding='{Binding CompletedStatus}' Value='Resolved (Temporary)'><Setter Property='Foreground' Value='#14b8a6'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></DataTemplate>"));
            grid.Columns.Add(CreateTextColumn("CreatedAtDisplay", "Created", 130));
            grid.Columns.Add(CreateTextColumn("CompletedAtDisplay", "Completed", 130));
            grid.Columns.Add(CreateTextColumn("TargetHoursDisplay", "Target (H)", 90));
            grid.Columns.Add(CreateTextColumn("ResolutionHoursDisplay", "Resolution (H)", 110));
            grid.Columns.Add(CreateReportTemplateColumn("SLA", 80, "<DataTemplate><TextBlock Text='{Binding SlaResult}' FontWeight='Bold' VerticalAlignment='Center'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Foreground' Value='#374151'/><Style.Triggers><DataTrigger Binding='{Binding SlaResult}' Value='Met'><Setter Property='Foreground' Value='#10b981'/></DataTrigger><DataTrigger Binding='{Binding SlaResult}' Value='Breached'><Setter Property='Foreground' Value='#ef4444'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></DataTemplate>"));
            grid.Columns.Add(CreateTextColumn("BreachedByHoursDisplay", "Breached (H)", 110));

            // Tint breached rows light red
            var breachedStyle = new Style(typeof(DataGridRow), grid.RowStyle);
            var breachedTrigger = new DataTrigger
            {
                Binding = new Binding("SlaResult"),
                Value = "Breached"
            };
            breachedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(254, 242, 242))));
            breachedStyle.Triggers.Add(breachedTrigger);
            grid.RowStyle = breachedStyle;
        }

        private static void SetupSolvedColumns(DataGrid grid)
        {
            grid.Columns.Add(CreateCheckColumn("IsSelected"));
            grid.Columns.Add(CreateReportTemplateColumn("Ticket", 90, "<DataTemplate><TextBlock Text='{Binding TicketCode}' FontWeight='Bold' Foreground='#0284c7' Cursor='Hand' ToolTip='Open ticket' VerticalAlignment='Center'/></DataTemplate>"));
            grid.Columns.Add(CreateTextColumn("CreatedAtDisplay", "Received", 130));
            grid.Columns.Add(CreateTextColumn("SolvedAtDisplay", "Solved", 130));
            grid.Columns.Add(CreateTextColumn("Company", "Company", 120));
            grid.Columns.Add(CreateTextColumn("CallerName", "Caller", 120));
            grid.Columns.Add(CreateTextColumn("Location", "Location", 170));
            grid.Columns.Add(CreateReportTemplateColumn("Priority", 85, "<DataTemplate><TextBlock Text='{Binding Priority}' FontWeight='Bold' VerticalAlignment='Center'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Foreground' Value='#374151'/><Style.Triggers><DataTrigger Binding='{Binding Priority}' Value='Critical'><Setter Property='Foreground' Value='#ef4444'/></DataTrigger><DataTrigger Binding='{Binding Priority}' Value='High'><Setter Property='Foreground' Value='#ef4444'/></DataTrigger><DataTrigger Binding='{Binding Priority}' Value='Medium'><Setter Property='Foreground' Value='#f59e0b'/></DataTrigger><DataTrigger Binding='{Binding Priority}' Value='Low'><Setter Property='Foreground' Value='#6b7280'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></DataTemplate>"));
            grid.Columns.Add(CreateTextColumn("ResponsiblePerson", "Responsible", 150));
            grid.Columns.Add(CreateReportTemplateColumn("Status", 120, "<DataTemplate><TextBlock Text='{Binding FinalStatus}' FontWeight='Bold' VerticalAlignment='Center'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Foreground' Value='#374151'/><Style.Triggers><DataTrigger Binding='{Binding FinalStatus}' Value='Solved'><Setter Property='Foreground' Value='#10b981'/></DataTrigger><DataTrigger Binding='{Binding FinalStatus}' Value='Closed'><Setter Property='Foreground' Value='#10b981'/></DataTrigger><DataTrigger Binding='{Binding FinalStatus}' Value='Resolved (Temporary)'><Setter Property='Foreground' Value='#14b8a6'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></DataTemplate>"));
            grid.Columns.Add(CreateTextColumn("Problem", "Problem", 220));
            grid.Columns.Add(CreateTextColumn("Solution", "Solution", 220));
            grid.Columns.Add(CreateTextColumn("ResolutionHoursDisplay", "Hours", 80));
        }

        private static DataGridCheckBoxColumn CreateCheckColumn(string bindingPath)
        {
            return new DataGridCheckBoxColumn
            {
                Header = "✓",
                Binding = new Binding(bindingPath) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = 45
            };
        }

        private static DataGridTextColumn CreateTextColumn(string bindingPath, string header, double width)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(bindingPath) { Mode = BindingMode.OneWay },
                Width = new DataGridLength(width),
                IsReadOnly = true
            };
        }

        private static DataGridTemplateColumn CreateReportTemplateColumn(string header, double width, string xaml)
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

        private static SolidColorBrush BrushFromRgb(byte red, byte green, byte blue)
        {
            return new SolidColorBrush(Color.FromRgb(red, green, blue));
        }

        private sealed class SlaComplianceDisplayRow
        {
            public int TicketId { get; set; }
            public string TicketCode { get; set; }
            public string Department { get; set; }
            public string Location { get; set; }
            public string ResponsiblePerson { get; set; }
            public string Priority { get; set; }
            public string CompletedByName { get; set; }
            public string CompletedStatus { get; set; }
            public DateTime CreatedAtUtc { get; set; }
            public DateTime CompletedAtUtc { get; set; }
            public int TargetHours { get; set; }
            public double ResolutionHours { get; set; }
            public string SlaResult { get; set; }
            public double BreachedByHours { get; set; }
        }

        private sealed class ResolutionRowVm
        {
            public ResolutionRowVm(CallResolutionReportRow source, bool isSelected)
            {
                Source = source;
                IsSelected = isSelected;
            }

            public bool IsSelected { get; set; }
            public CallResolutionReportRow Source { get; }
            public int TicketId => Source?.TicketId ?? 0;
            public string TicketCode => Source?.TicketCode ?? string.Empty;
            public string TicketStatus => Source?.TicketStatus ?? string.Empty;
            public string Priority => Source?.Priority ?? string.Empty;
            public string ResolutionType => Source?.ResolutionType ?? string.Empty;
            public string Location => Source?.Location ?? string.Empty;
            public string ResponsiblePerson => Source?.ResponsiblePerson ?? string.Empty;
            public string ReplacementOldItem => Source?.ReplacementOldItem ?? string.Empty;
            public string ReplacementNewItem => Source?.ReplacementNewItem ?? string.Empty;
            public string ReplacementQtyDisplay => Source?.ReplacementQty?.ToString() ?? string.Empty;
            public string MarkedByName => Source?.MarkedByName ?? string.Empty;
            public string ResolutionMarkedAtDisplay => FormatLocalDate(Source?.ResolutionMarkedAt);
            public string Remarks => Source?.Remarks ?? string.Empty;
        }

        private sealed class SlaRowVm
        {
            public SlaRowVm(SlaComplianceDisplayRow source, bool isSelected)
            {
                Source = source;
                IsSelected = isSelected;
            }

            public bool IsSelected { get; set; }
            public SlaComplianceDisplayRow Source { get; }
            public int TicketId => Source?.TicketId ?? 0;
            public string TicketCode => Source?.TicketCode ?? string.Empty;
            public string Location => Source?.Location ?? string.Empty;
            public string Priority => Source?.Priority ?? string.Empty;
            public string ResponsiblePerson => Source?.ResponsiblePerson ?? string.Empty;
            public string CompletedByName => Source?.CompletedByName ?? string.Empty;
            public string CompletedStatus => Source?.CompletedStatus ?? string.Empty;
            public string CreatedAtDisplay => FormatLocalDate(Source?.CreatedAtUtc ?? DateTime.MinValue);
            public string CompletedAtDisplay => FormatLocalDate(Source?.CompletedAtUtc ?? DateTime.MinValue);
            public string TargetHoursDisplay => Source?.TargetHours.ToString() ?? string.Empty;
            public string ResolutionHoursDisplay => Source?.ResolutionHours.ToString("0.0") ?? string.Empty;
            public string SlaResult => Source?.SlaResult ?? string.Empty;
            public string BreachedByHoursDisplay => Source?.BreachedByHours.ToString("0.0") ?? string.Empty;
        }

        private sealed class SolvedRowVm
        {
            public SolvedRowVm(CallSolvedSummaryReportRow source, bool isSelected)
            {
                Source = source;
                IsSelected = isSelected;
            }

            public bool IsSelected { get; set; }
            public CallSolvedSummaryReportRow Source { get; }
            public int TicketId => Source?.TicketId ?? 0;
            public string TicketCode => Source?.TicketCode ?? string.Empty;
            public string CreatedAtDisplay => FormatLocalDate(Source?.CreatedAtUtc ?? DateTime.MinValue);
            public string SolvedAtDisplay => FormatLocalDate(Source?.SolvedAtUtc ?? DateTime.MinValue);
            public string Company => Source?.Company ?? string.Empty;
            public string CallerName => Source?.CallerName ?? string.Empty;
            public string Location => Source?.Location ?? string.Empty;
            public string Priority => Source?.Priority ?? string.Empty;
            public string ResponsiblePerson => Source?.ResponsiblePerson ?? string.Empty;
            public string FinalStatus => Source?.FinalStatus ?? string.Empty;
            public string Problem => Source?.Problem ?? string.Empty;
            public string Solution => Source?.Solution ?? string.Empty;
            public string ResolutionHoursDisplay => Source?.ResolutionHours.ToString("0.0") ?? string.Empty;
        }

        private sealed class FieldWorkRowVm
        {
            public FieldWorkRowVm(CallFieldVisitReportRow source, bool isSelected)
            {
                Source = source;
                IsSelected = isSelected;
            }

            public bool IsSelected { get; set; }
            public CallFieldVisitReportRow Source { get; }
            public int FieldVisitId => Source?.FieldVisitId ?? 0;
            public int TicketId => Source?.TicketId ?? 0;
            public string TicketCode => Source?.TicketCode ?? string.Empty;
            public string Issue => Source?.Issue ?? string.Empty;
            public string Location => Source?.Location ?? string.Empty;
            public string TechnicianName => Source?.TechnicianName ?? string.Empty;
            public string Status => Source?.Status ?? string.Empty;
            public string ScheduledAtDisplay => FormatLocalDate(Source?.ScheduledAt);
            public string CompletedAtDisplay => FormatLocalDate(Source?.CompletedAt);
            public string HasSignatureDisplay => Source?.HasSignature == true ? "✓" : "—";
            public string AttachmentCountDisplay => Source?.AttachmentCount.ToString() ?? "0";
            public string Notes => Source?.Notes ?? string.Empty;
        }
    }
}









