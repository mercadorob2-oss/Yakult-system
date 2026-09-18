using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class VendorCartridgeRefillViewModel : ViewModelBase
    {
        private const int PageSize = 15;
        private readonly CartridgeRefillService _service;

        private List<RefillEligibilityResult> _all      = new List<RefillEligibilityResult>();
        private List<RefillEligibilityResult> _filtered = new List<RefillEligibilityResult>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        // ── State fields ─────────────────────────────────────────────────────────
        private bool   _isLoading;
        private string _searchText    = string.Empty;
        private string _vendorFilter  = "All Vendors";
        private string _statusFilter  = "All";
        private RefillEligibilityResult _selectedBatch;
        private bool   _showDamagedNotice;
        private string _damagedNoticeText = string.Empty;
        private string _pageInfo     = "Page 1 of 1  (0 records)";
        private bool   _canGoPrev;
        private bool   _canGoNext;
        private int    _totalBatches;
        private int    _activeCount;
        private int    _sentCount;
        private int    _closedCount;
        private int    _totalReturnedQty;
        private bool   _canEdit;
        private bool   _canDelete;
        private bool   _canSend;

        // ── Observable collections ───────────────────────────────────────────────
        public ObservableCollection<RefillEligibilityResult>  PagedBatches    { get; } = new ObservableCollection<RefillEligibilityResult>();
        public ObservableCollection<VendorBatchAuditTrailDto> AuditTrailItems { get; } = new ObservableCollection<VendorBatchAuditTrailDto>();
        public ObservableCollection<string>                   VendorOptions   { get; } = new ObservableCollection<string>();

        public VendorCartridgeRefillViewModel()
        {
            _service = new CartridgeRefillService();
        }

        // ── Properties ───────────────────────────────────────────────────────────
        public bool   IsLoading          { get => _isLoading;          set => SetField(ref _isLoading,          value); }
        public string SearchText         { get => _searchText;         set { if (SetField(ref _searchText,         value)) ApplyFilter(); } }
        public string VendorFilter       { get => _vendorFilter;       set { if (SetField(ref _vendorFilter,       value)) ApplyFilter(); } }
        public string StatusFilter       { get => _statusFilter;       set { if (SetField(ref _statusFilter,       value)) ApplyFilter(); } }
        public bool   ShowDamagedNotice  { get => _showDamagedNotice;  set => SetField(ref _showDamagedNotice,  value); }
        public string DamagedNoticeText  { get => _damagedNoticeText;  set => SetField(ref _damagedNoticeText,  value); }
        public string PageInfo           { get => _pageInfo;           set => SetField(ref _pageInfo,           value); }
        public bool   CanGoPrev          { get => _canGoPrev;          set => SetField(ref _canGoPrev,          value); }
        public bool   CanGoNext          { get => _canGoNext;          set => SetField(ref _canGoNext,          value); }
        public int    TotalBatches       { get => _totalBatches;       set => SetField(ref _totalBatches,       value); }
        public int    ActiveCount        { get => _activeCount;        set => SetField(ref _activeCount,        value); }
        public int    SentForRefillCount { get => _sentCount;          set => SetField(ref _sentCount,          value); }
        public int    ClosedCount        { get => _closedCount;        set => SetField(ref _closedCount,        value); }
        public int    TotalReturnedQty   { get => _totalReturnedQty;   set => SetField(ref _totalReturnedQty,   value); }
        public bool   CanEditBatch       { get => _canEdit;            set => SetField(ref _canEdit,            value); }
        public bool   CanDeleteBatch     { get => _canDelete;          set => SetField(ref _canDelete,          value); }
        public bool   CanSendToVendor    { get => _canSend;            set => SetField(ref _canSend,            value); }

        public RefillEligibilityResult SelectedBatch
        {
            get => _selectedBatch;
            set
            {
                if (SetField(ref _selectedBatch, value))
                {
                    OnPropertyChanged(nameof(HasSelectedBatch));
                    UpdateButtonStates();
                }
            }
        }

        public bool HasSelectedBatch => _selectedBatch != null;

        // ── Data loading ─────────────────────────────────────────────────────────
        public async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                await _service.ProcessEligibleBatchesAsync();
                _all = await _service.GetRefillEligibilityAsync();

                var vendors = _all
                    .Select(b => b.VendorName)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct()
                    .OrderBy(v => v)
                    .ToList();

                VendorOptions.Clear();
                VendorOptions.Add("All Vendors");
                foreach (var v in vendors)
                    VendorOptions.Add(v);

                // Clearing VendorOptions pushes null into VendorFilter via TwoWay binding.
                // Reset it so the ComboBox shows "All Vendors" and the filter is correct.
                _vendorFilter = "All Vendors";
                OnPropertyChanged(nameof(VendorFilter));

                UpdateStats();
                _currentPage = 1;
                ApplyFilter();

                AuditTrailItems.Clear();
                SelectedBatch = null;

                var damaged      = await _service.GetDamagedUnassignedReturnsAsync();
                int damagedCount = damaged.Sum(d => d.Quantity);
                if (damagedCount > 0)
                {
                    DamagedNoticeText = $"⚠  {damagedCount} DAMAGED returned cartridge(s) are excluded from refill batches " +
                                        "— process them via Cartridge Dispose / Sell Batch.";
                    ShowDamagedNotice = true;
                }
                else
                {
                    ShowDamagedNotice = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading vendor refill data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("VendorCartridgeRefillViewModel.LoadDataAsync failed", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadAuditTrailAsync(int batchId)
        {
            try
            {
                var raw = await _service.GetBatchAuditTrailAsync(batchId);

                var grouped = raw
                    .GroupBy(x => new { x.RequestId, x.CartridgeModelId, x.CartridgeModel, x.Vendor, x.BatchId })
                    .Select(g => new VendorBatchAuditTrailDto
                    {
                        EmptyCartridgeId = g.Max(x => x.EmptyCartridgeId),
                        RequestId        = g.Key.RequestId,
                        RequestDate      = g.Min(x => x.RequestDate),
                        ReturnedQty      = g.Sum(x => x.ReturnedQty),
                        CartridgeModelId = g.Key.CartridgeModelId,
                        CartridgeModel   = g.Key.CartridgeModel,
                        Vendor           = g.Key.Vendor,
                        BatchId          = g.Key.BatchId,
                        ReturnedByName   = g.Select(x => x.ReturnedByName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count() == 1
                            ? g.Select(x => x.ReturnedByName).FirstOrDefault()
                            : "Multiple",
                        Remarks = g.Select(x => x.Remarks).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count() == 1
                            ? g.Select(x => x.Remarks).FirstOrDefault()
                            : string.Empty
                    })
                    .OrderByDescending(x => x.RequestDate)
                    .ThenByDescending(x => x.RequestId ?? 0)
                    .ToList();

                AuditTrailItems.Clear();
                foreach (var item in grouped)
                    AuditTrailItems.Add(item);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading audit trail: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("VendorCartridgeRefillViewModel.LoadAuditTrailAsync failed", ex);
            }
        }

        // ── Filtering and pagination ──────────────────────────────────────────────
        private void ApplyFilter()
        {
            _filtered = _all.Where(b =>
            {
                if (!string.IsNullOrWhiteSpace(_searchText))
                {
                    var q = _searchText.ToLower();
                    if (!b.BatchId.ToString().Contains(q) &&
                        !(b.VendorName?.ToLower().Contains(q) ?? false) &&
                        !(b.CartridgeModel?.ToLower().Contains(q) ?? false))
                        return false;
                }
                if (!string.IsNullOrEmpty(_vendorFilter) && _vendorFilter != "All Vendors" && b.VendorName != _vendorFilter)
                    return false;
                if (_statusFilter != "All" && b.Status != _statusFilter)
                    return false;
                return true;
            }).ToList();

            _currentPage = 1;
            ApplyPage();
        }

        private void ApplyPage()
        {
            _totalPages  = _filtered.Count == 0 ? 1 : (int)Math.Ceiling(_filtered.Count / (double)PageSize);
            _currentPage = Math.Max(1, Math.Min(_currentPage, _totalPages));

            var page = _filtered
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            PagedBatches.Clear();
            foreach (var b in page)
                PagedBatches.Add(b);

            int from = _filtered.Count == 0 ? 0 : (_currentPage - 1) * PageSize + 1;
            int to   = Math.Min(_currentPage * PageSize, _filtered.Count);
            PageInfo = $"Page {_currentPage} of {_totalPages}  ({from}–{to} of {_filtered.Count})";
            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
        }

        public void GoToPrevPage() { if (_currentPage > 1)           { _currentPage--; ApplyPage(); } }
        public void GoToNextPage() { if (_currentPage < _totalPages) { _currentPage++; ApplyPage(); } }

        public void ClearFilters()
        {
            _searchText   = string.Empty;
            _vendorFilter = "All Vendors";
            _statusFilter = "All";
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(VendorFilter));
            OnPropertyChanged(nameof(StatusFilter));
            _currentPage = 1;
            ApplyFilter();
        }

        private void UpdateStats()
        {
            TotalBatches       = _all.Count;
            ActiveCount        = _all.Count(b => b.Status == "Active");
            SentForRefillCount = _all.Count(b => b.Status == "SentForRefill");
            ClosedCount        = _all.Count(b => b.Status == "Closed");
            TotalReturnedQty   = _all.Sum(b => b.ReturnedQty);
        }

        private void UpdateButtonStates()
        {
            CanEditBatch    = _selectedBatch != null && _selectedBatch.Status == "Active";
            CanDeleteBatch  = _selectedBatch != null;
            CanSendToVendor = _selectedBatch != null && _selectedBatch.Status == "Active" && _selectedBatch.IsRefillable;
        }

        // ── Business actions ──────────────────────────────────────────────────────
        public async Task HandleRowActionAsync(RefillEligibilityResult batch)
        {
            if (batch == null) return;
            switch (batch.Status)
            {
                case "Active":       await SendToVendorAsync(batch); break;
                case "SentForRefill": await CloseBatchAsync(batch);  break;
            }
        }

        public async Task SendToVendorAsync(RefillEligibilityResult batch)
        {
            if (!batch.IsRefillable)
            {
                MessageBox.Show(
                    $"This cartridge model is marked as non-refillable and cannot be sent to a vendor for refill.\n\n" +
                    $"Model: {batch.CartridgeModel}\n\nNon-refillable cartridges must be disposed of or sold internally by IT.",
                    "Non-Refillable Model", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(
                    $"Send this batch to {batch.VendorName} for refill?\n\n" +
                    $"Batch ID : {batch.BatchId}\nVendor   : {batch.VendorName}\n" +
                    $"Model    : {batch.CartridgeModel}\nReturned : {batch.ReturnedQty} cartridge(s)\n\n" +
                    "The batch status will change to 'SentForRefill'.\nProceed?",
                    "Confirm Send to Vendor", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                IsLoading = true;
                var details = await _service.GetBatchByIdAsync(batch.BatchId);
                if (details == null) { MessageBox.Show("Batch not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }

                await _service.SendBatchForRefillAsync(
                    batch.BatchId, details.VendorId, batch.ReturnedQty,
                    AppSession.CurrentUserId,
                    $"Sent {batch.ReturnedQty} empty cartridge(s) to {batch.VendorName} for refill");

                MessageBox.Show(
                    $"Batch {batch.BatchId} has been sent to {batch.VendorName} for refill.\n\n" +
                    $"Quantity: {batch.ReturnedQty} cartridge(s)\nStatus: SentForRefill",
                    "Sent Successfully", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending batch to vendor: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("VendorCartridgeRefillViewModel.SendToVendorAsync failed", ex);
            }
            finally { IsLoading = false; }
        }

        public async Task CloseBatchAsync(RefillEligibilityResult batch)
        {
            if (MessageBox.Show(
                    $"⚠ CLOSE BATCH — THIS CANNOT BE UNDONE\n\n" +
                    $"You are closing this refill batch as a completed audit record.\n" +
                    $"The empty cartridges assigned to it will be marked as Closed.\n\n" +
                    $"Batch ID : {batch.BatchId}\nVendor   : {batch.VendorName}\nModel    : {batch.CartridgeModel}\n\n" +
                    "To add refilled stock back to inventory, use Batch Add Item and select Origin = Refilled.\n\nProceed?",
                    "Close Batch", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                IsLoading = true;
                await _service.CloseRefillBatchAsync(batch.BatchId, AppSession.CurrentUserId);
                MessageBox.Show(
                    $"Batch #{batch.BatchId} has been closed.\n\nAll assigned empty cartridges have been marked as Closed.",
                    "Batch Closed", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error closing batch: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("VendorCartridgeRefillViewModel.CloseBatchAsync failed", ex);
            }
            finally { IsLoading = false; }
        }

        public async Task DeleteBatchAsync()
        {
            var batch = _selectedBatch;
            if (batch == null) return;

            if (batch.Status == "SentForRefill")
            {
                MessageBox.Show(
                    $"Batch #{batch.BatchId} has already been sent to the vendor and cannot be deleted.\n\n" +
                    "Only Active, Eligible, or Completed batches can be deleted.",
                    "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string msg = batch.Status == "Active"
                ? $"⚠  Batch #{batch.BatchId} is still Active.\n\n" +
                  $"  • Model:  {batch.CartridgeModel}\n  • Vendor: {batch.VendorName}\n  • Qty: {batch.ReturnedQty} assigned cartridge(s)\n\n" +
                  "Deleting this batch will unassign all linked cartridges and return them to Pending status.\n\nAre you sure?"
                : $"Delete Batch #{batch.BatchId}? (Status: {batch.Status})\n\n" +
                  $"  • Model:  {batch.CartridgeModel}\n  • Vendor: {batch.VendorName}\n\nThis action cannot be undone.";

            if (MessageBox.Show(msg, $"Confirm Delete Batch #{batch.BatchId}",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                IsLoading = true;
                int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
                await _service.DeleteRefillBatchAsync(batch.BatchId, userId);
                MessageBox.Show($"Batch #{batch.BatchId} deleted successfully.", "Deleted",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting batch: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError($"VendorCartridgeRefillViewModel.DeleteBatchAsync(batchId={batch.BatchId}) failed", ex);
            }
            finally { IsLoading = false; }
        }

        public async Task RemoveFromBatchAsync(VendorBatchAuditTrailDto row)
        {
            if (row == null || _selectedBatch == null) return;

            string reqInfo = row.RequestId.HasValue ? $"Request #{row.RequestId}" : $"Empty Cartridge #{row.EmptyCartridgeId}";
            if (MessageBox.Show(
                    $"Remove from Batch #{_selectedBatch.BatchId}?\n\n  {reqInfo}\n  {row.CartridgeModel}  ×  {row.ReturnedQty} unit(s)\n\n" +
                    "The cartridge(s) will be returned to the unassigned pool\n" +
                    "and can be re-assigned via Assign Returns to Refill Batch.",
                    "Remove from Batch", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                int qty = await _service.RemoveFromBatchAsync(
                    _selectedBatch.BatchId, row.RequestId, row.CartridgeModelId,
                    row.EmptyCartridgeId, AppSession.CurrentUserId);

                MessageBox.Show(
                    $"Removed {qty} unit(s) from Batch #{_selectedBatch.BatchId}.\n" +
                    "They are now available in Assign Returns to Refill Batch.",
                    "Removed Successfully", MessageBoxButton.OK, MessageBoxImage.Information);

                int bid = _selectedBatch.BatchId;
                await LoadDataAsync();
                await LoadAuditTrailAsync(bid);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing from batch: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task TransferToBatchAsync(VendorBatchAuditTrailDto row, VendorCartridgeBatchDto targetBatch)
        {
            if (row == null || _selectedBatch == null || targetBatch == null) return;

            try
            {
                int qty = await _service.TransferToBatchAsync(
                    _selectedBatch.BatchId, targetBatch.BatchId,
                    row.RequestId, row.CartridgeModelId,
                    row.EmptyCartridgeId, AppSession.CurrentUserId);

                MessageBox.Show(
                    $"Transferred {qty} unit(s)\nfrom Batch #{_selectedBatch.BatchId}  →  Batch #{targetBatch.BatchId}  ({targetBatch.VendorName}).",
                    "Transferred Successfully", MessageBoxButton.OK, MessageBoxImage.Information);

                int bid = _selectedBatch.BatchId;
                await LoadDataAsync();
                await LoadAuditTrailAsync(bid);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error transferring: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task<List<VendorCartridgeBatchDto>> GetActiveBatchesExceptCurrentAsync()
        {
            if (_selectedBatch == null) return new List<VendorCartridgeBatchDto>();
            var all = await _service.GetAllActiveBatchesAsync();
            return all.Where(b => b.BatchId != _selectedBatch.BatchId).ToList();
        }

        public async Task<VendorCartridgeBatchDto> GetBatchDetailsAsync(int batchId)
            => await _service.GetBatchByIdAsync(batchId);

        public CartridgeRefillService Service => _service;
    }
}
