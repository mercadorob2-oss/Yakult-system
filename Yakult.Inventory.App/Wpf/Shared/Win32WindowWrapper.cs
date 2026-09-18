using System;

namespace Yakult.Inventory.App.WPF.Shared
{
    // Lets a native WPF Window own a WinForms Form dialog (Form.ShowDialog(IWin32Window))
    // when there is no ElementHost in the ownership chain to provide one directly.
    internal sealed class Win32WindowWrapper : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; }

        public Win32WindowWrapper(IntPtr handle)
        {
            Handle = handle;
        }
    }
}
