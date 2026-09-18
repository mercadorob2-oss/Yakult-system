using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Wpf.CallMonitoring;

namespace Yakult.Inventory.App.Forms.CallMonitoring.Hybrid
{
    public sealed class WpfReportsHostControl : UserControl
    {
        private readonly ElementHost _host;
        private readonly WpfCallMonitoringReportsWorkspace _workspace;

        public WpfReportsHostControl(ICallMonitoringRepository repository, ICallMonitoringNavigator navigator)
        {
            BackColor = Color.FromArgb(241, 244, 247);
            Dock = DockStyle.Fill;

            _workspace = new WpfCallMonitoringReportsWorkspace();
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

        public Task ApplyFilterAsync(DateTime fromLocal, DateTime toLocal, string type)
        {
            return _workspace.ApplyFilterAsync(fromLocal, toLocal, type);
        }
    }
}
