using System.Windows;
using System.Windows.Controls;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs
{
    public partial class ChangeDeptPasswordWindow : Window
    {
        public string NewPassword { get; private set; }

        public ChangeDeptPasswordWindow(string company, string department, string branch)
        {
            InitializeComponent();
            TxtCompanyValue.Text = company;
            TxtDeptValue.Text    = department;
            TxtBranchValue.Text  = branch;
            PwdNew.Focus();
        }

        private void ChkShow_Changed(object sender, RoutedEventArgs e)
        {
            bool show = ChkShow.IsChecked == true;
            if (show)
            {
                TxtNewVisible.Text  = PwdNew.Password;
                TxtConfVisible.Text = PwdConfirm.Password;
                PwdNew.Visibility      = Visibility.Collapsed;
                PwdConfirm.Visibility  = Visibility.Collapsed;
                TxtNewVisible.Visibility  = Visibility.Visible;
                TxtConfVisible.Visibility = Visibility.Visible;
            }
            else
            {
                PwdNew.Password     = TxtNewVisible.Text;
                PwdConfirm.Password = TxtConfVisible.Text;
                TxtNewVisible.Visibility  = Visibility.Collapsed;
                TxtConfVisible.Visibility = Visibility.Collapsed;
                PwdNew.Visibility      = Visibility.Visible;
                PwdConfirm.Visibility  = Visibility.Visible;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string pwd     = ChkShow.IsChecked == true ? TxtNewVisible.Text  : PwdNew.Password;
            string confirm = ChkShow.IsChecked == true ? TxtConfVisible.Text : PwdConfirm.Password;

            if (string.IsNullOrWhiteSpace(pwd))
            { MessageBox.Show("Password is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (pwd != confirm)
            { MessageBox.Show("Passwords do not match.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            NewPassword  = pwd;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
