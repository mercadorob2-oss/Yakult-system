using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Dialogs
{
    public partial class TransferCartridgesWpfDialog : Window
    {
        public bool AnyTransferred { get; private set; }

        private readonly VendorCartridgeBatchDto _sourceBatch;
        private readonly CartridgeRefillService  _refillService = new CartridgeRefillService();

        private readonly List<int>               _targetBatchIds = new List<int>();
        private List<ModelTransferItem>          _models         = new List<ModelTransferItem>();

        public TransferCartridgesWpfDialog(VendorCartridgeBatchDto sourceBatch)
        {
            _sourceBatch = sourceBatch ?? throw new ArgumentNullException(nameof(sourceBatch));
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            TxtSubtitle.Text = $"Batch #{_sourceBatch.BatchId}  ·  {_sourceBatch.VendorName}";
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            TxtModelsLoading.Visibility = Visibility.Visible;
            ModelsBorder.Visibility     = Visibility.Collapsed;
            TxtNoRows.Visibility        = Visibility.Collapsed;

            try
            {
                // ── Target batches ──────────────────────────────────────────────
                _targetBatchIds.Clear();
                CmbTarget.Items.Clear();

                var activeBatches = await _refillService.GetAllActiveBatchesAsync();
                foreach (var b in activeBatches.Where(b => b.BatchId != _sourceBatch.BatchId))
                {
                    _targetBatchIds.Add(b.BatchId);
                    CmbTarget.Items.Add($"Batch #{b.BatchId}  —  {b.VendorName}");
                }

                // ── Audit trail rows grouped by model ──────────────────────────
                var rows = await _refillService.GetBatchAuditTrailAsync(_sourceBatch.BatchId);

                _models = rows
                    .GroupBy(r => new { r.CartridgeModelId, r.CartridgeModel })
                    .Select(g => new ModelTransferItem
                    {
                        CartridgeModelId = g.Key.CartridgeModelId,
                        ModelName        = g.Key.CartridgeModel ?? "[Unknown Model]",
                        AvailableQty     = g.Sum(r => r.ReturnedQty),
                        RowCount         = g.Count(),
                        Rows             = g.ToList(),
                        TransferQty      = g.Sum(r => r.ReturnedQty)
                    })
                    .OrderBy(m => m.ModelName)
                    .ToList();

                foreach (var m in _models)
                    m.PropertyChanged += (s, e) => UpdateFooter();

                TxtModelsLoading.Visibility = Visibility.Collapsed;

                if (_models.Count == 0)
                {
                    TxtNoRows.Visibility = Visibility.Visible;
                }
                else
                {
                    ModelsList.ItemsSource = _models;
                    ModelsBorder.Visibility = Visibility.Visible;
                }

                UpdateFooter();
            }
            catch (Exception ex)
            {
                TxtModelsLoading.Text = "Failed to load cartridge data.";
                Logger.LogError("TransferCartridgesWpfDialog.LoadAsync failed", ex);
            }
        }

        private void BtnDecrement_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn &&
                btn.DataContext is ModelTransferItem item)
            {
                item.TransferQty--;
                UpdateFooter();
            }
        }

        private void BtnIncrement_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn &&
                btn.DataContext is ModelTransferItem item)
            {
                item.TransferQty++;
                UpdateFooter();
            }
        }

        private void CmbTarget_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateFooter();
        }

        private void UpdateFooter()
        {
            var selected = _models.Where(m => m.IsSelected && m.TransferQty > 0).ToList();
            int totalQty = selected.Sum(m => m.TransferQty);

            if (selected.Count == 0)
                TxtSummary.Text = "No models selected";
            else
                TxtSummary.Text = $"{selected.Count} model(s)  ·  {totalQty} unit(s) to transfer";

            bool hasTarget = CmbTarget.SelectedIndex >= 0 &&
                             CmbTarget.SelectedIndex < _targetBatchIds.Count;

            BtnTransfer.IsEnabled = selected.Count > 0 && hasTarget;
        }

        private async void BtnTransfer_Click(object sender, RoutedEventArgs e)
        {
            if (CmbTarget.SelectedIndex < 0 || CmbTarget.SelectedIndex >= _targetBatchIds.Count)
            {
                MessageBox.Show("Please select a target batch.", "Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedModels = _models.Where(m => m.IsSelected && m.TransferQty > 0).ToList();
            if (selectedModels.Count == 0) return;

            int targetBatchId = _targetBatchIds[CmbTarget.SelectedIndex];
            int userId        = AppSession.CurrentUserId;

            // Build confirmation summary
            var summary = selectedModels
                .Select(m => $"  • {m.ModelName}: {m.TransferQty} unit(s)")
                .ToList();

            var confirm = MessageBox.Show(
                $"Transfer to Batch #{targetBatchId}?\n\n" +
                string.Join("\n", summary) +
                $"\n\nTotal: {selectedModels.Sum(m => m.TransferQty)} unit(s).\n\n" +
                "This action cannot be undone.",
                "Confirm Transfer",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            BtnTransfer.IsEnabled = false;

            int totalTransferred = 0;
            var errors           = new List<string>();

            foreach (var model in selectedModels)
            {
                int remaining = model.TransferQty;

                foreach (var row in model.Rows)
                {
                    if (remaining <= 0) break;

                    try
                    {
                        int qty = await _refillService.TransferToBatchAsync(
                            _sourceBatch.BatchId,
                            targetBatchId,
                            row.RequestId,
                            row.CartridgeModelId,
                            row.EmptyCartridgeId,
                            userId);

                        totalTransferred += qty;
                        remaining        -= qty;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{model.ModelName} (EmptyCartridge #{row.EmptyCartridgeId}): {ex.Message}");
                        Logger.LogError(
                            $"TransferCartridgesWpfDialog: transfer failed for EmptyCartridgeId={row.EmptyCartridgeId}", ex);
                    }
                }
            }

            AnyTransferred = totalTransferred > 0;

            if (errors.Count == 0)
            {
                MessageBox.Show(
                    $"Successfully transferred {totalTransferred} unit(s) to Batch #{targetBatchId}.",
                    "Transfer Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Transferred {totalTransferred} unit(s) with {errors.Count} failure(s):\n\n" +
                    string.Join("\n", errors),
                    "Partial Transfer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Reload to reflect remaining rows
            await LoadAsync();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = AnyTransferred;
        }

        // ── Inner types ─────────────────────────────────────────────────────────

        private class ModelTransferItem : INotifyPropertyChanged
        {
            public int    CartridgeModelId { get; set; }
            public string ModelName        { get; set; }
            public int    AvailableQty     { get; set; }
            public int    RowCount         { get; set; }
            public List<VendorBatchAuditTrailDto> Rows { get; set; } = new List<VendorBatchAuditTrailDto>();

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(QtyDecrEnabled));
                    OnPropertyChanged(nameof(QtyIncrEnabled));
                }
            }

            private int _transferQty;
            public int TransferQty
            {
                get => _transferQty;
                set
                {
                    _transferQty = Math.Max(0, Math.Min(AvailableQty, value));
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(QtyDecrEnabled));
                    OnPropertyChanged(nameof(QtyIncrEnabled));
                }
            }

            public bool QtyDecrEnabled => IsSelected && TransferQty > 0;
            public bool QtyIncrEnabled => IsSelected && TransferQty < AvailableQty;

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
