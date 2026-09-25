namespace Inventory.RequestPortal.Models
{
    /// <summary>
    /// A cartridge line of a mixed portal submission, as fulfilled on the Request Fulfillment pages.
    /// See CartridgeExchangeRepository.IssueMixedCartridgeLineAsync.
    /// MATCHES: Yakult.Inventory.App Repositories.MixedCartridgeLineInfo
    /// </summary>
    public class MixedCartridgeLineInfo
    {
        public int     ReqId             { get; set; }
        public int     Quantity          { get; set; }
        public int     IssuedQty         { get; set; }
        /// <summary>Cartridge item, Request and Set Management workflow, portal submission.</summary>
        public bool    IsExchangeLine    { get; set; }
        public int?    CartridgeModelId  { get; set; }
        public string? ModelNumber       { get; set; }
        public int     DeclaredGood      { get; set; }
        public int     DeclaredDamaged   { get; set; }
        public int     ReturnedGood      { get; set; }
        public int     ReturnedDamaged   { get; set; }
        public int     AvailableBrandNew { get; set; }
        public int     AvailableRefilled { get; set; }

        public int PendingQty => Math.Max(0, Quantity - IssuedQty);
    }
}
