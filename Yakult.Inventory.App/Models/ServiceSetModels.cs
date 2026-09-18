using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Models
{
    public class CompanyDto
    {
        public int ComId { get; set; }
        public string CompanyName { get; set; }
    }

    public class DistributorDto
    {
        public int DistributorId { get; set; }
        public string Name { get; set; }
    }

    public class ServiceSetDto
    {
        public int Id { get; set; }
        public string DocumentNumber { get; set; }
        public string ReferenceNumber { get; set; }
        public DateTime DocumentDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Notes { get; set; }
        public string Status { get; set; } = "Draft";
        public int? ComId { get; set; }
        public string CompanyName { get; set; } // For display purposes only
        public int? BranchId { get; set; }
        public string BranchName { get; set; } // For display purposes only
        public int? DistributorId { get; set; }
        public string DistributorName { get; set; } // For display purposes only

        public int CreatedByUserId { get; set; }
        public DateTime DateCreated { get; set; } = DateTime.Now;
        public int? ModifiedByUserId { get; set; }
        public DateTime? DateModified { get; set; }
        public List<ServiceSetItemDto> Items { get; set; } = new List<ServiceSetItemDto>();
    }

    public class ServiceSetItemDto
    {
        public int Id { get; set; }
        public int SetId { get; set; }
        public int ItemId { get; set; }
        public string ItemCode { get; set; }
        /// <summary>Which Contract/Subscription/License/Services group this item came
        /// from (Invoice Preparation page) — null for a manually-added item. Copied
        /// straight to dbo.SetItem.SubType.</summary>
        public string SubType { get; set; }
        /// <summary>The owning group's own Reference Code (Contract Code/Subscription ID/
        /// License ID/Service ID) — copied straight to dbo.SetItem.ReferenceCode and
        /// dbo.SetItemSubTypeGroup.ReferenceCode.</summary>
        public string ReferenceCode { get; set; }
        /// <summary>The group's shared date range — copied straight to dbo.SetItem.BeginDate/
        /// EndDate and dbo.SetItemSubTypeGroup.BeginDate/EndDate. Leave null to inherit
        /// LineStartDate/LineEndDate, which is what most callers do; the CSV import sets it
        /// explicitly from its GroupBeginDate/GroupEndDate columns.</summary>
        public DateTime? BeginDate { get; set; }
        public DateTime? EndDate { get; set; }

        /// <summary>The group's OWN financials — each Sub-Type group is priced independently of
        /// the invoice header (see ViewInvoiceDetailPage's group cards). Copied to
        /// dbo.SetItemSubTypeGroup.VatPercent/WhtPercent/DiscountPercent/SubtotalOverride.
        /// Null means "not set", and the group falls back to the header's percentages and to the
        /// computed sum of its line amounts.</summary>
        public decimal? GroupVatPercent { get; set; }
        public decimal? GroupWhtPercent { get; set; }
        public decimal? GroupDiscountPercent { get; set; }
        public decimal? GroupSubtotalOverride { get; set; }
        /// <summary>Free-text Parent Tag label (e.g. "Cisco") — an independent, orthogonal
        /// grouping from SubType with no financial semantics of its own. Copied straight to
        /// dbo.SetItem.ParentTagGroupId via dbo.SetItemParentTagGroup.Label.</summary>
        public string ParentTagLabel { get; set; }
        /// <summary>The item's Part# (sourced from dbo.Renewals.PartNumber), for display
        /// only — not persisted on dbo.SetItem.</summary>
        public string PartNumber { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public string UnitOfMeasure { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public DateTime? LineStartDate { get; set; }
        public DateTime? LineEndDate { get; set; }
    }
}
