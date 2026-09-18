using System;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Views
{
    public partial class ClosedCartridgesView : UserControl
    {
        private readonly ClosedCartridgesViewModel _vm;

        public ClosedCartridgesView(string status)
        {
            InitializeComponent();
            _vm         = new ClosedCartridgesViewModel(status);
            DataContext  = _vm;
            Loaded      += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Column index 4: Closed At (DataGridTextColumn doesn't support x:Name)
            if (MainGrid.Columns.Count > 4)
                MainGrid.Columns[4].Header = _vm.ClosedAtHeader;
            _ = _vm.LoadAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _vm.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing data:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("ClosedCartridgesView.BtnRefresh_Click failed", ex);
            }
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e) => _vm.GoToPrevPage();
        private void BtnNext_Click(object sender, RoutedEventArgs e) => _vm.GoToNextPage();
    }
}
