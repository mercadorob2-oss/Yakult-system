using System;
using System.Collections.ObjectModel;
using System.Linq;
using Yakult.Inventory.App.Forms.CartridgeManagement;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    /// <summary>
    /// State for the right-hand Cartridge Exchange panel.
    /// Covers both single-model and multi-model fulfillment modes.
    /// </summary>
    public class ExchangePanelViewModel : ViewModelBase
    {
        // ── Request summary ───────────────────────────────────────────────────────
        private string _reqIds                = "";
        private string _requester             = "";
        private string _model                 = "";
        private string _quantity              = "";
        private string _withCartridge         = "";
        private string _company               = "";
        private string _branch                = "";
        private string _department            = "";
        private string _goodEmptyQty          = "";
        private string _damagedEmptyQty       = "";
        private string _distributionMethod    = "";
        private string _receivedBy            = "";
        private string _additionalRemarks     = "";

        public string ReqIds             { get => _reqIds;             set => SetField(ref _reqIds,             value); }
        public string Requester          { get => _requester;          set => SetField(ref _requester,          value); }
        public string Model              { get => _model;              set => SetField(ref _model,              value); }
        public string Quantity           { get => _quantity;           set => SetField(ref _quantity,           value); }
        public string WithCartridge      { get => _withCartridge;      set => SetField(ref _withCartridge,      value); }
        public string Company            { get => _company;            set => SetField(ref _company,            value); }
        public string Branch             { get => _branch;             set => SetField(ref _branch,             value); }
        public string Department         { get => _department;         set => SetField(ref _department,         value); }
        public string GoodEmptyQty       { get => _goodEmptyQty;       set => SetField(ref _goodEmptyQty,       value); }
        public string DamagedEmptyQty    { get => _damagedEmptyQty;    set => SetField(ref _damagedEmptyQty,    value); }
        public string DistributionMethod { get => _distributionMethod; set => SetField(ref _distributionMethod, value); }
        public string ReceivedBy         { get => _receivedBy;         set => SetField(ref _receivedBy,         value); }
        public string AdditionalRemarks  { get => _additionalRemarks;  set => SetField(ref _additionalRemarks,  value); }

        // ── Mode ──────────────────────────────────────────────────────────────────
        private bool _isMultiModelMode;
        public bool IsMultiModelMode
        {
            get => _isMultiModelMode;
            set
            {
                if (SetField(ref _isMultiModelMode, value))
                {
                    OnPropertyChanged(nameof(IsSingleModelMode));
                }
            }
        }
        public bool IsSingleModelMode => !_isMultiModelMode;

        // ── Single-model issued quantities ────────────────────────────────────────
        private int _issuedBrandNewQty;
        private int _issuedRefilledQty;
        private int _availableBrandNewStock;
        private int _availableRefilledStock;
        private int _requestedQty;
        private string _cartridgeModelForRemarks = "";

        public int RequestedQty
        {
            get => _requestedQty;
            set { if (SetField(ref _requestedQty, value)) RefreshDerived(); }
        }

        public int AvailableBrandNewStock
        {
            get => _availableBrandNewStock;
            set
            {
                if (SetField(ref _availableBrandNewStock, value))
                {
                    OnPropertyChanged(nameof(StockInfoText));
                    OnPropertyChanged(nameof(MaxBrandNew));
                }
            }
        }

        public int AvailableRefilledStock
        {
            get => _availableRefilledStock;
            set
            {
                if (SetField(ref _availableRefilledStock, value))
                {
                    OnPropertyChanged(nameof(StockInfoText));
                    OnPropertyChanged(nameof(MaxRefilled));
                }
            }
        }

        public int IssuedBrandNewQty
        {
            get => _issuedBrandNewQty;
            set
            {
                int clamped = Math.Max(0, Math.Min(value, MaxBrandNew));
                if (SetField(ref _issuedBrandNewQty, clamped))
                {
                    OnPropertyChanged(nameof(MaxRefilled));
                    RefreshDerived();
                }
            }
        }

        public int IssuedRefilledQty
        {
            get => _issuedRefilledQty;
            set
            {
                int clamped = Math.Max(0, Math.Min(value, MaxRefilled));
                if (SetField(ref _issuedRefilledQty, clamped))
                {
                    OnPropertyChanged(nameof(MaxBrandNew));
                    RefreshDerived();
                }
            }
        }

        // Dynamic max per field = min(requestedQty - otherField, availableStock)
        public int MaxBrandNew => Math.Max(0, Math.Min(_requestedQty - _issuedRefilledQty, _availableBrandNewStock));
        public int MaxRefilled => Math.Max(0, Math.Min(_requestedQty - _issuedBrandNewQty, _availableRefilledStock));

        public string StockInfoText
            => $"Stock: Brand New={_availableBrandNewStock}, Refilled={_availableRefilledStock}   (Requested: {_requestedQty})";

        // ── Derived quantities (auto-updated) ─────────────────────────────────────
        private int    _issuedFullQty;
        private int    _unfulfilledQty;
        private string _fulfillmentStatus       = "Unfulfilled";
        private string _fulfillmentStatusColor  = "#E74C3C";
        private string _unfulfilledColor        = "#E67E22";
        private string _unfulfilledDisplay      = "";
        private string _autoRemarks             = "";

        public int    IssuedFullQty           { get => _issuedFullQty;          private set => SetField(ref _issuedFullQty,          value); }
        public int    UnfulfilledQty          { get => _unfulfilledQty;         private set => SetField(ref _unfulfilledQty,         value); }
        public string FulfillmentStatus       { get => _fulfillmentStatus;      private set => SetField(ref _fulfillmentStatus,      value); }
        public string FulfillmentStatusColor  { get => _fulfillmentStatusColor; private set => SetField(ref _fulfillmentStatusColor, value); }
        public string UnfulfilledColor        { get => _unfulfilledColor;       private set => SetField(ref _unfulfilledColor,       value); }
        public string UnfulfilledDisplay      { get => _unfulfilledDisplay;     private set => SetField(ref _unfulfilledDisplay,     value); }
        public string AutoRemarks             { get => _autoRemarks;            private set => SetField(ref _autoRemarks,            value); }

        private void RefreshDerived()
        {
            int issued     = _issuedBrandNewQty + _issuedRefilledQty;
            int unfulfilled = _requestedQty - issued;

            IssuedFullQty  = issued;
            UnfulfilledQty = unfulfilled;

            if (issued == 0)
            {
                FulfillmentStatus      = "Unfulfilled";
                FulfillmentStatusColor = "#E74C3C";
            }
            else if (issued < _requestedQty)
            {
                FulfillmentStatus      = "Partially Fulfilled";
                FulfillmentStatusColor = "#E67E22";
            }
            else
            {
                FulfillmentStatus      = "Fulfilled";
                FulfillmentStatusColor = "#27AE60";
            }

            UnfulfilledDisplay = unfulfilled < 0 ? $"{unfulfilled} ⚠ INVALID" : unfulfilled.ToString();
            UnfulfilledColor   = unfulfilled < 0 ? "#E74C3C" : (unfulfilled > 0 ? "#E67E22" : "#27AE60");
            AutoRemarks        = CartridgeExchangeRemarks.GenerateRemarks(issued, _requestedQty, _cartridgeModelForRemarks);

            OnPropertyChanged(nameof(MaxBrandNew));
            OnPropertyChanged(nameof(MaxRefilled));
            OnPropertyChanged(nameof(StockInfoText));
        }

        // ── Multi-model rows ──────────────────────────────────────────────────────
        public ObservableCollection<MultiModelRowViewModel> MultiModelRows { get; }
            = new ObservableCollection<MultiModelRowViewModel>();

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>Populates summary fields from a single-model request.</summary>
        public void LoadSingleModel(CartridgeRequestDto req, int availBrandNew, int availRefilled)
        {
            IsMultiModelMode = false;
            MultiModelRows.Clear();

            string displayModel = req.TypedModelNumber ?? req.ModelNumber ?? "Unknown Model";

            ReqIds             = req.ReqId.ToString();
            Requester          = BuildRequesterLabel(req.EmpId, req.EmployeeName, req.CompanyName, req.BranchName, req.DepartmentName);
            Model              = displayModel;
            Quantity           = req.Quantity.ToString();
            WithCartridge      = req.ConditionType?.Contains("Without Cartridge") == true ? "No"
                               : req.ConditionType?.Contains("With Cartridge")    == true ? "Yes"
                               : req.ConditionType ?? "N/A";
            GoodEmptyQty       = req.GoodEmptyQty.ToString();
            DamagedEmptyQty    = req.DamagedEmptyQty.ToString();
            Company            = req.CompanyName        ?? "N/A";
            Branch             = req.BranchName         ?? "N/A";
            Department         = req.DepartmentName     ?? "N/A";
            DistributionMethod = req.DistributionMethod ?? "N/A";
            ReceivedBy         = req.ReceivedByName     ?? "N/A";
            AdditionalRemarks  = req.AdditionalRemarks  ?? "";

            _cartridgeModelForRemarks  = displayModel;
            _requestedQty              = req.Quantity;
            _availableBrandNewStock    = availBrandNew;
            _availableRefilledStock    = availRefilled;
            _issuedBrandNewQty         = 0;
            _issuedRefilledQty         = 0;

            // Notify all stock/qty properties
            OnPropertyChanged(nameof(AvailableBrandNewStock));
            OnPropertyChanged(nameof(AvailableRefilledStock));
            OnPropertyChanged(nameof(IssuedBrandNewQty));
            OnPropertyChanged(nameof(IssuedRefilledQty));
            OnPropertyChanged(nameof(MaxBrandNew));
            OnPropertyChanged(nameof(MaxRefilled));
            OnPropertyChanged(nameof(StockInfoText));
            RefreshDerived();
        }

        /// <summary>Populates summary fields and row VMs from a multi-model session.</summary>
        public void LoadMultiModel(RequestSessionGroup session)
        {
            IsMultiModelMode = true;
            MultiModelRows.Clear();

            var first = session.Requests?[0];
            if (first == null) return;

            var reqIds = string.Join(", ", session.Requests.ConvertAll(r => r.ReqId.ToString()));

            ReqIds             = reqIds;
            Requester          = BuildRequesterLabel(first.EmpId, first.EmployeeName, first.CompanyName, first.BranchName, first.DepartmentName);
            Model              = $"Multi-Model ({session.Requests.Count} models)";
            Quantity           = session.TotalQuantity.ToString();
            WithCartridge      = "Yes";
            GoodEmptyQty       = session.Requests.Sum(r => r.GoodEmptyQty).ToString();
            DamagedEmptyQty    = session.Requests.Sum(r => r.DamagedEmptyQty).ToString();
            Company            = first.CompanyName        ?? "N/A";
            Branch             = first.BranchName         ?? "N/A";
            Department         = first.DepartmentName     ?? "N/A";
            DistributionMethod = first.DistributionMethod ?? "N/A";
            ReceivedBy         = first.ReceivedByName     ?? "N/A";
            AdditionalRemarks  = first.AdditionalRemarks  ?? "";
        }

        /// <summary>Adds a loaded stock row for the multi-model panel.</summary>
        private static string BuildRequesterLabel(int empId, string employeeName, string companyName, string branchName, string deptName)
        {
            if (empId > 0)
                return employeeName ?? "N/A";
            var parts = new[] { companyName, branchName, deptName }
                .Where(s => !string.IsNullOrWhiteSpace(s));
            string org = string.Join(" → ", parts);
            return string.IsNullOrWhiteSpace(org) ? "Dept Level" : org;
        }

        public void AddMultiModelRow(MultiModelRowViewModel row)
            => MultiModelRows.Add(row);

        /// <summary>Resets all fields (called on Clear).</summary>
        public void Clear()
        {
            IsMultiModelMode = false;
            MultiModelRows.Clear();

            ReqIds             = "";
            Requester          = "";
            Model              = "";
            Quantity           = "";
            WithCartridge      = "";
            Company            = "";
            Branch             = "";
            Department         = "";
            GoodEmptyQty       = "";
            DamagedEmptyQty    = "";
            DistributionMethod = "";
            ReceivedBy         = "";
            AdditionalRemarks  = "";

            _requestedQty           = 0;
            _availableBrandNewStock = 0;
            _availableRefilledStock = 0;
            _issuedBrandNewQty      = 0;
            _issuedRefilledQty      = 0;
            _cartridgeModelForRemarks = "";

            OnPropertyChanged(nameof(AvailableBrandNewStock));
            OnPropertyChanged(nameof(AvailableRefilledStock));
            OnPropertyChanged(nameof(IssuedBrandNewQty));
            OnPropertyChanged(nameof(IssuedRefilledQty));
            OnPropertyChanged(nameof(MaxBrandNew));
            OnPropertyChanged(nameof(MaxRefilled));
            OnPropertyChanged(nameof(StockInfoText));
            RefreshDerived();
        }
    }
}
