using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// DTO for UnfulfilledCartridgeExchange records.
    /// Tracks returned but unfulfilled cartridge exchanges for audit trail and FIFO fulfillment.
    /// 
    /// Business Rules:
    /// - ReturnedEmptyQty = RequestedQty (auto-populated, read-only)
    /// - UnfulfilledQty = ReturnedEmptyQty - IssuedFullQty
    /// - Status: Pending (awaiting stock) or Fulfilled (completed)
    /// - FIFO: Oldest pending records are fulfilled first
    /// </summary>
    public sealed class UnfulfilledCartridgeExchangeDto
    {
        public int UnfulfilledId { get; set; }
        public int ReqId { get; set; }
        public int EmpId { get; set; }
        public int? BranchId { get; set; }
        public int? DeptId { get; set; }
        public string CartridgeModel { get; set; }
        public int RequestedQty { get; set; }
        public int ReturnedEmptyQty { get; set; }
        public int IssuedFullQty { get; set; }
        public int UnfulfilledQty { get; set; }
        public string Remarks { get; set; }
        public string Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public int CreatedBy { get; set; }
        public DateTime? FulfilledDate { get; set; }
        public int? FulfilledBy { get; set; }
        public string FulfilledRemarks { get; set; }

        // Display fields (joined from related tables)
        public string RequesterName { get; set; }
        public string BranchName { get; set; }
        public string DepartmentName { get; set; }
        public string CreatedByName { get; set; }
        public string FulfilledByName { get; set; }

        // Session grouping (from dbo.Request — NULL for legacy non-portal requests)
        public Guid? SubmissionSessionId { get; set; }

        /// <summary>
        /// SetId from dbo.Request — shared across all models in the same multi-model
        /// exchange after EnsureSharedSetForPortalCartridgeGroup runs.
        /// NULL for legacy single-request exchanges.
        /// </summary>
        public int? SetId { get; set; }

        /// <summary>
        /// Gets the fulfillment status display text.
        /// </summary>
        public string FulfillmentStatusDisplay
        {
            get
            {
                if (IssuedFullQty == 0)
                    return "Unfulfilled";
                if (IssuedFullQty < ReturnedEmptyQty)
                    return "Partially Fulfilled";
                return "Fulfilled";
            }
        }

        /// <summary>
        /// Gets the summary display text for Step 3B.
        /// Format: "Returned: X | Issued: Y | Pending: Z | Status: [Status]"
        /// </summary>
        public string SummaryDisplay =>
            $"Returned: {ReturnedEmptyQty} | Issued: {IssuedFullQty} | Pending: {UnfulfilledQty} | Status: {FulfillmentStatusDisplay}";
    }

    /// <summary>
    /// Fulfillment status constants for cartridge exchanges.
    /// </summary>
    public static class CartridgeFulfillmentStatus
    {
        public const string Pending = "Pending";
        public const string Fulfilled = "Fulfilled";

        public static readonly string[] All = { Pending, Fulfilled };
    }

    /// <summary>
    /// Helper class for generating auto-remarks for cartridge exchanges.
    /// </summary>
    public static class CartridgeExchangeRemarks
    {
        /// <summary>
        /// Generates auto-remarks for partial fulfillment.
        /// </summary>
        public static string GeneratePartialFulfillmentRemarks(
            int issuedQty, int returnedQty, int unfulfilledQty, string cartridgeModel)
        {
            return $"Only {issuedQty} out of {returnedQty} returned cartridges were refilled " +
                   $"due to insufficient stock for model {cartridgeModel}. " +
                   $"The remaining {unfulfilledQty} cartridges are pending fulfillment.";
        }

        /// <summary>
        /// Generates auto-remarks for zero fulfillment (completely unfulfilled).
        /// </summary>
        public static string GenerateZeroFulfillmentRemarks(int returnedQty, string cartridgeModel)
        {
            return $"No cartridges were refilled due to insufficient stock for model {cartridgeModel}. " +
                   $"All {returnedQty} returned cartridges are pending fulfillment.";
        }

        /// <summary>
        /// Generates auto-remarks for full fulfillment.
        /// </summary>
        public static string GenerateFullFulfillmentRemarks(int issuedQty, string cartridgeModel)
        {
            return $"All {issuedQty} cartridges for model {cartridgeModel} were successfully refilled.";
        }

        /// <summary>
        /// Generates appropriate remarks based on fulfillment quantities.
        /// </summary>
        public static string GenerateRemarks(
            int issuedQty, int returnedQty, string cartridgeModel)
        {
            int unfulfilledQty = returnedQty - issuedQty;

            if (issuedQty == 0)
                return GenerateZeroFulfillmentRemarks(returnedQty, cartridgeModel);

            if (issuedQty < returnedQty)
                return GeneratePartialFulfillmentRemarks(issuedQty, returnedQty, unfulfilledQty, cartridgeModel);

            return GenerateFullFulfillmentRemarks(issuedQty, cartridgeModel);
        }
    }

    /// <summary>
    /// DTO for multi-model cartridge request support.
    /// A single request may contain multiple cartridge models.
    /// </summary>
    public sealed class CartridgeModelRequestDto
    {
        public int ReqId { get; set; }
        public string CartridgeModel { get; set; }
        public int RequestedQty { get; set; }
        public int AvailableStock { get; set; }
        public int IssuedQty { get; set; }
        public int UnfulfilledQty => RequestedQty - IssuedQty;

        /// <summary>Good empty cartridges returned (from CartridgeRequestModel).</summary>
        public int GoodEmptyQty { get; set; }

        /// <summary>Damaged empty cartridges returned (from CartridgeRequestModel).</summary>
        public int DamagedEmptyQty { get; set; }

        /// <summary>
        /// Auto-generated remarks for this model's fulfillment.
        /// </summary>
        public string Remarks => CartridgeExchangeRemarks.GenerateRemarks(
            IssuedQty, RequestedQty, CartridgeModel);
    }
}
