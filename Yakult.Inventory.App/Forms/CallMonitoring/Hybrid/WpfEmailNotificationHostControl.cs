using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Wpf.CallMonitoring;

namespace Yakult.Inventory.App.Forms.CallMonitoring.Hybrid
{
    public sealed class WpfEmailNotificationHostControl : UserControl
    {
        private readonly ElementHost _host;
        private readonly WpfEmailNotificationWorkspace _workspace;

        public WpfEmailNotificationHostControl(ICallMonitoringRepository repository)
        {
            BackColor = Color.FromArgb(241, 244, 247);
            Dock = DockStyle.Fill;

            _workspace = new WpfEmailNotificationWorkspace(repository);

            _host = new ElementHost
            {
                Dock = DockStyle.Fill,
                BackColorTransparent = true,
                Child = _workspace
            };

            Controls.Add(_host);
        }

        public void NavigateTo(
            WpfEmailNotificationDeepLinkTarget target,
            int? deptId = null,
            int? branchId = null,
            string emailLogSearch = null)
        {
            _workspace.NavigateTo(target, deptId, branchId, emailLogSearch);
        }
    }
}
