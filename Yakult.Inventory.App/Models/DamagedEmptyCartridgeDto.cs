using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Read-only projection of a returned empty cartridge whose ConditionStatus
    /// is 'DAMAGED'.  These records are excluded from all refill-batch assignment
    /// flows and surfaced on the Damaged Empty Cartridges page for visibility.
    /// </summary>
    public class DamagedEmptyCartridgeDto
    {
        public int      EmptyCartridgeId    { get; set; }
        public string   CartridgeModel      { get; set; }
        public int      Quantity            { get; set; }
        public DateTime ReturnedAt          { get; set; }
        public int?     ReqId               { get; set; }
        public string   Remarks             { get; set; }
        public string   DisposalCompanyName { get; set; }   // null until actioned
        public string   ConditionName       { get; set; }
    }
}
