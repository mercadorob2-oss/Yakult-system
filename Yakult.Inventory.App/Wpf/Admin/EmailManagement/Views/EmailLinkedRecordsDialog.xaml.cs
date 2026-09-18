using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.Admin.EmailManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.EmailManagement.Views
{
    /// <summary>
    /// Shows every record that still references an email address and lets the admin
    /// unlink them (so a permanent delete can proceed), delete the email outright once
    /// it is clear, or fall back to deactivating it.
    /// </summary>
    public partial class EmailLinkedRecordsDialog : Window
    {
        public enum ResultAction { None, Unlinked, Deleted, Deactivated }

        private readonly EmailAddressesViewModel _vm;
        private readonly EmailAddressDto _email;
        private List<EmailAddressReferenceDto> _refs;
        private bool _busy;

        /// <summary>What the dialog ended up doing — the caller reloads the grid unless this is None.</summary>
        public ResultAction Action { get; private set; } = ResultAction.None;

        public EmailLinkedRecordsDialog(EmailAddressesViewModel vm, EmailAddressDto email, List<EmailAddressReferenceDto> refs)
        {
            InitializeComponent();
            _vm    = vm;
            _email = email;
            _refs  = refs ?? new List<EmailAddressReferenceDto>();

            LblHeader.Text = $"{_refs.Count} record(s) still reference \"{_email.EmailAddress}\".";
            RenderRefs();
        }

        private void RenderRefs()
        {
            RefGrid.ItemsSource = _refs
                .OrderBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Description, StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool hasSmtp = _refs.Any(r => r.RequiresSmtpDeletion);
            SmtpWarnBox.Visibility = hasSmtp ? Visibility.Visible : Visibility.Collapsed;

            bool clear = _refs.Count == 0;
            BtnUnlink.IsEnabled = !clear;
            BtnDelete.IsEnabled = clear;
            LblStatus.Text = clear
                ? "All references cleared — the email can now be deleted."
                : $"{_refs.Count} linked record(s).";
        }

        private async void BtnUnlink_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;

            bool hasSmtp        = _refs.Any(r => r.RequiresSmtpDeletion);
            bool deleteSmtp     = ChkDeleteSmtp.IsChecked == true;
            int  smtpRemaining  = hasSmtp && !deleteSmtp ? _refs.Count(r => r.RequiresSmtpDeletion) : 0;

            string msg = "Unlink this email from the records listed?\n\n" +
                         "Nullable links are cleared and employee-email links are removed. This cannot be undone.";
            if (deleteSmtp)
                msg += "\n\nThe linked SMTP profile(s) will also be permanently deleted.";
            else if (smtpRemaining > 0)
                msg += $"\n\n{smtpRemaining} SMTP profile link(s) will remain (checkbox not ticked), " +
                       "so the email still won't be deletable until those are handled.";

            if (MessageBox.Show(msg, "Confirm Unlink", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _busy = true;
            SetButtons(false);
            try
            {
                await _vm.UnlinkAsync(_email.EmailId, deleteSmtp);
                Action = ResultAction.Unlinked;

                _refs = await _vm.GetReferencesAsync(_email.EmailId);
                RenderRefs();

                if (_refs.Count == 0)
                    MessageBox.Show("All references cleared. You can now delete the email address.",
                        "Unlinked", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error unlinking email address: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _busy = false;
                SetButtons(true);
                RenderRefs();
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;

            if (_refs.Count > 0)
            {
                MessageBox.Show("There are still linked records. Use \"Unlink All\" first.",
                    "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(
                    $"Permanently delete \"{_email.EmailAddress}\"?\n\nThis cannot be undone.",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _busy = true;
            SetButtons(false);
            try
            {
                await _vm.DeleteAsync(_email.EmailId);
                Action = ResultAction.Deleted;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting email address: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _busy = false;
                SetButtons(true);
            }
        }

        private async void BtnDeactivate_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;

            if (MessageBox.Show(
                    $"Deactivate \"{_email.EmailAddress}\"?\n\nIt stays in the system but is marked inactive.",
                    "Confirm Deactivation", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            _busy = true;
            SetButtons(false);
            try
            {
                await _vm.DeactivateAsync(_email.EmailId);
                Action = ResultAction.Deactivated;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deactivating email address: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _busy = false;
                SetButtons(true);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (Action != ResultAction.None)
                DialogResult = true;   // caller reloads the grid
            else
                Close();
        }

        private void SetButtons(bool enabled)
        {
            BtnUnlink.IsEnabled     = enabled && _refs.Count > 0;
            BtnDelete.IsEnabled     = enabled && _refs.Count == 0;
            BtnDeactivate.IsEnabled = enabled;
            BtnClose.IsEnabled      = enabled;
        }
    }
}
