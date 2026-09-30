using System;
using System.Threading;
using System.Windows.Forms;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Services.Gateway
{
    /// <summary>
    /// Shows GatewayReauthDialog when the gateway rejects the session. Installed once
    /// by MainForm, which lives for the whole app. The dialog is queued with
    /// BeginInvoke, never shown synchronously, because the failing call may be a
    /// blocking gateway call on the UI thread.
    /// </summary>
    public static class GatewaySessionGuard
    {
        private static Control _ui;
        private static int _showing;

        public static void Install(Control ui)
        {
            _ui = ui;
            GatewayClient.SessionExpired -= OnSessionExpired;
            GatewayClient.SessionExpired += OnSessionExpired;
        }

        private static void OnSessionExpired(object sender, EventArgs e)
        {
            var ui = _ui;
            if (ui == null || ui.IsDisposed || !ui.IsHandleCreated || !AppSession.IsLoggedIn)
                return;

            try { ui.BeginInvoke(new Action(ShowReauth)); }
            catch (InvalidOperationException) { /* handle destroyed while closing */ }
        }

        private static void ShowReauth()
        {
            if (Interlocked.Exchange(ref _showing, 1) == 1) return;
            try
            {
                if (!AppSession.IsLoggedIn || string.IsNullOrEmpty(GatewayClient.LoginName)) return;

                using (var dialog = new Pages.User.GatewayReauthDialog())
                {
                    if (dialog.ShowDialog() != DialogResult.OK)
                        GatewayClient.AllowSessionExpiredNotice(); // "Later": ask again on the next failed call
                }
            }
            finally
            {
                Interlocked.Exchange(ref _showing, 0);
            }
        }
    }
}
