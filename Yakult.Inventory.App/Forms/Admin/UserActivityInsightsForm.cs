using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Forms.Admin
{
    /// <summary>
    /// Popup shell for the User Activity Insights dashboard. All UI and analytics logic
    /// live in the WPF <see cref="UserActivityInsightsWpfHost"/> hosted below — this Form
    /// only provides the window chrome (size, title, centering) that the WinForms
    /// "Insights" button expects to Show().
    /// </summary>
    public sealed class UserActivityInsightsForm : Form
    {
        public UserActivityInsightsForm(UserActivityFilter initialFilter = null)
        {
            Text          = "User Activity Insights";
            Size          = new Size(1400, 900);
            MinimumSize   = new Size(1000, 600);
            StartPosition = FormStartPosition.CenterParent;

            var host = new UserActivityInsightsWpfHost(initialFilter)
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(host);
        }
    }
}
