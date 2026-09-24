using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.WPF.Admin.AccountManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs
{
    public partial class BulkCreateAccountsWindow : Window
    {
        private const string AccountColumn = "AccountStatus";
        private const int PageSize = 20;

        private int _page = 1;
        private string _sortColumn;
        private bool _sortAscending = true;

        private readonly ApproverManagementViewModel _vm;
        private readonly List<ApproverRow> _source;
        private string _search;
        private readonly bool _showArchived;
        private readonly Dictionary<string, HashSet<string>> _filters;

        private List<ApproverRow> _view = new List<ApproverRow>();
        private List<BulkAccountResult> _results;
        private bool _busy;

        /// <summary>True once at least one account was created, so the caller can reload.</summary>
        public bool AccountsCreated { get; private set; }

        public BulkCreateAccountsWindow(
            ApproverManagementViewModel vm,
            string searchText,
            bool showArchived,
            Dictionary<string, HashSet<string>> initialFilters)
        {
            InitializeComponent();
            _vm           = vm;
            _source       = vm.AllRows;
            _search       = (searchText ?? "").Trim();
            _showArchived = showArchived;
            _filters      = initialFilters ?? new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in _source) r.IsSelected = !r.HasAccount;   // everything listed starts checked

            TxtSearch.Text = _search;
            Refresh();
            TxtSearch.TextChanged += TxtSearch_TextChanged;   // hooked after the initial fill
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _search = TxtSearch.Text.Trim();
            Refresh();
        }

        // Every space-separated word must appear (as a substring, any case) in at least one column.
        private static bool MatchesSearch(ApproverRow r, string[] terms)
        {
            string hay = string.Join("\u0001", new[]
            {
                r.EmployeeNumber, r.Title, r.EmployeeName, r.Position, r.Department,
                r.Company, r.Branch, r.IsApprover, r.SystemRoles
            });
            return terms.All(t => hay.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // ── Selection ──────────────────────────────────────────────────────────

        private void UpdateSelectionUi()
        {
            int selected = _view.Count(r => r.IsSelected);
            TxtSummary.Text = $"{selected} of {_view.Count} listed employee(s) selected to get a new account";
            BtnCreate.IsEnabled = !_busy && _results == null && selected > 0;

            ChkAll.IsChecked = _view.Count == 0 || selected == 0 ? false
                             : selected == _view.Count ? true
                             : (bool?)null;
        }

        private void ChkAll_Click(object sender, RoutedEventArgs e)
        {
            bool select = _view.Any(r => !r.IsSelected);   // any unchecked -> check all, else uncheck all
            foreach (var r in _view) r.IsSelected = select;
            RebuildPage();
            UpdateSelectionUi();
        }

        private void RowCheck_Click(object sender, RoutedEventArgs e) => UpdateSelectionUi();

        // ── Filtering ──────────────────────────────────────────────────────────

        private static string GetValue(ApproverRow r, string column)
            => column == AccountColumn ? r.AccountStatus : ApproverManagementViewModel.GetColumnValue(r, column);

        private void Refresh()
        {
            // Employees that already have an account are never listed.
            IEnumerable<ApproverRow> rows = _source.Where(r => !r.HasAccount);
            if (!_showArchived) rows = rows.Where(r => !r.IsArchived);

            var terms = (_search ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length > 0)
                rows = rows.Where(x => MatchesSearch(x, terms));

            foreach (var kv in _filters)
            {
                var allowed = kv.Value;
                string col  = kv.Key;
                rows = rows.Where(r => allowed.Contains(GetValue(r, col) ?? "", StringComparer.OrdinalIgnoreCase));
            }

            _view = rows.ToList();

            if (!string.IsNullOrEmpty(_sortColumn))
            {
                Func<ApproverRow, string> key = _sortColumn == "EmployeeNumber"
                    ? (Func<ApproverRow, string>)(r => ApproverManagementViewModel.EmpNumSortKey(r.EmployeeNumber))
                    : (r => GetValue(r, _sortColumn) ?? "");
                _view = _sortAscending
                    ? _view.OrderBy(key, StringComparer.OrdinalIgnoreCase).ToList()
                    : _view.OrderByDescending(key, StringComparer.OrdinalIgnoreCase).ToList();
            }

            _page = 1;
            RebuildPage();

            BtnClearFilters.Visibility = _filters.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateSelectionUi();
        }

        // ── Paging & sorting ───────────────────────────────────────────────────

        private void RebuildPage()
        {
            int totalPages = Math.Max(1, (int)Math.Ceiling(_view.Count / (double)PageSize));
            if (_page > totalPages) _page = totalPages;
            if (_page < 1) _page = 1;

            PreviewGrid.ItemsSource = _view.Skip((_page - 1) * PageSize).Take(PageSize).ToList();
            ApplySortArrows();

            BtnFirst.IsEnabled = BtnPrev.IsEnabled = _page > 1;
            BtnNext.IsEnabled  = BtnLast.IsEnabled = _page < totalPages;
            TxtPageInfo.Text   = _view.Count == 0
                ? "Page 0 of 0 (0 rows)"
                : $"Page {_page} of {totalPages} ({_view.Count} rows)";
        }

        private int TotalPages => Math.Max(1, (int)Math.Ceiling(_view.Count / (double)PageSize));

        private void BtnFirst_Click(object sender, RoutedEventArgs e) { _page = 1;            RebuildPage(); }
        private void BtnPrev_Click(object sender, RoutedEventArgs e)  { _page--;              RebuildPage(); }
        private void BtnNext_Click(object sender, RoutedEventArgs e)  { _page++;              RebuildPage(); }
        private void BtnLast_Click(object sender, RoutedEventArgs e)  { _page = TotalPages;   RebuildPage(); }

        private void PreviewGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            string column = ColumnNameFor(e.Column.Header?.ToString());
            if (column == null) return;

            // Use our own state: reassigning ItemsSource makes WPF clear column.SortDirection.
            bool ascending = !(_sortColumn == column && _sortAscending);

            _sortColumn    = column;
            _sortAscending = ascending;
            Refresh();
        }

        private void ApplySortArrows()
        {
            foreach (var col in PreviewGrid.Columns)
            {
                col.SortDirection = ColumnNameFor(col.Header?.ToString()) == _sortColumn
                    ? (_sortAscending
                        ? System.ComponentModel.ListSortDirection.Ascending
                        : System.ComponentModel.ListSortDirection.Descending)
                    : (System.ComponentModel.ListSortDirection?)null;
            }
        }

        private static string ColumnNameFor(string header)
        {
            switch (header)
            {
                case "Employee #":     return "EmployeeNumber";
                case "Title":          return "Title";
                case "Employee Name":  return "EmployeeName";
                case "Position":       return "Position";
                case "Department":     return "Department";
                case "Company":        return "Company";
                case "Branch":         return "Branch";
                case "Is Approver":    return "IsApprover";
                case "System Role(s)": return "SystemRoles";
                default:               return null;
            }
        }

        private void PreviewGrid_HeaderButtonClick(object sender, RoutedEventArgs e)
        {
            if (_busy || _results != null) return;
            if (!(e.OriginalSource is Button btn) || btn.Name != "FilterBtn") return;
            string column = ColumnNameFor(btn.Tag?.ToString());
            if (string.IsNullOrEmpty(column)) return;

            var values = _source.Select(r => GetValue(r, column) ?? "")
                                .Where(v => v.Length > 0)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                                .ToList();
            if (values.Count == 0) return;

            _filters.TryGetValue(column, out var current);
            var popup = new ColumnFilterWindow(column, values, current) { Owner = this };
            if (popup.ShowDialog() != true) return;

            if (popup.ClearFilter || popup.SelectedValues == null || popup.SelectedValues.Count == 0)
                _filters.Remove(column);
            else
                _filters[column] = popup.SelectedValues;
            Refresh();
        }

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            _filters.Clear();
            Refresh();
        }

        // ── Password options ───────────────────────────────────────────────────

        private void PwdMode_Changed(object sender, RoutedEventArgs e)
        {
            if (ManualPanel == null) return;   // fires during InitializeComponent
            ManualPanel.Visibility = RbManual.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ChkShow_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkShow.IsChecked == true)
            {
                TxtPwdVisible.Text  = PwdPassword.Password;
                TxtConfVisible.Text = PwdConfirm.Password;
                PwdPassword.Visibility = PwdConfirm.Visibility = Visibility.Collapsed;
                TxtPwdVisible.Visibility = TxtConfVisible.Visibility = Visibility.Visible;
            }
            else
            {
                PwdPassword.Password = TxtPwdVisible.Text;
                PwdConfirm.Password  = TxtConfVisible.Text;
                TxtPwdVisible.Visibility = TxtConfVisible.Visibility = Visibility.Collapsed;
                PwdPassword.Visibility = PwdConfirm.Visibility = Visibility.Visible;
            }
        }

        // ── Create ─────────────────────────────────────────────────────────────

        private async void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            var targets = _view.Where(r => r.IsSelected).ToList();
            if (targets.Count == 0) return;

            bool manual = RbManual.IsChecked == true;
            string manualPwd = null;
            if (manual)
            {
                bool show = ChkShow.IsChecked == true;
                manualPwd    = show ? TxtPwdVisible.Text  : PwdPassword.Password;
                string conf  = show ? TxtConfVisible.Text : PwdConfirm.Password;
                if (string.IsNullOrWhiteSpace(manualPwd))
                { MessageBox.Show(this, "Password is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                if (manualPwd != conf)
                { MessageBox.Show(this, "Passwords do not match.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            }

            string how = manual ? "the manual password you entered" : "a generated temporary password each";
            if (MessageBox.Show(this,
                    $"Create {targets.Count} account(s) with {how}?",
                    "Confirm Bulk Create", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            SetBusy(true);
            Progress.Maximum = targets.Count;
            Progress.Value   = 0;
            TxtStatus.Text   = "Creating accounts…";

            try
            {
                var progress = new Progress<int>(n =>
                {
                    Progress.Value = n;
                    TxtStatus.Text = $"Creating accounts… {n} of {targets.Count}";
                });

                _results = await _vm.CreateAccountsAsync(
                    targets,
                    r => manual ? manualPwd : PasswordHelper.GenerateTemporaryPassword(),
                    ChkMustChange.IsChecked == true,
                    ChkAssignRole.IsChecked == true,
                    progress);
            }
            catch (Exception ex)
            {
                SetBusy(false);
                MessageBox.Show(this, $"Bulk create failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Refresh();   // some accounts may already exist, so re-evaluate
                return;
            }

            SetBusy(false);
            if (manual)
                foreach (var r in _results.Where(r => r.Status == "Created"))
                    r.Password = "(manual password)";

            int created = _results.Count(r => r.Status == "Created");
            int skipped = _results.Count(r => r.Status == "Skipped");
            int failed  = _results.Count(r => r.Status == "Failed");
            AccountsCreated = created > 0;

            ResultsGrid.ItemsSource = _results;
            PreviewGrid.Visibility  = Visibility.Collapsed;
            ResultsGrid.Visibility  = Visibility.Visible;
            OptionsPanel.Visibility = Visibility.Collapsed;
            PagerPanel.Visibility   = Visibility.Collapsed;
            SearchPanel.Visibility  = Visibility.Collapsed;
            BtnCreate.Visibility    = Visibility.Collapsed;
            BtnClearFilters.Visibility = Visibility.Collapsed;
            BtnExport.Visibility    = created > 0 ? Visibility.Visible : Visibility.Collapsed;

            TxtSummary.Text = $"Done:  {created} created,  {skipped} skipped,  {failed} failed";
            TxtStatus.Text  = manual
                ? "Accounts appear in Account Management. Everyone was given the manual password you entered."
                : "Accounts appear in Account Management. Save the results now: temporary passwords are not shown again.";
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            Progress.Visibility     = busy ? Visibility.Visible : Visibility.Collapsed;
            BtnCreate.IsEnabled     = !busy;
            BtnClose.IsEnabled      = !busy;
            OptionsPanel.IsEnabled  = !busy;
            PreviewGrid.IsEnabled   = !busy;
            PagerPanel.IsEnabled    = !busy;
        }

        // ── Export / close ─────────────────────────────────────────────────────

        private static string Csv(string v)
        {
            v = v ?? "";
            return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_results == null) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter   = "CSV file (*.csv)|*.csv",
                FileName = $"BulkAccounts_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };
            if (dlg.ShowDialog(this) != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("Employee #,Employee Name,Username,Email,Role,Password,Status,Note");
            foreach (var r in _results)
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(r.EmployeeNumber), Csv(r.EmployeeName), Csv(r.Username), Csv(r.Email), Csv(r.Role),
                    Csv(r.Password), Csv(r.Status), Csv(r.Message)
                }));

            try
            {
                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                MessageBox.Show(this, "Saved. Keep this file secure and delete it once the credentials are handed out.",
                    "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not save file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_busy) e.Cancel = true;
            base.OnClosing(e);
        }
    }
}
