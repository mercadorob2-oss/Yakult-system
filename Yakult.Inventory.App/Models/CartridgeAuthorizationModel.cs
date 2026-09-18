using System;

namespace Yakult.Inventory.App.Models
{
    public class CartridgeAuthorizationModel
    {
        public int      AuthorizationId      { get; set; }
        public int      EmployeeId           { get; set; }
        public int      DepartmentId         { get; set; }
        public string   Status               { get; set; }   // Pending | Approved | Rejected | Used
        public int?     SignedBySupervisorId { get; set; }
        public DateTime? SignedDate          { get; set; }
        public DateTime CreatedDate          { get; set; }

        // Populated via JOIN – not stored in the table
        public string EmployeeName           { get; set; }
        public string EmployeePosition       { get; set; }
        public string DepartmentName         { get; set; }
        public string BranchName             { get; set; }
        public string CompanyName            { get; set; }
        public string SignedByName           { get; set; }

        // Stored in the table
        public string RequestedModels        { get; set; }

        // Distribution — fetched from dbo.Request via SubmissionSessionId
        public string FulfillmentMethod      { get; set; }  // "Pickup" | "Delivery" | null
        public string ReceivedByName         { get; set; }  // null unless PICKUP

        // Routing — who is assigned to approve this request (NULL = no approver account yet)
        public int?   AssignedToUserId       { get; set; }
        public string AssignedToName         { get; set; }

        // IT-assisted tracking — which IT user submitted on behalf of the employee
        public int?   SubmittedByUserId      { get; set; }
        public string SubmittedByName        { get; set; }

        // Remarks — optional for Approved, required for Rejected (IT Manual Authorization)
        public string Remarks                { get; set; }

        public bool IsPending  => Status == "Pending";
        public bool IsApproved => Status == "Approved";
        public bool IsRejected => Status == "Rejected";
        public bool IsUsed     => Status == "Used";

        public bool IsITAssisted => SubmittedByUserId.HasValue;
    }
}
