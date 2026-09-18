using System.Collections.Generic;
using System.Windows;
using Yakult.Inventory.App.Pages.Admin.AccountManagement;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs
{
    public partial class EditDeptAccountWindow : Window
    {
        private struct EmailEntry
        {
            public int? EmailId;
            public string Label;
            public override string ToString() => Label;
        }

        public string Username      => TxtUsername.Text.Trim();
        public bool   AccountIsActive => ChkIsActive.IsChecked == true;
        public int?   EmailAddressId
        {
            get
            {
                if (CboEmail.SelectedItem is EmailEntry e && e.EmailId.HasValue)
                    return e.EmailId;
                return null;
            }
        }

        internal EditDeptAccountWindow(DepartmentAccountRowDto row, List<(int EmailId, string Label)> emails)
        {
            InitializeComponent();
            TxtCompany.Text    = row.CompanyName;
            TxtDept.Text       = row.DepartmentName;
            TxtBranch.Text     = row.BranchName;
            TxtUsername.Text   = row.Username ?? "";
            ChkIsActive.IsChecked = row.AccountIsActive;

            CboEmail.Items.Add(new EmailEntry { EmailId = null, Label = "(none)" });
            foreach (var em in emails)
                CboEmail.Items.Add(new EmailEntry { EmailId = em.EmailId, Label = em.Label });
            CboEmail.SelectedIndex = 0;

            if (row.EmailAddressId.HasValue)
            {
                for (int i = 1; i < CboEmail.Items.Count; i++)
                {
                    if (((EmailEntry)CboEmail.Items[i]).EmailId == row.EmailAddressId)
                    {
                        CboEmail.SelectedIndex = i;
                        break;
                    }
                }
            }

            TxtUsername.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtUsername.Text.Trim()))
            { MessageBox.Show("Username is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
