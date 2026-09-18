using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Wpf.CallMonitoring;

namespace Yakult.Inventory.App.Forms.CallMonitoring.Hybrid
{
    public sealed class WpfIncomingTicketsHostControl : UserControl
    {
        private readonly ElementHost _host;
        private readonly WpfIncomingTicketsWorkspace _workspace;

        public WpfIncomingTicketsHostControl()
        {
            BackColor = Color.FromArgb(236, 240, 241);
            Dock = DockStyle.Fill;

            _workspace = new WpfIncomingTicketsWorkspace();
            _host = new ElementHost
            {
                Dock = DockStyle.Fill,
                BackColorTransparent = true,
                Child = _workspace
            };

            Controls.Add(_host);
        }

        public void Initialize(
            ICallMonitoringRepository repository,
            ICallMonitoringNavigator navigator)
        {
            _workspace.Initialize(repository, navigator);
        }

        public Task LoadDataAsync()
        {
            return _workspace.LoadDataAsync();
        }

        public void RefreshData()
        {
            _workspace.RefreshData();
        }
    }
}
