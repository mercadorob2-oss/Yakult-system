using System.Windows;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Wpf.RepairPortal.Attendance.ViewModels;

// Disambiguate MessageBox — several namespaces are open across this hybrid WinForms/WPF app.
using WinMsgBox = System.Windows.MessageBox;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Attendance.Views
{
    public partial class RepairAttendanceHistoryWindow : Window
    {
        public RepairAttendanceHistoryWindow(IRepairTicketRepository repository)
        {
            InitializeComponent();

            var vm = new RepairAttendanceHistoryViewModel(repository);
            // Owner MUST be passed explicitly — see RepairReportsListWindow's identical note on
            // this hybrid WPF-in-WinForms app's "WPF Overlay Crash on Close" class of bug.
            vm.RequestError += (title, message) => WinMsgBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            DataContext = vm;

            // See WindowOwnerChainHelper — leaving this window and a sibling (e.g. Repair Reports)
            // open, Alt+Tabbing away and back, then closing one has been observed to minimize the
            // whole app instead of reactivating the Repair Portal shell.
            Closing += (s, e) => Yakult.Inventory.App.Wpf.RepairPortal.Shared.WindowOwnerChainHelper.RestoreAndActivate(this);
        }
    }
}
