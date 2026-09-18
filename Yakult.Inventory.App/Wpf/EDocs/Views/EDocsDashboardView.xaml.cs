using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.WPF.EDocs.ViewModels;

namespace Yakult.Inventory.App.WPF.EDocs.Views
{
    public partial class EDocsDashboardView : UserControl
    {
        public EDocsDashboardView()
        {
            InitializeComponent();
            DataContext = new EDocsDashboardViewModel();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            VisualStateManager.GoToState(this, ActualWidth < 700 ? "Narrow" : "Wide", true);
        }
    }
}
