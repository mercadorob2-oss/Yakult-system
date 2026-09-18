using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Yakult.Inventory.App.Pages.Inventory;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.Inventory.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Inventory.Views
{
    public partial class InventoryPageView : UserControl
    {
        private readonly InventoryPageViewModel _vm;

        public InventoryPageView()
        {
            InitializeComponent();

            _vm = new InventoryPageViewModel();
            DataContext = _vm;

            _vm.ErrorOccurred += msg => ShowError("Error", msg);
            _vm.RequestErrorInventory += ShowError;
            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestEditInventory += OnRequestEditInventory;
            _vm.RequestArchiveConfirm += OnRequestArchiveConfirm;
            _vm.RebuildSortByOptionsRequested += RebuildSortByOptions;

            RebuildSortByOptions();
        }

        public void ApplyInitialSearch(string query) => _vm.ApplyInitialSearch(query);

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void ShowError(string title, string message)
            => WinForms.MessageBox.Show(GetOwner(), message, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);

        private DataGrid ActiveGrid
        {
            get
            {
                switch (_vm.ActiveTabIndex)
                {
                    case 0: return CategoryStockGrid;
                    case 1: return HardwareGrid;
                    case 2: return SoftwareLicenseGrid;
                    case 3: return ServicesGrid;
                    default: return InventoryGrid;
                }
            }
        }

        private void RebuildSortByOptions()
        {
            var grid = ActiveGrid;
            var options = ListSortHelper.BuildOptions(grid);
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(grid);
            _vm.SetSortByOptions(options, defaultKey);
        }

        private void Grid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var grid = (DataGrid)sender;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            var direction = e.Column.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            _vm.SortByColumn(key, direction);

            foreach (var col in grid.Columns) col.SortDirection = null;
            e.Column.SortDirection = direction;
        }

        private void SelectAllCheckBox_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            bool newState = ((CheckBox)sender).IsChecked == true;
            foreach (var entry in InventoryGrid.Items.OfType<InventoryViewDto>())
                entry.Selected = newState;
            InventoryGrid.Items.Refresh();
        }

        private void InventoryGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!AppSession.IsReadOnly)
                _vm.EditCommand.Execute(null);
        }

        private void OnRequestEditInventory(InventoryViewDto entry)
        {
            try
            {
                using (var dialog = new EditInventoryDialog(entry))
                {
                    if (dialog.ShowDialog(GetOwner()) == WinForms.DialogResult.OK)
                        _vm.OnInventoryEdited();
                }
            }
            catch (System.Exception ex)
            {
                WinForms.MessageBox.Show($"Error editing inventory entry: {ex.Message}", "Error",
                    WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        /// <summary>Ported verbatim from BtnDelete_Click's inline archive-reason dialog.</summary>
        private void OnRequestArchiveConfirm(List<InventoryViewDto> checkedEntries)
        {
            string message = checkedEntries.Count == 1
                ? "Are you sure you want to archive the selected entry?"
                : $"Are you sure you want to archive the {checkedEntries.Count} selected entries?";

            using (var archiveDialog = new WinForms.Form())
            {
                archiveDialog.Text = "Archive Inventory Entry";
                archiveDialog.Size = new Size(500, 300);
                archiveDialog.StartPosition = WinForms.FormStartPosition.CenterParent;
                archiveDialog.FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
                archiveDialog.MaximizeBox = false;
                archiveDialog.MinimizeBox = false;

                var lblMessage = new WinForms.Label
                {
                    Text = message + "\n\nThe entry will be moved to the archive.",
                    AutoSize = false,
                    Size = new Size(460, 100),
                    Location = new System.Drawing.Point(10, 10)
                };

                var lblReason = new WinForms.Label
                {
                    Text = "Reason for archiving:",
                    AutoSize = true,
                    Location = new System.Drawing.Point(10, 115)
                };

                var txtReason = new WinForms.TextBox
                {
                    Size = new Size(460, 20),
                    Location = new System.Drawing.Point(10, 135)
                };

                var btnArchive = new WinForms.Button
                {
                    Text = "Archive",
                    DialogResult = WinForms.DialogResult.OK,
                    Location = new System.Drawing.Point(290, 165),
                    Size = new Size(90, 25)
                };

                var btnCancel = new WinForms.Button
                {
                    Text = "Cancel",
                    DialogResult = WinForms.DialogResult.Cancel,
                    Location = new System.Drawing.Point(390, 165),
                    Size = new Size(90, 25)
                };

                archiveDialog.Controls.AddRange(new WinForms.Control[] { lblMessage, lblReason, txtReason, btnArchive, btnCancel });
                archiveDialog.AcceptButton = btnArchive;
                archiveDialog.CancelButton = btnCancel;

                if (archiveDialog.ShowDialog(GetOwner()) == WinForms.DialogResult.OK)
                {
                    string reason = string.IsNullOrWhiteSpace(txtReason.Text) ? "No reason provided" : txtReason.Text;
                    _vm.ArchiveEntries(checkedEntries, reason);
                }
            }
        }
    }
}
