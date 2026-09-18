using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.WPF.RequestPortal.AuthorizationHistory.ViewModels;

namespace Yakult.Inventory.App.WPF.RequestPortal.AuthorizationHistory.Views
{
    public partial class AuthorizationHistoryView : UserControl
    {
        public AuthorizationHistoryViewModel ViewModel { get; }

        public AuthorizationHistoryView(AuthorizationHistoryViewModel vm)
        {
            InitializeComponent();
            ViewModel   = vm;
            DataContext = vm;
            Loaded     += async (s, e) => await vm.LoadAsync();
        }

        public AuthorizationHistoryView() : this(new AuthorizationHistoryViewModel()) { }

        private void AuthGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // The DataGrid keeps SelectedItem set to the last-clicked row even when the click
            // that just happened landed on the empty space below the rows (WPF doesn't clear
            // selection there) — so without this check, clicking that empty space re-opened the
            // details modal for whatever row was previously selected. Only react when the click
            // actually hit a row.
            if (!IsClickOnRow(e.OriginalSource)) return;

            if (AuthGrid.SelectedItem is AuthHistoryRowViewModel row)
                ViewModel.OnRowClicked(row);
        }

        private void SignedGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsClickOnRow(e.OriginalSource)) return;

            if (SignedGrid.SelectedItem is SignedRowViewModel row)
                ViewModel.OnSignedRowClicked(row);
        }

        private static bool IsClickOnRow(object originalSource)
        {
            var element = originalSource as DependencyObject;
            while (element != null)
            {
                if (element is DataGridRow) return true;
                element = VisualTreeHelper.GetParent(element);
            }
            return false;
        }

        // ── Tour targets — My History tab ─────────────────────────────────────
        public FrameworkElement TourTarget_AuthHeaderBar     => AuthHeaderBar;
        public FrameworkElement TourTarget_AuthGrid          => AuthGrid;
        public FrameworkElement TourTarget_AuthColumnHeaders => AuthColumnHeaderZone;
        public FrameworkElement TourTarget_AuthDemoRows      => AuthDemoRowsZone;
        public FrameworkElement TourTarget_AuthStatusColumn  => AuthStatusZone;

        // ── Tour targets — Approvals I've Signed tab ──────────────────────────
        public FrameworkElement TourTarget_SignedGrid          => SignedGrid;
        public FrameworkElement TourTarget_SignedColumnHeaders => SignedColumnHeaderZone;
        public FrameworkElement TourTarget_SignedDemoRows      => SignedDemoRowsZone;
        public FrameworkElement TourTarget_SignedStatusColumn  => SignedStatusZone;
    }
}
