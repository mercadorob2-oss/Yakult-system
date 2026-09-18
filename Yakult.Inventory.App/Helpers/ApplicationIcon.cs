using System;
using System.Drawing;
using System.IO;

namespace Yakult.Inventory.App.Helpers
{
    public static class ApplicationIcon
    {
        private static Icon _cachedIcon;

        public static Icon GetIcon()
        {
            if (_cachedIcon == null)
            {
                try
                {
                    string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yakult_icon_desktop.ico");
                    if (File.Exists(iconPath))
                    {
                        _cachedIcon = new Icon(iconPath);
                    }
                }
                catch
                {
                    // Icon loading failed, will use default
                }
            }
            return _cachedIcon;
        }
    }
}
