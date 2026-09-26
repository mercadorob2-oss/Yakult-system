using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Wpf.Set.BulkDeploy
{
    public partial class BulkDeployWindow : Window
    {
        private readonly BulkDeployViewModel _viewModel = new BulkDeployViewModel();
        private readonly ColumnFiltersDialog _filters = new ColumnFiltersDialog();

        public ObservableCollection<BulkDeployRow> Rows => _viewModel.Rows;

        private bool IsDateOn => _filters.IsDateOn;
        private bool IsPcOn => _filters.IsPcOn;
        private bool IsIpOn => _filters.IsIpOn;
        private bool IsDeptOn => _filters.IsDeptOn;
        private bool IsFaOn => _filters.IsFaOn;
        private bool IsEmpOn => _filters.IsEmpOn;
        private bool IsComOn => _filters.IsComOn;
        private bool IsBranchOn => _filters.IsBranchOn;
        private bool IsCatOn => _filters.IsCatOn;
        private bool IsDropdownOn => _filters.IsDropdownOn;

        public BulkDeployWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
            _viewModel.EnsureSeeded(300);
            _filters.OptionChanged += Filters_OptionChanged;
            _filters.DropdownChanged += Filters_DropdownChanged;
            ApplyColumnVisibility();
            UpdateColumnFiltersSummary();
            Loaded += BulkDeployWindow_Loaded;
            Closing += BulkDeployWindow_Closing;
        }

        private void BulkDeployWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Let the owned ColumnFiltersDialog's HWND actually be destroyed
            // now that this window (its permanent owner) is closing too -
            // otherwise it would stay alive forever as a hidden window since
            // BtnDone_Click/ColumnFiltersDialog_Closing normally just Hide()
            // it for reuse across multiple "Column Filters..." clicks.
            _filters.AllowRealClose();
            _filters.Close();
        }

        private void BtnColumnFilters_Click(object sender, RoutedEventArgs e)
        {
            if (_filters.IsVisible)
            {
                _filters.Activate();
                return;
            }
            _filters.Show();
        }

        private async void Filters_OptionChanged(object sender, EventArgs e)
        {
            UpdateColumnFiltersSummary();
            await OptionCheck_ChangedAsync();
        }

        private void Filters_DropdownChanged(object sender, EventArgs e)
        {
            UpdateColumnFiltersSummary();
            RebuildDropdownColumns();
        }

        private void UpdateColumnFiltersSummary()
        {
            if (ColumnFiltersSummary == null) return;
            var on = new List<string>();
            if (IsDateOn) on.Add("Date");
            if (IsPcOn) on.Add("PC");
            if (IsIpOn) on.Add("IP");
            if (IsFaOn) on.Add("FixedAsset");
            if (IsEmpOn) on.Add("Employee");
            if (IsComOn) on.Add("Company");
            if (IsBranchOn) on.Add("Branch");
            if (IsCatOn) on.Add("Category");
            if (IsDropdownOn) on.Add("Dropdowns");
            ColumnFiltersSummary.Text = on.Count == 0 ? "No optional columns enabled." : string.Join(" \u2022 ", on);
        }

        private async void BulkDeployWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Owner can only be set once this window has actually been shown
            // (ShowDialog() from SetPageView) - setting it in the constructor
            // throws InvalidOperationException("Cannot set Owner property to
            // a Window that has not been shown previously").
            if (_filters.Owner == null)
                _filters.Owner = this;

            await _viewModel.LoadCategoryOptionsAsync();
            await _viewModel.LoadCompanyAndBranchOptionsAsync();
            RebuildDropdownColumns();
        }

        // Columns that can switch between free-text and dropdown editing.
        // Unique-per-item identifiers (ComputerName, IPAddress, SerialNumber,
        // ModelNumber, FixedAssetNumber) are intentionally excluded.
        private static readonly string[] DropdownableHeaders =
        {
            "Department", "Category", "ItemRole", "Condition", "Company", "Branch"
        };

        private void RebuildDropdownColumns()
        {
            if (BulkGrid == null) return;

            foreach (var header in DropdownableHeaders)
            {
                int index = BulkGrid.Columns
                    .Select((c, i) => new { c, i })
                    .Where(x => string.Equals(x.c.Header as string, header, StringComparison.Ordinal))
                    .Select(x => x.i)
                    .DefaultIfEmpty(-1)
                    .First();
                if (index < 0) continue;

                var existing = BulkGrid.Columns[index];
                bool wasVisible = existing.Visibility == Visibility.Visible;
                double minWidth = existing.MinWidth;

                DataGridColumn replacement = IsDropdownOn
                    ? BuildComboColumn(header, minWidth)
                    : BuildTextColumn(header, minWidth);

                replacement.Visibility = wasVisible ? Visibility.Visible : Visibility.Collapsed;
                BulkGrid.Columns.RemoveAt(index);
                BulkGrid.Columns.Insert(index, replacement);
            }

            ApplyColumnVisibility();
            BulkGrid.Items.Refresh();
        }

        private DataGridColumn BuildTextColumn(string header, double minWidth)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(header),
                MinWidth = minWidth
            };
        }

        private DataGridColumn BuildComboColumn(string header, double minWidth)
        {
            IEnumerable<string> options;
            switch (header)
            {
                case "Department": options = _viewModel.DeptOptions; break;
                case "Category": options = _viewModel.CategoryOptions; break;
                case "ItemRole": options = _viewModel.RoleOptions; break;
                case "Condition": options = _viewModel.ConditionOptions; break;
                case "Company": options = _viewModel.CompanyOptions; break;
                case "Branch": options = _viewModel.BranchOptions; break;
                default: options = Enumerable.Empty<string>(); break;
            }

            // Editable style so values outside the live list (typos, legacy
            // free text, values not yet in the DB) can still be typed and
            // are left for BulkDeployValidator to flag - the dropdown is a
            // convenience, not a hard constraint.
            var editableStyle = new Style(typeof(ComboBox));
            editableStyle.Setters.Add(new Setter(ComboBox.IsEditableProperty, true));
            editableStyle.Setters.Add(new Setter(ComboBox.IsTextSearchEnabledProperty, true));
            editableStyle.Setters.Add(new Setter(ComboBox.StaysOpenOnEditProperty, true));

            return new DataGridComboBoxColumn
            {
                Header = header,
                TextBinding = new Binding(header) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                ItemsSource = options,
                EditingElementStyle = editableStyle,
                MinWidth = minWidth
            };
        }

        private void BulkGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                PasteFromClipboard();
            }
            else if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (!(Keyboard.FocusedElement is TextBox))
                {
                    e.Handled = true;
                    DeleteSelectedCells();
                }
            }
        }

        private void BulkGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var cell = FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
            if (cell == null) return;

            var row = FindVisualParent<DataGridRow>(cell);
            if (row == null || row.Item == null) return;

            BulkGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            BulkGrid.CommitEdit(DataGridEditingUnit.Row, true);

            var cellInfo = new DataGridCellInfo(row.Item, cell.Column);
            BulkGrid.SelectedCells.Clear();
            BulkGrid.SelectedCells.Add(cellInfo);
            BulkGrid.CurrentCell = cellInfo;
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject current = child;
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void PasteFromClipboard()
        {
            if (!Clipboard.ContainsText()) return;
            string text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text)) return;

            BulkGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            BulkGrid.CommitEdit(DataGridEditingUnit.Row, true);

            var editableColumns = BulkGrid.Columns
                .Where(c => GetColumnPropertyPath(c) != null)
                .OrderBy(c => c.DisplayIndex)
                .ToList();
            if (editableColumns.Count == 0) return;

            DataGridColumn anchorColumn;
            int anchorRowIndex;

            var selectedCells = BulkGrid.SelectedCells;
            if (selectedCells != null && selectedCells.Count > 0)
            {
                anchorColumn = selectedCells.Select(c => c.Column).OrderBy(c => c.DisplayIndex).First();
                anchorRowIndex = selectedCells.Min(c => BulkGrid.Items.IndexOf(c.Item));
            }
            else if (BulkGrid.CurrentCell.Column != null && BulkGrid.CurrentCell.Item != null)
            {
                anchorColumn = BulkGrid.CurrentCell.Column;
                anchorRowIndex = BulkGrid.Items.IndexOf(BulkGrid.CurrentCell.Item);
            }
            else
            {
                anchorColumn = editableColumns[0];
                anchorRowIndex = 0;
            }
            if (anchorRowIndex < 0) anchorRowIndex = 0;

            int startEditableIndex = editableColumns.FindIndex(c => c.DisplayIndex >= anchorColumn.DisplayIndex);
            if (startEditableIndex < 0) startEditableIndex = 0;
            anchorColumn = editableColumns[startEditableIndex];

            List<BulkDeployRow> parsed = BulkDeployParser.ParseClipboardText(
                text, IsDateOn, IsPcOn, IsIpOn, IsDeptOn, IsFaOn, IsEmpOn, null, IsComOn, IsBranchOn, IsCatOn);
            if (parsed.Count == 0) return;

            for (int i = 0; i < parsed.Count; i++)
            {
                int targetRow = anchorRowIndex + i;
                while (targetRow >= Rows.Count)
                    Rows.Add(new BulkDeployRow());
                CopyParsedOntoRow(Rows[targetRow], parsed[i]);
            }

            BulkGrid.Items.Refresh();
        }

        private void CopyParsedOntoRow(BulkDeployRow target, BulkDeployRow parsed)
        {
            target.BundleKey = parsed.BundleKey;
            target.Department = parsed.Department;
            if (IsPcOn) target.ComputerName = parsed.ComputerName;
            if (IsIpOn) target.IPAddress = parsed.IPAddress;
            target.ItemRole = parsed.ItemRole;
            target.ItemName = parsed.ItemName;
            target.ModelNumber = parsed.ModelNumber;
            target.SerialNumber = parsed.SerialNumber;
            if (IsFaOn) target.FixedAssetNumber = parsed.FixedAssetNumber;
            if (IsEmpOn) target.Employee = parsed.Employee;
            if (IsComOn) target.Company = parsed.Company;
            if (IsBranchOn) target.Branch = parsed.Branch;
            target.Category = parsed.Category;
            if (IsCatOn) target.Category = parsed.Category;
            else target.Category = BulkDeployParser.AutoCategoryFromRole(parsed.ItemRole);
            target.Quantity = parsed.Quantity;
            if (IsDateOn) target.DateDeployedText = parsed.DateDeployedText;
            target.Condition = parsed.Condition;
            target.Vendor = parsed.Vendor;
            target.Remarks = parsed.Remarks;
        }

        private void DeleteSelectedCells()
        {
            var selectedCells = BulkGrid.SelectedCells;
            if (selectedCells == null || selectedCells.Count == 0) return;

            BulkGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            BulkGrid.CommitEdit(DataGridEditingUnit.Row, true);

            foreach (var cellInfo in selectedCells)
            {
                if (!(cellInfo.Item is BulkDeployRow row)) continue;

                string propertyName = GetColumnPropertyPath(cellInfo.Column);
                if (propertyName == null) continue;

                SetBulkDeployCellValue(row, propertyName, null);
            }

            BulkGrid.Items.Refresh();
        }

        private static string GetColumnPropertyPath(DataGridColumn column)
        {
            if (column is DataGridComboBoxColumn comboCol)
            {
                if (comboCol.IsReadOnly) return null;
                return (comboCol.TextBinding as Binding)?.Path?.Path;
            }
            if (column is DataGridBoundColumn boundCol)
            {
                if (boundCol.IsReadOnly) return null;
                return (boundCol.Binding as Binding)?.Path?.Path;
            }
            return null;
        }

        private static void SetBulkDeployCellValue(BulkDeployRow row, string propertyName, string value)
        {
            string text = value ?? string.Empty;
            switch (propertyName)
            {
                case nameof(BulkDeployRow.BundleKey): row.BundleKey = text; break;
                case nameof(BulkDeployRow.Department): row.Department = text; break;
                case nameof(BulkDeployRow.ComputerName): row.ComputerName = text; break;
                case nameof(BulkDeployRow.IPAddress): row.IPAddress = text; break;
                case nameof(BulkDeployRow.ItemRole): row.ItemRole = text; break;
                case nameof(BulkDeployRow.ItemName): row.ItemName = text; break;
                case nameof(BulkDeployRow.ModelNumber): row.ModelNumber = text; break;
                case nameof(BulkDeployRow.SerialNumber): row.SerialNumber = text; break;
                case nameof(BulkDeployRow.FixedAssetNumber): row.FixedAssetNumber = text; break;
                case nameof(BulkDeployRow.Category): row.Category = text; break;
                case nameof(BulkDeployRow.Quantity): row.Quantity = 1; break;
                case nameof(BulkDeployRow.DateDeployedText): row.DateDeployedText = text; break;
                case nameof(BulkDeployRow.Condition): row.Condition = text; break;
                case nameof(BulkDeployRow.Vendor): row.Vendor = text; break;
                case nameof(BulkDeployRow.Remarks): row.Remarks = text; break;
                case nameof(BulkDeployRow.Employee): row.Employee = text; break;
                case nameof(BulkDeployRow.Company): row.Company = text; break;
                case nameof(BulkDeployRow.Branch): row.Branch = text; break;
                default: break;
            }
        }

        private async void BtnPaste_Click(object sender, RoutedEventArgs e)
        {
            ChkErrorsOnly.IsChecked = false;
            PasteFromClipboard();
            if (ChkAutoValidate.IsChecked == true)
                await _viewModel.ValidateAllAsync();
            RefreshGrid();
            UpdateSummary();
        }

        private async void BtnValidate_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ValidateAllAsync();
            RefreshGrid();
            UpdateSummary();
            int errors = Rows.Count(r => r.RowStatus == "Error");
            int warnings = Rows.Count(r => r.RowStatus == "Warning");
            AppendLog($"Validate: {Rows.Count} rows, {errors} error, {warnings} warning.");
        }

        private void BtnTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "DepartmentSet_Import_Template",
                DefaultExt = ".xlsx",
                Filter = "Excel Workbook|*.xlsx"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                BulkDeployTemplateExporter.ExportTemplate(dlg.FileName);
                AppendLog($"Template exported to {dlg.FileName}.");
                if (MessageBox.Show("Template exported successfully.\n\nOpen file?", "Export Complete",
                    MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Template export failed:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnImportFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Workbook|*.xlsx;*.xls",
                Title = "Select deployment Excel file"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var report = BulkDeployExcelImporter.InspectColumns(dlg.FileName);
                var mapping = new ImportColumnMappingDialog(System.IO.Path.GetFileName(dlg.FileName), report)
                {
                    Owner = this
                };
                if (mapping.ShowDialog() != true || !mapping.Confirmed)
                {
                    AppendLog($"Import cancelled: {System.IO.Path.GetFileName(dlg.FileName)}.");
                    return;
                }

                ChkErrorsOnly.IsChecked = false;
                var loaded = BulkDeployExcelImporter.LoadRows(
                    dlg.FileName, IsDateOn, IsPcOn, IsIpOn, IsDeptOn, IsFaOn, IsEmpOn, IsComOn, IsBranchOn, IsCatOn);

                // Drop every blank seed row (regardless of where it sits in the
                // collection) before inserting imported data, so:
                //  1. imported rows always land visibly at the top instead of
                //     being appended after ~300 empty rows, and
                //  2. importing a second file in the same session doesn't leave
                //     stray blank rows interleaved between the first and second
                //     import's real rows (EnsureSeeded always re-appends fresh
                //     blanks at the very end, after this removal).
                var blanks = Rows.Where(BulkDeployViewModel.IsBlankRow).ToList();
                foreach (var blank in blanks)
                    Rows.Remove(blank);

                foreach (var row in loaded)
                    Rows.Add(row);
                _viewModel.EnsureSeeded(Math.Max(300, Rows.Count + 50));

                if (ChkAutoValidate.IsChecked == true)
                    await _viewModel.ValidateAllAsync();
                RefreshGrid();
                UpdateSummary();
                AppendLog($"Imported {loaded.Count} row(s) from {System.IO.Path.GetFileName(dlg.FileName)}.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excel import failed:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ErrorsOnly_Changed(object sender, RoutedEventArgs e)
        {
            ApplyErrorsFilter();
        }

        private void ApplyErrorsFilter()
        {
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(Rows);
            if (view == null) return;
            string find = TxtFind != null && !string.IsNullOrWhiteSpace(TxtFind.Text)
                ? TxtFind.Text.Trim()
                : null;
            bool problemsOnly = ChkErrorsOnly.IsChecked == true;
            if (!problemsOnly && find == null)
            {
                view.Filter = null;
            }
            else
            {
                view.Filter = o =>
                {
                    if (!(o is BulkDeployRow r)) return false;
                    if (problemsOnly && r.RowStatus != "Error" && r.RowStatus != "Warning")
                        return false;
                    if (find != null && !RowMatchesFind(r, find))
                        return false;
                    return true;
                };
            }
            view.Refresh();
        }

        private static bool RowMatchesFind(BulkDeployRow r, string term)
        {
            return ContainsText(r.BundleKey, term)
                || ContainsText(r.Department, term)
                || ContainsText(r.ComputerName, term)
                || ContainsText(r.SerialNumber, term)
                || ContainsText(r.ItemName, term)
                || ContainsText(r.RowMessage, term);
        }

        private static bool ContainsText(string field, string term)
        {
            return !string.IsNullOrEmpty(field)
                && field.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void FindBox_Changed(object sender, TextChangedEventArgs e)
        {
            ApplyErrorsFilter();
        }

        private void BulkGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private string _sortProperty;
        private bool _sortDescending;

        private void BulkGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            string key = e.Column.Header as string;
            if (string.IsNullOrEmpty(key)) return;

            if (string.Equals(_sortProperty, key, StringComparison.Ordinal))
                _sortDescending = !_sortDescending;
            else
            {
                _sortProperty = key;
                _sortDescending = false;
            }

            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(Rows);
            if (view == null) return;
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(
                key, _sortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending));
            foreach (var col in BulkGrid.Columns)
                col.SortDirection = null;
            e.Column.SortDirection = _sortDescending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
            view.Refresh();
        }

        private void RefreshGrid()
        {
            BulkGrid.Items.Refresh();
            ApplyErrorsFilter();
        }

        private void UpdateSummary()
        {
            var active = Rows.Where(r => !BulkDeployViewModel.IsBlankRow(r)).ToList();
            int errors = active.Count(r => r.RowStatus == "Error");
            int warnings = active.Count(r => r.RowStatus == "Warning");
            int valid = active.Count(r => r.RowStatus == "Valid");
            int pending = active.Count - errors - warnings - valid;
            SummaryText.Text = active.Count == 0
                ? "No rows yet. Paste or import to begin."
                : $"{active.Count} rows: {valid} valid, {warnings} warning, {errors} error" +
                  (pending > 0 ? $", {pending} not validated" : "") + ".";
        }

        private async void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ValidateAllAsync();
            RefreshGrid();
            UpdateSummary();

            var validRows = Rows
                .Where(r => r.RowStatus != "Error" && !IsBlankRow(r))
                .Where(r => !string.IsNullOrWhiteSpace(r.BundleKey))
                .ToList();

            if (validRows.Count == 0)
            {
                AppendLog("Create: no valid rows to commit.");
                return;
            }

            var bundleDates = BuildBundleDates(validRows);

            BtnCreate.IsEnabled = false;
            try
            {
                CommitProgress.Minimum = 0;
                CommitProgress.Maximum = 100;
                CommitProgress.Value = 0;

                var result = await new SetBulkDeployRepository().CreateDeployedSetsAsync(
                    validRows, bundleDates, AppSession.CurrentUserId);

                int done = 0;
                foreach (var detail in result.Details)
                {
                    done++;
                    CommitProgress.Value = (double)done / Math.Max(1, result.Details.Count) * 100;
                    if (detail.Skipped)
                        AppendLog($"Bundle {detail.BundleKey}: skipped ({detail.Message}).");
                    else if (detail.SetId.HasValue)
                        AppendLog($"Bundle {detail.BundleKey}: created Set {detail.SetCode} ({detail.Message}).");
                    else
                        AppendLog($"Bundle {detail.BundleKey}: FAILED ({detail.Message}).");
                }

                AppendLog($"Create done: {result.Created} created, {result.Skipped} skipped, {result.Details.Count - result.Created - result.Skipped} failed.");
                if (result.Created > 0) DialogResult = true;
            }
            finally
            {
                BtnCreate.IsEnabled = true;
            }
        }

        private static Dictionary<string, DateTime?> BuildBundleDates(List<BulkDeployRow> rows)
        {
            var dates = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in rows.GroupBy(r => (r.BundleKey ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase))
            {
                DateTime? parsed = null;
                foreach (var row in group)
                {
                    if (string.IsNullOrWhiteSpace(row.DateDeployedText)) continue;
                    DateTime d;
                    double oa;
                    if (DateTime.TryParse(row.DateDeployedText.Trim(), out d)) { parsed = d.Date; break; }
                    if (double.TryParse(row.DateDeployedText.Trim(), out oa))
                    {
                        try { parsed = DateTime.FromOADate(oa).Date; break; }
                        catch { continue; }
                    }
                }
                dates[group.Key] = parsed;
            }
            return dates;
        }

        private static bool IsBlankRow(BulkDeployRow r)
        {
            return string.IsNullOrWhiteSpace(r.BundleKey)
                && string.IsNullOrWhiteSpace(r.ItemName)
                && string.IsNullOrWhiteSpace(r.SerialNumber)
                && string.IsNullOrWhiteSpace(r.ComputerName)
                && string.IsNullOrWhiteSpace(r.Employee)
                && string.IsNullOrWhiteSpace(r.Company)
                && string.IsNullOrWhiteSpace(r.Branch);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            Rows.Clear();
            _viewModel.EnsureSeeded(300);
            ChkErrorsOnly.IsChecked = false;
            if (TxtFind != null) TxtFind.Text = "";
            RefreshGrid();
            UpdateSummary();
            AppendLog("Grid cleared.");
        }

        private async Task OptionCheck_ChangedAsync()
        {
            ApplyColumnVisibility();
            if (!IsLoaded) return;
            await _viewModel.ValidateAllAsync();
            if (BulkGrid != null) BulkGrid.Items.Refresh();
        }

        private void ApplyColumnVisibility()
        {
            if (BulkGrid == null) return;
            SetColumnVisible("DateDeployedText", IsDateOn);
            SetColumnVisible("ComputerName", IsPcOn);
            SetColumnVisible("IPAddress", IsIpOn);
            SetColumnVisible("Department", IsDeptOn);
            SetColumnVisible("FixedAssetNumber", IsFaOn);
            SetColumnVisible("Employee", IsEmpOn);
            SetColumnVisible("Company", IsComOn);
            SetColumnVisible("Branch", IsBranchOn);
        }

        private void SetColumnVisible(string header, bool visible)
        {
            var col = BulkGrid.Columns
                .FirstOrDefault(c => string.Equals(c.Header as string, header, StringComparison.Ordinal));
            if (col != null) col.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AppendLog(string line)
        {
            ResultLog.AppendText(line + Environment.NewLine);
            ResultLog.ScrollToEnd();
        }
    }
}
