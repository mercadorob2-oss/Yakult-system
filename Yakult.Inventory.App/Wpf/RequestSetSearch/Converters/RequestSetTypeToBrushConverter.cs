using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Yakult.Inventory.App.Wpf.RequestSetSearch.Converters
{
    /// <summary>Card accent color by result type — "Request" (blue) or "Set" (orange).</summary>
    public class RequestSetTypeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value as string == "Set"
                ? new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16))
                : new SolidColorBrush(Color.FromRgb(0x3A, 0x8E, 0xF6));
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => System.Windows.DependencyProperty.UnsetValue;
    }

    /// <summary>Light background variant, used behind the type chip.</summary>
    public class RequestSetTypeToLightBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value as string == "Set"
                ? new SolidColorBrush(Color.FromRgb(0xFD, 0xEC, 0xDF))
                : new SolidColorBrush(Color.FromRgb(0xE8, 0xF3, 0xFF));
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => System.Windows.DependencyProperty.UnsetValue;
    }
}
