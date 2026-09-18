using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs;
using Yakult.Inventory.App.WPF.Admin.UserPositions.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.UserPositions.Views
{
    public partial class UserPositionsView : UserControl
    {
        private readonly UserPositionsViewModel _vm;

        public UserPositionsView()
        {
            InitializeComponent();
            _vm         = new UserPositionsViewModel();
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
                MessageBox.Show($"Error refreshing data:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selected = MainGrid.SelectedItem as UserPositionRow;
            if (selected == null)
            {
                MessageBox.Show("Please select a position to edit.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string newPosition = ShowEditDialog(selected.Position);
            if (newPosition == null) return;

            newPosition = newPosition.Trim();

            if (string.IsNullOrWhiteSpace(newPosition))
            {
                MessageBox.Show("Position name cannot be empty.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.Equals(newPosition, selected.Position, StringComparison.OrdinalIgnoreCase))
                return;

            _ = CommitPositionChangeAsync(selected, newPosition);
        }

        private string ShowEditDialog(string currentPosition)
        {
            using (var dlg = new System.Windows.Forms.Form())
            {
                dlg.Text            = "Edit Position";
                dlg.StartPosition   = System.Windows.Forms.FormStartPosition.CenterParent;
                dlg.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
                dlg.MinimizeBox     = false;
                dlg.MaximizeBox     = false;
                dlg.ClientSize      = new System.Drawing.Size(380, 130);
                dlg.Font            = new Font("Segoe UI", 9.5F);

                var lbl = new System.Windows.Forms.Label { Text = "Position name:", Left = 16, Top = 18, Width = 340, Height = 20, AutoSize = false };
                var txt = new System.Windows.Forms.TextBox { Left = 16, Top = 42, Width = 348, Text = currentPosition, MaxLength = 200 };
                txt.SelectAll();

                var btnOk = new System.Windows.Forms.Button
                    { Text = "Save", DialogResult = System.Windows.Forms.DialogResult.OK, Left = 192, Top = 82, Width = 85, Height = 28 };
                var btnCancel = new System.Windows.Forms.Button
                    { Text = "Cancel", DialogResult = System.Windows.Forms.DialogResult.Cancel, Left = 284, Top = 82, Width = 80, Height = 28 };

                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;
                dlg.Controls.AddRange(new System.Windows.Forms.Control[] { lbl, txt, btnOk, btnCancel });
                dlg.Shown += (s, ev) => txt.Focus();

                var owner = System.Windows.Forms.Form.ActiveForm;
                var result = owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
                return result == System.Windows.Forms.DialogResult.OK ? txt.Text : null;
            }
        }

        private async System.Threading.Tasks.Task CommitPositionChangeAsync(UserPositionRow row, string newPosition)
        {
            try
            {
                int affected = await _vm.RenamePositionAsync(row, newPosition);
                MainGrid.Items.Refresh();
                MessageBox.Show($"Position updated for {affected} employee(s) in {row.BranchName}.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving position:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            BtnEdit_Click(sender, e);
        }

        private void MainGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;

            string header = e.Column.Header?.ToString() ?? "";
            string column = header == "Employee Count" ? "Count"
                : header == "Position" ? "Position"
                : header == "Company"  ? "Company"
                : header == "Branch"   ? "Branch"
                : header;

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

            string header = btn.Tag?.ToString() ?? "";
            string column = header == "Employee Count" ? "Count"
                : header == "Position" ? "Position"
                : header == "Company"  ? "Company"
                : header == "Branch"   ? "Branch"
                : header;

            if (string.IsNullOrEmpty(column)) return;

            var values = _vm.GetUniqueValuesForColumn(column);
            if (values.Count == 0) return;

            var popup = new ColumnFilterWindow(header, values, _vm.GetColumnFilter(column));
            PositionPopupNearButton(popup, btn);
            SetWpfOwner(popup);
            if (popup.ShowDialog() != true) return;

            if (popup.ClearFilter)
                _vm.ClearColumnFilter(column);
            else
                _vm.SetColumnFilter(column, popup.SelectedValues);
        }

        private static void PositionPopupNearButton(Window popup, Button btn)
        {
            try
            {
                var pt     = btn.PointToScreen(new System.Windows.Point(0, btn.ActualHeight));
                var source = PresentationSource.FromVisual(btn);
                if (source?.CompositionTarget != null)
                    pt = source.CompositionTarget.TransformFromDevice.Transform(pt);

                var    area = SystemParameters.WorkArea;
                double estH = double.IsNaN(popup.Height) ? popup.MaxHeight : popup.Height;
                popup.Left = Math.Max(area.Left, Math.Min(pt.X, area.Right  - popup.Width));
                popup.Top  = Math.Max(area.Top,  Math.Min(pt.Y, area.Bottom - estH));
            }
            catch { popup.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
        }

        private static void SetWpfOwner(Window window)
        {
            var owner = System.Windows.Forms.Form.ActiveForm;
            if (owner != null) new System.Windows.Interop.WindowInteropHelper(window).Owner = owner.Handle;
        }

        private void BtnFirst_Click(object sender, RoutedEventArgs e) { _vm.GoToFirstPage(); }
        private void BtnPrev_Click(object sender, RoutedEventArgs e)  { _vm.GoToPrevPage();  }
        private void BtnNext_Click(object sender, RoutedEventArgs e)  { _vm.GoToNextPage();  }
        private void BtnLast_Click(object sender, RoutedEventArgs e)  { _vm.GoToLastPage();  }
    }
}
