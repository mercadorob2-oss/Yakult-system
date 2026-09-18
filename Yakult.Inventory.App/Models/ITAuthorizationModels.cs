namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// An employee eligible to authorize an IT-assisted cartridge request.
    /// Identified by EmpId (not UserId) because approvers may not have system accounts.
    /// </summary>
    public sealed class ApproverViewModel
    {
        public int    EmpId          { get; set; }
        public string DisplayName    { get; set; }
        public string Position       { get; set; }
        public string DepartmentName { get; set; }
        public string BranchName     { get; set; }
        public string ApprovalRole   { get; set; }  // Manager | Supervisor | Coordinator

        public override string ToString() => DisplayName ?? base.ToString();
    }

    /// <summary>
    /// Authorization details entered by the IT user when submitting an assisted request.
    /// Passed from AssistedRequestViewModel → RequesterPortalService → CartridgeAuthorizationRepository.
    /// </summary>
    public sealed class ITAuthorizationInfo
    {
        public int    AuthorizedByEmpId { get; set; }
        /// <summary>
        /// When true (Manual Authorization toggle is OFF), no offline decision is recorded —
        /// a Pending auth record is created so the approver can approve via the portal normally.
        /// When false (toggle is ON), IT records the offline/verbal decision in Decision/Remarks.
        /// </summary>
        public bool   UsePortalFlow     { get; set; }
        public string Decision          { get; set; }  // "Approved" | "Rejected" — only set when UsePortalFlow=false
        public string Remarks           { get; set; }
    }
}
