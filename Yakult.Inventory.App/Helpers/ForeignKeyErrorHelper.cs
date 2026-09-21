using System;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// Centralized handling for SQL Server foreign-key constraint violations (error 547)
    /// encountered during DELETE operations across the app.
    ///
    /// Before this helper existed, every ViewModel/Page that performed a permanent delete
    /// duplicated its own ad-hoc detection (some checked <c>SqlException.Number == 547</c>,
    /// others string-matched "REFERENCE constraint" on <c>ex.Message</c>) and its own
    /// friendly-message mapping. That duplication meant inconsistent wording, and any new
    /// delete flow risked showing a raw SQL error to the user instead of an actionable message.
    ///
    /// Usage:
    /// <code>
    /// try
    /// {
    ///     await _repo.DeleteAsync(id);
    /// }
    /// catch (SqlException ex) when (ForeignKeyErrorHelper.IsForeignKeyViolation(ex))
    /// {
    ///     string message = ForeignKeyErrorHelper.BuildFriendlyMessage(ex, "branch");
    ///     RequestError?.Invoke("Cannot Delete", message);
    /// }
    /// </code>
    /// </summary>
    public static class ForeignKeyErrorHelper
    {
        /// <summary>
        /// SQL Server error number for "The DELETE statement conflicted with the REFERENCE
        /// constraint" (and the equivalent for INSERT/UPDATE FK violations). This is the
        /// reliable, locale-independent way to detect the condition — prefer this over
        /// string-matching <c>ex.Message</c>, which breaks on non-English SQL Server
        /// installations and is fragile against message wording changes.
        /// </summary>
        public const int Fk_ViolationErrorNumber = 547;

        /// <summary>
        /// Returns true if the given exception (or any exception in its chain) represents
        /// a SQL Server foreign-key constraint violation.
        /// </summary>
        public static bool IsForeignKeyViolation(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is SqlException sqlEx && sqlEx.Number == Fk_ViolationErrorNumber)
                    return true;
            }

            // Fallback for callers on paths where the original SqlException was already
            // rethrown as a generic Exception (message text preserved). Kept for backward
            // compatibility with existing repository code that does this; new code should
            // let the SqlException propagate so the Number check above can be used.
            return ex != null &&
                   (ex.Message.IndexOf("REFERENCE constraint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ex.Message.IndexOf("conflicted with the", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Extracts the referencing table name (e.g. "dbo.SetItem") from a SQL Server FK
        /// violation message, if present. Returns null if the message doesn't match the
        /// expected shape.
        /// </summary>
        public static string ExtractReferencingTable(string sqlErrorMessage)
        {
            if (string.IsNullOrEmpty(sqlErrorMessage))
                return null;

            // SQL Server phrasing: The DELETE statement conflicted with the REFERENCE
            // constraint "FK_Foo_Bar". The conflict occurred in database "X", table
            // "dbo.SetItem", column 'BranchId'.
            var match = Regex.Match(sqlErrorMessage, "table \"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Maps a raw table name to a short, human-readable phrase suitable for end-user
        /// messages (e.g. "dbo.SetItem" -> "a Set"). Extend this map as new FK relationships
        /// are added to the schema. Falls back to "another record" for unmapped tables.
        /// </summary>
        public static string DescribeTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
                return "another record";

            switch (tableName)
            {
                case "dbo.Set":
                case "dbo.SetItem":
                    return "a Set";
                case "dbo.Request":
                case "dbo.RequestItem":
                    return "a Request";
                case "dbo.Inventory":
                    return "an Inventory record";
                case "dbo.Item":
                    return "an Item";
                case "dbo.Employee":
                    return "an Employee";
                case "dbo.Invoice":
                case "dbo.InvoiceItem":
                    return "an Invoice";
                case "dbo.Receipt":
                case "dbo.ReceiptSet":
                    return "a Receipt";
                case "dbo.CallTicket":
                case "dbo.RepairTicket":
                    return "a Repair/Call Ticket";
                case "dbo.CartridgeSet":
                case "dbo.Cartridge":
                    return "a Cartridge record";
                case "dbo.BorrowItem":
                    return "a Borrowed Item record";
                case "dbo.EmployeeEmail":
                case "dbo.Email":
                    return "an Email binding";
                default:
                    return "another record";
            }
        }

        /// <summary>
        /// Builds a complete, user-facing message for a blocked delete. Combines the
        /// referencing-table lookup with a consistent template, e.g.:
        /// "Cannot delete this branch because it is still linked to a Set. Remove or
        /// reassign the related records first, or use Archive instead."
        /// </summary>
        /// <param name="ex">The caught exception (SqlException or wrapped Exception).</param>
        /// <param name="entityLabel">Lowercase noun for the entity being deleted, e.g. "branch", "vendor", "email address".</param>
        /// <param name="suggestArchive">If true, appends a suggestion to use Archive/Deactivate instead.</param>
        public static string BuildFriendlyMessage(Exception ex, string entityLabel, bool suggestArchive = true)
        {
            string table = ExtractReferencingTable(ex?.Message);
            string relatedDescription = DescribeTable(table);

            string message = $"Cannot delete this {entityLabel} because it is still linked to {relatedDescription}.";
            if (suggestArchive)
                message += " Remove or reassign the related records first, or use Archive/Deactivate instead.";

            return message;
        }

        /// <summary>
        /// Short reason string for bulk-delete result lists (one line per failed item), e.g.
        /// "Has related records (a Set)". Use this where the existing UI already shows a
        /// per-item bullet list with a short reason, rather than the full sentence from
        /// <see cref="BuildFriendlyMessage"/>.
        /// </summary>
        public static string BuildShortReason(Exception ex)
        {
            string table = ExtractReferencingTable(ex?.Message);
            string relatedDescription = DescribeTable(table);
            return $"Has related records ({relatedDescription})";
        }
    }
}
