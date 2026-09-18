using System;

namespace Yakult.Inventory.App.Models.ViewModels
{
    /// <summary>
    /// ViewModel for displaying portal request status (read-only).
    /// Used in "My Requests" tab to show historical requests.
    /// </summary>
    public class PortalRequestStatusViewModel
    {
        /// <summary>
        /// Request ID.
        /// </summary>
        public int ReqId { get; set; }

        /// <summary>
        /// Date when request was created.
        /// </summary>
        public DateTime DateRequested { get; set; }

        /// <summary>
        /// Request status.
        /// Values: "Under Review", "Received", "Replaced", "Completed", "Cancelled"
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Requested quantity.
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Date when request record was created in database.
        /// </summary>
        public DateTime DateCreated { get; set; }

        /// <summary>
        /// Distribution method (parsed from Description field).
        /// Values: "PICKUP" or "DELIVERY"
        /// </summary>
        public string DistributionMethod { get; set; }

        /// <summary>
        /// Cartridge condition (parsed from Remarks field).
        /// Values: "With Cartridge" or "Without Cartridge"
        /// </summary>
        public string CartridgeCondition { get; set; }

        /// <summary>
        /// Full description from database (raw).
        /// Format: "[PORTAL] [MODEL:XXX] PICKUP → Branch, Dept"
        /// </summary>
        public string FullDescription { get; set; }

        /// <summary>
        /// Full remarks from database (raw).
        /// </summary>
        public string FullRemarks { get; set; }

        /// <summary>
        /// Item name.
        /// </summary>
        public string ItemName { get; set; }

        /// <summary>
        /// Item model number.
        /// </summary>
        public string ItemModelNumber { get; set; }

        /// <summary>
        /// Name of employee who will receive the cartridges.
        /// </summary>
        public string DestinationEmployeeName { get; set; }

        /// <summary>
        /// Destination branch name.
        /// </summary>
        public string DestinationBranch { get; set; }

        /// <summary>
        /// Destination department name.
        /// </summary>
        public string DestinationDepartment { get; set; }

        /// <summary>
        /// Destination company name.
        /// </summary>
        public string DestinationCompany { get; set; }

        /// <summary>
        /// Submission session ID (groups related multi-model requests).
        /// </summary>
        public Guid? SubmissionSessionId { get; set; }

        /// <summary>
        /// Name of user who created the request (IT staff).
        /// </summary>
        public string CreatedByUser { get; set; }

        /// <summary>
        /// Color for status display (WinForms Color).
        /// </summary>
        public System.Drawing.Color StatusColor
        {
            get
            {
                var s = Status?.ToUpperInvariant() ?? string.Empty;
                if (s == "UNFULFILLED")
                    return System.Drawing.Color.FromArgb(220, 53, 69);  // Red
                if (s.Contains("PARTIALLY"))
                    return System.Drawing.Color.FromArgb(255, 153, 0);  // Orange
                switch (s)
                {
                    case "UNDER REVIEW":
                    case "SUBMITTED":
                    case "PROCESSING":
                        return System.Drawing.Color.FromArgb(0, 123, 255); // Blue

                    case "RECEIVED":
                        return System.Drawing.Color.FromArgb(255, 193, 7); // Amber

                    case "REPLACED":
                    case "COMPLETED":
                        return System.Drawing.Color.FromArgb(40, 167, 69); // Green

                    case "CANCELLED":
                    case "REJECTED":
                        return System.Drawing.Color.FromArgb(220, 53, 69); // Red

                    default:
                        return System.Drawing.Color.Gray;
                }
            }
        }

        /// <summary>
        /// Full destination string.
        /// Format: "Company → Branch, Department"
        /// </summary>
        public string FullDestination
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(DestinationCompany)) parts.Add(DestinationCompany);

                var branchDept = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(DestinationBranch)) branchDept.Add(DestinationBranch);
                if (!string.IsNullOrWhiteSpace(DestinationDepartment)) branchDept.Add(DestinationDepartment);

                if (branchDept.Count > 0)
                    parts.Add(string.Join(", ", branchDept));

                return string.Join(" → ", parts);
            }
        }

        /// <summary>
        /// Formatted date for display.
        /// </summary>
        public string FormattedDate => DateRequested.ToString("yyyy-MM-dd HH:mm");

        /// <summary>
        /// Summary line for grid display.
        /// </summary>
        public string Summary => $"{ItemModelNumber} - {ItemName} (Qty: {Quantity})";
    }
}
