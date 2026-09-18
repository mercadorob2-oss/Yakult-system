using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Dialogs
{
    /// <summary>
    /// Two-step WPF wizard for assigning unassigned non-refillable / damaged empty cartridges
    /// to a DISPOSE or SELL outbound batch.  Preserves all business logic from
    /// OutboundBatchAssignmentDialog (WinForms).
    /// </summary>
    public partial class OutboundBatchAssignmentWpfDialog : Window
    {
        private readonly CartridgeRefillService _service;
        private readonly bool _nonRefillableOnly;
        private readonly bool _damagedOnly;
        private int _currentStep = 1;

        // ── Step 1 state ──────────────────────────────────────────────────────
        private List<UnassignedReturnDto> _allReturns = new List<UnassignedReturnDto>();
        private readonly ObservableCollection<SelectableOutboundReturn> _filteredReturns
            = new ObservableCollection<SelectableOutboundReturn>();
        private readonly List<int>    _modelFilterIds   = new List<int>();
        private readonly List<string> _modelFilterNames = new List<string>();

        // ── Step 2 state ──────────────────────────────────────────────────────
        private readonly ObservableCollection<OutboundModelGroupItem> _modelGroups
            = new ObservableCollection<OutboundModelGroupItem>();
        private readonly List<int>    _vendorIds         = new List<int>();
        private readonly List<string> _vendorNames       = new List<string>();
        private readonly List<int>    _activeBatchIds    = new List<int>();
        private readonly List<string> _activeBatchLabels = new List<string>();

        // ─────────────────────────────────────────────────────────────────────
        public OutboundBatchAssignmentWpfDialog(bool nonRefillableOnly = false, bool damagedOnly = false)
        {
            _nonRefillableOnly = nonRefillableOnly;
            _damagedOnly       = damagedOnly;
            _service           = new CartridgeRefillService();

            InitializeComponent();

            ReturnsGrid.ItemsSource = _filteredReturns;
            ModelCards.ItemsSource  = _modelGroups;

            Loaded += async (s, e) => await LoadStep1Async();
        }

        // ═════════════════════════════════════════════════════════════════════
        // STEP 1 — DATA LOADING
        // ═════════════════════════════════════════════════════════════════════

        private async Task LoadStep1Async()
        {
            BtnNext.IsEnabled = false;
            try
            {
                _allReturns = await _service.GetUnassignedOutboundCartridgesAsync(_nonRefillableOnly, _damagedOnly);

                _modelFilterIds.Clear();
                _modelFilterNames.Clear();
                CmbModelFilter.Items.Clear();

                _modelFilterIds.Add(0);
                _modelFilterNames.Add("(All Models)");
                CmbModelFilter.Items.Add("(All Models)");

                foreach (var g in _allReturns.GroupBy(r => r.CartridgeModelId).OrderBy(g => g.First().CartridgeModel))
                {
                    _modelFilterIds.Add(g.Key);
                    _modelFilterNames.Add(g.First().CartridgeModel);
                    CmbModelFilter.Items.Add(g.First().CartridgeModel);
                }

                CmbModelFilter.SelectedIndex = 0;
                BindFilteredReturns();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading cartridges:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("OutboundBatchAssignmentWpfDialog.LoadStep1Async failed", ex);
            }
            finally
            {
                BtnNext.IsEnabled = true;
            }
        }

        private void BindFilteredReturns()
        {
            foreach (var r in _filteredReturns)
                r.PropertyChanged -= OnReturnSelectionChanged;

            _filteredReturns.Clear();

            int filterModelId = CmbModelFilter.SelectedIndex >= 0
                ? _modelFilterIds[CmbModelFilter.SelectedIndex]
                : 0;

            var source = filterModelId == 0
                ? _allReturns
                : _allReturns.Where(r => r.CartridgeModelId == filterModelId).ToList();

            foreach (var item in source)
            {
                var sr = new SelectableOutboundReturn(item);
                sr.PropertyChanged += OnReturnSelectionChanged;
                _filteredReturns.Add(sr);
            }

            UpdateSelectionStatus();
            UpdateSelectAllCheckbox();
        }

        private void OnReturnSelectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectableOutboundReturn.IsSelected))
            {
                UpdateSelectionStatus();
                UpdateSelectAllCheckbox();
            }
        }

        private void UpdateSelectionStatus()
        {
            int rows  = _filteredReturns.Count(r => r.IsSelected);
            int units = _filteredReturns.Where(r => r.IsSelected).Sum(r => r.Quantity);

            if (rows == 0)
            {
                TxtSelectionStatus.Text       = "0 rows selected  •  0 units";
                TxtSelectionStatus.Foreground = Brushes.Gray;
            }
            else
            {
                TxtSelectionStatus.Text = $"{rows} row(s) selected  •  {units} unit(s)";
                TxtSelectionStatus.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#E07820"));
            }
        }

        private void UpdateSelectAllCheckbox()
        {
            if (_filteredReturns.Count == 0) { ChkSelectAll.IsChecked = false; return; }
            int checked_ = _filteredReturns.Count(r => r.IsSelected);
            ChkSelectAll.IsChecked = checked_ == _filteredReturns.Count ? true :
                                     checked_ == 0                      ? false :
                                     (bool?)null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // STEP 2 — DATA LOADING
        // ═════════════════════════════════════════════════════════════════════

        private async Task LoadStep2Async()
        {
            _modelGroups.Clear();
            _vendorIds.Clear();
            _vendorNames.Clear();
            _activeBatchIds.Clear();
            _activeBatchLabels.Clear();
            CmbVendor.Items.Clear();
            CmbExistingBatch.Items.Clear();
            RadNew.IsChecked = true;

            // Build model groups from all selected rows across the full list,
            // not just the currently filtered view — same approach as WinForms dialog.
            var allSelected = _filteredReturns
                .Where(r => r.IsSelected)
                .Select(r => r.Source)
                .ToList();

            foreach (var g in allSelected.GroupBy(r => r.CartridgeModelId).OrderBy(g => g.First().CartridgeModel))
            {
                _modelGroups.Add(new OutboundModelGroupItem
                {
                    CartridgeModelId  = g.Key,
                    CartridgeModel    = g.First().CartridgeModel,
                    EmptyCartridgeIds = g.Select(r => r.EmptyCartridgeId).ToList(),
                    TotalQty          = g.Sum(r => r.Quantity)
                });
            }

            int totalUnits = _modelGroups.Sum(m => m.TotalQty);
            TxtStep2Summary.Text = $"{_modelGroups.Count} model(s)  •  {totalUnits} unit(s) selected";

            // Show damaged warning if selection has damaged rows
            bool hasDamaged = allSelected.Any(dto => dto.ConditionStatus == "DAMAGED");
            DamagedWarnBorder.Visibility = hasDamaged ? Visibility.Visible : Visibility.Collapsed;
            RadSell.IsEnabled            = !hasDamaged;
            if (hasDamaged) RadDispose.IsChecked = true;

            await LoadVendorListAsync();

            // Load active outbound batches for "Use Existing" mode
            try
            {
                var activeBatches = await _service.GetOutboundBatchesAsync();
                foreach (var b in activeBatches)
                {
                    string label = $"Batch #{b.BatchId}  —  {b.BatchPurpose}  —  {b.VendorName}  ({b.TotalQty} units)";
                    _activeBatchIds.Add(b.BatchId);
                    _activeBatchLabels.Add(label);
                    CmbExistingBatch.Items.Add(label);
                }
                if (CmbExistingBatch.Items.Count > 0)
                    CmbExistingBatch.SelectedIndex = 0;
                else
                    RadExisting.IsEnabled = false;
            }
            catch (Exception ex)
            {
                Logger.LogError("OutboundBatchAssignmentWpfDialog.LoadStep2Async: failed to load active batches", ex);
            }
        }

        private async Task LoadVendorListAsync()
        {
            _vendorIds.Clear();
            _vendorNames.Clear();
            CmbVendor.Items.Clear();

            string purpose = RadDispose.IsChecked == true ? "DISPOSE" : "SELL";
            string flagCol = purpose == "DISPOSE" ? "IsDisposer" : "IsBuyer";

            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    await con.OpenAsync();

                    string vendorSql = $@"
                        SELECT v.VendorID, v.VendorName
                        FROM dbo.Vendor v
                        LEFT JOIN dbo.ArchiveStatus arc
                            ON arc.EntityType = 'Vendor' AND arc.EntityId = v.VendorID AND arc.IsArchived = 1
                        WHERE v.IsActive = 1 AND v.{flagCol} = 1 AND arc.ArchiveId IS NULL
                        ORDER BY v.VendorName";

                    using (var cmd    = new SqlCommand(vendorSql, con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _vendorIds.Add(reader.GetInt32(0));
                            _vendorNames.Add(reader.GetString(1));
                            CmbVendor.Items.Add(reader.GetString(1));
                        }
                    }
                }

                if (CmbVendor.Items.Count > 0)
                    CmbVendor.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Logger.LogError("OutboundBatchAssignmentWpfDialog.LoadVendorListAsync failed", ex);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // STEP 1 — EVENT HANDLERS
        // ═════════════════════════════════════════════════════════════════════

        private void CmbModelFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => BindFilteredReturns();

        private void ChkSelectAll_CheckChanged(object sender, RoutedEventArgs e)
        {
            bool val = ChkSelectAll.IsChecked == true;
            foreach (var r in _filteredReturns)
                r.IsSelected = val;
        }

        private void RowCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateSelectionStatus();
            UpdateSelectAllCheckbox();
        }

        // ═════════════════════════════════════════════════════════════════════
        // STEP 2 — EVENT HANDLERS
        // ═════════════════════════════════════════════════════════════════════

        private void RadBatchMode_Changed(object sender, RoutedEventArgs e)
        {
            if (PnlNew == null) return;
            bool isNew = RadNew.IsChecked == true;
            PnlNew.Visibility      = isNew ? Visibility.Visible  : Visibility.Collapsed;
            PnlExisting.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
            BtnConfirm.Content     = isNew ? "Create Batch" : "Add to Batch";
        }

        private async void RadPurpose_Changed(object sender, RoutedEventArgs e)
        {
            if (CmbVendor == null) return;
            await LoadVendorListAsync();
        }

        // ═════════════════════════════════════════════════════════════════════
        // NAVIGATION
        // ═════════════════════════════════════════════════════════════════════

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            var selected = _filteredReturns.Where(r => r.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Select at least one cartridge to continue.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnNext.IsEnabled = false;
            try
            {
                await LoadStep2Async();
                GoToStep(2);
            }
            finally
            {
                BtnNext.IsEnabled = true;
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == 2) GoToStep(1);
        }

        private void GoToStep(int step)
        {
            _currentStep = step;

            Step1Panel.Visibility = step == 1 ? Visibility.Visible  : Visibility.Collapsed;
            Step2Panel.Visibility = step == 2 ? Visibility.Visible  : Visibility.Collapsed;
            BtnBack.Visibility    = step > 1  ? Visibility.Visible  : Visibility.Collapsed;
            BtnNext.Visibility    = step < 2  ? Visibility.Visible  : Visibility.Collapsed;
            BtnConfirm.Visibility = step == 2 ? Visibility.Visible  : Visibility.Collapsed;

            var activeColor   = (Color)ColorConverter.ConvertFromString("#E07820");
            var inactiveColor = (Color)ColorConverter.ConvertFromString("#CCCCCC");

            Dot1.Fill  = new SolidColorBrush(step >= 1 ? activeColor : inactiveColor);
            Dot2.Fill  = new SolidColorBrush(step >= 2 ? activeColor : inactiveColor);
            Lbl1.Foreground = new SolidColorBrush(step >= 1 ? activeColor : inactiveColor);
            Lbl2.Foreground = new SolidColorBrush(step >= 2 ? activeColor : inactiveColor);
        }

        // ═════════════════════════════════════════════════════════════════════
        // CONFIRM
        // ═════════════════════════════════════════════════════════════════════

        private async void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (_modelGroups.Count == 0)
            {
                MessageBox.Show("No model groups to assign.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (RadExisting.IsChecked == true)
            {
                await ConfirmUseExistingAsync();
                return;
            }

            await ConfirmCreateNewAsync();
        }

        private async Task ConfirmCreateNewAsync()
        {
            if (CmbVendor.SelectedIndex < 0 || _vendorIds.Count == 0)
            {
                MessageBox.Show("Please select a vendor.", "No Vendor Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string purpose    = RadDispose.IsChecked == true ? "DISPOSE" : "SELL";
            int    vendorId   = _vendorIds[CmbVendor.SelectedIndex];
            string vendorName = _vendorNames[CmbVendor.SelectedIndex];
            string remarks    = TxtRemarks.Text.Trim();
            int    totalUnits = _modelGroups.Sum(g => g.TotalQty);
            int    userId     = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;

            var summaryLines = _modelGroups.Select(g =>
                $"  • [{g.CartridgeModel}]  {g.TotalQty} unit(s)  ({g.EmptyCartridgeIds.Count} row(s))");

            var confirm = MessageBox.Show(
                $"Create a new {purpose} batch for {_modelGroups.Count} model(s)?\n\n" +
                $"Vendor: {vendorName}\nTotal units: {totalUnits}\n\n" +
                string.Join("\n", summaryLines) + "\n\nProceed?",
                "Confirm Batch Creation", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            BtnConfirm.IsEnabled = false;
            BtnBack.IsEnabled    = false;
            try
            {
                int batchId = await _service.CreateOutboundBatchAsync(
                    vendorId,
                    _modelGroups.Select(g => new { g.CartridgeModelId, g.TotalQty }).ToList(),
                    purpose,
                    userId,
                    string.IsNullOrWhiteSpace(remarks) ? null : remarks);

                var allIds = _modelGroups.SelectMany(g => g.EmptyCartridgeIds).ToList();
                await _service.AssignReturnsToBatchAsync(batchId, allIds, userId);

                Logger.LogInfo(
                    $"OutboundBatchAssignmentWpfDialog: Created batch {batchId} ({purpose}) — " +
                    $"vendor={vendorId}, models={_modelGroups.Count}, units={totalUnits}");

                MessageBox.Show(
                    $"Outbound batch #{batchId} created ({purpose}).\n\n" +
                    $"Vendor: {vendorName}\n" +
                    $"{allIds.Count} row(s) assigned.\n\n" +
                    string.Join("\n", _modelGroups.Select(g => $"  • [{g.CartridgeModel}]  {g.TotalQty} unit(s)")),
                    "Batch Created", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating batch:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("OutboundBatchAssignmentWpfDialog.ConfirmCreateNewAsync failed", ex);
            }
            finally
            {
                BtnConfirm.IsEnabled = true;
                BtnBack.IsEnabled    = true;
            }
        }

        private async Task ConfirmUseExistingAsync()
        {
            if (CmbExistingBatch.SelectedIndex < 0 || _activeBatchIds.Count == 0)
            {
                MessageBox.Show("Please select an active batch.", "No Batch Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int    existingBatchId   = _activeBatchIds[CmbExistingBatch.SelectedIndex];
            string existingBatchLabel = _activeBatchLabels[CmbExistingBatch.SelectedIndex];
            var    allIds             = _modelGroups.SelectMany(g => g.EmptyCartridgeIds).ToList();
            int    totalUnits         = _modelGroups.Sum(g => g.TotalQty);
            int    userId             = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;

            var confirm = MessageBox.Show(
                $"Add {totalUnits} unit(s) to existing batch?\n\nBatch: {existingBatchLabel}\n\nProceed?",
                "Confirm Assignment", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            BtnConfirm.IsEnabled = false;
            BtnBack.IsEnabled    = false;
            try
            {
                int assigned = await _service.AssignReturnsToBatchAsync(existingBatchId, allIds, userId);

                Logger.LogInfo(
                    $"OutboundBatchAssignmentWpfDialog: Assigned {assigned} units to existing batch {existingBatchId}");

                MessageBox.Show(
                    $"Added {assigned} unit(s) to batch #{existingBatchId}.\n\nBatch: {existingBatchLabel}",
                    "Added to Batch", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding to batch:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("OutboundBatchAssignmentWpfDialog.ConfirmUseExistingAsync failed", ex);
            }
            finally
            {
                BtnConfirm.IsEnabled = true;
                BtnBack.IsEnabled    = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;

        // ═════════════════════════════════════════════════════════════════════
        // NESTED TYPES
        // ═════════════════════════════════════════════════════════════════════

        public class SelectableOutboundReturn : INotifyPropertyChanged
        {
            public UnassignedReturnDto Source { get; }

            public int    EmptyCartridgeId    { get; }
            public int    CartridgeModelId    { get; }
            public string CartridgeModel      { get; }
            public string SupplierName        { get; }
            public int    Quantity            { get; }
            public string ConditionStatus     { get; }
            public string ReturnedAtFormatted { get; }
            public string Remarks             { get; }

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public SelectableOutboundReturn(UnassignedReturnDto dto)
            {
                Source              = dto;
                EmptyCartridgeId    = dto.EmptyCartridgeId;
                CartridgeModelId    = dto.CartridgeModelId;
                CartridgeModel      = dto.CartridgeModel;
                SupplierName        = dto.SupplierName;
                Quantity            = dto.Quantity;
                ConditionStatus     = dto.ConditionStatus ?? "GOOD";
                ReturnedAtFormatted = dto.ReturnedAt.ToString("yyyy-MM-dd HH:mm");
                Remarks             = dto.Remarks;
            }
        }

        public class OutboundModelGroupItem
        {
            public int       CartridgeModelId  { get; set; }
            public string    CartridgeModel    { get; set; }
            public List<int> EmptyCartridgeIds { get; set; } = new List<int>();
            public int       TotalQty          { get; set; }
            public int       RowCount          => EmptyCartridgeIds?.Count ?? 0;
        }
    }
}
