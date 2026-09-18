using System;
using Yakult.Inventory.App.Forms.CartridgeManagement;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    /// <summary>
    /// Per-model fulfillment row inside a multi-model session.
    /// Mirrors MultiModelRowState but as a proper observable ViewModel.
    /// </summary>
    public class MultiModelRowViewModel : ViewModelBase
    {
        private int _issuedBrandNewQty;
        private int _issuedRefilledQty;

        public int    ReqId            { get; }
        public string DisplayModel     { get; }
        public int    RequestedQty     { get; }
        public int    AvailableBrandNew { get; }
        public int    AvailableRefilled { get; }

        public int IssuedBrandNewQty
        {
            get => _issuedBrandNewQty;
            set
            {
                int clamped = Math.Max(0, Math.Min(value, MaxBrandNew));
                if (SetField(ref _issuedBrandNewQty, clamped))
                {
                    OnPropertyChanged(nameof(MaxRefilled));
                    OnPropertyChanged(nameof(IssuedFullQty));
                    OnPropertyChanged(nameof(UnfulfilledQty));
                    OnPropertyChanged(nameof(FulfillmentStatus));
                    OnPropertyChanged(nameof(FulfillmentStatusColor));
                    OnPropertyChanged(nameof(UnfulfilledColor));
                    OnPropertyChanged(nameof(Remarks));
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
                    OnPropertyChanged(nameof(IssuedFullQty));
                    OnPropertyChanged(nameof(UnfulfilledQty));
                    OnPropertyChanged(nameof(FulfillmentStatus));
                    OnPropertyChanged(nameof(FulfillmentStatusColor));
                    OnPropertyChanged(nameof(UnfulfilledColor));
                    OnPropertyChanged(nameof(Remarks));
                }
            }
        }

        public int IssuedFullQty  => _issuedBrandNewQty + _issuedRefilledQty;
        public int UnfulfilledQty => RequestedQty - IssuedFullQty;

        // Dynamic maxima enforce sum ≤ RequestedQty
        public int MaxBrandNew => Math.Min(RequestedQty - _issuedRefilledQty, AvailableBrandNew);
        public int MaxRefilled => Math.Min(RequestedQty - _issuedBrandNewQty, AvailableRefilled);

        public string StockInfo
            => $"Stock: Brand New={AvailableBrandNew}, Refilled={AvailableRefilled}   (Requested: {RequestedQty})";

        public string FulfillmentStatus
        {
            get
            {
                if (IssuedFullQty == 0)            return "Unfulfilled";
                if (IssuedFullQty < RequestedQty)  return "Partially Fulfilled";
                return "Fulfilled";
            }
        }

        public string FulfillmentStatusColor
        {
            get
            {
                if (IssuedFullQty == 0)            return "#E74C3C";
                if (IssuedFullQty < RequestedQty)  return "#E67E22";
                return "#27AE60";
            }
        }

        public string UnfulfilledColor
            => UnfulfilledQty < 0 ? "#E74C3C"
             : UnfulfilledQty > 0 ? "#E67E22"
             : "#27AE60";

        public string UnfulfilledDisplay
            => UnfulfilledQty < 0 ? $"{UnfulfilledQty} ⚠ INVALID" : UnfulfilledQty.ToString();

        public string Remarks
            => CartridgeExchangeRemarks.GenerateRemarks(IssuedFullQty, RequestedQty, DisplayModel);

        public MultiModelRowViewModel(CartridgeRequestDto req, int availBrandNew, int availRefilled)
        {
            ReqId             = req.ReqId;
            DisplayModel      = req.TypedModelNumber ?? req.ModelNumber ?? "Unknown Model";
            RequestedQty      = req.Quantity;
            AvailableBrandNew = availBrandNew;
            AvailableRefilled = availRefilled;
        }
    }
}
