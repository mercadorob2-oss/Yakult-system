using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Yakult.Inventory.App.WPF.Admin.ReferenceData.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.ReferenceData.Views
{
    /// <summary>
    /// Branch Acronyms / Dept Acronyms (WPF). The acronym is edited in place in the grid
    /// (Edit Acronym button, double-click or F2), and saved when the cell edit is committed.
    /// </summary>
    public partial class AcronymListView : UserControl
    {
        private readonly AcronymListViewModel _vm;
        private bool _loadedOnce;

        public AcronymListView(AcronymKind kind)
        {
            InitializeComponent();
            _vm = new AcronymListViewModel(kind);
            DataContext = _vm;

            ColName.Header = _vm.NameHeader;
            // Departments keep Section out of the grid (it's still searchable), as before.
            ColType.Visibility = _vm.IsBranch ? Visibility.Visible : Visibility.Collapsed;

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_loadedOnce) return;
            _loadedOnce = true;
            await LoadAsync(keepPage: false);
            TxtSearch.Focus();
        }

        private async System.Threading.Tasks.Task LoadAsync(bool keepPage)
        {
            try { await _vm.LoadAsync(keepPage); }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load {(_vm.IsBranch ? "branches" : "departments")}: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            foreach (var col in MainGrid.Columns)
                col.SortDirection = null;
            _vm.ResetSort();
            _vm.StatusMessage = string.Empty;
            await LoadAsync(keepPage: false);
        }

        // ── Editing ──────────────────────────────────────────────────────────

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (!(MainGrid.SelectedItem is AcronymRow row))
            {
                MessageBox.Show($"Please select a {(_vm.IsBranch ? "branch" : "department")} to edit.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            BeginAcronymEdit(row);
        }

        private void MainGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Ignore double-clicks on the header (those sort).
            if (e.OriginalSource is DependencyObject src && FindParent<DataGridRow>(src) is DataGridRow gridRow
                && gridRow.Item is AcronymRow row)
            {
                BeginAcronymEdit(row);
                e.Handled = true;
            }
        }

        private void MainGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // F2 edits the acronym whichever column has focus (it's the only editable one).
            if (e.Key == Key.F2 && MainGrid.SelectedItem is AcronymRow row && !IsEditingCell())
            {
                BeginAcronymEdit(row);
                e.Handled = true;
            }
        }

        private bool IsEditingCell()
            => MainGrid.CurrentCell.Column == ColAcronym
               && FindParent<DataGridCell>(Keyboard.FocusedElement as DependencyObject) is DataGridCell cell
               && cell.IsEditing;

        private void BeginAcronymEdit(AcronymRow row)
        {
            MainGrid.SelectedItem = row;
            MainGrid.ScrollIntoView(row, ColAcronym);
            MainGrid.CurrentCell = new DataGridCellInfo(row, ColAcronym);
            MainGrid.Focus();
            MainGrid.BeginEdit();
        }

        private async void MainGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit || e.Column != ColAcronym) return;
            if (!(e.Row.Item is AcronymRow row) || !(e.EditingElement is TextBox box)) return;

            // The column binding is OneWay: the row only changes once the database update succeeds.
            string newValue = box.Text;
            try
            {
                await _vm.SaveAcronymAsync(row, newValue);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save acronym: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Sorting / paging ─────────────────────────────────────────────────

        private void MainGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // Sort the whole filtered list, not just the visible page.
            e.Handled = true;

            bool ascending = e.Column.SortDirection != ListSortDirection.Ascending;
            foreach (var col in MainGrid.Columns)
                col.SortDirection = null;
            e.Column.SortDirection = ascending ? ListSortDirection.Ascending : ListSortDirection.Descending;

            _vm.SetSort(e.Column.SortMemberPath, ascending);
        }

        private void BtnFirst_Click(object sender, RoutedEventArgs e) => _vm.GoToFirstPage();
        private void BtnPrev_Click(object sender, RoutedEventArgs e)  => _vm.GoToPrevPage();
        private void BtnNext_Click(object sender, RoutedEventArgs e)  => _vm.GoToNextPage();
        private void BtnLast_Click(object sender, RoutedEventArgs e)  => _vm.GoToLastPage();

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null && !(child is T))
                child = child is System.Windows.Media.Visual || child is System.Windows.Media.Media3D.Visual3D
                    ? System.Windows.Media.VisualTreeHelper.GetParent(child)
                    : LogicalTreeHelper.GetParent(child);
            return child as T;
        }
    }
}
