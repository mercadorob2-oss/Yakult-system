using System.Collections.Generic;
using Yakult.Inventory.App.WPF.EDocs.Transmittal.Models;

namespace Yakult.Inventory.App.WPF.EDocs.Transmittal.ViewModels
{
    // Print-time snapshot over a TransmittalDocument (same role as
    // GatepassPrintViewModel). Pads/truncates to exactly 12 ruled lines.
    public class TransmittalPrintViewModel
    {
        public const int TemplateItemCount = 12;

        public TransmittalPrintViewModel(TransmittalDocument document)
        {
            Document = document ?? new TransmittalDocument();
        }

        public TransmittalDocument Document { get; }

        public sealed class TransmittalPrintLine
        {
            public int No { get; set; }
            public string Text { get; set; } = "";
        }

        public List<TransmittalPrintLine> DisplayLines
        {
            get
            {
                var lines = new List<TransmittalPrintLine>();
                var items = Document.Items;
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (lines.Count >= TemplateItemCount) break;
                        var text = item == null || item.Description == null ? "" : item.Description;
                        lines.Add(new TransmittalPrintLine { No = lines.Count + 1, Text = text });
                    }
                }
                while (lines.Count < TemplateItemCount)
                    lines.Add(new TransmittalPrintLine { No = lines.Count + 1, Text = "" });
                return lines;
            }
        }

        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(Document.To)) return "TO is required.";
            if (string.IsNullOrWhiteSpace(Document.Date)) return "DATE is required.";
            foreach (var line in DisplayLines)
                if (!string.IsNullOrWhiteSpace(line.Text)) return null;
            return "Add at least one item line.";
        }
    }
}
