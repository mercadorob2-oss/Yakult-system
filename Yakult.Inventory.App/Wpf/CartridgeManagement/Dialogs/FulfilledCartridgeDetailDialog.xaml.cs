using System.Windows;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Dialogs
{
    public partial class FulfilledCartridgeDetailDialog : Window
    {
        public FulfilledCartridgeDetailDialog(FulfilledCartridgeDetailDto detail)
        {
            InitializeComponent();

            TxtTitle.Text          = $"Fulfilled Request Detail — {detail.SetCode}";
            TxtSetCode.Text        = detail.SetCode;
            TxtFulfilledAt.Text    = detail.FulfilledAtDisplay;
            TxtRequester.Text      = detail.RequesterName;
            TxtReceivedBy.Text     = detail.ReceivedByName;
            TxtCompany.Text        = detail.CompanyName;
            TxtBranch.Text         = detail.BranchName;
            TxtDepartment.Text     = detail.DepartmentName;
            TxtTotalIssuedQty.Text = detail.TotalIssuedQty.ToString();

            ModelsGrid.ItemsSource = detail.Models;
            TxtEmptyState.Visibility = detail.Models.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
