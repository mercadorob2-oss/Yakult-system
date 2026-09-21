using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.Archive.ViewModels;

namespace Yakult.Inventory.App.WPF.Archive.Views
{
    public partial class ArchivePageView : UserControl
    {
        private readonly ArchivePageViewModel _vm;
        private CheckBox _selectAllHeaderChk;

        public ArchivePageView()
        {
            InitializeComponent();

            _vm = new ArchivePageViewModel(DatabaseConfig.ConnectionString);
            DataContext = _vm;

            _vm.RequestOpenDetails += OnOpenDetailsRequested;
            _vm.RequestExportCsv   += OnExportCsvRequested;
            _vm.PagedRecords.CollectionChanged += (s, e) => SyncSelectAllHeader();

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await _vm.LoadDataAsync();
        }

        private void OnOpenDetailsRequested(ArchiveRecordDto record)
        {
            if (record == null) return;

            var window = new ArchiveDetailsWindow(record.ArchiveId, record.EntityType, record.EntityId);

            // Attempt to set WPF owner — may be null when hosted in WinForms ElementHost
            var owner = Window.GetWindow(this);
            if (owner != null) window.Owner = owner;

            var result = window.ShowDialog();
            if (result == true)
            {
                // Record was restored — reload the grid
                _ = _vm.LoadDataAsync();
            }
        }

        private void OnExportCsvRequested()
        {
            var records = _vm.GetFilteredRecords();
            if (records == null || records.Count == 0)
            {
                MessageBox.Show("No records to export.", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter   = "CSV Files (*.csv)|*.csv",
                FileName = $"Archive_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                using (var writer = new StreamWriter(dlg.FileName))
                {
                    writer.WriteLine("ArchiveId,EntityType,EntityId,RecordName,ArchivedAt,ArchivedBy,ArchiveReason");
                    foreach (var r in records)
                    {
                        writer.WriteLine(
                            $"{r.ArchiveId}," +
                            $"\"{Escape(r.EntityType)}\"," +
                            $"{r.EntityId}," +
                            $"\"{Escape(r.RecordName)}\"," +
                            $"\"{r.ArchivedAt:yyyy-MM-dd HH:mm:ss}\"," +
                            $"\"{Escape(r.ArchivedBy)}\"," +
                            $"\"{Escape(r.ArchiveReason)}\"");
                    }
                }

                MessageBox.Show($"Exported {records.Count} records to:\n{dlg.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ApplyInitialSearch(string query)
        {
            if (_vm != null && !string.IsNullOrWhiteSpace(query))
                _vm.SearchText = query;
        }

        // ── Bulk checkbox selection (current page only, per "select all") ──────────────
        private void ChkSelectAllHeader_Loaded(object sender, RoutedEventArgs e)
        {
            _selectAllHeaderChk = sender as CheckBox;
            SyncSelectAllHeader();
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is CheckBox chk)) return;
            _vm.SetPageSelected(chk.IsChecked == true);
        }

        private void RowSelectCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (((CheckBox)sender).DataContext is ArchiveRecordDto row)
                _vm.SetRowSelected(row.ArchiveId, ((CheckBox)sender).IsChecked == true);
            SyncSelectAllHeader();
        }

        private void SyncSelectAllHeader()
        {
            if (_selectAllHeaderChk == null) return;
            _selectAllHeaderChk.IsChecked = _vm.PagedRecords.Count > 0
                && _vm.PagedRecords.All(r => r.IsSelected);
        }

        // ── Bulk Restore / Permanently Delete ───────────────────────────────────────────
        private async void BtnRestoreSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!Yakult.Inventory.App.Security.PermissionResolver.CanPerformAction("Archive.Restore"))
            {
                MessageBox.Show("You do not have permission to restore archived records.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selected = _vm.GetCheckedRecords();
            if (selected.Count == 0)
            {
                MessageBox.Show("Select one or more records to restore first.", "Nothing Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Restore {selected.Count} selected record(s)?\n\nThey will be reactivated and reappear in their normal active lists.",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            var errors = new List<BulkActionResultRow>();
            int succeeded = 0;
            foreach (var record in selected)
            {
                try
                {
                    await _vm.RestoreAsync(record, AppSession.CurrentUserName ?? "System");
                    succeeded++;
                }
                catch (Exception ex)
                {
                    errors.Add(new BulkActionResultRow { RecordName = record.RecordName, Reason = ex.Message });
                    Logger.LogError("ArchivePageView.BtnRestoreSelected_Click failed", ex);
                }
            }

            await _vm.LoadDataAsync();

            if (errors.Count > 0)
                ShowResults("Restore Results", "\uE777", succeeded, "restored",
                    blockedIntro: null, blockedRows: Array.Empty<BulkActionResultRow>(), otherErrorRows: errors);
        }

        private async void BtnPermanentDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!Yakult.Inventory.App.Security.PermissionResolver.CanPerformAction("Archive.Delete"))
            {
                MessageBox.Show("You do not have permission to permanently delete archived records.", "Access Denied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selected = _vm.GetCheckedRecords();
            if (selected.Count == 0)
            {
                MessageBox.Show("Select one or more records to permanently delete first.", "Nothing Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Permanently delete {selected.Count} selected record(s)?\n\n" +
                "This destroys the underlying record entirely (not just the archive entry) and CANNOT be undone.",
                "Confirm Permanent Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            var stillReferenced = new List<BulkActionResultRow>();
            var otherErrors      = new List<BulkActionResultRow>();
            int succeeded = 0;
            foreach (var record in selected)
            {
                try
                {
                    await _vm.PermanentDeleteAsync(record);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    var reference = DescribeReferenceBlock(ex.Message);
                    if (reference != null)
                    {
                        string detail = reference;
                        if (string.Equals(record.EntityType, "Item", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var setCodes = await Yakult.Inventory.App.WPF.Archive.Services.ArchivePermanentDeleteService
                                    .GetBlockingSetCodesAsync(DatabaseConfig.ConnectionString, record.EntityId);
                                if (setCodes.Count > 0)
                                    detail = $"Set{(setCodes.Count > 1 ? "s" : "")} {string.Join(", ", setCodes)}";
                            }
                            catch { /* fall back to the generic phrase already in `detail` */ }
                        }
                        stillReferenced.Add(new BulkActionResultRow
                        {
                            RecordName = record.RecordName,
                            Reason     = $"Still used in {detail}",
                            IsWarning  = true
                        });
                    }
                    else
                    {
                        otherErrors.Add(new BulkActionResultRow { RecordName = record.RecordName, Reason = ex.Message });
                    }
                    Logger.LogError("ArchivePageView.BtnPermanentDeleteSelected_Click failed", ex);
                }
            }

            await _vm.LoadDataAsync();

            if (stillReferenced.Count > 0 || otherErrors.Count > 0)
                ShowResults("Permanent Delete Results", "\uE74D", succeeded, "permanently deleted",
                    blockedIntro: "These records can't be permanently deleted because they're still included on a Request or a Set. Remove them from that Request/Set first, or restore and archive it separately:",
                    blockedRows: stillReferenced, otherErrorRows: otherErrors);
        }

        /// <summary>Shows a scrollable, per-record breakdown of a bulk operation's outcome.</summary>
        private void ShowResults(string title, string headerIcon, int succeededCount, string succeededNoun,
            string blockedIntro, IEnumerable<BulkActionResultRow> blockedRows, IEnumerable<BulkActionResultRow> otherErrorRows)
        {
            var window = new BulkActionResultWindow(title, headerIcon, succeededCount, succeededNoun,
                blockedIntro, blockedRows, otherErrorRows);
            var owner = Window.GetWindow(this);
            if (owner != null) window.Owner = owner;
            window.ShowDialog();
        }

        /// <summary>
        /// Translates a SQL "conflicted with the REFERENCE constraint" failure message into a
        /// short human phrase (e.g. "a Set" / "a Request"). Returns null if the message doesn't
        /// match that shape, so the caller can fall back to showing the raw error.
        ///
        /// Delegates to the shared <see cref="Yakult.Inventory.App.Helpers.ForeignKeyErrorHelper"/>
        /// so the table-name mapping stays consistent with every other delete flow in the app.
        /// </summary>
        private static string DescribeReferenceBlock(string sqlErrorMessage)
        {
            if (string.IsNullOrEmpty(sqlErrorMessage) || !sqlErrorMessage.Contains("REFERENCE constraint"))
                return null;

            string table = Yakult.Inventory.App.Helpers.ForeignKeyErrorHelper.ExtractReferencingTable(sqlErrorMessage);
            return Yakult.Inventory.App.Helpers.ForeignKeyErrorHelper.DescribeTable(table);
        }

        private static string Escape(string value)
            => (value ?? string.Empty).Replace("\"", "\"\"");
    }
}
