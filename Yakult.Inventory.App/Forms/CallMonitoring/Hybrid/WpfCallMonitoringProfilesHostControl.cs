using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Wpf.CallMonitoring;

namespace Yakult.Inventory.App.Forms.CallMonitoring.Hybrid
{
    public sealed class WpfCallMonitoringProfilesHostControl : UserControl
    {
        private readonly ElementHost _host;
        private readonly WpfCallMonitoringProfilesWorkspace _workspace;

        public WpfCallMonitoringProfilesHostControl()
        {
            BackColor = Color.FromArgb(241, 244, 247);
            Dock = DockStyle.Fill;

            _workspace = new WpfCallMonitoringProfilesWorkspace();
            _host = new ElementHost
            {
                Dock = DockStyle.Fill,
                BackColorTransparent = true,
                Child = _workspace
            };

            Controls.Add(_host);
        }

        public void Initialize(ICallMonitoringRepository repository, string itDepartmentName, ICallMonitoringNavigator navigator = null)
        {
            _workspace.Initialize(repository, itDepartmentName, navigator);
        }

        public void RefreshData()
        {
            _workspace.RefreshData();
        }
    }
}
