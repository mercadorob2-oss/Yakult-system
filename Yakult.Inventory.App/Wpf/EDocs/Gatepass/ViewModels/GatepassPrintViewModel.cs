using System.Collections.Generic;
using System.Linq;
using Yakult.Inventory.App.WPF.EDocs.Gatepass.Models;

namespace Yakult.Inventory.App.WPF.EDocs.Gatepass.ViewModels
{
    public class GatepassPrintViewModel
    {
        public const int TemplateItemCount = 12;

        public GatepassDocument Document { get; }

        public GatepassPrintViewModel(GatepassDocument document)
        {
            Document = document;
        }

        // Item Descriptions padded with "" to exactly 12 lines.
        public List<string> DisplayLines
        {
            get
            {
                var lines = (Document?.Items ?? new List<GatepassDocument.GatepassItem>())
                    .Take(TemplateItemCount)
                    .Select(i => i?.Description ?? "")
                    .ToList();
                while (lines.Count < TemplateItemCount)
                    lines.Add("");
                return lines;
            }
        }

        public bool FormTypeIsGatepass   => Document?.FormType == GatepassFormType.Gatepass;
        public bool FormTypeIsFile       => Document?.FormType == GatepassFormType.File;
        public bool FormTypeIsTransmittal => Document?.FormType == GatepassFormType.Transmittal;

        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(Document?.To))
                return "To is required.";
            if (string.IsNullOrWhiteSpace(Document?.Date))
                return "Date is required.";
            // Item lines are optional — the printed form keeps its 12 blank
            // ruled lines so they can be filled in by hand after printing.
            return null;
        }
    }
}
