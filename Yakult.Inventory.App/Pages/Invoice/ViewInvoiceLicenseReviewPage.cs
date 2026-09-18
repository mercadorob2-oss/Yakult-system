using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.WPF.Invoice.Views;

namespace Yakult.Inventory.App.Pages.Invoice
{
    public class ViewInvoiceLicenseReviewPage : UserControl
    {
        private ElementHost _host;
        private InvoiceLicenseReviewView _view;

        public ViewInvoiceLicenseReviewPage()
        {
            Dock = DockStyle.Fill;
            _view = new InvoiceLicenseReviewView();
            _host = new ElementHost { Dock = DockStyle.Fill, Child = _view };
            Controls.Add(_host);
        }

        /// <summary>True while the page has staged classification decisions that have not been
        /// saved yet — checked by MainForm before navigating away from or closing over this page.</summary>
        public bool HasUnsavedChanges => _view?.HasUnsavedChanges ?? false;

        /// <summary>Prompts the user if there are unsaved changes. Returns true if it's safe to
        /// proceed with navigating away (nothing pending, or the user confirmed discarding it).</summary>
        public bool ConfirmNavigateAway() => _view?.ConfirmNavigateAway() ?? true;

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
