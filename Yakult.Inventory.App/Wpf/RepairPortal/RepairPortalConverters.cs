using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Yakult.Inventory.App.Wpf.RepairPortal
{
    /// <summary>For a row of "current status" selector buttons (Ticket Repair Actions, Part
    /// Change Status): highlights whichever button's ConverterParameter matches the bound current
    /// Status value (accent blue background / white text), leaves the rest looking like normal
    /// secondary buttons (#EEF2F7 / #334155) — instead of one button being hardcoded as visually
    /// "selected" regardless of the actual current status.</summary>
    public sealed class PartStatusMatchToBackgroundConverter : IValueConverter
    {
        private static readonly Brush Selected = new SolidColorBrush(Color.FromRgb(37, 99, 235));
        private static readonly Brush Unselected = new SolidColorBrush(Color.FromRgb(238, 242, 247));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.Equals(value as string, parameter as string, StringComparison.OrdinalIgnoreCase) ? Selected : Unselected;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public sealed class PartStatusMatchToForegroundConverter : IValueConverter
    {
        private static readonly Brush Selected = Brushes.White;
        private static readonly Brush Unselected = new SolidColorBrush(Color.FromRgb(51, 65, 85));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.Equals(value as string, parameter as string, StringComparison.OrdinalIgnoreCase) ? Selected : Unselected;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Status → accent brush. Waiting=slate, Diagnosing=blue, Repairing=amber,
    /// AwaitingParts=purple, Testing=teal, Completed=green, Unrepairable=red.</summary>
    public sealed class RepairStatusToBrushConverter : IValueConverter
    {
        private static readonly Brush Waiting = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        private static readonly Brush Diagnosing = new SolidColorBrush(Color.FromRgb(37, 99, 235));
        private static readonly Brush Repairing = new SolidColorBrush(Color.FromRgb(217, 119, 6));
        private static readonly Brush AwaitingParts = new SolidColorBrush(Color.FromRgb(147, 51, 234));
        private static readonly Brush Testing = new SolidColorBrush(Color.FromRgb(13, 148, 136));
        private static readonly Brush Completed = new SolidColorBrush(Color.FromRgb(22, 163, 74));
        private static readonly Brush Unrepairable = new SolidColorBrush(Color.FromRgb(220, 38, 38));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((value as string ?? string.Empty).Trim())
            {
                case "Waiting": return Waiting;
                case "Diagnosing": return Diagnosing;
                case "Repairing": return Repairing;
                case "AwaitingParts": return AwaitingParts;
                case "Testing": return Testing;
                case "Completed": return Completed;
                case "Unrepairable": return Unrepairable;
                default: return Waiting;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Part Status → accent brush (7-value Part status domain — distinct from the
    /// Ticket's RepairStatusToBrushConverter). WaitingDiagnosis=slate, Diagnosing=blue,
    /// Repairing=amber, WaitingParts=purple, Testing=teal, Repaired=green, CannotRepair=red —
    /// same palette as RepairStatusToBrushConverter, remapped to Part's value strings.</summary>
    public sealed class RepairPartStatusToBrushConverter : IValueConverter
    {
        private static readonly Brush WaitingDiagnosis = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        private static readonly Brush Diagnosing = new SolidColorBrush(Color.FromRgb(37, 99, 235));
        private static readonly Brush Repairing = new SolidColorBrush(Color.FromRgb(217, 119, 6));
        private static readonly Brush WaitingParts = new SolidColorBrush(Color.FromRgb(147, 51, 234));
        private static readonly Brush Testing = new SolidColorBrush(Color.FromRgb(13, 148, 136));
        private static readonly Brush Repaired = new SolidColorBrush(Color.FromRgb(22, 163, 74));
        private static readonly Brush CannotRepair = new SolidColorBrush(Color.FromRgb(220, 38, 38));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((value as string ?? string.Empty).Trim())
            {
                case "WaitingDiagnosis": return WaitingDiagnosis;
                case "Diagnosing": return Diagnosing;
                case "Repairing": return Repairing;
                case "WaitingParts": return WaitingParts;
                case "Testing": return Testing;
                case "Repaired": return Repaired;
                case "CannotRepair": return CannotRepair;
                default: return WaitingDiagnosis;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Priority → accent brush. Low=gray, Medium=blue, High=orange, Critical=red.</summary>
    public sealed class RepairPriorityToBrushConverter : IValueConverter
    {
        private static readonly Brush Low = new SolidColorBrush(Color.FromRgb(107, 114, 128));
        private static readonly Brush Medium = new SolidColorBrush(Color.FromRgb(37, 99, 235));
        private static readonly Brush High = new SolidColorBrush(Color.FromRgb(234, 88, 12));
        private static readonly Brush Critical = new SolidColorBrush(Color.FromRgb(220, 38, 38));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((value as string ?? string.Empty).Trim())
            {
                case "Low": return Low;
                case "Medium": return Medium;
                case "High": return High;
                case "Critical": return Critical;
                default: return Medium;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public sealed class BoolToVisibilityInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Plain bool negation — for IsEnabled bindings where BoolToVisibilityInverseConverter
    /// (which returns a Visibility, not a bool) doesn't fit.</summary>
    public sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => !(value is bool b && b);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => !(value is bool b && b);
    }

    /// <summary>Drives the fishbone timeline's alternating above/below branches — bound to
    /// (ItemsControl.AlternationIndex) with ConverterParameter "Top" or "Bottom": even-indexed
    /// entries branch above the spine, odd-indexed ones branch below.</summary>
    public sealed class AlternationIndexToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is int index)) return Visibility.Collapsed;
            var wantTop = string.Equals(parameter as string, "Top", StringComparison.OrdinalIgnoreCase);
            var isEven = index % 2 == 0;
            return (wantTop == isEven) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Used for quick-filter chip IsChecked bindings: compares the bound string to
    /// ConverterParameter for equality (case-insensitive).</summary>
    public sealed class StringEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.Equals(value as string, parameter as string, StringComparison.OrdinalIgnoreCase);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value == null ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public sealed class InverseNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value == null ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public sealed class BytesToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var bytes = value as byte[];
            if (bytes == null || bytes.Length == 0) return null;

            try
            {
                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(bytes))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                }
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Visible when a bound int count is 0 (used for "empty state" hint text next to a
    /// list bound elsewhere to the same collection's Count).</summary>
    public sealed class ZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is int i && i == 0) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public sealed class EmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
