using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Wpf.CallMonitoring;
using Yakult.Inventory.App.Forms.CallMonitoring;

namespace Yakult.Inventory.App.Forms.CallMonitoring.Hybrid
{
    public sealed class WpfDiagnosticsHostControl : UserControl
    {
        private readonly ElementHost _host;
        private readonly WpfCallMonitoringDiagnosticsWorkspace _workspace;

        public WpfDiagnosticsHostControl(
            ICallMonitoringRepository repository,
            ICallMonitoringNavigator navigator)
        {
            BackColor = Color.FromArgb(241, 244, 247);
            Dock = DockStyle.Fill;

            _workspace = new WpfCallMonitoringDiagnosticsWorkspace();
            _workspace.Initialize(repository, navigator);

            _host = new ElementHost
            {
                Dock = DockStyle.Fill,
                BackColorTransparent = true,
                Child = _workspace
            };

            Controls.Add(_host);
        }
    }
}
