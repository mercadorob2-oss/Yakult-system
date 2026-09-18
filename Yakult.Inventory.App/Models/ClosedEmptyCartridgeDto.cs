using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Read-only projection of a non-refillable empty cartridge that has been
    /// either Disposed or Sold by IT.  Used by the Sold/Disposed audit pages.
    /// </summary>
    public class ClosedEmptyCartridgeDto
    {
        public int      EmptyCartridgeId    { get; set; }
        public string   CartridgeModel      { get; set; }
        public int      Quantity            { get; set; }
        public string   Status             { get; set; }   // 'Sold' | 'Disposed'
        public DateTime ReturnedAt          { get; set; }
        public DateTime? ClosedAt           { get; set; }  // DateModified — when IT actioned
        public int?     ReqId              { get; set; }
        public string   Remarks            { get; set; }
        public string   DisposalCompanyName { get; set; }  // optional free-text, may be null
    }
}
