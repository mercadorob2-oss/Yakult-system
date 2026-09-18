using System;
using System.Data.SqlClient;
using Dapper;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Shared find-or-create logic for dbo.SetItemSubTypeGroup — every code path that
    /// inserts dbo.SetItem rows tagged with a Sub-Type (ServiceSetRepository's create/update
    /// paths, InvoiceRepository's Bulk Add) needs to resolve the same group row instead of
    /// each maintaining its own copy of this SQL.
    /// </summary>
    public static class SetItemSubTypeGroupHelper
    {
        /// <summary>
        /// Finds the dbo.SetItemSubTypeGroup row matching this SetId+SubType+ReferenceCode
        /// (BeginDate/EndDate compared with NULLs treated as equal), or creates one. Returns
        /// null if subType is blank (no group). Must run on the same connection/transaction
        /// as the SetItem insert that will reference the returned GroupId.
        ///
        /// The four financial arguments are the group's OWN figures — each group carries its
        /// own VAT/WHT/Discount percentages and an optional subtotal override, independent of
        /// the invoice header's. ViewInvoiceDetailPage reads them back through
        /// dbo.vw_SetItemSubTypeGroups, which prefers them over the computed line sum whenever
        /// they are not NULL. Leave them null to keep the previous behaviour (group inherits
        /// the header's percentages at display time); callers that predate per-group
        /// financials pass nothing and are unaffected.
        /// </summary>
        public static int? FindOrCreateGroupId(
            SqlConnection connection, SqlTransaction transaction,
            int setId, string subType, string referenceCode, DateTime? beginDate, DateTime? endDate,
            int? createdBy,
            decimal? vatPercent = null, decimal? whtPercent = null,
            decimal? discountPercent = null, decimal? subtotalOverride = null)
        {
            if (string.IsNullOrWhiteSpace(subType)) return null;

            var parameters = new
            {
                SetId = setId,
                SubType = subType,
                ReferenceCode = referenceCode,
                BeginDate = beginDate,
                EndDate = endDate,
                CreatedBy = createdBy,
                VatPercent = vatPercent,
                WhtPercent = whtPercent,
                DiscountPercent = discountPercent,
                SubtotalOverride = subtotalOverride
            };

            const string findSql = @"
                SELECT TOP 1 GroupId FROM dbo.SetItemSubTypeGroup
                WHERE SetId = @SetId AND SubType = @SubType
                  AND ISNULL(ReferenceCode, '') = ISNULL(@ReferenceCode, '')
                  AND ISNULL(BeginDate, '1900-01-01') = ISNULL(@BeginDate, '1900-01-01')
                  AND ISNULL(EndDate, '1900-01-01') = ISNULL(@EndDate, '1900-01-01')";
            var existing = connection.QuerySingleOrDefault<int?>(findSql, parameters, transaction);

            if (existing.HasValue)
            {
                // Several SetItem rows resolve to the same group, so this runs once per line.
                // COALESCE keeps whatever is already stored when this caller supplies nothing,
                // so a later line without financials can never blank out an earlier line's.
                if (vatPercent.HasValue || whtPercent.HasValue || discountPercent.HasValue || subtotalOverride.HasValue)
                {
                    const string updateSql = @"
                        UPDATE dbo.SetItemSubTypeGroup
                           SET VatPercent       = COALESCE(@VatPercent,       VatPercent),
                               WhtPercent       = COALESCE(@WhtPercent,       WhtPercent),
                               DiscountPercent  = COALESCE(@DiscountPercent,  DiscountPercent),
                               SubtotalOverride = COALESCE(@SubtotalOverride, SubtotalOverride)
                         WHERE GroupId = @GroupId";
                    connection.Execute(updateSql,
                        new
                        {
                            GroupId = existing.Value,
                            VatPercent = vatPercent,
                            WhtPercent = whtPercent,
                            DiscountPercent = discountPercent,
                            SubtotalOverride = subtotalOverride
                        },
                        transaction);
                }
                return existing;
            }

            const string insertSql = @"
                INSERT INTO dbo.SetItemSubTypeGroup
                    (SetId, SubType, ReferenceCode, BeginDate, EndDate,
                     VatPercent, WhtPercent, DiscountPercent, SubtotalOverride,
                     CreatedBy, CreatedAt)
                VALUES
                    (@SetId, @SubType, @ReferenceCode, @BeginDate, @EndDate,
                     @VatPercent, @WhtPercent, @DiscountPercent, @SubtotalOverride,
                     @CreatedBy, sysutcdatetime());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
            return connection.QuerySingle<int>(insertSql, parameters, transaction);
        }
    }
}
