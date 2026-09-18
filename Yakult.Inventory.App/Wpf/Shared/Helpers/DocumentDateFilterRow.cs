using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Shared.Helpers
{
    /// <summary>
    /// One row of the Document Date filter (Month/Day/Year, each independently "Any"-able).
    /// A page can hold a collection of these to let the user filter by multiple document dates
    /// at once (OR'd together) — e.g. "March 2026" OR "June 2026".
    /// </summary>
    public sealed class DocumentDateFilterRow : ViewModelBase
    {
        public static readonly string[] MonthNames =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };

        public ObservableCollection<string> YearOptions { get; } = new ObservableCollection<string> { "Any" };
        public ObservableCollection<string> MonthOptions { get; } = new ObservableCollection<string>(new[] { "Any" }.Concat(MonthNames));
        public ObservableCollection<string> DayOptions { get; } = new ObservableCollection<string>(new[] { "Any" }.Concat(Enumerable.Range(1, 31).Select(d => d.ToString())));

        private string _selectedYear = "Any";
        public string SelectedYear
        {
            get => _selectedYear;
            set { if (SetField(ref _selectedYear, value)) Changed?.Invoke(); }
        }

        private string _selectedMonth = "Any";
        public string SelectedMonth
        {
            get => _selectedMonth;
            set { if (SetField(ref _selectedMonth, value)) Changed?.Invoke(); }
        }

        private string _selectedDay = "Any";
        public string SelectedDay
        {
            get => _selectedDay;
            set { if (SetField(ref _selectedDay, value)) Changed?.Invoke(); }
        }

        /// <summary>Raised whenever Year/Month/Day changes, so the owning page can re-filter.</summary>
        public event Action Changed;

        /// <summary>Set by the owning page/collection so the "✕" button can remove this row.</summary>
        public RelayCommand RemoveCommand { get; set; }

        /// <summary>True once at least one of Year/Month/Day is constrained away from "Any".</summary>
        public bool HasAnyConstraint => _selectedYear != "Any" || _selectedMonth != "Any" || _selectedDay != "Any";

        public bool Matches(DateTime? documentDate)
        {
            if (!HasAnyConstraint) return true;
            if (!documentDate.HasValue) return false;

            if (_selectedYear != "Any")
            {
                if (!int.TryParse(_selectedYear, out var y) || documentDate.Value.Year != y) return false;
            }
            if (_selectedMonth != "Any")
            {
                var m = Array.IndexOf(MonthNames, _selectedMonth) + 1;
                if (m <= 0 || documentDate.Value.Month != m) return false;
            }
            if (_selectedDay != "Any")
            {
                if (!int.TryParse(_selectedDay, out var d) || documentDate.Value.Day != d) return false;
            }
            return true;
        }

        /// <summary>Rebuilds YearOptions from the currently-loaded rows' document years, preserving
        /// the current selection if it's still valid.</summary>
        public void SetYearOptions(IEnumerable<int> years)
        {
            var previouslySelected = _selectedYear;

            YearOptions.Clear();
            YearOptions.Add("Any");
            foreach (var y in years.Distinct().OrderByDescending(y => y))
                YearOptions.Add(y.ToString());

            _selectedYear = YearOptions.Contains(previouslySelected) ? previouslySelected : "Any";
            OnPropertyChanged(nameof(SelectedYear));
        }

        public void Reset()
        {
            _selectedYear = "Any";
            OnPropertyChanged(nameof(SelectedYear));
            _selectedMonth = "Any";
            OnPropertyChanged(nameof(SelectedMonth));
            _selectedDay = "Any";
            OnPropertyChanged(nameof(SelectedDay));
        }
    }
}
