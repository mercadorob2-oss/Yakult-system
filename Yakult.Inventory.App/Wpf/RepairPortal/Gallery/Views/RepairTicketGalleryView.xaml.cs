using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Wpf.RepairPortal.Shell.ViewModels;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Gallery.Views
{
    /// <summary>
    /// Facebook-feed-style card gallery, bound to the Shell's shared
    /// ObservableCollection&lt;RepairTicketListItem&gt; (inherited DataContext = RepairPortalShellViewModel).
    /// Paginated the same way RepairTicketTableView is — only one page of cards is ever realized,
    /// so the cards area and the paging bar underneath both stay within the visible window instead
    /// of growing with the full result set.
    /// </summary>
    public partial class RepairTicketGalleryView : UserControl
    {
        private const int PageSize = 8;

        // Cards target ~300-460px each (see MinWidth/MaxWidth on the card Border in the DataTemplate).
        // Used only to decide how many columns fit — the UniformGrid then stretches each card to fill
        // its column, so 3 cards with room to spare grow to fill the row instead of leaving dead
        // space, and a 4th column only ever appears once there's genuinely enough width for one.
        private const double MinCardWidthWithMargin = 300 + 16;

        // The UniformGrid lives inside TicketsItemsControl's ItemsPanelTemplate, which has its own
        // XAML namescope — x:Name inside a template doesn't produce a code-behind field, so the
        // actual realized instance has to be found via the visual tree instead. Cached once found;
        // it's the same single panel instance for the ItemsControl's whole lifetime.
        private UniformGrid _cardsGrid;

        private readonly ObservableCollection<RepairTicketListItem> _pagedTickets = new ObservableCollection<RepairTicketListItem>();
        private RepairPortalShellViewModel _vm;
        private int _currentPage = 1;

        private readonly RelayCommand _firstPageCommand;
        private readonly RelayCommand _prevPageCommand;
        private readonly RelayCommand _nextPageCommand;
        private readonly RelayCommand _lastPageCommand;

        public RepairTicketGalleryView()
        {
            InitializeComponent();
            TicketsItemsControl.ItemsSource = _pagedTickets;

            _firstPageCommand = new RelayCommand(() => { _currentPage = 1; BindPage(); }, () => _currentPage > 1);
            _prevPageCommand = new RelayCommand(() => { _currentPage = Math.Max(1, _currentPage - 1); BindPage(); }, () => _currentPage > 1);
            _nextPageCommand = new RelayCommand(() => { _currentPage++; BindPage(); }, () => _currentPage < GetTotalPages());
            _lastPageCommand = new RelayCommand(() => { _currentPage = GetTotalPages(); BindPage(); }, () => _currentPage < GetTotalPages());
            PagingBar.FirstPageCommand = _firstPageCommand;
            PagingBar.PrevPageCommand = _prevPageCommand;
            PagingBar.NextPageCommand = _nextPageCommand;
            PagingBar.LastPageCommand = _lastPageCommand;

            // Covers the case where the very first SizeChanged fires before the ItemsPanelTemplate
            // has been applied (so FindVisualChild would come up empty) — by the time Loaded fires,
            // the visual tree is guaranteed to exist.
            Loaded += (s, e) => RecomputeColumns(GalleryScrollViewer.ActualWidth);
            DataContextChanged += OnDataContextChanged;
            Unloaded += (s, e) => Detach();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Detach();
            _vm = e.NewValue as RepairPortalShellViewModel;
            if (_vm == null) return;

            _vm.Tickets.CollectionChanged += Tickets_CollectionChanged;
            _currentPage = 1;
            BindPage();
        }

        private void Detach()
        {
            if (_vm != null)
                _vm.Tickets.CollectionChanged -= Tickets_CollectionChanged;
        }

        private void Tickets_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // A full reload (new search/filter results) starts with Clear() — a Reset action — so
            // that's the one case worth snapping back to page 1. A single delete instead raises a
            // per-item Remove action; that just needs the current page re-clamped if it no longer
            // exists, not forced back to the start.
            if (e.Action == NotifyCollectionChangedAction.Reset) _currentPage = 1;
            BindPage();
        }

        private int GetTotalPages()
        {
            var total = _vm?.Tickets?.Count ?? 0;
            var pages = (int)Math.Ceiling(total / (double)PageSize);
            return pages < 1 ? 1 : pages;
        }

        private void BindPage()
        {
            _pagedTickets.Clear();

            var tickets = _vm?.Tickets;
            var total = tickets?.Count ?? 0;
            var totalPages = GetTotalPages();
            if (_currentPage < 1) _currentPage = 1;
            if (_currentPage > totalPages) _currentPage = totalPages;

            if (tickets != null)
                foreach (var ticket in tickets.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                    _pagedTickets.Add(ticket);

            PagingBar.PageInfoText = total == 0
                ? "No repair tickets found."
                : $"Page {_currentPage} / {totalPages} • {total} ticket(s)";

            _firstPageCommand.RaiseCanExecuteChanged();
            _prevPageCommand.RaiseCanExecuteChanged();
            _nextPageCommand.RaiseCanExecuteChanged();
            _lastPageCommand.RaiseCanExecuteChanged();
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) return match;
                var descendant = FindVisualChild<T>(child);
                if (descendant != null) return descendant;
            }
            return null;
        }

        private void GalleryScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) =>
            RecomputeColumns(e.NewSize.Width);

        private void RecomputeColumns(double availableWidth)
        {
            if (availableWidth <= 0) return;

            if (_cardsGrid == null)
                _cardsGrid = FindVisualChild<UniformGrid>(TicketsItemsControl);
            if (_cardsGrid == null) return;

            var columns = Math.Max(1, (int)(availableWidth / MinCardWidthWithMargin));
            if (_cardsGrid.Columns != columns)
                _cardsGrid.Columns = columns;
        }

        private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            OpenTicket((sender as FrameworkElement)?.Tag as RepairTicketListItem);
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            OpenTicket((sender as FrameworkElement)?.Tag as RepairTicketListItem);
        }

        private void OpenTicket(RepairTicketListItem ticket)
        {
            if (ticket == null) return;
            if (DataContext is RepairPortalShellViewModel vm && vm.OpenTicketCommand.CanExecute(ticket))
                vm.OpenTicketCommand.Execute(ticket);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // Card click-anywhere (Card_MouseLeftButtonUp) would otherwise also fire and open the
            // ticket — stop it from bubbling past this button.
            e.Handled = true;

            var ticket = (sender as FrameworkElement)?.Tag as RepairTicketListItem;
            if (ticket == null) return;
            if (DataContext is RepairPortalShellViewModel vm && vm.DeleteTicketCommand.CanExecute(ticket))
                vm.DeleteTicketCommand.Execute(ticket);
        }
    }
}
