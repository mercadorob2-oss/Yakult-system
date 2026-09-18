using System;
using System.Collections.Generic;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Invoice.ViewModels
{
    /// <summary>
    /// Presentation-layer row wrapper around Pages.SetDto for the Invoice Reports grid.
    /// Wraps (rather than reuses) SetDto directly so the checkbox column can raise
    /// PropertyChanged on Selected — required for the header "select all" toggle to
    /// visually update already-rendered rows, since SetDto itself is a plain POCO.
    /// </summary>
    public sealed class InvoiceRow : ViewModelBase
    {
        public int SetId { get; set; }

        /// <summary>Distinct item categories (e.g. Ink, Toner, Cartridge) found among this
        /// invoice's underlying set items — used by the Category multi-select filter.</summary>
        public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();

        /// <summary>Distinct item types (Hardware / Software License / Services) found among
        /// this invoice's underlying set items — used by the Item Type multi-select filter.</summary>
        public IReadOnlyList<string> ItemTypes { get; set; } = Array.Empty<string>();

        /// <summary>Distinct Sub-Types (Contract/Subscription/License/Services) found among this
        /// invoice's dbo.SetItem rows — used by the Sub-Type filter.</summary>
        public IReadOnlyList<string> SubTypes { get; set; } = Array.Empty<string>();

        /// <summary>Distinct Sub-Type Group Reference Codes (Contract Code/License ID/etc.,
        /// nullable) found among this invoice's dbo.SetItem rows — searchable via SearchText.</summary>
        public IReadOnlyList<string> SubTypeReferenceCodes { get; set; } = Array.Empty<string>();

        /// <summary>True when at least one item on this invoice has no Sub-Type at all —
        /// including invoices with no dbo.SetItem rows whatsoever (pure Hardware/Request sets).
        /// Backs the "Non-Subtype" option in the Sub-Type filter.</summary>
        public bool HasNonSubTypeItems { get; set; } = true;
        public string SetCode { get; set; }
        public string DocumentNumber { get; set; }
        public string ReferenceNumber { get; set; }
        public DateTime? DocumentDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Company { get; set; }
        public string Distributor { get; set; }
        public string Status { get; set; }
        public string Remarks { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedByName { get; set; }
        public int? DaysLeft { get; set; }

        public decimal Subtotal { get; set; }
        public decimal VatAmount { get; set; }
        public decimal WhtAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmountDue { get; set; }

        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set => SetField(ref _selected, value);
        }

        public string EndDateDisplay => EndDate.HasValue ? EndDate.Value.ToString("MM/dd/yyyy") : "N/A";

        public string DaysLeftDisplay
        {
            get
            {
                if (!EndDate.HasValue || !DaysLeft.HasValue) return "N/A";
                var d = DaysLeft.Value;
                return d < 0 ? $"Expired {Math.Abs(d)}d" : $"{d}d";
            }
        }
    }
}
