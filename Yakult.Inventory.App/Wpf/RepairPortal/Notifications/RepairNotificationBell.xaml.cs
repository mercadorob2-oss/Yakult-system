using System.Windows.Controls;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Notifications
{
    /// <summary>
    /// Header notification bell for the Repair Technician Portal shell. Self-contained: owns its
    /// own <see cref="RepairNotificationBellViewModel"/>. The shell wires
    /// <see cref="RepairNotificationBellViewModel.RequestOpenTicket"/> to open a ticket's detail
    /// window and calls <see cref="RepairNotificationBellViewModel.OnNewNotifications"/> from the
    /// RepairPortalNotificationPoller callback.
    /// </summary>
    public partial class RepairNotificationBell : UserControl
    {
        public RepairNotificationBellViewModel ViewModel { get; }

        public RepairNotificationBell()
        {
            InitializeComponent();
            ViewModel = new RepairNotificationBellViewModel();
            DataContext = ViewModel;
        }
    }
}
