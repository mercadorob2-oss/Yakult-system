using System;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.RequestManagement.ViewModels
{
    public class FulfillRequestRowStateViewModel : ViewModelBase
    {
        private int    _issueQty;
        private int    _issueBrandNewQty;
        private int    _issueRefilledQty;
        private string _remarks = string.Empty;

        public RequestDto Dto             { get; }
        public int        AvailableStock  { get; }
        public int        MaxIssuable     { get; }
        public int        PendingQty      { get; }

        /// <summary>
        /// Set for a cartridge line of a mixed portal submission. Such a line is issued like the
        /// Cartridge Exchange: Brand New / Refilled quantities, with the requester's empties
        /// returned (RequestRepository.FulfillCartridgeLine).
        /// </summary>
        public MixedCartridgeLineInfo Cartridge { get; }
        public bool IsExchangeLine => Cartridge?.IsExchangeLine == true;
        public bool IsPlainLine    => !IsExchangeLine;

        public FulfillRequestRowStateViewModel(RequestDto dto, int availableStock, MixedCartridgeLineInfo cartridge = null)
        {
            Dto            = dto ?? throw new ArgumentNullException(nameof(dto));
            Cartridge      = cartridge;
            PendingQty     = Math.Max(0, dto.Quantity - dto.IssuedQty);
            AvailableStock = IsExchangeLine
                ? (cartridge.CartridgeModelId.HasValue ? cartridge.AvailableBrandNew + cartridge.AvailableRefilled : 0)
                : availableStock;
            MaxIssuable    = Math.Min(AvailableStock, PendingQty);

            IncreaseQtyCommand = new RelayCommand(
                () => IssueQty++,
                () => IsPlainLine && IssueQty < MaxIssuable);

            DecreaseQtyCommand = new RelayCommand(
                () => IssueQty--,
                () => IsPlainLine && IssueQty > 0);

            IncreaseBrandNewCommand = new RelayCommand(() => IssueBrandNewQty++, () => IssueBrandNewQty < MaxBrandNew);
            DecreaseBrandNewCommand = new RelayCommand(() => IssueBrandNewQty--, () => IssueBrandNewQty > 0);
            IncreaseRefilledCommand = new RelayCommand(() => IssueRefilledQty++, () => IssueRefilledQty < MaxRefilled);
            DecreaseRefilledCommand = new RelayCommand(() => IssueRefilledQty--, () => IssueRefilledQty > 0);
        }

        /// <summary>Quantity issued now. On a cartridge exchange line it is Brand New + Refilled.</summary>
        public int IssueQty
        {
            get => _issueQty;
            set
            {
                if (IsExchangeLine) return;
                value = Math.Max(0, Math.Min(value, MaxIssuable));
                if (SetField(ref _issueQty, value))
                    RefreshSummary();
            }
        }

        // ── Cartridge exchange quantities ────────────────────────────────────────
        public int AvailableBrandNew => IsExchangeLine && Cartridge.CartridgeModelId.HasValue ? Cartridge.AvailableBrandNew : 0;
        public int AvailableRefilled => IsExchangeLine && Cartridge.CartridgeModelId.HasValue ? Cartridge.AvailableRefilled : 0;

        public int MaxBrandNew => Math.Max(0, Math.Min(PendingQty - _issueRefilledQty, AvailableBrandNew));
        public int MaxRefilled => Math.Max(0, Math.Min(PendingQty - _issueBrandNewQty, AvailableRefilled));

        public int IssueBrandNewQty
        {
            get => _issueBrandNewQty;
            set
            {
                if (SetField(ref _issueBrandNewQty, Math.Max(0, Math.Min(value, MaxBrandNew))))
                    SyncExchangeTotal();
            }
        }

        public int IssueRefilledQty
        {
            get => _issueRefilledQty;
            set
            {
                if (SetField(ref _issueRefilledQty, Math.Max(0, Math.Min(value, MaxRefilled))))
                    SyncExchangeTotal();
            }
        }

        private void SyncExchangeTotal()
        {
            _issueQty = _issueBrandNewQty + _issueRefilledQty;
            OnPropertyChanged(nameof(IssueQty));
            OnPropertyChanged(nameof(MaxBrandNew));
            OnPropertyChanged(nameof(MaxRefilled));
            OnPropertyChanged(nameof(EmptiesInfo));
            RefreshSummary();
        }

        public string CartridgeStockText
        {
            get
            {
                if (!IsExchangeLine) return string.Empty;
                if (!Cartridge.CartridgeModelId.HasValue)
                    return $"Model '{Cartridge.ModelNumber}' is not in Cartridge Models; cannot issue";
                return $"{Cartridge.ModelNumber} in stock: Brand New {AvailableBrandNew}, Refilled {AvailableRefilled}";
            }
        }

        /// <summary>
        /// The empties to collect, allocated the same way
        /// CartridgeManagementRepository.IssueMixedCartridgeLine records them.
        /// </summary>
        public string EmptiesInfo
        {
            get
            {
                if (!IsExchangeLine) return string.Empty;
                var c = Cartridge;
                string declared = c.DeclaredGood + c.DeclaredDamaged == 0
                    ? "No empties declared by the requester"
                    : $"Declared empties: {c.DeclaredGood} Good, {c.DeclaredDamaged} Damaged";
                if (c.ReturnedGood + c.ReturnedDamaged > 0)
                    declared += $" (already returned: {c.ReturnedGood} Good, {c.ReturnedDamaged} Damaged)";

                if (_issueQty <= 0) return declared;

                int good    = Math.Min(_issueQty, Math.Max(0, c.DeclaredGood - c.ReturnedGood));
                int damaged = Math.Min(_issueQty - good, Math.Max(0, c.DeclaredDamaged - c.ReturnedDamaged));
                int plain   = _issueQty - good - damaged;
                string collect = $"Collect {_issueQty} empty cartridge(s): {good + plain} Good, {damaged} Damaged";
                if (plain > 0) collect += $" ({plain} not declared)";
                return declared + "\n" + collect;
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
        public RelayCommand IncreaseBrandNewCommand { get; }
        public RelayCommand DecreaseBrandNewCommand { get; }
        public RelayCommand IncreaseRefilledCommand { get; }
        public RelayCommand DecreaseRefilledCommand { get; }

        private void RefreshSummary()
        {
            OnPropertyChanged(nameof(IssuedAfter));
            OnPropertyChanged(nameof(PendingAfter));
            OnPropertyChanged(nameof(StatusAfterText));
            OnPropertyChanged(nameof(StatusAfterColor));
        }
    }
}
