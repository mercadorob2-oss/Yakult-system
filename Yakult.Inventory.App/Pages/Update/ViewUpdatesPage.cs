using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Update.Views;

namespace Yakult.Inventory.App.Pages.Update
{
    public partial class ViewUpdatesPage : UserControl
    {
        private ElementHost _host;
        private UpdatesPageView _view;

        public ViewUpdatesPage()
        {
            InitializeComponent();

            _view = new UpdatesPageView();

            _host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _view
            };

            Controls.Add(_host);
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            Name = "ViewUpdatesPage";
            Size = new Size(1200, 800);
            BackColor = Color.White;
            ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _host?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
