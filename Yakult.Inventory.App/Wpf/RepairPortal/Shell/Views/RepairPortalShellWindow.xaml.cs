using System;
using System.Collections.Generic;
using System.Windows;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Wpf.RepairPortal.Detail.Views;
using Yakult.Inventory.App.Wpf.RepairPortal.NewTicketIntake.Views;
using Yakult.Inventory.App.Wpf.RepairPortal.Shell.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Shell.Views
{
    /// <summary>
    /// Top-level shell for the Repair Technician Portal. Opened non-modally from MainForm.cs
    /// (Show() + Closed event), mirroring WpfCallMonitoringShell. Owns the single
    /// RepairPortalShellViewModel instance shared by Gallery/Table/Kanban views.
    /// </summary>
    public partial class RepairPortalShellWindow : Window
    {
        private readonly RepairPortalShellViewModel _vm;
        private Services.RepairPortalNotificationPoller _notifPoller;

        public RepairPortalShellWindow()
        {
            InitializeComponent();

            _vm = new RepairPortalShellViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(this.GetOwnerWindow(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(this.GetOwnerWindow(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.RequestNewTicketDialog += OnRequestNewTicketDialog;
            _vm.RequestOpenDetail += OnRequestOpenDetail;
            _vm.RequestConfirm += (title, msg) => WinForms.MessageBox.Show(this.GetOwnerWindow(), msg, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Warning) == WinForms.DialogResult.Yes;

            Closed += (s, e) =>
            {
                _vm.MarkDisposed();
                _notifPoller?.Dispose();
                _notifPoller = null;
            };

            // Header notification bell — the shell opens a ticket's Detail window when a
            // notification is clicked, reusing the same path as a Gallery/Table/Kanban card.
            NotificationBell.ViewModel.RequestOpenTicket += OnNotificationOpenTicket;

            // WPF can implicitly create Application.Current on first Show() in this hosted-WPF
            // process, defaulting ShutdownMode to OnMainWindowClose — which would tear down the
            // whole app the moment ANY window it considers "main" (Shell, or a Detail window)
            // closes, not just this one. Same fix as WpfPortalTourService.EnsureOverlayCreated():
            // force OnExplicitShutdown once Application.Current exists, so closing any of this
            // module's windows (Shell, Detail, dialogs) only closes that window.
            //
            // That alone doesn't cover every crash path, though. Per the documented "WPF Overlay
            // Crash on Close" chain (see CLAUDE.md): closing a WPF window after a focus-transfer
            // interaction (Alt+Tab away and back, then close) can make Win32 post WM_ACTIVATE to
            // this window's owner, whose WinForms focus-restoration logic then tries to restore
            // keyboard focus into a WPF element whose visual tree is already being torn down —
            // throwing a dispatcher exception. With nothing subscribed to catch it, that exception
            // reaches AppDomain.UnhandledException with IsTerminating=true and the CLR kills the
            // whole process, not just this window. Subscribe once Application.Current exists so
            // that class of exception is swallowed instead of taking the app down.
            Loaded += (s, e) =>
            {
                if (System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    System.Windows.Application.Current.DispatcherUnhandledException -= OnAppDispatcherUnhandledException;
                    System.Windows.Application.Current.DispatcherUnhandledException += OnAppDispatcherUnhandledException;
                }
            };

            Loaded += (s, e) =>
            {
                // Populate the bell/badge from any dbo.Notification rows already unread for this user.
                NotificationBell.ViewModel.LoadAsync();

                if (_notifPoller != null) return;
                _notifPoller = new Services.RepairPortalNotificationPoller();
                _notifPoller.NewNotificationsArrived += list =>
                    Dispatcher.BeginInvoke(new Action(() => OnNewRepairNotifications(list)));
            };
        }

        private void OnNewRepairNotifications(IReadOnlyList<NotificationDto> list)
        {
            if (list == null || list.Count == 0) return;

            NotificationBell.ViewModel.OnNewNotifications(list);

            // Also toast, mirroring RequesterPortalForm.OnNewNotificationsArrived.
            foreach (var n in list)
            {
                if (n.NotificationType == NotificationType.RepairTicketCompleted)
                    Services.ToastNotificationService.Instance.ShowSuccess(n.Title, n.Message);
                else
                    Services.ToastNotificationService.Instance.ShowInfo(n.Title, n.Message);
            }
        }

        private async void OnNotificationOpenTicket(int repairTicketId)
        {
            await _vm.OpenTicketByIdAsync(repairTicketId);
        }

        private static void OnAppDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Swallow rather than crash the whole host process — see the Loaded handler above for
            // why this fires (focus-restoration into a torn-down window after Alt+Tab + close).
            System.Diagnostics.Debug.WriteLine("RepairPortal: swallowed unhandled dispatcher exception: " + e.Exception);
            e.Handled = true;
        }

        private System.Windows.Forms.IWin32Window GetOwnerWindow() => System.Windows.Forms.Form.ActiveForm;

        private void BackToPortalButton_Click(object sender, RoutedEventArgs e)
        {
            // Now lives at the bottom of the hamburger menu — collapse it before leaving.
            // Closing returns control to MainForm.cs, which awaits this window's Closed event
            // and then re-shows the portal selector — same mechanism as the window's [X] button.
            SetSidebarOpen(false);
            Close();
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            SetSidebarOpen(SidebarPanel.Visibility != Visibility.Visible);
        }

        private void SidebarClickOutside_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SetSidebarOpen(false);
        }

        private void SetSidebarOpen(bool open)
        {
            var visibility = open ? Visibility.Visible : Visibility.Collapsed;
            SidebarPanel.Visibility = visibility;
            SidebarClickOutside.Visibility = visibility;
        }

        private void ReportsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetSidebarOpen(false);
            var reportsWindow = new Yakult.Inventory.App.Wpf.RepairPortal.Reports.Views.RepairReportsListWindow
            {
                Owner = this
            };
            reportsWindow.Show();
        }

        private void HistoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetSidebarOpen(false);
            var historyWindow = new Yakult.Inventory.App.Wpf.RepairPortal.Attendance.Views.RepairAttendanceHistoryWindow(_vm.RepositoryForDialogs)
            {
                Owner = this
            };
            historyWindow.Show();
        }

        private void RegenerateThumbnailsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetSidebarOpen(false);
            if (_vm.RegenerateThumbnailsCommand.CanExecute(null))
                _vm.RegenerateThumbnailsCommand.Execute(null);
        }

        private void OnRequestNewTicketDialog()
        {
            var dialog = new NewRepairTicketDialog(_vm.RepositoryForDialogs) { Owner = this };
            dialog.TicketCreated += ticket => _vm.PrependNewTicket(ticket);
            dialog.ShowDialog();
        }

        private async void OnRequestOpenDetail(RepairTicketListItem ticket)
        {
            if (ticket == null) return;

            // Modal (ShowDialog), not Show(): a raw owned WPF Window shown non-modally in this
            // hosted-WPF process never reliably receives real Win32 activation — it renders on top
            // but nothing typed anywhere in it registers (confirmed: this is the only place in the
            // whole app that opened a second WPF Window this way; every other secondary WPF window,
            // e.g. in the ITCM module, uses ShowDialog(), which drives its own native modal message
            // loop and activates properly). ShowDialog() also makes the earlier "duplicate window"
            // concern moot — only one can be open at a time now.
            var detailWindow = new RepairTicketDetailWindow(ticket.RepairTicketId, _vm.RepositoryForDialogs) { Owner = this };
            detailWindow.ShowDialog();
            await _vm.RefreshSingleTicketAsync(ticket.RepairTicketId);
        }
    }
}
