using System;
using System.Windows;
using Yakult.Inventory.App.WPF.Renewal.ChainReceipts.Views;
using Yakult.Inventory.App.WPF.Renewal.RenewalWorkspace.ViewModels;
using Yakult.Inventory.App.WPF.Renewal.RenewItems.Views;
using Yakult.Inventory.App.Wpf.Receipt;
using Yakult.Inventory.App.WPF.Shared.Views;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalWorkspace.Views
{
    public partial class RenewalWorkspaceWindow : Window
    {
        private RenewalWorkspaceViewModel _vm;

        public RenewalWorkspaceWindow(int setId)
        {
            InitializeComponent();
            AttachViewModel(new RenewalWorkspaceViewModel(setId));
            Loaded += async (_, __) => await _vm.LoadAsync();
        }

        public RenewalWorkspaceWindow(int itemId, bool isItemIdConstructor)
        {
            InitializeComponent();
            AttachViewModel(new RenewalWorkspaceViewModel(itemId, isItemIdConstructor));
            Loaded += async (_, __) => await _vm.LoadAsync();
        }

        /// <summary>
        /// Preserved from the WinForms page — invoked after a first-time renewal is created
        /// from the Warranty page's first-time-renewal flow.
        /// </summary>
        public Action OnRenewalCreated
        {
            get => _vm.OnRenewalCreated;
            set => _vm.OnRenewalCreated = value;
        }

        private void AttachViewModel(RenewalWorkspaceViewModel vm)
        {
            _vm = vm;

            // Wire commands/events BEFORE assigning DataContext. On a window that's already
            // visible (chain re-navigation reusing this window), WPF re-evaluates bindings the
            // instant DataContext changes — if the ICommand properties are still null at that
            // point, every button's Command binding captures null and the buttons go dead.
            _vm.WireCommands();
            _vm.CloseRequested         += (_, __) => Close();
            _vm.NavigateToSetRequested += OnNavigateToSet;
            _vm.RequestRenewItems      += OnRequestRenewItems;
            _vm.RequestAttachReceipt   += OnRequestAttachReceipt;
            _vm.RequestViewChainReceipts += OnRequestViewChainReceipts;
            _vm.RequestInfo            += msg => MessageBox.Show(this, msg, "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            _vm.RequestWarning         += msg => MessageBox.Show(this, msg, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            _vm.RequestError           += msg => MessageBox.Show(this, msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _vm.ConfirmYesNo           = (title, message) =>
                MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            _vm.RequestPrompt          = (text, caption, defaultValue) => InputPromptWindow.Show(this, text, caption, defaultValue);

            DataContext = _vm;
        }

        private async void OnNavigateToSet(object sender, int targetSetId)
        {
            // Reuse this same window instead of opening a new one per chain hop — otherwise
            // clicking through a long chain leaves a trail of open windows behind.
            AttachViewModel(new RenewalWorkspaceViewModel(targetSetId));
            await _vm.LoadAsync();
        }

        private async void OnRequestRenewItems(RenewItemsRequest req)
        {
            var dlg = new RenewItemsWindow(
                req.SetId, req.Title, req.Items, req.Catalog,
                req.BaseSubtotal, req.BaseVatPct, req.BaseWhtPct, req.BaseDiscountPct, req.OriginalTotal,
                req.SetCode)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
                await _vm.ReloadAfterRenewItemsAsync();
        }

        private void OnRequestAttachReceipt(int setId)
        {
            try
            {
                var dlg = new ReceiptSetViewerWindow(setId) { Owner = this };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to open receipt set viewer: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnRequestViewChainReceipts(int setId)
        {
            try
            {
                var dlg = new ChainReceiptsWindow(setId) { Owner = this };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to open chain receipts: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
