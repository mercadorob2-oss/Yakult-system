using System;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Archive.ViewModels
{
    public class ArchiveRecordDto : ViewModelBase
    {
        public int      ArchiveId     { get; set; }
        public string   EntityType    { get; set; }
        public int      EntityId      { get; set; }
        public string   RecordName    { get; set; }
        public DateTime ArchivedAt    { get; set; }
        public string   ArchivedBy    { get; set; }
        public string   ArchiveReason { get; set; }

        /// <summary>dbo.Item.Category — only populated for EntityType == "Item"; null otherwise.</summary>
        public string   Category      { get; set; }

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

        public string ArchivedAtDisplay => ArchivedAt.ToString("yyyy-MM-dd HH:mm");

        public string EntityTypeBadgeColor
        {
            get
            {
                switch (EntityType)
                {
                    case "Item":           return "#3A8EF6";
                    case "Set":            return "#9B59B6";
                    case "Request":        return "#E74C3C";
                    case "Inventory":      return "#F39C12";
                    case "Employee":       return "#1E9E5E";
                    case "Department":     return "#27AE60";
                    case "Branch":         return "#16A085";
                    case "Company":        return "#2980B9";
                    case "Vendor":         return "#8E44AD";
                    case "EmptyCartridge": return "#D35400";
                    case "CartridgeModel": return "#C0392B";
                    case "ItemCategory":   return "#7F8C8D";
                    case "Condition":      return "#95A5A6";
                    case "Renewal":        return "#E67E22";
                    default:               return "#5A6A7E";
                }
            }
        }
    }
}
