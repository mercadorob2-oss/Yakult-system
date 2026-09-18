namespace Inventory.RequestPortal.Models
{
    public class UserAccountDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
        public string? EmployeeName { get; set; }
        public List<string> Roles { get; set; } = new();
        public bool IsDeveloper { get; set; }
        public bool IsActive { get; set; }
    }
}
