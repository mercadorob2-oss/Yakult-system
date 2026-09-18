using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.WPF.SendNotifications.ViewModels;

namespace Yakult.Inventory.App.WPF.SendNotifications.Views
{
    public partial class SendNotificationsView : UserControl
    {
        private SendNotificationsViewModel _vm;

        public SendNotificationsView()
        {
            InitializeComponent();

            _vm = new SendNotificationsViewModel();
            DataContext = _vm;

            Loaded   += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await _vm.InitializeAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _vm?.Dispose();
        }
    }
}
