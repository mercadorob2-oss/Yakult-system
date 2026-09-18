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
    public partial class EmailTemplatesTabView : UserControl
    {
        private EmailTemplatesViewModel _vm;

        public EmailTemplatesTabView()
        {
            InitializeComponent();
        }

        public void Bind(EmailTemplatesViewModel vm)
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
                MessageBox.Show($"Error loading templates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                var profiles = await _vm.GetSmtpProfilesAsync();
                using (var dlg = new EmailTemplateEditorDialog(null, profiles))
                {
                    if (dlg.ShowDialog(Owner()) == System.Windows.Forms.DialogResult.OK)
                    {
                        await _vm.SaveAsync(dlg.Template);
                        MessageBox.Show("Template saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        await _vm.LoadAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selected = MainGrid.SelectedItem as EmailTemplateDto;
            if (selected == null)
            {
                MessageBox.Show("Please select a template to edit.", "Edit Template", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var profiles = await _vm.GetSmtpProfilesAsync();
                using (var dlg = new EmailTemplateEditorDialog(selected, profiles))
                {
                    if (dlg.ShowDialog(Owner()) == System.Windows.Forms.DialogResult.OK)
                    {
                        await _vm.SaveAsync(dlg.Template);
                        MessageBox.Show("Template updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        await _vm.LoadAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            var selected = MainGrid.SelectedItem as EmailTemplateDto;
            if (selected == null)
            {
                MessageBox.Show("Please select a template to preview.", "Preview HTML", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var dlg = new EmailTemplateHtmlPreviewDialog(selected))
                {
                    dlg.ShowDialog(Owner());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening preview: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnFirst_Click(object sender, RoutedEventArgs e) => _vm.GoToFirstPage();
        private void BtnPrev_Click(object sender, RoutedEventArgs e)  => _vm.GoToPrevPage();
        private void BtnNext_Click(object sender, RoutedEventArgs e)  => _vm.GoToNextPage();
        private void BtnLast_Click(object sender, RoutedEventArgs e)  => _vm.GoToLastPage();
    }
}
