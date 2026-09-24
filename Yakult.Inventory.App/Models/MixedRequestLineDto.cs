using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// One Request line of an approved portal submission that mixes cartridge and
    /// non-cartridge items. Feeds the Mixed Request Exchange page.
    /// </summary>
    public class MixedRequestLineDto
    {
        public int      ReqId               { get; set; }
        public int?     SetId               { get; set; }
        public Guid     SubmissionSessionId { get; set; }
        public DateTime DateCreated         { get; set; }
        public int      Quantity            { get; set; }
        public int      IssuedQty           { get; set; }
        public string   Remarks             { get; set; }
        public string   Status              { get; set; }
        public int      ItemId              { get; set; }
        public string   ItemName            { get; set; }
        public string   Category            { get; set; }
        public int      StockOnHand         { get; set; }
        public int      EmpId               { get; set; }
        public string   EmployeeName        { get; set; }
        public string   CompanyName         { get; set; }
        public string   BranchName          { get; set; }
        public string   DepartmentName      { get; set; }
        public string   DistributionMethod  { get; set; }
        public string   ReceivedByName      { get; set; }
    }
}
