using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Data;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Pages.Admin.AccountManagement;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs;
using Yakult.Inventory.App.WPF.Admin.AccountManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Views
{
    public partial class UserAccountManagementView : UserControl
    {
        private readonly UserAccountManagementViewModel _vm;

        public UserAccountManagementView()
        {
            InitializeComponent();
            _vm         = new UserAccountManagementViewModel();
            DataContext  = _vm;
            Loaded      += OnLoaded;
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
                MessageBox.Show($"Error refreshing:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CboFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || CboFilter.SelectedItem == null) return;
            _vm.FilterOption = ((ComboBoxItem)CboFilter.SelectedItem).Content?.ToString() ?? "All";
        }

        private void MainGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (MainGrid.SelectedItem != null) BtnEdit_Click(sender, e);
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var owner = System.Windows.Forms.Form.ActiveForm;
            using (var dialog = new UserAccountEditDialog(DatabaseConfig.ConnectionString))
            {
                if (dialog.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK)
                    _ = _vm.LoadAsync();
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selected = MainGrid.SelectedItem as LegacyUserAccountDto;
            if (selected == null)
            {
                MessageBox.Show("Please select a user to edit.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var owner = System.Windows.Forms.Form.ActiveForm;
            using (var dialog = new UserAccountEditDialog(DatabaseConfig.ConnectionString, selected))
            {
                if (dialog.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK)
                    _ = _vm.LoadAsync();
            }
        }

        private async void BtnResetPwd_Click(object sender, RoutedEventArgs e)
        {
            var selected = MainGrid.SelectedItem as LegacyUserAccountDto;
            if (selected == null)
            {
                MessageBox.Show("Please select a user to reset password.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Reset password for '{selected.Username}'?\n\nA temporary password will be generated.",
                "Confirm Password Reset", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                string tempPassword = PasswordHelper.GenerateTemporaryPassword();
                await _vm.ResetPasswordAsync(selected.UserId, tempPassword);

                ActivityLogger.Log(ActivityLogger.Actions.ResetPassword, "User", selected.UserId,
                    $"Password reset for user '{selected.Username}'");

                var owner = System.Windows.Forms.Form.ActiveForm;
                using (var dlg = new PasswordResetSuccessDialog(selected.Username, tempPassword))
                    dlg.ShowDialog(owner);

                await _vm.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to reset password:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnManualReset_Click(object sender, RoutedEventArgs e)
        {
            var selected = MainGrid.SelectedItem as LegacyUserAccountDto;
            if (selected == null)
            {
                MessageBox.Show("Please select a user.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var owner   = System.Windows.Forms.Form.ActiveForm;
            var repo    = new UserRepository(DatabaseConfig.ConnectionString);
            using (var dialog = new ResetPasswordDialog(repo, selected.UserId))
                dialog.ShowDialog(owner);
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = MainGrid.SelectedItem as LegacyUserAccountDto;
            if (selected == null)
            {
                MessageBox.Show("Please select a user to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (selected.UserId == AppSession.CurrentUserId)
            {
                MessageBox.Show("You cannot delete your own account while logged in.", "Cannot Delete",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selected.IsDeveloper)
            {
                var devResult = MessageBox.Show(
                    $"WARNING: '{selected.Username}' is a Developer account.\n\nAre you sure?",
                    "Delete Developer Account", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (devResult != MessageBoxResult.Yes) return;
            }

            string message = $"Delete user account '{selected.Username}'?\n\nThis will permanently remove:\n• The user account and credentials\n• All role assignments\n\n";
            if (!string.IsNullOrEmpty(selected.EmployeeName))
                message += $"The linked employee '{selected.EmployeeName}' will NOT be deleted.\n\n";
            message += "This action CANNOT be undone.";

            if (MessageBox.Show(message, "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                await _vm.DeleteUserAsync(selected.UserId);
                await _vm.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete user:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnFirst_Click(object sender, RoutedEventArgs e) => _vm.GoToFirstPage();
        private void BtnPrev_Click(object sender, RoutedEventArgs e)  => _vm.GoToPrevPage();
        private void BtnNext_Click(object sender, RoutedEventArgs e)  => _vm.GoToNextPage();
        private void BtnLast_Click(object sender, RoutedEventArgs e)  => _vm.GoToLastPage();

        private void MainGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;

            string header = (e.Column.Header?.ToString() ?? "").Replace(" ✎", "").Trim();
            bool ascending = e.Column.SortDirection != System.ComponentModel.ListSortDirection.Ascending;

            foreach (var col in MainGrid.Columns)
                col.SortDirection = null;
            e.Column.SortDirection = ascending
                ? System.ComponentModel.ListSortDirection.Ascending
                : System.ComponentModel.ListSortDirection.Descending;

            _vm.SetSort(header, ascending);
        }

        private void MainGrid_HeaderButtonClick(object sender, RoutedEventArgs e)
        {
            if (!(e.OriginalSource is Button btn) || btn.Name != "FilterBtn") return;
            string column = btn.Tag?.ToString();
            if (string.IsNullOrEmpty(column)) return;

            var values = _vm.GetUniqueValuesForColumn(column);
            if (values.Count == 0) return;

            var popup = new ColumnFilterWindow(column, values, _vm.GetColumnFilter(column));
            PositionPopupNearButton(popup, btn);
            SetWpfOwner(popup);
            if (popup.ShowDialog() != true) return;

            if (popup.ClearFilter)
                _vm.ClearColumnFilter(column);
            else
                _vm.SetColumnFilter(column, popup.SelectedValues);
        }

        private static void PositionPopupNearButton(System.Windows.Window popup, Button btn)
        {
            try
            {
                var pt     = btn.PointToScreen(new System.Windows.Point(0, btn.ActualHeight));
                var source = System.Windows.PresentationSource.FromVisual(btn);
                if (source?.CompositionTarget != null)
                    pt = source.CompositionTarget.TransformFromDevice.Transform(pt);

                var    area = System.Windows.SystemParameters.WorkArea;
                double estH = double.IsNaN(popup.Height) ? popup.MaxHeight : popup.Height;
                popup.Left = Math.Max(area.Left, Math.Min(pt.X, area.Right  - popup.Width));
                popup.Top  = Math.Max(area.Top,  Math.Min(pt.Y, area.Bottom - estH));
            }
            catch { popup.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner; }
        }

        private static void SetWpfOwner(System.Windows.Window window)
        {
            var owner = System.Windows.Forms.Form.ActiveForm;
            if (owner != null) new WindowInteropHelper(window).Owner = owner.Handle;
        }
    }
}
