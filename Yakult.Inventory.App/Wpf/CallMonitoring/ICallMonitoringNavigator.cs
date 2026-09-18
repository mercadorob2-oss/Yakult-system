using System;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    /// <summary>
    /// Defines navigation operations that WPF Call Monitoring workspaces can
    /// request from the host shell. Decouples workspaces from knowing about
    /// WinForms view switching or specific Action delegate shapes.
    /// </summary>
    public interface ICallMonitoringNavigator
    {
        void OpenDashboard();
        void OpenTicketWorkspace();
        void OpenDisplayMode();
        void OpenReports();
        void OpenMobileUpdates();
        void OpenEmailNotifications();
        void OpenDiagnostics();
        void OpenProfiles();

        /// <summary>Opens the incoming portal tickets view.</summary>
        void OpenIncomingTickets();

        /// <summary>Opens the ticket details dialog for the given ticket id.</summary>
        void OpenTicket(int ticketId);

        /// <summary>Navigates the email notification workspace to a deep link target.</summary>
        void OpenEmailDeepLink(
            WpfEmailNotificationDeepLinkTarget target,
            int? deptId = null,
            int? branchId = null,
            string emailLogSearch = null);

        /// <summary>Triggers an SMTP connectivity test.</summary>
        void OpenSmtpTest();
    }
}
