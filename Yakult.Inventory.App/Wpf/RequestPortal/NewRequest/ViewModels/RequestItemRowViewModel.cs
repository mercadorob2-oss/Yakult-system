using Yakult.Inventory.App.Models.ViewModels;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.RequestPortal.NewRequest.ViewModels
{
    public class RequestItemRowViewModel : ViewModelBase
    {
        private string _category = "Cartridge";
        public string Category
        {
            get => _category;
            set
            {
                if (!SetField(ref _category, value)) return;
                OnPropertyChanged(nameof(IsCartridge));
                OnPropertyChanged(nameof(GoodDisplay));
                OnPropertyChanged(nameof(DamagedDisplay));
            }
        }

        private string _cartridgeModel;
        public string CartridgeModel
        {
            get => _cartridgeModel;
            set => SetField(ref _cartridgeModel, value);
        }

        private string _modelKey;
        public string ModelKey
        {
            get => _modelKey;
            set => SetField(ref _modelKey, value);
        }

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set => SetField(ref _quantity, value);
        }

        private int _goodEmptyQty;
        public int GoodEmptyQty
        {
            get => _goodEmptyQty;
            set
            {
                if (!SetField(ref _goodEmptyQty, value)) return;
                OnPropertyChanged(nameof(GoodDisplay));
            }
        }

        private int _damagedEmptyQty;
        public int DamagedEmptyQty
        {
            get => _damagedEmptyQty;
            set
            {
                if (!SetField(ref _damagedEmptyQty, value)) return;
                OnPropertyChanged(nameof(DamagedDisplay));
            }
        }

        public string CartridgeType => "With Cartridge";

        public bool IsCartridge => string.IsNullOrWhiteSpace(Category) || Category == "Cartridge";

        // Good/Damaged empty-cartridge tracking only applies to the Cartridge category —
        // show a dash for other categories rather than a misleading "0".
        public string GoodDisplay => IsCartridge ? GoodEmptyQty.ToString() : "—";
        public string DamagedDisplay => IsCartridge ? DamagedEmptyQty.ToString() : "—";

        public CartridgeRequestItemViewModel ToServiceModel() => new CartridgeRequestItemViewModel
        {
            Category        = Category,
            CartridgeModel  = CartridgeModel,
            ModelKey        = ModelKey,
            Quantity        = Quantity,
            GoodEmptyQty    = GoodEmptyQty,
            DamagedEmptyQty = DamagedEmptyQty,
            CartridgeType   = CartridgeType
        };
    }
}
