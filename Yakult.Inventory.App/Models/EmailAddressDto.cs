using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Represents a central email address registry entry
    /// </summary>
    public class EmailAddressDto
    {
        public int EmailId { get; set; }
        public string EmailAddress { get; set; }
        public string DisplayName { get; set; }
        public bool IsActive { get; set; }

        // Audit fields
        public DateTime? DateCreated { get; set; }
        public int? CreatedByUserId { get; set; }
        public DateTime? DateModified { get; set; }
        public int? ModifiedByUserId { get; set; }
    }

    /// <summary>
    /// One record in another table that still points at an <see cref="EmailAddressDto"/>.
    /// Populated by <c>EmailRepository.GetEmailAddressReferencesAsync</c> so the UI can
    /// explain why a hard delete is blocked and what an unlink would change.
    /// </summary>
    public class EmailAddressReferenceDto
    {
        /// <summary>Grouping label, e.g. "SMTP Profile", "Employee Email", "Branch".</summary>
        public string Category { get; set; }

        /// <summary>Human-readable identity of the specific linked record.</summary>
        public string Description { get; set; }

        /// <summary>What an "Unlink All" operation will do to this record.</summary>
        public string OnUnlink { get; set; }

        /// <summary>
        /// True when clearing this reference requires deleting the row itself
        /// (SMTP profiles — their From-address column is NOT NULL).
        /// </summary>
        public bool RequiresSmtpDeletion { get; set; }
    }
}
