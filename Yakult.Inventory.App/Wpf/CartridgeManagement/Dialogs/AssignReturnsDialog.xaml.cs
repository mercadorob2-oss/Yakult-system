using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Dialogs
{
    public partial class AssignReturnsDialog : Window
    {
        private readonly CartridgeRefillService _service;
        private int _currentStep = 1;

        // Step 1 state
        private List<UnassignedReturnDto> _allReturns = new List<UnassignedReturnDto>();
        private readonly ObservableCollection<SelectableReturn> _filteredReturns
            = new ObservableCollection<SelectableReturn>();
        private readonly List<int>    _modelFilterIds   = new List<int>();
        private readonly List<string> _modelFilterNames = new List<string>();

        // Step 2 state
        private readonly ObservableCollection<ModelGroupItem> _modelGroups
            = new ObservableCollection<ModelGroupItem>();
        private readonly List<int>    _vendorIds         = new List<int>();
        private readonly List<string> _vendorNames       = new List<string>();
        private readonly List<int>    _activeBatchIds    = new List<int>();
        private readonly List<string> _activeBatchLabels = new List<string>();

        public AssignReturnsDialog()
        {
            _service = new CartridgeRefillService();
            InitializeComponent();
            ReturnsGrid.ItemsSource = _filteredReturns;
            ModelCards.ItemsSource  = _modelGroups;
            Loaded += async (s, e) => await LoadStep1Async();
        }

        // ── Step 1: load data ──────────────────────────────────────────────────
        private async Task LoadStep1Async()
        {
            BtnNext.IsEnabled = false;
            try
            {
                _allReturns = await _service.GetUnassignedReturnsAsync();

                _modelFilterIds.Clear();
                _modelFilterNames.Clear();
                CmbModelFilter.Items.Clear();

                _modelFilterIds.Add(0);
                _modelFilterNames.Add("(All Models)");
                CmbModelFilter.Items.Add("(All Models)");

                foreach (var g in _allReturns
                    .GroupBy(r => r.CartridgeModelId)
                    .OrderBy(g => g.First().CartridgeModel))
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
                MessageBox.Show($"Error loading returns:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("AssignReturnsDialog.LoadStep1Async failed", ex);
            }
            finally
            {
                BtnNext.IsEnabled = true;
            }
        }

        private void BindFilteredReturns()
        {
            // Unsubscribe old
            foreach (var r in _filteredReturns)
                r.PropertyChanged -= OnReturnSelectionChanged;

            _filteredReturns.Clear();

            int filterModelId = (CmbModelFilter.SelectedIndex >= 0)
                ? _modelFilterIds[CmbModelFilter.SelectedIndex]
                : 0;

            var source = filterModelId == 0
                ? _allReturns
                : _allReturns.Where(r => r.CartridgeModelId == filterModelId).ToList();

            foreach (var item in source)
            {
                var sr = new SelectableReturn(item);
                sr.PropertyChanged += OnReturnSelectionChanged;
                _filteredReturns.Add(sr);
            }

            UpdateSelectionStatus();
            UpdateSelectAllCheckbox();
        }

        private void OnReturnSelectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectableReturn.IsSelected))
            {
                UpdateSelectionStatus();
                UpdateSelectAllCheckbox();
            }
        }

        private void UpdateSelectionStatus()
        {
            int rows = _filteredReturns.Count(r => r.IsSelected);
            int qty  = _filteredReturns.Where(r => r.IsSelected).Sum(r => r.Quantity);
            int models = _filteredReturns.Where(r => r.IsSelected)
                                         .Select(r => r.CartridgeModelId)
                                         .Distinct().Count();

            if (rows == 0)
            {
                TxtSelectionStatus.Text      = "0 rows selected • 0 units";
                TxtSelectionStatus.Foreground = System.Windows.Media.Brushes.Gray;
            }
            else
            {
                string modelNote = models > 1
                    ? $"  •  {models} models — will be grouped into one batch"
                    : "";
                TxtSelectionStatus.Text = $"{rows} rows selected • {qty} units{modelNote}";
                TxtSelectionStatus.Foreground =
                    new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter
                            .ConvertFromString("#1E9E5E"));
            }
        }

        private void UpdateSelectAllCheckbox()
        {
            if (_filteredReturns.Count == 0)
            {
                ChkSelectAll.IsChecked = false;
                return;
            }
            int checked_ = _filteredReturns.Count(r => r.IsSelected);
            ChkSelectAll.IsChecked = (checked_ == _filteredReturns.Count) ? true :
                                     (checked_ == 0)                      ? false :
                                     (bool?)null; // indeterminate
        }

        // ── Step 1: event handlers ─────────────────────────────────────────────
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
            // Binding handles IsSelected; just refresh status + header
            UpdateSelectionStatus();
            UpdateSelectAllCheckbox();
        }

        // ── Navigation ─────────────────────────────────────────────────────────
        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep != 1) return;

            var selected = _filteredReturns.Where(r => r.IsSelected).Select(r => r.Source).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Please select at least one returned cartridge row to assign.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnNext.IsEnabled = false;
            try
            {
                var selectedIds = selected.Select(r => r.EmptyCartridgeId).ToList();
                var eligibleIds = await _service.GetEligibleRefillEmptyCartridgeIdsAsync(selectedIds);
                var eligibleSet = new HashSet<int>(eligibleIds);
                var filtered    = selected.Where(r => eligibleSet.Contains(r.EmptyCartridgeId)).ToList();

                if (filtered.Count == 0)
                {
                    MessageBox.Show(
                        "None of the selected returns are eligible for refill batching.\n" +
                        "Only refillable cartridge models can be assigned to a refill batch.",
                        "No Eligible Models", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (filtered.Count != selected.Count)
                {
                    var excluded = selected
                        .Where(r => !eligibleSet.Contains(r.EmptyCartridgeId))
                        .GroupBy(r => r.CartridgeModel)
                        .OrderBy(g => g.Key)
                        .Select(g => $"  • {g.Key} ({g.Sum(x => x.Quantity)} unit(s))");

                    MessageBox.Show(
                        "Some selected returns are not eligible and will be excluded:\n\n" +
                        string.Join("\n", excluded),
                        "Ineligible Models Excluded", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                _modelGroups.Clear();
                foreach (var g in filtered
                    .GroupBy(r => r.CartridgeModelId)
                    .OrderBy(g => g.First().CartridgeModel))
                {
                    _modelGroups.Add(new ModelGroupItem
                    {
                        CartridgeModelId  = g.Key,
                        CartridgeModel    = g.First().CartridgeModel,
                        EmptyCartridgeIds = g.Select(r => r.EmptyCartridgeId).ToList(),
                        TotalQty          = g.Sum(r => r.Quantity)
                    });
                }

                UpdateStep2Summary();
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

            Step1Panel.Visibility = (step == 1) ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = (step == 2) ? Visibility.Visible : Visibility.Collapsed;
            BtnBack.Visibility    = (step > 1)  ? Visibility.Visible : Visibility.Collapsed;
            BtnNext.Visibility    = (step < 2)  ? Visibility.Visible : Visibility.Collapsed;
            BtnConfirm.Visibility = (step == 2) ? Visibility.Visible : Visibility.Collapsed;

            // Step indicator colors
            var activeColor   = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3A8EF6");
            var inactiveColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CCCCCC");

            Dot1.Fill = new System.Windows.Media.SolidColorBrush(step >= 1 ? activeColor : inactiveColor);
            Dot2.Fill = new System.Windows.Media.SolidColorBrush(step >= 2 ? activeColor : inactiveColor);
            Lbl1.Foreground = new System.Windows.Media.SolidColorBrush(step >= 1 ? activeColor : inactiveColor);
            Lbl2.Foreground = new System.Windows.Media.SolidColorBrush(step >= 2 ? activeColor : inactiveColor);
        }

        // ── Step 2: load data ──────────────────────────────────────────────────
        private async Task LoadStep2Async()
        {
            _vendorIds.Clear();
            _vendorNames.Clear();
            _activeBatchIds.Clear();
            _activeBatchLabels.Clear();
            CmbVendor.Items.Clear();
            CmbExistingBatch.Items.Clear();
            RadNew.IsChecked = true;

            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();

                    const string vendorSql = @"
                        SELECT v.VendorID, v.VendorName
                        FROM dbo.Vendor v
                        LEFT JOIN dbo.ArchiveStatus arc
                            ON arc.EntityType = 'Vendor' AND arc.EntityId = v.VendorID AND arc.IsArchived = 1
                        WHERE v.IsActive = 1 AND v.IsRefiller = 1 AND arc.ArchiveId IS NULL
                        ORDER BY v.VendorName";

                    using (var cmd = new SqlCommand(vendorSql, con))
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            _vendorIds.Add(rdr.GetInt32(0));
                            _vendorNames.Add(rdr.GetString(1));
                            CmbVendor.Items.Add(rdr.GetString(1));
                        }
                    }
                }

                if (CmbVendor.Items.Count > 0)
                    CmbVendor.SelectedIndex = 0;

                var activeBatches = await _service.GetAllActiveBatchesAsync();
                foreach (var b in activeBatches)
                {
                    string label = $"Batch #{b.BatchId}  —  {b.VendorName}  —  {b.CartridgeModel}  ({b.ReturnedQty} returned)";
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
                MessageBox.Show($"Error loading vendor data:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("AssignReturnsDialog.LoadStep2Async failed", ex);
            }
        }

        private void UpdateStep2Summary()
        {
            int totalModels = _modelGroups.Count;
            int totalUnits  = _modelGroups.Sum(g => g.TotalQty);
            TxtStep2Summary.Text = totalModels == 0
                ? "0 model(s) — 0 total units"
                : $"{totalModels} model(s)  •  {totalUnits} total units  •  " +
                  string.Join(", ", _modelGroups.Select(g => $"{g.CartridgeModel} ({g.TotalQty})"));

            BtnConfirm.IsEnabled = totalModels > 0;
        }

        // ── Step 2: event handlers ─────────────────────────────────────────────
        private void RadBatchMode_Changed(object sender, RoutedEventArgs e)
        {
            if (PnlNew == null) return;
            bool isNew = RadNew.IsChecked == true;
            PnlNew.Visibility      = isNew ? Visibility.Visible : Visibility.Collapsed;
            PnlExisting.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
        }

        private void BtnRemoveModel_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as Button)?.Tag is int modelId)) return;
            var group = _modelGroups.FirstOrDefault(g => g.CartridgeModelId == modelId);
            if (group != null) _modelGroups.Remove(group);
            UpdateStep2Summary();
        }

        // ── Confirm ────────────────────────────────────────────────────────────
        private async void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (_modelGroups.Count == 0)
            {
                MessageBox.Show("No eligible models remain. Please go back and select eligible returns.",
                    "Nothing to Confirm", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show("Please select a refill vendor.", "No Vendor Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int vendorId    = _vendorIds[CmbVendor.SelectedIndex];
            string vendorName = _vendorNames[CmbVendor.SelectedIndex];
            string remarks  = TxtRemarks.Text.Trim();
            int totalUnits  = _modelGroups.Sum(g => g.TotalQty);

            var summaryLines = _modelGroups.Select(g =>
                $"  • [{g.CartridgeModel}]  {g.TotalQty} unit(s)  ({g.EmptyCartridgeIds.Count} row(s))");

            var confirm = MessageBox.Show(
                $"Create one refill batch for {_modelGroups.Count} model(s)?\n\n" +
                $"Vendor: {vendorName}\nTotal units: {totalUnits}\n\n" +
                string.Join("\n", summaryLines) + "\n\n" +
                "All models will be grouped into a single batch.\n\nProceed?",
                "Confirm Batch Creation", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            BtnConfirm.IsEnabled = false;
            BtnBack.IsEnabled    = false;

            try
            {
                int batchId = await _service.CreateMultiModelRefillBatchAsync(
                    vendorId,
                    _modelGroups,
                    AppSession.CurrentUserId,
                    string.IsNullOrEmpty(remarks) ? null : remarks);

                Logger.LogInfo(
                    $"AssignReturnsDialog: Created multi-model batch {batchId} — " +
                    $"vendor={vendorId}, models={_modelGroups.Count}");

                var allEmptyIds = _modelGroups.SelectMany(g => g.EmptyCartridgeIds).ToList();
                int assignedQty = await _service.AssignReturnsToBatchAsync(
                    batchId, allEmptyIds, AppSession.CurrentUserId);

                Logger.LogInfo($"AssignReturnsDialog: Assigned {assignedQty} units to batch {batchId}");

                MessageBox.Show(
                    $"Batch #{batchId} created successfully!\n\n" +
                    $"Vendor: {vendorName}\nTotal units assigned: {assignedQty}\n\n" +
                    string.Join("\n", _modelGroups.Select(g => $"  • [{g.CartridgeModel}]  {g.TotalQty} unit(s)")) +
                    "\n\nAll selected returns are now linked to this refill batch.",
                    "Assignment Successful", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
            }
            catch (InvalidOperationException ioe)
            {
                MessageBox.Show(ioe.Message, "Cannot Create Batch", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.LogError("AssignReturnsDialog.ConfirmCreateNewAsync: blocked", ioe);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during batch creation:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("AssignReturnsDialog.ConfirmCreateNewAsync failed", ex);
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
                MessageBox.Show("Please select an existing active batch.", "No Batch Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int existingBatchId       = _activeBatchIds[CmbExistingBatch.SelectedIndex];
            string existingBatchLabel = _activeBatchLabels[CmbExistingBatch.SelectedIndex];
            var allEmptyIds           = _modelGroups.SelectMany(g => g.EmptyCartridgeIds).ToList();
            int totalUnits            = _modelGroups.Sum(g => g.TotalQty);

            var summaryLines = _modelGroups.Select(g =>
                $"  • [{g.CartridgeModel}]  {g.TotalQty} unit(s)  ({g.EmptyCartridgeIds.Count} row(s))");

            var confirm = MessageBox.Show(
                $"Add {totalUnits} unit(s) to existing batch?\n\n" +
                $"Batch: {existingBatchLabel}\n\n" +
                string.Join("\n", summaryLines) + "\n\nProceed?",
                "Confirm Assignment", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            BtnConfirm.IsEnabled = false;
            BtnBack.IsEnabled    = false;

            try
            {
                int assignedQty = await _service.AssignReturnsToBatchAsync(
                    existingBatchId, allEmptyIds, AppSession.CurrentUserId);

                Logger.LogInfo(
                    $"AssignReturnsDialog: Assigned {assignedQty} units to existing batch {existingBatchId}");

                MessageBox.Show(
                    $"Assignment successful!\n\nBatch: {existingBatchLabel}\n" +
                    $"Total units assigned: {assignedQty}\n\n" +
                    "All selected returns are now linked to this refill batch.",
                    "Assignment Successful", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
            }
            catch (InvalidOperationException ioe)
            {
                MessageBox.Show(ioe.Message, "Cannot Assign", MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.LogError("AssignReturnsDialog.ConfirmUseExistingAsync: blocked", ioe);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during assignment:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("AssignReturnsDialog.ConfirmUseExistingAsync failed", ex);
            }
            finally
            {
                BtnConfirm.IsEnabled = true;
                BtnBack.IsEnabled    = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // ── Nested types ───────────────────────────────────────────────────────

        public class SelectableReturn : INotifyPropertyChanged
        {
            public UnassignedReturnDto Source { get; }

            public int    EmptyCartridgeId { get; }
            public int    CartridgeModelId { get; }
            public string CartridgeModel   { get; }
            public string SupplierName     { get; }
            public int    Quantity         { get; }
            public string ConditionStatus  { get; }
            public string ReturnedAtFormatted { get; }
            public string Remarks          { get; }

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

            public SelectableReturn(UnassignedReturnDto dto)
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

        public class ModelGroupItem
        {
            public int       CartridgeModelId  { get; set; }
            public string    CartridgeModel    { get; set; }
            public List<int> EmptyCartridgeIds { get; set; } = new List<int>();
            public int       TotalQty          { get; set; }
            public int       RowCount          => EmptyCartridgeIds?.Count ?? 0;
        }
    }
}
