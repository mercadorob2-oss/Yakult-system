using System;
using System.Windows;
using System.Windows.Controls;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs
{
    public partial class CreateDeptAccountWindow : Window
    {
        public string Username { get; private set; }
        public string Password { get; private set; }

        public CreateDeptAccountWindow(string company, string department, string branch, string suggestedUsername = null)
        {
            InitializeComponent();
            TxtInfo.Text = $"{company}  /  {department}  /  {branch}";
            if (!string.IsNullOrWhiteSpace(suggestedUsername))
                TxtUsername.Text = suggestedUsername;
            TxtUsername.Focus();
        }

        private void ChkShow_Changed(object sender, RoutedEventArgs e)
        {
            bool show = ChkShow.IsChecked == true;
            if (show)
            {
                var p1 = PwdPassword.Password;
                var p2 = PwdConfirm.Password;
                PwdPassword.Visibility = Visibility.Collapsed;
                PwdConfirm.Visibility  = Visibility.Collapsed;

                TxtPwdVisible.Text    = p1;
                TxtConfVisible.Text   = p2;
                TxtPwdVisible.Visibility   = Visibility.Visible;
                TxtConfVisible.Visibility  = Visibility.Visible;
            }
            else
            {
                PwdPassword.Password  = TxtPwdVisible.Text;
                PwdConfirm.Password   = TxtConfVisible.Text;
                TxtPwdVisible.Visibility   = Visibility.Collapsed;
                TxtConfVisible.Visibility  = Visibility.Collapsed;
                PwdPassword.Visibility = Visibility.Visible;
                PwdConfirm.Visibility  = Visibility.Visible;
            }
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtUsername.Text.Trim();
            string pwd      = ChkShow.IsChecked == true ? TxtPwdVisible.Text  : PwdPassword.Password;
            string confirm  = ChkShow.IsChecked == true ? TxtConfVisible.Text : PwdConfirm.Password;

            if (string.IsNullOrWhiteSpace(username))
            { MessageBox.Show("Username is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (string.IsNullOrWhiteSpace(pwd))
            { MessageBox.Show("Password is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (pwd != confirm)
            { MessageBox.Show("Passwords do not match.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            Username    = username;
            Password    = pwd;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
