using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Wpf.CallMonitoring;

namespace Yakult.Inventory.App.Forms.CallMonitoring.Hybrid
{
    public sealed class WpfTicketManagementHostControl : UserControl
    {
        private readonly ElementHost _host;
        private readonly WpfTicketManagementWorkspace _workspace;

        public WpfTicketManagementHostControl()
        {
            BackColor = Color.FromArgb(236, 240, 241);
            Dock = DockStyle.Fill;

            _workspace = new WpfTicketManagementWorkspace();
            _host = new ElementHost
            {
                Dock = DockStyle.Fill,
                BackColorTransparent = true,
                Child = _workspace
            };

            Controls.Add(_host);
        }

        public void Initialize(ICallMonitoringRepository repository)
        {
            _workspace.Initialize(repository);
        }

        public Task LoadDataAsync()
        {
            return _workspace.LoadDataAsync();
        }

        public Task SelectTicketAsync(int ticketId)
        {
            return _workspace.SelectTicketAsync(ticketId);
        }
    }
}
