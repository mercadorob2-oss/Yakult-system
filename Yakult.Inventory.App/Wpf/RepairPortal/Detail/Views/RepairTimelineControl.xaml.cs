using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Detail.Views
{
    /// <summary>Compact horizontal timeline preview (timestamp/technician/description) driven by
    /// RepairTicketHistoryItem/RepairPartHistoryItem rows, with a "See Full Timeline" button that
    /// opens the full Graph/Table view in TimelineViewerWindow. Bound explicitly via ItemsSource
    /// so it's reusable for both the ticket-wide and per-part timelines.</summary>
    public partial class RepairTimelineControl : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource), typeof(IEnumerable), typeof(RepairTimelineControl),
            new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(RepairTimelineControl), new PropertyMetadata("Timeline"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty HasItemsProperty = DependencyProperty.Register(
            nameof(HasItems), typeof(bool), typeof(RepairTimelineControl), new PropertyMetadata(false));

        public bool HasItems
        {
            get => (bool)GetValue(HasItemsProperty);
            private set => SetValue(HasItemsProperty, value);
        }

        public RepairTimelineControl()
        {
            InitializeComponent();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (RepairTimelineControl)d;

            if (e.OldValue is INotifyCollectionChanged oldIncc)
                oldIncc.CollectionChanged -= control.OnCollectionChanged;
            if (e.NewValue is INotifyCollectionChanged newIncc)
                newIncc.CollectionChanged += control.OnCollectionChanged;

            control.UpdateHasItems();
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => UpdateHasItems();

        private void UpdateHasItems()
        {
            var any = false;
            if (ItemsSource != null)
            {
                foreach (var _ in ItemsSource) { any = true; break; }
            }
            HasItems = any;
        }

        // This ScrollViewer only ever scrolls horizontally (VerticalScrollBarVisibility="Disabled")
        // — but WPF's ScrollViewer always marks MouseWheel as handled regardless of orientation, so
        // hovering the timeline strip and scrolling silently ate every wheel tick instead of letting
        // it bubble up to scroll the rest of the Detail window. Re-raise the event on the visual
        // parent so it continues bubbling normally.
        private void TimelineScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;
            e.Handled = true;

            var parent = ((FrameworkElement)sender).Parent as UIElement;
            if (parent == null) return;

            var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = sender };
            parent.RaiseEvent(args);
        }

        private void SeeFullTimeline_Click(object sender, RoutedEventArgs e)
        {
            var viewer = new TimelineViewerWindow(ItemsSource, Title) { Owner = Window.GetWindow(this) };
            viewer.ShowDialog();
        }
    }
}
