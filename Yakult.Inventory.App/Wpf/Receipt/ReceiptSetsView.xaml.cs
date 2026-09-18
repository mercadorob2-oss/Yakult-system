using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Wpf.Receipt
{
    /// <summary>Inverts a bool into Visibility (true -> Collapsed, false -> Visible).
    /// Used to show the empty-state placeholder only when there are no rows.</summary>
    public sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool bv && bv;
            return b ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public partial class ReceiptSetsView : UserControl
    {
        private readonly ReceiptSetsViewModel _vm;

        /// <summary>Raised when the user clicks "Go to Manage Sets", mirroring the legacy
        /// WinForms ViewReceiptsPage.GoToManageSetsRequested event.</summary>
        public event Action GoToManageSetsRequested;

        public ReceiptSetsView()
        {
            InitializeComponent();
            _vm = new ReceiptSetsViewModel();
            DataContext = _vm;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ReloadAsync();
        }

        private async System.Threading.Tasks.Task ReloadAsync()
        {
            try
            {
                await _vm.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading receipt sets:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("ReceiptSetsView.ReloadAsync failed", ex);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await ReloadAsync();
        }

        private async void BtnAddReceipt_Click(object sender, RoutedEventArgs e)
        {
            Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForNewReceipt(GetOwnerForm());
            await ReloadAsync();
        }

        private async void BtnOpenViewer_Click(object sender, RoutedEventArgs e)
        {
            var row = _vm.SelectedRow;
            if (row == null)
            {
                MessageBox.Show("Select a receipt row first.", "Open Receipt Viewer",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForReceipt(GetOwnerForm(), row.Dto);
            await ReloadAsync();
        }

        private void BtnGoToManageSets_Click(object sender, RoutedEventArgs e)
        {
            GoToManageSetsRequested?.Invoke();
        }

        private async void MainGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var row = _vm.SelectedRow;
            if (row == null)
                return;

            Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForReceipt(GetOwnerForm(), row.Dto);
            await ReloadAsync();
        }

        private async void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is ReceiptSetRow row))
                return;

            if (_vm.IsReceiptLockedForDelete(row, out var lockReason))
            {
                MessageBox.Show(lockReason, "Delete Disabled", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var supplier = string.IsNullOrWhiteSpace(row.Supplier) ? "(none)" : row.Supplier.Trim();
            var si = string.IsNullOrWhiteSpace(row.SiNumber) ? "(none)" : row.SiNumber.Trim();
            var confirm = MessageBox.Show(
                $"Delete this receipt set?\n\nSupplier: {supplier}\nSI #: {si}\nReceipt #{row.ReceiptSetId}",
                "Delete Receipt Set",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                _vm.DeleteReceipt(row);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to delete receipt set.\n\n" + ex.Message, "Receipt Sets",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private System.Windows.Forms.Form GetOwnerForm()
        {
            return System.Windows.Forms.Form.ActiveForm;
        }
    }
}
