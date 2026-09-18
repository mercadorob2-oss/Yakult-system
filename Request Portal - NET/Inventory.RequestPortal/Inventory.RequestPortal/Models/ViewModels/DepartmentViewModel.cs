namespace Inventory.RequestPortal.Models.ViewModels
{
    /// <summary>
    /// Simple DTO for department dropdown.
    /// COPIED FROM: Yakult.Inventory.App/Services/RequesterPortalService.cs (DepartmentDto)
    /// </summary>
    public class DepartmentViewModel
    {
        public int DeptId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ComId { get; set; }
    }
}
