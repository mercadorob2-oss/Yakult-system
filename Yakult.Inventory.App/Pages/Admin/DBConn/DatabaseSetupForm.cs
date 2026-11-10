using System;
using System.Windows;
using System.Windows.Interop;
using Yakult.Inventory.App.Wpf.Admin.Database;
using WinForms = global::System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Admin.DBConn
{
    // Shared entry point for startup, login and WPF/WinForms dashboards.
    public sealed class DatabaseSetupForm : IDisposable
    {
        private readonly bool _initialSetup;
        private DatabaseConnectionWindow _window;
        public DatabaseSetupForm(bool initialSetup = false) { _initialSetup = initialSetup; }
        public WinForms.DialogResult ShowDialog() => Show(null);
        public WinForms.DialogResult ShowDialog(WinForms.IWin32Window owner) => Show(owner);
        public WinForms.DialogResult ShowDialog(Window owner) => Show(owner);
        private WinForms.DialogResult Show(object owner)
        {
            if (System.Windows.Application.Current == null)
                _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _window = new DatabaseConnectionWindow(_initialSetup);
            if (owner is Window wpfOwner) { _window.Owner = wpfOwner; _window.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
            else if (owner is WinForms.IWin32Window formsOwner) { new WindowInteropHelper(_window).Owner = formsOwner.Handle; _window.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
            return _window.ShowDialog() == true ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }
        public void Dispose() { if (_window?.IsVisible == true) _window.Close(); _window = null; }
    }
}
