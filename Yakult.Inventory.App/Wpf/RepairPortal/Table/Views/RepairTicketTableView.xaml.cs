using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Wpf.RepairPortal.Shell.ViewModels;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Table.Views
{
    public partial class RepairTicketTableView : UserControl
    {
        private const int PageSize = 8;

        // Table-local wrapper collection, kept in sync with the shared RepairPortalShellViewModel
        // .Tickets (also bound directly by Gallery/Kanban) — see RepairTicketTableRow for why this
        // can't just be an IsSelected flag added onto the shared model itself. Holds only the
        // CURRENT PAGE's rows — Select All and the summary-row Open/Delete actions both operate on
        // this page only, same convention as RepairedItemsPageViewModel's SelectAllState.
        private readonly ObservableCollection<RepairTicketTableRow> _pagedRows = new ObservableCollection<RepairTicketTableRow>();
        private RepairPortalShellViewModel _vm;
        private int _currentPage = 1;

        private readonly RelayCommand _firstPageCommand;
        private readonly RelayCommand _prevPageCommand;
        private readonly RelayCommand _nextPageCommand;
        private readonly RelayCommand _lastPageCommand;

        public RepairTicketTableView()
        {
            InitializeComponent();
            TicketsGrid.ItemsSource = _pagedRows;

            _firstPageCommand = new RelayCommand(() => { _currentPage = 1; BindGridPage(); }, () => _currentPage > 1);
            _prevPageCommand = new RelayCommand(() => { _currentPage = Math.Max(1, _currentPage - 1); BindGridPage(); }, () => _currentPage > 1);
            _nextPageCommand = new RelayCommand(() => { _currentPage++; BindGridPage(); }, () => _currentPage < GetTotalPages());
            _lastPageCommand = new RelayCommand(() => { _currentPage = GetTotalPages(); BindGridPage(); }, () => _currentPage < GetTotalPages());
            PagingBar.FirstPageCommand = _firstPageCommand;
            PagingBar.PrevPageCommand = _prevPageCommand;
            PagingBar.NextPageCommand = _nextPageCommand;
            PagingBar.LastPageCommand = _lastPageCommand;

            DataContextChanged += OnDataContextChanged;
            Unloaded += (s, e) => Detach();
            TicketsGrid.PreviewMouseLeftButtonUp += TicketsGrid_PreviewMouseLeftButtonUp;
        }

        // Clicking anywhere on a row toggles its checkbox — not just the checkbox cell itself.
        // Skips clicks that already landed on the checkbox (its own binding already handled those;
        // toggling again here would just flip it straight back).
        private void TicketsGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            if (FindAncestor<CheckBox>(source) != null) return;

            var row = FindAncestor<DataGridRow>(source);
            if (row?.Item is RepairTicketTableRow tableRow)
                tableRow.IsSelected = !tableRow.IsSelected;
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Detach();
            _vm = e.NewValue as RepairPortalShellViewModel;
            if (_vm == null) return;

            _vm.Tickets.CollectionChanged += Tickets_CollectionChanged;
            _vm.TableSelectionReviewRequested += OnTableSelectionReviewRequested;
            _currentPage = 1;
            BindGridPage();
        }

        private void Detach()
        {
            if (_vm != null)
            {
                _vm.Tickets.CollectionChanged -= Tickets_CollectionChanged;
                _vm.TableSelectionReviewRequested -= OnTableSelectionReviewRequested;
            }
            foreach (var row in _pagedRows) row.PropertyChanged -= Row_PropertyChanged;
            _vm?.SetTableSelection(null);
        }

        // Fired by the "N selected" text (clickable, in the Shell's summary row) via
        // RepairPortalShellViewModel.ReviewTableSelectionCommand — same "click the selection badge
        // to review/unselect" pattern as ItemsPageView's SelectedBadge_Click
        // (SelectedItemsReviewDialog). Only the rows the technician explicitly removes in that
        // dialog get unchecked here — everything else stays selected.
        private void OnTableSelectionReviewRequested()
        {
            var selected = _pagedRows.Where(r => r.IsSelected).Select(r => r.Ticket).ToList();
            if (selected.Count == 0) return;

            var dialog = new RepairTicketSelectionReviewDialog(selected) { Owner = Window.GetWindow(this) };
            dialog.ShowDialog();

            if (dialog.RemovedTicketIds.Count == 0) return;
            foreach (var row in _pagedRows)
                if (dialog.RemovedTicketIds.Contains(row.Ticket.RepairTicketId))
                    row.IsSelected = false;
        }

        private void Tickets_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // A full reload (new search/filter results) starts with Clear() — a Reset action —
            // so that's the one case worth snapping back to page 1. A bulk/single delete instead
            // raises per-item Remove actions; those just need the current page re-clamped if it
            // no longer exists, not forced back to the start.
            if (e.Action == NotifyCollectionChangedAction.Reset) _currentPage = 1;
            BindGridPage();
        }

        private int GetTotalPages()
        {
            var total = _vm?.Tickets?.Count ?? 0;
            var pages = (int)Math.Ceiling(total / (double)PageSize);
            return pages < 1 ? 1 : pages;
        }

        // Rebuilds the current page's rows from the shared Tickets collection and resets selection
        // — selection is deliberately scoped to whatever's visibly checked on the current page, so
        // it always resets on page change/list change rather than trying to track it across pages.
        private void BindGridPage()
        {
            foreach (var row in _pagedRows) row.PropertyChanged -= Row_PropertyChanged;
            _pagedRows.Clear();

            var tickets = _vm?.Tickets;
            var total = tickets?.Count ?? 0;
            var totalPages = GetTotalPages();
            if (_currentPage < 1) _currentPage = 1;
            if (_currentPage > totalPages) _currentPage = totalPages;

            if (tickets != null)
            {
                var pageTickets = tickets.Skip((_currentPage - 1) * PageSize).Take(PageSize);
                foreach (var ticket in pageTickets)
                {
                    var row = new RepairTicketTableRow(ticket);
                    row.PropertyChanged += Row_PropertyChanged;
                    _pagedRows.Add(row);
                }
            }

            PagingBar.PageInfoText = total == 0
                ? "No repair tickets found."
                : $"Page {_currentPage} / {totalPages} • {total} ticket(s)";

            _firstPageCommand.RaiseCanExecuteChanged();
            _prevPageCommand.RaiseCanExecuteChanged();
            _nextPageCommand.RaiseCanExecuteChanged();
            _lastPageCommand.RaiseCanExecuteChanged();

            if (SelectAllCheckBox.IsChecked == true) SelectAllCheckBox.IsChecked = false;
            PushSelection();
        }

        private void Row_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RepairTicketTableRow.IsSelected))
                PushSelection();
        }

        private void PushSelection()
        {
            _vm?.SetTableSelection(_pagedRows.Where(r => r.IsSelected).Select(r => r.Ticket).ToList());
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool select = SelectAllCheckBox.IsChecked == true;
            foreach (var row in _pagedRows) row.IsSelected = select;
        }
    }
}
