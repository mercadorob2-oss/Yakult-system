using System.Windows;
using Yakult.Inventory.App.WPF.Renewal.RenewalDetail.ViewModels;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalDetail.Views
{
    public partial class RenewalDetailWindow : Window
    {
        private readonly RenewalDetailViewModel _vm;

        public RenewalDetailWindow(int setId)
        {
            InitializeComponent();
            _vm = new RenewalDetailViewModel(setId);
            DataContext = _vm;

            _vm.CloseRequested         += (_, __) => Close();
            _vm.NavigateToSetRequested += OnNavigateToSet;
            _vm.RequestInfo  += (title, msg) => MessageBox.Show(this, msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
            _vm.RequestError += (title, msg) => MessageBox.Show(this, msg, title, MessageBoxButton.OK, MessageBoxImage.Error);

            Loaded += async (_, __) => await _vm.LoadAsync();
        }

        private void OnNavigateToSet(object sender, int targetSetId)
        {
            var next = new RenewalDetailWindow(targetSetId);
            next.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            next.Show();
            Close();
        }
    }
}
