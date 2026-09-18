using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Represents a returned empty cartridge whose model has IsRefillable == false.
    /// These are never entered into the refill queue and must be handled by IT
    /// (disposed of or sold internally).
    /// </summary>
    public class NonRefillableEmptyDto
    {
        public int      EmptyCartridgeId { get; set; }
        public string   CartridgeModel   { get; set; }
        public int      Quantity         { get; set; }
        public string   Status           { get; set; }
        public DateTime ReturnedAt       { get; set; }
        public int?     ReqId            { get; set; }
        public string   Remarks          { get; set; }

        // Used by dispose/sell workflow to locate the IT custody Item row
        // and anchor Inventory / CartridgeMovement records.
        public int?     SourceItemId        { get; set; }
        public int      CartridgeModelId    { get; set; }
        public int?     ConditionId         { get; set; }

        // Optional free-text recorded at the time of disposal/sale; null until actioned.
        public string   DisposalCompanyName { get; set; }
    }
}
