using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Branch;
using Yakult.Inventory.App.WPF.Branch.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Branch.Views
{
    public partial class BranchPageView : UserControl
    {
        private readonly BranchPageViewModel _vm;

        public BranchPageView()
        {
            InitializeComponent();

            _vm = new BranchPageViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.ConfirmYesNo = (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Question) == WinForms.DialogResult.Yes;
            _vm.RequestAddBranch += OnRequestAddBranch;
            _vm.RequestEditBranch += OnRequestEditBranch;
            _vm.RequestClearSortGlyphs += () =>
            {
                foreach (var col in BranchesGrid.Columns) col.SortDirection = null;
            };

            RebuildSortByOptions();
            _vm.LoadBranches();
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestAddBranch()
        {
            using (var dlg = new AddBranchDialog())
            {
                if (dlg.ShowDialog(GetOwner()) == WinForms.DialogResult.OK)
                    _vm.LoadBranches();
            }
        }

        private void OnRequestEditBranch(BranchDto branch)
        {
            using (var dlg = new EditBranchDialog(branch))
            {
                if (dlg.ShowDialog() == WinForms.DialogResult.OK)
                    _vm.LoadBranches();
            }
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool newState = ((CheckBox)sender).IsChecked == true;
            _vm.SetPageSelected(newState);
            BranchesGrid.Items.Refresh();
        }

        private void RowSelectCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (((CheckBox)sender).DataContext is BranchViewDto branch)
                _vm.SetBranchSelected(branch.BranchId, ((CheckBox)sender).IsChecked == true);
        }

        private void SelectedBadge_Click(object sender, RoutedEventArgs e)
        {
            var summaries = _vm.GetSelectedBranches().Select(b => new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowSummary
            {
                Id = b.BranchId,
                Values = new[] { b.BranchId.ToString(), b.Name, b.CompanyName, b.DepartmentName }
            });

            var dialog = new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowsReviewDialog(
                "Selected Branches", "branches",
                new[] { "ID", "Name", "Company", "Department" },
                summaries);
            SetWpfOwner(dialog);
            dialog.ShowDialog();

            foreach (var branchId in dialog.RemovedIds)
                _vm.SetBranchSelected(branchId, false);

            BranchesGrid.Items.Refresh();
        }

        private static void SetWpfOwner(Window window)
        {
            var owner = GetOwner();
            if (owner != null) new System.Windows.Interop.WindowInteropHelper(window).Owner = owner.Handle;
        }

        private void BranchesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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
            var options = ListSortHelper.BuildOptions(BranchesGrid);
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(BranchesGrid);
            _vm.SetSortByOptions(options, defaultKey);
        }

        private void BranchesGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            var direction = e.Column.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            _vm.SortByColumn(key, direction);

            foreach (var col in BranchesGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = direction;
        }
    }

    /// <summary>Page-local: renders a bool as "✔"/"" — matches the original's CellFormatting
    /// override for the Factory/Depot/Center/Distributor columns.</summary>
    public sealed class BoolToCheckmarkConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? "✔" : string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
