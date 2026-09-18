using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Helper methods for SetRepository email functionality
    /// </summary>
    public static class SetRepositoryEmailHelpers
    {
        /// <summary>
        /// Builds HTML table for item list in deployment email
        /// Matches the same item data displayed in ViewSetDetailPage
        /// </summary>
        public static string BuildItemListTableHtml(List<SetDetailRequestDto> setRequests)
        {
            if (setRequests == null || setRequests.Count == 0)
            {
                return "<div style=\"font-family:Segoe UI, Arial, sans-serif; font-size:13px; color:#6b7280;\">No items found.</div>";
            }

            var sb = new StringBuilder();

            sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"border-collapse:collapse;\">");
            sb.Append("<tr>");
            sb.Append("<th align=\"left\" style=\"font-family:Segoe UI, Arial, sans-serif; font-size:12px; color:#374151; padding:10px; border:1px solid #e5e7eb; background-color:#f9fafb;\">Item</th>");
            sb.Append("<th align=\"left\" style=\"font-family:Segoe UI, Arial, sans-serif; font-size:12px; color:#374151; padding:10px; border:1px solid #e5e7eb; background-color:#f9fafb;\">Model</th>");
            sb.Append("<th align=\"right\" style=\"font-family:Segoe UI, Arial, sans-serif; font-size:12px; color:#374151; padding:10px; border:1px solid #e5e7eb; background-color:#f9fafb;\">Qty</th>");
            sb.Append("<th align=\"left\" style=\"font-family:Segoe UI, Arial, sans-serif; font-size:12px; color:#374151; padding:10px; border:1px solid #e5e7eb; background-color:#f9fafb;\">Serial</th>");
            sb.Append("</tr>");

            foreach (var r in setRequests.Where(x => x != null))
            {
                var itemName = WebUtility.HtmlEncode(r.ItemName ?? string.Empty);
                var model = WebUtility.HtmlEncode(r.ModelNumber ?? string.Empty);
                var qty = r.Quantity;
                var serial = string.IsNullOrWhiteSpace(r.SerialNumber) ? "&mdash;" : WebUtility.HtmlEncode(r.SerialNumber.Trim());

                sb.Append("<tr>");
                sb.Append($"<td style=\"font-family:Segoe UI, Arial, sans-serif; font-size:13px; color:#111827; padding:10px; border:1px solid #e5e7eb;\">{itemName}</td>");
                sb.Append($"<td style=\"font-family:Segoe UI, Arial, sans-serif; font-size:13px; color:#111827; padding:10px; border:1px solid #e5e7eb;\">{model}</td>");
                sb.Append($"<td align=\"right\" style=\"font-family:Segoe UI, Arial, sans-serif; font-size:13px; color:#111827; padding:10px; border:1px solid #e5e7eb;\">{qty}</td>");
                sb.Append($"<td style=\"font-family:Segoe UI, Arial, sans-serif; font-size:13px; color:#111827; padding:10px; border:1px solid #e5e7eb;\">{serial}</td>");
                sb.Append("</tr>");
            }

            sb.Append("</table>");
            return sb.ToString();
        }

        /// <summary>
        /// Normalizes QR code path to safe URL for email
        /// Only returns http/https URLs, not local file paths
        /// </summary>
        public static string NormalizeQrCodeUrl(string qrImagePath)
        {
            if (string.IsNullOrWhiteSpace(qrImagePath))
                return string.Empty;

            if (Uri.TryCreate(qrImagePath.Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return uri.ToString();
            }

            // Do NOT emit local file paths into email templates
            return string.Empty;
        }
    }
}
