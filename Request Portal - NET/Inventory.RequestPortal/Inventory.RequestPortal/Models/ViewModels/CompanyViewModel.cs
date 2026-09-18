namespace Inventory.RequestPortal.Models.ViewModels
{
    /// <summary>
    /// Simple DTO for company dropdown.
    /// COPIED FROM: Yakult.Inventory.App/Services/RequesterPortalService.cs (CompanyDto)
    /// </summary>
    public class CompanyViewModel
    {
        public int ComId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
