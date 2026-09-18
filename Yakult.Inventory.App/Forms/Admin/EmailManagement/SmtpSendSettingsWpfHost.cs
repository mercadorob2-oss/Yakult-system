using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.Admin.EmailManagement.ViewModels;
using Yakult.Inventory.App.WPF.Admin.EmailManagement.Views;

namespace Yakult.Inventory.App.Forms.Admin.EmailManagement
{
    /// <summary>
    /// Hosts the WPF SMTP Send Settings view inside the WinForms admin portal content panel.
    /// </summary>
    public class SmtpSendSettingsWpfHost : UserControl
    {
        private ElementHost _host;
        private SmtpSendSettingsView _view;
        private bool _disposed;

        public SmtpSendSettingsWpfHost()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(240, 242, 246);

            _view = new SmtpSendSettingsView();
            _view.Bind(new SmtpSendSettingsViewModel(new SystemSettingRepository(), new EmailRepository()));
            _host = new ElementHost { Dock = DockStyle.Fill, Child = _view };

            Controls.Add(_host);
            HandleDestroyed += (s, e) => DisposeHost();
        }

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
