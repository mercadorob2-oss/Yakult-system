using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Yakult.Inventory.App.Pages.Admin.AccountManagement;
using Yakult.Inventory.App.Pages.Department;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs;
using Yakult.Inventory.App.WPF.Admin.AccountManagement.ViewModels;
using Yakult.Inventory.App.WPF.Admin.DepartmentRequestHistory.ViewModels;
using Yakult.Inventory.App.WPF.Admin.DepartmentRequestHistory.Views;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Views
{
    public partial class DepartmentAccountsView : UserControl
    {
        private readonly DepartmentAccountsViewModel _vm;
        private string          _editOriginalValue = string.Empty;
        private DispatcherTimer _statusTimer;

        public DepartmentAccountsView()
        {
            InitializeComponent();
            _vm         = new DepartmentAccountsViewModel();
            DataContext  = _vm;
            Loaded      += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try { await _vm.LoadAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading Department Accounts:\n{ex.Message}", "Load Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            foreach (var col in MainGrid.Columns)
                col.SortDirection = null;
            try { await _vm.LoadAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CboFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || CboFilter.SelectedItem == null) return;
            _vm.FilterOption = ((ComboBoxItem)CboFilter.SelectedItem).Content?.ToString() ?? "All";
        }

        // ── Toolbar ──────────────────────────────────────────────────────────────────

        private void BtnAddDept_Click(object sender, RoutedEventArgs e)
        {
            var owner = System.Windows.Forms.Form.ActiveForm;
            using (var dlg = new AddDepartmentDialog())
            {
                if (dlg.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK)
                    _ = _vm.LoadAsync();
            }
        }

        // ── Row action buttons ────────────────────────────────────────────────────────

        private async void BtnCreateAccount_Click(object sender, RoutedEventArgs e)
        {
            var row = ((Button)sender).Tag as DepartmentAccountRowDto;
            if (row == null) return;

            string suggested = row.BuildSuggestedUsername();
            var dlg = new CreateDeptAccountWindow(row.CompanyName, row.DepartmentName, row.BranchName, suggested);
            SetWpfOwner(dlg);
            if (dlg.ShowDialog() != true) return;

            try
            {
                bool created = await _vm.CreateAccountAsync(row, dlg.Username, dlg.Password);
                if (!created)
                {
                    MessageBox.Show("That username already exists. Please choose a different one.",
                        "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                ShowStatus("Account created.");
                _vm.ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create account:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            var row = ((Button)sender).Tag as DepartmentAccountRowDto;
            if (row == null) return;

            // Same view as Admin Portal → Department Request History, fixed to this department.
            var view = new DepartmentRequestHistoryView(
                new DepartmentRequestHistoryViewModel(row.ComId, row.BranchId, row.DeptId));

            var window = new Window
            {
                Title                 = $"Request History: {row.CompanyName} / {row.DepartmentName} / {row.BranchName}",
                Content               = view,
                Width                 = 1280,
                Height                = 720,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar         = false
            };
            // Own it by the form hosting this page, not Form.ActiveForm (which can be null
            // and leave the dialog behind a disabled app; see CLAUDE.md WPF interop notes).
            var hostHandle = GetHostFormHandle();
            if (hostHandle != IntPtr.Zero)
                new WindowInteropHelper(window).Owner = hostHandle;
            window.ShowDialog();
        }

        private IntPtr GetHostFormHandle()
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                var control = System.Windows.Forms.Control.FromChildHandle(source.Handle);
                var form    = control?.TopLevelControl;
                if (form != null) return form.Handle;
            }
            return System.Windows.Forms.Form.ActiveForm?.Handle ?? IntPtr.Zero;
        }

        private async void BtnChangePwd_Click(object sender, RoutedEventArgs e)
        {
            var row = ((Button)sender).Tag as DepartmentAccountRowDto;
            if (row == null) return;

            var dlg = new ChangeDeptPasswordWindow(row.CompanyName, row.DepartmentName, row.BranchName);
            SetWpfOwner(dlg);
            if (dlg.ShowDialog() != true) return;

            try
            {
                await _vm.ChangePasswordAsync(row, dlg.NewPassword);
                ShowStatus("Password changed.");
                _vm.ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to change password:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnShowPwd_Click(object sender, RoutedEventArgs e)
        {
            var row = ((Button)sender).Tag as DepartmentAccountRowDto;
            if (row == null) return;

            var verifyDlg = new VerifyAdminPasswordWindow(AppSession.CurrentUserName);
            SetWpfOwner(verifyDlg);
            if (verifyDlg.ShowDialog() != true) return;

            try
            {
                bool verified = await _vm.VerifyAdminPasswordAsync(verifyDlg.EnteredPassword);
                if (!verified)
                {
                    MessageBox.Show("Incorrect password. Access denied.", "Authentication Failed",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string plain = await _vm.GetPlainPasswordAsync(row);
                if (string.IsNullOrEmpty(plain))
                {
                    MessageBox.Show(
                        "No stored password is available for this account.\n\n" +
                        "This may happen for accounts created before the Show Password feature was added. " +
                        "Use Change Password to set a new one.",
                        "Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var showDlg = new ShowDeptPasswordWindow(
                    row.CompanyName, row.DepartmentName, row.BranchName, row.Username, plain);
                SetWpfOwner(showDlg);
                showDlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error retrieving password:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEditAccount_Click(object sender, RoutedEventArgs e)
        {
            var row = ((Button)sender).Tag as DepartmentAccountRowDto;
            if (row == null) return;

            var emails = _vm.EmailIdMap
                .Select(kv => (EmailId: kv.Value, Label: kv.Key))
                .OrderBy(t => t.Label)
                .ToList();

            var dlg = new EditDeptAccountWindow(row, emails);
            SetWpfOwner(dlg);
            if (dlg.ShowDialog() == true)
                _ = _vm.LoadAsync();
        }

        private async void BtnDeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            var row = ((Button)sender).Tag as DepartmentAccountRowDto;
            if (row == null) return;

            var result = MessageBox.Show(
                $"Delete account for:\n{row.CompanyName} / {row.DepartmentName} / {row.BranchName}\n\nUsername: {row.Username}\n\nThis will clear credentials. The row will remain.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _vm.DeleteAccountAsync(row);
                ShowStatus("Account deleted.");
                _vm.ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete account:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Inline email editing ──────────────────────────────────────────────────────

        private void MainGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            string colName = e.Column.Header?.ToString() ?? "";
            if (colName != "Dept Email" && colName != "Branch Email")
            {
                e.Cancel = true;
                return;
            }

            var row = e.Row.Item as DepartmentAccountRowDto;
            if (row == null) { e.Cancel = true; return; }

            _editOriginalValue = colName == "Dept Email"
                ? (row.DepartmentEmail ?? "")
                : (row.BranchEmail     ?? "");
        }

        private async void MainGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;

            string colName = e.Column.Header?.ToString() ?? "";
            if (colName != "Dept Email" && colName != "Branch Email") return;

            var row = e.Row.Item as DepartmentAccountRowDto;
            if (row == null) return;

            var tb = e.EditingElement as TextBox;
            if (tb == null) return;

            string newVal = tb.Text.Trim();
            if (newVal == _editOriginalValue) return;

            if (string.IsNullOrEmpty(newVal))
            {
                var confirm = MessageBox.Show(
                    $"Remove the {colName} email for this row?",
                    "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                {
                    tb.Text = _editOriginalValue;
                    return;
                }

                try
                {
                    if (colName == "Dept Email")
                        await _vm.DeleteDeptEmailAsync(row.CompanyName, row.DepartmentName);
                    else
                        await _vm.DeleteBranchEmailAsync(row);
                    ShowStatus("Email removed.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to remove email:\n{ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            if (!DepartmentAccountsViewModel.IsValidEmail(newVal))
            {
                MessageBox.Show($"'{newVal}' is not a valid email address.", "Invalid Email",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                tb.Text = _editOriginalValue;
                return;
            }

            try
            {
                int emailId = await ResolveOrCreateEmailIdAsync(newVal);
                if (emailId <= 0) { tb.Text = _editOriginalValue; return; }

                if (colName == "Dept Email")
                    await _vm.UpsertDeptEmailAsync(row.CompanyName, row.DepartmentName, emailId);
                else
                    await _vm.SaveBranchEmailAsync(row, emailId);

                ShowStatus("Email saved.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save email:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                tb.Text = _editOriginalValue;
            }
        }

        private async System.Threading.Tasks.Task<int> ResolveOrCreateEmailIdAsync(string email)
        {
            // Only prompt for genuinely new addresses (not in the loaded directory).
            if (!_vm.EmailIdMap.ContainsKey(email))
            {
                var result = MessageBox.Show(
                    $"'{email}' is not in the email directory.\n\nAdd it?",
                    "New Email", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return 0;
            }

            // Always resolve through the idempotent upsert: a cached id can be stale if the
            // address was deleted elsewhere (e.g. Email Configuration), which would fail the FK.
            return await _vm.InsertOrGetEmailIdAsync(email);
        }

        private void ShowStatus(string msg)
        {
            _vm.StatusMessage = msg;
            _statusTimer?.Stop();
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _statusTimer.Tick += (s, e) => { _vm.StatusMessage = null; _statusTimer.Stop(); };
            _statusTimer.Start();
        }

        private static void SetWpfOwner(System.Windows.Window window)
        {
            var owner = System.Windows.Forms.Form.ActiveForm;
            if (owner != null)
                new WindowInteropHelper(window).Owner = owner.Handle;
        }

        private void BtnFirst_Click(object sender, RoutedEventArgs e) => _vm.GoToFirstPage();
        private void BtnPrev_Click(object sender, RoutedEventArgs e)  => _vm.GoToPrevPage();
        private void BtnNext_Click(object sender, RoutedEventArgs e)  => _vm.GoToNextPage();
        private void BtnLast_Click(object sender, RoutedEventArgs e)  => _vm.GoToLastPage();

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

        private static void PositionPopupNearButton(System.Windows.Window popup, Button btn)
        {
            try
            {
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
    }
}
