using System.Collections;
using System.Windows;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Detail.Views
{
    /// <summary>Full timeline dialog opened from RepairTimelineControl's "See Full Timeline"
    /// button. Offers the same data in two forms: Graph (the original dot/connector list) and
    /// Table (a sortable grid) — plain code-behind since there's no state here beyond which view
    /// is currently showing.</summary>
    public partial class TimelineViewerWindow : Window
    {
        public TimelineViewerWindow(IEnumerable items, string title)
        {
            InitializeComponent();
            TitleText.Text = title;
            GraphList.ItemsSource = items;
            TableGrid.ItemsSource = items;
        }

        private void GraphToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (GraphPanel == null || TablePanel == null) return;
            GraphPanel.Visibility = Visibility.Visible;
            TablePanel.Visibility = Visibility.Collapsed;
        }

        private void TableToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (GraphPanel == null || TablePanel == null) return;
            GraphPanel.Visibility = Visibility.Collapsed;
            TablePanel.Visibility = Visibility.Visible;
        }
    }
}
