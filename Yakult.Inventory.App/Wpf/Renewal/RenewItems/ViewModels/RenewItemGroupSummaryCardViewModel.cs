using System;
using System.Windows.Input;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Renewal.RenewItems.ViewModels
{
    /// <summary>
    /// One editable Sub-Type Group financial card in the "Renew Items" window — same
    /// Subtotal/VAT%/WHT%/Discount%/Total calculation chain as ViewInvoiceDetailPage's group
    /// cards, but purely in-memory: there is no dbo.SetItemSubTypeGroup row to save against yet
    /// (the new renewal Set doesn't exist until Confirm), so this only feeds the overall
    /// Financial Calculator's Subtotal (see RenewItemsViewModel.UpdateSubtotalFromGroups).
    /// OriginalSubtotal is the read-only live sum of member rows' Amount (kept current across
    /// rebuilds); Subtotal starts equal to it but is freely user-editable — e.g. to set a new
    /// renewal price for the group — and, once edited, is preserved across rebuilds triggered by
    /// unrelated row edits until reverted. VAT%/WHT%/Discount% are likewise freely editable and
    /// preserved.
    /// </summary>
    public class RenewItemGroupSummaryCardViewModel : ViewModelBase
    {
        public string SubType { get; }
        public string ReferenceCode { get; }
        public DateTime? BeginDate { get; }
        public DateTime? EndDate { get; }

        public string ReferenceCodeLabel => ItemSubTypeCatalog.GetReferenceCodeLabel(SubType);
        public string ReferenceCodeDisplay => string.IsNullOrWhiteSpace(ReferenceCode) ? "(none)" : ReferenceCode;
        public string BeginDateDisplay => BeginDate?.ToString("yyyy-MM-dd") ?? "Not set";
        public string EndDateDisplay => EndDate?.ToString("yyyy-MM-dd") ?? "Not set";

        /// <summary>Read-only — the live sum of this group's member rows' Amount, kept current by
        /// RenewItemsViewModel.RefreshGroupSummaryCards regardless of what the user has typed
        /// into Subtotal below.</summary>
        private decimal _originalSubtotal;
        public decimal OriginalSubtotal
        {
            get => _originalSubtotal;
            set => SetField(ref _originalSubtotal, value);
        }

        private decimal _subtotal;
        public decimal Subtotal
        {
            get => _subtotal;
            set { if (SetField(ref _subtotal, value)) Recalculate(); }
        }

        private decimal _vatPercent;
        public decimal VatPercent
        {
            get => _vatPercent;
            set { if (SetField(ref _vatPercent, value)) Recalculate(); }
        }

        private decimal _whtPercent;
        public decimal WhtPercent
        {
            get => _whtPercent;
            set { if (SetField(ref _whtPercent, value)) Recalculate(); }
        }

        private decimal _discountPercent;
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set { if (SetField(ref _discountPercent, value)) Recalculate(); }
        }

        private decimal _total;
        public decimal Total { get => _total; private set => SetField(ref _total, value); }

        /// <summary>Resets Subtotal back to OriginalSubtotal — undoes a typed-in new price.</summary>
        public ICommand RevertSubtotalCommand { get; }

        /// <summary>Fires whenever Total changes, so the owning ViewModel can refresh the
        /// overall Financial Calculator's Subtotal.</summary>
        public event Action TotalChanged;

        public RenewItemGroupSummaryCardViewModel(
            string subType, string referenceCode, DateTime? beginDate, DateTime? endDate,
            decimal originalSubtotal, decimal vatPercent, decimal whtPercent, decimal discountPercent)
        {
            SubType = subType;
            ReferenceCode = referenceCode;
            BeginDate = beginDate;
            EndDate = endDate;
            _originalSubtotal = originalSubtotal;
            _subtotal = originalSubtotal;
            _vatPercent = vatPercent;
            _whtPercent = whtPercent;
            _discountPercent = discountPercent;
            RevertSubtotalCommand = new RelayCommand(() => Subtotal = OriginalSubtotal);
            Recalculate();
        }

        // Same chain as ViewInvoiceDetailPage.RecalculateGroupCardTotal: Subtotal is net-of-VAT;
        // Discount is a percentage of Subtotal taken off first; VAT and WHT are then both
        // computed on the post-discount base; VAT is added back in and WHT subtracted.
        private void Recalculate()
        {
            decimal discount = Subtotal * (DiscountPercent / 100m);
            decimal netAfterDiscount = Subtotal - discount;
            decimal vat = netAfterDiscount * (VatPercent / 100m);
            decimal wht = netAfterDiscount * (WhtPercent / 100m);
            Total = netAfterDiscount + vat - wht;
            TotalChanged?.Invoke();
        }
    }
}
