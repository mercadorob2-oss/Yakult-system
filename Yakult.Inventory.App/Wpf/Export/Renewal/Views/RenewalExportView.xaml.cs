using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages.Export;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.Export.Renewal.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Export.Renewal.Views
{
    public partial class RenewalExportView : UserControl
    {
        private readonly RenewalExportViewModel _vm;

        public event Action BackRequested;

        public RenewalExportView()
        {
            InitializeComponent();

            _vm = new RenewalExportViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.RequestExport += OnRequestExport;
            _vm.RequestBack += () => BackRequested?.Invoke();

            _ = _vm.LoadAsync();
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestExport(List<RenewalDto> selected, RenewalRepository repo)
        {
            try
            {
                using (var dlg = new ExportRenewalPreviewDialog(selected, repo))
                    dlg.ShowDialog(GetOwner());
            }
            catch (Exception ex)
            {
                WinForms.MessageBox.Show(GetOwner(), $"Error preparing renewal export:\n{ex.Message}", "Error", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
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

        private void RenewalsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = e.Column.SortMemberPath;
            if (string.IsNullOrEmpty(key)) return;

            var direction = _vm.SortByColumn(key);

            foreach (var col in RenewalsGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = direction;
        }
    }

    /// <summary>Page-local: colors the Status cell text, mirroring the original's
    /// OnCellFormatting switch on SetLevelStatus.</summary>
    public sealed class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((value as string ?? "").ToLowerInvariant())
            {
                case "active":
                case "fully renewed":
                    return new SolidColorBrush(Color.FromRgb(21, 128, 61));
                case "expiring soon":
                case "warning":
                case "partially renewed":
                    return new SolidColorBrush(Color.FromRgb(146, 64, 14));
                case "expired":
                    return new SolidColorBrush(Color.FromRgb(153, 27, 27));
                case "no expiry date":
                case "no items":
                    return new SolidColorBrush(Color.FromRgb(100, 116, 139));
                default:
                    return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
