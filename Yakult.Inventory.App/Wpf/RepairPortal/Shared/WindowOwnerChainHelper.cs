using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Shared
{
    /// <summary>Mitigates a Win32 owned-window quirk seen in this app's WinForms(MainForm) →
    /// WPF(RepairPortalShellWindow) → WPF(Reports/Attendance History/...) owner chain: leaving two
    /// or more owned windows open, Alt+Tabbing away and back, then closing one has been observed to
    /// minimize the entire application instead of reactivating the owner. Windows' default
    /// "reactivate the owner when an owned window closes" heuristic appears to lose track of which
    /// ancestor to restore once the Alt+Tab cycle disturbs the owner chain's activation order.
    /// Call <see cref="RestoreAndActivate"/> from a window's Closing handler to force the whole
    /// owner chain back to a normal, activated state explicitly instead of letting Win32 guess.</summary>
    internal static class WindowOwnerChainHelper
    {
        private const int GW_OWNER = 4;
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public static void RestoreAndActivate(Window closingWindow)
        {
            if (closingWindow?.Owner != null)
            {
                if (closingWindow.Owner.WindowState == WindowState.Minimized)
                    closingWindow.Owner.WindowState = WindowState.Normal;
                closingWindow.Owner.Activate();
            }

            var hwnd = new WindowInteropHelper(closingWindow).Handle;
            if (hwnd == IntPtr.Zero) return;

            var ownerHwnd = GetWindow(hwnd, GW_OWNER);
            IntPtr topmostOwner = IntPtr.Zero;
            while (ownerHwnd != IntPtr.Zero)
            {
                if (IsIconic(ownerHwnd))
                    ShowWindow(ownerHwnd, SW_RESTORE);
                topmostOwner = ownerHwnd;
                ownerHwnd = GetWindow(ownerHwnd, GW_OWNER);
            }

            if (topmostOwner != IntPtr.Zero)
                SetForegroundWindow(topmostOwner);
        }
    }
}
