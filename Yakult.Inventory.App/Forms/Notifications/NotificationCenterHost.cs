using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using Yakult.Inventory.App.WPF.NotificationCenter.Views;

namespace Yakult.Inventory.App.Forms.Notifications
{
    /// <summary>
    /// WinForms UserControl that hosts the WPF NotificationCenterView via ElementHost.
    /// </summary>
    public sealed class NotificationCenterHost : UserControl
    {
        private readonly ElementHost           _host;
        private readonly NotificationCenterView _view;

        // "View All" navigation — wired by MainForm (section-level).
        public Action OnViewAllLicense  { get; set; }
        public Action OnViewAllWarranty { get; set; }
        public Action OnViewAllMobile   { get; set; }
        public Action OnClose           { get; set; }

        // Row-level navigation — wired by MainForm after construction.
        // License: (searchQuery, destination) where destination is "Renewal" or "Items".
        public Action<string, string> OnNavigateToLicenseItem  { get; set; }
        public Action<string>         OnNavigateToWarrantyItem { get; set; }
        // Mobile Updates page has no filter API yet; receives the SerialNumber for future use.
        public Action<string>         OnNavigateToMobileItem   { get; set; }
        // Activity row click: (notificationType, referenceId, sourceLabel).
        public Action<string, int, string> OnNavigateToActivityItem { get; set; }
        // Raised after the Activity list / unread count changes — MainForm refreshes the bell badge.
        public Action                 OnActivityChanged        { get; set; }

        /// <summary>Unread count of the InventorySystem "Activity" feed — feeds MainForm's bell badge.</summary>
        public int UnreadActivityCount => _view?.ViewModel?.UnreadActivityCount ?? 0;

        public NotificationCenterHost()
        {
            Dock      = DockStyle.Fill;
            BackColor = Color.White;

            _view = new NotificationCenterView();

            // Bridge WPF commands/actions → WinForms message loop via BeginInvoke.
            _view.ViewModel.OnViewAllLicense  = () => BeginInvoke(new Action(() => OnViewAllLicense?.Invoke()));
            _view.ViewModel.OnViewAllWarranty = () => BeginInvoke(new Action(() => OnViewAllWarranty?.Invoke()));
            _view.ViewModel.OnViewAllMobile   = () => BeginInvoke(new Action(() => OnViewAllMobile?.Invoke()));
            _view.ViewModel.OnClose           = () => BeginInvoke(new Action(() => OnClose?.Invoke()));

            // Row-level: lambdas capture the host's delegate properties by reference,
            // so they read the values set by MainForm's object initializer.
            _view.ViewModel.OnNavigateToLicenseItem  = (name, dest) => BeginInvoke(new Action(() => OnNavigateToLicenseItem?.Invoke(name, dest)));
            _view.ViewModel.OnNavigateToWarrantyItem = name => BeginInvoke(new Action(() => OnNavigateToWarrantyItem?.Invoke(name)));
            _view.ViewModel.OnNavigateToMobileItem   = sn   => BeginInvoke(new Action(() => OnNavigateToMobileItem?.Invoke(sn)));
            _view.ViewModel.OnNavigateToActivityItem = (type, refId, src) => BeginInvoke(new Action(() => OnNavigateToActivityItem?.Invoke(type, refId, src)));
            _view.ViewModel.OnActivityChanged        = () => BeginInvoke(new Action(() => OnActivityChanged?.Invoke()));

            _host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = _view
            };

            Controls.Add(_host);
        }

        /// <summary>
        /// Runs the three SQL queries and populates the WPF card lists.
        /// Awaitable — returns after the UI is updated.
        /// </summary>
        public Task LoadAsync() => _view.ViewModel.LoadAsync();

        /// <summary>
        /// Updates the "Unprocessed: N" label on the Mobile Updates card
        /// without re-querying the database.  Called by the connection poller.
        /// </summary>
        public void SetMobilePendingCount(int count)
            => _view.ViewModel.SetMobilePendingCount(count);

        /// <summary>
        /// Clears WPF keyboard focus before the panel is hidden.
        /// Prevents the ElementHost focus-restoration crash documented in CLAUDE.md.
        /// </summary>
        public void PrepareForHide()
        {
            try
            {
                _view.Dispatcher.Invoke(() => Keyboard.ClearFocus());
            }
            catch { /* best-effort; hiding still proceeds */ }
        }

        private bool _disposed;
        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                try { _host?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
