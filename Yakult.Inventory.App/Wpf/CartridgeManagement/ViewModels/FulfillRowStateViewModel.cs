using System;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class FulfillRowStateViewModel : ViewModelBase
    {
        private int    _brandNewQty;
        private int    _refilledQty;
        private string _remarks = string.Empty;

        public UnfulfilledCartridgeExchangeDto Dto           { get; }
        public int                             AvailBrandNew { get; }
        public int                             AvailRefilled { get; }
        public int                             MaxBrandNew   { get; }
        public int                             MaxRefilled   { get; }

        public FulfillRowStateViewModel(
            UnfulfilledCartridgeExchangeDto dto,
            int availBrandNew,
            int availRefilled)
        {
            Dto           = dto ?? throw new ArgumentNullException(nameof(dto));
            AvailBrandNew = availBrandNew;
            AvailRefilled = availRefilled;
            MaxBrandNew   = Math.Min(availBrandNew, dto.UnfulfilledQty);
            MaxRefilled   = Math.Min(availRefilled,  dto.UnfulfilledQty);

            IncreaseBrandNewCommand = new RelayCommand(
                () => BrandNewQty++,
                () => BrandNewQty < MaxBrandNew && (BrandNewQty + RefilledQty) < Dto.UnfulfilledQty);

            DecreaseBrandNewCommand = new RelayCommand(
                () => BrandNewQty--,
                () => BrandNewQty > 0);

            IncreaseRefilledCommand = new RelayCommand(
                () => RefilledQty++,
                () => RefilledQty < MaxRefilled && (BrandNewQty + RefilledQty) < Dto.UnfulfilledQty);

            DecreaseRefilledCommand = new RelayCommand(
                () => RefilledQty--,
                () => RefilledQty > 0);
        }

        public int BrandNewQty
        {
            get => _brandNewQty;
            set
            {
                int cap = Math.Max(0, Dto.UnfulfilledQty - RefilledQty);
                value   = Math.Max(0, Math.Min(value, Math.Min(cap, MaxBrandNew)));
                if (SetField(ref _brandNewQty, value))
                    RefreshSummary();
            }
        }

        public int RefilledQty
        {
            get => _refilledQty;
            set
            {
                int cap = Math.Max(0, Dto.UnfulfilledQty - BrandNewQty);
                value   = Math.Max(0, Math.Min(value, Math.Min(cap, MaxRefilled)));
                if (SetField(ref _refilledQty, value))
                    RefreshSummary();
            }
        }

        public string Remarks
        {
            get => _remarks;
            set => SetField(ref _remarks, value);
        }

        public int    TotalToIssue   => BrandNewQty + RefilledQty;
        public int    IssuedAfter    => Dto.IssuedFullQty + TotalToIssue;
        public int    PendingAfter   => Math.Max(0, Dto.UnfulfilledQty - TotalToIssue);

        public string StatusAfterText
        {
            get
            {
                if (PendingAfter <= 0) return "Fulfilled";
                if (TotalToIssue > 0) return "Partially Fulfilled";
                return "Pending";
            }
        }

        public string StatusAfterColor
        {
            get
            {
                switch (StatusAfterText)
                {
                    case "Fulfilled":          return "#158063";
                    case "Partially Fulfilled": return "#B46400";
                    default:                   return "#B91C1C";
                }
            }
        }

        public string AvailBrandNewText  => $"({AvailBrandNew} available)";
        public string AvailRefilledText  => $"({AvailRefilled} available)";
        public string AvailBrandNewColor => AvailBrandNew > 0 ? "#15803D" : "#B43C3C";
        public string AvailRefilledColor => AvailRefilled > 0 ? "#15803D" : "#B43C3C";

        public string CartridgeModel     => Dto.CartridgeModel ?? "—";
        public string SubHeaderText      =>
            $"Req #{Dto.ReqId}  ·  Returned: {Dto.ReturnedEmptyQty}  ·  Previously Issued: {Dto.IssuedFullQty}  ·  Still Pending: {Dto.UnfulfilledQty}";

        public RelayCommand IncreaseBrandNewCommand { get; }
        public RelayCommand DecreaseBrandNewCommand { get; }
        public RelayCommand IncreaseRefilledCommand { get; }
        public RelayCommand DecreaseRefilledCommand { get; }

        private void RefreshSummary()
        {
            OnPropertyChanged(nameof(TotalToIssue));
            OnPropertyChanged(nameof(IssuedAfter));
            OnPropertyChanged(nameof(PendingAfter));
            OnPropertyChanged(nameof(StatusAfterText));
            OnPropertyChanged(nameof(StatusAfterColor));
        }
    }
}
