using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Yakult.Inventory.App.Wpf.Search.Converters
{
    public class BoolToVisConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
    }

    public class InverseBoolToVisConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
    }

    public class NullOrEmptyToVisConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
    }

    public class DestinationToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            switch (value as string)
            {
                case "Archive":        return new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
                case "Repaired Items": return new SolidColorBrush(Color.FromRgb(0xC8, 0x5A, 0x20));
                case "Fixed Assets":   return new SolidColorBrush(Color.FromRgb(0x8E, 0x44, 0xAD));
                case "Warranty":       return new SolidColorBrush(Color.FromRgb(0xD4, 0x7E, 0x00));
                case "Items":          return new SolidColorBrush(Color.FromRgb(0x55, 0x6A, 0xBE));
                default:               return new SolidColorBrush(Color.FromRgb(0x3A, 0x8E, 0xF6)); // Inventory
            }
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
    }

    public class DestinationToLightBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            switch (value as string)
            {
                case "Archive":        return new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
                case "Repaired Items": return new SolidColorBrush(Color.FromRgb(0xFF, 0xEF, 0xE8));
                case "Fixed Assets":   return new SolidColorBrush(Color.FromRgb(0xF3, 0xE8, 0xFD));
                case "Warranty":       return new SolidColorBrush(Color.FromRgb(0xFE, 0xF4, 0xDC));
                case "Items":          return new SolidColorBrush(Color.FromRgb(0xEB, 0xEE, 0xF9));
                default:               return new SolidColorBrush(Color.FromRgb(0xE8, 0xF3, 0xFF)); // Inventory
            }
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
    }

    public class StockToCountBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is int stock)
            {
                if (stock == 0) return new SolidColorBrush(Color.FromRgb(0xD3, 0x2F, 0x2F));
                if (stock <= 5) return new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x00));
            }
            return new SolidColorBrush(Color.FromRgb(0x3A, 0x8E, 0xF6));
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
    }
}
