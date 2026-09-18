using System;
using System.Collections.Generic;
using System.Linq;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Set.ViewModels
{
    /// <summary>
    /// Presentation-layer row wrapper around Pages.SetDto for the Sets grid. Wraps (rather than
    /// reuses) SetDto directly so the checkbox column can raise PropertyChanged on Selected —
    /// required for the "select all" toggle to visually update already-rendered rows, since
    /// SetDto itself is a plain POCO.
    /// </summary>
    public sealed class SetRow : ViewModelBase
    {
        public SetDto Dto { get; }

        /// <summary>Distinct item categories (e.g. Cartridge, Ink, Toner, Printhead) found among
        /// this set's items — used by the Category multi-select filter and the Categories column.</summary>
        public IReadOnlyList<string> Categories { get; }
        public string CategoriesText => Categories.Count == 0 ? "—" : string.Join(", ", Categories);

        /// <summary>Earliest/latest DatePurchased among this set's items — used by the
        /// Date Purchased columns and the Sort By dropdown (via ListSortHelper).</summary>
        public DateTime? EarliestItemPurchaseDate { get; }
        public DateTime? LatestItemPurchaseDate { get; }

        public SetRow(SetDto dto, IReadOnlyList<string> categories = null, DateTime? earliestItemPurchaseDate = null, DateTime? latestItemPurchaseDate = null)
        {
            Dto = dto;
            Categories = categories ?? Array.Empty<string>();
            EarliestItemPurchaseDate = earliestItemPurchaseDate;
            LatestItemPurchaseDate = latestItemPurchaseDate;
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
        public bool IsInvoice => Dto.IsInvoice;
        public bool IsArchived => Dto.IsArchived;
        public string SetCode => Dto.SetCode;
        public string SetType => Dto.SetType;
        public string SetStatus => Dto.SetStatus;
        public string CreatedByName => Dto.CreatedByName;
        public DateTime CreatedAt => Dto.CreatedAt;
        public DateTime? DateRequested => Dto.DateRequested;
        public int ItemCount => Dto.ItemCount;
        public string CurrentEmployeeName => Dto.CurrentEmployeeName;
        public string CurrentEmployeeNumber => Dto.CurrentEmployeeNumber;
        public string CurrentCompanyName => Dto.CurrentCompanyName;
        public string CurrentBranchName => Dto.CurrentBranchName;
        public string CurrentDepartmentName => Dto.CurrentDepartmentName;
        public DateTime EffectiveExpiry => Dto.EffectiveExpiry;
        public int? DaysLeft => Dto.DaysLeft;
        public string QRImagePath => Dto.QRImagePath;
        public bool HasQrImage => Dto.HasQrImage;
        public string QRImageDisplay => Dto.QRImageDisplay;
        public string Remarks => Dto.Remarks;
    }
}
