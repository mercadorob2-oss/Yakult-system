using System;
using System.Windows.Input;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalDetail.ViewModels
{
    /// <summary>
    /// One Sub-Type Group financial card on RenewalDetailWindow's Invoice Items tab — same
    /// Subtotal/VAT%/WHT%/Discount%/Total calculation chain as ViewInvoiceDetailPage's group
    /// cards (TryCalculateFinancialsFromPercentages applied per-group), with its own Save
    /// button persisting the SubtotalOverride/VatPercent/WhtPercent/DiscountPercent columns.
    /// </summary>
    public class SubTypeGroupCardViewModel : ViewModelBase
    {
        private readonly RenewalRepository _repository = new RenewalRepository();

        public int GroupId { get; }
        public string SubType { get; }
        public string ReferenceCode { get; }
        public DateTime? BeginDate { get; }
        public DateTime? EndDate { get; }
        public int ItemCount { get; }

        public string ReferenceCodeLabel => ItemSubTypeCatalog.GetReferenceCodeLabel(SubType);
        public string ReferenceCodeDisplay => string.IsNullOrWhiteSpace(ReferenceCode) ? "(none)" : ReferenceCode;
        public string BeginDateDisplay => BeginDate?.ToString("yyyy-MM-dd") ?? "Not set";
        public string EndDateDisplay => EndDate?.ToString("yyyy-MM-dd") ?? "Not set";

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

        public ICommand SaveCommand { get; }

        public event Action<string> SaveSucceeded;
        public event Action<string> SaveFailed;

        public SubTypeGroupCardViewModel(SetItemSubTypeGroupSummaryDto dto)
        {
            GroupId = dto.GroupId;
            SubType = dto.SubType;
            ReferenceCode = dto.ReferenceCode;
            BeginDate = dto.BeginDate;
            EndDate = dto.EndDate;
            ItemCount = dto.ItemCount;

            _subtotal = dto.SubtotalOverride ?? dto.Subtotal;
            _vatPercent = dto.VatPercent ?? 0m;
            _whtPercent = dto.WhtPercent ?? 0m;
            _discountPercent = dto.DiscountPercent ?? 0m;

            SaveCommand = new RelayCommand(Save);
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
        }

        private void Save()
        {
            try
            {
                _repository.SaveSubTypeGroupFinancials(GroupId, Subtotal, VatPercent, WhtPercent, DiscountPercent, AppSession.CurrentUserId);
                SaveSucceeded?.Invoke($"{SubType} #{ReferenceCodeDisplay} saved.");
            }
            catch (Exception ex)
            {
                SaveFailed?.Invoke(ex.Message);
            }
        }
    }
}
