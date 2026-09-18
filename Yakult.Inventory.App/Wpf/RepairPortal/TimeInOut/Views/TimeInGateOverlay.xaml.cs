using System.Windows;
using System.Windows.Controls;

namespace Yakult.Inventory.App.Wpf.RepairPortal.TimeInOut.Views
{
    /// <summary>
    /// Centered "Time In" card shown as the last child of the Shell's root Grid so it paints on
    /// top. Its own Visibility is bound from the parent (RepairPortalShellWindow.xaml) to
    /// !IsTimedIn — kept out of this file to avoid a StaticResource-before-Resources-populated
    /// ordering pitfall on the root element.
    /// </summary>
    public partial class TimeInGateOverlay : UserControl
    {
        public TimeInGateOverlay()
        {
            InitializeComponent();
        }

        private void BackToPortalFloatingButton_Click(object sender, RoutedEventArgs e)
        {
            // The gate overlay sits on top of the (disabled) workspace before Time In, so this
            // is the only reachable "leave the portal" action pre-Time-In. Closing the window
            // triggers the same Closed event MainForm.cs awaits to return to the portal selector.
            Window.GetWindow(this)?.Close();
        }
    }
}
