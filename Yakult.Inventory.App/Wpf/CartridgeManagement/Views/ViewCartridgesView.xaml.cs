using System;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Views
{
    public partial class ViewCartridgesView : UserControl
    {
        private readonly ViewCartridgesViewModel _vm;

        public ViewCartridgesView()
        {
            InitializeComponent();
            _vm         = new ViewCartridgesViewModel();
            DataContext  = _vm;
            Loaded      += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
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
                Logger.LogError("ViewCartridgesView.BtnRefresh_Click failed", ex);
            }
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e) => _vm.GoToPrevPage();
        private void BtnNext_Click(object sender, RoutedEventArgs e) => _vm.GoToNextPage();
    }
}
