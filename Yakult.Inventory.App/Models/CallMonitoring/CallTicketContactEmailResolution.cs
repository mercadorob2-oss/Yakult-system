using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    /// <summary>
    /// The ticket-scoped contact resolved before a Call IT ticket is created.
    /// The address is copied to dbo.CallTicket.ContactEmail; it never changes
    /// shared employee, branch, or department email configuration.
    /// </summary>
    public sealed class CallTicketContactEmailResolution
    {
        public string Email { get; set; }
        public string Source { get; set; }

        public bool HasEmail => !string.IsNullOrWhiteSpace(Email);
    }
}
