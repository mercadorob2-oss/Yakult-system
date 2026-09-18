using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.WPF.ConsumableManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.ConsumableManagement.Views
{
    /// <summary>
    /// Admin Portal · Consumable Stock Monitor. Polls per-item stock for Ink / Toner /
    /// Printhead / Cartridge catalog items on a timer and shows a colour-coded live feed
    /// of every quantity change. Starts polling on load, stops on unload.
    /// </summary>
    public partial class ConsumableStockMonitorView : UserControl
    {
        private readonly ConsumableStockMonitorViewModel _vm;
        private bool _started;

        public ConsumableStockMonitorView()
        {
            InitializeComponent();
            _vm = new ConsumableStockMonitorViewModel();
            DataContext = _vm;

            Loaded   += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_started)
            {
                _vm.Resume();
                return;
            }
            _started = true;
            await _vm.StartAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _vm.Stop();
        }
    }
}
