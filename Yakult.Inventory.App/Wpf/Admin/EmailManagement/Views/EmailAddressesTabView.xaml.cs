using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs;
using Yakult.Inventory.App.WPF.Admin.EmailManagement.ViewModels;
using Yakult.Inventory.App.WPF.Shared;
using static Yakult.Inventory.App.Forms.SystemSettings.SystemEmailSettingsForm;

namespace Yakult.Inventory.App.WPF.Admin.EmailManagement.Views
{
    public partial class EmailAddressesTabView : UserControl
    {
        private EmailAddressesViewModel _vm;

        public EmailAddressesTabView()
        {
            InitializeComponent();
        }

        public void Bind(EmailAddressesViewModel vm)
        {
            _vm = vm;
            DataContext = vm;
        }

        private System.Windows.Forms.IWin32Window Owner()
            => new Win32WindowWrapper(new WindowInteropHelper(System.Windows.Window.GetWindow(this)).Handle);

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            foreach (var col in MainGrid.Columns) col.SortDirection = null;
            try { await _vm.LoadAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading email addresses: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new BulkAddEmailAddressDialog(_vm.NextEmailId()))
            {
                if (dlg.ShowDialog(Owner()) != System.Windows.Forms.DialogResult.OK)
                    return;

                try
                {
                    int saved = await _vm.AddEmailsAsync(dlg.NewEmails);
                    MessageBox.Show($"{saved} email address(es) added successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    await _vm.LoadAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving email addresses: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // BulkEditEmailAddressDialog needs the shared EmailRepository instance from the parent window's composing VM.
        private Yakult.Inventory.App.Repositories.EmailRepository _emailRepo;
        public void SetRepository(Yakult.Inventory.App.Repositories.EmailRepository repo) => _emailRepo = repo;

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var addresses = _vm.AllRows.ToList();
                using (var dlg = new BulkEditEmailAddressDialog(addresses, _emailRepo))
                {
                    dlg.ShowDialog(Owner());
                    await _vm.LoadAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening edit dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var (saved, errors) = await _vm.SaveAllAsync();
            if (errors.Count > 0)
            {
                MessageBox.Show(
                    "Please fix the following errors before saving:\n\n" + string.Join("\n", errors),
                    "Validation Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"{saved} email address(es) saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            await _vm.LoadAsync();
        }

        private async void BtnDeactivate_Click(object sender, RoutedEventArgs e)
        {
            var selected = MainGrid.SelectedItem as EmailAddressDto;
            if (selected == null)
            {
                MessageBox.Show("Please select a row to deactivate.", "Deactivate Email Address", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to deactivate:\n\n{selected.EmailAddress}\n\nThis will mark it as inactive.",
                "Confirm Deactivation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _vm.DeactivateAsync(selected.EmailId);
                MessageBox.Show("Email address deactivated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                await _vm.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deactivating email address: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = MainGrid.SelectedItem as EmailAddressDto;
            if (selected == null)
            {
                MessageBox.Show("Please select a row to delete.", "Delete Email Address", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            List<EmailAddressReferenceDto> refs;
            try
            {
                refs = await _vm.GetReferencesAsync(selected.EmailId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking linked records: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Still linked somewhere — hand off to the review / unlink / deactivate dialog.
            if (refs.Count > 0)
            {
                await ShowLinkedRecordsDialogAsync(selected, refs);
                return;
            }

            var result = MessageBox.Show(
                $"Permanently delete this email address?\n\n{selected.EmailAddress}\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _vm.DeleteAsync(selected.EmailId);
                MessageBox.Show("Email address deleted permanently.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                await _vm.LoadAsync();
            }
            catch (System.Data.SqlClient.SqlException ex) when (ex.Number == 547)
            {
                MessageBox.Show(
                    "This email address is still referenced by other records. Use \"Linked Records\" to review and unlink them, or deactivate it instead.",
                    "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting email address: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnLinked_Click(object sender, RoutedEventArgs e)
        {
            var selected = MainGrid.SelectedItem as EmailAddressDto;
            if (selected == null)
            {
                MessageBox.Show("Please select a row to inspect.", "Linked Records", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            List<EmailAddressReferenceDto> refs;
            try
            {
                refs = await _vm.GetReferencesAsync(selected.EmailId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading linked records: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (refs.Count == 0)
            {
                var go = MessageBox.Show(
                    $"Nothing is linked to:\n\n{selected.EmailAddress}\n\nDelete it permanently now?",
                    "No Linked Records", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (go != MessageBoxResult.Yes) return;

                try
                {
                    await _vm.DeleteAsync(selected.EmailId);
                    MessageBox.Show("Email address deleted permanently.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    await _vm.LoadAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting email address: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            await ShowLinkedRecordsDialogAsync(selected, refs);
        }

        private async Task ShowLinkedRecordsDialogAsync(EmailAddressDto selected, List<EmailAddressReferenceDto> refs)
        {
            var dlg = new EmailLinkedRecordsDialog(_vm, selected, refs);
            new WindowInteropHelper(dlg).Owner =
                new WindowInteropHelper(System.Windows.Window.GetWindow(this)).Handle;
            dlg.ShowDialog();

            if (dlg.Action != EmailLinkedRecordsDialog.ResultAction.None)
                await _vm.LoadAsync();
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

            foreach (var col in MainGrid.Columns) col.SortDirection = null;
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
            new WindowInteropHelper(popup).Owner = new WindowInteropHelper(System.Windows.Window.GetWindow(this)).Handle;
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
                case "Email Address": return "EmailAddress";
                case "Display Name":  return "DisplayName";
                case "Active":        return "Active";
                default:              return null;
            }
        }
    }
}
