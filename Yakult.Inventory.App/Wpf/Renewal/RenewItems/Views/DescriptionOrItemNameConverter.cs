using System;
using System.Globalization;
using System.Windows.Data;

namespace Yakult.Inventory.App.WPF.Renewal.RenewItems.Views
{
    /// <summary>
    /// Displays RenewItemRow.Description, falling back to ItemName when Description is blank.
    /// Used as a two-way MultiBinding on a plain DataGridTextColumn instead of a
    /// DataGridTemplateColumn, because WPF's DataGrid does not reliably give keyboard focus to a
    /// DataGridTemplateColumn's CellEditingTemplate control when a cell enters edit mode — typing
    /// appears to do nothing. A plain DataGridTextColumn's built-in editing TextBox does not have
    /// this problem.
    /// </summary>
    public sealed class DescriptionOrItemNameConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string description = values.Length > 0 ? values[0] as string : null;
            string itemName = values.Length > 1 ? values[1] as string : null;
            return string.IsNullOrWhiteSpace(description) ? itemName : description;
        }

        // Whatever the user types goes straight back into Description; ItemName (the second
        // source binding) is one-way and never written back.
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { value, Binding.DoNothing };
        }
    }
}
