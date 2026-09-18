using System;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Models.BorrowItems
{
    public sealed class BorrowLogRow
    {
        public int BorrowId { get; set; }

        public int ItemId { get; set; }
        public string SerialNumber { get; set; }
        public string ItemName { get; set; }
        public string ItemDescription { get; set; }
        public string ModelNumber { get; set; }

        public int? BorrowedByEmpId { get; set; }
        public string BorrowedByEmpName { get; set; }
        public int? BorrowedByDeptId { get; set; }
        public string BorrowedByDeptName { get; set; }
        public int BorrowEncodedByUserId { get; set; }
        public string BorrowEncodedByUserName { get; set; }
        public DateTime BorrowedAtUtc { get; set; }

        public int? ReturnedByEmpId { get; set; }
        public string ReturnedByEmpName { get; set; }
        public int? ReturnedByDeptId { get; set; }
        public string ReturnedByDeptName { get; set; }
        public int? ReturnEncodedByUserId { get; set; }
        public string ReturnEncodedByUserName { get; set; }
        public DateTime? ReturnedAtUtc { get; set; }

        /// <summary>Set when this loan was made through the Repair Portal's "Spare Item" feature
        /// (RepairTicketRepository.SpareAssignment.cs) — ties the loan back to the ticket that
        /// spawned it. Null for ordinary Borrow Items dashboard loans.</summary>
        public int? RepairTicketId { get; set; }

        public bool IsOpen => !ReturnedAtUtc.HasValue;

        public string ItemDisplay
        {
            get
            {
                var name = (ItemName ?? string.Empty).Trim();
                var model = (ModelNumber ?? string.Empty).Trim();
                var desc = (ItemDescription ?? string.Empty).Trim();
                var s = name.Length > 0 ? name : (SerialNumber ?? string.Empty).Trim();
                if (model.Length > 0) s += $" ({model})";
                if (desc.Length > 0) s += $" - {desc}";
                return s;
            }
        }

        public string BorrowedAtLocal => BorrowedAtUtc == default ? string.Empty : AppTime.ToLocalString(BorrowedAtUtc, "yyyy-MM-dd HH:mm");
        public string ReturnedAtLocal => ReturnedAtUtc.HasValue ? AppTime.ToLocalString(ReturnedAtUtc.Value, "yyyy-MM-dd HH:mm") : string.Empty;

        public string ElapsedText
        {
            get
            {
                if (BorrowedAtUtc == default)
                    return string.Empty;

                var start = AppTime.AssumeUtc(BorrowedAtUtc);
                var end = ReturnedAtUtc.HasValue ? AppTime.AssumeUtc(ReturnedAtUtc.Value) : AppTime.UtcNow;
                var span = end - start;
                if (span.TotalSeconds < 0) span = TimeSpan.Zero;

                if (span.TotalDays >= 1)
                    return $"{(int)span.TotalDays}d {span.Hours:D2}h {span.Minutes:D2}m";

                if (span.TotalHours >= 1)
                    return $"{(int)span.TotalHours}h {span.Minutes:D2}m";

                return $"{Math.Max(0, (int)span.TotalMinutes)}m";
            }
        }
    }
}

