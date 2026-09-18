using System;
using System.ComponentModel;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Wpf.Search.ViewModels
{
    public class SearchCardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ItemDto Item { get; }
        public string Destination { get; }
        public int GroupCount { get; }

        public string DisplayTitle { get; }
        public string DisplaySubtitle { get; }

        public string Category => string.IsNullOrWhiteSpace(Item.Category) ? "Uncategorized" : Item.Category;
        public string ItemType  => string.IsNullOrWhiteSpace(Item.ItemType)  ? "Hardware"      : Item.ItemType;
        public string ModelLabel  => string.IsNullOrWhiteSpace(Item.ModelNumber)  ? null : $"Model: {Item.ModelNumber}";
        public string SerialLabel => string.IsNullOrWhiteSpace(Item.SerialNumber) ? null : $"SN: {Item.SerialNumber}";

        public int  StockOnHand => Item.StockOnHand;
        public bool IsActive    => Item.Active;
        public bool IsArchived  => Item.IsArchived;

        public int    CountBadgeValue => GroupCount;
        public string CountBadgeText  => GroupCount.ToString();
        public string StatusText      => Item.IsArchived ? "Archived" : (Item.Active ? "Active" : "Inactive");

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                Notify(nameof(IsSelected));
            }
        }

        public SearchCardViewModel(ItemDto item, string destination, string searchQuery, int groupCount)
        {
            Item        = item        ?? throw new ArgumentNullException(nameof(item));
            Destination = destination ?? "Items";
            GroupCount  = groupCount;
            DisplayTitle    = ComputeTitle(item, searchQuery);
            DisplaySubtitle = ComputeSubtitle(item, DisplayTitle);
        }

        private static string ComputeTitle(ItemDto item, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return item.Name ?? "(Unnamed)";
            string q = query.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(item.SerialNumber) && item.SerialNumber.ToLowerInvariant().Contains(q))
                return item.SerialNumber;
            if (!string.IsNullOrWhiteSpace(item.ModelNumber) && item.ModelNumber.ToLowerInvariant().Contains(q))
                return item.ModelNumber;
            if (!string.IsNullOrWhiteSpace(item.MatchedSubTypeReferenceCode))
                return item.MatchedSubTypeReferenceCode;
            return item.Name ?? "(Unnamed)";
        }

        private static string ComputeSubtitle(ItemDto item, string title)
        {
            if (!string.IsNullOrWhiteSpace(item.Name) &&
                !string.Equals(title, item.Name, StringComparison.OrdinalIgnoreCase))
                return item.Name;
            return null;
        }
    }
}
