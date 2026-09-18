using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    /// <summary>1:1 with RepairTicket. Null until the technician saves it for the first time.</summary>
    public sealed class RepairConclusion
    {
        public int RepairTicketId { get; set; }
        public string RootCause { get; set; }
        public string WorkPerformed { get; set; }
        public string FinalOutcome { get; set; }
        public string Recommendations { get; set; }
        public string CompletedByName { get; set; }
        public DateTime? CompletedAt { get; set; }

        /// <summary>IT Dept. employee(s) who actually performed the repair — many-to-many, since
        /// more than one person can work a ticket. Ids are what gets saved; Names is populated on
        /// load for display.</summary>
        public List<int> RepairedByEmpIds { get; set; } = new List<int>();
        public List<string> RepairedByNames { get; set; } = new List<string>();

        /// <summary>Set when this repair was handed to an external vendor instead of/alongside
        /// being handled in-house. Null = repaired in-house, no 3rd party involved.</summary>
        public int? HandedOverToVendorId { get; set; }
        public string HandedOverToVendorName { get; set; }

        // ── Unrepairable disposition — see RepairTicketRepository.Disposition.cs ────────────────
        /// <summary>"Discard" or "Replace" — only meaningful once RepairTicket.Status = 'Unrepairable'.
        /// Always changeable: picking the other option re-runs SetDispositionAsync, which reverses
        /// this choice's side effects before applying the new one.</summary>
        public string Disposition { get; set; }
        public int? DispositionItemId { get; set; }
        public DateTime? DispositionDecidedAt { get; set; }
        public DateTime? DispositionExecutedAt { get; set; }
        public int? ReplacementItemId { get; set; }
        public string ReplacementItemName { get; set; }
        public int? ReplacementRequestId { get; set; }
        public int? ReplacementSetId { get; set; }
        public string ReplacementSetCode { get; set; }
    }
}
