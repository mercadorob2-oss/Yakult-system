using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Wpf.CallMonitoring;

namespace Yakult.Inventory.App.Forms.CallMonitoring.Hybrid
{
    public sealed class WpfDisplayModeHostControl : UserControl
    {
        private readonly ElementHost _host;
        private readonly WpfDisplayModeWorkspace _workspace;

        public WpfDisplayModeHostControl(ICallMonitoringRepository repository, ICallMonitoringNavigator navigator)
        {
            BackColor = Color.FromArgb(239, 243, 247);
            Dock = DockStyle.Fill;

            _workspace = new WpfDisplayModeWorkspace();
            _workspace.Initialize(repository, navigator);

            _host = new ElementHost
            {
                Dock = DockStyle.Fill,
                BackColorTransparent = true,
                Child = _workspace
            };

            Controls.Add(_host);
        }

        public Task LoadDataAsync(bool force = false)
        {
            return _workspace.LoadDataAsync(force);
        }
    }
}
