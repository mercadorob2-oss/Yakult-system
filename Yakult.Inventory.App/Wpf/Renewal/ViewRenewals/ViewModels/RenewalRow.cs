using System;
using System.Collections.Generic;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Renewal.ViewRenewals.ViewModels
{
    /// <summary>
    /// Presentation-layer row wrapper around Models.RenewalDto for the Renewals grid. Wraps
    /// (rather than reuses) RenewalDto directly so the checkbox column can raise PropertyChanged
    /// on Selected — required for the cross-page "select all" toggle to visually update
    /// already-rendered rows, since RenewalDto itself is a plain POCO.
    /// </summary>
    public sealed class RenewalRow : ViewModelBase
    {
        public RenewalDto Dto { get; }

        /// <summary>Distinct item categories (e.g. Ink, Toner, Cartridge) found among this
        /// renewal's underlying set items — used by the Category multi-select filter.</summary>
        public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();

        /// <summary>Distinct item types (Hardware / Software License / Services) found among
        /// this renewal's underlying set items — used by the Item Type multi-select filter.</summary>
        public IReadOnlyList<string> ItemTypes { get; set; } = Array.Empty<string>();

        /// <summary>Distinct Sub-Types (Contract/Subscription/License/Services) found among this
        /// renewal's dbo.SetItem rows — used by the Sub-Type filter.</summary>
        public IReadOnlyList<string> SubTypes { get; set; } = Array.Empty<string>();

        /// <summary>True when at least one item on this renewal has no Sub-Type at all —
        /// including sets with no dbo.SetItem rows whatsoever. Backs the "Non-Subtype" option
        /// in the Sub-Type filter.</summary>
        public bool HasNonSubTypeItems { get; set; } = true;

        /// <summary>Distinct Sub-Type Group Reference Codes (Contract Code/License ID/etc.,
        /// nullable) found among this renewal's dbo.SetItem rows — searchable via SearchText.</summary>
        public IReadOnlyList<string> SubTypeReferenceCodes { get; set; } = Array.Empty<string>();

        public RenewalRow(RenewalDto dto)
        {
            Dto = dto;
            _selected = dto.Selected;
        }

        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set
            {
                if (SetField(ref _selected, value))
                    Dto.Selected = value;
            }
        }

        public int SetId => Dto.SetId;
        public string SetCode => Dto.SetCode;
        public int? RenewalOfSetId => Dto.RenewalOfSetId;
        public string SetType => Dto.SetType;
        public string DocumentNumber => Dto.DocumentNumber;
        public DateTime? DocumentDate => Dto.DocumentDate;
        public string CompanyName => Dto.CompanyName;
        public string SiteDisplay => Dto.SiteDisplay;
        public DateTime? StartDate => Dto.StartDate;
        public DateTime? EndDate => Dto.EndDate;
        public int? DaysUntilExpiry => Dto.DaysUntilExpiry;
        public string ExpiryStatus => Dto.ExpiryStatus;
        public string SetLevelStatus => Dto.SetLevelStatus;
        public int ItemCount => Dto.ItemCount;
        public decimal TotalAmountDue => Dto.TotalAmountDue;
        public DateTime? RenewedDate => Dto.RenewedDate;
        public string PartNumber => Dto.PartNumber;
        public DateTime? CreatedDate => Dto.CreatedDate;
        public bool Active => Dto.Active;

        /// <summary>"N (Xa Xr)" when the set has renewed/expired items, else just "N" — conveys
        /// partial-renewal state exactly like the original DgvRenewals_CellFormatting.</summary>
        public string ItemCountDisplay
        {
            get
            {
                if (ItemCount <= 0) return ItemCount.ToString();
                bool hasRenewed = Dto.RenewedItemsCount > 0;
                bool hasExpired = Dto.ExpiredItemsCount > 0;
                return (hasRenewed || hasExpired)
                    ? $"{ItemCount} ({Dto.TotalActiveItems}a {Dto.RenewedItemsCount}r)"
                    : ItemCount.ToString();
            }
        }

        public string FinancialTooltip =>
            "Financial Breakdown\n" +
            "────────────────────\n" +
            $"Subtotal:   ₱{Dto.Subtotal:N2}\n" +
            $"VAT (12%):  ₱{Dto.VatAmount:N2}\n" +
            $"Discount:   ₱{Dto.DiscountAmount:N2}\n" +
            $"WHT (2%):   ₱{Dto.WhtAmount:N2}\n" +
            "────────────────────\n" +
            $"Total:      ₱{Dto.TotalAmountDue:N2}";
    }
}
