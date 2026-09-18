using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.SendNotifications.Views;

namespace Yakult.Inventory.App.Forms.CartridgeManagement
{
    /// <summary>
    /// WinForms Form that hosts the WPF SendNotificationsView via ElementHost.
    /// Used by CartridgeManagementPortalForm.ShowForm() in place of the legacy
    /// CartridgeFulfillmentNotificationPage UserControl.
    /// </summary>
    public class SendNotificationsWpfHost : Form
    {
        private ElementHost           _host;
        private SendNotificationsView _view;

        public SendNotificationsWpfHost()
        {
            Text            = "Send Notifications";
            MinimumSize     = new Size(1100, 640);
            StartPosition   = FormStartPosition.CenterScreen;
            WindowState     = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.Sizable;
            BackColor       = Color.FromArgb(240, 242, 246);

            _view = new SendNotificationsView();

            _host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _view
            };

            Controls.Add(_host);

            FormClosed      += (s, e) => DisposeHost();
            HandleDestroyed += (s, e) => DisposeHost();
        }

        private bool _disposed;
        private void DisposeHost()
        {
            if (_disposed) return;
            _disposed = true;
            try { _host?.Dispose(); } catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeHost();
            base.Dispose(disposing);
        }
    }
}
