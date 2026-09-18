using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.Repositories
{
    public class SearchRepository
    {
        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public SearchRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        /// <summary>
        /// Searches items by name, description, serial number, or model number.
        /// Returns up to maxResults items with their valid destinations based on actual data sources.
        /// </summary>
        public async Task<List<ItemDto>> SearchItemsAsync(string query, int maxResults = 50)
        {
            var results = new List<ItemDto>();

            var tokens = TokenizeQuery(query);
            if (tokens.Count == 0)
                return results;
            int n = tokens.Count;

            // Every token must land in at least one of Name/SerialNumber/ModelNumber/ReferenceCode
            // (AND of ORs) — same tokenization used by the AutoCompleteBox suggestions, so a
            // multi-word query like "1389 samsung" behaves identically whether it's typed into the
            // dropdown or run as a full page search.
            string itemsTokenClause = BuildTokenClause(n,
                "i.Name LIKE {0}",
                "i.SerialNumber LIKE {0}",
                "i.ModelNumber LIKE {0}",
                "EXISTS (SELECT 1 FROM dbo.SetItem sirc2 WHERE sirc2.ItemId = i.ItemId AND sirc2.ReferenceCode LIKE {0})");
            string refCodeAnyToken = BuildAnyTokenClause(n, "sirc.ReferenceCode LIKE {0}");
            string nameAnyToken    = BuildAnyTokenClause(n, "i.Name LIKE {0}");
            string serialAnyToken  = BuildAnyTokenClause(n, "i.SerialNumber LIKE {0}");
            string modelAnyToken   = BuildAnyTokenClause(n, "i.ModelNumber LIKE {0}");

            // Step 1: Search for matching items (basic search without destination filtering)
            string searchSql = $@"
SELECT TOP (@MaxResults)
    i.ItemId,
    i.Name,
    i.Description,
    i.Active,
    ISNULL(i.Category, '') AS Category,
    i.SerialNumber,
    i.ModelNumber,
    ISNULL(i.CategoryId, 0) AS CategoryId,
    ISNULL(i.UnitOfMeasure, '') AS UnitOfMeasure,
    ISNULL(i.StockOnHand, 0) AS StockOnHand,
    ISNULL(i.ItemType, 'Hardware') AS ItemType,
    ISNULL(i.Amount, 0) AS Amount,
    i.DateCreated,
    i.CreatedBy,
    i.DateModified,
    i.ModifiedBy,
    ISNULL(i.ConditionID, 1) AS ConditionID,
    ISNULL(cond.ConditionName, '') AS ConditionName,
    i.VendorId,
    v.VendorName,
    i.Remarks,
    i.RefillStatus,
    ISNULL(i.AffectsInventory, 0) AS AffectsInventory,
    i.AcquisitionType,
    ISNULL(i.IsTrackedAsset, 0) AS IsTrackedAsset,
    ISNULL(i.WarrantyYears, 0) AS WarrantyYears,
    i.WarrantyStartDate,
    i.WarrantyEndDate,
    -- Set-level dates for warranty qualification (mirrors ViewWarrantyPage logic)
    COALESCE(s.StartDate, i.StartDate)  AS EffectiveStartDate,
    COALESCE(s.EndDate, i.EndDate)      AS EffectiveEndDate,
    CASE WHEN s.EndDate IS NOT NULL THEN 1 ELSE 0 END AS HasSetDates,
    CAST(CASE WHEN arch.EntityId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsArchived,
    -- Sub-Type Group Reference Code (Contract Code/License ID/etc.) that matched, if any —
    -- lets the UI show/navigate on the matched code instead of falling back to the item name.
    (SELECT TOP 1 sirc.ReferenceCode
     FROM dbo.SetItem sirc
     WHERE sirc.ItemId = i.ItemId AND ({refCodeAnyToken})
     ORDER BY sirc.SetItemId DESC) AS MatchedReferenceCode
FROM dbo.Item i
LEFT JOIN dbo.Condition cond ON cond.ConditionID = i.ConditionID
LEFT JOIN dbo.Vendor v ON v.VendorId = i.VendorId
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
-- OUTER APPLY instead of plain JOIN so one item in many Sets doesn't produce duplicate rows
OUTER APPLY (
    SELECT TOP 1 s2.StartDate, s2.EndDate
    FROM dbo.SetItem si2
    INNER JOIN dbo.[Set] s2 ON s2.SetId = si2.SetId AND s2.IsInvoice = 1
    WHERE si2.ItemId = i.ItemId
    ORDER BY s2.SetId DESC
) AS s
WHERE ({itemsTokenClause})
ORDER BY
    CASE WHEN ({nameAnyToken}) THEN 0 ELSE 1 END,
    CASE WHEN ({serialAnyToken}) THEN 0 ELSE 1 END,
    CASE WHEN ({modelAnyToken}) THEN 0 ELSE 1 END,
    i.Name,
    i.ItemId;";

            var matchingItems = new List<ItemDto>();

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();

                // Step 1: Get matching items
                using (var cmd = new SqlCommand(searchSql, con))
                {
                    for (int t = 0; t < tokens.Count; t++)
                        cmd.Parameters.AddWithValue("@Token" + t, "%" + tokens[t] + "%");
                    cmd.Parameters.AddWithValue("@MaxResults", maxResults);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new ItemDto
                            {
                                ItemId       = reader.GetInt32(reader.GetOrdinal("ItemId")),
                                Name         = reader.GetString(reader.GetOrdinal("Name")),
                                Description  = reader.IsDBNull(reader.GetOrdinal("Description"))  ? null : reader.GetString(reader.GetOrdinal("Description")),
                                Active       = reader.GetBoolean(reader.GetOrdinal("Active")),
                                Category     = reader.GetString(reader.GetOrdinal("Category")),
                                SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? null : reader.GetString(reader.GetOrdinal("SerialNumber")),
                                ModelNumber  = reader.IsDBNull(reader.GetOrdinal("ModelNumber"))  ? null : reader.GetString(reader.GetOrdinal("ModelNumber")),
                                CategoryId   = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                                UnitOfMeasure= reader.GetString(reader.GetOrdinal("UnitOfMeasure")),
                                StockOnHand  = reader.GetInt32(reader.GetOrdinal("StockOnHand")),
                                ItemType     = reader.GetString(reader.GetOrdinal("ItemType")),
                                Amount       = reader.GetDecimal(reader.GetOrdinal("Amount")),
                                DateCreated  = reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                                CreatedByUserId = reader.GetInt32(reader.GetOrdinal("CreatedBy")),
                                DateModified    = reader.IsDBNull(reader.GetOrdinal("DateModified")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DateModified")),
                                ModifiedByUserId= reader.IsDBNull(reader.GetOrdinal("ModifiedBy"))   ? (int?)null    : reader.GetInt32(reader.GetOrdinal("ModifiedBy")),
                                ConditionId     = reader.GetInt32(reader.GetOrdinal("ConditionID")),
                                ConditionName   = reader.GetString(reader.GetOrdinal("ConditionName")),
                                VendorId        = reader.IsDBNull(reader.GetOrdinal("VendorId"))    ? (int?)null    : reader.GetInt32(reader.GetOrdinal("VendorId")),
                                VendorName      = reader.IsDBNull(reader.GetOrdinal("VendorName"))  ? null          : reader.GetString(reader.GetOrdinal("VendorName")),
                                Remarks         = reader.IsDBNull(reader.GetOrdinal("Remarks"))     ? null          : reader.GetString(reader.GetOrdinal("Remarks")),
                                RefillStatus    = reader.IsDBNull(reader.GetOrdinal("RefillStatus"))? null          : reader.GetString(reader.GetOrdinal("RefillStatus")),
                                AffectsInventory= reader.GetBoolean(reader.GetOrdinal("AffectsInventory")),
                                AcquisitionType = reader.IsDBNull(reader.GetOrdinal("AcquisitionType")) ? null : reader.GetString(reader.GetOrdinal("AcquisitionType")),
                                IsTrackedAsset  = reader.GetBoolean(reader.GetOrdinal("IsTrackedAsset")),
                                WarrantyYears   = reader.GetInt32(reader.GetOrdinal("WarrantyYears")),
                                WarrantyStartDate = reader.IsDBNull(reader.GetOrdinal("WarrantyStartDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("WarrantyStartDate")),
                                WarrantyEndDate   = reader.IsDBNull(reader.GetOrdinal("WarrantyEndDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("WarrantyEndDate")),
                                StartDate       = reader.IsDBNull(reader.GetOrdinal("EffectiveStartDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("EffectiveStartDate")),
                                EndDate         = reader.IsDBNull(reader.GetOrdinal("EffectiveEndDate"))   ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("EffectiveEndDate")),
                                IsArchived      = reader.GetBoolean(reader.GetOrdinal("IsArchived")),
                                MatchedSubTypeReferenceCode = reader.IsDBNull(reader.GetOrdinal("MatchedReferenceCode")) ? null : reader.GetString(reader.GetOrdinal("MatchedReferenceCode"))
                            };
                            matchingItems.Add(dto);
                        }
                    }
                }

                // Step 2: For each item, check which destinations it appears in
                foreach (var item in matchingItems)
                {
                    var destinations = await GetValidDestinationsAsync(con, item, query);
                    if (destinations.Count > 0)
                    {
                        item.ValidDestinations = destinations;
                        results.Add(item);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Checks which destinations an item appears in based on each page's data source.
        /// </summary>
        private async Task<System.Collections.Generic.HashSet<string>> GetValidDestinationsAsync(SqlConnection con, ItemDto item, string searchQuery)
        {
            var destinations = new System.Collections.Generic.HashSet<string>();

            // Check Archive
            if (item.IsArchived)
            {
                destinations.Add("Archive");
                return destinations; // Archived items only go to Archive
            }

            bool isCartridge = string.Equals(item.Category?.Trim(), "Cartridge", StringComparison.OrdinalIgnoreCase);

            // Cartridge items are managed by the Cartridge Management Portal (StockOnHand-based).
            // They do not have dbo.Inventory entries and should not route to the Inventory page.
            if (!isCartridge)
            {
                // Check Inventory: Item must have active inventory entries
                // Matches ViewInventoryPage logic: EntryType filter, not archived, and item must be Active (unless Show Inactive is checked)
                const string inventoryCheck = @"
                SELECT COUNT(*)
                FROM dbo.Inventory inv
                LEFT JOIN dbo.ArchiveStatus arch_inv ON arch_inv.EntityType = 'Inventory' AND arch_inv.EntityId = inv.InvId AND arch_inv.IsArchived = 1
                LEFT JOIN dbo.ArchiveStatus arch_itm ON arch_itm.EntityType = 'Item' AND arch_itm.EntityId = inv.ItemId AND arch_itm.IsArchived = 1
                LEFT JOIN dbo.Item it ON it.ItemId = inv.ItemId
                WHERE inv.ItemId = @ItemId
                  AND inv.EntryType IN ('Negative', 'Positive', 'Fixed Assets')
                  AND arch_inv.EntityId IS NULL
                  AND arch_itm.EntityId IS NULL
                  AND it.Active = 1";

                using (var cmd = new SqlCommand(inventoryCheck, con))
                {
                    cmd.Parameters.AddWithValue("@ItemId", item.ItemId);
                    var count = (int)await cmd.ExecuteScalarAsync();
                    if (count > 0)
                        destinations.Add("Inventory");
                }
            }

            // Check Items: Matches ViewItemsPage logic
            // The page shows items where: not archived, and (Active = 1 OR Show Inactive is checked)
            // For serialized items: also checks if item is requested
            // For non-serialized items: checks if remaining quantity > 0
            // Simplified: check if item is not archived and Active = 1 (default Show Inactive unchecked)
            if (item.Active && !item.IsArchived)
            {
                destinations.Add("Items");
            }

            // Check Warranty: Items with valid warranty dates
            bool qualifiesForWarranty = false;
            if (item.ItemType == "Software/License")
            {
                qualifiesForWarranty = item.EndDate.HasValue;
            }
            else
            {
                qualifiesForWarranty = item.WarrantyYears > 0 || item.EndDate.HasValue;
            }
            if (qualifiesForWarranty)
            {
                destinations.Add("Warranty");
            }

            // Check Repaired Items: Hardware items (not archived, not cartridges)
            // Cartridges are consumables managed by the Cartridge Management Portal — not tracked as repairable hardware.
            bool isHardwareType = string.IsNullOrWhiteSpace(item.ItemType) ||
                                  string.Equals(item.ItemType?.Trim(), "Hardware", StringComparison.OrdinalIgnoreCase);

            if (isHardwareType && !item.IsArchived && !isCartridge)
            {
                destinations.Add("Repaired Items");
            }

            // Check Fixed Assets: Tracked assets
            if (item.IsTrackedAsset)
            {
                destinations.Add("Fixed Assets");
            }

            // Badge counts use the same string that SearchCardViewModel.ComputeTitle resolves
            // to — which is what gets passed to ApplyInitialSearch on each destination page.
            // Priority: SerialNumber → ModelNumber → Sub-Type Reference Code → Name (whichever
            // field matched the query). Presence checks remain ItemId-based to avoid false positives.
            string queryLower = (searchQuery ?? "").Trim().ToLowerInvariant();
            string navigationTerm;
            if (!string.IsNullOrWhiteSpace(item.SerialNumber) && item.SerialNumber.ToLowerInvariant().Contains(queryLower))
                navigationTerm = item.SerialNumber;
            else if (!string.IsNullOrWhiteSpace(item.ModelNumber) && item.ModelNumber.ToLowerInvariant().Contains(queryLower))
                navigationTerm = item.ModelNumber;
            else if (!string.IsNullOrWhiteSpace(item.MatchedSubTypeReferenceCode))
                navigationTerm = item.MatchedSubTypeReferenceCode;
            else
                navigationTerm = item.Name ?? "";
            string nameLike = "%" + navigationTerm.Replace("[", "[[]").ToLower() + "%";

            // Check Set: Item appears in a non-invoice, non-renewal Set
            const string setPresence = @"
                SELECT COUNT(DISTINCT s.SetId)
                FROM dbo.SetItem si
                INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
                WHERE si.ItemId = @ItemId
                  AND (s.IsInvoice IS NULL OR s.IsInvoice = 0)
                  AND s.RenewalOfSetId IS NULL";

            using (var cmd = new SqlCommand(setPresence, con))
            {
                cmd.Parameters.AddWithValue("@ItemId", item.ItemId);
                var presence = (int)await cmd.ExecuteScalarAsync();
                if (presence > 0)
                {
                    destinations.Add("Set");
                    const string setCount = @"
                        SELECT COUNT(DISTINCT s.SetId)
                        FROM dbo.[Set] s
                        INNER JOIN dbo.SetItem si ON si.SetId = s.SetId
                        WHERE (s.IsInvoice IS NULL OR s.IsInvoice = 0)
                          AND s.RenewalOfSetId IS NULL
                          AND LOWER(ISNULL(si.Description, '')) LIKE @NameLike";
                    using (var cnt = new SqlCommand(setCount, con))
                    {
                        cnt.Parameters.AddWithValue("@NameLike", nameLike);
                        item.DestinationCounts["Set"] = Math.Max(presence, (int)await cnt.ExecuteScalarAsync());
                    }
                }
            }

            // Check Request: Item has at least one request
            const string requestPresence = @"
                SELECT COUNT(*)
                FROM dbo.Request
                WHERE ItemId = @ItemId";

            using (var cmd = new SqlCommand(requestPresence, con))
            {
                cmd.Parameters.AddWithValue("@ItemId", item.ItemId);
                var presence = (int)await cmd.ExecuteScalarAsync();
                if (presence > 0)
                {
                    destinations.Add("Request");
                    const string requestCount = @"
                        SELECT COUNT(*)
                        FROM dbo.Request r
                        INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
                        WHERE LOWER(i.Name) LIKE @NameLike";
                    using (var cnt = new SqlCommand(requestCount, con))
                    {
                        cnt.Parameters.AddWithValue("@NameLike", nameLike);
                        item.DestinationCounts["Request"] = Math.Max(presence, (int)await cnt.ExecuteScalarAsync());
                    }
                }
            }

            // Check Invoice: Item appears in an invoice Set (IsInvoice = 1)
            const string invoicePresence = @"
                SELECT COUNT(DISTINCT s.SetId)
                FROM dbo.SetItem si
                INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
                WHERE si.ItemId = @ItemId
                  AND s.IsInvoice = 1";

            using (var cmd = new SqlCommand(invoicePresence, con))
            {
                cmd.Parameters.AddWithValue("@ItemId", item.ItemId);
                var presence = (int)await cmd.ExecuteScalarAsync();
                if (presence > 0)
                {
                    destinations.Add("Invoice");
                    const string invoiceCount = @"
                        SELECT COUNT(DISTINCT s.SetId)
                        FROM dbo.[Set] s
                        INNER JOIN dbo.SetItem si ON si.SetId = s.SetId
                        WHERE s.IsInvoice = 1
                          AND LOWER(ISNULL(si.Description, '')) LIKE @NameLike";
                    using (var cnt = new SqlCommand(invoiceCount, con))
                    {
                        cnt.Parameters.AddWithValue("@NameLike", nameLike);
                        item.DestinationCounts["Invoice"] = Math.Max(presence, (int)await cnt.ExecuteScalarAsync());
                    }
                }
            }

            // Check Renewal: Item has active renewal records (shared by Renewal and Renewals (Grouped))
            const string renewalPresence = @"
                SELECT COUNT(*)
                FROM dbo.Renewals
                WHERE ItemId = @ItemId AND IsArchived = 0";

            using (var cmd = new SqlCommand(renewalPresence, con))
            {
                cmd.Parameters.AddWithValue("@ItemId", item.ItemId);
                var presence = (int)await cmd.ExecuteScalarAsync();
                if (presence > 0)
                {
                    destinations.Add("Renewal");
                    destinations.Add("Renewals (Grouped)");
                    const string renewalCount = @"
                        SELECT COUNT(*)
                        FROM dbo.Renewals r
                        INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
                        WHERE LOWER(i.Name) LIKE @NameLike
                          AND r.IsArchived = 0";
                    using (var cnt = new SqlCommand(renewalCount, con))
                    {
                        cnt.Parameters.AddWithValue("@NameLike", nameLike);
                        var nameCount = (int)await cnt.ExecuteScalarAsync();
                        item.DestinationCounts["Renewal"] = Math.Max(presence, nameCount);
                        item.DestinationCounts["Renewals (Grouped)"] = Math.Max(presence, nameCount);
                    }
                }
            }

            return destinations;
        }

        /// <summary>
        /// Searches Requests and Sets directly (by SetCode, Request Description/Remarks, or
        /// exact ReqId/SetId) — used by the Home Page global search to fill the gap left by
        /// <see cref="SearchItemsAsync"/>, which only matches via an underlying dbo.Item row.
        /// Results come back as synthetic <see cref="ItemDto"/> instances (IsDirectMatch = true)
        /// with ValidDestinations/DestinationCounts pre-populated so the caller can feed them
        /// straight into the existing card-grouping pipeline without going through
        /// <see cref="GetValidDestinationsAsync"/>.
        /// </summary>
        public async Task<List<ItemDto>> SearchDirectMatchesAsync(string query, bool includeArchived, int maxResults = 100)
        {
            var results = new List<ItemDto>();

            var tokens = TokenizeQuery(query);
            if (tokens.Count == 0)
                return results;
            int n = tokens.Count;

            string archivedGuardRequest = includeArchived ? "" : "AND arch_r.EntityId IS NULL";
            string archivedGuardSet = includeArchived ? "" : "AND arch_s.EntityId IS NULL";

            // Same AND-of-ORs tokenization as the suggestions dropdown: every token must land in at
            // least one relevant column, in any order — "1389 samsung" and "samsung 1389" both match.
            // The whole-string exact-ID lookups (TRY_CONVERT on @RawQuery) are intentionally left
            // un-tokenized: a numeric ReqId/SetId lookup only makes sense against the raw input.
            string requestTokens = BuildTokenClause(n, "r.Description LIKE {0}", "r.Remarks LIKE {0}");
            string setTokens = BuildTokenClause(n, "s.SetCode LIKE {0}",
                "EXISTS (SELECT 1 FROM dbo.SetItem sit INNER JOIN dbo.SetItemParentTagGroup ptg ON ptg.ParentTagGroupId = sit.ParentTagGroupId WHERE sit.SetId = s.SetId AND ptg.Label LIKE {0})");

            string sql = $@"
SELECT TOP (@MaxResults)
    'Request' AS ResultType,
    r.ReqId AS Id,
    ISNULL(i.Name, r.Description) AS Title,
    COALESCE(
        e.Name,
        NULLIF(LTRIM(RTRIM(
            ISNULL(b.Name, '') + CASE WHEN b.Name IS NOT NULL AND d.Name IS NOT NULL THEN ', ' ELSE '' END + ISNULL(d.Name, '')
        )), '')
    ) AS Subtitle,
    r.Status AS Status,
    CAST(CASE WHEN arch_r.EntityId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsArchived
FROM dbo.Request r
LEFT JOIN dbo.Item i ON r.ItemId = i.ItemId
LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
LEFT JOIN dbo.Branch b ON r.BranchId = b.BranchId
LEFT JOIN dbo.Department d ON r.DeptId = d.DeptId
LEFT JOIN dbo.ArchiveStatus arch_r ON arch_r.EntityType = 'Request' AND arch_r.EntityId = r.ReqId AND arch_r.IsArchived = 1
WHERE (({requestTokens}) OR r.ReqId = TRY_CONVERT(int, @RawQuery))
  {archivedGuardRequest}

UNION ALL

SELECT TOP (@MaxResults)
    -- Invoices are dbo.[Set] rows with IsInvoice = 1 — there is no separate Invoice table. Reporting
    -- this as its own ResultType (rather than always 'Set') is what lets the caller badge the card as
    -- 'Invoice', build invoice-appropriate detail fields, and route the Go-to button at
    -- ViewInvoiceReportPage instead of the plain Set page.
    CASE WHEN s.IsInvoice = 1 THEN 'Invoice' ELSE 'Set' END AS ResultType,
    s.SetId AS Id,
    s.SetCode AS Title,
    u.Name AS Subtitle,
    s.Status AS Status,
    CAST(CASE WHEN arch_s.EntityId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsArchived
FROM dbo.[Set] s
LEFT JOIN dbo.[User] u ON s.CreatedBy = u.UserId
LEFT JOIN dbo.ArchiveStatus arch_s ON arch_s.EntityType = 'Set' AND arch_s.EntityId = s.SetId AND arch_s.IsArchived = 1
WHERE (({setTokens}) OR s.SetId = TRY_CONVERT(int, @RawQuery))
  {archivedGuardSet};";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@MaxResults", maxResults);
                cmd.Parameters.AddWithValue("@RawQuery", query.Trim());
                for (int t = 0; t < tokens.Count; t++)
                    cmd.Parameters.AddWithValue("@Token" + t, "%" + tokens[t] + "%");

                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string resultType = reader.GetString(0);
                        string destination = resultType; // "Request", "Set", or "Invoice" — already resolved in SQL
                        string title = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        string subtitle = reader.IsDBNull(3) ? null : reader.GetString(3);
                        string status = reader.IsDBNull(4) ? null : reader.GetString(4);
                        bool isArchived = reader.GetBoolean(5);

                        var dto = new ItemDto
                        {
                            ItemId = 0,
                            Name = string.IsNullOrWhiteSpace(title) ? $"({destination} match)" : title,
                            Category = destination,
                            ItemType = destination,
                            Active = true,
                            IsArchived = isArchived,
                            DateCreated = DateTime.Now,
                            IsDirectMatch = true,
                            DirectMatchStatus = status,
                            DirectMatchSubtitle = subtitle
                        };
                        dto.ValidDestinations.Add(destination);
                        dto.DestinationCounts[destination] = 1;
                        results.Add(dto);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Searches Requests and Sets together — used by the Consumable Management Portal's
        /// Card 2 (Request &amp; Set Management) home search. Requests match on Description,
        /// Remarks, requester name, item name, or exact ReqId. Sets match on SetCode, the
        /// creating user's name, or exact SetId. Results from both are merged and sorted by
        /// creation date, most recent first.
        /// </summary>
        public async Task<List<RequestSetSearchResultDto>> SearchRequestsAndSetsAsync(string query, int maxResults = 50)
        {
            var results = new List<RequestSetSearchResultDto>();

            if (string.IsNullOrWhiteSpace(query))
                return results;

            const string sql = @"
SELECT ResultType, Id, Title, Subtitle, Status, SortDate FROM (
    SELECT TOP (@MaxResults)
        'Request'        AS ResultType,
        r.ReqId           AS Id,
        ISNULL(i.Name, r.Description) AS Title,
        -- Requester name when the request is tied to a specific employee; otherwise (dept-level
        -- submissions, which store ComId/DeptId/BranchId directly on Request with EmpId NULL)
        -- fall back to Branch, Department so the card isn't left with a blank subtitle.
        COALESCE(
            e.Name,
            NULLIF(LTRIM(RTRIM(
                ISNULL(b.Name, '') + CASE WHEN b.Name IS NOT NULL AND d.Name IS NOT NULL THEN ', ' ELSE '' END + ISNULL(d.Name, '')
            )), '')
        ) AS Subtitle,
        r.Status          AS Status,
        r.DateCreated     AS SortDate
    FROM dbo.Request r
    LEFT JOIN dbo.Item i       ON r.ItemId = i.ItemId
    LEFT JOIN dbo.Employee e   ON r.EmpId  = e.EmpId
    LEFT JOIN dbo.Branch b     ON r.BranchId = b.BranchId
    LEFT JOIN dbo.Department d ON r.DeptId   = d.DeptId
    WHERE r.Description LIKE @Query
       OR r.Remarks      LIKE @Query
       OR i.Name         LIKE @Query
       OR e.Name         LIKE @Query
       OR b.Name         LIKE @Query
       OR d.Name         LIKE @Query
       OR r.ReqId = TRY_CONVERT(int, @RawQuery)
    ORDER BY r.DateCreated DESC
) reqResults

UNION ALL

SELECT ResultType, Id, Title, Subtitle, Status, SortDate FROM (
    SELECT TOP (@MaxResults)
        'Set'         AS ResultType,
        s.SetId        AS Id,
        s.SetCode      AS Title,
        u.Name         AS Subtitle,
        s.Status       AS Status,
        s.CreatedAt    AS SortDate
    FROM dbo.[Set] s
    LEFT JOIN dbo.[User] u ON s.CreatedBy = u.UserId
    WHERE s.SetCode LIKE @Query
       OR u.Name     LIKE @Query
       OR s.SetId = TRY_CONVERT(int, @RawQuery)
    ORDER BY s.CreatedAt DESC
) setResults

ORDER BY SortDate DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@MaxResults", maxResults);
                cmd.Parameters.AddWithValue("@Query", $"%{query.Trim()}%");
                cmd.Parameters.AddWithValue("@RawQuery", query.Trim());

                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new RequestSetSearchResultDto
                        {
                            ResultType = reader.GetString(0),
                            Id         = reader.GetInt32(1),
                            Title      = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Subtitle   = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            Status     = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            SortDate   = reader.GetDateTime(5)
                        });
                    }
                }
            }

            return results.Count > maxResults ? results.GetRange(0, maxResults) : results;
        }

        /// <summary>
        /// Gets search suggestions quickly by matching Name, SerialNumber, ModelNumber, or a
        /// Sub-Type Group Reference Code (Contract Code/License ID/etc.) on one of the item's
        /// dbo.SetItem lines. Returns up to maxResults distinct results.
        /// </summary>
        public async Task<List<string>> GetSearchSuggestionsAsync(string query, int maxResults = 8)
        {
            var suggestions = new List<string>();

            if (string.IsNullOrWhiteSpace(query))
                return suggestions;

            // Search across Name, SerialNumber, ModelNumber, and Sub-Type Reference Code.
            // Using UNION to collect distinct matching fields.
            const string sql = @"
SELECT TOP (@MaxResults) MatchText
FROM (
    SELECT Name AS MatchText FROM dbo.Item WHERE Active = 1 AND Name LIKE @Query
    UNION
    SELECT SerialNumber AS MatchText FROM dbo.Item WHERE Active = 1 AND SerialNumber LIKE @Query AND SerialNumber IS NOT NULL AND SerialNumber <> ''
    UNION
    SELECT ModelNumber AS MatchText FROM dbo.Item WHERE Active = 1 AND ModelNumber LIKE @Query AND ModelNumber IS NOT NULL AND ModelNumber <> ''
    UNION
    SELECT DISTINCT si.ReferenceCode AS MatchText
    FROM dbo.SetItem si
    INNER JOIN dbo.Item i ON i.ItemId = si.ItemId
    WHERE i.Active = 1 AND si.ReferenceCode LIKE @Query
) AS Matches
ORDER BY LEN(MatchText), MatchText;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Query", "%" + query.Trim() + "%");
                cmd.Parameters.AddWithValue("@MaxResults", maxResults);
                await con.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        suggestions.Add(reader.GetString(0));
                    }
                }
            }

            return suggestions;
        }

        /// <summary>
        /// Splits raw AutoCompleteBox input into clean, lowercase search tokens: whitespace-delimited,
        /// stripped of everything except letters/digits/"-"/"_"/"." (enough to keep SetCode-shaped
        /// tokens like "set-1391" intact while dropping stray punctuation), empties discarded.
        /// Capped at <paramref name="maxTokens"/> so a pathologically long paste can't blow up the
        /// per-token AND-of-ORs clause built in <see cref="GetRichSearchSuggestionsAsync"/>.
        /// </summary>
        private static List<string> TokenizeQuery(string query, int maxTokens = 6)
        {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(query)) return tokens;

            foreach (var raw in query.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                var chars = new List<char>(raw.Length);
                foreach (char c in raw)
                {
                    if (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.')
                        chars.Add(char.ToLowerInvariant(c));
                }
                if (chars.Count == 0) continue;

                tokens.Add(new string(chars.ToArray()));
                if (tokens.Count >= maxTokens) break;
            }

            return tokens;
        }

        /// <summary>Generates ["@Token0", "@Token1", ...] (or any given prefix) — the default, contiguous
        /// naming every exact-match branch uses; the fuzzy fallback instead assigns its own separate
        /// "@Desc0.."/"@Code0.." names so code-like and descriptive tokens can be parameterized independently.</summary>
        private static List<string> TokenParamNames(int count, string prefix = "@Token")
        {
            var names = new List<string>(count);
            for (int i = 0; i < count; i++) names.Add(prefix + i);
            return names;
        }

        /// <summary>
        /// Builds "(colA LIKE @Token0 OR colB LIKE @Token0) AND (colA LIKE @Token1 OR colB LIKE @Token1) ..."
        /// — every token must find a home in at least one of <paramref name="columnExprs"/> somewhere
        /// (OR across columns), and every token must match (AND across tokens), so "set-1391 samsung"
        /// and "samsung set-1391" both match the same row regardless of typed order.
        /// <paramref name="columnExprs"/> entries are plain "column LIKE {0}" style templates, OR a full
        /// custom boolean expression (e.g. an EXISTS subquery) with "{0}" standing in for the per-token
        /// parameter name — see the Invoices branch's Parent Tag check below for the latter.
        /// </summary>
        private static string BuildTokenClause(int tokenCount, params string[] columnExprs)
            => BuildTokenClause(TokenParamNames(tokenCount), columnExprs);

        /// <summary>Same as above but against an explicit parameter-name list rather than the default
        /// "@Token0.." sequence — lets the fuzzy fallback reuse this for just its descriptive tokens
        /// (named "@Desc0.." there) while the code-like token is parameterized and scored separately.</summary>
        private static string BuildTokenClause(IReadOnlyList<string> paramNames, params string[] columnExprs)
        {
            var perToken = new List<string>(paramNames.Count);
            foreach (var paramName in paramNames)
            {
                var arms = new List<string>(columnExprs.Length);
                foreach (var expr in columnExprs)
                    arms.Add(string.Format(expr, paramName));
                perToken.Add("(" + string.Join(" OR ", arms) + ")");
            }
            return string.Join(" AND ", perToken);
        }

        /// <summary>"colExpr LIKE @Token0 OR colExpr LIKE @Token1 OR ..." — any token, not all of them;
        /// used for the Invoices detail line, which just needs to know *which* tag caused the match.</summary>
        private static string BuildAnyTokenClause(int tokenCount, string columnExpr)
            => BuildAnyTokenClause(TokenParamNames(tokenCount), columnExpr);

        private static string BuildAnyTokenClause(IReadOnlyList<string> paramNames, string columnExpr)
        {
            var arms = new List<string>(paramNames.Count);
            foreach (var paramName in paramNames)
                arms.Add(string.Format(columnExpr, paramName));
            return string.Join(" OR ", arms);
        }

        /// <summary>
        /// Powers the Google-style AutoCompleteBox on the home search bar. Tokenizes the raw input and
        /// requires every token to match somewhere across the relevant columns of each of the six master
        /// tables (Items, Sets, Request, Renewals, Invoices, Warranty) — so "set-1391 samsung" and
        /// "samsung set-1391" both find the same invoice regardless of which order the user types the
        /// SetCode/vendor/tag/etc. in. Bounded to <paramref name="maxResults"/> total rows via an outer
        /// TOP so the dropdown never renders more than a handful of items. Returns an empty list for
        /// null/short (&lt; 2 char) queries or on any DB error — callers should treat that as "no
        /// suggestions available" rather than a fatal failure.
        /// </summary>
        public async Task<List<SearchSuggestionDto>> GetRichSearchSuggestionsAsync(
            string query, int maxResults = 15, CancellationToken cancellationToken = default)
        {
            var suggestions = new List<SearchSuggestionDto>();

            if (string.IsNullOrEmpty(query?.Trim()) || query.Trim().Length < 2)
                return suggestions;

            var tokens = TokenizeQuery(query);
            if (tokens.Count == 0)
                return suggestions;

            maxResults = Math.Min(Math.Max(maxResults, 1), 15);
            int n = tokens.Count;

            // Every token must match at least one column for a given master table (AND of ORs) —
            // built once per table here, then dropped into that table's WHERE clause below.
            string itemsTokens    = BuildTokenClause(n, "i.Name LIKE {0}", "i.SerialNumber LIKE {0}", "i.ModelNumber LIKE {0}");
            string setsTokens     = BuildTokenClause(n, "s.SetCode LIKE {0}");
            string requestTokens  = BuildTokenClause(n, "r.Description LIKE {0}", "ri.Name LIKE {0}");
            string renewalsTokens = BuildTokenClause(n, "rni.Name LIKE {0}", "rni.SerialNumber LIKE {0}", "rni.ModelNumber LIKE {0}");
            string invoicesTokens = BuildTokenClause(n, "si.SetCode LIKE {0}",
                "EXISTS (SELECT 1 FROM dbo.SetItem sit INNER JOIN dbo.SetItemParentTagGroup ptg ON ptg.ParentTagGroupId = sit.ParentTagGroupId WHERE sit.SetId = si.SetId AND ptg.Label LIKE {0})");
            string invoicesTagAnyToken = BuildAnyTokenClause(n, "ptg.Label LIKE {0}");
            string warrantyTokens = BuildTokenClause(n, "wi.Name LIKE {0}", "wi.SerialNumber LIKE {0}", "wi.ModelNumber LIKE {0}");

            // Every branch of the UNION already carries its own TOP (@MaxResults) so a query that
            // matches heavily in one master table can't crowd out the other five before the outer
            // TOP even runs.
            string sql = $@"
SELECT TOP (@MaxResults) PrimaryText, DetailText, Destination FROM (

    SELECT TOP (@MaxResults)
        i.Name AS PrimaryText,
        NULLIF(LTRIM(RTRIM(
            ISNULL(i.ModelNumber, '') + CASE WHEN i.ModelNumber IS NOT NULL AND i.SerialNumber IS NOT NULL THEN '  ·  ' ELSE '' END + ISNULL(i.SerialNumber, '')
        )), '') AS DetailText,
        'Items' AS Destination
    FROM dbo.Item i
    WHERE i.Active = 1
      AND {itemsTokens}
    ORDER BY LEN(i.Name)

    UNION ALL

    SELECT TOP (@MaxResults)
        s.SetCode AS PrimaryText,
        NULLIF(s.Status, '') AS DetailText,
        'Sets' AS Destination
    FROM dbo.[Set] s
    WHERE s.Active = 1 AND s.IsInvoice = 0
      AND {setsTokens}
    ORDER BY s.CreatedAt DESC

    UNION ALL

    SELECT TOP (@MaxResults)
        ISNULL(ri.Name, r.Description) AS PrimaryText,
        NULLIF(r.Status, '') AS DetailText,
        'Request' AS Destination
    FROM dbo.Request r
    LEFT JOIN dbo.Item ri ON r.ItemId = ri.ItemId
    WHERE r.Active = 1
      AND {requestTokens}
    ORDER BY r.DateCreated DESC

    UNION ALL

    SELECT TOP (@MaxResults)
        rni.Name AS PrimaryText,
        NULLIF(rn.RenewalStatus, '') AS DetailText,
        'Renewals' AS Destination
    FROM dbo.Renewals rn
    INNER JOIN dbo.Item rni ON rn.ItemId = rni.ItemId
    WHERE rn.IsArchived = 0
      AND {renewalsTokens}
    ORDER BY rn.RenewalId DESC

    UNION ALL

    SELECT TOP (@MaxResults)
        si.SetCode AS PrimaryText,
        -- When the match came from a Parent Tag rather than the invoice's own code, surface the
        -- matched tag as the detail line instead of the invoice status — that's the reason it showed up.
        COALESCE(
            (SELECT TOP 1 ptg.Label
             FROM dbo.SetItem sit
             INNER JOIN dbo.SetItemParentTagGroup ptg ON ptg.ParentTagGroupId = sit.ParentTagGroupId
             WHERE sit.SetId = si.SetId AND ({invoicesTagAnyToken})
             ORDER BY sit.SetItemId),
            NULLIF(si.Status, '')
        ) AS DetailText,
        'Invoices' AS Destination
    FROM dbo.[Set] si
    WHERE si.Active = 1 AND si.IsInvoice = 1
      AND {invoicesTokens}
    ORDER BY si.CreatedAt DESC

    UNION ALL

    SELECT TOP (@MaxResults)
        wi.Name AS PrimaryText,
        'Warranty  ·  ' + CASE
            WHEN wi.WarrantyEndDate IS NOT NULL THEN CONVERT(varchar(11), wi.WarrantyEndDate, 106)
            WHEN wi.EndDate IS NOT NULL         THEN CONVERT(varchar(11), wi.EndDate, 106)
            ELSE 'Active'
        END AS DetailText,
        'Warranty' AS Destination
    FROM dbo.Item wi
    WHERE wi.Active = 1
      AND (wi.WarrantyYears > 0 OR wi.EndDate IS NOT NULL)
      AND {warrantyTokens}
    ORDER BY LEN(wi.Name)

) AS Combined
ORDER BY LEN(PrimaryText);";

            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@MaxResults", maxResults);
                    for (int t = 0; t < tokens.Count; t++)
                        cmd.Parameters.AddWithValue("@Token" + t, "%" + tokens[t] + "%");

                    await con.OpenAsync(cancellationToken);

                    using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            suggestions.Add(new SearchSuggestionDto
                            {
                                Primary     = reader.GetString(0),
                                Detail      = reader.IsDBNull(1) ? null : reader.GetString(1),
                                Destination = reader.GetString(2)
                            });
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw; // let the caller's debounce/cancellation logic see this
            }
            catch (Exception)
            {
                // Connectivity/timeout issues degrade to "no suggestions" rather than crashing the UI.
                return new List<SearchSuggestionDto>();
            }

            return suggestions;
        }

        /// <summary>
        /// Strict "Did you mean...?" fallback — only ever called by the caller after the normal
        /// tokenized search (<see cref="SearchItemsAsync"/>/<see cref="SearchDirectMatchesAsync"/>)
        /// already came back with zero results, so it never touches the hot path.
        ///
        /// Exactly one token gets relaxed into a fuzzy match: the shortest code-shaped token
        /// (SetCode/Serial/Model — has a digit or a "-") if there is one, otherwise the shortest
        /// remaining token if it's a plain descriptive word (e.g. a brand: "Xiaoomi" vs "Xiaomi").
        /// Every OTHER token still has to match exactly somewhere in the row — that's what keeps the
        /// candidate pool "smart" instead of a table scan. The two shapes need different SQL-side
        /// pooling: a code-shaped fuzz token bounds candidates by column LENGTH (±2 of the token,
        /// since it's compared against a whole short column like SetCode); a descriptive fuzz token
        /// instead bounds by a LIKE '%prefix%' on its first few characters, since it's compared
        /// word-by-word against a potentially multi-word column like Item Name — LEN() bounding would
        /// wrongly reject "XIAOMI REDMI 15 5G (8GB 256GB)" for a 7-character token. Both are still
        /// backstopped by TOP 200. Only the shortest fuzz-worthy token is relaxed — matches the common
        /// case of one typo plus exact context, and keeps this bounded no matter how many tokens were
        /// typed. Candidates are scored word-by-word in C# and discarded past edit distance 2.
        /// </summary>
        public async Task<List<ItemDto>> FuzzySearchFallbackAsync(
            string query, int maxResults = 5, CancellationToken cancellationToken = default)
        {
            var results = new List<ItemDto>();

            var tokens = TokenizeQuery(query);
            if (tokens.Count == 0) return results;

            var codeTokens = tokens.Where(SearchTextHelper.LooksLikeCode).ToList();
            var descriptiveTokens = tokens.Except(codeTokens).ToList();

            string fuzzToken;
            bool fuzzTokenIsCode;
            if (codeTokens.Count > 0)
            {
                fuzzToken = codeTokens.OrderBy(t => t.Length).First();
                fuzzTokenIsCode = true;
                descriptiveTokens = tokens.Except(new[] { fuzzToken }).ToList();
            }
            else if (descriptiveTokens.Count > 0)
            {
                fuzzToken = descriptiveTokens.OrderBy(t => t.Length).First();
                fuzzTokenIsCode = false;
                descriptiveTokens = tokens.Except(new[] { fuzzToken }).ToList();
            }
            else
            {
                return results; // shouldn't happen — tokens.Count == 0 already returned above
            }

            int lenLo = Math.Max(1, fuzzToken.Length - 2);
            int lenHi = fuzzToken.Length + 2;
            string fuzzPrefix = "%" + fuzzToken.Substring(0, Math.Min(4, fuzzToken.Length)) + "%";
            maxResults = Math.Min(Math.Max(maxResults, 1), 10);

            var descParams = TokenParamNames(descriptiveTokens.Count, "@Desc");

            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                {
                    await con.OpenAsync(cancellationToken);

                    // (Primary, Detail, Destination, IsArchived, fuzz-candidate values to score against fuzzToken)
                    var pool = new List<(string Primary, string Detail, string Destination, bool IsArchived, string[] FuzzValues)>();

                    await CollectFuzzyCandidatesAsync(con, pool, cancellationToken,
                        destination: "Items",
                        primaryCol: "i.Name",
                        fuzzCols: fuzzTokenIsCode ? new[] { "i.SerialNumber", "i.ModelNumber" } : new[] { "i.Name" },
                        descCols: new[] { "i.Name LIKE {0}", "i.SerialNumber LIKE {0}", "i.ModelNumber LIKE {0}" },
                        from: "FROM dbo.Item i",
                        activeGuard: "i.Active = 1",
                        detailExpr: "ISNULL(i.Category, 'Item')",
                        archivedExpr: "0",
                        descParams: descParams, descriptiveTokens: descriptiveTokens,
                        fuzzTokenIsCode: fuzzTokenIsCode, lenLo: lenLo, lenHi: lenHi, fuzzPrefix: fuzzPrefix);

                    await CollectFuzzyCandidatesAsync(con, pool, cancellationToken,
                        destination: "Set",
                        primaryCol: "s.SetCode",
                        fuzzCols: fuzzTokenIsCode ? new[] { "s.SetCode" } : new[] { "u.Name" },
                        descCols: new[] { "u.Name LIKE {0}" },
                        from: "FROM dbo.[Set] s LEFT JOIN dbo.[User] u ON s.CreatedBy = u.UserId",
                        activeGuard: "s.Active = 1 AND s.IsInvoice = 0",
                        detailExpr: "ISNULL(s.Status, '')",
                        archivedExpr: "0",
                        descParams: descParams, descriptiveTokens: descriptiveTokens,
                        fuzzTokenIsCode: fuzzTokenIsCode, lenLo: lenLo, lenHi: lenHi, fuzzPrefix: fuzzPrefix);

                    const string invoiceParentTagExpr =
                        "(SELECT TOP 1 ptg.Label FROM dbo.SetItem sit INNER JOIN dbo.SetItemParentTagGroup ptg ON ptg.ParentTagGroupId = sit.ParentTagGroupId WHERE sit.SetId = s.SetId)";
                    await CollectFuzzyCandidatesAsync(con, pool, cancellationToken,
                        destination: "Invoice",
                        primaryCol: "s.SetCode",
                        fuzzCols: fuzzTokenIsCode ? new[] { "s.SetCode" } : new[] { invoiceParentTagExpr },
                        descCols: new[] { "EXISTS (SELECT 1 FROM dbo.SetItem sit INNER JOIN dbo.SetItemParentTagGroup ptg ON ptg.ParentTagGroupId = sit.ParentTagGroupId WHERE sit.SetId = s.SetId AND ptg.Label LIKE {0})" },
                        from: "FROM dbo.[Set] s",
                        activeGuard: "s.Active = 1 AND s.IsInvoice = 1",
                        detailExpr: "ISNULL(s.Status, '')",
                        archivedExpr: "0",
                        descParams: descParams, descriptiveTokens: descriptiveTokens,
                        fuzzTokenIsCode: fuzzTokenIsCode, lenLo: lenLo, lenHi: lenHi, fuzzPrefix: fuzzPrefix);

                    await CollectFuzzyCandidatesAsync(con, pool, cancellationToken,
                        destination: "Renewal",
                        primaryCol: "rni.Name",
                        fuzzCols: fuzzTokenIsCode ? new[] { "rni.SerialNumber", "rni.ModelNumber" } : new[] { "rni.Name" },
                        descCols: new[] { "rni.Name LIKE {0}", "rni.SerialNumber LIKE {0}", "rni.ModelNumber LIKE {0}" },
                        from: "FROM dbo.Renewals rn INNER JOIN dbo.Item rni ON rn.ItemId = rni.ItemId",
                        activeGuard: "rn.IsArchived = 0",
                        detailExpr: "ISNULL(rn.RenewalStatus, '')",
                        archivedExpr: "0",
                        descParams: descParams, descriptiveTokens: descriptiveTokens,
                        fuzzTokenIsCode: fuzzTokenIsCode, lenLo: lenLo, lenHi: lenHi, fuzzPrefix: fuzzPrefix);

                    await CollectFuzzyCandidatesAsync(con, pool, cancellationToken,
                        destination: "Warranty",
                        primaryCol: "wi.Name",
                        fuzzCols: fuzzTokenIsCode ? new[] { "wi.SerialNumber", "wi.ModelNumber" } : new[] { "wi.Name" },
                        descCols: new[] { "wi.Name LIKE {0}", "wi.SerialNumber LIKE {0}", "wi.ModelNumber LIKE {0}" },
                        from: "FROM dbo.Item wi",
                        activeGuard: "wi.Active = 1 AND (wi.WarrantyYears > 0 OR wi.EndDate IS NOT NULL)",
                        detailExpr: "'Warranty'",
                        archivedExpr: "0",
                        descParams: descParams, descriptiveTokens: descriptiveTokens,
                        fuzzTokenIsCode: fuzzTokenIsCode, lenLo: lenLo, lenHi: lenHi, fuzzPrefix: fuzzPrefix);

                    // Score every pooled candidate against the fuzzed token; keep the closest match per
                    // row (a row may offer several fuzz-able columns, e.g. Serial AND Model) and discard
                    // anything past the strict edit-distance-2 threshold.
                    var scored = pool
                        .Select(c => new
                        {
                            c.Primary,
                            c.Detail,
                            c.Destination,
                            c.IsArchived,
                            Distance = c.FuzzValues.Where(v => !string.IsNullOrEmpty(v))
                                                    .Select(v => SearchTextHelper.MinWordDistance(fuzzToken, v))
                                                    .DefaultIfEmpty(int.MaxValue)
                                                    .Min()
                        })
                        .Where(c => c.Distance <= 2)
                        .OrderBy(c => c.Distance)
                        .ThenBy(c => c.Primary)
                        .Take(maxResults);

                    foreach (var c in scored)
                    {
                        results.Add(new ItemDto
                        {
                            ItemId = 0,
                            Name = c.Primary,
                            Category = c.Destination,
                            ItemType = c.Destination,
                            Active = true,
                            IsArchived = c.IsArchived,
                            DateCreated = DateTime.Now,
                            IsDirectMatch = true,
                            IsFuzzyMatch = true,
                            DirectMatchStatus = c.Detail,
                            DirectMatchSubtitle = $"Closest match for \"{fuzzToken}\" ({c.Distance} character{(c.Distance == 1 ? "" : "s")} different)",
                            ValidDestinations = new System.Collections.Generic.HashSet<string> { c.Destination },
                            DestinationCounts = new System.Collections.Generic.Dictionary<string, int> { [c.Destination] = 1 }
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Same rule as the rest of this repository: DB errors on this best-effort fallback
                // degrade to "no suggestions" rather than surfacing a second failure to the user.
                return new List<ItemDto>();
            }

            return results;
        }

        /// <summary>
        /// One master table's bounded candidate pool for <see cref="FuzzySearchFallbackAsync"/>: every
        /// descriptive (non-fuzzed) token must still match exactly somewhere in <paramref name="descCols"/>
        /// (kept tight — this is what makes the pool "smart" rather than a scan), and at least one of
        /// <paramref name="fuzzCols"/> must plausibly be able to match the fuzzed token — bounded by
        /// column LENGTH when the fuzz token is code-shaped (compared whole-column, e.g. SetCode), or
        /// by a LIKE '%prefix%' on the fuzz token's first few characters when it's descriptive
        /// (compared word-by-word against a potentially multi-word column like Item Name, where a
        /// length bound would wrongly reject a long Name over a short typo'd brand word). Capped at
        /// TOP 200 as a hard backstop either way. <paramref name="primaryCol"/> is always the row's
        /// natural display name/code, independent of which columns are actually being fuzz-scored.
        /// </summary>
        private static async Task CollectFuzzyCandidatesAsync(
            SqlConnection con,
            List<(string Primary, string Detail, string Destination, bool IsArchived, string[] FuzzValues)> pool,
            CancellationToken cancellationToken,
            string destination, string primaryCol, string[] fuzzCols, string[] descCols, string from, string activeGuard,
            string detailExpr, string archivedExpr,
            List<string> descParams, List<string> descriptiveTokens,
            bool fuzzTokenIsCode, int lenLo, int lenHi, string fuzzPrefix)
        {
            string poolFilter = fuzzTokenIsCode
                ? string.Join(" OR ", fuzzCols.Select(c => $"LEN(ISNULL({c}, '')) BETWEEN @LenLo AND @LenHi"))
                : string.Join(" OR ", fuzzCols.Select(c => $"{c} LIKE @FuzzPrefix"));
            string descClause = descParams.Count == 0 ? "1 = 1" : BuildTokenClause(descParams, descCols);
            string fuzzSelect = string.Join(", ", fuzzCols.Select(c => c));

            string sql = $@"
SELECT TOP (200)
    {primaryCol} AS Primary_,
    {detailExpr} AS Detail_,
    CAST(({archivedExpr}) AS BIT) AS IsArchived_,
    {fuzzSelect}
{from}
WHERE {activeGuard}
  AND ({descClause})
  AND ({poolFilter})";

            using (var cmd = new SqlCommand(sql, con))
            {
                if (fuzzTokenIsCode)
                {
                    cmd.Parameters.AddWithValue("@LenLo", lenLo);
                    cmd.Parameters.AddWithValue("@LenHi", lenHi);
                }
                else
                {
                    cmd.Parameters.AddWithValue("@FuzzPrefix", fuzzPrefix);
                }
                for (int i = 0; i < descParams.Count; i++)
                    cmd.Parameters.AddWithValue(descParams[i], "%" + descriptiveTokens[i] + "%");

                using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        string primary = reader.IsDBNull(0) ? null : reader.GetString(0);
                        if (string.IsNullOrWhiteSpace(primary)) continue;

                        string detail = reader.IsDBNull(1) ? null : reader.GetString(1);
                        bool isArchived = !reader.IsDBNull(2) && reader.GetBoolean(2);

                        var fuzzValues = new string[fuzzCols.Length];
                        for (int i = 0; i < fuzzCols.Length; i++)
                            fuzzValues[i] = reader.IsDBNull(3 + i) ? null : reader.GetString(3 + i);

                        pool.Add((primary, string.IsNullOrWhiteSpace(detail) ? null : detail, destination, isArchived, fuzzValues));
                    }
                }
            }
        }
    }

    /// <summary>
    /// One row in the home search bar's AutoCompleteBox dropdown — a primary name plus a short
    /// status/detail line, tagged with which master table it came from.
    /// </summary>
    public class SearchSuggestionDto
    {
        public string Primary     { get; set; }
        public string Detail      { get; set; }
        public string Destination { get; set; }

        /// <summary>Text to drop into the search box / run a full search with when this suggestion is picked.</summary>
        public string MatchText => Primary;
    }

    /// <summary>
    /// A single Request or Set match from <see cref="SearchRepository.SearchRequestsAndSetsAsync"/>.
    /// </summary>
    public class RequestSetSearchResultDto
    {
        /// <summary>"Request" or "Set".</summary>
        public string ResultType { get; set; }

        /// <summary>ReqId for a Request result, SetId for a Set result.</summary>
        public int Id { get; set; }

        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Status { get; set; }
        public DateTime SortDate { get; set; }

        public bool IsRequest => ResultType == "Request";
        public bool IsSet => ResultType == "Set";
    }
}
