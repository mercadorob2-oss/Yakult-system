using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Yakult.Inventory.App.Wpf.RepairPortal.Detail.ViewModels;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Detail.Views
{
    /// <summary>Horizontal image/video/document carousel for the Detail window. Reusable —
    /// bound explicitly via the ItemsSource dependency property rather than inherited
    /// DataContext, since it displays a specific collection (Attachments) off the Detail VM.</summary>
    public partial class ImageCarouselControl : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource), typeof(IEnumerable), typeof(ImageCarouselControl),
            new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty HasAttachmentsProperty = DependencyProperty.Register(
            nameof(HasAttachments), typeof(bool), typeof(ImageCarouselControl), new PropertyMetadata(false));

        public bool HasAttachments
        {
            get => (bool)GetValue(HasAttachmentsProperty);
            private set => SetValue(HasAttachmentsProperty, value);
        }

        /// <summary>Bound from the host (RepairTicketDetailWindow / RepairPartCard) to that
        /// ViewModel's DeleteSelectedAttachmentsCommand — the carousel itself has no VM access.</summary>
        public static readonly DependencyProperty DeleteSelectedCommandProperty = DependencyProperty.Register(
            nameof(DeleteSelectedCommand), typeof(ICommand), typeof(ImageCarouselControl), new PropertyMetadata(null));

        public ICommand DeleteSelectedCommand
        {
            get => (ICommand)GetValue(DeleteSelectedCommandProperty);
            set => SetValue(DeleteSelectedCommandProperty, value);
        }

        public ImageCarouselControl()
        {
            InitializeComponent();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ImageCarouselControl)d;

            if (e.OldValue is INotifyCollectionChanged oldIncc)
                oldIncc.CollectionChanged -= control.OnCollectionChanged;
            if (e.NewValue is INotifyCollectionChanged newIncc)
                newIncc.CollectionChanged += control.OnCollectionChanged;

            control.UpdateHasAttachments();
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => UpdateHasAttachments();

        /// <summary>Opens the clicked thumbnail fullscreen/maximized, positioned within the whole
        /// gallery so Next/Prev can navigate every attachment (not just the one clicked). The bound
        /// item is either AttachmentDisplayItem (ticket-level) or PartAttachmentDisplayItem
        /// (part-level) — both expose the same Bytes/FileName/AttachmentType shape but share no
        /// common base, so build a normalized entry list explicitly rather than pulling in a
        /// `dynamic`/reflection dependency for this.</summary>
        private void Thumbnail_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is FrameworkElement fe)) return;

            // Don't open the viewer if the click originated on/inside the selection checkbox —
            // let it just toggle normally instead of also popping the viewer open. Checking the
            // click's origin here (rather than trying to intercept on the checkbox itself) avoids
            // fighting with CheckBox's own internal click-to-toggle handling.
            if (IsWithinCheckBox(e.OriginalSource as DependencyObject)) return;

            var entries = new List<EvidenceViewerEntry>();
            var startIndex = 0;

            foreach (var obj in ItemsSource)
            {
                switch (obj)
                {
                    case AttachmentDisplayItem ticketItem:
                        if (ReferenceEquals(obj, fe.Tag)) startIndex = entries.Count;
                        entries.Add(new EvidenceViewerEntry(ticketItem.FileName, ticketItem.AttachmentType, ticketItem.Bytes));
                        break;
                    case PartAttachmentDisplayItem partItem:
                        if (ReferenceEquals(obj, fe.Tag)) startIndex = entries.Count;
                        entries.Add(new EvidenceViewerEntry(partItem.FileName, partItem.AttachmentType, partItem.Bytes));
                        break;
                }
            }

            if (entries.Count == 0) return;

            var viewer = new EvidenceViewerWindow(entries, startIndex) { Owner = Window.GetWindow(this) };
            viewer.ShowDialog();
        }

        /// <summary>The Evidence carousel's ScrollViewer only scrolls horizontally
        /// (VerticalScrollBarVisibility="Disabled"), which otherwise swallows the mouse wheel
        /// entirely instead of letting it scroll the outer Detail window — since a disabled
        /// ScrollViewer still marks the wheel event handled without doing anything with it.
        /// Re-raise the wheel event on the nearest ancestor so it scrolls the page instead.</summary>
        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;

            e.Handled = true;
            var parent = ((FrameworkElement)sender).Parent as UIElement;
            if (parent == null) return;

            var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            parent.RaiseEvent(args);
        }

        private static bool IsWithinCheckBox(DependencyObject source)
        {
            var current = source;
            while (current != null)
            {
                if (current is CheckBox) return true;
                current = (current as FrameworkElement)?.Parent ?? System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private void UpdateHasAttachments()
        {
            var any = false;
            if (ItemsSource != null)
            {
                foreach (var _ in ItemsSource) { any = true; break; }
            }
            HasAttachments = any;
        }
    }
}
