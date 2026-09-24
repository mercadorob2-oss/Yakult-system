using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages.Admin.AccountManagement;
using Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs;
using Yakult.Inventory.App.WPF.Admin.AccountManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Views
{
    public partial class ApproverManagementView : UserControl
    {
        private readonly ApproverManagementViewModel _vm;

        public ApproverManagementView()
        {
            InitializeComponent();
            _vm         = new ApproverManagementViewModel();
            DataContext = _vm;
            Loaded     += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = _vm.LoadAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            foreach (var col in MainGrid.Columns)
                col.SortDirection = null;
            try { await _vm.LoadAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load employees.\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnManageTitles_Click(object sender, RoutedEventArgs e)
        {
            var owner = System.Windows.Forms.Form.ActiveForm;
            using (var dlg = new ManageApproverTitlesDialog(DatabaseConfig.ConnectionString))
            {
                dlg.ShowDialog(owner);
                _ = _vm.LoadAsync();
            }
        }

        private void BtnPositionSummary_Click(object sender, RoutedEventArgs e)
        {
            var counts = _vm.AllRows
                .GroupBy(r => r.Position.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var roleOrder = new[] { "Coordinator", "Manager", "Supervisor" };
            var summaryRows = new System.Collections.Generic.List<(string Position, string Role, int Count)>();

            if (_vm.ApproverTitleEntries.Count > 0)
            {
                foreach (string role in roleOrder)
                {
                    var entries = _vm.ApproverTitleEntries
                        .Where(t => t.ApprovalRole.Equals(role, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(t => t.RolePriority)
                        .ThenBy(t => t.PositionTitle);
                    foreach (var entry in entries)
                    {
                        int count = counts.TryGetValue(entry.PositionTitle, out int c) ? c : 0;
                        summaryRows.Add((entry.PositionTitle, role, count));
                    }
                }
            }

            var owner = System.Windows.Forms.Form.ActiveForm;
            using (var dlg = new PositionSummaryDialog(summaryRows))
                dlg.ShowDialog(owner);
        }

        private void BtnBulkCreate_Click(object sender, RoutedEventArgs e)
        {
            var win = new BulkCreateAccountsWindow(
                _vm, _vm.SearchText, _vm.ShowArchived, _vm.GetColumnFiltersSnapshot());
            SetWpfOwner(win);
            win.ShowDialog();

            if (win.AccountsCreated)
                _ = _vm.LoadAsync();   // refresh System Role(s) / account state
        }

        private void BtnFirst_Click(object sender, RoutedEventArgs e) => _vm.GoToFirstPage();
        private void BtnPrev_Click(object sender, RoutedEventArgs e)  => _vm.GoToPrevPage();
        private void BtnNext_Click(object sender, RoutedEventArgs e)  => _vm.GoToNextPage();
        private void BtnLast_Click(object sender, RoutedEventArgs e)  => _vm.GoToLastPage();

        private void MainGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;

            string column = ColumnNameFor(e.Column.Header?.ToString());
            if (column == null) return;

            bool ascending = e.Column.SortDirection != System.ComponentModel.ListSortDirection.Ascending;

            foreach (var col in MainGrid.Columns)
                col.SortDirection = null;
            e.Column.SortDirection = ascending
                ? System.ComponentModel.ListSortDirection.Ascending
                : System.ComponentModel.ListSortDirection.Descending;

            _vm.SetSort(column, ascending);
        }

        private void MainGrid_HeaderButtonClick(object sender, RoutedEventArgs e)
        {
            if (!(e.OriginalSource is Button btn) || btn.Name != "FilterBtn") return;
            string column = ColumnNameFor(btn.Tag?.ToString());
            if (string.IsNullOrEmpty(column)) return;

            var values = _vm.GetUniqueValuesForColumn(column);
            if (values.Count == 0) return;

            var popup = new ColumnFilterWindow(column, values, _vm.GetColumnFilter(column));
            SetWpfOwner(popup);
            if (popup.ShowDialog() != true) return;

            if (popup.ClearFilter)
                _vm.ClearColumnFilter(column);
            else
                _vm.SetColumnFilter(column, popup.SelectedValues);
        }

        private static string ColumnNameFor(string header)
        {
            switch (header)
            {
                case "Employee #":     return "EmployeeNumber";
                case "Title":          return "Title";
                case "Employee Name":  return "EmployeeName";
                case "Position":       return "Position";
                case "Department":     return "Department";
                case "Company":        return "Company";
                case "Branch":         return "Branch";
                case "Is Approver":    return "IsApprover";
                case "System Role(s)": return "SystemRoles";
                default:               return null;
            }
        }

        private static void SetWpfOwner(Window window)
        {
            var owner = System.Windows.Forms.Form.ActiveForm;
            if (owner != null) new WindowInteropHelper(window).Owner = owner.Handle;
        }
    }
}
