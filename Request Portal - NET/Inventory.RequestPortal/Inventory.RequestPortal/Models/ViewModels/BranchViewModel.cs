namespace Inventory.RequestPortal.Models.ViewModels
{
    /// <summary>
    /// Simple DTO for branch dropdown.
    /// COPIED FROM: Yakult.Inventory.App/Services/RequesterPortalService.cs (BranchDto)
    /// </summary>
    public class BranchViewModel
    {
        public int BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ComId { get; set; }
        public int? DeptId { get; set; }
    }
}
