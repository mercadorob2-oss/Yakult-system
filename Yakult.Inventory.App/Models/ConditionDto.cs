namespace Yakult.Inventory.App.Models
{
    public class ConditionDto
    {
        public int ConditionId { get; set; }
        public string ConditionName { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
