using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Models
{
    public sealed class LicenseHistoricalPeriodDto
    {
        public int HistoricalPeriodId { get; set; }
        public int AnchorSetId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string ReferenceNumber { get; set; }
        public decimal? Amount { get; set; }
        public string Notes { get; set; }
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<LicenseHistoricalPeriodItemDto> Items { get; set; } = new List<LicenseHistoricalPeriodItemDto>();

        public string PeriodDisplay => $"{StartDate:MMM d, yyyy} – {EndDate:MMM d, yyyy}";
        public string AmountDisplay => Amount.HasValue ? $"₱{Amount:N2}" : "Amount not recorded";
        public string ReferenceDisplay => string.IsNullOrWhiteSpace(ReferenceNumber) ? "Reference not recorded" : ReferenceNumber;
        public string EnteredDisplay => $"Entered by {CreatedByName ?? "Unknown"} on {DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc).ToLocalTime():MMM d, yyyy h:mm tt}";
    }

    public sealed class LicenseHistoricalPeriodItemDto
    {
        public int HistoricalPeriodId { get; set; }
        public int? ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemCode { get; set; }
        public decimal Quantity { get; set; } = 1m;
        public int DisplayOrder { get; set; }
    }

    public sealed class LicenseHistoricalPeriodContext
    {
        public int RootSetId { get; set; }
        public List<int> SetIds { get; set; } = new List<int>();
        public DateTime? FirstRecordedStartDate { get; set; }
        public List<LicenseHistoricalPeriodItemDto> CurrentItems { get; set; } = new List<LicenseHistoricalPeriodItemDto>();
        public List<LicenseHistoricalPeriodDto> Periods { get; set; } = new List<LicenseHistoricalPeriodDto>();
    }

    public sealed class HistoricalPeriodOverlapException : InvalidOperationException
    {
        public HistoricalPeriodOverlapException()
            : base("This period overlaps an existing historical period. Save it anyway?") { }
    }
}
