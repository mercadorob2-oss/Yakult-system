using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Checks the home dashboard's expiry/mobile-update data (the same three queries the
    /// notification bell already shows on demand — see ExpiryNotificationRepository) once
    /// shortly after startup, then hourly, and raises events for items that have newly
    /// appeared since the last check. MainForm forwards each one to ToastNotificationService.
    ///
    /// Hourly (not the Request Portal poller's 60s) because expiry dates don't change
    /// minute-to-minute — a tighter interval would just re-check the same unchanged rows.
    ///
    /// The very first check is a silent baseline: this data is a live snapshot of "still true
    /// right now" state (an item overdue by 357 days was just as true yesterday), not a stream
    /// of discrete events like the Request Portal's notifications — so opening the app and
    /// immediately toasting every currently-overdue item (there can easily be 20+) would be a
    /// flood, not a notification. The first poll records what's currently overdue/expiring
    /// without raising any events; only items that appear on a later poll and weren't in that
    /// baseline actually toast.
    /// </summary>
    public sealed class HomeNotificationPoller : IDisposable
    {
        private readonly Timer _timer;
        private readonly Timer _startupTimer;
        private readonly HashSet<string> _shownKeys = new HashSet<string>();
        private bool _baselineEstablished;
        private bool _disposed;

        private const int PollIntervalMs = 60 * 60 * 1000; // hourly
        private const int StartupDelayMs = 2_000;

        public event Action<IReadOnlyList<ExpiryNotificationItem>>       NewLicenseExpiryItemsArrived;
        public event Action<IReadOnlyList<ExpiryNotificationItem>>       NewWarrantyExpiryItemsArrived;
        public event Action<IReadOnlyList<MobileUpdateNotificationItem>> NewMobileUpdatesArrived;

        public HomeNotificationPoller()
        {
            _startupTimer = new Timer { Interval = StartupDelayMs };
            _startupTimer.Tick += async (s, e) =>
            {
                _startupTimer.Stop();
                await PollAsync();
                _timer.Start();
            };

            _timer = new Timer { Interval = PollIntervalMs };
            _timer.Tick += async (s, e) => await PollAsync();

            _startupTimer.Start();
        }

        private async Task PollAsync()
        {
            try
            {
                var cs = DatabaseConfig.ConnectionString;
                if (string.IsNullOrWhiteSpace(cs)) return;

                var repo = new ExpiryNotificationRepository();

                using (var con = new SqlConnection(cs))
                {
                    await con.OpenAsync();

                    var license  = await repo.GetLicenseExpiryAsync(con);
                    var warranty = await repo.GetWarrantyExpiryAsync(con);
                    var mobile   = await repo.GetUnprocessedMobileUpdatesAsync(con);

                    if (!_baselineEstablished)
                    {
                        // First check ever: seed the "already known" set from everything
                        // currently overdue/expiring/unprocessed, but don't toast any of it.
                        Baseline(license,  i => $"L:{i.ItemName}:{i.ItemType}");
                        Baseline(warranty, i => $"W:{i.ItemName}:{i.ItemType}");
                        Baseline(mobile,   i => $"M:{i.SerialNumber}:{i.CreatedAt.Ticks}");
                        _baselineEstablished = true;
                        return;
                    }

                    var newLicense = Dedup(license, i => $"L:{i.ItemName}:{i.ItemType}");
                    if (newLicense.Count > 0)
                        NewLicenseExpiryItemsArrived?.Invoke(newLicense);

                    var newWarranty = Dedup(warranty, i => $"W:{i.ItemName}:{i.ItemType}");
                    if (newWarranty.Count > 0)
                        NewWarrantyExpiryItemsArrived?.Invoke(newWarranty);

                    var newMobile = Dedup(mobile, i => $"M:{i.SerialNumber}:{i.CreatedAt.Ticks}");
                    if (newMobile.Count > 0)
                        NewMobileUpdatesArrived?.Invoke(newMobile);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HomeNotificationPoller] Poll failed: {ex}");
            }
        }

        private void Baseline<T>(List<T> items, Func<T, string> keySelector)
        {
            foreach (var item in items)
                _shownKeys.Add(keySelector(item));
        }

        private List<T> Dedup<T>(List<T> items, Func<T, string> keySelector)
        {
            var result = new List<T>();
            foreach (var item in items)
            {
                var key = keySelector(item);
                if (_shownKeys.Contains(key)) continue;
                _shownKeys.Add(key);
                result.Add(item);
            }
            return result;
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
