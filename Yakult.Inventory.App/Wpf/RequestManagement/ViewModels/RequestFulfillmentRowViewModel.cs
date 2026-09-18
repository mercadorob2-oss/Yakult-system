using System;
using System.Windows;
using System.Windows.Media;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.RequestManagement.ViewModels
{
    public class RequestFulfillmentRowViewModel : ViewModelBase
    {
        public RequestDto Dto { get; }

        public RequestFulfillmentRowViewModel(RequestDto dto)
        {
            Dto = dto ?? throw new ArgumentNullException(nameof(dto));
        }

        public int    ReqId               => Dto.ReqId;
        public string ItemName            => Dto.ItemName;
        public int    Quantity            => Dto.Quantity;
        public int    IssuedQty           => Dto.IssuedQty;
        public int    PendingQty          => Math.Max(0, Dto.Quantity - Dto.IssuedQty);
        public string Remarks             => Dto.Remarks;
        public DateTime CreatedDate       => Dto.DateCreated;
        public string CreatedDateFormatted => Dto.DateCreated.ToString("MM/dd/yyyy");

        public bool IsPending => PendingQty > 0;

        public string FulfillmentStatusDisplay
        {
            get
            {
                if (Dto.IssuedQty >= Dto.Quantity) return "Fulfilled";
                if (Dto.IssuedQty > 0) return "Partially Fulfilled";
                return "Unfulfilled";
            }
        }

        public SolidColorBrush StatusColor
        {
            get
            {
                if (Dto.IssuedQty >= Dto.Quantity)
                    return new SolidColorBrush(Color.FromRgb(39, 174, 96));
                if (Dto.IssuedQty > 0)
                    return new SolidColorBrush(Color.FromRgb(230, 126, 34));
                return new SolidColorBrush(Color.FromRgb(192, 57, 43));
            }
        }

        // For the Unfulfilled page: Status is only "Unfulfilled" (red) or "Fulfilled" (green)
        public SolidColorBrush SimpleStatusColor
            => Dto.IssuedQty >= Dto.Quantity
                ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
                : new SolidColorBrush(Color.FromRgb(192, 57, 43));

        public SolidColorBrush PendingQtyColor
            => PendingQty > 0
                ? new SolidColorBrush(Color.FromRgb(231, 76, 60))
                : new SolidColorBrush(Color.FromRgb(42, 58, 74));

        public SolidColorBrush IssuedQtyColor
            => Dto.IssuedQty > 0
                ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
                : new SolidColorBrush(Color.FromRgb(42, 58, 74));

        public FontWeight PendingQtyWeight
            => PendingQty > 0 ? FontWeights.Bold : FontWeights.Normal;

        public FontWeight IssuedQtyWeight
            => Dto.IssuedQty > 0 ? FontWeights.SemiBold : FontWeights.Normal;
    }
}
