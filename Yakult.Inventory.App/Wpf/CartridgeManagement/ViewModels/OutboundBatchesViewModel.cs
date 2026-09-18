using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class OutboundBatchesViewModel : ViewModelBase
    {
        private readonly CartridgeRefillService _service;

        private List<OutboundBatchDto>         _allBatches = new List<OutboundBatchDto>();
        private List<VendorBatchAuditTrailDto> _rawAudit   = new List<VendorBatchAuditTrailDto>();

        private bool             _isLoading;
        private string           _summaryText   = "";
        private OutboundBatchDto _selectedBatch;
        private bool             _canDelete;
        private int              _totalBatches;
        private int              _totalUnits;

        public ObservableCollection<OutboundBatchDto>         Batches    { get; } = new ObservableCollection<OutboundBatchDto>();
        public ObservableCollection<VendorBatchAuditTrailDto> AuditTrail { get; } = new ObservableCollection<VendorBatchAuditTrailDto>();

        public bool             IsLoading     { get => _isLoading;     set => SetField(ref _isLoading,     value); }
        public string           SummaryText   { get => _summaryText;   set => SetField(ref _summaryText,   value); }
        public bool             CanDelete     { get => _canDelete;     set => SetField(ref _canDelete,     value); }
        public int              TotalBatches  { get => _totalBatches;  set => SetField(ref _totalBatches,  value); }
        public int              TotalUnits    { get => _totalUnits;    set => SetField(ref _totalUnits,    value); }

        public OutboundBatchDto SelectedBatch
        {
            get => _selectedBatch;
            set
            {
                if (!SetField(ref _selectedBatch, value)) return;
                CanDelete = value != null;
                if (value != null)
                    _ = LoadAuditTrailAsync(value.BatchId);
                else
                {
                    _rawAudit = new List<VendorBatchAuditTrailDto>();
                    AuditTrail.Clear();
                }
            }
        }

        public int AuditTrailCount => _rawAudit.Count;

        public OutboundBatchesViewModel()
        {
            _service = new CartridgeRefillService();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            CanDelete = false;
            try
            {
                _allBatches = await _service.GetOutboundBatchesAsync();

                Batches.Clear();
                foreach (var b in _allBatches) Batches.Add(b);

                AuditTrail.Clear();
                _rawAudit     = new List<VendorBatchAuditTrailDto>();
                SelectedBatch = null;

                TotalBatches = _allBatches.Count;
                TotalUnits   = _allBatches.Sum(r => r.TotalQty);

                SummaryText = _allBatches.Count == 0
                    ? "No active outbound batches."
                    : $"{_allBatches.Count} active outbound batch(es)  •  {TotalUnits} total unit(s)";
            }
            catch (Exception ex)
            {
                Logger.LogError("OutboundBatchesViewModel.LoadAsync failed", ex);
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task FinalizeAsync(OutboundBatchDto batch)
        {
            if (batch == null) return;
            int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
            IsLoading = true;
            try
            {
                await _service.FinalizeOutboundBatchAsync(batch.BatchId, userId);
                await LoadAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task DeleteAsync(OutboundBatchDto batch)
        {
            if (batch == null) return;
            int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
            IsLoading = true;
            try
            {
                await _service.DeleteOutboundBatchAsync(batch.BatchId, userId);
                await LoadAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAuditTrailAsync(int batchId)
        {
            try
            {
                var raw = await _service.GetBatchAuditTrailAsync(batchId);
                _rawAudit = raw
                    .OrderByDescending(x => x.RequestDate)
                    .ThenByDescending(x => x.EmptyCartridgeId)
                    .ToList();

                AuditTrail.Clear();
                foreach (var r in _rawAudit) AuditTrail.Add(r);
            }
            catch (Exception ex)
            {
                Logger.LogError("OutboundBatchesViewModel.LoadAuditTrailAsync failed", ex);
                throw;
            }
        }
    }
}
