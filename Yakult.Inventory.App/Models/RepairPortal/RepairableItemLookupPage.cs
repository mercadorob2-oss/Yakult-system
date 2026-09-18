using System.Collections.Generic;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    /// <summary>One page of results from RepairTicketRepository.SearchReplacementCandidatesAsync,
    /// plus the total row count across all pages (needed to render the paging control).</summary>
    public sealed class RepairableItemLookupPage
    {
        public List<RepairableItemLookup> Items { get; set; } = new List<RepairableItemLookup>();
        public int TotalCount { get; set; }
    }
}
