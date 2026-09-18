using System;
using System.Windows;
using Yakult.Inventory.App.Forms.SystemSettings;
using Yakult.Inventory.App.WPF.Admin.EmailManagement.ViewModels;
using Yakult.Inventory.App.WPF.Shared;

namespace Yakult.Inventory.App.WPF.Admin.EmailManagement.Views
{
    public partial class EmailConfigurationWindow : Window
    {
        private readonly EmailConfigurationViewModel _vm;

        public EmailConfigurationWindow()
        {
            InitializeComponent();

            // Clamp to the working monitor's usable area so the dialog can't extend
            // past the taskbar/screen edge on displays smaller than the design size.
            var workArea = SystemParameters.WorkArea;
            MaxHeight = workArea.Height;
            MaxWidth  = workArea.Width;
            if (Height > MaxHeight) Height = MaxHeight;
            if (Width  > MaxWidth)  Width  = MaxWidth;

            _vm = new EmailConfigurationViewModel();

            EmailAddressesTab.Bind(_vm.EmailAddresses);
            EmailAddressesTab.SetRepository(_vm.EmailRepo);

            SmtpProfilesTab.Bind(_vm.SmtpProfiles);
            SmtpProfilesTab.SetRepository(_vm.EmailRepo);

            EmailTemplatesTab.Bind(_vm.EmailTemplates);

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _vm.LoadAllAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading email configuration: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnSendTestEmail_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var profiles  = await _vm.EmailRepo.GetSmtpProfilesAsync(activeOnly: true);
                var templates = await _vm.EmailRepo.GetEmailTemplatesAsync(activeOnly: true);

                if (profiles.Count == 0)
                {
                    MessageBox.Show("No active SMTP profiles found. Please add an SMTP profile first.", "Send Test Email",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (templates.Count == 0)
                {
                    MessageBox.Show("No active email templates found. Please add a template first.", "Send Test Email",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var owner = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                using (var dlg = new SendTestEmailDialog(profiles, templates, _vm.EmailService))
                    dlg.ShowDialog(new Win32WindowWrapper(owner));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Send Test Email", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
