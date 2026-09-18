using System;
using System.Windows.Threading;
using ToastNotifications;
using ToastNotifications.Core;
using ToastNotifications.Lifetime;
using ToastNotifications.Position;
using Yakult.Inventory.App.Wpf.Toast;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// The app's single in-app notification popup, backed by the ToastNotifications WPF library
    /// (core only — see YakultToastNotification for why the ToastNotifications.Messages package
    /// isn't used). This is the only popup mechanism in the app: it replaced the native Windows
    /// balloon tip (WindowsToastService, removed) and the Request Portal's hand-rolled bubble
    /// popup for Request Portal notifications, and drives the home dashboard's expiry/mobile
    /// update toasts too (see HomeNotificationPoller).
    ///
    /// Bottom-right slide corner, 5s auto-dismiss, hover-to-pause, and a 3-toast stacking cap
    /// mirror the visual/interaction behaviour of the pre-existing CallMonitoring ToastNotification
    /// class (Forms\CallMonitoring\ToastNotification.cs, still used as-is by the unrelated Call
    /// Monitoring feature) so the on-screen experience is consistent across the app.
    /// </summary>
    public sealed class ToastNotificationService : INotificationService
    {
        public static readonly ToastNotificationService Instance = new ToastNotificationService();

        private const int    AutoDismissSeconds = 5;
        private const int    MaxStackedToasts    = 3;
        private const double ToastWidth          = 360;
        private const double ScreenOffset        = 20;

        private readonly Dispatcher _dispatcher;
        private readonly Notifier   _notifier;

        private ToastNotificationService()
        {
            // ToastNotifications' Notifier.CreateConfiguration() unconditionally reads
            // System.Windows.Application.Current.Dispatcher the first time a toast is shown
            // (Notify() -> Configure() -> CreateConfiguration(), lazy — it does not run at
            // construction). This app hosts WPF purely via ElementHost/standalone Windows and
            // never creates a System.Windows.Application (Application.Current is always null —
            // see CLAUDE.md's WPF/WinForms interop notes), so that read throws
            // NullReferenceException unless an Application already exists. Create one eagerly
            // here, before the Notifier is ever asked to show anything, with ShutdownMode locked
            // down immediately so no WPF window closing can ever tear down the whole WinForms
            // process (the same hazard documented for WpfPortalTourWindow in CLAUDE.md, just
            // guarded proactively instead of reactively after Show()).
            if (System.Windows.Application.Current == null)
                _ = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };

            // Dispatcher.CurrentDispatcher (not Application.Current.Dispatcher) is still the
            // right way to obtain this thread's dispatcher for our own config below.
            _dispatcher = Dispatcher.CurrentDispatcher;

            _notifier = new Notifier(cfg =>
            {
                cfg.PositionProvider = new PrimaryScreenPositionProvider(
                    corner: Corner.BottomRight,
                    offsetX: ScreenOffset,
                    offsetY: ScreenOffset);

                cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(
                    notificationLifetime: TimeSpan.FromSeconds(AutoDismissSeconds),
                    maximumNotificationCount: MaximumNotificationCount.FromCount(MaxStackedToasts));

                cfg.Dispatcher = _dispatcher;

                cfg.DisplayOptions.TopMost = true;
                cfg.DisplayOptions.Width   = ToastWidth;
            });
        }

        public void ShowSuccess(string title, string message) => Show(title, message, YakultToastKind.Success);
        public void ShowInfo(string title, string message)    => Show(title, message, YakultToastKind.Info);
        public void ShowWarning(string title, string message) => Show(title, message, YakultToastKind.Warning);
        public void ShowError(string title, string message)   => Show(title, message, YakultToastKind.Error);

        private void Show(string title, string message, YakultToastKind kind) =>
            Dispatch(() => _notifier.Notify(() =>
                new YakultToastNotification(title, message ?? string.Empty, kind, DismissibleOptions())));

        private static MessageOptions DismissibleOptions() => new MessageOptions
        {
            ShowCloseButton      = true,
            FreezeOnMouseEnter   = true,
            UnfreezeOnMouseLeave = true,
        };

        // Show the toast synchronously when already on the UI thread (the common case — the
        // poller and both notification testers already marshal onto the UI thread before
        // calling in) so nothing adds latency between the event and the popup appearing.
        private void Dispatch(Action action)
        {
            if (_dispatcher.CheckAccess()) action();
            else _dispatcher.BeginInvoke(action);
        }
    }
}
