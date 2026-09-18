using System;
using System.Globalization;
using System.Windows.Data;
using Yakult.Inventory.App.WPF.Admin.EmailManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.EmailManagement.Converters
{
    /// <summary>
    /// Maps a nullable per-template SMTP profile id to/from the "— None —" sentinel id
    /// used by <see cref="SmtpSendSettingsViewModel.ProfilesWithNone"/>, so an unset
    /// profile shows the None row selected instead of a blank combo box.
    /// </summary>
    public class NullableProfileIdConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value as int?) ?? SmtpSendSettingsViewModel.NoneProfileSentinelId;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int id && id == SmtpSendSettingsViewModel.NoneProfileSentinelId)
                return null;
            return value;
        }
    }
}
