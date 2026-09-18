using System;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    public sealed class RepairTicketListItem
    {
        public int RepairTicketId { get; set; }
        public string TicketCode { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemSerial { get; set; }
        public string Category { get; set; }
        public string SetCode { get; set; }
        public string CompanyName { get; set; }
        public string BranchName { get; set; }
        public string DeptName { get; set; }
        public string Problem { get; set; }
        public string Priority { get; set; }
        public string Status { get; set; }
        public string SubmittedByName { get; set; }
        public string AssignedTechName { get; set; }
        public string RequestedByType { get; set; }
        public string RequestedByEmpName { get; set; }
        public string RequestedByDeptName { get; set; }

        /// <summary>Table/Kanban "Employee" cell — the specific employee if Requested By is set to
        /// Employee, or the department name flagged "(By Dept)" if it's set to Department instead
        /// (there's no single employee to show in that case).</summary>
        public string RequestedByDisplay
        {
            get
            {
                if (string.Equals(RequestedByType, "Employee", System.StringComparison.OrdinalIgnoreCase))
                    return RequestedByEmpName;
                if (string.Equals(RequestedByType, "Department", System.StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrEmpty(RequestedByDeptName) ? null : RequestedByDeptName + " (By Dept)";
                return null;
            }
        }
        public DateTime DateReceived { get; set; }
        public DateTime UpdatedAt { get; set; }
        public byte[] ThumbnailImageBytes { get; set; }
        public string ThumbnailMimeType { get; set; }

        /// <summary>Days spent in the CURRENT status only — see RepairTicketDetail.DaysInCurrentStatus.</summary>
        public int DaysInCurrentStatus { get; set; }

        public bool HasThumbnail => ThumbnailImageBytes != null && ThumbnailImageBytes.Length > 0;
    }
}
