using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.ReferenceData.ViewModels
{
    public sealed class HolidayRow
    {
        public HolidayRow(HolidayDto dto, DateTime displayDate)
        {
            Dto = dto;
            DisplayDate = displayDate;
        }

        public HolidayDto Dto { get; }
        public DateTime DisplayDate { get; }

        public int    HolidayId     => Dto.HolidayId;
        public string DateText      => DisplayDate.ToString("MMM dd, yyyy");
        public string HolidayName   => Dto.HolidayName;
        public string HolidayType   => Dto.HolidayType;
        public string RecurringText => Dto.IsRecurring ? "Yes" : "—";
        public string StatusText    => Dto.IsActive ? "Active" : "Inactive";
        public bool   IsActive      => Dto.IsActive;
        public bool   IsRegular     => Dto.HolidayType == HolidayManagementViewModel.RegularType;
    }

    /// <summary>
    /// Admin Portal > Reference Data > Holidays (WPF). Same data and rules as the old WinForms
    /// HolidayManagementPage: recurring holidays show in every year, the Year / Show / Search
    /// filters, quick stats over active holidays, and a calendar that marks holiday dates.
    /// The list shows every holiday for the selected year (a year is short, so no paging).
    /// </summary>
    public sealed class HolidayManagementViewModel : ViewModelBase
    {
        public const string RegularType = "Regular Holiday";
        public const string SpecialType = "Special Non-Working";

        private readonly HolidayRepository _repo = new HolidayRepository();

        private List<HolidayDto> _all = new List<HolidayDto>();
        private List<HolidayRow> _filtered = new List<HolidayRow>();

        private string _searchText = string.Empty;
        private string _showOption = "Active Only";
        private int _selectedYear = DateTime.Today.Year;
        private bool _isLoading;
        private string _countText = string.Empty;
        private string _statTotal = "—", _statRegular = "—", _statSpecial = "—", _statUpcoming = "—";
        private HolidayRow _selectedRow;

        public HolidayManagementViewModel()
        {
            int cy = DateTime.Today.Year;
            for (int y = cy - 3; y <= cy + 3; y++) Years.Add(y);
        }

        /// <summary>Raised after the list is refiltered, so the calendar can re-mark holiday dates.</summary>
        public event EventHandler HolidaysChanged;

        public List<string> ShowOptions { get; } = new List<string>
            { "Active Only", "All", "Regular Holidays", "Special Non-Working", "Inactive Only" };
        public ObservableCollection<int> Years { get; } = new ObservableCollection<int>();
        public ObservableCollection<HolidayRow> Rows { get; } = new ObservableCollection<HolidayRow>();
        public List<HolidayDto> AllHolidays => _all;

        public bool   IsLoading    { get => _isLoading;    private set => SetField(ref _isLoading, value); }
        public string CountText    { get => _countText;    private set => SetField(ref _countText, value); }
        public string StatTotal    { get => _statTotal;    private set => SetField(ref _statTotal, value); }
        public string StatRegular  { get => _statRegular;  private set => SetField(ref _statRegular, value); }
        public string StatSpecial  { get => _statSpecial;  private set => SetField(ref _statSpecial, value); }
        public string StatUpcoming { get => _statUpcoming; private set => SetField(ref _statUpcoming, value); }

        public bool IsEmpty => _filtered.Count == 0;

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilter(); }
        }

        public string ShowOption
        {
            get => _showOption;
            set { if (SetField(ref _showOption, value)) ApplyFilter(); }
        }

        public int SelectedYear
        {
            get => _selectedYear;
            set { if (SetField(ref _selectedYear, value)) ApplyFilter(); }
        }

        // ── Selection / details ──────────────────────────────────────────────

        public HolidayRow SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (!SetField(ref _selectedRow, value)) return;
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(DetailName));
                OnPropertyChanged(nameof(DetailMeta));
                OnPropertyChanged(nameof(DetailNotes));
                OnPropertyChanged(nameof(DetailIsRegular));
            }
        }

        public bool HasSelection => _selectedRow != null;
        public bool DetailIsRegular => _selectedRow?.IsRegular == true;
        public string DetailName => _selectedRow?.HolidayName ?? "No holiday selected";

        public string DetailMeta
        {
            get
            {
                if (_selectedRow == null) return "Select a row or click a date on the calendar";
                var h = _selectedRow.Dto;
                string recurring = h.IsRecurring ? " · Recurring annually" : "";
                string status = h.IsActive ? "" : " · Inactive";
                return $"{_selectedRow.DisplayDate:dddd, MMMM dd, yyyy}  ·  {h.HolidayType}{recurring}{status}";
            }
        }

        public string DetailNotes =>
            string.IsNullOrWhiteSpace(_selectedRow?.Dto.Notes) ? "" : $"Notes: {_selectedRow.Dto.Notes}";

        // ── Loading / filtering ──────────────────────────────────────────────

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                _all = await Task.Run(() => _repo.GetAll());
                ApplyFilter();
                UpdateStats();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public static DateTime DisplayDate(HolidayDto h, int year)
        {
            if (!h.IsRecurring) return h.HolidayDate;
            try { return new DateTime(year, h.HolidayDate.Month, h.HolidayDate.Day); }
            catch { return h.HolidayDate; }   // Feb 29 in a non-leap year
        }

        private void ApplyFilter()
        {
            int year = _selectedYear;
            var filter = _showOption ?? "Active Only";
            var q = (_searchText ?? "").Trim().ToLowerInvariant();

            _filtered = _all.Where(h =>
            {
                // Recurring holidays appear in every year
                if (!h.IsRecurring && h.HolidayDate.Year != year) return false;

                if (filter == "Active Only"         && !h.IsActive)                return false;
                if (filter == "Inactive Only"       &&  h.IsActive)                return false;
                if (filter == "Regular Holidays"    && h.HolidayType != RegularType) return false;
                if (filter == "Special Non-Working" && h.HolidayType != SpecialType) return false;

                if (!string.IsNullOrEmpty(q) &&
                    !(h.HolidayName ?? "").ToLowerInvariant().Contains(q) &&
                    !(h.HolidayType ?? "").ToLowerInvariant().Contains(q)) return false;

                return true;
            })
            .Select(h => new HolidayRow(h, DisplayDate(h, year)))
            .OrderBy(r => r.DisplayDate)
            .ToList();

            CountText = $"{_filtered.Count} record{(_filtered.Count == 1 ? "" : "s")}";
            OnPropertyChanged(nameof(IsEmpty));

            Rows.Clear();
            foreach (var r in _filtered) Rows.Add(r);
            SelectedRow = null;

            HolidaysChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateStats()
        {
            var active = _all.Where(h => h.IsActive).ToList();
            int today = DateTime.Today.DayOfYear;
            int in30 = DateTime.Today.AddDays(30).DayOfYear;
            int upcoming = active.Count(h =>
            {
                int doy = DisplayDate(h, DateTime.Today.Year).DayOfYear;
                return doy >= today && doy <= in30;
            });

            StatTotal    = active.Count.ToString();
            StatRegular  = active.Count(h => h.HolidayType == RegularType).ToString();
            StatSpecial  = active.Count(h => h.HolidayType == SpecialType).ToString();
            StatUpcoming = upcoming.ToString();
        }

        /// <summary>Active holiday dates in the selected year, with their type, for marking the calendar.</summary>
        public Dictionary<DateTime, string> GetCalendarMarks()
        {
            var marks = new Dictionary<DateTime, string>();
            foreach (var h in _all.Where(h => h.IsActive))
            {
                var d = DisplayDate(h, _selectedYear).Date;
                if (d.Year != _selectedYear) continue;
                // A Regular Holiday wins when two holidays share a date.
                if (!marks.TryGetValue(d, out var existing) || existing != RegularType)
                    marks[d] = h.HolidayType;
            }
            return marks;
        }

        /// <summary>Selects the listed holiday on this date. Returns false if none is listed.</summary>
        public bool SelectByDate(DateTime date)
        {
            var row = _filtered.FirstOrDefault(r => r.DisplayDate.Date == date.Date);
            if (row == null) return false;
            SelectedRow = row;
            return true;
        }

        // ── CRUD ─────────────────────────────────────────────────────────────

        public async Task AddAsync(HolidayDto dto)
        {
            dto.CreatedBy = AppSession.CurrentUserId;
            await Task.Run(() => _repo.Insert(dto));
            ActivityLogger.Log("Create", "CompanyHoliday", 0,
                $"Holiday added: {dto.HolidayName} ({dto.HolidayDate:yyyy-MM-dd})");
            await LoadAsync();
        }

        public async Task UpdateAsync(int holidayId, HolidayDto dto)
        {
            dto.HolidayId = holidayId;
            await Task.Run(() => _repo.Update(dto));
            ActivityLogger.Log("Update", "CompanyHoliday", holidayId, $"Holiday updated: {dto.HolidayName}");
            await LoadAsync();
        }

        public async Task DeleteAsync(HolidayDto dto)
        {
            await Task.Run(() => _repo.Delete(dto.HolidayId));
            ActivityLogger.Log("Delete", "CompanyHoliday", dto.HolidayId, $"Holiday deleted: {dto.HolidayName}");
            await LoadAsync();
        }
    }
}
