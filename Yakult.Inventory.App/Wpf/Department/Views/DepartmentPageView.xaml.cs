using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Department;
using Yakult.Inventory.App.WPF.Department.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Department.Views
{
    public partial class DepartmentPageView : UserControl
    {
        private readonly DepartmentPageViewModel _vm;

        public DepartmentPageView()
        {
            InitializeComponent();

            _vm = new DepartmentPageViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.ConfirmYesNo = (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Question) == WinForms.DialogResult.Yes;
            _vm.RequestAddDepartment += OnRequestAddDepartment;
            _vm.RequestEditDepartment += OnRequestEditDepartment;
            _vm.RequestClearSortGlyphs += () =>
            {
                foreach (var col in DepartmentsGrid.Columns) col.SortDirection = null;
            };

            RebuildSortByOptions();
            _vm.LoadDepartments();
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestAddDepartment()
        {
            using (var dialog = new AddDepartmentDialog())
            {
                if (dialog.ShowDialog(GetOwner()) == WinForms.DialogResult.OK)
                    _vm.LoadDepartments();
            }
        }

        private void OnRequestEditDepartment(DepartmentDto deptDto)
        {
            using (var dialog = new DepartmentDialog(deptDto))
            {
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    var updatedDept = dialog.ResultDepartment;
                    if (_vm.UpdateDepartment(updatedDept))
                    {
                        WinForms.MessageBox.Show(GetOwner(), "Department updated successfully!", "Success", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                        _vm.LoadDepartments();
                    }
                }
            }
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool newState = ((CheckBox)sender).IsChecked == true;
            _vm.SetPageSelected(newState);
            DepartmentsGrid.Items.Refresh();
        }

        private void RowSelectCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (((CheckBox)sender).DataContext is DepartmentViewDto dept)
                _vm.SetDepartmentSelected(dept.DeptId, ((CheckBox)sender).IsChecked == true);
        }

        private void SelectedBadge_Click(object sender, RoutedEventArgs e)
        {
            var summaries = _vm.GetSelectedDepartments().Select(d => new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowSummary
            {
                Id = d.DeptId,
                Values = new[] { d.DeptId.ToString(), d.Name, d.Section, d.Description }
            });

            var dialog = new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowsReviewDialog(
                "Selected Departments", "departments",
                new[] { "ID", "Name", "Section", "Description" },
                summaries);
            SetWpfOwner(dialog);
            dialog.ShowDialog();

            foreach (var deptId in dialog.RemovedIds)
                _vm.SetDepartmentSelected(deptId, false);

            DepartmentsGrid.Items.Refresh();
        }

        private static void SetWpfOwner(Window window)
        {
            var owner = GetOwner();
            if (owner != null) new System.Windows.Interop.WindowInteropHelper(window).Owner = owner.Handle;
        }

        private void DepartmentsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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
            var options = ListSortHelper.BuildOptions(DepartmentsGrid);
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(DepartmentsGrid);
            _vm.SetSortByOptions(options, defaultKey);
        }

        private void DepartmentsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            var direction = e.Column.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            _vm.SortByColumn(key, direction);

            foreach (var col in DepartmentsGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = direction;
        }
    }
}
