using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Yakult.Inventory.App.Wpf.RepairPortal.Reports.ViewModels;

// Disambiguate MessageBox — several namespaces are open across this hybrid WinForms/WPF app.
using WinMsgBox = System.Windows.MessageBox;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Reports.Views
{
    public partial class RepairReportsListWindow : Window
    {
        public RepairReportsListWindow()
        {
            InitializeComponent();

            var vm = new RepairReportsListViewModel();
            // Owner MUST be passed explicitly — a MessageBox shown without one has no place in the
            // Win32 owner chain, and closing it can leave the wrong window activated. In this hybrid
            // WPF-in-WinForms app that has been observed to minimize the whole application (see
            // CLAUDE.md's "WPF Overlay Crash on Close" section) — same class of bug, same fix as
            // ReportViewerForm/RepairSignatoryPickerWindow's owner assignment elsewhere in this module.
            vm.RequestError += (title, message) => WinMsgBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            vm.RequestInfo += (title, message) => WinMsgBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            DataContext = vm;

            // See WindowOwnerChainHelper — leaving this window and a sibling (e.g. Attendance
            // History) open, Alt+Tabbing away and back, then closing one has been observed to
            // minimize the whole app instead of reactivating the Repair Portal shell.
            Closing += (s, e) => Yakult.Inventory.App.Wpf.RepairPortal.Shared.WindowOwnerChainHelper.RestoreAndActivate(this);
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (DataContext is RepairReportsListViewModel vm && vm.SearchCommand.CanExecute(null))
                vm.SearchCommand.Execute(null);
        }

        // Convenience: double-click a row to open just that ticket's report, without needing to
        // check its box and use the top "View Reports" button.
        private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if ((sender as DataGrid)?.SelectedItem is RepairReportRow row
                && DataContext is RepairReportsListViewModel vm
                && vm.ViewReportCommand.CanExecute(row))
            {
                vm.ViewReportCommand.Execute(row);
            }
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is RepairReportsListViewModel vm)) return;
            bool select = SelectAllCheckBox.IsChecked == true;
            foreach (var row in vm.Tickets)
                row.IsSelected = select;
        }
    }
}
