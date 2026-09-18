using System.Collections.Generic;
using System.Windows;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.Renewal.LinkRenewal.ViewModels;

namespace Yakult.Inventory.App.WPF.Renewal.LinkRenewal.Views
{
    public partial class LinkRenewalWindow : Window
    {
        private readonly LinkRenewalViewModel _vm;

        public int?   SelectedSetId   => _vm.ResultSetId;
        public string SelectedSetCode => _vm.ResultSetCode;

        public LinkRenewalWindow(RenewalDto original, List<InvoiceSetPickerDto> candidates)
        {
            InitializeComponent();

            _vm = new LinkRenewalViewModel(original, candidates);
            _vm.CloseRequested += success =>
            {
                DialogResult = success;
                Close();
            };

            DataContext = _vm;
            Title = $"Link Renewal Set — {original.SetCode}";
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await _vm.LoadAsync();
        }
    }
}
