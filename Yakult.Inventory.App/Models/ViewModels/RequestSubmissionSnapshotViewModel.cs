using System;
using System.Collections.Generic;
using System.Linq;

namespace Yakult.Inventory.App.Models.ViewModels
{
    /// <summary>
    /// Snapshot of a request submission captured at the moment of submission.
    /// Used for confirmation view (Google Forms style).
    /// IMPORTANT: This captures the EXACT user input, NOT queried from database later.
    /// </summary>
    public class RequestSubmissionSnapshotViewModel
    {
        /// <summary>
        /// Summary of cartridge model(s) requested.
        /// For single model: "HP 680 Black"
        /// For multiple models: "3 models"
        /// </summary>
        public string CartridgeModel { get; set; }

        /// <summary>
        /// Total quantity requested across all models.
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Primary cartridge condition (from first item if multiple).
        /// </summary>
        public string CartridgeCondition { get; set; }

        /// <summary>
        /// Detailed list of all requested items (for multi-model submissions).
        /// </summary>
        public List<CartridgeRequestItemViewModel> Items { get; set; }

        /// <summary>
        /// Name of the employee for whom the request was created.
        /// (The employee who will receive the cartridges, not the IT staff who submitted)
        /// </summary>
        public string RequesterName { get; set; }

        /// <summary>
        /// Position of the employee.
        /// </summary>
        public string RequesterPosition { get; set; }

        /// <summary>
        /// Employee number (for tracking).
        /// </summary>
        public string RequesterEmployeeNumber { get; set; }

        /// <summary>
        /// Destination company name (looked up at submission time).
        /// </summary>
        public string Company { get; set; }

        /// <summary>
        /// Destination branch name (looked up at submission time).
        /// </summary>
        public string Branch { get; set; }

        /// <summary>
        /// Destination department name (looked up at submission time).
        /// </summary>
        public string Department { get; set; }

        /// <summary>
        /// Fulfillment method: "PICKUP" or "DELIVERY"
        /// </summary>
        public string FulfillmentMethod { get; set; }

        /// <summary>
        /// Additional remarks entered by user.
        /// </summary>
        public string AdditionalRemarks { get; set; }

        /// <summary>
        /// Timestamp when the request was submitted.
        /// </summary>
        public DateTime SubmissionDate { get; set; }

        /// <summary>
        /// List of Request IDs created from this submission.
        /// One request ID per model in multi-model submissions.
        /// </summary>
        public List<int> RequestIds { get; set; }

        /// <summary>
        /// User who submitted the request (IT staff/developer).
        /// Different from RequesterName (the employee receiving cartridges).
        /// </summary>
        public string SubmittedByUser { get; set; }

        /// <summary>
        /// Display status (always "Submitted" for snapshots).
        /// </summary>
        public string Status => "Submitted";

        /// <summary>
        /// Full destination string for display.
        /// Format: "Company → Branch, Department"
        /// </summary>
        public string FullDestination => $"{Company} → {Branch}, {Department}";

        /// <summary>
        /// Summary line for confirmation.
        /// Example: "3 models, Total: 5 items"
        /// </summary>
        public string Summary
        {
            get
            {
                var modelCount = Items?.Count ?? 1;
                var modelText = modelCount == 1 ? "1 model" : $"{modelCount} models";
                return $"{modelText}, Total: {Quantity} items";
            }
        }

        /// <summary>
        /// Formatted submission date for display.
        /// </summary>
        public string FormattedSubmissionDate => SubmissionDate.ToString("yyyy-MM-dd HH:mm");

        /// <summary>
        /// Comma-separated list of request IDs.
        /// </summary>
        public string RequestIdsList => RequestIds != null ? string.Join(", ", RequestIds) : "N/A";

        public RequestSubmissionSnapshotViewModel()
        {
            Items = new List<CartridgeRequestItemViewModel>();
            RequestIds = new List<int>();
            SubmissionDate = DateTime.Now;
        }
    }
}
