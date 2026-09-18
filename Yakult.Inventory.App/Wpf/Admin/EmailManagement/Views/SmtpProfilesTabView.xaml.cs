using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Yakult.Inventory.App.Forms.SystemSettings;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.Admin.EmailManagement.ViewModels;
using Yakult.Inventory.App.WPF.Shared;

namespace Yakult.Inventory.App.WPF.Admin.EmailManagement.Views
{
    public partial class SmtpProfilesTabView : UserControl
    {
        private SmtpProfilesViewModel _vm;

        public SmtpProfilesTabView()
        {
            InitializeComponent();
        }

        public void Bind(SmtpProfilesViewModel vm)
        {
            _vm = vm;
            DataContext = vm;
        }

        private System.Windows.Forms.IWin32Window Owner()
            => new Win32WindowWrapper(new WindowInteropHelper(Window.GetWindow(this)).Handle);

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try { await _vm.LoadAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading SMTP profiles: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (MainGrid.SelectedItem != null) BtnEdit_Click(sender, e);
        }

        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dlg = new SystemSmtpProfileDialog(null, RepoOf()))
                {
                    if (dlg.ShowDialog(Owner()) == System.Windows.Forms.DialogResult.OK)
                    {
                        await _vm.SaveAsync(dlg.Profile);
                        MessageBox.Show("SMTP Profile saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        await _vm.LoadAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving SMTP profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selected = MainGrid.SelectedItem as SystemSmtpProfileDto;
            if (selected == null)
            {
                MessageBox.Show("Please select a profile to edit.", "Edit Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var profile = await _vm.GetByIdAsync(selected.ProfileId);
                using (var dlg = new SystemSmtpProfileDialog(profile, RepoOf()))
                {
                    if (dlg.ShowDialog(Owner()) == System.Windows.Forms.DialogResult.OK)
                    {
                        await _vm.SaveAsync(dlg.Profile);
                        MessageBox.Show("SMTP Profile updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        await _vm.LoadAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating SMTP profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Yakult.Inventory.App.Repositories.EmailRepository _emailRepo;
        public void SetRepository(Yakult.Inventory.App.Repositories.EmailRepository repo) => _emailRepo = repo;
        private Yakult.Inventory.App.Repositories.EmailRepository RepoOf() => _emailRepo;

        private void BtnFirst_Click(object sender, RoutedEventArgs e) => _vm.GoToFirstPage();
        private void BtnPrev_Click(object sender, RoutedEventArgs e)  => _vm.GoToPrevPage();
        private void BtnNext_Click(object sender, RoutedEventArgs e)  => _vm.GoToNextPage();
        private void BtnLast_Click(object sender, RoutedEventArgs e)  => _vm.GoToLastPage();
    }
}
