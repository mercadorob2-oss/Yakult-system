using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Wpf.Portal;

namespace Yakult.Inventory.App.Services
{
    /// <summary>Session-wide message polling continues even when chat and dashboard windows close.</summary>
    public sealed class DeviceMessengerNotifications
    {
        public static readonly DeviceMessengerNotifications Instance = new DeviceMessengerNotifications();
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        private readonly DispatcherTimer _timer;
        private readonly HashSet<long> _announced = new HashSet<long>();
        private int _userId, _generation;
        private bool _polling;
        private Action _closePopup;
        internal Func<bool> IsMessengerReading = () => WpfDeviceMessengerWindow.IsReadingMessages;
        internal Func<string,string,Action,Action> ShowPopup = (title, message, activate) =>
            ToastNotificationService.Instance.ShowActionableInfo(title, message, activate);
        public int PendingCount { get; private set; }
        public event Action<int> PendingCountChanged;
        private DeviceMessengerNotifications()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timer.Tick += async (_,__) => await RefreshAsync();
            AppSession.SessionCleared += Stop;
            System.Windows.Forms.Application.ApplicationExit += (_,__) => Stop();
        }
        public void Start()
        {
            if (!_dispatcher.CheckAccess()) { _dispatcher.BeginInvoke(new Action(Start)); return; }
            if (!AppSession.IsLoggedIn || AppSession.CurrentUserId <= 0) return;
            if (_userId != AppSession.CurrentUserId)
            {
                Stop(); _userId = AppSession.CurrentUserId;
            }
            _timer.Start();
            _ = RefreshAsync();
        }
        public void Stop()
        {
            if (!_dispatcher.CheckAccess()) { _dispatcher.BeginInvoke(new Action(Stop)); return; }
            _timer.Stop(); _generation++; _userId = 0; _announced.Clear();
            ClosePopup();
            SetCount(0);
        }
        private void ClosePopup()
        {
            var close = _closePopup; _closePopup = null;
            try { close?.Invoke(); } catch (Exception ex) { Debug.WriteLine("[DeviceMessengerNotifications] Close popup: " + ex.Message); }
        }
        private void SetCount(int count)
        {
            if (PendingCount == count) return;
            PendingCount = count; PendingCountChanged?.Invoke(count);
        }
        // Remember every observed ID, including messages seen while chat is active or popups are disabled.
        // Dismissing a message, changing the count, or navigating between windows must not replay it.
        internal List<TemporaryDeviceMessage> ObserveIncoming(IReadOnlyList<TemporaryDeviceMessage> incoming)
        {
            SetCount(incoming.Count);
            return incoming.Where(x => _announced.Add(x.MessageId)).ToList();
        }
        public async Task RefreshAsync()
        {
            if (!_dispatcher.CheckAccess()) { _dispatcher.BeginInvoke(new Action(async () => await RefreshAsync())); return; }
            if (_polling || _userId <= 0 || !AppSession.IsLoggedIn || AppSession.CurrentUserId != _userId) return;
            _polling = true;
            var generation = _generation; var userId = _userId;
            try
            {
                var incoming = await new DeviceMessengerRepository().GetPendingIncomingAsync();
                if (generation != _generation || userId != AppSession.CurrentUserId || !AppSession.IsLoggedIn) return;
                var fresh = ObserveIncoming(incoming);
                if (fresh.Count == 0 || !AppSession.NotificationsEnabled || IsMessengerReading()) return;
                var latest = fresh[fresh.Count-1];
                var senders = fresh.Select(x => x.SenderMachine + "\n" + x.SenderUser).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                var title = senders > 1 ? fresh.Count + " new messages from " + senders + " people"
                    : fresh.Count == 1 ? "Message from " + latest.SenderUser : fresh.Count + " messages from " + latest.SenderUser;
                var preview = (latest.Body ?? "").Replace("\r", " ").Replace("\n", " ");
                if (preview.Length > 120) preview = preview.Substring(0,120) + "…";
                var peer = new MessengerDevice { MachineName = latest.SenderMachine, UserName = latest.SenderUser };
                ClosePopup();
                _closePopup = ShowPopup(title,
                    latest.SenderMachine + " • " + preview + "\nClick to open conversation", () =>
                    {
                        if (generation == _generation && AppSession.IsLoggedIn && AppSession.CurrentUserId == userId)
                            WpfDeviceMessengerWindow.OpenMessenger(peer);
                    });
            }
            catch (Exception ex) { Debug.WriteLine("[DeviceMessengerNotifications] " + ex.Message); }
            finally { _polling = false; }
        }
    }
}
