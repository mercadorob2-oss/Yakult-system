using System;
using System.Drawing;
using H.NotifyIcon;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// System tray presence for the Request Portal, backed by H.NotifyIcon's WPF TaskbarIcon.
    ///
    /// This is tray/application-presence only — it never shows notification popups. The old
    /// WindowsToastService created a bare System.Windows.Forms.NotifyIcon purely as a vessel for
    /// balloon-tip toasts; it had Visible=true but no context menu, no click handling, and no
    /// show/hide behaviour of its own. This service is what actually gives the tray icon a
    /// purpose: a context menu, double-click to restore, and an Exit command.
    /// </summary>
    public static class TrayIconService
    {
        private static TaskbarIcon                    _trayIcon;
        private static System.Windows.Forms.Form       _ownerForm;

        /// <summary>Call once, after the Request Portal form's handle exists.</summary>
        public static void Initialize(System.Windows.Forms.Form ownerForm)
        {
            if (_trayIcon != null) return;
            _ownerForm = ownerForm ?? throw new ArgumentNullException(nameof(ownerForm));

            Icon icon;
            try { icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath); }
            catch { icon = SystemIcons.Application; }

            _trayIcon = new TaskbarIcon
            {
                Icon        = icon,
                ToolTipText = "Yakult Inventory System",
            };

            var menu = new System.Windows.Controls.ContextMenu();

            var showItem = new System.Windows.Controls.MenuItem { Header = "Show Request Portal" };
            showItem.Click += (s, e) => ShowOwner();

            var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
            exitItem.Click += (s, e) => System.Windows.Forms.Application.Exit();

            menu.Items.Add(showItem);
            menu.Items.Add(new System.Windows.Controls.Separator());
            menu.Items.Add(exitItem);

            _trayIcon.ContextMenu = menu;
            _trayIcon.TrayLeftMouseDoubleClick += (s, e) => ShowOwner();

            _trayIcon.ForceCreate(enablesEfficiencyMode: false);

            _ownerForm.FormClosed += (s, e) => Shutdown();
        }

        private static void ShowOwner()
        {
            if (_ownerForm == null || _ownerForm.IsDisposed) return;
            if (_ownerForm.InvokeRequired)
            {
                _ownerForm.BeginInvoke(new Action(ShowOwner));
                return;
            }
            if (_ownerForm.WindowState == System.Windows.Forms.FormWindowState.Minimized)
                _ownerForm.WindowState = System.Windows.Forms.FormWindowState.Normal;
            _ownerForm.Show();
            _ownerForm.Activate();
        }

        public static void Shutdown()
        {
            _trayIcon?.Dispose();
            _trayIcon  = null;
            _ownerForm = null;
        }
    }
}
