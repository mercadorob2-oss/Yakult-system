using System;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Dialogs;
using Yakult.Inventory.App.WPF.CartridgeManagement.Services;
using Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Views
{
    public partial class FulfilledCartridgesView : UserControl
    {
        private readonly FulfilledCartridgesViewModel _vm;

        public FulfilledCartridgesView()
        {
            InitializeComponent();
            _vm             = new FulfilledCartridgesViewModel();
            _vm.ReprintRequested = OnReprintRequested;
            DataContext     = _vm;
            Loaded         += OnLoaded;
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
                Logger.LogError("FulfilledCartridgesView.BtnRefresh_Click failed", ex);
            }
        }

        private void BtnReprint_Click(object sender, RoutedEventArgs e)
        {
            _vm.Reprint();
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var row = _vm.SelectedRow;
            if (row == null) return;

            try
            {
                var repo   = new CartridgeManagementRepository();
                var detail = repo.GetFulfilledCartridgeDetail(row.SetId);
                if (detail == null)
                {
                    MessageBox.Show("This fulfilled request could no longer be found.", "Not Found",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new FulfilledCartridgeDetailDialog(detail);
                var helper = new System.Windows.Interop.WindowInteropHelper(dialog);
                var wfForm = System.Windows.Forms.Form.ActiveForm;
                if (wfForm != null)
                    helper.Owner = wfForm.Handle;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening fulfilled request detail:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("FulfilledCartridgesView.BtnOpen_Click failed", ex);
            }
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e) => _vm.GoToPrevPage();
        private void BtnNext_Click(object sender, RoutedEventArgs e) => _vm.GoToNextPage();

        private void OnReprintRequested(FulfilledCartridgeRowDto row)
        {
            try
            {
                var vm = CartridgeTransmittalViewModel.FromHistory(row);
                TransmittalPrintService.ShowPrintDialog(vm);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reprinting transmittal:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("FulfilledCartridgesView.OnReprintRequested failed", ex);
            }
        }
    }
}
