using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Wpf.OwnershipHistory;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Wpf.OwnershipHistory.Views
{
    public partial class OwnershipHistoryPageView : UserControl
    {
        private readonly AuditRepository _auditRepository = new AuditRepository();
        private readonly ItemAuditTrailRepository _itemAuditTrailRepository = new ItemAuditTrailRepository();
        private readonly ObservableCollection<HistoryRow> _rows = new ObservableCollection<HistoryRow>();
        private readonly ObservableCollection<TransferCycleStep> _transferCycle = new ObservableCollection<TransferCycleStep>();
        private readonly bool _autoLoad;
        private bool _loaded;

        public OwnershipHistoryPageView(bool autoLoad = true)
        {
            InitializeComponent();
            GridHistory.ItemsSource = _rows;
            ListTransferCycle.ItemsSource = _transferCycle;

            _autoLoad = autoLoad;
            Loaded += OwnershipHistoryPageView_Loaded;
        }

        private async void OwnershipHistoryPageView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_loaded || !_autoLoad)
                return;

            _loaded = true;
            await SafeLoadAsync();
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private async System.Threading.Tasks.Task SafeLoadAsync()
        {
            try
            {
                await LoadAsync();
            }
            catch (Exception ex)
            {
                WinForms.MessageBox.Show(GetOwner(), $"Failed to load ownership/site history: {ex.Message}",
                    "Error", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        private string SelectedEntityType()
        {
            var item = CmbEntityType.SelectedItem as ComboBoxItem;
            var text = item?.Content?.ToString();
            return string.IsNullOrEmpty(text) || text == "All" ? null : text;
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            _rows.Clear();
            _transferCycle.Clear();

            int? entityId = null;
            var entityIdText = TxtEntityId?.Text;
            if (!string.IsNullOrWhiteSpace(entityIdText) && int.TryParse(entityIdText.Trim(), out var parsedId))
                entityId = parsedId;

            var entityType = SelectedEntityType();

            List<AuditEntry> entries;
            if (entityType == null)
            {
                var setEntries = await _auditRepository.GetHistoryAsync("Set", entityId);
                var invoiceEntries = await _auditRepository.GetHistoryAsync("Invoice", entityId);
                entries = setEntries.Concat(invoiceEntries).OrderByDescending(x => x.Timestamp).ToList();
            }
            else
            {
                entries = await _auditRepository.GetHistoryAsync(entityType, entityId);
            }

            foreach (var entry in entries)
                _rows.Add(HistoryRow.From(entry));
        }

        private async void GridHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _transferCycle.Clear();

            if (!(GridHistory.SelectedItem is HistoryRow row) || row.EntityType != "Set" || row.EntityId == null)
                return;

            try
            {
                foreach (var step in await BuildTransferCycleStepsAsync(row.EntityId.Value))
                    _transferCycle.Add(step);
            }
            catch (Exception ex)
            {
                WinForms.MessageBox.Show(GetOwner(), $"Failed to load transfer cycle: {ex.Message}",
                    "Error", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        private async System.Threading.Tasks.Task<List<TransferCycleStep>> BuildTransferCycleStepsAsync(int setId)
        {
            var steps = new List<TransferCycleStep>();
            var itemEntries = await _itemAuditTrailRepository.GetAuditTrailAsync(null, null, "Set", setId);

            // LogTransferAuditAsync writes one ItemAuditTrail row per Set item for the same
            // logical transfer step (same Action/Notes), so group them back into one step
            // instead of showing an identical-looking row per item.
            var groups = itemEntries
                .GroupBy(x => new { x.Action, x.Notes })
                .OrderBy(g => g.Min(x => x.ActionTime));

            foreach (var group in groups)
            {
                var first = group.First();
                var serials = group.Select(x => x.SerialNumber).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                var itemLabel = serials.Count > 0 ? string.Join(", ", serials) : $"{group.Count()} item(s)";

                steps.Add(new TransferCycleStep
                {
                    Timestamp = first.ActionTime,
                    Header = $"{first.ActionTime:yyyy-MM-dd HH:mm} — {first.Action}",
                    Detail = $"{first.SetCode} / {itemLabel}: {first.Notes}",
                    AccentBrush = AuditFormatting.AccentBrushForAction(first.Action)
                });
            }

            return steps;
        }

        private async void BtnSearch_Click(object sender, System.Windows.RoutedEventArgs e) => await SafeLoadAsync();

        private async void BtnRefresh_Click(object sender, System.Windows.RoutedEventArgs e) => await SafeLoadAsync();

        private async void CmbEntityType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loaded)
                await SafeLoadAsync();
        }

        private async void TxtEntityId_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await SafeLoadAsync();
        }

        private void BtnPreviewSelected_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!(GridHistory.SelectedItem is HistoryRow row))
            {
                WinForms.MessageBox.Show(GetOwner(), "Select a row in the history grid first.",
                    "No Selection", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                return;
            }

            OpenEntityDetail(row);
        }

        private void OpenEntityDetail(HistoryRow row)
        {
            if (row.EntityId == null)
                return;

            try
            {
                if (row.EntityType == "Set")
                {
                    using (var detail = new Yakult.Inventory.App.Pages.Set.ViewSetDetailPage(row.EntityId.Value))
                    {
                        detail.ShowDialog(GetOwner());
                    }
                }
                else if (row.EntityType == "Invoice")
                {
                    using (var detail = new Yakult.Inventory.App.Pages.Invoice.ViewInvoiceDetailPage(row.EntityId.Value))
                    {
                        detail.ShowDialog(GetOwner());
                    }
                }
            }
            catch (Exception ex)
            {
                WinForms.MessageBox.Show(GetOwner(), $"Failed to open {row.EntityType} #{row.EntityId}: {ex.Message}",
                    "Error", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        private async void BtnFullView_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is HistoryRow row) || row.EntityId == null)
                return;

            try
            {
                var auditEntries = await _auditRepository.GetHistoryAsync(row.EntityType, row.EntityId);
                var transferSteps = row.EntityType == "Set"
                    ? await BuildTransferCycleStepsAsync(row.EntityId.Value)
                    : new List<TransferCycleStep>();

                var window = new OwnershipTimelineWindow(row.EntityType, row.EntityId.Value, auditEntries, transferSteps);
                var owner = GetOwner();
                if (owner != null)
                    new System.Windows.Interop.WindowInteropHelper(window).Owner = owner.Handle;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                WinForms.MessageBox.Show(GetOwner(), $"Failed to load full history: {ex.Message}",
                    "Error", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        private class HistoryRow
        {
            public int Id { get; set; }
            public DateTime Timestamp { get; set; }
            public string EntityType { get; set; }
            public int? EntityId { get; set; }
            public string EntityLabel { get; set; }
            public string Action { get; set; }
            public string UserName { get; set; }
            public string Headline { get; set; }
            public Brush AccentBrush { get; set; }

            public static HistoryRow From(AuditEntry entry)
            {
                return new HistoryRow
                {
                    Id = entry.Id,
                    Timestamp = entry.Timestamp,
                    EntityType = entry.EntityType,
                    EntityId = entry.EntityId,
                    EntityLabel = $"{entry.EntityType} #{entry.EntityId}",
                    Action = entry.Action,
                    UserName = entry.UserName,
                    Headline = AuditFormatting.BuildHeadline(entry),
                    AccentBrush = AuditFormatting.AccentBrushForAction(entry.Action)
                };
            }
        }
    }
}
