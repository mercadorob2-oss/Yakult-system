using System;
using System.Windows;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Wpf.Receipt;
using Yakult.Inventory.App.WPF.Renewal.ChainReceipts.ViewModels;

namespace Yakult.Inventory.App.WPF.Renewal.ChainReceipts.Views
{
    public partial class ChainReceiptsWindow : Window
    {
        private readonly ChainReceiptsViewModel _vm;

        public ChainReceiptsWindow(int anySetId)
        {
            InitializeComponent();

            _vm = new ChainReceiptsViewModel(anySetId);
            _vm.CloseRequested   += Close;
            _vm.RequestViewReceipt += OnRequestViewReceipt;
            _vm.RequestError     += msg => MessageBox.Show(this, msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            DataContext = _vm;

            Loaded += async (_, __) => await _vm.LoadAsync();
        }

        private void OnRequestViewReceipt(ReceiptSetDto dto)
        {
            try
            {
                var dlg = new ReceiptSetViewerWindow(dto) { Owner = this };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to open receipt: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
