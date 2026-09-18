using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed partial class WpfCallFieldWorkDialog
    {
        private DatePicker _scheduleDatePicker;
        private TextBox _scheduleTimeBox;
        private Border _scheduleModeHintBorder;
        private TextBlock _scheduleModeHint;
        private CheckBox _backdateCheck;
        private StackPanel _completedRow;
        private DatePicker _completedDatePicker;
        private TextBox _completedTimeBox;

        private void UpdateScheduleModeHint(string status)
        {
            if (_scheduleModeHintBorder == null || _scheduleModeHint == null) return;
            var s = (status ?? string.Empty).Trim();
            if (_backdateCheck != null && _backdateCheck.IsChecked == true)
            {
                _scheduleModeHint.Text = "Backdate mode — past dates allowed. Completion records the picked finish time.";
                _scheduleModeHint.Foreground = BrushFromRgb(146, 64, 14);
                _scheduleModeHintBorder.Background = BrushFromRgb(254, 243, 199);
                _scheduleModeHintBorder.BorderBrush = BrushFromRgb(252, 211, 77);
                _scheduleModeHintBorder.Visibility = Visibility.Visible;
            }
            else if (string.IsNullOrEmpty(s))
            {
                _scheduleModeHint.Text = "Schedule mode — pick a technician and date/time, then click Schedule.";
                _scheduleModeHint.Foreground = BrushFromRgb(29, 78, 216);
                _scheduleModeHintBorder.Background = BrushFromRgb(219, 234, 254);
                _scheduleModeHintBorder.BorderBrush = BrushFromRgb(147, 197, 253);
                _scheduleModeHintBorder.Visibility = Visibility.Visible;
            }
            else if (s.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                _scheduleModeHint.Text = "Reschedule mode — pick a new technician and date/time, then click Reschedule below or in the footer.";
                _scheduleModeHint.Foreground = BrushFromRgb(146, 64, 14);
                _scheduleModeHintBorder.Background = BrushFromRgb(254, 243, 199);
                _scheduleModeHintBorder.BorderBrush = BrushFromRgb(252, 211, 77);
                _scheduleModeHintBorder.Visibility = Visibility.Visible;
            }
            else if (s.Equals("Scheduled", StringComparison.OrdinalIgnoreCase))
            {
                _scheduleModeHint.Text = "Scheduled — technician and date are locked. Complete, Cancel, or wait for field work.";
                _scheduleModeHint.Foreground = BrushFromRgb(71, 85, 105);
                _scheduleModeHintBorder.Background = BrushFromRgb(241, 245, 249);
                _scheduleModeHintBorder.BorderBrush = BrushFromRgb(226, 232, 240);
                _scheduleModeHintBorder.Visibility = Visibility.Visible;
            }
            else
            {
                _scheduleModeHintBorder.Visibility = Visibility.Collapsed;
            }
        }

        private Grid BuildScheduleSection()
        {
            var grid = new Grid { Margin = new Thickness(0,12,0,0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var techStack = new StackPanel { Margin = new Thickness(0,0,8,0) };
            techStack.Children.Add(new TextBlock { Text = "Technician", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(100,116,139), Margin = new Thickness(0,0,0,6) });
            // _techCombo is created in main ctor; reuse it
            techStack.Children.Add(_techCombo);
            Grid.SetColumn(techStack, 0); grid.Children.Add(techStack);

            var dateStack = new StackPanel { Margin = new Thickness(8,0,8,0) };
            dateStack.Children.Add(new TextBlock { Text = "Schedule Date", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(100,116,139), Margin = new Thickness(0,0,0,6) });
            _scheduleDatePicker = new DatePicker { SelectedDate = DateTime.Today, DisplayDateStart = DateTime.Today, Height = 36, Background = Brushes.White, BorderBrush = BrushFromRgb(203,213,225), Foreground = BrushFromRgb(15,23,42), FontSize = 13 };
            dateStack.Children.Add(_scheduleDatePicker);
            Grid.SetColumn(dateStack, 1); grid.Children.Add(dateStack);

            var timeStack = new StackPanel { Margin = new Thickness(8,0,0,0) };
            timeStack.Children.Add(new TextBlock { Text = "Time (HH:mm)", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(100,116,139), Margin = new Thickness(0,0,0,6) });
            _scheduleTimeBox = new TextBox { Text = DateTime.Now.ToString("HH:mm"), Height = 36, Padding = new Thickness(8,6,8,6), Background = Brushes.White, BorderBrush = BrushFromRgb(203,213,225), Foreground = BrushFromRgb(15,23,42), FontSize = 13 };
            timeStack.Children.Add(_scheduleTimeBox);
            Grid.SetColumn(timeStack, 2); grid.Children.Add(timeStack);

            // Backdate toggle (row 1): log a visit that already happened.
            _backdateCheck = new CheckBox
            {
                Content = "Log past visit (backdate)",
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(100,116,139),
                Margin = new Thickness(0,10,0,0)
            };
            _backdateCheck.Checked += (_, __) => UpdateBackdateMode();
            _backdateCheck.Unchecked += (_, __) => UpdateBackdateMode();
            Grid.SetRow(_backdateCheck, 1);
            Grid.SetColumnSpan(_backdateCheck, 3);
            grid.Children.Add(_backdateCheck);

            // Completed date/time (row 2): visible only in backdate mode, used
            // when completing a past visit. Silent: no marker is stored.
            _completedRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,8,0,0), Visibility = Visibility.Collapsed };
            var doneDateStack = new StackPanel { Width = 220, Margin = new Thickness(0,0,8,0) };
            doneDateStack.Children.Add(new TextBlock { Text = "Completed Date", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(100,116,139), Margin = new Thickness(0,0,0,6) });
            _completedDatePicker = new DatePicker { SelectedDate = DateTime.Today, Height = 36, Background = Brushes.White, BorderBrush = BrushFromRgb(203,213,225), Foreground = BrushFromRgb(15,23,42), FontSize = 13 };
            doneDateStack.Children.Add(_completedDatePicker);
            var doneTimeStack = new StackPanel { Width = 160 };
            doneTimeStack.Children.Add(new TextBlock { Text = "Time (HH:mm)", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(100,116,139), Margin = new Thickness(0,0,0,6) });
            _completedTimeBox = new TextBox { Text = DateTime.Now.ToString("HH:mm"), Height = 36, Padding = new Thickness(8,6,8,6), Background = Brushes.White, BorderBrush = BrushFromRgb(203,213,225), Foreground = BrushFromRgb(15,23,42), FontSize = 13 };
            doneTimeStack.Children.Add(_completedTimeBox);
            _completedRow.Children.Add(doneDateStack);
            _completedRow.Children.Add(doneTimeStack);
            Grid.SetRow(_completedRow, 2);
            Grid.SetColumnSpan(_completedRow, 3);
            grid.Children.Add(_completedRow);

            return grid;
        }

        private void UpdateBackdateMode()
        {
            var backdate = _backdateCheck != null && _backdateCheck.IsChecked == true;
            if (_scheduleDatePicker != null)
                _scheduleDatePicker.DisplayDateStart = backdate ? (DateTime?)null : DateTime.Today;
            if (_completedRow != null)
                _completedRow.Visibility = backdate ? Visibility.Visible : Visibility.Collapsed;
            UpdateScheduleModeHint(_currentVisit != null ? _currentVisit.Status : null);
        }

        private DateTime? GetScheduledAtOrNull()
        {
            if (_scheduleDatePicker == null || !_scheduleDatePicker.SelectedDate.HasValue)
                throw new InvalidOperationException("Pick a schedule date first.");
            // Strict 24-hour HH:mm: TimeSpan.TryParse would accept "25:00"
            // (as 25 hours) and silently shift the day.
            var text = (_scheduleTimeBox.Text ?? string.Empty).Trim();
            if (!Regex.IsMatch(text, @"^([01]\d|2[0-3]):[0-5]\d$"))
                throw new InvalidOperationException("Time must be HH:mm in 24-hour format (e.g. 09:30).");
            DateTime local;
            try { local = _scheduleDatePicker.SelectedDate.Value.Date + TimeSpan.Parse(text); }
            catch { throw new InvalidOperationException("Time must be HH:mm in 24-hour format (e.g. 09:30)."); }
            var backdate = _backdateCheck != null && _backdateCheck.IsChecked == true;
            if (!backdate && local < DateTime.Today)
                throw new InvalidOperationException("Schedule date cannot be in the past. Check 'Log past visit' to backdate.");
            // Store UTC to match report window (fromUtc/toUtc) + mobile ISO-UTC; display converts back via ToLocalTime.
            try { return DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime(); }
            catch { return local; }
        }

        private DateTime? GetCompletedAtOrNull()
        {
            // Only meaningful in backdate mode; otherwise the server stamps now.
            if (_backdateCheck == null || _backdateCheck.IsChecked != true)
                return null;
            if (_completedDatePicker == null || !_completedDatePicker.SelectedDate.HasValue)
                throw new InvalidOperationException("Pick the completed date for the backdated visit.");
            var text = (_completedTimeBox.Text ?? string.Empty).Trim();
            if (!Regex.IsMatch(text, @"^([01]\d|2[0-3]):[0-5]\d$"))
                throw new InvalidOperationException("Completed time must be HH:mm in 24-hour format (e.g. 09:30).");
            DateTime local;
            try { local = _completedDatePicker.SelectedDate.Value.Date + TimeSpan.Parse(text); }
            catch { throw new InvalidOperationException("Completed time must be HH:mm in 24-hour format (e.g. 09:30)."); }
            if (local > DateTime.Now.AddMinutes(1))
                throw new InvalidOperationException("Completed date cannot be in the future.");
            try { return DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime(); }
            catch { return local; }
        }
    }
}


