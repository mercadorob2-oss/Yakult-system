using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    /// <summary>Everything the printable Repair Report needs, assembled once by
    /// RepairTicketRepository.GetReportDataAsync. Deliberately minimal/raw — RepairReportBuilder
    /// decides what's "meaningful enough to print" (dedup, N/A filtering, section visibility), not
    /// this model. Fields with no backing schema anywhere (Asset Tag, Brand, Testing, Parts
    /// Replaced, Attachment Description, ticket-wide audit Timeline) are intentionally excluded —
    /// per the report's data-driven design, a section with nothing meaningful to show is omitted
    /// entirely rather than printed as "N/A".</summary>
    public sealed class RepairReportData
    {
        // Header / metadata
        public int RepairTicketId { get; set; }
        public string TicketCode { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string GeneratedByName { get; set; }
        public string TechnicianName { get; set; }

        // Asset Info
        public string ItemName { get; set; }
        public string Category { get; set; }
        public string ModelNumber { get; set; }
        public string SerialNumber { get; set; }
        public string DeptName { get; set; }
        public int? DeptId { get; set; }
        public string BranchName { get; set; }
        public string AssignedUserName { get; set; }

        // Reported Problem — raw pieces; the builder dedupes Observations vs. the free-text Problem
        public List<string> ObservationTexts { get; set; } = new List<string>();
        public string ProblemFreeText { get; set; }

        // Work Performed / Diagnosis / Recommendations — raw, possibly null; builder decides N/A
        public string WorkPerformed { get; set; }
        public string RootCause { get; set; }
        public string ResolutionSummary { get; set; }
        public string Recommendations { get; set; }

        // Who actually repaired it (IT Dept. staff, can be more than one) and, if it was sent out,
        // which vendor handled it — both raw/possibly-empty; builder decides "worth printing".
        public List<string> RepairedByNames { get; set; } = new List<string>();
        public string HandedOverToVendorName { get; set; }

        // Item Disposition — pre-formatted (built in GetReportDataAsync, not the RDLC): states
        // whether a Spare was loaned and/or the item was permanently Replaced (Unrepairable
        // disposition), plus that item's Name/Model/Serial. Null/empty when neither applies —
        // ShowItemDisposition follows this file's "omit, don't print N/A" convention.
        public string ItemDispositionText { get; set; }
        public bool ShowItemDisposition => !string.IsNullOrWhiteSpace(ItemDispositionText);

        // Parts — just enough for one compact line per part ("Part Name: Result")
        public List<RepairReportPartSection> Parts { get; set; } = new List<RepairReportPartSection>();

        // Attachments
        public List<RepairReportAttachmentRow> Attachments { get; set; } = new List<RepairReportAttachmentRow>();
    }

    public sealed class RepairReportPartSection
    {
        public string PartDisplayName { get; set; }
        public string Status { get; set; }
    }

    public sealed class RepairReportAttachmentRow
    {
        public string FileName { get; set; }
        public string AttachmentType { get; set; }
        public byte[] ThumbnailBytes { get; set; }

        /// <summary>Where this evidence came from — "Whole-Equipment" or a Part's display name
        /// (e.g. "Part #2 - Power Board"), so the report attachment picker can group/label rows.</summary>
        public string SourceLabel { get; set; }

        /// <summary>AttachmentId (whole-equipment) or PartAttachmentId (part-level) — lets
        /// RepairReportBuilder re-fetch this specific row's full-resolution FileBytes for the
        /// handful of images the technician actually picks in RepairReportAttachmentPickerDialog,
        /// instead of using the small Gallery-quality ThumbnailBytes for the printed report.</summary>
        public int AttachmentId { get; set; }
        public bool IsPartAttachment { get; set; }
    }
}
