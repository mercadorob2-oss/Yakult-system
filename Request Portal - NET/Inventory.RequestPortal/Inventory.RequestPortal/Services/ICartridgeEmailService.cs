using System.Collections.Generic;
using System.Threading.Tasks;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Models.ViewModels;

namespace Inventory.RequestPortal.Services
{
    /// <summary>
    /// Sends cartridge request notification emails from the web portal.
    /// Recipient resolution mirrors the WinForms CartridgeRequestEmailService:
    ///   1. Employee primary email
    ///   2. Fallback: branch email
    /// </summary>
    public interface ICartridgeEmailService
    {
        /// <summary>
        /// Sends a "request submitted" notification email.
        /// Never throws – errors are logged internally.
        /// </summary>
        Task SendRequestSubmittedAsync(
            int empId,
            string employeeName,
            string branchName,
            string departmentName,
            string fulfillmentMethod,
            string cartridgeCondition,
            List<CartridgeRequestItemViewModel> items,
            List<int> requestIds,
            string additionalRemarks);

        /// <summary>
        /// Renders the subject + HTML body for a Set-level cartridge fulfillment notification,
        /// without sending it. Backs the Send Notifications page's "Preview HTML" action and the
        /// Send action (which renders, then sends, the exact same content).
        /// PORTED FROM: Yakult.Inventory.App/Forms/CartridgeManagement/CartridgeManagementForm.cs
        /// (GenerateEmailContentForSet), condensed to the data already available from
        /// FulfilledSetNotificationDto + FulfilledCartridgeDetailDto rather than re-querying every
        /// desktop placeholder source table.
        /// </summary>
        (string Subject, string Body) BuildFulfillmentNotificationEmail(
            FulfilledSetNotificationDto row,
            FulfilledCartridgeDetailDto? detail,
            string? notes);

        /// <summary>
        /// Sends an already-rendered fulfillment notification email to the requester (or branch
        /// fallback) identified by empId. Returns an error message on failure, or null on success —
        /// mirrors desktop's SendFulfillmentEmailWithPreview return contract. Never throws.
        /// </summary>
        Task<string?> SendRenderedEmailAsync(int empId, string subject, string htmlBody);
    }
}
