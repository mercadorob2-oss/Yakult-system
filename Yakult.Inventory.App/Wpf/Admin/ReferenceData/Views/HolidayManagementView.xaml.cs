using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Yakult.Inventory.App.WPF.Admin.ReferenceData.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.ReferenceData.Views
{
    /// <summary>
    /// Admin Portal > Reference Data > Holidays (WPF). Calendar sidebar + full holiday list for
    /// the selected year; picking a row moves the calendar to it and clicking a calendar date
    /// selects its holiday.
    /// </summary>
    public partial class HolidayManagementView : UserControl
    {
        private readonly HolidayManagementViewModel _vm = new HolidayManagementViewModel();
        private bool _loadedOnce;
        private bool _suppressCalendarSync;

        public HolidayManagementView()
        {
            InitializeComponent();
            DataContext = _vm;
            _vm.HolidaysChanged += (s, e) => RefreshCalendarMarks();
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(HolidayManagementViewModel.SelectedYear))
                    SyncCalendarYear();
            };
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_loadedOnce) return;
            _loadedOnce = true;
            await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try { await _vm.LoadAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load holidays:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Calendar ─────────────────────────────────────────────────────────

        /// <summary>Re-marks holiday dates: day buttons only re-evaluate their style when it is re-applied.</summary>
        private void RefreshCalendarMarks()
        {
            if (TryFindResource("DayMark") is HolidayDateMarkConverter marker)
                marker.Marks = _vm.GetCalendarMarks();

            var style = HolidayCalendar.CalendarDayButtonStyle;
            HolidayCalendar.CalendarDayButtonStyle = null;
            HolidayCalendar.CalendarDayButtonStyle = style;
        }

        private void SyncCalendarYear()
        {
            if (HolidayCalendar.DisplayDate.Year == _vm.SelectedYear) return;
            HolidayCalendar.DisplayDate = _vm.SelectedYear == DateTime.Today.Year
                ? DateTime.Today
                : new DateTime(_vm.SelectedYear, 1, 1);
        }

        private void HolidayCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressCalendarSync || !(HolidayCalendar.SelectedDate is DateTime date)) return;
            if (_vm.SelectByDate(date) && _vm.SelectedRow != null)
                HolidayGrid.ScrollIntoView(_vm.SelectedRow);
        }

        /// <summary>WPF's Calendar keeps mouse capture after a click, so the next click elsewhere is swallowed.</summary>
        private void HolidayCalendar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.Captured is CalendarItem || Mouse.Captured is CalendarDayButton)
                Mouse.Capture(null);
        }

        private void HolidayCalendar_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Only a double-click on a day adds a holiday (not on the month header / arrows).
            if (FindParent<CalendarDayButton>(e.OriginalSource as DependencyObject) == null) return;
            BtnAdd_Click(sender, e);
        }

        // ── Grid ─────────────────────────────────────────────────────────────

        private void HolidayGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(_vm.SelectedRow is HolidayRow row)) return;

            _suppressCalendarSync = true;
            try
            {
                HolidayCalendar.DisplayDate = row.DisplayDate;
                HolidayCalendar.SelectedDate = row.DisplayDate;
            }
            catch { /* outside the calendar's range */ }
            finally { _suppressCalendarSync = false; }
        }

        private void HolidayGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FindParent<DataGridRow>(e.OriginalSource as DependencyObject) != null)
                BtnEdit_Click(sender, e);
        }

        // ── CRUD ─────────────────────────────────────────────────────────────

        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var selected = HolidayCalendar.SelectedDate;
            var defaultDate = selected.HasValue && selected.Value.Year == _vm.SelectedYear
                ? selected.Value
                : new DateTime(_vm.SelectedYear, 1, 1);

            var dlg = new HolidayEditWindow(null, defaultDate, _vm.AllHolidays);
            if (!ShowOwnedDialog(dlg)) return;

            try { await _vm.AddAsync(dlg.Result); }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add holiday:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (!(_vm.SelectedRow is HolidayRow row))
            {
                MessageBox.Show("Please select a holiday to edit.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new HolidayEditWindow(row.Dto, row.Dto.HolidayDate, _vm.AllHolidays);
            if (!ShowOwnedDialog(dlg)) return;

            try { await _vm.UpdateAsync(row.HolidayId, dlg.Result); }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update holiday:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!(_vm.SelectedRow is HolidayRow row))
            {
                MessageBox.Show("Please select a holiday to delete.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Delete \"{row.HolidayName}\" ({row.Dto.HolidayDate:MMM dd, yyyy})?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try { await _vm.DeleteAsync(row.Dto); }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete holiday:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

        /// <summary>
        /// Shows a WPF dialog owned by the WinForms form hosting this page (never Form.ActiveForm;
        /// see CLAUDE.md), so it can't open behind the form it disables.
        /// </summary>
        private bool ShowOwnedDialog(Window dlg)
        {
            IntPtr owner = GetHostFormHandle();
            if (owner != IntPtr.Zero)
                new WindowInteropHelper(dlg).Owner = owner;
            else
                dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            bool ok = dlg.ShowDialog() == true;

            if (Application.Current != null)
                Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            return ok;
        }

        private IntPtr GetHostFormHandle()
        {
            if (!(PresentationSource.FromVisual(this) is HwndSource source))
                return IntPtr.Zero;

            var control = System.Windows.Forms.Control.FromChildHandle(source.Handle);
            var topLevel = control?.TopLevelControl ?? control?.FindForm();
            return topLevel != null && topLevel.IsHandleCreated ? topLevel.Handle : IntPtr.Zero;
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null && !(child is T))
                child = child is Visual || child is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(child)
                    : LogicalTreeHelper.GetParent(child);
            return child as T;
        }
    }

    /// <summary>
    /// Marks holiday dates on the Calendar. Bound per CalendarDayButton (DataContext = its DateTime);
    /// parameter "fg" / "weight" / "bg" picks what to return. Non-holidays get UnsetValue (default look).
    /// </summary>
    public sealed class HolidayDateMarkConverter : IValueConverter
    {
        private static readonly Brush RegularFore = Frozen(Color.FromRgb(0xE7, 0x4C, 0x3C));
        private static readonly Brush SpecialFore = Frozen(Color.FromRgb(0x34, 0x98, 0xDB));
        private static readonly Brush RegularBack = Frozen(Color.FromRgb(0xFD, 0xEC, 0xEA));
        private static readonly Brush SpecialBack = Frozen(Color.FromRgb(0xE8, 0xF3, 0xFC));

        public Dictionary<DateTime, string> Marks { get; set; } = new Dictionary<DateTime, string>();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is DateTime date) || Marks == null || !Marks.TryGetValue(date.Date, out var type))
                return DependencyProperty.UnsetValue;

            bool regular = type == HolidayManagementViewModel.RegularType;
            switch (parameter as string)
            {
                case "fg":     return regular ? RegularFore : SpecialFore;
                case "bg":     return regular ? RegularBack : SpecialBack;
                case "weight": return FontWeights.Bold;
                default:       return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}
