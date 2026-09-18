using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Polls dbo.Notification on a 60-second interval and raises new unread records via
    /// NewNotificationsArrived. The poller only fetches/tracks notifications — it has no
    /// knowledge of how they're displayed; RequesterPortalForm's handler forwards each one to
    /// ToastNotificationService, which owns the actual popup.
    ///
    /// First poll: fires 1 second after start so the ShowDialog message loop is
    /// fully running before any toast is created.
    /// Subsequent polls: every 60 seconds.
    ///
    /// Duplicate suppression: NotificationIds are tracked in a per-session HashSet.
    /// Navigation: clicking a toast fires onNavigate(referenceId).
    /// </summary>
    public sealed class DesktopNotificationPoller : IDisposable
    {
        private readonly Timer        _timer;
        private readonly Timer        _startupTimer;
        private readonly HashSet<int> _shownIds = new HashSet<int>();
        private readonly string       _portalKey;
        private readonly bool         _silentFirstPoll;
        private          bool         _baselined;
        private          bool         _disposed;

        private const int PollIntervalMs = 60_000;
        private const int StartupDelayMs = 1_000;  // wait for message loop to stabilize

        /// <param name="portalKey">
        /// Which portal's dbo.Notification rows to poll. Defaults to the Requester Portal so the
        /// existing RequesterPortalForm call site is unchanged; the Inventory System main window
        /// passes NotificationType.InventorySystemKey for its "Activity" toasts.
        /// </param>
        /// <param name="silentFirstPoll">
        /// When true, the first poll only records what is currently unread (baseline) and raises
        /// nothing — so opening the app doesn't toast a backlog. Only notifications that appear
        /// on a later poll are surfaced.
        /// </param>
        public DesktopNotificationPoller(string portalKey = NotificationType.RequesterPortalKey, bool silentFirstPoll = false)
        {
            _portalKey       = portalKey;
            _silentFirstPoll = silentFirstPoll;

            // One-shot startup timer — fires once after 1 s, then hands off to the main timer
            _startupTimer = new Timer { Interval = StartupDelayMs };
            _startupTimer.Tick += (s, e) =>
            {
                _startupTimer.Stop();
                Poll();
                _timer.Start();
            };

            _timer = new Timer { Interval = PollIntervalMs };
            _timer.Tick += (s, e) => Poll();

            _startupTimer.Start();
        }

        // Fires with newly-seen unread notifications each poll cycle.
        // Subscribers are responsible for displaying toasts on the UI thread.
        public event Action<IReadOnlyList<NotificationDto>> NewNotificationsArrived;

        private void Poll()
        {
            if (!AppSession.IsLoggedIn) return;
            if (!AppSession.NotificationsEnabled) return;

            try
            {
                var repo          = new NotificationRepository();
                // dbo.Notification is shared by every portal — only surface this poller's own rows.
                var notifications = repo.GetByUserAndPortal(
                    AppSession.CurrentUserId, _portalKey, 50);

                var newNotifications = new List<NotificationDto>();
                foreach (var n in notifications)
                {
                    if (n.IsRead)                              continue;
                    if (_shownIds.Contains(n.NotificationId)) continue;

                    _shownIds.Add(n.NotificationId);
                    newNotifications.Add(n);
                    Debug.WriteLine($"[DesktopNotificationPoller] New notification: {n.NotificationType} — {n.Title}");
                }

                if (_silentFirstPoll && !_baselined)
                {
                    _baselined = true;   // _shownIds now holds every currently-unread id — don't toast them
                    return;
                }

                if (newNotifications.Count > 0)
                    NewNotificationsArrived?.Invoke(newNotifications.AsReadOnly());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DesktopNotificationPoller] Poll failed: {ex}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _startupTimer?.Stop();
            _startupTimer?.Dispose();
            _timer?.Stop();
            _timer?.Dispose();
        }
    }
}
