using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Item;
using Yakult.Inventory.App.Pages.Software;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.Items.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Items.Views
{
    public partial class ItemsPageView : UserControl
    {
        private readonly ItemsPageViewModel _vm;
        private readonly ItemRepository _itemRepo = new ItemRepository();

        public ItemsPageView()
        {
            InitializeComponent();

            _vm = new ItemsPageViewModel();
            DataContext = _vm;

            _vm.RequestAddItems += OnRequestAddItems;
            _vm.RequestEditItem += OnRequestEditItem;
            _vm.RequestArchiveItems += OnRequestArchiveItems;
            _vm.RequestDeleteItems += OnRequestDeleteItems;
            _vm.RequestGetFromMobile += OnRequestGetFromMobile;
            _vm.RequestImport += OnRequestImport;
            _vm.RequestDownloadTemplate += OnRequestDownloadTemplate;
            _vm.RequestExportPdf += OnRequestExportPdf;
            _vm.RequestBulkAddRequest += OnRequestBulkAddRequest;
            _vm.RequestAddToGroup += OnRequestAddToGroup;
            _vm.RequestBulkEditItems += OnRequestBulkEditItems;
            _vm.RequestUpdateCellphoneDetails += OnRequestUpdateCellphoneDetails;

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await _vm.ReloadAsync();
        }

        // Purely cosmetic: closes the Import/More Actions split-button popups after a menu item
        // is clicked. The bound Command still does the actual work — this just tidies the dropdown.
        private void CloseImportDropdown(object sender, RoutedEventArgs e) => ImportDropdownToggle.IsChecked = false;
        private void CloseMoreActionsDropdown(object sender, RoutedEventArgs e) => MoreActionsDropdownToggle.IsChecked = false;

        public void ApplyInitialSearch(string query) => _vm.ApplyInitialSearch(query);

        // ── Win32 owner bridge (for WinForms dialogs opened from this WPF view) ──

        private static WinForms.IWin32Window GetWin32Owner() => WinForms.Form.ActiveForm;

        private static void SetWpfOwner(Window window)
        {
            var owner = WinForms.Form.ActiveForm;
            if (owner != null) new System.Windows.Interop.WindowInteropHelper(window).Owner = owner.Handle;
        }

        // ── Tabs ──────────────────────────────────────────────────────────────

        private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != MainTabs) return;
            _vm.IsItemsTabActive = MainTabs.SelectedIndex == 0;
        }

        // ── Grid interaction: selection, sort, double-click ──────────────────

        private void ItemsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!_vm.IsItemsTabActive) return;
            _vm.EditCommand.Execute(null);
        }

        private void RowSelectCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (((CheckBox)sender).DataContext is ItemDto item)
                _vm.SetItemSelected(item.ItemId, ((CheckBox)sender).IsChecked == true);
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool newState = _vm.SelectAllState != true;
            _vm.SetAllSelected(newState);
        }

        private void ItemsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var propertyName = e.Column.SortMemberPath;
            if (string.IsNullOrEmpty(propertyName)) return;

            _vm.SortByColumnCommand.Execute(propertyName);

            foreach (var col in ItemsGrid.Columns)
                col.SortDirection = null;

            e.Column.SortDirection = _vm.SortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending;
        }

        private static readonly Dictionary<string, string> ColumnDisplayNames = new Dictionary<string, string>
        {
            ["Name"] = "Name",
            ["Category"] = "Category",
            ["ItemType"] = "Type",
            ["ModelNumber"] = "Model Number",
            ["SerialNumber"] = "Serial Number",
            ["VendorName"] = "Vendor"
        };

        private void ColumnFilterButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            var button = (Button)sender;
            var propertyName = button.Tag as string;
            if (string.IsNullOrEmpty(propertyName)) return;

            var distinctValues = _vm.GetDistinctColumnValues(propertyName);
            if (distinctValues.Count == 0) return;

            var currentFilter = _vm.GetColumnFilter(propertyName);
            var headerTitle = ColumnDisplayNames.TryGetValue(propertyName, out var display) ? display : propertyName;

            var popup = new Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs.ColumnFilterWindow(headerTitle, distinctValues, currentFilter);
            PositionPopupNearButton(popup, button);
            SetWpfOwner(popup);

            if (popup.ShowDialog() != true) return;

            if (popup.ClearFilter)
                _vm.ClearColumnFilter(propertyName);
            else
                _vm.SetColumnFilter(propertyName, popup.SelectedValues, distinctValues.Count);

            button.Content = _vm.IsColumnFiltered(propertyName) ? "🔽" : "▾";
        }

        private static void PositionPopupNearButton(Window popup, FrameworkElement btn)
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

        // ── Selected-count badge ─────────────────────────────────────────────

        private void SelectedBadge_Click(object sender, MouseButtonEventArgs e)
        {
            var summaries = _vm.GetSelectedItems().Select(i => new SelectedItemSummary
            {
                ItemId = i.ItemId,
                Name = i.Name,
                Category = i.Category,
                SerialNumber = i.SerialNumber
            });

            using (var dialog = new SelectedItemsReviewDialog(summaries))
            {
                var owner = GetWin32Owner();
                if (owner != null) dialog.ShowDialog(owner);
                else dialog.ShowDialog();

                foreach (var itemId in dialog.RemovedItemIds)
                    _vm.SetItemSelected(itemId, false);
            }
        }

        // ── Add / Edit ────────────────────────────────────────────────────────

        private void OnRequestAddItems()
        {
            if (!Yakult.Inventory.App.Security.PermissionResolver.CanPerformAction("Item.Add"))
            {
                MessageBox.Show("Access denied. You do not have permission to add items.", "Unauthorized", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var dialog = new BatchAddItemDialog();
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    _ = _vm.ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Add Items dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnRequestEditItem(List<ItemDto> selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one item to edit.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (selectedItems.Count > 1)
            {
                MessageBox.Show("Please select only one item to edit.", "Multiple Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!Yakult.Inventory.App.Security.PermissionResolver.CanPerformAction("Item.Edit"))
            {
                MessageBox.Show("Access denied. You do not have permission to edit items.", "Unauthorized", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var dialog = new EditItemDialog(selectedItems[0]);
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    _ = _vm.ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Edit Item dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Archive ───────────────────────────────────────────────────────────

        private async void OnRequestArchiveItems(List<ItemDto> selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one item to archive.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string message = selectedItems.Count == 1
                ? "Are you sure you want to archive the selected item?"
                : $"Are you sure you want to archive the {selectedItems.Count} selected items?";

            if (!ShowArchiveDialog(message, out string reason, out bool deactivate))
                return;

            try
            {
                await _vm.ArchiveItemsAsync(selectedItems, reason, deactivate);
                MessageBox.Show("Item(s) archived successfully!\n\nYou can view archived items in the Archive page.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error archiving item(s):\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ShowArchiveDialog(string message, out string reason, out bool deactivate)
        {
            reason = "No reason provided";
            deactivate = true;

            var window = new Window
            {
                Title = "Archive Item",
                Width = 460,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = Window.GetWindow(this)
            };

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock
            {
                Text = message + "\n\nThe item(s) will be moved to the archive and will no longer appear in active lists. This action can be reviewed in the Archive page.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });
            panel.Children.Add(new TextBlock { Text = "Reason for archiving:", Margin = new Thickness(0, 0, 0, 4) });
            var txtReason = new TextBox { Margin = new Thickness(0, 0, 0, 12) };
            panel.Children.Add(txtReason);
            var chkDeactivate = new CheckBox { Content = "Also mark as inactive (not available for requests)", IsChecked = true, Margin = new Thickness(0, 0, 0, 16) };
            panel.Children.Add(chkDeactivate);

            var buttonPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnArchive = new Button { Content = "Archive", Width = 90, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var btnCancel = new Button { Content = "Cancel", Width = 90, IsCancel = true };
            buttonPanel.Children.Add(btnArchive);
            buttonPanel.Children.Add(btnCancel);
            panel.Children.Add(buttonPanel);

            window.Content = panel;

            bool confirmed = false;
            btnArchive.Click += (s, e) => { confirmed = true; window.DialogResult = true; };
            btnCancel.Click += (s, e) => { window.DialogResult = false; };

            if (window.ShowDialog() == true && confirmed)
            {
                reason = string.IsNullOrWhiteSpace(txtReason.Text) ? "No reason provided" : txtReason.Text;
                deactivate = chkDeactivate.IsChecked == true;
                return true;
            }

            return false;
        }

        // ── Delete ────────────────────────────────────────────────────────────

        private async void OnRequestDeleteItems(List<ItemDto> selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one item to delete.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Yakult.Inventory.App.Security.PermissionResolver.CanPerformAction("Item.Delete"))
            {
                MessageBox.Show("Access denied. You do not have permission to delete items.", "Unauthorized", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var itemsWithDeps = new List<(ItemDto item, int sets, int requests, int inventory)>();
            var itemsWithoutDeps = new List<ItemDto>();

            foreach (var item in selectedItems)
            {
                var (hasDependencies, setItemCount, requestCount, inventoryCount) = await _itemRepo.CheckItemDependencies(item.ItemId);
                if (hasDependencies)
                    itemsWithDeps.Add((item, setItemCount, requestCount, inventoryCount));
                else
                    itemsWithoutDeps.Add(item);
            }

            if (itemsWithDeps.Count > 0)
            {
                var totalSets = itemsWithDeps.Sum(x => x.sets);
                var totalRequests = itemsWithDeps.Sum(x => x.requests);
                var totalInventory = itemsWithDeps.Sum(x => x.inventory);

                var choice = ShowDeleteChoiceDialog(selectedItems.Count, itemsWithDeps.Count, itemsWithoutDeps.Count, totalSets, totalRequests, totalInventory);

                if (choice == "INACTIVE")
                {
                    int successCount = 0;
                    var failedItems = new List<string>();

                    foreach (var item in selectedItems)
                    {
                        try
                        {
                            await _itemRepo.SetItemInactive(item.ItemId);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            failedItems.Add($"{item.Name} ({item.SerialNumber}): {ex.Message}");
                        }
                    }

                    ShowBatchResult(successCount, failedItems, "marked as INACTIVE", "Items Deactivated");
                    await _vm.ReloadAsync();
                }
                else if (choice == "FORCE_DELETE")
                {
                    var finalWarning = MessageBox.Show(
                        $"FINAL WARNING - FORCE DELETE\n\n" +
                        $"This will PERMANENTLY DELETE {selectedItems.Count} item(s):\n" +
                        $"- {itemsWithDeps.Count} with relations\n" +
                        $"- {itemsWithoutDeps.Count} without relations\n\n" +
                        $"This will also DELETE:\n" +
                        $"- {totalInventory} inventory ledger records\n" +
                        $"- {totalRequests} request records\n" +
                        $"- {totalSets} set/invoice item records\n\n" +
                        $"Stock will be restored for submitted requests/sets.\n\n" +
                        $"THIS CANNOT BE UNDONE!\n\n" +
                        $"Only proceed if this was a DATA ENTRY ERROR.\n\n" +
                        $"Click 'OK' to confirm:",
                        "FORCE DELETE CONFIRMATION",
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Stop,
                        MessageBoxResult.Cancel);

                    if (finalWarning == MessageBoxResult.OK)
                    {
                        var (successCount, failedItems) = await ForceDeleteAllAsync(selectedItems);
                        ShowBatchResult(successCount, failedItems, "permanently deleted", "Force Delete Completed");
                        await _vm.ReloadAsync();
                    }
                }
            }
            else
            {
                var result = MessageBox.Show(
                    $"PERMANENT DELETE WARNING\n\n" +
                    $"This will PERMANENTLY delete {selectedItems.Count} item(s).\n\n" +
                    $"These items have NO relations, so they can be safely deleted.\n\n" +
                    $"Items to delete:\n" +
                    string.Join("\n", selectedItems.Take(5).Select(x => $"- {x.Name} ({x.SerialNumber})")) +
                    (selectedItems.Count > 5 ? $"\n... and {selectedItems.Count - 5} more" : "") +
                    $"\n\nAre you sure?",
                    "Confirm Deletion",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (result == MessageBoxResult.Yes)
                {
                    var (successCount, failedItems) = await ForceDeleteAllAsync(selectedItems);
                    ShowBatchResult(successCount, failedItems, "permanently deleted", "Deleted Successfully");
                    await _vm.ReloadAsync();
                }
            }
        }

        private async System.Threading.Tasks.Task<(int successCount, List<string> failedItems)> ForceDeleteAllAsync(List<ItemDto> items)
        {
            int successCount = 0;
            var failedItems = new List<string>();

            foreach (var item in items)
            {
                try
                {
                    var (success, message) = await _itemRepo.ForceDeleteItemWithRelations(item.ItemId);
                    if (success) successCount++;
                    else failedItems.Add($"{item.Name} ({item.SerialNumber}): {message}");
                }
                catch (Exception ex)
                {
                    failedItems.Add($"{item.Name} ({item.SerialNumber}): {ex.Message}");
                }
            }

            return (successCount, failedItems);
        }

        private static void ShowBatchResult(int successCount, List<string> failedItems, string successVerb, string successTitle)
        {
            if (failedItems.Count == 0)
            {
                MessageBox.Show($"{successCount} item(s) {successVerb}.", successTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Completed with {successCount} success(es) and {failedItems.Count} failure(s).\n\n" +
                    $"Failed items:\n" + string.Join("\n", failedItems.Take(10)) +
                    (failedItems.Count > 10 ? $"\n... and {failedItems.Count - 10} more" : ""),
                    "Partial Success", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string ShowDeleteChoiceDialog(int totalCount, int withDepsCount, int withoutDepsCount, int totalSets, int totalRequests, int totalInventory)
        {
            var window = new Window
            {
                Title = "Items Have Relations",
                Width = 560,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = Window.GetWindow(this)
            };

            string choice = null;

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock
            {
                Text = $"WARNING: {withDepsCount} selected item(s) have existing relations\n\n" +
                       $"Selected items: {totalCount} total\n" +
                       $"- {withoutDepsCount} item(s) with NO relations\n" +
                       $"- {withDepsCount} item(s) WITH relations\n\n" +
                       $"Items with relations are referenced in:\n" +
                       $"- {totalSets} set(s)/invoice(s)\n" +
                       $"- {totalRequests} request(s)\n" +
                       $"- {totalInventory} inventory ledger record(s)\n\n" +
                       "How do you want to proceed?",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            });

            var btnInactive = new Button
            {
                Content = "Option A: Mark as Inactive (Safe)\nKeeps all data intact - Maintains audit trail - Reversible",
                Height = 56,
                Background = System.Windows.Media.Brushes.DodgerBlue,
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 0, 0, 10)
            };
            btnInactive.Click += (s, e) => { choice = "INACTIVE"; window.DialogResult = true; };

            var btnForce = new Button
            {
                Content = "Option B: Force Delete & Remove All Relations\nDeletes ALL relations - CANNOT be undone - Use only for wrong data",
                Height = 56,
                Background = System.Windows.Media.Brushes.Crimson,
                Foreground = System.Windows.Media.Brushes.White
            };
            btnForce.Click += (s, e) => { choice = "FORCE_DELETE"; window.DialogResult = true; };

            panel.Children.Add(btnInactive);
            panel.Children.Add(btnForce);

            window.Content = panel;
            window.ShowDialog();

            return choice;
        }

        // ── Get from Mobile ───────────────────────────────────────────────────

        private async void OnRequestGetFromMobile()
        {
            string claimToken = null;

            try
            {
                var (success, token, mobileItems) = await _vm.ClaimMobileSerialsAsync();
                if (!success || mobileItems.Count == 0)
                {
                    MessageBox.Show("No serials received from mobile app yet.", "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                claimToken = token;

                List<MobileSerialItem> selectedItems;
                using (var preview = new MobileSerialsPreviewDialog(mobileItems, mobileItems.Count))
                {
                    var owner = GetWin32Owner();
                    var previewResult = owner != null ? preview.ShowDialog(owner) : preview.ShowDialog();

                    if (previewResult != WinForms.DialogResult.OK)
                    {
                        await _vm.CancelMobileClaimAsync(claimToken);
                        claimToken = null;
                        return;
                    }

                    selectedItems = preview.SelectedItems ?? new List<MobileSerialItem>();
                }

                if (selectedItems.Count == 0)
                {
                    await _vm.CancelMobileClaimAsync(claimToken);
                    claimToken = null;
                    MessageBox.Show("No serials selected.", "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                await _vm.ConfirmMobileClaimAsync(claimToken);
                claimToken = null;

                selectedItems = selectedItems
                    .Where(i => i != null && !string.IsNullOrWhiteSpace(i.SerialNumber))
                    .GroupBy(i => i.SerialNumber.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                var existingDialog = BatchAddItemDialog.CurrentInstance;
                if (existingDialog != null)
                {
                    foreach (var item in selectedItems)
                        existingDialog.AddMobileItemFromMobile(item.SerialNumber, item.CellPhoneNumber, item.IMEI1, item.IMEI2);
                    existingDialog.Activate();
                }
                else
                {
                    var dialog = new BatchAddItemDialog();
                    foreach (var item in selectedItems)
                        dialog.AddMobileItemFromMobile(item.SerialNumber, item.CellPhoneNumber, item.IMEI1, item.IMEI2);

                    if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                        await _vm.ReloadAsync();
                }
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(claimToken))
                    await _vm.CancelMobileClaimAsync(claimToken);

                MessageBox.Show($"Error retrieving serials from mobile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Import / Template ─────────────────────────────────────────────────

        private async void OnRequestImport()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Import Items (Excel/CSV)",
                    Filter = "Excel/CSV Files (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv|All Files (*.*)|*.*"
                };

                if (dialog.ShowDialog() != true) return;

                var filePath = dialog.FileName;
                var extension = Path.GetExtension(filePath)?.ToLowerInvariant();

                DataTable table = extension == ".csv"
                    ? _vm.LoadCsvToDataTable(filePath)
                    : _vm.LoadExcelToDataTable(filePath);

                if (table == null || table.Rows.Count == 0)
                {
                    MessageBox.Show("No data rows found in the selected file.", "Import Items", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var (successCount, errors) = await _vm.ImportItemsFromTableAsync(table);
                ShowImportSummary(successCount, errors);
                await _vm.ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import Excel file.\n\nDetails: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void ShowImportSummary(int successCount, List<string> errors)
        {
            var message = new StringBuilder();
            message.AppendLine($"Successfully imported {successCount} item(s).");

            if (errors != null && errors.Count > 0)
            {
                message.AppendLine($"Skipped {errors.Count} row(s) due to errors.");

                int maxToShow = Math.Min(10, errors.Count);
                for (int i = 0; i < maxToShow; i++)
                    message.AppendLine(errors[i]);

                if (errors.Count > maxToShow)
                    message.AppendLine($"...and {errors.Count - maxToShow} more error(s).");

                MessageBox.Show(message.ToString(), "Import Summary", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(message.ToString(), "Import Summary", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OnRequestDownloadTemplate()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Save Items Excel Template",
                    Filter = "Excel CSV (*.csv)|*.csv|All Files (*.*)|*.*",
                    FileName = "ItemsTemplate.csv",
                    DefaultExt = "csv"
                };

                if (dialog.ShowDialog() != true) return;

                File.WriteAllText(dialog.FileName, _vm.BuildTemplateCsvContent());

                MessageBox.Show("Template saved successfully. You can open and edit it in Excel.", "Template Created", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── PDF export ────────────────────────────────────────────────────────

        private void OnRequestExportPdf(List<ItemDto> selectedItems)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Export Selected Items to PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    DefaultExt = "pdf",
                    FileName = $"SelectedItems_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
                };

                if (dialog.ShowDialog() != true) return;

                ItemPdfGenerator.GenerateSelectedItemsPdf(selectedItems, dialog.FileName, "Selected Inventory Items");

                var result = MessageBox.Show("PDF generated successfully.\n\nOpen it now?", "Export Complete", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                    ItemPdfGenerator.TryOpen(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Bulk Add Request ──────────────────────────────────────────────────

        private void OnRequestBulkAddRequest(List<int> selectedIds)
        {
            try
            {
                var dialog = new Yakult.Inventory.App.Pages.Request.BatchAddRequestDialog(selectedIds);
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    _vm.ClearSelection();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open Bulk Add Request: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Add to Sub-Type Group ─────────────────────────────────────────────

        private void OnRequestAddToGroup(List<int> selectedIds, string subType)
        {
            if (selectedIds == null || selectedIds.Count == 0) return;

            try
            {
                var selectedItems = _vm.GetSelectedItems();
                var owner = GetWin32Owner();

                var repository = new Yakult.Inventory.App.Repositories.InvoicePreparationRepository();

                // Checked before the dialog opens: there is no point collecting a reference code
                // and dates for a group that cannot be created.
                var eligibility = repository.CheckSubTypeEligibility(selectedIds, subType);
                if (!eligibility.AllEligible)
                {
                    ShowSubTypeNotSetMessage(eligibility, subType);
                    return;
                }

                using (var dialog = new AssignSubTypeGroupDialog(subType, selectedItems.Count))
                {
                    var result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
                    if (result != WinForms.DialogResult.OK) return;

                    var items = selectedItems
                        .Select(i => (i.ItemId, Quantity: i.StockOnHand > 0 ? (decimal)i.StockOnHand : 1m, UnitPrice: i.Amount))
                        .ToList();

                    repository.CreateGroup(subType, dialog.ReferenceCode, dialog.BeginDate, dialog.EndDate, items,
                        Yakult.Inventory.App.Session.AppSession.CurrentUserId);

                    _vm.ClearSelection();
                    MessageBox.Show($"{items.Count} item(s) added to the {subType} group.", "Sub-Type Group",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create Sub-Type Group: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Explains why the selection cannot form this group, and what to do about it. Items with
        /// no Sub-Type are listed separately from items carrying a different one, because the fix
        /// differs: the first needs a Sub-Type set, the second needs the right group (or its
        /// Sub-Type corrected).
        /// </summary>
        private void ShowSubTypeNotSetMessage(
            Yakult.Inventory.App.Repositories.InvoicePreparationRepository.SubTypeEligibility eligibility,
            string subType)
        {
            const int MaxNamesShown = 10;
            var message = new System.Text.StringBuilder();

            if (eligibility.Untagged.Count > 0)
            {
                message.AppendLine($"{eligibility.Untagged.Count} selected item(s) do not have a Sub-Type:");
                foreach (var name in eligibility.Untagged.Take(MaxNamesShown))
                    message.AppendLine("   • " + name);
                if (eligibility.Untagged.Count > MaxNamesShown)
                    message.AppendLine($"   … and {eligibility.Untagged.Count - MaxNamesShown} more");
                message.AppendLine();
            }

            if (eligibility.Mismatched.Count > 0)
            {
                message.AppendLine($"{eligibility.Mismatched.Count} selected item(s) have a different Sub-Type:");
                foreach (var name in eligibility.Mismatched.Take(MaxNamesShown))
                    message.AppendLine("   • " + name);
                if (eligibility.Mismatched.Count > MaxNamesShown)
                    message.AppendLine($"   … and {eligibility.Mismatched.Count - MaxNamesShown} more");
                message.AppendLine();
            }

            message.AppendLine($"Set their Sub-Type to '{subType}' before adding them to a {subType} group.");
            message.AppendLine();
            message.AppendLine("Use Edit for one item, or More Actions ▸ Bulk Edit Item to set several at once.");

            MessageBox.Show(message.ToString(), $"Sub-Type required for a {subType} group",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // ── Bulk Edit Items ───────────────────────────────────────────────────

        private async void OnRequestBulkEditItems(System.Collections.Generic.List<int> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0) return;

            try
            {
                bool saved;
                using (var dlg = new Yakult.Inventory.App.Pages.Item.BulkEditItemsDialog(itemIds))
                {
                    var owner = GetWin32Owner();
                    if (owner != null) dlg.ShowDialog(owner);
                    else dlg.ShowDialog();
                    saved = dlg.SavedChanges;
                }

                if (saved) await _vm.ReloadAsync();
            }
            catch (System.Exception ex)
            {
                Yakult.Inventory.App.Core.Logger.LogError("Bulk Edit Items failed", ex);
                WinForms.MessageBox.Show("Could not open Bulk Edit Items." + Environment.NewLine + Environment.NewLine + ex.Message,
                    "Bulk Edit Items", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        // ── Update Cellphone Details ──────────────────────────────────────────

        private async void OnRequestUpdateCellphoneDetails()
        {
            try
            {
                using (var dlg = new UpdateCellphoneDetailsDialog())
                {
                    var owner = GetWin32Owner();
                    if (owner != null) dlg.ShowDialog(owner);
                    else dlg.ShowDialog();
                }

                await _vm.ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open Update Cellphone Details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Row background for the Category Stock Summary grid: red when out of stock,
    // yellow when running low — ported from ViewItemsPage.UpdateCategorySummaryPagination().
    internal sealed class StockLevelToBrushConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int stock)
            {
                if (stock <= 0) return System.Windows.Media.Brushes.LightCoral;
                if (stock < 5) return System.Windows.Media.Brushes.LightYellow;
            }
            return System.Windows.Media.Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}
