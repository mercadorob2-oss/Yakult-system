using System.Linq;
using System.Windows.Controls;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Pages.Set;
using Yakult.Inventory.App.WPF.Set.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using Yakult.Inventory.App.Wpf.Set.BulkAddFiles;
using Yakult.Inventory.App.Wpf.Set.BulkDeploy;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Set.Views
{
    public partial class SetPageView : UserControl
    {
        private readonly SetPageViewModel _vm;

        public SetPageView(bool restrictToConsumablesByDefault = false)
        {
            InitializeComponent();

            _vm = new SetPageViewModel(restrictToConsumablesByDefault);
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.ConfirmYesNo = (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Warning) == WinForms.DialogResult.Yes;
            _vm.RequestAddSet += OnRequestAddSet;
            _vm.RequestOpenSetDetail += OnRequestOpenSetDetail;
            _vm.RequestArchiveRows += OnRequestArchiveRows;
            _vm.RequestGenerateReport += OnRequestGenerateReport;
            _vm.RequestBulkAddFiles += OnRequestBulkAddFiles;
            _vm.RequestBulkDeploy += OnRequestBulkDeploy;
            _vm.HighlightApplied += row => SetsGrid.ScrollIntoView(row);

            RebuildSortByOptions();
            _vm.LoadSets();
        }

        public void ApplyInitialSearch(string query) => _vm.ApplyInitialSearch(query);

        public void HighlightSet(int setId) => _vm.RequestHighlight(setId);

        // A DataGridTemplateColumn CheckBox's TwoWay IsChecked binding does not reliably commit
        // back to the row when clicked (see SetPageView.xaml CellTemplate comment) — read the
        // toggled state explicitly here and write it back to the row manually.
        private void RowCheckBox_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is SetRow row)
                row.Selected = cb.IsChecked == true;

            // Belt-and-suspenders alongside the ViewModel's own Row_PropertyChanged subscription
            // — see RefreshSelectionState's doc comment.
            _vm.RefreshSelectionState();
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestAddSet()
        {
            using (var addDialog = new AddSetPage(showStatus: false))
            {
                if (addDialog.ShowDialog(GetOwner()) != WinForms.DialogResult.OK) return;

                if (addDialog.Tag is int newSetId && newSetId > 0)
                    OnRequestOpenSetDetail(newSetId); // matches original: opens detail, which itself reloads

                _vm.LoadSets(); // matches original: BtnAdd_Click reloads again after OpenSetDetail returns
            }
        }

        private void OnRequestOpenSetDetail(int setId)
        {
            using (var detail = new ViewSetDetailPage(setId))
            {
                detail.ShowDialog(GetOwner());
            }
            _vm.LoadSets();
        }

        private void OnRequestArchiveRows(System.Collections.Generic.List<SetRow> rows)
        {
            using (var dlg = new ArchiveSetDialog(rows.Select(r => r.Dto).ToList()))
            {
                if (dlg.ShowDialog(GetOwner()) == WinForms.DialogResult.OK)
                    _vm.CommitArchive(rows, dlg.ReasonText, dlg.ArchiveItems, dlg.DeactivateItems);
            }
        }

        private void OnRequestBulkAddFiles()
        {
            using (var bulkDialog = new BulkAddFilesWindow(_vm.AllRows))
            {
                bulkDialog.ShowDialog(GetOwner());
            }
            _vm.LoadSets();
        }

        private void OnRequestBulkDeploy()
        {
            var dialog = new BulkDeployWindow();
            SetWpfOwner(dialog);
            dialog.ShowDialog();
            _vm.LoadSets();
        }

        private void OnRequestGenerateReport(System.Collections.Generic.List<int> selectedSetIds)
        {
            if (selectedSetIds != null && selectedSetIds.Count > 0)
                ReportLauncher.ShowSetsReport(selectedSetIds);
            else
                ReportLauncher.ShowSetsReport();
        }

        private void RebuildSortByOptions()
        {
            var options = ListSortHelper.BuildOptions(SetsGrid);
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(SetsGrid);
            _vm.SetSortByOptions(options, defaultKey);
        }

        private void SetsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            _vm.SortByColumn(key);

            foreach (var col in SetsGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = _vm.CurrentSortDirection == System.ComponentModel.ListSortDirection.Descending
                ? System.ComponentModel.ListSortDirection.Descending
                : System.ComponentModel.ListSortDirection.Ascending;
        }

        private void SelectedBadge_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var summaries = _vm.GetSelectedRows().Select(r => new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowSummary
            {
                Id = r.SetId,
                Values = new[] { r.SetId.ToString(), r.SetCode, r.SetType, r.SetStatus }
            });

            var dialog = new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowsReviewDialog(
                "Selected Sets", "sets",
                new[] { "ID", "Set Code", "Type", "Status" },
                summaries);
            SetWpfOwner(dialog);
            dialog.ShowDialog();

            if (dialog.RemovedIds.Count > 0)
            {
                var removed = new System.Collections.Generic.HashSet<int>(dialog.RemovedIds);
                foreach (var row in _vm.GetSelectedRows())
                    if (removed.Contains(row.SetId))
                        row.Selected = false;
            }
        }

        private static void SetWpfOwner(System.Windows.Window window)
        {
            var owner = GetOwner();
            if (owner != null) new System.Windows.Interop.WindowInteropHelper(window).Owner = owner.Handle;
        }
    }
}
