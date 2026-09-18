using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using ClosedXML.Excel;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs;
using Yakult.Inventory.App.WPF.ConsumableManagement.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.ConsumableManagement.Views
{
    public partial class ViewConsumableModelsView : UserControl
    {
        private readonly ViewConsumableModelsViewModel _vm;
        private CheckBox _selectAllHeaderChk;

        public ViewConsumableModelsView()
        {
            InitializeComponent();
            _vm = new ViewConsumableModelsViewModel();
            DataContext = _vm;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = _vm.LoadAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _vm.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing data:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("ViewConsumableModelsView.BtnRefresh_Click failed", ex);
            }
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e) { _vm.GoToPrevPage(); SyncSelectAllHeader(); }
        private void BtnNext_Click(object sender, RoutedEventArgs e) { _vm.GoToNextPage(); SyncSelectAllHeader(); }

        private void ChkSelectAllHeader_Loaded(object sender, RoutedEventArgs e)
        {
            _selectAllHeaderChk = sender as CheckBox;
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is CheckBox chk)) return;
            bool select = chk.IsChecked == true;
            _vm.SetPageSelected(select);
        }

        private void RowSelectCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (((CheckBox)sender).DataContext is ConsumableModelRowVm row)
                _vm.SetRowSelected(row.ConsumableModelId, ((CheckBox)sender).IsChecked == true);
        }

        private void SelectedBadge_Click(object sender, RoutedEventArgs e)
        {
            var summaries = _vm.GetSelectedRows().Select(r => new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowSummary
            {
                Id = r.ConsumableModelId,
                Values = new[] { r.ConsumableModelId.ToString(), r.ModelNumber, r.Category, r.AvailableStock.ToString() }
            });

            var dialog = new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowsReviewDialog(
                "Selected Consumable Models", "models",
                new[] { "ID", "Model Number", "Category", "Available Stock" },
                summaries);
            SetWpfOwner(dialog);
            dialog.ShowDialog();

            foreach (var id in dialog.RemovedIds)
                _vm.SetRowSelected(id, false);
        }

        private void SyncSelectAllHeader()
        {
            if (_selectAllHeaderChk == null) return;
            _selectAllHeaderChk.IsChecked = _vm.PagedRows.Count > 0
                && _vm.PagedRows.All(r => r.IsSelected);
        }

        private void MainGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;

            string header = e.Column.Header?.ToString() ?? "";
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

        private static void PositionPopupNearButton(Window popup, Button btn)
        {
            try
            {
                var pt = btn.PointToScreen(new Point(0, btn.ActualHeight));
                var source = PresentationSource.FromVisual(btn);
                if (source?.CompositionTarget != null)
                    pt = source.CompositionTarget.TransformFromDevice.Transform(pt);

                var area = SystemParameters.WorkArea;
                double estH = double.IsNaN(popup.Height) ? popup.MaxHeight : popup.Height;
                popup.Left = Math.Max(area.Left, Math.Min(pt.X, area.Right - popup.Width));
                popup.Top = Math.Max(area.Top, Math.Min(pt.Y, area.Bottom - estH));
            }
            catch { popup.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
        }

        private static void SetWpfOwner(Window window)
        {
            var owner = WinForms.Form.ActiveForm;
            if (owner != null) new WindowInteropHelper(window).Owner = owner.Handle;
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var selected = _vm.GetSelectedRows();
            var rows = selected.Count > 0 ? selected : _vm.GetFilteredForExport();
            if (rows == null || rows.Count == 0)
            {
                MessageBox.Show("No consumable models to export.", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName   = $"ConsumableModels_{DateTime.Now:yyyyMMdd_HHmmss}",
                DefaultExt = ".xlsx",
                Filter     = "Excel Workbook|*.xlsx"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                using (var wb = new XLWorkbook())
                {
                    var ws = wb.Worksheets.Add("Consumable Models");

                    var headers = new[] { "ID", "Model Number", "Category", "Available Stock",
                                          "Requestable", "Created At", "Created By" };
                    for (int i = 0; i < headers.Length; i++)
                        ws.Cell(1, i + 1).Value = headers[i];

                    var hdr = ws.Range(1, 1, 1, headers.Length);
                    hdr.Style.Font.Bold = true;
                    hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#4E9AFC");
                    hdr.Style.Font.FontColor = XLColor.White;
                    hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    int row = 2;
                    foreach (var m in rows)
                    {
                        ws.Cell(row, 1).Value = m.ConsumableModelId;
                        ws.Cell(row, 2).Value = m.ModelNumber ?? "";
                        ws.Cell(row, 3).Value = m.Category ?? "";
                        ws.Cell(row, 4).Value = m.AvailableStock;
                        ws.Cell(row, 5).Value = m.IsRequestable ? "Yes" : "No";
                        ws.Cell(row, 6).Value = m.CreatedAt.ToString("MM/dd/yyyy");
                        ws.Cell(row, 7).Value = m.CreatedByName ?? "";
                        row++;
                    }

                    // Set explicit column widths (AdjustToContents triggers a SixLabors.Fonts version conflict)
                    int[] widths = { 8, 24, 16, 16, 14, 14, 26 };
                    for (int c = 0; c < widths.Length; c++)
                        ws.Column(c + 1).Width = widths[c];

                    wb.SaveAs(dlg.FileName);
                }

                var open = MessageBox.Show(
                    $"Exported {rows.Count} consumable model(s) successfully.\n\nOpen file?",
                    "Export Complete", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (open == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("ViewConsumableModelsView.BtnExport_Click failed", ex);
            }
        }

        private async void BtnCompareCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "CSV Files|*.csv",
                Title  = "Select Beginning Balance CSV"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                (sender as Button).IsEnabled = false;

                var service = new ConsumableStockReconciliationService();
                var results = await service.CompareAsync(dlg.FileName);

                using (var resultsDialog = BuildComparisonResultsDialog(results))
                {
                    resultsDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Comparison failed:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("ViewConsumableModelsView.BtnCompareCsv_Click failed", ex);
            }
            finally
            {
                (sender as Button).IsEnabled = true;
            }
        }

        private static WinForms.Form BuildComparisonResultsDialog(System.Collections.Generic.List<ConsumableStockComparisonRow> results)
        {
            int mismatches = results.Count(r => !r.IsMatch);

            var form = new WinForms.Form
            {
                Text = $"CSV Stock Comparison — {mismatches} mismatch(es) of {results.Count}",
                Size = new System.Drawing.Size(900, 560),
                StartPosition = WinForms.FormStartPosition.CenterScreen
            };

            var grid = new WinForms.DataGridView
            {
                Dock = WinForms.DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = WinForms.DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                SelectionMode = WinForms.DataGridViewSelectionMode.FullRowSelect
            };

            grid.Columns.Add("ModelNumber", "Model Number");
            grid.Columns.Add("Category", "Category");
            grid.Columns.Add("CsvBalance", "CSV Balance");
            grid.Columns.Add("DbAvailableStock", "DB Available");
            grid.Columns.Add("OutstandingRequestedQty", "Outstanding Requested");
            grid.Columns.Add("NetDbStock", "Net DB Stock");
            grid.Columns.Add("Difference", "Difference");
            grid.Columns.Add("StatusText", "Status");

            foreach (var r in results.OrderBy(r => r.IsMatch).ThenBy(r => r.ModelNumber))
            {
                string status = !r.FoundInDatabase ? "Not Found in DB" : r.IsMatch ? "Match" : "Mismatch";

                int idx = grid.Rows.Add(
                    r.ModelNumber, r.Category, r.CsvBalance,
                    r.FoundInDatabase ? (object)r.DbAvailableStock : "—",
                    r.FoundInDatabase ? (object)r.OutstandingRequestedQty : "—",
                    r.FoundInDatabase ? (object)r.NetDbStock : "—",
                    r.FoundInDatabase ? (object)r.Difference : "—",
                    status);

                if (!r.FoundInDatabase)
                    grid.Rows[idx].DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(255, 235, 205);
                else if (!r.IsMatch)
                    grid.Rows[idx].DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(255, 210, 210);
            }

            form.Controls.Add(grid);
            return form;
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is ConsumableModelRowVm row)) return;

            if (row.IsCartridge)
            {
                var win = new WPF.CartridgeManagement.Views.EditCartridgeModelWindow(row.ConsumableModelId)
                    { Owner = Window.GetWindow(this) };
                if (win.ShowDialog() == true)
                    _ = _vm.LoadAsync();
            }
            else
            {
                var win = new EditConsumableModelWindow(row.ConsumableModelId) { Owner = Window.GetWindow(this) };
                if (win.ShowDialog() == true)
                    _ = _vm.LoadAsync();
            }
        }

        private async void BtnArchiveSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!Yakult.Inventory.App.Security.PermissionResolver.CanPerformAction("ConsumableModel.Archive"))
            {
                MessageBox.Show("You do not have permission to archive consumable models.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selected = _vm.GetSelectedRows();
            if (selected.Count == 0)
            {
                MessageBox.Show("Select one or more models to archive first.", "Nothing Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int totalStock = selected.Sum(r => r.AvailableStock);
            int totalItems = 0;
            foreach (var row in selected)
                totalItems += await _vm.GetLinkedItemCountAsync(row);

            var stockWarning = totalStock > 0
                ? $"\n\n{totalStock} unit(s) of stock across {totalItems} item(s) will no longer be counted in this list once archived (the items themselves keep their quantity and stay active elsewhere)."
                : "";

            var result = MessageBox.Show(
                $"Archive {selected.Count} selected model(s)?\n\nThey will be moved to the archive and will no longer appear in active lists.{stockWarning}",
                "Confirm Archive",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            var errors = new System.Collections.Generic.List<string>();
            foreach (var row in selected)
            {
                try
                {
                    await _vm.ArchiveAsync(row, AppSession.CurrentUserName);
                }
                catch (Exception ex)
                {
                    errors.Add($"{row.ModelNumber}: {ex.Message}");
                    Logger.LogError("ViewConsumableModelsView.BtnArchiveSelected_Click failed", ex);
                }
            }

            await _vm.LoadAsync();
            SyncSelectAllHeader();

            if (errors.Count > 0)
                MessageBox.Show($"{selected.Count - errors.Count} model(s) archived.\n\nFailed:\n{string.Join("\n", errors)}",
                    "Archive Completed With Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!Yakult.Inventory.App.Security.PermissionResolver.CanPerformAction("ConsumableModel.Delete"))
            {
                MessageBox.Show("You do not have permission to delete consumable models.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selected = _vm.GetSelectedRows();
            if (selected.Count == 0)
            {
                MessageBox.Show("Select one or more models to delete first.", "Nothing Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int totalLinkedItems = 0;
            var blockers = new System.Collections.Generic.Dictionary<ConsumableModelRowVm, (int emptyCartridgeCount, int batchLineCount)>();
            foreach (var row in selected)
            {
                totalLinkedItems += await _vm.GetLinkedItemCountAsync(row);
                var blocked = await _vm.GetForceDeleteBlockersAsync(row);
                if (blocked.emptyCartridgeCount > 0 || blocked.batchLineCount > 0)
                    blockers[row] = blocked;
            }
            int totalStock = selected.Sum(r => r.AvailableStock);

            var warning = totalLinkedItems > 0
                ? $"\n\n{totalLinkedItems} linked item(s) carrying {totalStock} unit(s) of stock will be unlinked from these models (the items and their quantities are kept, just ungrouped)."
                : "";

            var result = MessageBox.Show(
                $"Permanently delete {selected.Count} selected model(s)?\n\nThis cannot be undone.{warning}",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            bool forceBlocked = false;
            if (blockers.Count > 0)
            {
                var detail = string.Join("\n", blockers.Select(b =>
                    $"  • {b.Key.ModelNumber}: {b.Value.emptyCartridgeCount} returned-cartridge record(s), {b.Value.batchLineCount} vendor batch line(s)"));
                var forceResult = MessageBox.Show(
                    $"{blockers.Count} of the selected model(s) still have physical inventory / audit history and can't be deleted normally:\n\n{detail}\n\n" +
                    "Force delete anyway? This will PERMANENTLY destroy those returned-cartridge and vendor batch records too.",
                    "Blocked By Inventory History",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                forceBlocked = forceResult == MessageBoxResult.Yes;
            }

            var errors = new System.Collections.Generic.List<string>();
            foreach (var row in selected)
            {
                try
                {
                    if (blockers.ContainsKey(row))
                    {
                        if (forceBlocked)
                            await _vm.ForceDeleteAsync(row);
                        else
                            errors.Add($"{row.ModelNumber}: skipped (blocked by inventory history)");
                    }
                    else
                    {
                        await _vm.DeleteAsync(row);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{row.ModelNumber}: {ex.Message}");
                    Logger.LogError("ViewConsumableModelsView.BtnDeleteSelected_Click failed", ex);
                }
            }

            await _vm.LoadAsync();
            SyncSelectAllHeader();

            if (errors.Count > 0)
                MessageBox.Show($"{selected.Count - errors.Count} model(s) deleted.\n\nNot deleted:\n{string.Join("\n", errors)}",
                    "Delete Completed With Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = BuildAddDialog())
            {
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    _ = _vm.LoadAsync();
            }
        }

        private static WinForms.Form BuildAddDialog()
        {
            var dialog = new WinForms.Form
            {
                Text = "Add Consumable Model",
                Size = new System.Drawing.Size(420, 300),
                StartPosition = WinForms.FormStartPosition.CenterScreen,
                FormBorderStyle = WinForms.FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var mainPanel = new WinForms.TableLayoutPanel
            {
                Dock = WinForms.DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new WinForms.Padding(20)
            };
            mainPanel.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Absolute, 120F));
            mainPanel.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Percent, 100F));
            dialog.Controls.Add(mainPanel);

            var lblModelNumber = new WinForms.Label { Text = "Model Number *", Dock = WinForms.DockStyle.Fill };
            var txtModelNumber = new WinForms.TextBox { Dock = WinForms.DockStyle.Fill };
            mainPanel.Controls.Add(lblModelNumber, 0, 0);
            mainPanel.Controls.Add(txtModelNumber, 1, 0);

            var lblCategory = new WinForms.Label { Text = "Category *", Dock = WinForms.DockStyle.Fill };
            var cmbCategory = new WinForms.ComboBox { Dock = WinForms.DockStyle.Fill, DropDownStyle = WinForms.ComboBoxStyle.DropDownList };
            cmbCategory.Items.AddRange(new object[] { "Ink", "Toner", "Print Head" });
            cmbCategory.SelectedIndex = 0;
            mainPanel.Controls.Add(lblCategory, 0, 1);
            mainPanel.Controls.Add(cmbCategory, 1, 1);

            var chkRequestable = new WinForms.CheckBox { Text = "Is Requestable", Checked = true, Dock = WinForms.DockStyle.Fill };
            mainPanel.Controls.Add(new WinForms.Label(), 0, 2);
            mainPanel.Controls.Add(chkRequestable, 1, 2);

            var buttonPanel = new WinForms.FlowLayoutPanel
            {
                FlowDirection = WinForms.FlowDirection.RightToLeft,
                Dock = WinForms.DockStyle.Fill,
                AutoSize = true
            };

            var btnSave = new WinForms.Button { Text = "Save", Width = 80, DialogResult = WinForms.DialogResult.None };
            var btnCancel = new WinForms.Button { Text = "Cancel", Width = 80, DialogResult = WinForms.DialogResult.Cancel };

            btnSave.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtModelNumber.Text))
                {
                    WinForms.MessageBox.Show("Please enter a model number.", "Validation Error",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                    return;
                }

                string category = cmbCategory.SelectedItem?.ToString() ?? "Ink";

                try
                {
                    btnSave.Enabled = false;
                    btnSave.Text = "Saving...";

                    var repo = new ConsumableModelRepository();
                    var existing = await repo.FindByModelNumberAsync(txtModelNumber.Text.Trim(), category);
                    if (existing != null)
                    {
                        WinForms.MessageBox.Show($"Model number '{txtModelNumber.Text.Trim()}' already exists for {category}.",
                            "Duplicate", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                        btnSave.Enabled = true;
                        btnSave.Text = "Save";
                        return;
                    }

                    var model = new ConsumableModelDto
                    {
                        ModelNumber = txtModelNumber.Text.Trim(),
                        Category = category,
                        IsRequestable = chkRequestable.Checked,
                        IsActive = true,
                        CreatedBy = AppSession.CurrentUserId,
                        CreatedAt = DateTime.Now
                    };

                    await repo.CreateAsync(model);
                    WinForms.MessageBox.Show("Consumable model added successfully!", "Success",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);

                    dialog.DialogResult = WinForms.DialogResult.OK;
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    WinForms.MessageBox.Show($"Error: {ex.Message}", "Error",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
                    btnSave.Enabled = true;
                    btnSave.Text = "Save";
                }
            };

            buttonPanel.Controls.Add(btnSave);
            buttonPanel.Controls.Add(btnCancel);
            mainPanel.Controls.Add(buttonPanel, 0, 3);
            mainPanel.SetColumnSpan(buttonPanel, 2);

            dialog.AcceptButton = btnSave;
            dialog.CancelButton = btnCancel;

            return dialog;
        }
    }
}
