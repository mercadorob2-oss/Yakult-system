namespace Yakult.Inventory.App.Models
{
    public sealed class LookupItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }

        public override string ToString() => DisplayName ?? Name ?? base.ToString();
    }
}
