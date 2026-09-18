using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace Yakult.Inventory.App.Wpf.Receipt
{
    /// <summary>
    /// WinForms host for the WPF ReceiptSetsView. This is the drop-in replacement for the legacy
    /// WinForms ViewReceiptsPage (Pages\Receipt\ViewReceiptsPage.cs) at its single call site in
    /// MainForm.cs, preserving the GoToManageSetsRequested event contract.
    /// </summary>
    public class ReceiptSetsWpfHost : UserControl
    {
        private ElementHost _host;
        private ReceiptSetsView _view;
        private bool _disposed;

        public event Action GoToManageSetsRequested;

        public ReceiptSetsWpfHost()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(240, 242, 246);

            _view = new ReceiptSetsView();
            _view.GoToManageSetsRequested += () => GoToManageSetsRequested?.Invoke();

            _host = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = _view
            };

            Controls.Add(_host);
            HandleDestroyed += (s, e) => DisposeHost();
        }

        private void DisposeHost()
        {
            if (_disposed) return;
            _disposed = true;
            try { _host?.Dispose(); } catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeHost();
            base.Dispose(disposing);
        }
    }
}
