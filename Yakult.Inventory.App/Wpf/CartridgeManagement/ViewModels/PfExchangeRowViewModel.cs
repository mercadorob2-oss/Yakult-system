using System;
using System.Windows;
using System.Windows.Media;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class PfExchangeRowViewModel : ViewModelBase
    {
        public UnfulfilledCartridgeExchangeDto Dto { get; }

        public PfExchangeRowViewModel(UnfulfilledCartridgeExchangeDto dto)
        {
            Dto = dto ?? throw new ArgumentNullException(nameof(dto));
        }

        public int    ReqId               => Dto.ReqId;
        public string CartridgeModel      => Dto.CartridgeModel;
        public int    ReturnedEmptyQty    => Dto.ReturnedEmptyQty;
        public int    IssuedFullQty       => Dto.IssuedFullQty;
        public int    UnfulfilledQty      => Dto.UnfulfilledQty;
        public string Status              => Dto.Status;
        public string FulfillmentStatusDisplay => Dto.FulfillmentStatusDisplay;
        public DateTime CreatedDate       => Dto.CreatedDate;
        public string CreatedDateFormatted => Dto.CreatedDate.ToString("MM/dd/yyyy");
        public string Remarks             => Dto.Remarks;
        public string FulfilledRemarks    => Dto.FulfilledRemarks;

        public bool IsPending => Dto.Status == "Pending" && Dto.UnfulfilledQty > 0;

        public SolidColorBrush StatusColor
        {
            get
            {
                if (Dto.Status == "Fulfilled")
                    return new SolidColorBrush(Color.FromRgb(39, 174, 96));
                if (Dto.IssuedFullQty > 0)
                    return new SolidColorBrush(Color.FromRgb(230, 126, 34));
                return new SolidColorBrush(Color.FromRgb(192, 57, 43));
            }
        }

        public SolidColorBrush PendingQtyColor
            => Dto.UnfulfilledQty > 0
                ? new SolidColorBrush(Color.FromRgb(231, 76, 60))
                : new SolidColorBrush(Color.FromRgb(42, 58, 74));

        public SolidColorBrush IssuedQtyColor
            => Dto.IssuedFullQty > 0
                ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
                : new SolidColorBrush(Color.FromRgb(42, 58, 74));

        public FontWeight PendingQtyWeight
            => Dto.UnfulfilledQty > 0 ? FontWeights.Bold : FontWeights.Normal;

        public FontWeight IssuedQtyWeight
            => Dto.IssuedFullQty > 0 ? FontWeights.SemiBold : FontWeights.Normal;

        // For the Unfulfilled page: Status is only "Pending" (orange) or "Fulfilled" (green)
        public SolidColorBrush SimpleStatusColor
            => Dto.Status == "Fulfilled"
                ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
                : new SolidColorBrush(Color.FromRgb(230, 126, 34));
    }
}
