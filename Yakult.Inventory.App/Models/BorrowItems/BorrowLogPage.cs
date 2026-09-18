using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Models.BorrowItems
{
    public sealed class BorrowLogPage
    {
        public List<BorrowLogRow> Rows { get; set; } = new List<BorrowLogRow>();

        public int TotalCount { get; set; }

        // For open-borrow paging (and any filtered variant), this is the oldest BorrowedAtUtc
        // among the full filtered result set (not just the current page).
        public DateTime? OldestBorrowedAtUtc { get; set; }
    }
}

