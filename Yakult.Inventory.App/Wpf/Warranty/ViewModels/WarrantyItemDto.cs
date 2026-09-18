using System;

namespace Yakult.Inventory.App.WPF.Warranty.ViewModels
{
    /// <summary>Ported verbatim from the private nested WarrantyItemDto in the original
    /// Pages\Warranty\ViewWarrantyPage.cs.</summary>
    public sealed class WarrantyItemDto
    {
        public int ItemId { get; set; }
        public string Name { get; set; }
        public string ItemType { get; set; }
        public string SerialNumber { get; set; }
        public string ModelNumber { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int WarrantyYears { get; set; }
        public DateTime? WarrantyStartDate { get; set; }
        public DateTime? WarrantyEndDate { get; set; }
        public int? DaysRemaining { get; set; }
        public string WarrantyStatus { get; set; }
        public string SetCode { get; set; }
    }
}
