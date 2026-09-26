using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// Makes the Minimize button on a modal dialog minimize only that dialog, not the whole app.
    ///
    /// Why the whole app seemed to minimize: the main window is NOT actually minimized. A modal
    /// dialog disables every other window of the app. A normal minimize (SW_MINIMIZE) hands
    /// activation to the next window, and since all of this app's windows are disabled, Windows
    /// activates a different program (VS Code, the browser, ...), which covers the app. Verified
    /// with a repro: after the minimize the main window was not iconic, but the foreground
    /// window belonged to another process. It is not a 32/64-bit issue.
    ///
    /// The fix is to minimize with SW_SHOWMINNOACTIVE, which keeps activation in this app, so
    /// the main window stays in front. A thread-local WH_CBT hook sees every minimize on the UI
    /// thread before it happens (HCBT_MINMAX), whatever triggered it: a native title bar, a custom
    /// title bar button setting WindowState, an owned or an ownerless dialog, WinForms or WPF.
    /// While a modal dialog is open (another window of the thread is disabled) the activating
    /// minimize is cancelled and redone as SW_SHOWMINNOACTIVE. Minimizing the app itself
    /// (e.g. from the taskbar) is untouched, because that targets a disabled window.
    /// </summary>
    internal static class ModalMinimizeGuard
    {
        private const int WH_CBT = 5;
        private const int HCBT_MINMAX = 1;

        private const int SW_SHOWMINIMIZED = 2;
        private const int SW_MINIMIZE = 6;
        private const int SW_SHOWMINNOACTIVE = 7;
        private const int SW_FORCEMINIMIZE = 11;

        private const uint GA_ROOT = 2;

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate bool EnumThreadWndProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadWndProc lpfn, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        // Kept in a static field so the GC never collects the delegate while Windows holds it.
        private static HookProc _hookProc;
        private static IntPtr _hook = IntPtr.Zero;
        private static uint _uiThreadId;

        /// <summary>Call once on the UI thread, before Application.Run.</summary>
        internal static void Install()
        {
            if (_hook != IntPtr.Zero)
                return;

            _uiThreadId = GetCurrentThreadId();
            _hookProc = CbtHook;
            _hook = SetWindowsHookEx(WH_CBT, _hookProc, IntPtr.Zero, _uiThreadId);

            Application.ApplicationExit += (s, e) =>
            {
                if (_hook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hook);
                    _hook = IntPtr.Zero;
                }
            };
        }

        private static IntPtr CbtHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode == HCBT_MINMAX)
                {
                    var hwnd = wParam;
                    var cmd = (int)(lParam.ToInt64() & 0xFFFF);
                    if ((cmd == SW_MINIMIZE || cmd == SW_SHOWMINIMIZED || cmd == SW_FORCEMINIMIZE)
                        && IsTopModalWindow(hwnd))
                    {
                        // Cancel this minimize and redo it as a non-activating one. Deferred so
                        // the window is not re-entered from inside the hook.
                        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => MinimizeNoActivate(hwnd)));
                        return new IntPtr(1);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ModalMinimizeGuard hook failed: " + ex);
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        /// <summary>
        /// True when <paramref name="hwnd"/> is an enabled top-level window while some other visible
        /// top-level window of the UI thread is disabled, i.e. it is the modal dialog on top.
        /// </summary>
        private static bool IsTopModalWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || GetAncestor(hwnd, GA_ROOT) != hwnd || !IsWindowEnabled(hwnd))
                return false;

            var otherDisabled = false;
            EnumThreadWindows(_uiThreadId, (h, _) =>
            {
                if (h != hwnd && IsWindowVisible(h) && !IsWindowEnabled(h))
                {
                    otherDisabled = true;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return otherDisabled;
        }

        private static void MinimizeNoActivate(IntPtr hwnd)
        {
            try
            {
                if (HwndSource.FromHwnd(hwnd)?.RootVisual is System.Windows.Window wpfWindow)
                    Minimize(wpfWindow);
                else
                    ShowWindow(hwnd, SW_SHOWMINNOACTIVE);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ModalMinimizeGuard minimize failed: " + ex);
            }
        }

        private static readonly ConditionalWeakTable<System.Windows.Window, object> _taskbarRestore =
            new ConditionalWeakTable<System.Windows.Window, object>();

        /// <summary>
        /// Minimizes a WPF dialog without handing activation to another program, so the app's
        /// other windows stay in front. Custom title bar Minimize handlers call this directly;
        /// every other minimize is routed here by the hook.
        ///
        /// A dialog that is not in the taskbar gets a taskbar button while minimized (a borderless
        /// window has no caption bar to restore from); it is removed again once restored.
        /// </summary>
        internal static void Minimize(System.Windows.Window window)
        {
            if (window == null)
                return;

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                window.WindowState = System.Windows.WindowState.Minimized;
                return;
            }

            if (!window.ShowInTaskbar && !_taskbarRestore.TryGetValue(window, out _))
            {
                window.ShowInTaskbar = true;
                EventHandler onStateChanged = null;
                onStateChanged = (s, e) =>
                {
                    if (window.WindowState == System.Windows.WindowState.Minimized)
                        return;
                    window.StateChanged -= onStateChanged;
                    _taskbarRestore.Remove(window);
                    window.ShowInTaskbar = false;
                };
                window.StateChanged += onStateChanged;
                _taskbarRestore.Add(window, onStateChanged);
            }

            ShowWindow(hwnd, SW_SHOWMINNOACTIVE);
        }
    }
}
