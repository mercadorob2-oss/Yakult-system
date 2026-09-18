namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Movement types for cartridge tracking in CartridgeMovement table.
    /// Used by Cartridge Management for fulfillment operations.
    /// </summary>
    public enum MovementType
    {
        /// <summary>
        /// Cartridge issued to employee (full cartridge going out).
        /// </summary>
        Issue = 1,

        /// <summary>
        /// Empty cartridge returned by employee for refill.
        /// </summary>
        ReturnForRefill = 2,

        /// <summary>
        /// Cartridge refill completed (empty → full).
        /// </summary>
        RefillComplete = 3,

        /// <summary>
        /// Cartridge disposed/written off.
        /// </summary>
        Dispose = 4
    }
}
