using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Pages.Admin.AccountManagement;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs
{
    public partial class ExportPreviewWindow : Window
    {
        private readonly List<EmployeeManagementDto> _allRows;
        private readonly ObservableCollection<EmployeeManagementDto> _displayRows = new ObservableCollection<EmployeeManagementDto>();

        public ExportPreviewWindow(List<EmployeeManagementDto> rowsToExport, string sourceDescription)
        {
            InitializeComponent();

            _allRows = rowsToExport;
            TxtSubtitle.Text = $"{_allRows.Count} employee(s) will be exported — {sourceDescription}";
            TxtSearch.ToolTip = "Search does not change what gets exported — it only filters this preview.";

            GridPreview.ItemsSource = _displayRows;
            RefreshDisplayRows(string.Empty);
        }

        private void RefreshDisplayRows(string search)
        {
            var trimmed = (search ?? "").Trim().ToLowerInvariant();
            IEnumerable<EmployeeManagementDto> src = _allRows;
            if (!string.IsNullOrEmpty(trimmed))
            {
                src = src.Where(e =>
                    (e.Name           ?? "").ToLowerInvariant().Contains(trimmed) ||
                    (e.EmployeeNumber ?? "").ToLowerInvariant().Contains(trimmed) ||
                    (e.Position       ?? "").ToLowerInvariant().Contains(trimmed) ||
                    (e.CompanyName    ?? "").ToLowerInvariant().Contains(trimmed) ||
                    (e.DepartmentName ?? "").ToLowerInvariant().Contains(trimmed) ||
                    (e.BranchName     ?? "").ToLowerInvariant().Contains(trimmed));
            }

            _displayRows.Clear();
            foreach (var row in src) _displayRows.Add(row);

            TxtRowCount.Text = trimmed.Length == 0
                ? $"{_allRows.Count} row(s)"
                : $"Showing {_displayRows.Count} of {_allRows.Count} row(s) (preview filter only)";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => RefreshDisplayRows(TxtSearch.Text);

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
