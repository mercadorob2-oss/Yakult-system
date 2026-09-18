using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Archive.ViewModels
{
    /// <summary>
    /// One checkbox row in the Archive Records page's multi-select "Type" filter.
    /// </summary>
    public class EntityTypeCheckItem : ViewModelBase
    {
        public string Type { get; }

        private bool _isChecked;
        public bool IsChecked { get => _isChecked; set => SetField(ref _isChecked, value); }

        public EntityTypeCheckItem(string type)
        {
            Type = type;
        }
    }
}
