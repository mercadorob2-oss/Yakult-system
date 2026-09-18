using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs
{
    public partial class ShowDeptPasswordWindow : Window
    {
        private readonly string _password;
        private DispatcherTimer _copyResetTimer;

        public ShowDeptPasswordWindow(string company, string department, string branch,
                                      string username, string password)
        {
            InitializeComponent();
            TxtCompany.Text  = company;
            TxtDept.Text     = department;
            TxtBranch.Text   = branch;
            TxtUsername.Text = username;
            TxtPassword.Text = password;
            _password        = password;
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Clipboard.SetText(_password);
            BtnCopy.Content    = "Copied!";
            BtnCopy.Background = new SolidColorBrush(Color.FromRgb(46, 125, 50));

            _copyResetTimer?.Stop();
            _copyResetTimer          = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _copyResetTimer.Tick    += (s, args) =>
            {
                _copyResetTimer.Stop();
                BtnCopy.Content    = "Copy";
                BtnCopy.Background = new SolidColorBrush(Color.FromRgb(78, 154, 252));
            };
            _copyResetTimer.Start();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
