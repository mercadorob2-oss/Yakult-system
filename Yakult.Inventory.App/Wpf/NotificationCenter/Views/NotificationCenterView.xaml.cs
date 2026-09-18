using System.Windows.Controls;
using Yakult.Inventory.App.WPF.NotificationCenter.ViewModels;

namespace Yakult.Inventory.App.WPF.NotificationCenter.Views
{
    public partial class NotificationCenterView : UserControl
    {
        public NotificationCenterViewModel ViewModel { get; }

        public NotificationCenterView()
        {
            InitializeComponent();
            ViewModel   = new NotificationCenterViewModel();
            DataContext = ViewModel;
        }
    }
}
