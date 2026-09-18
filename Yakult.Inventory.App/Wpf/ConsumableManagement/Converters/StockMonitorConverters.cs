using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Yakult.Inventory.App.WPF.ConsumableManagement.Converters
{
    /// <summary>
    /// Shared colour palette for the Consumable Stock Monitor's bucket colour-coding.
    /// Cartridge = purple, Ink = blue, Toner = amber, Printhead = teal, Other = grey.
    /// </summary>
    internal static class BucketPalette
    {
        public static SolidColorBrush Accent(string bucket)
        {
            switch (bucket)
            {
                case "Cartridge": return Frozen(0x8E, 0x5E, 0xDB);
                case "Ink":       return Frozen(0x2E, 0x86, 0xDE);
                case "Toner":     return Frozen(0xE6, 0x7E, 0x22);
                case "Printhead": return Frozen(0x16, 0xA0, 0x85);
                default:          return Frozen(0x7A, 0x8A, 0x9A);
            }
        }

        public static SolidColorBrush Tint(string bucket)
        {
            switch (bucket)
            {
                case "Cartridge": return Frozen(0xF3, 0xED, 0xFB);
                case "Ink":       return Frozen(0xE8, 0xF2, 0xFC);
                case "Toner":     return Frozen(0xFD, 0xF1, 0xE7);
                case "Printhead": return Frozen(0xE7, 0xF7, 0xF3);
                default:          return Frozen(0xEE, 0xF1, 0xF4);
            }
        }

        private static SolidColorBrush Frozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }

    /// <summary>Bucket name → accent brush (for text, borders, chip headers).</summary>
    public class BucketToAccentBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => BucketPalette.Accent(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Bucket name → pale tint brush (for chip / cell backgrounds).</summary>
    public class BucketToTintBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => BucketPalette.Tint(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Signed int delta → green (increase) / red (decrease) / grey (no change).</summary>
    public class DeltaToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Up   = Freeze(0x1E, 0x9E, 0x54);
        private static readonly SolidColorBrush Down = Freeze(0xD0, 0x3A, 0x3A);
        private static readonly SolidColorBrush Flat = Freeze(0x7A, 0x8A, 0x9A);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int d = ToInt(value);
            return d > 0 ? Up : d < 0 ? Down : Flat;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static int ToInt(object v)
        {
            try { return System.Convert.ToInt32(v); } catch { return 0; }
        }

        private static SolidColorBrush Freeze(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }

    /// <summary>Signed int delta → "▲" / "▼" / "•" glyph.</summary>
    public class DeltaToGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int d;
            try { d = System.Convert.ToInt32(value); } catch { d = 0; }
            return d > 0 ? "▲" : d < 0 ? "▼" : "•";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Signed int delta → "+3" / "-2" / "0".</summary>
    public class SignedNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int d;
            try { d = System.Convert.ToInt32(value); } catch { d = 0; }
            return d > 0 ? "+" + d : d.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
