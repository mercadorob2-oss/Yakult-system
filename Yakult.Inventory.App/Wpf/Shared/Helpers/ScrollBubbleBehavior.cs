using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Yakult.Inventory.App.WPF.Shared.Helpers
{
    /// <summary>
    /// Nested scrollable controls (DataGrid, ListBox, etc.) capture PreviewMouseWheel and mark
    /// it handled even when they have no more room to scroll internally, which makes an outer
    /// page ScrollViewer appear stuck/unresponsive whenever the cursor is over them. Attaching
    /// BubbleScroll="True" forwards the wheel event to the nearest ancestor ScrollViewer once the
    /// inner control has reached the top/bottom of its own scrollable content (or has none).
    /// </summary>
    public static class ScrollBubbleBehavior
    {
        public static readonly DependencyProperty BubbleScrollProperty =
            DependencyProperty.RegisterAttached("BubbleScroll", typeof(bool), typeof(ScrollBubbleBehavior),
                new PropertyMetadata(false, OnBubbleScrollChanged));

        public static void SetBubbleScroll(DependencyObject element, bool value) => element.SetValue(BubbleScrollProperty, value);
        public static bool GetBubbleScroll(DependencyObject element) => (bool)element.GetValue(BubbleScrollProperty);

        private static void OnBubbleScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is UIElement element)) return;

            element.PreviewMouseWheel -= OnPreviewMouseWheel;
            if ((bool)e.NewValue)
                element.PreviewMouseWheel += OnPreviewMouseWheel;
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var element = (UIElement)sender;
            var innerScroll = FindDescendantScrollViewer(element as DependencyObject);

            bool innerCanScroll = innerScroll != null &&
                ((e.Delta > 0 && innerScroll.VerticalOffset > 0) ||
                 (e.Delta < 0 && innerScroll.VerticalOffset < innerScroll.ScrollableHeight));

            if (innerCanScroll) return; // inner control still has room to scroll — let it handle normally

            var outerScroll = FindAncestorScrollViewer(element as DependencyObject);
            if (outerScroll == null) return;

            e.Handled = true;
            outerScroll.ScrollToVerticalOffset(outerScroll.VerticalOffset - e.Delta);
        }

        private static ScrollViewer FindDescendantScrollViewer(DependencyObject root)
        {
            if (root == null) return null;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is ScrollViewer sv) return sv;

                var found = FindDescendantScrollViewer(child);
                if (found != null) return found;
            }
            return null;
        }

        private static ScrollViewer FindAncestorScrollViewer(DependencyObject node)
        {
            while (node != null)
            {
                DependencyObject parent = (node is Visual || node is Visual3D)
                    ? VisualTreeHelper.GetParent(node)
                    : null;
                parent = parent ?? LogicalTreeHelper.GetParent(node);

                node = parent;
                if (node is ScrollViewer sv) return sv;
            }
            return null;
        }
    }
}
