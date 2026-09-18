using System;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.RequestManagement.ViewModels
{
    public class FulfillRequestRowStateViewModel : ViewModelBase
    {
        private int    _issueQty;
        private string _remarks = string.Empty;

        public RequestDto Dto             { get; }
        public int        AvailableStock  { get; }
        public int        MaxIssuable     { get; }
        public int        PendingQty      { get; }

        public FulfillRequestRowStateViewModel(RequestDto dto, int availableStock)
        {
            Dto            = dto ?? throw new ArgumentNullException(nameof(dto));
            AvailableStock = availableStock;
            PendingQty     = Math.Max(0, dto.Quantity - dto.IssuedQty);
            MaxIssuable    = Math.Min(availableStock, PendingQty);

            IncreaseQtyCommand = new RelayCommand(
                () => IssueQty++,
                () => IssueQty < MaxIssuable);

            DecreaseQtyCommand = new RelayCommand(
                () => IssueQty--,
                () => IssueQty > 0);
        }

        public int IssueQty
        {
            get => _issueQty;
            set
            {
                value = Math.Max(0, Math.Min(value, MaxIssuable));
                if (SetField(ref _issueQty, value))
                    RefreshSummary();
            }
        }

        public string Remarks
        {
            get => _remarks;
            set => SetField(ref _remarks, value);
        }

        public int IssuedAfter  => Dto.IssuedQty + IssueQty;
        public int PendingAfter => Math.Max(0, PendingQty - IssueQty);

        public string StatusAfterText
        {
            get
            {
                if (PendingAfter <= 0) return "Fulfilled";
                if (IssueQty > 0) return "Partially Fulfilled";
                return "Unfulfilled";
            }
        }

        public string StatusAfterColor
        {
            get
            {
                switch (StatusAfterText)
                {
                    case "Fulfilled":           return "#158063";
                    case "Partially Fulfilled":  return "#B46400";
                    default:                    return "#B91C1C";
                }
            }
        }

        public string AvailableStockText  => $"({AvailableStock} available)";
        public string AvailableStockColor => AvailableStock > 0 ? "#15803D" : "#B43C3C";

        public string ItemName      => Dto.ItemName ?? "—";
        public string SubHeaderText =>
            $"Req #{Dto.ReqId}  ·  Requested: {Dto.Quantity}  ·  Previously Issued: {Dto.IssuedQty}  ·  Still Pending: {PendingQty}";

        public RelayCommand IncreaseQtyCommand { get; }
        public RelayCommand DecreaseQtyCommand { get; }

        private void RefreshSummary()
        {
            OnPropertyChanged(nameof(IssuedAfter));
            OnPropertyChanged(nameof(PendingAfter));
            OnPropertyChanged(nameof(StatusAfterText));
            OnPropertyChanged(nameof(StatusAfterColor));
        }
    }
}
