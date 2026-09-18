using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Admin.AccountManagement;
using Yakult.Inventory.App.Pages.Employee;
using Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs;
using Yakult.Inventory.App.WPF.Employee.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Employee.Views
{
    public partial class EmployeePageView : UserControl
    {
        private readonly EmployeePageViewModel _vm;

        public EmployeePageView()
        {
            InitializeComponent();

            _vm = new EmployeePageViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.ConfirmYesNo = (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Question) == WinForms.DialogResult.Yes;
            _vm.RequestAddEmployee += OnRequestAddEmployee;
            _vm.RequestEditEmployee += OnRequestEditEmployee;

            RebuildSortByOptions();
            _vm.LoadEmployees();
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestAddEmployee()
        {
            using (var dialog = new EmployeeDialog())
            {
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    var employee = dialog.ResultEmployee;
                    if (_vm.SaveEmployee(employee))
                    {
                        WinForms.MessageBox.Show(GetOwner(), "Employee added successfully!", "Success", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                        _vm.LoadEmployees();
                    }
                }
            }
        }

        private void OnRequestEditEmployee(EmployeeDto empDto)
        {
            using (var dialog = new EmployeeDialog(empDto))
            {
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    var updatedEmp = dialog.ResultEmployee;
                    if (_vm.UpdateEmployee(updatedEmp))
                    {
                        WinForms.MessageBox.Show(GetOwner(), "Employee updated successfully!", "Success", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                        _vm.LoadEmployees();
                    }
                }
            }
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool newState = ((CheckBox)sender).IsChecked == true;
            _vm.SetPageSelected(newState);
            EmployeesGrid.Items.Refresh();
        }

        private void RowSelectCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (((CheckBox)sender).DataContext is EmployeeViewDto emp)
                _vm.SetEmployeeSelected(emp.EmpId, ((CheckBox)sender).IsChecked == true);
        }

        private void SelectedBadge_Click(object sender, MouseButtonEventArgs e)
        {
            var summaries = _vm.GetSelectedEmployees().Select(emp => new EmployeeManagementDto
            {
                EmpId = emp.EmpId,
                EmployeeNumber = emp.EmployeeNumber,
                Name = emp.Name,
                Position = emp.Position,
                CompanyName = emp.CompanyName,
                BranchName = emp.BranchName
            });

            var dialog = new SelectedEmployeesReviewDialog(summaries);
            SetWpfOwner(dialog);
            dialog.ShowDialog();

            foreach (var empId in dialog.RemovedEmpIds)
                _vm.SetEmployeeSelected(empId, false);

            EmployeesGrid.Items.Refresh();
        }

        private void EmployeesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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
            var options = ListSortHelper.BuildOptions(EmployeesGrid);
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(EmployeesGrid);
            _vm.SetSortByOptions(options, defaultKey);
        }

        private void EmployeesGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            var direction = e.Column.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            _vm.SortByColumn(key, direction);

            foreach (var col in EmployeesGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = direction;
        }

        private void EmployeesGrid_HeaderButtonClick(object sender, RoutedEventArgs e)
        {
            if (!(e.OriginalSource is Button btn) || btn.Name != "FilterBtn") return;
            string column = btn.Tag?.ToString();
            if (string.IsNullOrEmpty(column)) return;

            var values = _vm.GetUniqueValuesForColumn(column);
            if (values.Count == 0) return;

            var popup = new ColumnFilterWindow(column, values, _vm.GetColumnFilter(column));
            PositionPopupNearButton(popup, btn);
            SetWpfOwner(popup);
            if (popup.ShowDialog() != true) return;

            if (popup.ClearFilter)
                _vm.ClearColumnFilter(column);
            else
                _vm.SetColumnFilter(column, popup.SelectedValues);
        }

        private static void PositionPopupNearButton(Window popup, Button btn)
        {
            try
            {
                var pt = btn.PointToScreen(new Point(0, btn.ActualHeight));
                var source = PresentationSource.FromVisual(btn);
                if (source?.CompositionTarget != null)
                    pt = source.CompositionTarget.TransformFromDevice.Transform(pt);

                var area = SystemParameters.WorkArea;
                double estH = double.IsNaN(popup.Height) ? popup.MaxHeight : popup.Height;
                popup.Left = System.Math.Max(area.Left, System.Math.Min(pt.X, area.Right - popup.Width));
                popup.Top = System.Math.Max(area.Top, System.Math.Min(pt.Y, area.Bottom - estH));
            }
            catch { popup.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
        }

        private static void SetWpfOwner(Window window)
        {
            var owner = GetOwner();
            if (owner != null) new WindowInteropHelper(window).Owner = owner.Handle;
        }
    }
}
