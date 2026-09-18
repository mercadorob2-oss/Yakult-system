using System.Collections.Generic;

namespace Yakult.Inventory.App.WPF.EDocs.Transmittal.Models
{
    // Standalone transmittal document (mobile TransmittalReport basis):
    // header inputs + free item lines + sign-offs. Not tied to any set,
    // request, or cartridge record — filled in by hand, then printed.
    public class TransmittalLineItem
    {
        public string Description { get; set; } = "";
    }

    public class TransmittalDocument
    {
        public string To             { get; set; } = "";
        // FROM is permanent — every transmittal issued by this section comes
        // from the IT department (mirrors GatepassDocument.PermanentFrom).
        public const string PermanentFrom = "INFORMATION TECHNOLOGY DEPARTMENT";
        public string From           { get; set; } = PermanentFrom;
        public string Date           { get; set; } = "";
        public List<TransmittalLineItem> Items { get; set; } = new List<TransmittalLineItem>();
        public string PreparedBy     { get; set; } = "";
        public string TransmitBy     { get; set; } = "";
        public string ReceivedByDate { get; set; } = "";
        public string NotedBy        { get; set; } = "";
        public string ApprovedByDate { get; set; } = "";
    }
}
