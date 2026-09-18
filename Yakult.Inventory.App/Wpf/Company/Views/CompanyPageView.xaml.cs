using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Pages.Company;
using Yakult.Inventory.App.WPF.Company.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using WinForms = System.Windows.Forms;
using CompanyDto = Yakult.Inventory.App.Pages.CompanyDto;

namespace Yakult.Inventory.App.WPF.Company.Views
{
    public partial class CompanyPageView : UserControl
    {
        private readonly CompanyPageViewModel _vm;

        public CompanyPageView()
        {
            InitializeComponent();

            _vm = new CompanyPageViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.ConfirmYesNo = (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Question) == WinForms.DialogResult.Yes;
            _vm.RequestAddCompany += OnRequestAddCompany;
            _vm.RequestEditCompany += OnRequestEditCompany;
            _vm.RequestArchiveReason += OnRequestArchiveReason;

            RebuildSortByOptions();
            _vm.LoadCompanies();
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestAddCompany()
        {
            using (var dialog = new CompanyDialog())
            {
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    var company = dialog.ResultCompany;
                    if (_vm.SaveCompany(company))
                    {
                        WinForms.MessageBox.Show(GetOwner(), "Company added successfully!", "Success", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                        _vm.LoadCompanies();
                    }
                }
            }
        }

        private void OnRequestEditCompany(CompanyDto companyDto)
        {
            using (var dialog = new CompanyDialog(companyDto))
            {
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    var updatedCompany = dialog.ResultCompany;
                    if (_vm.UpdateCompany(updatedCompany))
                    {
                        WinForms.MessageBox.Show(GetOwner(), "Company updated successfully!", "Success", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                        _vm.LoadCompanies();
                    }
                }
            }
        }

        private string OnRequestArchiveReason(string message)
        {
            using (var dlg = new ArchiveCompanyDialog(message))
            {
                if (dlg.ShowDialog(GetOwner()) == WinForms.DialogResult.OK)
                    return dlg.ReasonText;
                return null;
            }
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool newState = ((CheckBox)sender).IsChecked == true;
            _vm.SetPageSelected(newState);
            CompaniesGrid.Items.Refresh();
            _vm.NotifySelectionChanged();
        }

        private void RowCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (((CheckBox)sender).DataContext is CompanyViewDto company)
                _vm.SetCompanySelected(company.ComId, ((CheckBox)sender).IsChecked == true);
            _vm.NotifySelectionChanged();
        }

        private void SelectedBadge_Click(object sender, RoutedEventArgs e)
        {
            var summaries = _vm.GetSelectedCompanies().Select(c => new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowSummary
            {
                Id = c.ComId,
                Values = new[] { c.ComId.ToString(), c.Name, c.StatusDisplay, c.Description }
            });

            var dialog = new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowsReviewDialog(
                "Selected Companies", "companies",
                new[] { "ID", "Name", "Status", "Description" },
                summaries);
            SetWpfOwner(dialog);
            dialog.ShowDialog();

            foreach (var comId in dialog.RemovedIds)
                _vm.SetCompanySelected(comId, false);

            CompaniesGrid.Items.Refresh();
            _vm.NotifySelectionChanged();
        }

        private static void SetWpfOwner(Window window)
        {
            var owner = GetOwner();
            if (owner != null) new System.Windows.Interop.WindowInteropHelper(window).Owner = owner.Handle;
        }

        private void CompaniesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is CheckBox) return;
                source = VisualTreeHelper.GetParent(source);
            }

            if (_vm.EditCommand.CanExecute(null))
                _vm.EditCommand.Execute(null);
        }

        private void RebuildSortByOptions()
        {
            var options = ListSortHelper.BuildOptions(CompaniesGrid);
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(CompaniesGrid);
            _vm.SetSortByOptions(options, defaultKey);
        }

        private void CompaniesGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            var direction = e.Column.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            _vm.SortByColumn(key, direction);

            foreach (var col in CompaniesGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = direction;
        }
    }
}
