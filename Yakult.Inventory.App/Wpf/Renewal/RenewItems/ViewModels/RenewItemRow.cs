using System;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Renewal.RenewItems.ViewModels
{
    /// <summary>
    /// One editable row in the Renew Items grid. Wraps a SetItem being renewed, carrying
    /// whatever edits the user makes to Description/Quantity/UnitPrice/Start/End Date.
    /// Original* fields are kept so "Clear" (remove replacement) can restore them.
    /// </summary>
    public class RenewItemRow : ViewModelBase
    {
        public int SetItemId { get; }
        public int OriginalItemId { get; }
        public DateTime? OriginalLineEndDate { get; }

        public string OriginalItemCode { get; }
        public string OriginalDescription { get; }
        public string OriginalUnitOfMeasure { get; }
        public decimal OriginalUnitPrice { get; }

        private bool _selected = true;
        public bool Selected
        {
            get => _selected;
            set => SetField(ref _selected, value);
        }

        private string _itemCode;
        public string ItemCode
        {
            get => _itemCode;
            set => SetField(ref _itemCode, value);
        }

        public string ItemName { get; }

        private string _description;
        public string Description
        {
            get => _description;
            set => SetField(ref _description, value);
        }

        private decimal _quantity;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (SetField(ref _quantity, value))
                    OnPropertyChanged(nameof(Amount));
            }
        }

        private string _unitOfMeasure;
        public string UnitOfMeasure
        {
            get => _unitOfMeasure;
            set => SetField(ref _unitOfMeasure, value);
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (SetField(ref _unitPrice, value))
                    OnPropertyChanged(nameof(Amount));
            }
        }

        public decimal Amount => Quantity * UnitPrice;

        private DateTime _startDate;
        public DateTime StartDate
        {
            get => _startDate;
            set => SetField(ref _startDate, value);
        }

        private DateTime _endDate;
        public DateTime EndDate
        {
            get => _endDate;
            set => SetField(ref _endDate, value);
        }

        private string _replacementDisplay = "— Original —";
        public string ReplacementDisplay
        {
            get => _replacementDisplay;
            set => SetField(ref _replacementDisplay, value);
        }

        /// <summary>Sub-Type Group fields, defaulted from the source SetItem (carry-forward) and
        /// reassignable via the "Convert Selected to Sub-Type Group" panel before Confirm.
        /// SubType null/blank means this row stays ungrouped.</summary>
        private string _subType;
        public string SubType
        {
            get => _subType;
            set
            {
                if (SetField(ref _subType, value))
                    OnPropertyChanged(nameof(GroupDisplay));
            }
        }

        private string _referenceCode;
        public string ReferenceCode
        {
            get => _referenceCode;
            set
            {
                if (SetField(ref _referenceCode, value))
                    OnPropertyChanged(nameof(GroupDisplay));
            }
        }

        private DateTime? _groupBeginDate;
        public DateTime? GroupBeginDate
        {
            get => _groupBeginDate;
            set => SetField(ref _groupBeginDate, value);
        }

        private DateTime? _groupEndDate;
        public DateTime? GroupEndDate
        {
            get => _groupEndDate;
            set => SetField(ref _groupEndDate, value);
        }

        // Matches ViewInvoiceDetailPage's SubTypeGroupKeyConverter format exactly, so the same
        // Sub-Type Group keys/header text appear consistently across both windows.
        public string GroupDisplay => string.IsNullOrWhiteSpace(SubType)
            ? "Ungrouped"
            : $"{SubType} #{(string.IsNullOrWhiteSpace(ReferenceCode) ? "(none)" : ReferenceCode)}";

        /// <summary>Parent Tag Group field (free-text label, e.g. "HX Cluster with 40% Storage
        /// Buffer"), defaulted from the source SetItem (carry-forward via dbo.SetItemParentTagGroup)
        /// and reassignable via the grouping panel before Confirm. Independent of Sub-Type
        /// Group above -- a row can carry both. Blank means this row stays ungrouped.</summary>
        private string _parentTag;
        public string ParentTag
        {
            get => _parentTag;
            set
            {
                if (SetField(ref _parentTag, value))
                    OnPropertyChanged(nameof(ParentTagGroupDisplay));
            }
        }

        // Second-level grouping key, nested under GroupDisplay (Sub-Type) in the Renew Items
        // grid — see RenewItemsWindow.xaml.cs's ConfigureItemsGrouping.
        public string ParentTagGroupDisplay => string.IsNullOrWhiteSpace(ParentTag) ? "Ungrouped" : ParentTag;

        /// <summary>True when this row has no backing dbo.SetItem yet — it was added via
        /// "Add Item" for an extra item included in the renewal, not carried over from the
        /// original Set. On confirm it is inserted fresh instead of going through the
        /// renew-existing-item path.</summary>
        public bool IsNewRow => SetItemId <= 0;

        public RenewItemRow(Yakult.Inventory.App.Models.SetItemRenewalDto source)
        {
            SetItemId = source.SetItemId;
            OriginalItemId = source.ItemId;
            OriginalLineEndDate = source.LineEndDate;

            OriginalItemCode = source.ItemCode;
            OriginalDescription = source.Description;
            OriginalUnitOfMeasure = source.UnitOfMeasure;
            OriginalUnitPrice = source.UnitPrice;
            ItemName = source.ItemName;

            _itemCode = source.ItemCode;
            _description = source.Description;
            _quantity = source.Quantity;
            _unitOfMeasure = source.UnitOfMeasure;
            _unitPrice = source.UnitPrice;
            _startDate = source.LineStartDate ?? DateTime.Today;
            _endDate = source.LineEndDate ?? _startDate.AddYears(1);

            _subType = source.SubType;
            _referenceCode = source.ReferenceCode;
            _groupBeginDate = source.BeginDate;
            _groupEndDate = source.EndDate;

            _parentTag = source.ParentTag;
        }

        /// <summary>Builds a brand-new row for an item picked from the catalog that isn't part
        /// of the original Set at all — e.g. an extra item the renewal now needs to include.</summary>
        public RenewItemRow(Yakult.Inventory.App.Models.ItemCatalogDto catalogItem, DateTime startDate, DateTime endDate)
        {
            SetItemId = 0;
            OriginalItemId = catalogItem.ItemId;
            OriginalLineEndDate = null;

            OriginalItemCode = catalogItem.ItemCode;
            OriginalDescription = catalogItem.Name;
            OriginalUnitOfMeasure = catalogItem.UnitOfMeasure;
            OriginalUnitPrice = catalogItem.UnitPrice;
            ItemName = catalogItem.Name;

            _itemCode = catalogItem.ItemCode;
            _description = catalogItem.Name;
            _quantity = 1;
            _unitOfMeasure = catalogItem.UnitOfMeasure;
            _unitPrice = catalogItem.UnitPrice;
            _startDate = startDate;
            _endDate = endDate;

            _replacementDisplay = "— New Item —";
        }
    }
}
