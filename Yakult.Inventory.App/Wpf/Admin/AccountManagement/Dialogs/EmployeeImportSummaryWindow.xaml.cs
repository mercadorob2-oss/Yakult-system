using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs
{
    public partial class EmployeeImportSummaryWindow : Window
    {
        public class MissingRow : INotifyPropertyChanged
        {
            public int    EmpId          { get; set; }
            public string Name           { get; set; }
            public string Position       { get; set; }
            public string CompanyName    { get; set; }
            public string BranchName     { get; set; }

            public string ActionText => IsSelected ? "Will Archive" : "Left As-Is";

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActionText)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public List<int> ArchiveEmpIds { get; private set; } = new List<int>();

        private readonly List<MissingRow> _missingRows;
        private readonly int _totalRows;
        private readonly int _addUpdateCount;

        public EmployeeImportSummaryWindow(
            string fileName,
            List<EmployeeImportRow> newRows,
            List<EmployeeImportRow> updateRows,
            List<EmployeeImportPreviewWindow.ExistingRow> missingRows,
            bool archiveMissing)
        {
            InitializeComponent();

            _totalRows      = missingRows.Count;
            _addUpdateCount = newRows.Count + updateRows.Count;

            TxtSubtitle.Text = $"{fileName}  •  {newRows.Count} to add  •  {updateRows.Count} to update  •  {missingRows.Count} not in file";

            TabAdd.Header     = $"Add ({newRows.Count})";
            TabUpdate.Header  = $"Update ({updateRows.Count})";
            TabMissing.Header = $"Not In File ({missingRows.Count})";

            GridAdd.ItemsSource    = newRows;
            GridUpdate.ItemsSource = updateRows;

            _missingRows = missingRows
                .Select(m => new MissingRow
                {
                    EmpId       = m.EmpId,
                    Name        = m.Name,
                    Position    = m.Position,
                    CompanyName = m.CompanyName,
                    BranchName  = m.BranchName,
                    IsSelected  = archiveMissing
                })
                .ToList();

            foreach (var row in _missingRows)
                row.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(MissingRow.IsSelected)) RefreshMissingCount(); };

            GridMissing.ItemsSource = _missingRows;
            RefreshMissingCount();
        }

        private void RefreshMissingCount()
        {
            int selected = _missingRows.Count(r => r.IsSelected);

            if (_totalRows == 0)
            {
                TxtMissingCount.Text = "No existing employees are missing from this file.";
                TxtFooterNote.Text   = "";
                return;
            }

            TxtMissingCount.Text = $"{selected} of {_totalRows} selected to archive";

            TxtFooterNote.Text = selected > 0
                ? $"{selected} existing employee(s) not found in this file will be archived (e.g. resigned/transferred out)."
                : $"{_totalRows} existing employee(s) not found in this file will be left as-is. Check the box next to anyone who resigned or transferred out to archive them.";
        }

        private void BtnSelectAllMissing_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _missingRows) row.IsSelected = true;
        }

        private void BtnClearAllMissing_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _missingRows) row.IsSelected = false;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            ArchiveEmpIds = _missingRows.Where(r => r.IsSelected).Select(r => r.EmpId).ToList();

            if (_addUpdateCount == 0 && ArchiveEmpIds.Count == 0)
            {
                MessageBox.Show("Nothing selected to import or archive.", "Nothing to Do",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
        }
    }
}
