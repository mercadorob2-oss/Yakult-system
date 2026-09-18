using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.WPF.CartridgeManagement.Dialogs;
using Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Views
{
    public partial class DamagedEmptyCartridgesView : UserControl
    {
        private DamagedEmptyCartridgesViewModel _vm;

        public DamagedEmptyCartridgesView()
        {
            InitializeComponent();
            _vm         = new DamagedEmptyCartridgesViewModel();
            DataContext = _vm;
            Loaded     += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
            => _ = _vm.LoadAsync();

        private void SetDialogOwner(Window dialog)
        {
            var wpfOwner = Window.GetWindow(this);
            if (wpfOwner != null)
            {
                dialog.Owner = wpfOwner;
            }
            else
            {
                var helper = new WindowInteropHelper(dialog);
                var wfForm = System.Windows.Forms.Form.ActiveForm;
                if (wfForm != null)
                    helper.Owner = wfForm.Handle;
            }
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
                Logger.LogError("DamagedEmptyCartridgesView.BtnRefresh_Click failed", ex);
            }
        }

        private async void BtnAssignOutbound_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OutboundBatchAssignmentWpfDialog(damagedOnly: true);
                SetDialogOwner(dialog);
                if (dialog.ShowDialog() == true)
                    await _vm.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("DamagedEmptyCartridgesView.BtnAssignOutbound_Click failed", ex);
            }
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e) => _vm.GoToPrevPage();
        private void BtnNext_Click(object sender, RoutedEventArgs e) => _vm.GoToNextPage();
    }
}
