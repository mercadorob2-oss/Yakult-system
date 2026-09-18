using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Export;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.Export.Sets.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Export.Sets.Views
{
    public partial class SetsExportView : UserControl
    {
        private readonly SetsExportViewModel _vm;

        public event Action BackRequested;

        public SetsExportView()
        {
            InitializeComponent();

            _vm = new SetsExportViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestExport += OnRequestExport;
            _vm.RequestBack += () => BackRequested?.Invoke();

            _ = _vm.LoadAsync();
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestExport(List<SetDto> selected, SetRepository repo)
        {
            using (var preview = new ExportSetsPreviewDialog(selected, repo))
                preview.ShowDialog(GetOwner());
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool newState = ((CheckBox)sender).IsChecked == true;
            _vm.SetAllFilteredSelection(newState);
        }

        private void RowCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _vm.NotifySelectionChanged();
        }

        private void SetsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = e.Column.SortMemberPath;
            if (string.IsNullOrEmpty(key)) return;

            var direction = _vm.SortByColumn(key);

            foreach (var col in SetsGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = direction;
        }
    }

    /// <summary>Page-local: colors the Status cell text, mirroring the original's
    /// OnCellFormatting switch on the Status column.</summary>
    public sealed class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((value as string ?? "").ToLowerInvariant())
            {
                case "dispatched":
                case "completed":
                case "active":
                    return new SolidColorBrush(Color.FromRgb(21, 128, 61));
                case "pending":
                case "for approval":
                case "processing":
                case "submitted":
                    return new SolidColorBrush(Color.FromRgb(146, 64, 14));
                case "cancelled":
                case "rejected":
                    return new SolidColorBrush(Color.FromRgb(153, 27, 27));
                case "expired":
                case "inactive":
                    return new SolidColorBrush(Color.FromRgb(100, 116, 139));
                default:
                    return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Page-local: mirrors `dto.CurrentCompanyName ?? dto.Company ?? ""`.</summary>
    public sealed class FirstNonEmptyConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => values?.FirstOrDefault(v => !string.IsNullOrEmpty(v as string)) as string ?? "";

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
