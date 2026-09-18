using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Filter parameters for the User Activity query.
    /// All fields are optional; null means "no filter on this field".
    /// </summary>
    public class UserActivityFilter
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        /// <summary>Null = all users. Non-null = specific UserId.</summary>
        public int? UserId { get; set; }

        /// <summary>Null or empty = all action types.</summary>
        public string ActionType { get; set; }

        /// <summary>Null or empty = all entity types.</summary>
        public string EntityType { get; set; }

        /// <summary>When true, rows from dbo.AuditTrail are UNION-ed into the result.</summary>
        public bool IncludeAuditTrail { get; set; }
    }
}
