using System;
using System.Globalization;
using System.Windows.Data;
using Yakult.Inventory.App.WPF.EDocs.Gatepass.Models;

namespace Yakult.Inventory.App.WPF.EDocs.Gatepass.Views
{
    // Enum -> friendly chip label for the gatepass item/model selectors,
    // matching the mobile "Item selection" wording.
    public class GatepassCategoryLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is GatepassItemCategory category)) return value?.ToString() ?? "";
            switch (category)
            {
                case GatepassItemCategory.CartridgeRibbon: return "Cartridge Ribbon";
                case GatepassItemCategory.Monitor: return "Monitor";
                case GatepassItemCategory.Cpu: return "CPU";
                case GatepassItemCategory.Mouse: return "Mouse";
                case GatepassItemCategory.Keyboard: return "Keyboard";
                case GatepassItemCategory.Printer: return "Printer";
                case GatepassItemCategory.BackupUps: return "Backup UPS";
                case GatepassItemCategory.Avr: return "AVR";
                case GatepassItemCategory.ComputerTableFixedAsset: return "Computer Table / Fixed Asset";
                case GatepassItemCategory.Others: return "Others";
                default: return category.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class GatepassModelLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is GatepassModel model)) return value?.ToString() ?? "";
            switch (model)
            {
                case GatepassModel.Lx300: return "LX300";
                case GatepassModel.Lx310: return "LX310";
                case GatepassModel.Lq2190: return "LQ2190";
                case GatepassModel.None: return "None";
                default: return model.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
