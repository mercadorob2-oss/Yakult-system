using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Pages.Admin.AccountManagement;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs
{
    public partial class EmployeeImportPreviewWindow : Window
    {
        public List<EmployeeImportRow> RowsToApply { get; private set; } = new List<EmployeeImportRow>();
        public bool ArchiveMissing { get; private set; }
        public List<int> ArchiveCandidateEmpIds { get; private set; } = new List<int>();

        public class ExistingRow
        {
            public int    EmpId          { get; set; }
            public string EmployeeNumber { get; set; }
            public string Name           { get; set; }
            public string Position       { get; set; }
            public string CompanyName    { get; set; }
            public string BranchName     { get; set; }
            public string DepartmentName { get; set; }
            public bool   Active         { get; set; }
            public bool   InFile         { get; set; }
            public string InFileText => InFile ? "✓ In File" : "⚠ Not in File";
        }

        private readonly List<EmployeeImportRow> _allRows;
        private readonly List<ExistingRow>        _allExisting;
        private readonly ObservableCollection<EmployeeImportRow> _importView  = new ObservableCollection<EmployeeImportRow>();
        private readonly ObservableCollection<ExistingRow>       _existingView = new ObservableCollection<ExistingRow>();

        public EmployeeImportPreviewWindow(List<EmployeeImportRow> importRows, List<EmployeeManagementDto> existingEmployees, string fileName)
        {
            InitializeComponent();

            _allRows = importRows;
            TxtFileName.Text = $"File: {fileName}";

            // Scope to Company+Branch pairs actually present in the file — a file may cover
            // only one branch/facility of a company (e.g. a single-plant export), and scoping
            // by company alone would pull in every employee from unrelated branches, wrongly
            // flagging them as "missing" and risking their being archived.
            var companyBranchesInFile = new HashSet<string>(
                importRows.Where(r => !string.IsNullOrWhiteSpace(r.CompanyName) && !string.IsNullOrWhiteSpace(r.BranchName))
                          .Select(r => CompanyBranchKey(r.CompanyName, r.BranchName)),
                StringComparer.OrdinalIgnoreCase);

            var importedNumbers = new HashSet<string>(
                importRows.Where(r => !string.IsNullOrWhiteSpace(r.EmployeeNumber)).Select(r => r.EmployeeNumber.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var importedNameCompany = new HashSet<string>(
                importRows.Where(r => string.IsNullOrWhiteSpace(r.EmployeeNumber) && !string.IsNullOrWhiteSpace(r.Name))
                          .Select(r => NameCompanyKey(r.Name, r.CompanyName)),
                StringComparer.OrdinalIgnoreCase);

            _allExisting = existingEmployees
                .Where(e => !e.IsArchived && companyBranchesInFile.Contains(CompanyBranchKey(e.CompanyName, e.BranchName)))
                .Select(e => new ExistingRow
                {
                    EmpId          = e.EmpId,
                    EmployeeNumber = e.EmployeeNumber,
                    Name           = e.Name,
                    Position       = e.Position,
                    CompanyName    = e.CompanyName,
                    BranchName     = e.BranchName,
                    DepartmentName = e.DepartmentName,
                    Active         = e.Active,
                    InFile         = !string.IsNullOrWhiteSpace(e.EmployeeNumber)
                        ? importedNumbers.Contains(e.EmployeeNumber.Trim())
                        : importedNameCompany.Contains(NameCompanyKey(e.Name, e.CompanyName))
                })
                .OrderBy(e => e.Name)
                .ToList();

            foreach (var r in _allRows)     _importView.Add(r);
            foreach (var r in _allExisting) _existingView.Add(r);

            GridImport.ItemsSource   = _importView;
            GridExisting.ItemsSource = _existingView;

            RefreshSummary();
        }

        private static string NameCompanyKey(string name, string company)
            => ((name ?? "").Trim() + "|" + (company ?? "").Trim()).ToUpperInvariant();

        private static string CompanyBranchKey(string company, string branch)
            => ((company ?? "").Trim() + "|" + (branch ?? "").Trim()).ToUpperInvariant();

        private void RefreshSummary()
        {
            int n       = _allRows.Count(r => r.Status == EmployeeImportStatus.New);
            int u       = _allRows.Count(r => r.Status == EmployeeImportStatus.Update);
            int s       = _allRows.Count(r => r.Status == EmployeeImportStatus.Unchanged);
            int i       = _allRows.Count(r => r.Status == EmployeeImportStatus.Invalid);
            int missing = _allExisting.Count(e => !e.InFile);

            TxtSummary.Text = $"{_allRows.Count} rows in file  •  New {n}  •  Update {u}  •  Unchanged {s}  •  Invalid {i}  •  {missing} existing not in file";
        }

        private void TxtSearchImport_TextChanged(object sender, TextChangedEventArgs e)
        {
            string term = (TxtSearchImport.Text ?? "").Trim().ToLowerInvariant();
            IEnumerable<EmployeeImportRow> src = _allRows;
            if (!string.IsNullOrEmpty(term))
                src = src.Where(r =>
                    (r.Name ?? "").ToLowerInvariant().Contains(term) ||
                    (r.EmployeeNumber ?? "").ToLowerInvariant().Contains(term) ||
                    (r.CompanyName ?? "").ToLowerInvariant().Contains(term) ||
                    (r.BranchName ?? "").ToLowerInvariant().Contains(term));

            _importView.Clear();
            foreach (var r in src) _importView.Add(r);
        }

        private void TxtSearchExisting_TextChanged(object sender, TextChangedEventArgs e)
        {
            string term = (TxtSearchExisting.Text ?? "").Trim().ToLowerInvariant();
            IEnumerable<ExistingRow> src = _allExisting;
            if (!string.IsNullOrEmpty(term))
                src = src.Where(r =>
                    (r.Name ?? "").ToLowerInvariant().Contains(term) ||
                    (r.EmployeeNumber ?? "").ToLowerInvariant().Contains(term) ||
                    (r.CompanyName ?? "").ToLowerInvariant().Contains(term) ||
                    (r.BranchName ?? "").ToLowerInvariant().Contains(term));

            _existingView.Clear();
            foreach (var r in src) _existingView.Add(r);
        }

        private void GridImport_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(GridImport.SelectedItem is EmployeeImportRow row) || !row.MatchedEmpId.HasValue) return;
            var match = _existingView.FirstOrDefault(x => x.EmpId == row.MatchedEmpId.Value);
            if (match == null) return;
            GridExisting.SelectedItem = match;
            GridExisting.ScrollIntoView(match);
        }

        private void BtnSelectAllValid_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in _allRows.Where(r => r.Status == EmployeeImportStatus.New || r.Status == EmployeeImportStatus.Update))
                r.IsIncluded = true;
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in _allRows) r.IsIncluded = false;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            var candidateRows = _allRows
                .Where(r => r.IsIncluded && (r.Status == EmployeeImportStatus.New || r.Status == EmployeeImportStatus.Update))
                .ToList();

            var missing = _allExisting.Where(x => !x.InFile).ToList();

            if (candidateRows.Count == 0 && missing.Count == 0)
            {
                MessageBox.Show("Select at least one row to import.",
                    "Nothing to Import", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var newRows    = candidateRows.Where(r => r.Status == EmployeeImportStatus.New).ToList();
            var updateRows = candidateRows.Where(r => r.Status == EmployeeImportStatus.Update).ToList();

            var summary = new EmployeeImportSummaryWindow(TxtFileName.Text, newRows, updateRows, missing, archiveMissing: false)
            {
                Owner = this
            };
            if (summary.ShowDialog() != true) return;

            RowsToApply            = candidateRows;
            ArchiveCandidateEmpIds = summary.ArchiveEmpIds;
            ArchiveMissing          = ArchiveCandidateEmpIds.Count > 0;

            DialogResult = true;
        }
    }
}
