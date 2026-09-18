using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Yakult.Inventory.App.WPF.Set.RequisitionForm.Converters
{
    // Multiplies a base double (passed as ConverterParameter) by the bound FontScale value.
    // Used to shrink FontSize/MinHeight values uniformly when a requisition form has too
    // many line items to fit its half of the page at full size.
    public class ScaleDoubleConverter : IValueConverter
    {
        // x:Static instance — resolving this via {StaticResource} on the root element's
        // own FontSize attribute fails because UserControl.Resources isn't attached to
        // the tree yet at that point in parsing. A static instance sidesteps the lookup.
        public static readonly ScaleDoubleConverter Instance = new ScaleDoubleConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double scale = value is double d ? d : 1.0;
            double baseValue = parameter == null ? 0 : double.Parse((string)parameter, CultureInfo.InvariantCulture);
            return baseValue * scale;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // Same idea as ScaleDoubleConverter but for Thickness (Margin/Padding), parameterized as
    // "left,top,right,bottom" (or the WPF shorthand "uniform" / "horizontal,vertical").
    public class ScaleThicknessConverter : IValueConverter
    {
        public static readonly ScaleThicknessConverter Instance = new ScaleThicknessConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double scale = value is double d ? d : 1.0;
            if (parameter == null) return new Thickness(0);
            var parts = ((string)parameter).Split(',');
            double[] n = Array.ConvertAll(parts, p => double.Parse(p, CultureInfo.InvariantCulture) * scale);

            switch (n.Length)
            {
                case 1: return new Thickness(n[0]);
                case 2: return new Thickness(n[0], n[1], n[0], n[1]);
                case 4: return new Thickness(n[0], n[1], n[2], n[3]);
                default: throw new FormatException("Thickness parameter must have 1, 2, or 4 components.");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
