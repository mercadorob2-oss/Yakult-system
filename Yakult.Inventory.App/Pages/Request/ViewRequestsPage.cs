using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Request.Views;

namespace Yakult.Inventory.App.Pages.Request
{
    public partial class ViewRequestsPage : UserControl
    {
        private ElementHost _host;
        private RequestPageView _view;

        public ViewRequestsPage() : this(null, false) { }

        /// <param name="allowedCategories">
        /// When provided, restricts this page to only requests whose Item.Category is in this
        /// set (case-insensitive). Null (default) shows everything, matching MainForm's usage.
        /// </param>
        /// <param name="restrictToRequestSetManagementWorkflow">
        /// When true, additionally excludes pure-cartridge submissions (WorkflowType =
        /// 'CartridgeManagement'), which belong exclusively to Card 1's queue instead.
        /// </param>
        public ViewRequestsPage(System.Collections.Generic.IEnumerable<string> allowedCategories, bool restrictToRequestSetManagementWorkflow = false)
        {
            InitializeComponent();

            Dock = DockStyle.Fill;
            _view = new RequestPageView(allowedCategories, restrictToRequestSetManagementWorkflow);
            _host = new ElementHost { Dock = DockStyle.Fill, Child = _view };
            Controls.Add(_host);
        }

        public void ApplyInitialSearch(string query) => _view?.ApplyInitialSearch(query?.Trim());

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _host?.Dispose(); } catch { }
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
