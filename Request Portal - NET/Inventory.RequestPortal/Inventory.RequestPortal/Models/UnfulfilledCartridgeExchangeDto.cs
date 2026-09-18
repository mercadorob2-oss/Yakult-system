namespace Inventory.RequestPortal.Models
{
    /// <summary>
    /// DTO for UnfulfilledCartridgeExchange records — the second-stage cartridge-exchange
    /// fulfillment backlog (populated only after a first-pass fulfillment attempt on desktop).
    /// PORTED FROM: Yakult.Inventory.App/Models/UnfulfilledCartridgeExchangeDto.cs
    /// </summary>
    public class UnfulfilledCartridgeExchangeDto
    {
        public int UnfulfilledId { get; set; }
        public int ReqId { get; set; }
        public int EmpId { get; set; }
        public int? BranchId { get; set; }
        public int? DeptId { get; set; }
        public string? CartridgeModel { get; set; }
        public int RequestedQty { get; set; }
        public int ReturnedEmptyQty { get; set; }
        public int IssuedFullQty { get; set; }
        public int UnfulfilledQty { get; set; }
        public string? Remarks { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public int CreatedBy { get; set; }
        public DateTime? FulfilledDate { get; set; }
        public int? FulfilledBy { get; set; }
        public string? FulfilledRemarks { get; set; }

        // Display fields (joined from related tables)
        public string? RequesterName { get; set; }
        public string? BranchName { get; set; }
        public string? DepartmentName { get; set; }
        public string? CreatedByName { get; set; }
        public string? FulfilledByName { get; set; }

        // Session grouping (from dbo.Request — NULL for legacy non-portal requests)
        public Guid? SubmissionSessionId { get; set; }
        public int? SetId { get; set; }

        public string FulfillmentStatusDisplay
        {
            get
            {
                if (IssuedFullQty == 0) return "Unfulfilled";
                if (IssuedFullQty < ReturnedEmptyQty) return "Partially Fulfilled";
                return "Fulfilled";
            }
        }

        public string SummaryDisplay =>
            $"Returned: {ReturnedEmptyQty} | Issued: {IssuedFullQty} | Pending: {UnfulfilledQty} | Status: {FulfillmentStatusDisplay}";
    }

    /// <summary>
    /// Auto-generated remarks for cartridge exchange fulfillment, used when the fulfiller
    /// leaves the remarks box blank. PORTED FROM: Yakult.Inventory.App/Models/UnfulfilledCartridgeExchangeDto.cs
    /// </summary>
    public static class CartridgeExchangeRemarks
    {
        public static string GenerateRemarks(int issuedQty, int returnedQty, string? cartridgeModel)
        {
            int unfulfilledQty = returnedQty - issuedQty;

            if (issuedQty == 0)
                return $"No cartridges were refilled due to insufficient stock for model {cartridgeModel}. " +
                       $"All {returnedQty} returned cartridges are pending fulfillment.";

            if (issuedQty < returnedQty)
                return $"Only {issuedQty} out of {returnedQty} returned cartridges were refilled " +
                       $"due to insufficient stock for model {cartridgeModel}. " +
                       $"The remaining {unfulfilledQty} cartridges are pending fulfillment.";

            return $"All {issuedQty} cartridges for model {cartridgeModel} were successfully refilled.";
        }
    }
}
