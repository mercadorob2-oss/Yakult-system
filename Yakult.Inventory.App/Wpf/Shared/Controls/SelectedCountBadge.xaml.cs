using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Yakult.Inventory.App.WPF.Shared.Controls
{
    /// <summary>
    /// Shared "N Selected" badge — blue pill, underlined count, click to review/unselect.
    /// Originally built for ItemsPageView; reused across every list page with a checkbox
    /// selection column (Employee, Vendor, Category, Company, Branch, Department, Fixed
    /// Assets, Consumable Models, Assets).
    ///
    /// Usage:
    ///   &lt;shared:SelectedCountBadge Count="{Binding SelectedCount}" Click="SelectedBadge_Click"
    ///                              EntityLabel="employees"/&gt;
    /// The page code-behind's Click handler is responsible for building the row summaries,
    /// showing SelectedRowsReviewDialog, and applying RemovedIds back to its ViewModel —
    /// this control only owns the visual badge and the click signal.
    /// </summary>
    public partial class SelectedCountBadge : UserControl
    {
        public static readonly DependencyProperty CountProperty =
            DependencyProperty.Register(nameof(Count), typeof(int), typeof(SelectedCountBadge),
                new PropertyMetadata(0, OnCountChanged));

        public int Count
        {
            get => (int)GetValue(CountProperty);
            set => SetValue(CountProperty, value);
        }

        /// <summary>Plural noun shown in the tooltip, e.g. "employees", "vendors". Defaults to "rows".</summary>
        public static readonly DependencyProperty EntityLabelProperty =
            DependencyProperty.Register(nameof(EntityLabel), typeof(string), typeof(SelectedCountBadge),
                new PropertyMetadata("rows", OnEntityLabelChanged));

        public string EntityLabel
        {
            get => (string)GetValue(EntityLabelProperty);
            set => SetValue(EntityLabelProperty, value);
        }

        public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(
            nameof(Click), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SelectedCountBadge));

        public event RoutedEventHandler Click
        {
            add => AddHandler(ClickEvent, value);
            remove => RemoveHandler(ClickEvent, value);
        }

        public SelectedCountBadge()
        {
            InitializeComponent();
        }

        private static void OnCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var badge = (SelectedCountBadge)d;
            int count = (int)e.NewValue;
            badge.RunCount.Text = count.ToString();
            badge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void OnEntityLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var badge = (SelectedCountBadge)d;
            badge.RootToolTip.Content = $"Click to view and unselect {e.NewValue}";
        }

        private void Root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ClickEvent, this));
        }
    }
}
