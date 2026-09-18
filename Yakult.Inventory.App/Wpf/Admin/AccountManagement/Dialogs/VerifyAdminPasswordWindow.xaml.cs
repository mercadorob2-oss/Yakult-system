using System.Windows;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs
{
    public partial class VerifyAdminPasswordWindow : Window
    {
        public string EnteredPassword => PwdEntry.Password;

        public VerifyAdminPasswordWindow(string currentUsername)
        {
            InitializeComponent();
            TxtPrompt.Text = $"Enter your password to view the account password ({currentUsername}):";
            PwdEntry.Focus();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PwdEntry.Password))
            { MessageBox.Show("Please enter your password.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
