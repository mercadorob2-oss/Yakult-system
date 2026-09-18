using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Pages.Admin.AccountManagement;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs
{
    public partial class SelectedEmployeesReviewDialog : Window
    {
        public List<int> RemovedEmpIds { get; } = new List<int>();

        private readonly ObservableCollection<EmployeeManagementDto> _items;

        public SelectedEmployeesReviewDialog(IEnumerable<EmployeeManagementDto> selectedEmployees)
        {
            InitializeComponent();

            _items = new ObservableCollection<EmployeeManagementDto>(selectedEmployees);
            GridSelected.ItemsSource = _items;
            UpdateSubtitle();
        }

        private void UpdateSubtitle()
        {
            TxtSubtitle.Text = $"{_items.Count} employee{(_items.Count == 1 ? "" : "s")} currently selected";
        }

        private void BtnRemoveRow_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is int empId)) return;

            var row = _items.FirstOrDefault(x => x.EmpId == empId);
            if (row == null) return;

            _items.Remove(row);
            RemovedEmpIds.Add(empId);
            UpdateSubtitle();
        }

        private void BtnRemoveAll_Click(object sender, RoutedEventArgs e)
        {
            RemovedEmpIds.AddRange(_items.Select(x => x.EmpId));
            _items.Clear();
            UpdateSubtitle();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
