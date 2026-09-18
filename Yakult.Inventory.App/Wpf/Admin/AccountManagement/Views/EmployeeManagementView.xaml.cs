using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ClosedXML.Excel;
using ExcelDataReader;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages.Admin.AccountManagement;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs;
using Yakult.Inventory.App.WPF.Admin.AccountManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Views
{
    public partial class EmployeeManagementView : UserControl
    {
        private readonly EmployeeManagementViewModel _vm;
        private string _editOriginalValue = string.Empty;
        private System.Windows.Controls.CheckBox _selectAllHeaderChk;

        public EmployeeManagementView()
        {
            InitializeComponent();
            _vm         = new EmployeeManagementViewModel();
            DataContext  = _vm;
            Loaded      += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = _vm.LoadAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            foreach (var col in MainGrid.Columns)
                col.SortDirection = null;
            try { await _vm.LoadAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing data:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CboFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || CboFilter.SelectedItem == null) return;
            _vm.FilterOption = ((ComboBoxItem)CboFilter.SelectedItem).Content?.ToString() ?? "All";
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dialog = new Pages.Employee.EmployeeDialog())
                {
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var emp = dialog.ResultEmployee;
                        SaveNewEmployee(emp);
                        _ = _vm.LoadAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open Add Employee dialog:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveNewEmployee(Pages.EmployeeDto emp)
        {
            try
            {
                using (var con = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand(@"
                        INSERT INTO dbo.Employee (Name, Position, Description, DateCreated, Createdby, ComId, DeptId, BranchId, EmployeeNumber, TitleId)
                        VALUES (@Name, @Position, @Description, @DateCreated, @CreatedBy, @ComId, @DeptId, @BranchId, @EmployeeNumber, @TitleId)", con))
                    {
                        cmd.Parameters.AddWithValue("@Name",           emp.Name);
                        cmd.Parameters.AddWithValue("@Position",       (object)emp.Position       ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Description",    (object)emp.Description    ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DateCreated",    emp.DateCreated);
                        cmd.Parameters.AddWithValue("@CreatedBy",      emp.CreatedByUserId);
                        cmd.Parameters.AddWithValue("@ComId",          emp.CompanyId);
                        cmd.Parameters.AddWithValue("@DeptId",         emp.DepartmentId.HasValue ? (object)emp.DepartmentId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@BranchId",       emp.BranchId);
                        cmd.Parameters.AddWithValue("@EmployeeNumber", string.IsNullOrWhiteSpace(emp.EmployeeNumber) ? (object)DBNull.Value : emp.EmployeeNumber);
                        cmd.Parameters.AddWithValue("@TitleId",        emp.TitleId.HasValue ? (object)emp.TitleId.Value : DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save employee:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selected = MainGrid.SelectedItem as EmployeeManagementDto;
            if (selected == null)
            {
                MessageBox.Show("Please select an employee to edit.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var emp = LoadEmployeeForEdit(selected.EmpId);
            if (emp == null) return;

            using (var dialog = new Pages.Employee.EmployeeDialog(emp))
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SaveEmployeeUpdate(dialog.ResultEmployee);
                    _ = _vm.LoadAsync();
                }
            }
        }

        private Pages.EmployeeDto LoadEmployeeForEdit(int empId)
        {
            try
            {
                using (var con = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand(@"
                        SELECT e.EmpId, e.Name, e.Position, e.Description, e.DateCreated,
                               e.ComId, e.DeptId, e.BranchId, u.Name AS CreatedByName,
                               e.EmployeeNumber, e.TitleId,
                               CASE WHEN arc.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS IsArchived
                        FROM dbo.Employee e
                        LEFT JOIN dbo.[User] u ON e.Createdby = u.UserId
                        LEFT JOIN dbo.ArchiveStatus arc
                               ON arc.EntityType = 'Employee' AND arc.EntityId = e.EmpId AND arc.IsArchived = 1
                        WHERE e.EmpId = @EmpId", con))
                    {
                        cmd.Parameters.AddWithValue("@EmpId", empId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Pages.EmployeeDto
                                {
                                    EmpId          = reader.GetInt32(0),
                                    Name           = reader.GetString(1),
                                    Position       = reader.IsDBNull(2)  ? null      : reader.GetString(2),
                                    Description    = reader.IsDBNull(3)  ? null      : reader.GetString(3),
                                    DateCreated    = reader.GetDateTime(4),
                                    CompanyId      = reader.GetInt32(5),
                                    DepartmentId   = reader.IsDBNull(6)  ? (int?)null : reader.GetInt32(6),
                                    BranchId       = reader.GetInt32(7),
                                    CreatedByName  = reader.IsDBNull(8)  ? "N/A"     : reader.GetString(8),
                                    EmployeeNumber = reader.IsDBNull(9)  ? null      : reader.GetString(9),
                                    TitleId        = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10),
                                    IsArchived     = !reader.IsDBNull(11) && reader.GetInt32(11) == 1
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load employee:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }

        private void SaveEmployeeUpdate(Pages.EmployeeDto emp)
        {
            try
            {
                using (var con = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand(@"
                        UPDATE dbo.Employee
                        SET Name           = @Name,
                            Position       = @Position,
                            Description    = @Description,
                            ComId          = @ComId,
                            DeptId         = @DeptId,
                            BranchId       = @BranchId,
                            EmployeeNumber = @EmployeeNumber,
                            TitleId        = @TitleId
                        WHERE EmpId = @EmpId", con))
                    {
                        cmd.Parameters.AddWithValue("@EmpId",          emp.EmpId);
                        cmd.Parameters.AddWithValue("@Name",           emp.Name);
                        cmd.Parameters.AddWithValue("@Position",       (object)emp.Position    ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Description",    (object)emp.Description ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ComId",          emp.CompanyId);
                        cmd.Parameters.AddWithValue("@DeptId",         emp.DepartmentId.HasValue ? (object)emp.DepartmentId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@BranchId",       emp.BranchId);
                        cmd.Parameters.AddWithValue("@EmployeeNumber", string.IsNullOrWhiteSpace(emp.EmployeeNumber) ? (object)DBNull.Value : emp.EmployeeNumber);
                        cmd.Parameters.AddWithValue("@TitleId",        emp.TitleId.HasValue ? (object)emp.TitleId.Value : DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save employee:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChkSelectAllHeader_Loaded(object sender, RoutedEventArgs e)
        {
            _selectAllHeaderChk = sender as System.Windows.Controls.CheckBox;
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is System.Windows.Controls.CheckBox chk)) return;
            bool select = chk.IsChecked == true;
            foreach (var row in _vm.PagedRows)
                row.IsSelected = select;
        }

        private async void BtnArchive_Click(object sender, RoutedEventArgs e)
        {
            var toArchive = _vm.AllRows.Where(r => r.IsSelected && !r.IsArchived).ToList();
            if (toArchive.Count == 0)
            {
                MessageBox.Show("Please check at least one employee to archive.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string detail = toArchive.Count == 1
                ? $"Name: {toArchive[0].Name}\nPosition: {toArchive[0].Position ?? "N/A"}\nCompany: {toArchive[0].CompanyName}"
                : $"{toArchive.Count} employees selected:\n" +
                  string.Join("\n", toArchive.Take(5).Select(r => $"  • {r.Name}")) +
                  (toArchive.Count > 5 ? $"\n  … and {toArchive.Count - 5} more" : "");

            string noun = toArchive.Count == 1 ? "employee" : "employees";
            if (MessageBox.Show(
                    $"Are you sure you want to archive the following {noun}?\n\n{detail}\n\nThey will be moved to the archive.",
                    "Confirm Archive", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                string archivedBy = AppSession.CurrentUserName ?? "System";
                foreach (var emp in toArchive)
                    await _vm.ArchiveEmployeeAsync(emp.EmpId, archivedBy);

                MessageBox.Show($"{toArchive.Count} {noun} archived successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                await _vm.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to archive:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectedBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dialog = new SelectedEmployeesReviewDialog(_vm.GetSelectedItems());
            SetWpfOwner(dialog);
            dialog.ShowDialog();

            foreach (var empId in dialog.RemovedEmpIds)
                _vm.SetSelected(empId, false);
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            List<EmployeeManagementDto> toExport;

            var checkedRows = _vm.GetSelectedItems();
            if (checkedRows.Count > 0)
            {
                // Explicit row checkboxes take priority over the branch/status picker —
                // if the user hand-picked employees, export exactly those.
                toExport = checkedRows;
            }
            else
            {
                // Nothing checked: fall back to the current on-screen filter (search text,
                // status dropdown, column header filters, Show Archived), narrowed further
                // by the branch/status picker below.
                var baseRows = _vm.GetFilteredRows();

                if (baseRows == null || baseRows.Count == 0)
                {
                    MessageBox.Show("No data to export — the current search/filter has no matches.",
                        "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var branches = baseRows
                    .Where(r => !string.IsNullOrWhiteSpace(r.BranchName))
                    .Select(r => r.BranchName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                int archivedCount = baseRows.Count(r => r.IsArchived);

                var filterDialog = new ExportFilterDialog(branches, baseRows.Count, archivedCount);
                SetWpfOwner(filterDialog);
                if (filterDialog.ShowDialog() != true) return;

                toExport = baseRows
                    .Where(r => !string.IsNullOrWhiteSpace(r.BranchName) && filterDialog.SelectedBranches.Contains(r.BranchName))
                    .Where(r => filterDialog.IncludeArchived || !r.IsArchived)
                    .ToList();

                if (toExport.Count == 0)
                {
                    MessageBox.Show("No employees match the selected branches/status.", "Export",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            string sourceDescription = checkedRows.Count > 0
                ? "from rows checked in the grid"
                : "matching your current filter and branch/status selection";

            var preview = new ExportPreviewWindow(toExport, sourceDescription);
            SetWpfOwner(preview);
            if (preview.ShowDialog() != true) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName   = $"Employees_{DateTime.Now:yyyyMMdd_HHmm}",
                DefaultExt = ".xlsx",
                Filter     = "Excel Workbook|*.xlsx"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                using (var wb = new XLWorkbook())
                {
                    var ws = wb.Worksheets.Add("Employees");
                    string[] headers = { "Emp #", "Title", "Name", "Position", "Company", "Department", "Branch", "Status", "Dept Email", "Branch Email", "Personal Email" };
                    for (int c = 0; c < headers.Length; c++)
                    {
                        var cell = ws.Cell(1, c + 1);
                        cell.Value = headers[c];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4E9AFC");
                        cell.Style.Font.FontColor       = XLColor.White;
                    }

                    int row = 2;
                    foreach (var emp in toExport)
                    {
                        ws.Cell(row, 1).Value  = emp.EmployeeNumber ?? "";
                        ws.Cell(row, 2).Value  = emp.TitleCode ?? "";
                        ws.Cell(row, 3).Value  = emp.Name ?? "";
                        ws.Cell(row, 4).Value  = emp.Position ?? "";
                        ws.Cell(row, 5).Value  = emp.CompanyName ?? "";
                        ws.Cell(row, 6).Value  = emp.DepartmentName ?? "";
                        ws.Cell(row, 7).Value  = emp.BranchName ?? "";
                        ws.Cell(row, 8).Value  = emp.ActiveDisplay;
                        ws.Cell(row, 9).Value  = emp.DepartmentEmail ?? "";
                        ws.Cell(row, 10).Value = emp.BranchEmail ?? "";
                        ws.Cell(row, 11).Value = emp.PrimaryEmail ?? "";
                        row++;
                    }

                    // Explicit widths — AdjustToContents triggers a SixLabors.Fonts version conflict
                    int[] colWidths = { 10, 10, 30, 25, 22, 22, 22, 12, 30, 30, 30 };
                    for (int c = 0; c < colWidths.Length; c++)
                        ws.Column(c + 1).Width = colWidths[c];
                    wb.SaveAs(dlg.FileName);
                }

                MessageBox.Show($"Exported {toExport.Count} employees to:\n{dlg.FileName}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Select Employee Import File",
                Filter = "Excel or CSV|*.xlsx;*.csv|Excel Workbook|*.xlsx|CSV File|*.csv|All Files|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            List<EmployeeImportRow> rows;
            try
            {
                rows = ParseImportFile(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read import file:\n{ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (rows.Count == 0)
            {
                MessageBox.Show("No data rows found in the file.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                CrossCheckAgainstExisting(rows);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to cross-check against existing employees:\n{ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var preview = new EmployeeImportPreviewWindow(rows, _vm.AllRows, Path.GetFileName(dlg.FileName));
            SetWpfOwner(preview);
            if (preview.ShowDialog() != true) return;

            try
            {
                var result = ExecuteImport(preview.RowsToApply, preview.ArchiveMissing ? preview.ArchiveCandidateEmpIds : null);
                MessageBox.Show(
                    $"Import complete.\nInserted: {result.Inserted}  |  Updated: {result.Updated}  |  Archived: {result.Archived}  |  Errors: {result.Errors}",
                    "Import Result", MessageBoxButton.OK, MessageBoxImage.Information);
                _ = _vm.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Import: file parsing (xlsx / csv) ──────────────────────────────────

        private List<EmployeeImportRow> ParseImportFile(string path)
        {
            var rows = new List<EmployeeImportRow>();
            string ext = Path.GetExtension(path)?.ToLowerInvariant();

            if (ext == ".csv")
            {
                var lines = ParseCsv(path);
                if (lines.Count < 2) return rows;

                var headers  = lines[0].Select(h => h.Trim()).ToArray();
                int idxNum   = GetHeaderIndex(headers, "Emp #", "Employee Number");
                int idxTitle = GetHeaderIndex(headers, "Title");
                int idxName  = GetHeaderIndex(headers, "Name*", "Name");
                int idxPos   = GetHeaderIndex(headers, "Position");
                int idxCo    = GetHeaderIndex(headers, "Company*", "Company");
                int idxDept  = GetHeaderIndex(headers, "Department");
                int idxBr    = GetHeaderIndex(headers, "Branch*", "Branch");
                int idxActive = GetHeaderIndex(headers, "Active (TRUE/FALSE)", "Active");

                for (int r = 1; r < lines.Count; r++)
                {
                    var f = lines[r];
                    string Get(int idx) => idx >= 0 && idx < f.Length ? f[idx].Trim() : "";

                    string name = Get(idxName);
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    rows.Add(new EmployeeImportRow
                    {
                        RowNumber      = r + 1,
                        EmployeeNumber = Get(idxNum),
                        TitleText      = Get(idxTitle),
                        Name           = name,
                        Position       = Get(idxPos),
                        CompanyName    = Get(idxCo),
                        DepartmentName = Get(idxDept),
                        BranchName     = Get(idxBr),
                        Active         = !string.Equals(Get(idxActive), "FALSE", StringComparison.OrdinalIgnoreCase)
                    });
                }
            }
            else
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var ds = reader.AsDataSet(new ExcelDataSetConfiguration
                    {
                        ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                    });
                    if (ds.Tables.Count == 0) return rows;
                    var table = ds.Tables[0];

                    string GetCol(System.Data.DataRow row, params string[] names)
                    {
                        foreach (var n in names)
                            if (table.Columns.Contains(n))
                                return row[n]?.ToString()?.Trim() ?? "";
                        return "";
                    }

                    int rowNum = 1;
                    foreach (System.Data.DataRow row in table.Rows)
                    {
                        rowNum++;
                        string name = GetCol(row, "Name*", "Name");
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        rows.Add(new EmployeeImportRow
                        {
                            RowNumber      = rowNum,
                            EmployeeNumber = GetCol(row, "Emp #", "Employee Number"),
                            TitleText      = GetCol(row, "Title"),
                            Name           = name,
                            Position       = GetCol(row, "Position"),
                            CompanyName    = GetCol(row, "Company*", "Company"),
                            DepartmentName = GetCol(row, "Department"),
                            BranchName     = GetCol(row, "Branch*", "Branch"),
                            Active         = !string.Equals(GetCol(row, "Active (TRUE/FALSE)", "Active"), "FALSE", StringComparison.OrdinalIgnoreCase)
                        });
                    }
                }
            }

            return rows;
        }

        private static List<string[]> ParseCsv(string path)
        {
            var rows = new List<string[]>();
            // Excel commonly exports CSV as Windows-1252 (ANSI), not UTF-8 — e.g. "ñ" in
            // branch names like "BIÑAN CENTER" is a single CP1252 byte (0xF1), which is an
            // invalid UTF-8 sequence and would otherwise come through as "�".
            // detectEncodingFromByteOrderMarks still honors a UTF-8/UTF-16 BOM if present.
            using (var streamReader = new StreamReader(path, System.Text.Encoding.GetEncoding(1252), detectEncodingFromByteOrderMarks: true))
            {
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    rows.Add(SplitCsvLine(line));
                }
            }
            return rows;
        }

        private static string[] SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var sb = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                    else sb.Append(c);
                }
            }
            fields.Add(sb.ToString());
            return fields.ToArray();
        }

        private static int GetHeaderIndex(string[] headers, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                int idx = Array.FindIndex(headers, h => string.Equals(h, candidate, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) return idx;
            }
            return -1;
        }

        // ── Import: cross-check parsed rows against the database ───────────────

        private void CrossCheckAgainstExisting(List<EmployeeImportRow> rows)
        {
            var companies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var branches  = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var depts     = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var titles    = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            using (var con = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
            {
                con.Open();
                LoadLookup(con, "SELECT ComId, Name FROM dbo.Company", companies);
                LoadLookup(con, "SELECT BranchId, Name FROM dbo.Branch", branches);
                LoadLookup(con, "SELECT DeptId, Name FROM dbo.Department", depts);
                LoadLookup(con, "SELECT TitleId, Code FROM dbo.Title", titles);
            }

            var byNumber = _vm.AllRows
                .Where(emp => !string.IsNullOrWhiteSpace(emp.EmployeeNumber))
                .GroupBy(emp => emp.EmployeeNumber.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var byNameCompany = _vm.AllRows
                .GroupBy(emp => NameCompanyKey(emp.Name, emp.CompanyName))
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var row in rows)
            {
                if (!string.IsNullOrWhiteSpace(row.CompanyName) && companies.TryGetValue(row.CompanyName.Trim(), out int comId))
                    row.ComId = comId;
                if (!string.IsNullOrWhiteSpace(row.BranchName) && branches.TryGetValue(row.BranchName.Trim(), out int brId))
                    row.BranchId = brId;
                if (!string.IsNullOrWhiteSpace(row.DepartmentName) && depts.TryGetValue(row.DepartmentName.Trim(), out int deptId))
                    row.DeptId = deptId;
                if (!string.IsNullOrWhiteSpace(row.TitleText) && titles.TryGetValue(row.TitleText.Trim(), out int titleId))
                    row.TitleId = titleId;

                if (!row.ComId.HasValue)
                {
                    row.Status = EmployeeImportStatus.Invalid;
                    row.ErrorMessage = $"Unknown company \"{row.CompanyName}\".";
                    row.ChangeSummary = row.ErrorMessage;
                    continue;
                }
                if (!row.BranchId.HasValue)
                {
                    row.Status = EmployeeImportStatus.Invalid;
                    row.ErrorMessage = $"Unknown branch \"{row.BranchName}\".";
                    row.ChangeSummary = row.ErrorMessage;
                    continue;
                }

                Pages.Admin.AccountManagement.EmployeeManagementDto match = null;
                if (!string.IsNullOrWhiteSpace(row.EmployeeNumber))
                    byNumber.TryGetValue(row.EmployeeNumber.Trim(), out match);
                if (match == null)
                    byNameCompany.TryGetValue(NameCompanyKey(row.Name, row.CompanyName), out match);

                if (match == null)
                {
                    row.Status = EmployeeImportStatus.New;
                    continue;
                }

                row.MatchedEmpId = match.EmpId;

                var changes = new List<string>();
                if (!string.Equals(match.Name ?? "", row.Name ?? "", StringComparison.OrdinalIgnoreCase)) changes.Add("Name");
                if (!string.Equals(match.Position ?? "", row.Position ?? "", StringComparison.OrdinalIgnoreCase)) changes.Add("Position");
                if (!string.Equals(match.CompanyName ?? "", row.CompanyName ?? "", StringComparison.OrdinalIgnoreCase)) changes.Add("Company");
                if (!string.Equals(match.BranchName ?? "", row.BranchName ?? "", StringComparison.OrdinalIgnoreCase)) changes.Add("Branch");
                if (!string.Equals(match.DepartmentName ?? "", row.DepartmentName ?? "", StringComparison.OrdinalIgnoreCase)) changes.Add("Dept");
                if (match.Active != row.Active) changes.Add("Status");

                if (changes.Count == 0)
                {
                    row.Status = EmployeeImportStatus.Unchanged;
                }
                else
                {
                    row.Status        = EmployeeImportStatus.Update;
                    row.ChangeSummary = "Changed: " + string.Join(", ", changes);
                }
            }
        }

        private static string NameCompanyKey(string name, string company)
            => ((name ?? "").Trim() + "|" + (company ?? "").Trim()).ToUpperInvariant();

        private static void LoadLookup(System.Data.SqlClient.SqlConnection con, string sql, Dictionary<string, int> map)
        {
            using (var cmd = new System.Data.SqlClient.SqlCommand(sql, con))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (reader.IsDBNull(1)) continue;
                    string name = reader.GetString(1).Trim();
                    if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name))
                        map[name] = reader.GetInt32(0);
                }
            }
        }

        // ── Import: commit accepted rows ────────────────────────────────────────

        private (int Inserted, int Updated, int Archived, int Errors) ExecuteImport(List<EmployeeImportRow> rows, List<int> archiveEmpIds)
        {
            int inserted = 0, updated = 0, archived = 0, errors = 0;

            using (var con = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
            {
                con.Open();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        foreach (var row in rows)
                        {
                            try
                            {
                                if (row.Status == EmployeeImportStatus.New)
                                {
                                    using (var cmd = new System.Data.SqlClient.SqlCommand(@"
                                        INSERT INTO dbo.Employee (Name, Position, ComId, BranchId, DeptId, EmployeeNumber, TitleId, Active, DateCreated, Createdby)
                                        VALUES (@Name, @Pos, @Co, @Br, @De, @Num, @Ti, @Active, GETDATE(), @By)", con, tx))
                                    {
                                        cmd.Parameters.AddWithValue("@Name",   row.Name);
                                        cmd.Parameters.AddWithValue("@Pos",    (object)row.Position ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@Co",     row.ComId.Value);
                                        cmd.Parameters.AddWithValue("@Br",     row.BranchId.Value);
                                        cmd.Parameters.AddWithValue("@De",     row.DeptId.HasValue ? (object)row.DeptId.Value : DBNull.Value);
                                        cmd.Parameters.AddWithValue("@Num",    string.IsNullOrWhiteSpace(row.EmployeeNumber) ? (object)DBNull.Value : row.EmployeeNumber);
                                        cmd.Parameters.AddWithValue("@Ti",     row.TitleId.HasValue ? (object)row.TitleId.Value : DBNull.Value);
                                        cmd.Parameters.AddWithValue("@Active", row.Active);
                                        cmd.Parameters.AddWithValue("@By",     AppSession.CurrentUserId);
                                        cmd.ExecuteNonQuery();
                                    }
                                    inserted++;
                                }
                                else if (row.Status == EmployeeImportStatus.Update && row.MatchedEmpId.HasValue)
                                {
                                    using (var cmd = new System.Data.SqlClient.SqlCommand(@"
                                        UPDATE dbo.Employee
                                        SET Name = @Name, Position = @Pos, ComId = @Co, BranchId = @Br, DeptId = @De,
                                            EmployeeNumber = @Num, TitleId = @Ti, Active = @Active
                                        WHERE EmpId = @Id", con, tx))
                                    {
                                        cmd.Parameters.AddWithValue("@Name",   row.Name);
                                        cmd.Parameters.AddWithValue("@Pos",    (object)row.Position ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@Co",     row.ComId.Value);
                                        cmd.Parameters.AddWithValue("@Br",     row.BranchId.Value);
                                        cmd.Parameters.AddWithValue("@De",     row.DeptId.HasValue ? (object)row.DeptId.Value : DBNull.Value);
                                        cmd.Parameters.AddWithValue("@Num",    string.IsNullOrWhiteSpace(row.EmployeeNumber) ? (object)DBNull.Value : row.EmployeeNumber);
                                        cmd.Parameters.AddWithValue("@Ti",     row.TitleId.HasValue ? (object)row.TitleId.Value : DBNull.Value);
                                        cmd.Parameters.AddWithValue("@Active", row.Active);
                                        cmd.Parameters.AddWithValue("@Id",     row.MatchedEmpId.Value);
                                        cmd.ExecuteNonQuery();
                                    }
                                    updated++;
                                }
                            }
                            catch { errors++; }
                        }

                        if (archiveEmpIds != null && archiveEmpIds.Count > 0)
                        {
                            string archivedBy = AppSession.CurrentUserName ?? "System";
                            foreach (var empId in archiveEmpIds)
                            {
                                try
                                {
                                    using (var cmd = new System.Data.SqlClient.SqlCommand(@"
                                        IF NOT EXISTS (SELECT 1 FROM dbo.ArchiveStatus WHERE EntityType = 'Employee' AND EntityId = @Id AND IsArchived = 1)
                                        INSERT INTO dbo.ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                        VALUES ('Employee', @Id, 1, GETDATE(), @By, 'Archived — not present in latest employee import')", con, tx))
                                    {
                                        cmd.Parameters.AddWithValue("@Id", empId);
                                        cmd.Parameters.AddWithValue("@By", archivedBy);
                                        cmd.ExecuteNonQuery();
                                    }
                                    archived++;
                                }
                                catch { errors++; }
                            }
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }

            return (inserted, updated, archived, errors);
        }

        private void BtnTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName   = "EmployeeImportTemplate",
                DefaultExt = ".xlsx",
                Filter     = "Excel Workbook|*.xlsx"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                using (var wb = new XLWorkbook())
                {
                    var ws = wb.Worksheets.Add("Employees");
                    string[] headers = { "Emp #", "Title", "Name*", "Position", "Company*", "Department", "Branch*", "Active (TRUE/FALSE)" };
                    for (int c = 0; c < headers.Length; c++)
                    {
                        var cell = ws.Cell(1, c + 1);
                        cell.Value = headers[c];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4E9AFC");
                        cell.Style.Font.FontColor       = XLColor.White;
                    }
                    // Explicit widths — AdjustToContents triggers a SixLabors.Fonts version conflict
                    int[] colWidths = { 10, 12, 30, 25, 22, 22, 22, 20 };
                    for (int c = 0; c < colWidths.Length; c++)
                        ws.Column(c + 1).Width = colWidths[c];
                    wb.SaveAs(dlg.FileName);
                }

                MessageBox.Show($"Template saved to:\n{dlg.FileName}", "Template Downloaded",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save template:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Double-click → Edit ───────────────────────────────────────────────
        private void MainGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Only trigger edit for non-email cells (email cells handle inline edit)
            var hit = MainGrid.InputHitTest(e.GetPosition(MainGrid)) as DependencyObject;
            if (hit == null) return;

            var cell = FindVisualParent<DataGridCell>(hit);
            if (cell == null) return;

            var col = cell.Column;
            if (col == ColDeptEmail || col == ColBranchEmail || col == ColPersonalEmail) return;

            BtnEdit_Click(sender, e);
        }

        // ── Inline email editing ──────────────────────────────────────────────

        private void MainGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            // Cancel edit for all non-email columns
            if (e.Column != ColDeptEmail && e.Column != ColBranchEmail && e.Column != ColPersonalEmail)
            {
                e.Cancel = true;
                return;
            }

            var emp = e.Row.Item as EmployeeManagementDto;
            if (emp == null) { e.Cancel = true; return; }

            string current = string.Empty;
            if (e.Column == ColDeptEmail)     current = emp.DepartmentEmail ?? string.Empty;
            else if (e.Column == ColBranchEmail)  current = emp.BranchEmail     ?? string.Empty;
            else if (e.Column == ColPersonalEmail) current = emp.PrimaryEmail   ?? string.Empty;

            _editOriginalValue = current.Equals("(None)", StringComparison.OrdinalIgnoreCase)
                ? string.Empty : current;
        }

        private async void MainGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (e.Column != ColDeptEmail && e.Column != ColBranchEmail && e.Column != ColPersonalEmail) return;

            var emp     = e.Row.Item as EmployeeManagementDto;
            if (emp == null) return;

            var tb      = e.EditingElement as TextBox;
            string newValue = tb?.Text?.Trim() ?? string.Empty;
            if (newValue.Equals("(None)", StringComparison.OrdinalIgnoreCase)) newValue = string.Empty;

            if (string.Equals(newValue, _editOriginalValue, StringComparison.OrdinalIgnoreCase)) return;

            // Validate empty → remove
            if (!string.IsNullOrWhiteSpace(newValue) && !EmployeeManagementViewModel.IsValidEmail(newValue))
            {
                MessageBox.Show($"\"{newValue}\" is not a valid email address.", "Invalid Email",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                if (tb != null) tb.Text = string.IsNullOrEmpty(_editOriginalValue) ? "(None)" : _editOriginalValue;
                e.Cancel = true;
                return;
            }

            try
            {
                if (e.Column == ColDeptEmail)
                {
                    if (string.IsNullOrWhiteSpace(newValue))
                    {
                        if (MessageBox.Show(
                            $"Remove the department email for {emp.CompanyName} / {emp.DepartmentName}?\nThis affects all employees in the department.",
                            "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        {
                            if (tb != null) tb.Text = string.IsNullOrEmpty(_editOriginalValue) ? "(None)" : _editOriginalValue;
                            e.Cancel = true;
                            return;
                        }
                        await _vm.DeleteDeptEmailAsync(emp.CompanyName, emp.DepartmentName);
                        ShowStatus("Department email removed.");
                    }
                    else
                    {
                        int emailId = await ResolveOrCreateEmailId(newValue);
                        if (emailId < 0) { e.Cancel = true; return; }
                        await _vm.SaveDeptEmailAsync(emp.CompanyName, emp.DepartmentName, emailId);
                        ShowStatus($"Department email updated for {emp.DepartmentName}.");
                    }
                    _vm.ApplyFilter();
                }
                else if (e.Column == ColBranchEmail)
                {
                    if (string.IsNullOrWhiteSpace(newValue))
                    {
                        if (MessageBox.Show(
                            $"Remove the branch email for {emp.CompanyName} / {emp.BranchName}?",
                            "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        {
                            if (tb != null) tb.Text = string.IsNullOrEmpty(_editOriginalValue) ? "(None)" : _editOriginalValue;
                            e.Cancel = true;
                            return;
                        }
                        await _vm.DeleteBranchEmailAsync(emp.CompanyName, emp.DepartmentName, emp.BranchName);
                        ShowStatus("Branch email removed.");
                    }
                    else
                    {
                        int emailId = await ResolveOrCreateEmailId(newValue);
                        if (emailId < 0) { e.Cancel = true; return; }
                        await _vm.SaveBranchEmailAsync(emp.CompanyName, emp.DepartmentName, emp.BranchName, emailId);
                        ShowStatus($"Branch email updated for {emp.BranchName}.");
                    }
                    _vm.ApplyFilter();
                }
                else if (e.Column == ColPersonalEmail)
                {
                    if (string.IsNullOrWhiteSpace(newValue))
                    {
                        if (MessageBox.Show(
                            $"Remove personal email for {emp.Name}?",
                            "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        {
                            if (tb != null) tb.Text = string.IsNullOrEmpty(_editOriginalValue) ? "(None)" : _editOriginalValue;
                            e.Cancel = true;
                            return;
                        }
                        await _vm.DeletePersonalEmailAsync(emp.EmpId);
                        ShowStatus($"Personal email removed for {emp.Name}.");
                    }
                    else
                    {
                        int emailId = await ResolveOrCreateEmailId(newValue);
                        if (emailId < 0) { e.Cancel = true; return; }
                        await _vm.SavePersonalEmailAsync(emp.EmpId, newValue);
                        ShowStatus($"Personal email updated for {emp.Name}.");
                    }
                    _vm.ApplyFilter();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving email:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task<int> ResolveOrCreateEmailId(string email)
        {
            // Only prompt for genuinely new addresses (not in the loaded directory).
            if (!_vm.EmailIdMap.ContainsKey(email) && MessageBox.Show(
                $"\"{email}\" is not in the email list. Add it now?",
                "New Email Address", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return -1;

            // Always resolve through the idempotent upsert: a cached id can be stale if the
            // address was deleted elsewhere (e.g. Email Configuration), which would fail the
            // FK when the dept/branch email row is saved.
            return await _vm.InsertOrGetEmailIdAsync(email);
        }

        private void ShowStatus(string message)
        {
            _vm.StatusMessage = message;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            timer.Tick += (s, e) => { _vm.StatusMessage = string.Empty; timer.Stop(); };
            timer.Start();
        }

        private void BtnFirst_Click(object sender, RoutedEventArgs e) { _vm.GoToFirstPage(); SyncSelectAllHeader(); }
        private void BtnPrev_Click(object sender, RoutedEventArgs e)  { _vm.GoToPrevPage();  SyncSelectAllHeader(); }
        private void BtnNext_Click(object sender, RoutedEventArgs e)  { _vm.GoToNextPage();  SyncSelectAllHeader(); }
        private void BtnLast_Click(object sender, RoutedEventArgs e)  { _vm.GoToLastPage();  SyncSelectAllHeader(); }

        private void SyncSelectAllHeader()
        {
            if (_selectAllHeaderChk == null) return;
            _selectAllHeaderChk.IsChecked = _vm.PagedRows.Count > 0
                && _vm.PagedRows.All(r => r.IsSelected);
        }

        private void MainGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;

            string header = (e.Column.Header?.ToString() ?? "").Replace(" ✎", "").Trim();
            bool ascending = e.Column.SortDirection != System.ComponentModel.ListSortDirection.Ascending;

            foreach (var col in MainGrid.Columns)
                col.SortDirection = null;
            e.Column.SortDirection = ascending
                ? System.ComponentModel.ListSortDirection.Ascending
                : System.ComponentModel.ListSortDirection.Descending;

            _vm.SetSort(header, ascending);
        }

        private void MainGrid_HeaderButtonClick(object sender, RoutedEventArgs e)
        {
            if (!(e.OriginalSource is System.Windows.Controls.Button btn) || btn.Name != "FilterBtn") return;
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

        private static void PositionPopupNearButton(System.Windows.Window popup, System.Windows.Controls.Button btn)
        {
            try
            {
                // PointToScreen returns device pixels; Window.Left/Top are WPF logical pixels.
                // TransformFromDevice converts device px → logical px so position is correct on non-100% DPI.
                var pt     = btn.PointToScreen(new System.Windows.Point(0, btn.ActualHeight));
                var source = System.Windows.PresentationSource.FromVisual(btn);
                if (source?.CompositionTarget != null)
                    pt = source.CompositionTarget.TransformFromDevice.Transform(pt);

                var    area = System.Windows.SystemParameters.WorkArea;
                double estH = double.IsNaN(popup.Height) ? popup.MaxHeight : popup.Height;
                popup.Left = Math.Max(area.Left, Math.Min(pt.X, area.Right  - popup.Width));
                popup.Top  = Math.Max(area.Top,  Math.Min(pt.Y, area.Bottom - estH));
            }
            catch { popup.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner; }
        }

        private static void SetWpfOwner(System.Windows.Window window)
        {
            var owner = System.Windows.Forms.Form.ActiveForm;
            if (owner != null) new WindowInteropHelper(window).Owner = owner.Handle;
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parent == null) return null;
            return parent is T t ? t : FindVisualParent<T>(parent);
        }
    }
}
