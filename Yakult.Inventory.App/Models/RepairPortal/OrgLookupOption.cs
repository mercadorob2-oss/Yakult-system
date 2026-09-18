namespace Yakult.Inventory.App.Models.RepairPortal
{
    /// <summary>Generic Id/Name pair used for Company / Branch / Department / Technician filter dropdowns.</summary>
    public sealed class OrgLookupOption
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public override string ToString() => Name;
    }
}
