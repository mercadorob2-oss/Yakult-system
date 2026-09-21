using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Pages.Request;
using Yakult.Inventory.App.WPF.Request.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Request.Views
{
    public partial class RequestPageView : UserControl
    {
        private readonly RequestPageViewModel _vm;

        public RequestPageView() : this(null, false) { }

        /// <param name="allowedCategories">
        /// When provided, restricts this page to only requests whose Item.Category is in this
        /// set (case-insensitive) — used by the Consumable Management Portal's Card 2 to scope
        /// View Requests to Cartridge/Ink/Printhead/Toner Cartridge. Null shows everything.
        /// </param>
        /// <param name="restrictToRequestSetManagementWorkflow">
        /// When true, additionally excludes requests whose dbo.Request.WorkflowType is explicitly
        /// 'CartridgeManagement' (pure-cartridge submissions, which own Card 1's queue instead).
        /// </param>
        public RequestPageView(System.Collections.Generic.IEnumerable<string> allowedCategories, bool restrictToRequestSetManagementWorkflow = false)
        {
            InitializeComponent();

            _vm = new RequestPageViewModel();
            if (allowedCategories != null)
                _vm.AllowedCategories = new System.Collections.Generic.HashSet<string>(allowedCategories, System.StringComparer.OrdinalIgnoreCase);
            _vm.RestrictToRequestSetManagementWorkflow = restrictToRequestSetManagementWorkflow;
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.ConfirmYesNo = (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Warning) == WinForms.DialogResult.Yes;
            _vm.ConfirmYesNoCancel = (title, msg) =>
            {
                var result = WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.YesNoCancel, WinForms.MessageBoxIcon.Warning);
                if (result == WinForms.DialogResult.Yes) return "Yes";
                if (result == WinForms.DialogResult.No) return "No";
                return "Cancel";
            };
            _vm.RequestSaveFilePath = OnRequestSaveFilePath;
            _vm.RequestAddNew += OnRequestAddNew;
            _vm.RequestEditRow += OnRequestEditRow;
            _vm.RequestArchiveRows += OnRequestArchiveRows;
            _vm.RequestBulkAddToSet += OnRequestBulkAddToSet;

            RebuildSortByOptions();
            _vm.LoadRequests();
        }

        public void ApplyInitialSearch(string query) => _vm.ApplyInitialSearch(query);

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestAddNew()
        {
            using (var dialog = new BatchAddRequestDialog())
            {
                if (dialog.ShowDialog(GetOwner()) == WinForms.DialogResult.OK)
                    _vm.LoadRequests();
            }
        }

        private void OnRequestEditRow(RequestRow row)
        {
            string originalStatus = row.Dto.Status;
            using (var editDialog = new EditRequestDialog(row.Dto))
            {
                if (editDialog.ShowDialog(GetOwner()) != WinForms.DialogResult.OK) return;

                if (editDialog.Tag?.ToString() == "DELETED")
                {
                    _vm.LoadRequests();
                    return;
                }

                _vm.CommitEditedRequest(row.Dto, originalStatus);
            }
        }

        private void OnRequestArchiveRows(System.Collections.Generic.List<RequestRow> rows)
        {
            using (var dlg = new ArchiveRequestDialog(rows.Select(r => r.Dto).ToList()))
            {
                if (dlg.ShowDialog(GetOwner()) == WinForms.DialogResult.OK)
                    _vm.CommitArchive(rows, dlg.ReasonText, dlg.AlsoArchiveItem);
            }
        }

        // Same pattern as BatchAddRequestDialog.xaml.cs's OfferAddToSetAsync / ItemsPageView's
        // Bulk Add to Invoice: pick-or-create Set dialog, then link each checked request to it
        // via SetRepository.AddRequestToSetAsync.
        private async void OnRequestBulkAddToSet(System.Collections.Generic.List<int> reqIds)
        {
            try
            {
                var owner = GetOwner();

                using (var dlg = new Yakult.Inventory.App.Pages.Set.AddRequestsToSetDialog(reqIds.Count))
                {
                    var result = owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
                    if (result != WinForms.DialogResult.OK || !dlg.ResultSetId.HasValue)
                        return;

                    Mouse.OverrideCursor = Cursors.Wait;
                    try
                    {
                        var setRepo = new Yakult.Inventory.App.Repositories.SetRepository();
                        foreach (var reqId in reqIds)
                            await setRepo.AddRequestToSetAsync(reqId, dlg.ResultSetId.Value);
                    }
                    finally
                    {
                        Mouse.OverrideCursor = null;
                    }

                    _vm.LoadRequests();

                    WinForms.MessageBox.Show(owner, $"Linked {reqIds.Count} request(s) to the Set.", "Bulk Add to Set",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);

                    // Jump straight to the Set that was created/selected, matching
                    // BatchAddRequestDialog's OfferAddToSetAsync behavior.
                    using (var detail = new Yakult.Inventory.App.Pages.Set.ViewSetDetailPage(dlg.ResultSetId.Value))
                    {
                        detail.ShowDialog(owner);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Mouse.OverrideCursor = null;
                WinForms.MessageBox.Show(GetOwner(), $"Failed to add requests to Set: {ex.Message}", "Error",
                    WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        private string OnRequestSaveFilePath(string defaultFileName)
        {
            using (var sfd = new WinForms.SaveFileDialog())
            {
                sfd.Title = "Export Selected Requests to PDF";
                sfd.Filter = "PDF (*.pdf)|*.pdf";
                sfd.DefaultExt = "pdf";
                sfd.FileName = defaultFileName;
                return sfd.ShowDialog(GetOwner()) == WinForms.DialogResult.OK ? sfd.FileName : null;
            }
        }

        private void RebuildSortByOptions()
        {
            var options = ListSortHelper.BuildOptions(RequestsGrid);
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(RequestsGrid);
            _vm.SetSortByOptions(options, defaultKey);
        }

        private void RequestsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            _vm.SortByColumn(key);

            foreach (var col in RequestsGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = _vm.CurrentSortDirection == System.ComponentModel.ListSortDirection.Descending
                ? System.ComponentModel.ListSortDirection.Descending
                : System.ComponentModel.ListSortDirection.Ascending;
        }

        private void CloseMoreActionsDropdown(object sender, RoutedEventArgs e) => MoreActionsDropdownToggle.IsChecked = false;

        private void RequestsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is CheckBox) return; // double-click on the checkbox column is not an "edit" gesture
                source = VisualTreeHelper.GetParent(source);
            }

            if (_vm.EditCommand.CanExecute(null))
                _vm.EditCommand.Execute(null);
        }

        private void RowSelectCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // Set explicitly from the checkbox's own (guaranteed-current) IsChecked rather than
            // trusting the TwoWay binding already pushed it by the time this event fires — in
            // this ElementHost-hosted WPF surface that push isn't reliably synchronous, which was
            // undercounting SelectedCount (only the first-checked row ever got counted).
            if (sender is CheckBox cb && cb.DataContext is RequestRow row)
                row.Selected = cb.IsChecked == true;

            _vm.RefreshSelectionState();
        }

        private void SelectedBadge_Click(object sender, RoutedEventArgs e)
        {
            var summaries = _vm.GetSelectedRows().Select(r => new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowSummary
            {
                Id = r.ReqId,
                Values = new[] { r.ReqId.ToString(), r.EmployeeName, r.ItemName, r.Status }
            });

            var dialog = new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowsReviewDialog(
                "Selected Requests", "requests",
                new[] { "ID", "Employee", "Item", "Status" },
                summaries);
            SetWpfOwner(dialog);
            dialog.ShowDialog();

            if (dialog.RemovedIds.Count > 0)
            {
                var removed = new System.Collections.Generic.HashSet<int>(dialog.RemovedIds);
                foreach (var row in _vm.GetSelectedRows())
                    if (removed.Contains(row.ReqId))
                        row.Selected = false;
            }
        }

        private static void SetWpfOwner(Window window)
        {
            var owner = GetOwner();
            if (owner != null) new System.Windows.Interop.WindowInteropHelper(window).Owner = owner.Handle;
        }
    }
}
