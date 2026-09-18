using System.Collections.Generic;
using System.Threading.Tasks;
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
    }
}
