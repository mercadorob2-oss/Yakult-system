using System.Collections.Generic;

namespace Yakult.Inventory.App.WPF.EDocs.Gatepass.Models
{
    public enum GatepassFormType
    {
        File,
        Gatepass,
        Transmittal
    }

    public enum GatepassItemCategory
    {
        CartridgeRibbon,
        Monitor,
        Cpu,
        Mouse,
        Keyboard,
        Printer,
        BackupUps,
        Avr,
        ComputerTableFixedAsset,
        Others
    }

    public enum GatepassModel
    {
        None,
        Lx300,
        Lx310,
        Lq2190
    }

    public class GatepassDocument
    {
        public class GatepassItem
        {
            public string Description { get; set; } = "";
        }

        // ── Form type ─────────────────────────────────────────────────────────
        public GatepassFormType FormType { get; set; } = GatepassFormType.File;

        // ── Company header ────────────────────────────────────────────────────
        public bool IsYakultPhilippines { get; set; } = true;
        public bool IsYakultMarketing   { get; set; }

        // ── Header fields ─────────────────────────────────────────────────────
        // FROM is permanent — every gatepass/transmittal/file copy issued by
        // this section comes from the IT department, so it is fixed here and
        // shown read-only on the form.
        public const string PermanentFrom = "INFORMATION TECHNOLOGY DEPARTMENT";

        public string To   { get; set; } = "";
        public string From { get; set; } = PermanentFrom;
        public string Date { get; set; } = "";

        // ── Category / model ──────────────────────────────────────────────────
        public GatepassItemCategory? ItemCategory      { get; set; }
        public string                ItemCategoryOther { get; set; } = "";

        public GatepassModel Model { get; set; } = GatepassModel.None;

        public string FixedAssetNumber { get; set; } = "";
        public string Quantity         { get; set; } = "";

        // ── Line items ────────────────────────────────────────────────────────
        public List<GatepassItem> Items { get; set; } = new List<GatepassItem>();

        // ── Notes ─────────────────────────────────────────────────────────────
        public string Others  { get; set; } = "";
        public string Remarks { get; set; } = "";

        // ── Signatories ───────────────────────────────────────────────────────
        public string IssuedBy       { get; set; } = "";
        public string NotedBy        { get; set; } = "";
        public string ReceivedByDate { get; set; } = "";
        public string ApprovedByDate { get; set; } = "";

        // Mobile print-all basis: when true, printing produces one Legal
        // sheet per form type (Gatepass, Transmittal, File) instead of a
        // single sheet of the selected type.
        public bool PrintAllThree { get; set; }

        // Shallow clone for per-type sheet rendering (print-all mode).
        // The Items list is shared read-only and never mutated while printing.
        public GatepassDocument Clone()
        {
            return (GatepassDocument)MemberwiseClone();
        }
    }
}
