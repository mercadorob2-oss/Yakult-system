using System.Threading.Tasks;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Interface for SMTP email sending operations
    /// </summary>
    public interface ISmtpService
    {
        /// <summary>
        /// Sends an email using the active SMTP profile
        /// </summary>
        /// <param name="to">Recipient email address</param>
        /// <param name="subject">Email subject</param>
        /// <param name="body">Email body content</param>
        /// <param name="isHtml">Whether the body is HTML formatted</param>
        /// <returns>True if email was sent successfully, false otherwise</returns>
        Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = false);
    }
}
