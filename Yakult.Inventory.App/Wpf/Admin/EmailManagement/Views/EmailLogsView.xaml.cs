using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs;
using Yakult.Inventory.App.WPF.Admin.EmailManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.EmailManagement.Views
{
    public partial class EmailLogsView : UserControl
    {
        private readonly EmailLogsViewModel _vm;

        public EmailLogsView()
        {
            InitializeComponent();
            _vm = new EmailLogsViewModel();
            DataContext = _vm;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = _vm.LoadAsync();
        }

        private void MainGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;

            string column = e.Column.SortMemberPath;
            if (string.IsNullOrEmpty(column))
                column = e.Column.Header?.ToString();
            if (string.IsNullOrEmpty(column)) return;

            bool ascending = e.Column.SortDirection != System.ComponentModel.ListSortDirection.Ascending;

            foreach (var col in MainGrid.Columns)
                col.SortDirection = null;
            e.Column.SortDirection = ascending
                ? System.ComponentModel.ListSortDirection.Ascending
                : System.ComponentModel.ListSortDirection.Descending;

            // The VM's per-column value lookup is keyed on the visible header text.
            _vm.SetSort(e.Column.Header?.ToString() ?? column, ascending);
        }

        private void MainGrid_HeaderButtonClick(object sender, RoutedEventArgs e)
        {
            if (!(e.OriginalSource is Button btn) || btn.Name != "FilterBtn") return;

            string column = btn.Tag?.ToString();
            if (string.IsNullOrEmpty(column)) return;

            var values = _vm.GetUniqueValuesForColumn(column);
            if (values.Count == 0) return;

            var popup = new ColumnFilterWindow(column, values, _vm.GetColumnFilter(column));
            PositionPopupNearButton(popup, btn);
            SetWpfOwner(popup);
            if (popup.ShowDialog() != true) return;

            if (popup.ClearFilter)
                _vm.ClearColumnFilter(column);
            else
                _vm.SetColumnFilter(column, popup.SelectedValues);
        }

        private static void PositionPopupNearButton(Window popup, Button btn)
        {
            try
            {
                var pt = btn.PointToScreen(new Point(0, btn.ActualHeight));
                var source = PresentationSource.FromVisual(btn);
                if (source?.CompositionTarget != null)
                    pt = source.CompositionTarget.TransformFromDevice.Transform(pt);

                var area = SystemParameters.WorkArea;
                double estH = double.IsNaN(popup.Height) ? popup.MaxHeight : popup.Height;
                popup.Left = Math.Max(area.Left, Math.Min(pt.X, area.Right - popup.Width));
                popup.Top = Math.Max(area.Top, Math.Min(pt.Y, area.Bottom - estH));
            }
            catch { popup.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
        }

        private static void SetWpfOwner(Window window)
        {
            var owner = System.Windows.Forms.Form.ActiveForm;
            if (owner != null) new WindowInteropHelper(window).Owner = owner.Handle;
        }
    }

    /// <summary>Colours the Status cell: Sent green, Failed red, Skipped amber, anything else grey.</summary>
    public sealed class EmailLogStatusToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Sent    = new SolidColorBrush(Color.FromRgb(0x2E, 0x9E, 0x5B));
        private static readonly SolidColorBrush Failed  = new SolidColorBrush(Color.FromRgb(0xDC, 0x35, 0x45));
        private static readonly SolidColorBrush Skipped = new SolidColorBrush(Color.FromRgb(0xE0, 0xA1, 0x06));
        private static readonly SolidColorBrush Other   = new SolidColorBrush(Color.FromRgb(0x7A, 0x8A, 0x9A));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            if (string.Equals(s, "Sent", StringComparison.OrdinalIgnoreCase)) return Sent;
            if (string.Equals(s, "Failed", StringComparison.OrdinalIgnoreCase)) return Failed;
            if (string.Equals(s, "Skipped", StringComparison.OrdinalIgnoreCase)) return Skipped;
            return Other;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
