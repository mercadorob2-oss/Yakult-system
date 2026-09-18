using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.UserActivityInsights.ViewModels
{
    /// <summary>Display wrapper for the User combo box (mirrors the old WinForms UserItem).</summary>
    public class UserItem
    {
        public int UserId { get; }
        public string Name { get; }
        public UserItem(int userId, string name) { UserId = userId; Name = name; }
        public override string ToString() => Name;
    }

    /// <summary>Checkable month entry used by the "Compare" mode of the Monthly Activity Breakdown (max 5 selected).</summary>
    public class MonthCheckItem : ViewModelBase
    {
        private bool _isChecked;
        public int Month { get; }
        public string Label { get; }
        public Action<MonthCheckItem> OnChanged { get; set; }

        public MonthCheckItem(int month, string label)
        {
            Month = month;
            Label = label;
        }

        public bool IsChecked
        {
            get => _isChecked;
            set { if (SetField(ref _isChecked, value)) OnChanged?.Invoke(this); }
        }
    }

    /// <summary>Checkable year entry used by the "Quarterly" mode of the Monthly Activity Breakdown.</summary>
    public class YearCheckItem : ViewModelBase
    {
        private bool _isChecked;
        public int Year { get; }
        public Action OnChanged { get; set; }

        public YearCheckItem(int year) { Year = year; }

        public bool IsChecked
        {
            get => _isChecked;
            set { if (SetField(ref _isChecked, value)) OnChanged?.Invoke(); }
        }
    }

    /// <summary>
    /// ViewModel for the WPF User Activity Insights dashboard. All aggregation logic is
    /// delegated to <see cref="UserActivityInsightsService"/> and <see cref="UserActivityRepository"/> —
    /// this class only shapes their results into chart series / KPI strings for the view.
    /// Chart objects are created via reflection so this file has no compile-time dependency on
    /// LiveChartsCore or SkiaSharp.
    /// </summary>
    public class UserActivityInsightsViewModel : ViewModelBase
    {
        // Color palette as byte tuples — no SkiaSharp dependency at compile time
        private static readonly (byte R, byte G, byte B)[] Palette =
        {
            (0x3B, 0x82, 0xF6), // Blue
            (0x22, 0xC5, 0x5E), // Green
            (0xF5, 0x9E, 0x0B), // Amber
            (0xEF, 0x44, 0x44), // Red
            (0x8B, 0x5C, 0xF6), // Purple
            (0x06, 0xB6, 0xD4), // Cyan
            (0xF9, 0x73, 0x16), // Orange
            (0x64, 0x74, 0x8B), // Slate
        };

        private static readonly (byte R, byte G, byte B) AxisLabelColor = (0x47, 0x55, 0x69); // #475569
        private static readonly (byte R, byte G, byte B) GridLineColor  = (0xE5, 0xE7, 0xEB); // #E5E7EB

        private static readonly string[] MonthAbbr =
            { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        // Sentinel entries so the filter combos default to a readable "All ..." label
        private static readonly UserItem AllUsersItem = new UserItem(0, "All Users");
        private const string AllActionsLabel     = "All Actions";
        private const string AllEntityTypesLabel = "All Entity Types";

        private readonly UserActivityInsightsService _service;
        private readonly UserActivityRepository _repository;
        private readonly UserActivityFilter _initialFilter;
        private UserActivityFilter _lastFilter;

        private bool _isBusy;
        private DateTime _dateFrom;
        private DateTime _dateTo;
        private UserItem _selectedUser;
        private string _selectedAction;
        private string _selectedEntityType;
        private bool _includeAuditTrail;

        private string _totalActivities = "—";
        private string _activeUsersCount = "—";
        private string _mostCommonAction = "—";
        private string _mostActiveEntityType = "—";
        private string _mostActiveUser = "—";
        private string _peakHour = "—";

        private int _monthlyViewMode; // 0 = Monthly, 1 = Compare, 2 = Quarterly
        private int _selectedYear;
        private int _selectedMonthIndex;

        public UserActivityInsightsViewModel(UserActivityFilter initialFilter = null)
        {
            _service    = new UserActivityInsightsService();
            _repository = new UserActivityRepository();
            _initialFilter = initialFilter ?? new UserActivityFilter
            {
                DateFrom = new DateTime(2026, 1, 20),
                DateTo   = DateTime.Today
            };

            _dateFrom = _initialFilter.DateFrom?.Date ?? new DateTime(2026, 1, 20);
            _dateTo   = _initialFilter.DateTo?.Date   ?? DateTime.Today;
            _includeAuditTrail = _initialFilter.IncludeAuditTrail;

            Users       = new ObservableCollection<UserItem>();
            ActionTypes = new ObservableCollection<string>();
            EntityTypes = new ObservableCollection<string>();

            // Series collections: runtime type is ObservableCollection<ISeries>, stored as IList
            MostActiveHoursSeries    = LvcHelper.CreateSeriesCollection();
            ActivitiesOverTimeSeries = LvcHelper.CreateSeriesCollection();
            ActivitiesByActionSeries = LvcHelper.CreateSeriesCollection();
            EntityDistributionSeries = LvcHelper.CreateSeriesCollection();
            TopUsersSeries           = LvcHelper.CreateSeriesCollection();
            RecentActivities         = new ObservableCollection<UserActivityLogDto>();

            MostActiveHoursXAxes    = LvcHelper.CreateAxisArray(new List<string>(), AxisLabelColor, GridLineColor);
            ActivitiesOverTimeXAxes = LvcHelper.CreateAxisArray(new List<string>(), AxisLabelColor, GridLineColor);
            TopUsersYAxes           = LvcHelper.CreateAxisArray(new List<string>(), AxisLabelColor, GridLineColor);

            int currentYear = DateTime.Today.Year;
            AvailableYears = new ObservableCollection<int>(Enumerable.Range(2020, currentYear - 2020 + 1));
            MonthNames     = new ObservableCollection<string>(MonthAbbr);
            _selectedYear       = currentYear;
            _selectedMonthIndex = DateTime.Today.Month - 1;

            CompareMonths = new ObservableCollection<MonthCheckItem>();
            for (int m = 1; m <= 12; m++)
            {
                var item = new MonthCheckItem(m, MonthAbbr[m - 1]) { IsChecked = m == DateTime.Today.Month };
                item.OnChanged = OnCompareMonthChanged;
                CompareMonths.Add(item);
            }

            QuarterlyYears = new ObservableCollection<YearCheckItem>();
            foreach (var y in AvailableYears)
            {
                var item = new YearCheckItem(y) { IsChecked = y == currentYear };
                item.OnChanged = () => { _ = UpdateMonthlyBreakdownAsync(); };
                QuarterlyYears.Add(item);
            }

            MonthlyBreakdownSeries = LvcHelper.CreateSeriesCollection();
            MonthlyBreakdownXAxes  = LvcHelper.CreateAxisArray(new List<string>(), AxisLabelColor, GridLineColor);
            CompareInsights        = new ObservableCollection<string>();

            ApplyCommand         = new RelayCommand(async () => await LoadAsync(skipDropdowns: true));
            ClearCommand         = new RelayCommand(async () => await ClearAsync());
            // CommandParameter from XAML always arrives as a string (e.g. "1"), never as a
            // boxed int — a RelayCommand<int> would silently fail the "is int" cast on every
            // click and fall back to default(0), making the Compare/Quarterly buttons no-ops.
            SetMonthlyModeCommand = new RelayCommand<string>(s => SetMonthlyViewMode(int.Parse(s)));

            _ = LoadAsync(skipDropdowns: false);
        }

        // ─────────────────────────────────────────────────────────────
        //  Filter-bound properties
        // ─────────────────────────────────────────────────────────────

        public bool IsBusy
        {
            get => _isBusy;
            set => SetField(ref _isBusy, value);
        }

        public DateTime DateFrom
        {
            get => _dateFrom;
            set => SetField(ref _dateFrom, value);
        }

        public DateTime DateTo
        {
            get => _dateTo;
            set => SetField(ref _dateTo, value);
        }

        public ObservableCollection<UserItem> Users { get; }

        public UserItem SelectedUser
        {
            get => _selectedUser;
            set => SetField(ref _selectedUser, value);
        }

        public ObservableCollection<string> ActionTypes { get; }

        public string SelectedAction
        {
            get => _selectedAction;
            set => SetField(ref _selectedAction, value);
        }

        public ObservableCollection<string> EntityTypes { get; }

        public string SelectedEntityType
        {
            get => _selectedEntityType;
            set => SetField(ref _selectedEntityType, value);
        }

        public bool IncludeAuditTrail
        {
            get => _includeAuditTrail;
            set => SetField(ref _includeAuditTrail, value);
        }

        // ─────────────────────────────────────────────────────────────
        //  KPI cards
        // ─────────────────────────────────────────────────────────────

        public string TotalActivities
        {
            get => _totalActivities;
            private set => SetField(ref _totalActivities, value);
        }

        public string ActiveUsersCount
        {
            get => _activeUsersCount;
            private set => SetField(ref _activeUsersCount, value);
        }

        public string MostCommonAction
        {
            get => _mostCommonAction;
            private set => SetField(ref _mostCommonAction, value);
        }

        public string MostActiveEntityType
        {
            get => _mostActiveEntityType;
            private set => SetField(ref _mostActiveEntityType, value);
        }

        public string MostActiveUser
        {
            get => _mostActiveUser;
            private set => SetField(ref _mostActiveUser, value);
        }

        public string PeakHour
        {
            get => _peakHour;
            private set => SetField(ref _peakHour, value);
        }

        // ─────────────────────────────────────────────────────────────
        //  Charts — series stored as IList (runtime: ObservableCollection<ISeries>)
        //           axes stored as object[] (runtime: Axis[])
        // ─────────────────────────────────────────────────────────────

        public IList MostActiveHoursSeries { get; }
        public object[] MostActiveHoursXAxes { get; private set; }

        public IList ActivitiesOverTimeSeries { get; }
        public object[] ActivitiesOverTimeXAxes { get; private set; }

        public IList ActivitiesByActionSeries { get; }

        public IList EntityDistributionSeries { get; }

        public IList TopUsersSeries { get; }
        public object[] TopUsersYAxes { get; private set; }

        public ObservableCollection<UserActivityLogDto> RecentActivities { get; }

        // ─────────────────────────────────────────────────────────────
        //  Monthly Activity Breakdown (Monthly / Compare up to 5 / Quarterly)
        // ─────────────────────────────────────────────────────────────

        public ObservableCollection<int> AvailableYears { get; }
        public ObservableCollection<string> MonthNames { get; }
        public ObservableCollection<MonthCheckItem> CompareMonths { get; }
        public ObservableCollection<YearCheckItem> QuarterlyYears { get; }
        public IList MonthlyBreakdownSeries { get; }
        public object[] MonthlyBreakdownXAxes { get; private set; }
        public ObservableCollection<string> CompareInsights { get; }

        public int MonthlyViewMode => _monthlyViewMode;
        public bool IsMonthlyMode => _monthlyViewMode == 0;
        public bool IsCompareMode => _monthlyViewMode == 1;
        public bool IsQuarterlyMode => _monthlyViewMode == 2;

        public int SelectedYear
        {
            get => _selectedYear;
            set { if (SetField(ref _selectedYear, value)) _ = UpdateMonthlyBreakdownAsync(); }
        }

        public int SelectedMonthIndex
        {
            get => _selectedMonthIndex;
            set { if (SetField(ref _selectedMonthIndex, value)) _ = UpdateMonthlyBreakdownAsync(); }
        }

        public ICommand SetMonthlyModeCommand { get; private set; }

        public ICommand ApplyCommand { get; }
        public ICommand ClearCommand { get; }

        // ─────────────────────────────────────────────────────────────
        //  Data loading
        // ─────────────────────────────────────────────────────────────

        private async Task ClearAsync()
        {
            DateFrom = new DateTime(2026, 1, 20);
            DateTo   = DateTime.Today;
            SelectedUser = AllUsersItem;
            SelectedAction = AllActionsLabel;
            SelectedEntityType = AllEntityTypesLabel;
            IncludeAuditTrail = false;
            await LoadAsync(skipDropdowns: true);
        }

        private UserActivityFilter BuildFilter()
        {
            var toDate = new DateTime(DateTo.Year, DateTo.Month, DateTime.DaysInMonth(DateTo.Year, DateTo.Month));
            return new UserActivityFilter
            {
                DateFrom          = DateFrom.Date,
                DateTo            = toDate,
                UserId            = SelectedUser == null || SelectedUser == AllUsersItem ? (int?)null : SelectedUser.UserId,
                ActionType        = string.IsNullOrWhiteSpace(SelectedAction) || SelectedAction == AllActionsLabel ? null : SelectedAction,
                EntityType        = string.IsNullOrWhiteSpace(SelectedEntityType) || SelectedEntityType == AllEntityTypesLabel ? null : SelectedEntityType,
                IncludeAuditTrail = IncludeAuditTrail
            };
        }

        public async Task LoadAsync(bool skipDropdowns)
        {
            IsBusy = true;
            try
            {
                if (!skipDropdowns)
                {
                    var users    = await Task.Run(() => _repository.GetDistinctUsers());
                    var actions  = await Task.Run(() => _repository.GetDistinctActionTypes());
                    var entities = await Task.Run(() => _repository.GetDistinctEntityTypes());

                    Users.Clear();
                    Users.Add(AllUsersItem);
                    foreach (var u in users) Users.Add(new UserItem(u.UserId, u.Name));
                    SelectedUser = _initialFilter.UserId.HasValue
                        ? Users.FirstOrDefault(u => u.UserId == _initialFilter.UserId.Value) ?? AllUsersItem
                        : AllUsersItem;

                    ActionTypes.Clear();
                    ActionTypes.Add(AllActionsLabel);
                    foreach (var a in actions) ActionTypes.Add(a);
                    SelectedAction = string.IsNullOrWhiteSpace(_initialFilter.ActionType)
                        ? AllActionsLabel
                        : _initialFilter.ActionType;

                    EntityTypes.Clear();
                    EntityTypes.Add(AllEntityTypesLabel);
                    foreach (var e in entities) EntityTypes.Add(e);
                    SelectedEntityType = string.IsNullOrWhiteSpace(_initialFilter.EntityType)
                        ? AllEntityTypesLabel
                        : _initialFilter.EntityType;
                }

                var filter = BuildFilter();
                _lastFilter = filter;

                var hoursTask    = Task.Run(() => _service.GetMostActiveHours(filter));
                var topUsersTask = Task.Run(() => _service.GetTopUsers(filter));
                var entityTask   = Task.Run(() => _service.GetByEntityType(filter));
                var monthlyTask  = Task.Run(() => _service.GetMonthlyTrendData(DateTime.Today.Year, filter.UserId, filter.ActionType, filter.EntityType));
                var rowsTask     = Task.Run(() => _repository.GetFiltered(filter));
                await Task.WhenAll(hoursTask, topUsersTask, entityTask, monthlyTask, rowsTask);

                var hours     = hoursTask.Result;
                var topUsers  = topUsersTask.Result;
                var entities2 = entityTask.Result;
                var monthly   = monthlyTask.Result;
                var rows      = rowsTask.Result;

                UpdateKpis(rows, hours, topUsers, entities2);
                UpdateMostActiveHours(hours);
                UpdateActivitiesOverTime(monthly);
                UpdateActivitiesByAction(rows);
                UpdateEntityDistribution(entities2);
                UpdateTopUsers(topUsers);
                UpdateRecentActivities(rows);
                await UpdateMonthlyBreakdownAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SetMonthlyViewMode(int mode)
        {
            if (_monthlyViewMode == mode) return;
            _monthlyViewMode = mode;
            OnPropertyChanged(nameof(MonthlyViewMode));
            OnPropertyChanged(nameof(IsMonthlyMode));
            OnPropertyChanged(nameof(IsCompareMode));
            OnPropertyChanged(nameof(IsQuarterlyMode));
            _ = UpdateMonthlyBreakdownAsync();
        }

        private void OnCompareMonthChanged(MonthCheckItem changed)
        {
            if (changed.IsChecked && CompareMonths.Count(m => m.IsChecked) > 5)
            {
                changed.IsChecked = false;
                return;
            }
            _ = UpdateMonthlyBreakdownAsync();
        }

        private async Task UpdateMonthlyBreakdownAsync()
        {
            if (_lastFilter == null) return;
            var filter = _lastFilter;

            MonthlyBreakdownSeries.Clear();
            CompareInsights.Clear();

            if (_monthlyViewMode == 0)
            {
                var days = await Task.Run(() =>
                    _service.GetDailyTrendData(SelectedYear, SelectedMonthIndex + 1, filter.UserId, filter.ActionType, filter.EntityType));

                MonthlyBreakdownSeries.Add(LvcHelper.CreateColumnSeries(
                    name:   MonthAbbr[SelectedMonthIndex],
                    values: days.Select(d => (double)d.Count).ToArray(),
                    color:  Palette[0],
                    rx: 3, ry: 3));

                MonthlyBreakdownXAxes = LvcHelper.CreateAxisArray(
                    days.Select(d => d.Day.ToString()).ToList(), AxisLabelColor, GridLineColor);
            }
            else if (_monthlyViewMode == 1)
            {
                var selected = CompareMonths.Where(m => m.IsChecked).ToList();
                if (selected.Count == 0) selected.Add(CompareMonths[SelectedMonthIndex]);

                var results = new List<(MonthCheckItem Item, List<(int Day, int Count)> Days)>();
                foreach (var m in selected)
                {
                    var days = await Task.Run(() =>
                        _service.GetDailyTrendData(SelectedYear, m.Month, filter.UserId, filter.ActionType, filter.EntityType));
                    results.Add((m, days));
                }

                int maxLen = results.Count > 0 ? results.Max(r => r.Days.Count) : 0;
                for (int i = 0; i < results.Count; i++)
                {
                    var (item, days) = results[i];
                    var color = Palette[i % Palette.Length];
                    MonthlyBreakdownSeries.Add(LvcHelper.CreateLineSeries(
                        name:   item.Label,
                        values: days.Select(d => (double)d.Count).ToArray(),
                        color:  color,
                        strokeThickness: 3,
                        geometrySize: 5));
                }
                MonthlyBreakdownXAxes = LvcHelper.CreateAxisArray(
                    Enumerable.Range(1, maxLen).Select(i => i.ToString()).ToList(),
                    AxisLabelColor, GridLineColor);

                // A dominant month squashes smaller series flat — show totals/deltas as text
                CompareInsights.Clear();
                for (int i = 0; i < results.Count; i++)
                {
                    int total = results[i].Days.Sum(d => d.Count);
                    if (i == 0)
                    {
                        CompareInsights.Add($"{results[i].Item.Label} activity — baseline ({total:N0} activities)");
                    }
                    else
                    {
                        int prevTotal = results[i - 1].Days.Sum(d => d.Count);
                        string verb;
                        string pctText;
                        if (prevTotal == 0)
                        {
                            verb = total > 0 ? "increased" : "stayed the same";
                            pctText = total > 0 ? "from 0" : "0%";
                        }
                        else
                        {
                            double pct = (total - prevTotal) * 100.0 / prevTotal;
                            verb = pct >= 0 ? "increased" : "decreased";
                            pctText = $"{Math.Abs(pct):N0}%";
                        }
                        CompareInsights.Add(
                            $"{results[i].Item.Label} activity {verb} by {pctText} compared to {results[i - 1].Item.Label} ({total:N0} vs {prevTotal:N0})");
                    }
                }
            }
            else
            {
                var selectedYears = QuarterlyYears.Where(y => y.IsChecked).ToList();
                if (selectedYears.Count == 0) selectedYears.Add(QuarterlyYears.Last());

                for (int i = 0; i < selectedYears.Count; i++)
                {
                    var y = selectedYears[i];
                    var q = await Task.Run(() =>
                        _service.GetQuarterlyTrendData(y.Year, filter.UserId, filter.ActionType, filter.EntityType));
                    var color = Palette[i % Palette.Length];
                    MonthlyBreakdownSeries.Add(LvcHelper.CreateColumnSeries(
                        name:   y.Year.ToString(),
                        values: q.Select(x => (double)x.Count).ToArray(),
                        color:  color,
                        rx: 3, ry: 3));
                }
                MonthlyBreakdownXAxes = LvcHelper.CreateAxisArray(
                    new List<string> { "Q1", "Q2", "Q3", "Q4" }, AxisLabelColor, GridLineColor);
            }

            OnPropertyChanged(nameof(MonthlyBreakdownXAxes));
        }

        private void UpdateKpis(
            List<UserActivityLogDto> rows,
            List<(int Hour, int Count)> hours,
            List<(string UserName, int Count)> topUsers,
            List<(string EntityType, int Count)> entities)
        {
            TotalActivities  = rows.Count.ToString("N0");
            ActiveUsersCount = rows.Select(r => r.UserId).Distinct().Count().ToString("N0");

            MostCommonAction = rows.Count == 0
                ? "—"
                : rows.GroupBy(r => string.IsNullOrWhiteSpace(r.ActionType) ? "(None)" : r.ActionType)
                      .OrderByDescending(g => g.Count())
                      .First().Key;

            MostActiveEntityType = entities.Count > 0 ? entities[0].EntityType : "—";
            MostActiveUser        = topUsers.Count > 0 ? topUsers[0].UserName : "—";

            PeakHour = hours.Count > 0
                ? FormatHour(hours.OrderByDescending(h => h.Count).First().Hour)
                : "—";
        }

        private void UpdateMostActiveHours(List<(int Hour, int Count)> hours)
        {
            var byHour = new Dictionary<int, int>();
            foreach (var h in hours) byHour[h.Hour] = h.Count;

            var range  = Enumerable.Range(8, 11).ToList(); // 8..18 inclusive
            var counts = range.Select(h => byHour.TryGetValue(h, out int c) ? c : 0).ToList();

            MostActiveHoursSeries.Clear();
            MostActiveHoursSeries.Add(LvcHelper.CreateColumnSeries(
                name:   "Activities",
                values: counts.Select(c => (double)c).ToArray(),
                color:  Palette[0],
                rx: 3, ry: 3));

            MostActiveHoursXAxes = LvcHelper.CreateAxisArray(
                range.Select(FormatHour).ToList(), AxisLabelColor, GridLineColor);
            OnPropertyChanged(nameof(MostActiveHoursXAxes));
        }

        private void UpdateActivitiesOverTime(List<(int Month, int Count)> monthly)
        {
            string[] monthAbbr = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

            ActivitiesOverTimeSeries.Clear();
            ActivitiesOverTimeSeries.Add(LvcHelper.CreateLineSeries(
                name:            "Activities",
                values:          monthly.Select(m => (double)m.Count).ToArray(),
                color:           Palette[0],
                strokeThickness: 3,
                geometrySize:    6));

            ActivitiesOverTimeXAxes = LvcHelper.CreateAxisArray(
                monthly.Select(m => monthAbbr[m.Month - 1]).ToList(), AxisLabelColor, GridLineColor);
            OnPropertyChanged(nameof(ActivitiesOverTimeXAxes));
        }

        private void UpdateActivitiesByAction(List<UserActivityLogDto> rows)
        {
            var byAction = rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.ActionType) ? "(None)" : r.ActionType)
                .Select(g => (Action: g.Key, Count: g.Count()))
                .OrderByDescending(x => x.Count)
                .ToList();

            ActivitiesByActionSeries.Clear();
            for (int i = 0; i < byAction.Count; i++)
            {
                var (action, count) = byAction[i];
                ActivitiesByActionSeries.Add(LvcHelper.CreatePieSeries(
                    name:   action,
                    values: new[] { (double)count },
                    color:  Palette[i % Palette.Length]));
            }
        }

        private void UpdateEntityDistribution(List<(string EntityType, int Count)> entities)
        {
            EntityDistributionSeries.Clear();
            for (int i = 0; i < entities.Count; i++)
            {
                var (entityType, count) = entities[i];
                EntityDistributionSeries.Add(LvcHelper.CreatePieSeries(
                    name:   entityType,
                    values: new[] { (double)count },
                    color:  Palette[i % Palette.Length]));
            }
        }

        private void UpdateTopUsers(List<(string UserName, int Count)> topUsers)
        {
            var ordered = topUsers.OrderBy(u => u.Count).ToList();

            TopUsersSeries.Clear();
            TopUsersSeries.Add(LvcHelper.CreateRowSeries(
                name:   "Activities",
                values: ordered.Select(u => (double)u.Count).ToArray(),
                color:  Palette[4],
                rx: 3, ry: 3));

            TopUsersYAxes = LvcHelper.CreateAxisArray(
                ordered.Select(u => u.UserName).ToList(), AxisLabelColor, GridLineColor);
            OnPropertyChanged(nameof(TopUsersYAxes));
        }

        private void UpdateRecentActivities(List<UserActivityLogDto> rows)
        {
            RecentActivities.Clear();
            foreach (var r in rows.OrderByDescending(r => r.CreatedDate).Take(10))
                RecentActivities.Add(r);
        }

        private static string FormatHour(int hour)
        {
            var dt = new DateTime(2000, 1, 1, hour, 0, 0);
            return dt.ToString("h tt");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Reflection wrapper for LiveChartsCore + SkiaSharp types.
    //  Keeps all compile-time references to those assemblies out of the ViewModel
    //  so the project builds even when Roslyn cannot load the native SkiaSharp DLL.
    // ─────────────────────────────────────────────────────────────────────────
    internal static class LvcHelper
    {
        private static bool _initialized;

        private static Type _skColorType;
        private static ConstructorInfo _skColorCtor;   // SKColor(byte, byte, byte)

        private static Type _solidColorPaintType;
        private static ConstructorInfo _solidColorPaintCtor; // SolidColorPaint(SKColor)

        private static Type _axisType;
        private static Type _iSeriesType;
        private static Type _obsCollOfISeriesType;     // ObservableCollection<ISeries>

        private static Type _columnDoubleType;         // ColumnSeries<double>
        private static Type _lineDoubleType;           // LineSeries<double>
        private static Type _pieDoubleType;            // PieSeries<double>
        private static Type _rowDoubleType;            // RowSeries<double>

        private static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                var skiaAsm    = Assembly.Load("SkiaSharp");
                var lvcCoreAsm = Assembly.Load("LiveChartsCore");
                var lvcSkiaAsm = Assembly.Load("LiveChartsCore.SkiaSharpView");

                _skColorType = skiaAsm.GetType("SkiaSharp.SKColor");
                if (_skColorType != null)
                    _skColorCtor = _skColorType.GetConstructor(new[] { typeof(byte), typeof(byte), typeof(byte) });

                _solidColorPaintType = lvcSkiaAsm.GetType("LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint");
                if (_solidColorPaintType != null && _skColorType != null)
                    _solidColorPaintCtor = _solidColorPaintType.GetConstructor(new[] { _skColorType });

                _axisType    = lvcSkiaAsm.GetType("LiveChartsCore.SkiaSharpView.Axis");
                _iSeriesType = lvcCoreAsm.GetType("LiveChartsCore.ISeries");

                if (_iSeriesType != null)
                    _obsCollOfISeriesType = typeof(ObservableCollection<>).MakeGenericType(_iSeriesType);

                _columnDoubleType = lvcSkiaAsm.GetType("LiveChartsCore.SkiaSharpView.ColumnSeries`1")
                                              ?.MakeGenericType(typeof(double));
                _lineDoubleType   = lvcSkiaAsm.GetType("LiveChartsCore.SkiaSharpView.LineSeries`1")
                                              ?.MakeGenericType(typeof(double));
                _pieDoubleType    = lvcSkiaAsm.GetType("LiveChartsCore.SkiaSharpView.PieSeries`1")
                                              ?.MakeGenericType(typeof(double));
                _rowDoubleType    = lvcSkiaAsm.GetType("LiveChartsCore.SkiaSharpView.RowSeries`1")
                                              ?.MakeGenericType(typeof(double));
            }
            catch { /* chart features unavailable at runtime */ }
        }

        // Returns an ObservableCollection<ISeries> as IList so the ViewModel has no
        // compile-time reference to ISeries. Falls back to ArrayList if load fails.
        public static IList CreateSeriesCollection()
        {
            EnsureInit();
            if (_obsCollOfISeriesType != null)
                return (IList)Activator.CreateInstance(_obsCollOfISeriesType);
            return new ArrayList();
        }

        // Returns an Axis[] (via Array.CreateInstance) cast to object[].
        // Reference-type array covariance makes the cast valid; reflection SetValue
        // on CartesianChart.XAxes/YAxes receives the real Axis[] at runtime.
        public static object[] CreateAxisArray(
            IList<string> labels,
            (byte R, byte G, byte B) labelColor,
            (byte R, byte G, byte B) gridColor,
            double rotation = 0)
        {
            EnsureInit();
            var axis = BuildAxis(labels, labelColor, gridColor, rotation);
            if (_axisType != null)
            {
                var arr = Array.CreateInstance(_axisType, 1);
                arr.SetValue(axis, 0);
                return (object[])arr;
            }
            return new object[] { axis };
        }

        private static object BuildAxis(
            IList<string> labels,
            (byte R, byte G, byte B) labelColor,
            (byte R, byte G, byte B) gridColor,
            double rotation)
        {
            if (_axisType == null) return new object();
            var axis = Activator.CreateInstance(_axisType);
            _axisType.GetProperty("Labels")?.SetValue(axis, labels);
            _axisType.GetProperty("LabelsRotation")?.SetValue(axis, rotation);
            _axisType.GetProperty("TextSize")?.SetValue(axis, 12.0);
            _axisType.GetProperty("LabelsPaint")?.SetValue(axis, MakePaint(labelColor));
            var sepPaint = MakePaint(gridColor);
            if (sepPaint != null)
                _solidColorPaintType?.GetProperty("StrokeThickness")?.SetValue(sepPaint, 1.0f);
            _axisType.GetProperty("SeparatorsPaint")?.SetValue(axis, sepPaint);
            return axis;
        }

        private static object MakeSKColor((byte R, byte G, byte B) c)
            => _skColorCtor?.Invoke(new object[] { c.R, c.G, c.B });

        private static object MakePaint((byte R, byte G, byte B) c, float strokeThickness = 0)
        {
            var skColor = MakeSKColor(c);
            if (skColor == null || _solidColorPaintCtor == null) return null;
            var paint = _solidColorPaintCtor.Invoke(new[] { skColor });
            if (strokeThickness > 0)
                _solidColorPaintType.GetProperty("StrokeThickness")?.SetValue(paint, strokeThickness);
            return paint;
        }

        private static void SetProp(object obj, string name, object value)
        {
            if (obj == null || value == null) return;
            obj.GetType().GetProperty(name)?.SetValue(obj, value);
        }

        public static object CreateColumnSeries(
            string name, double[] values, (byte R, byte G, byte B) color, int rx = 0, int ry = 0)
        {
            EnsureInit();
            if (_columnDoubleType == null) return new object();
            var s = Activator.CreateInstance(_columnDoubleType);
            SetProp(s, "Name",   name);
            SetProp(s, "Values", values);
            SetProp(s, "Fill",   MakePaint(color));
            if (rx != 0) SetProp(s, "Rx", (double)rx);
            if (ry != 0) SetProp(s, "Ry", (double)ry);
            return s;
        }

        public static object CreateLineSeries(
            string name, double[] values, (byte R, byte G, byte B) color,
            float strokeThickness = 2, double geometrySize = 6)
        {
            EnsureInit();
            if (_lineDoubleType == null) return new object();
            var s = Activator.CreateInstance(_lineDoubleType);
            SetProp(s, "Name",           name);
            SetProp(s, "Values",         values);
            SetProp(s, "Fill",           null);   // no area fill for line charts
            SetProp(s, "Stroke",         MakePaint(color, strokeThickness));
            SetProp(s, "GeometryFill",   MakePaint(color));
            SetProp(s, "GeometryStroke", MakePaint(color));
            SetProp(s, "GeometrySize",   geometrySize);
            return s;
        }

        public static object CreatePieSeries(
            string name, double[] values, (byte R, byte G, byte B) color)
        {
            EnsureInit();
            if (_pieDoubleType == null) return new object();
            var s = Activator.CreateInstance(_pieDoubleType);
            SetProp(s, "Name",   name);
            SetProp(s, "Values", values);
            SetProp(s, "Fill",   MakePaint(color));
            return s;
        }

        public static object CreateRowSeries(
            string name, double[] values, (byte R, byte G, byte B) color, int rx = 0, int ry = 0)
        {
            EnsureInit();
            if (_rowDoubleType == null) return new object();
            var s = Activator.CreateInstance(_rowDoubleType);
            SetProp(s, "Name",   name);
            SetProp(s, "Values", values);
            SetProp(s, "Fill",   MakePaint(color));
            if (rx != 0) SetProp(s, "Rx", (double)rx);
            if (ry != 0) SetProp(s, "Ry", (double)ry);
            return s;
        }
    }
}
