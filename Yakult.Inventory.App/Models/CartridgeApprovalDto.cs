using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Maps to a row returned by usp_CartridgeApproval_CheckOrCreate
    /// and usp_CartridgeApproval_GetForSupervisor.
    /// </summary>
    public class CartridgeApprovalDto
    {
        public int       ApprovalId          { get; set; }
        public int       EmpId               { get; set; }
        public string    EmployeeName        { get; set; }
        public string    EmployeePosition    { get; set; }
        public string    EmployeeNumber      { get; set; }
        public string    EmployeeEmail       { get; set; }
        public int       ComId               { get; set; }
        public string    CompanyName         { get; set; }
        public int       BranchId            { get; set; }
        public string    BranchName          { get; set; }
        public int       DeptId              { get; set; }
        public string    DepartmentName      { get; set; }
        public int       SupervisorUserId    { get; set; }
        public int?      SupervisorEmpId     { get; set; }
        public string    SupervisorName      { get; set; }
        public string    SupervisorEmail     { get; set; }
        public string    ApprovalRole        { get; set; }   // "Coordinator"|"Supervisor"|"Manager"
        public string    SupervisorPosition  { get; set; }

        /// <summary>"Pending" | "Approved" | "Rejected" | "Created" (newly inserted)</summary>
        public string    Status              { get; set; }

        public string    Notes               { get; set; }
        public DateTime  RequestedAt         { get; set; }
        public DateTime? ReviewedAt          { get; set; }
        public DateTime? ExpiresAt           { get; set; }
        public DateTime? EmailSentAt         { get; set; }
        public Guid      ApprovalToken       { get; set; }
        public string    RequestorIpAddress  { get; set; }

        /// <summary>Comma-separated recent cartridge models (display only).</summary>
        public string    RecentCartridgeModels { get; set; }

        // Signature info (populated when already approved)
        public int?      SignatureId         { get; set; }
        public string    SignatureType       { get; set; }   // "Canvas" | "Upload"
        public string    SignaturePath       { get; set; }
        public DateTime? SignedAt            { get; set; }

        public bool IsApproved  => Status == "Approved";
        public bool IsPending   => Status == "Pending";
        public bool IsRejected  => Status == "Rejected";
        public bool IsExpired   => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    }

    /// <summary>
    /// Maps to dbo.DepartmentSupervisor for admin management UI.
    /// </summary>
    public class DepartmentSupervisorDto
    {
        public int      DeptSupervisorId  { get; set; }
        public int      DeptId            { get; set; }
        public string   DepartmentName    { get; set; }
        public int      SupervisorUserId  { get; set; }
        public string   SupervisorName    { get; set; }
        public string   SupervisorEmail   { get; set; }
        public bool     IsActive          { get; set; }
        public DateTime DateAssigned      { get; set; }
        public int?     AssignedByUserId  { get; set; }
        public string   AssignedByName    { get; set; }
    }

    /// <summary>
    /// Input model for saving a supervisor's e-signature.
    /// </summary>
    public class ApprovalSignatureInput
    {
        public int      ApprovalId        { get; set; }
        public Guid     ApprovalToken     { get; set; }
        public int      SupervisorUserId  { get; set; }

        /// <summary>"Canvas" | "Upload"</summary>
        public string   SignatureType     { get; set; }

        /// <summary>PNG/JPG bytes — from canvas export or file upload.</summary>
        public byte[]   SignatureData     { get; set; }

        /// <summary>Original filename when uploaded.</summary>
        public string   OriginalFileName  { get; set; }

        /// <summary>"image/png" | "image/jpeg"</summary>
        public string   MimeType         { get; set; }

        public string   ReviewerIpAddress { get; set; }
        public string   Notes             { get; set; }
    }
}
