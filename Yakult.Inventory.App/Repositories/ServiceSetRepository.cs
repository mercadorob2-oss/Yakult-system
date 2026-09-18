using System;
using System.Collections.Generic;
using System.Data;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Repositories
{
    public class ServiceSetRepository
    {
        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public ServiceSetRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
            // This allows pages to instantiate repositories without crashing.
        }

        /// <summary>
        /// Gets the connection string, throwing if not configured.
        /// Call this at the start of each method that needs database access.
        /// </summary>
        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        private sealed class ItemTypeLookup
        {
            public int ItemId { get; set; }
            public string ItemType { get; set; }
        }

        private static bool IsSoftwareOrServiceItemType(string itemType)
        {
            if (string.IsNullOrWhiteSpace(itemType)) return false;

            return itemType.Equals(ItemTypes.SoftwareLicense, StringComparison.OrdinalIgnoreCase)
                || itemType.Equals("Service", StringComparison.OrdinalIgnoreCase)
                || itemType.Equals(ItemTypes.Services, StringComparison.OrdinalIgnoreCase);
        }

        private static int ComputeRenewalYears(DateTime startDate, DateTime endDate)
        {
            if (endDate <= startDate) return 1;

            int years = endDate.Year - startDate.Year;
            if (startDate.AddYears(years) < endDate)
            {
                years++;
            }

            return years < 1 ? 1 : years;
        }

        public int CreateSoftwareServiceSet(Yakult.Inventory.App.Models.ServiceSetDto set)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                System.Diagnostics.Debug.WriteLine($"[ServiceSetRepository] CreateSoftwareServiceSet connected to {connection.DataSource} / DB={connection.Database}");

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int setId = InsertSoftwareServiceSet(set, connection, transaction);
                        transaction.Commit();
                        return setId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Overload that creates the invoice Set + SetItems + seeded Renewals on a caller-supplied
        /// connection/transaction, so the whole invoice (its Items included) can be committed or
        /// rolled back atomically. The CALLER owns commit/rollback. Returns the new SetId.
        /// </summary>
        public int CreateSoftwareServiceSet(Yakult.Inventory.App.Models.ServiceSetDto set, SqlConnection connection, SqlTransaction transaction)
            => InsertSoftwareServiceSet(set, connection, transaction);

        /// <summary>Delegates to the shared SetItemSubTypeGroupHelper (also used by
        /// InvoiceRepository's Bulk Add) so every SetItem-inserting code path resolves
        /// dbo.SetItemSubTypeGroup rows identically.</summary>
        private static int? FindOrCreateSubTypeGroup(
            SqlConnection connection, SqlTransaction transaction,
            int setId, string subType, string referenceCode, DateTime? beginDate, DateTime? endDate,
            int? createdBy,
            decimal? vatPercent = null, decimal? whtPercent = null,
            decimal? discountPercent = null, decimal? subtotalOverride = null)
            => SetItemSubTypeGroupHelper.FindOrCreateGroupId(connection, transaction, setId, subType, referenceCode, beginDate, endDate, createdBy,
                vatPercent, whtPercent, discountPercent, subtotalOverride);

        /// <summary>Delegates to the shared SetItemParentTagGroupHelper, mirroring
        /// FindOrCreateSubTypeGroup above — Parent Tag is an independent, orthogonal grouping
        /// with no financial semantics of its own.</summary>
        private static int? FindOrCreateParentTagGroup(
            SqlConnection connection, SqlTransaction transaction,
            int setId, string parentTagLabel, int? createdBy)
            => SetItemParentTagGroupHelper.FindOrCreateGroupId(connection, transaction, setId, parentTagLabel, createdBy);

        /// <summary>
        /// Core inserts for a software/service (invoice) set, run entirely on the supplied
        /// connection/transaction. Returns the new SetId. Does not commit or roll back.
        /// </summary>
        private int InsertSoftwareServiceSet(Yakult.Inventory.App.Models.ServiceSetDto set, SqlConnection connection, SqlTransaction transaction)
        {
                        // Determine SetType from items:
                        // Prefer a Software/Service ItemType (required for renewals + vw_RenewalStatus),
                        // otherwise fall back to the first item's ItemType.
                        if (set.Items == null || set.Items.Count == 0)
                        {
                            throw new InvalidOperationException("Software/Service set must contain at least one item to determine SetType.");
                        }

                        var distinctItemIdsForType = set.Items
                            .Select(i => i.ItemId)
                            .Where(id => id > 0)
                            .Distinct()
                            .ToArray();

                        const string itemTypesSqlForSet = @"SELECT ItemId, ItemType FROM dbo.Item WHERE ItemId IN @ItemIds";
                        var itemTypesForSet = connection
                            .Query<ItemTypeLookup>(itemTypesSqlForSet, new { ItemIds = distinctItemIdsForType }, transaction)
                            .ToDictionary(x => x.ItemId, x => x.ItemType);

                        string setType = null;
                        foreach (var line in set.Items)
                        {
                            if (itemTypesForSet.TryGetValue(line.ItemId, out var t) && IsSoftwareOrServiceItemType(t))
                            {
                                setType = t;
                                break;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(setType))
                        {
                            var firstItemId = set.Items[0].ItemId;
                            itemTypesForSet.TryGetValue(firstItemId, out setType);

                            if (string.IsNullOrWhiteSpace(setType))
                            {
                                throw new InvalidOperationException("ItemType is not set for the first item in the Software/Service set.");
                            }
                        }

                        // Insert into [Set] table (SetCode is computed by the database)
                        // CRITICAL: IsInvoice=1 marks this as an invoice (authoritative flag)
                        string setQuery = @"
                            INSERT INTO [dbo].[Set] (
                                [DocumentNumber], [ReferenceNumber], [DispatchDate],
                                [StartDate], [EndDate],
                                [SetType], [Status], [CreatedBy],
                                [CreatedAt], [Remarks], [ComId], [CurrentBranchId], [DistributorId], [IsInvoice]
                            ) VALUES (
                                @DocumentNumber, @ReferenceNumber, @DocumentDate,
                                @StartDate, @EndDate,
                                @SetType, @Status, @CreatedByUserId,
                                @DateCreated, @Notes, @ComId, @BranchId, @DistributorId, 1
                            );
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        // Execute the query with parameters
                        var parameters = new
                        {
                            DocumentNumber = set.DocumentNumber,
                            ReferenceNumber = set.ReferenceNumber,
                            DocumentDate = set.DocumentDate,
                            StartDate = set.StartDate,
                            EndDate = set.EndDate,
                            SetType = setType,
                            Status = string.IsNullOrWhiteSpace(set.Status) ? "Draft" : set.Status.Trim(),
                            ComId = set.ComId,
                            BranchId = set.BranchId,
                            DistributorId = set.DistributorId,
                            CreatedByUserId = set.CreatedByUserId,
                            DateCreated = set.DateCreated,
                            Notes = set.Notes ?? string.Empty
                        };

                        int setId = connection.QuerySingle<int>(setQuery, parameters, transaction);

                        // Insert into [SetItem] table
                        if (setId > 0 && set.Items?.Count > 0)
                        {
                            string itemQuery = @"
                                INSERT INTO [dbo].[SetItem] (
                                    [SetId], [ItemId], [ItemCode],
                                    [Description], [Quantity], [UnitOfMeasure],
                                    [UnitPrice], [Amount], [LineStartDate],
                                    [LineEndDate], [SubType], [ReferenceCode], [BeginDate], [EndDate], [GroupId], [ParentTagGroupId], [CreatedBy], [CreatedAt]
                                ) VALUES (
                                    @SetId, @ItemId, @ItemCode,
                                    @Description, @Quantity, @UnitOfMeasure,
                                    @UnitPrice, @Amount, @LineStartDate,
                                    @LineEndDate, @SubType, @ReferenceCode, @BeginDate, @EndDate, @GroupId, @ParentTagGroupId, @CreatedBy, GETDATE()
                                )";

                            bool shouldCreateRenewals = set.StartDate.HasValue
                                && set.EndDate.HasValue;

                            const string insertRenewalSql = @"
                                INSERT INTO dbo.Renewals
                                (
                                    ItemId,
                                    RenewalStatus,
                                    RenewalCount,
                                    NewStartDate,
                                    NewEndDate,
                                    RenewalYears,
                                    RenewalAmount,
                                    IsArchived,
                                    CreatedBy,
                                    CreatedAt
                                )
                                SELECT
                                    @ItemId,
                                    'None',
                                    ISNULL((SELECT MAX(RenewalCount) FROM dbo.Renewals WHERE ItemId = @ItemId), 0) + 1,
                                    @NewStartDate,
                                    @NewEndDate,
                                    @RenewalYears,
                                    @RenewalAmount,
                                    0,
                                    @CreatedBy,
                                    GETDATE()
                                WHERE NOT EXISTS (SELECT 1 FROM dbo.Renewals WHERE ItemId = @ItemId AND IsArchived = 0);";

                            foreach (var item in set.Items)
                            {
                                // The group's period comes from BeginDate/EndDate when the caller
                                // supplies one (the CSV import does, via its GroupBeginDate/
                                // GroupEndDate columns); otherwise it inherits the line's own dates,
                                // which is what every other caller has always relied on.
                                int? groupId = FindOrCreateSubTypeGroup(
                                    connection, transaction, setId, item.SubType, item.ReferenceCode,
                                    item.BeginDate ?? item.LineStartDate,
                                    item.EndDate ?? item.LineEndDate, set.CreatedByUserId,
                                    item.GroupVatPercent, item.GroupWhtPercent,
                                    item.GroupDiscountPercent, item.GroupSubtotalOverride);

                                int? parentTagGroupId = FindOrCreateParentTagGroup(
                                    connection, transaction, setId, item.ParentTagLabel, set.CreatedByUserId);

                                var itemParams = new
                                {
                                    SetId = setId,
                                    ItemId = item.ItemId,
                                    ItemCode = item.ItemCode,
                                    Description = item.Description,
                                    Quantity = item.Quantity,
                                    UnitOfMeasure = item.UnitOfMeasure,
                                    UnitPrice = item.UnitPrice,
                                    Amount = item.Amount,
                                    LineStartDate = item.LineStartDate,
                                    LineEndDate = item.LineEndDate,
                                    SubType = item.SubType,
                                    ReferenceCode = item.ReferenceCode,
                                    BeginDate = item.BeginDate ?? item.LineStartDate,
                                    EndDate = item.EndDate ?? item.LineEndDate,
                                    GroupId = groupId,
                                    ParentTagGroupId = parentTagGroupId,
                                    CreatedBy = set.CreatedByUserId
                                };
                                connection.Execute(itemQuery, itemParams, transaction);

                                System.Diagnostics.Debug.WriteLine($"[Renewals] ItemId={item.ItemId} shouldCreateRenewals={shouldCreateRenewals} StartDate={set.StartDate} EndDate={set.EndDate} CreatedBy={set.CreatedByUserId}");
                                if (shouldCreateRenewals)
                                {
                                    DateTime newStartDate = (item.LineStartDate ?? set.StartDate.Value);
                                    DateTime newEndDate = (item.LineEndDate ?? set.EndDate.Value);

                                    int renewalRows = connection.Execute(
                                        insertRenewalSql,
                                        new
                                        {
                                            ItemId = item.ItemId,
                                            NewStartDate = newStartDate,
                                            NewEndDate = newEndDate,
                                            RenewalYears = ComputeRenewalYears(newStartDate, newEndDate),
                                            RenewalAmount = item.Amount,
                                            CreatedBy = set.CreatedByUserId
                                        },
                                        transaction);
                                    System.Diagnostics.Debug.WriteLine($"[Renewals] INSERT rows affected={renewalRows} for ItemId={item.ItemId} StartDate={newStartDate} EndDate={newEndDate}");
                                }
                            }
                        }

                        return setId;
        }

        /// <summary>
        /// Loads a software/service set (header + items) by SetId.
        /// Used for editing an existing invoice.
        /// </summary>
        public Yakult.Inventory.App.Models.ServiceSetDto GetSoftwareServiceSetById(int setId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();

                const string setSql = @"
                    SELECT
                        s.SetId AS Id,
                        s.DocumentNumber,
                        s.ReferenceNumber,
                        s.DispatchDate AS DocumentDate,
                        s.StartDate,
                        s.EndDate,
                        s.Status,
                        s.ComId,
                        c.Name AS CompanyName,
                        s.CurrentBranchId AS BranchId,
                        br.Name AS BranchName,
                        s.DistributorId,
                        dist.Name AS DistributorName,
                        s.CreatedBy AS CreatedByUserId,
                        s.CreatedAt AS DateCreated
                    FROM dbo.[Set] s
                    LEFT JOIN dbo.Company c ON s.ComId = c.ComId
                    LEFT JOIN dbo.Branch br ON s.CurrentBranchId = br.BranchId
                    LEFT JOIN dbo.Distributor dist ON s.DistributorId = dist.DistributorId
                    WHERE s.SetId = @SetId";

                var set = connection.QuerySingleOrDefault<Yakult.Inventory.App.Models.ServiceSetDto>(
                    setSql,
                    new { SetId = setId });

                if (set == null)
                {
                    return null;
                }

                const string itemsSql = @"
                    SELECT
                        si.SetId,
                        si.ItemId,
                        si.ItemCode,
                        si.Description,
                        ISNULL(i.StockOnHand, si.Quantity) AS Quantity,
                        si.UnitOfMeasure,
                        si.UnitPrice,
                        si.Amount,
                        si.LineStartDate,
                        si.LineEndDate,
                        si.SubType,
                        si.ReferenceCode,
                        si.BeginDate,
                        si.EndDate,
                        (SELECT TOP 1 rn.PartNumber FROM dbo.Renewals rn WHERE rn.ItemId = si.ItemId AND rn.IsArchived = 0) AS PartNumber
                    FROM dbo.SetItem si
                    LEFT JOIN dbo.Item i ON si.ItemId = i.ItemId
                    WHERE si.SetId = @SetId
                    ORDER BY si.ItemId";

                set.Items = connection.Query<Yakult.Inventory.App.Models.ServiceSetItemDto>(
                    itemsSql,
                    new { SetId = setId }).ToList();

                return set;
            }
        }

        /// <summary>
        /// Updates an existing software/service set (header + items).
        /// This replaces all existing SetItem rows for the set.
        /// </summary>
        public void UpdateSoftwareServiceSet(Yakult.Inventory.App.Models.ServiceSetDto set)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            if (set.Id <= 0) throw new ArgumentException("Set.Id must be a valid SetId for update.");

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                System.Diagnostics.Debug.WriteLine($"[ServiceSetRepository] UpdateSoftwareServiceSet connected to {connection.DataSource} / DB={connection.Database}");

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Determine SetType from items:
                        // Prefer a Software/Service item type; otherwise keep existing Set.SetType.
                        string existingSetType = connection.QuerySingleOrDefault<string>(
                            "SELECT SetType FROM dbo.[Set] WHERE SetId = @SetId",
                            new { SetId = set.Id },
                            transaction);

                        string setType = existingSetType;

                        Dictionary<int, string> itemTypesForSet = null;
                        if (set.Items != null && set.Items.Count > 0)
                        {
                            var distinctItemIdsForType = set.Items
                                .Select(i => i.ItemId)
                                .Where(id => id > 0)
                                .Distinct()
                                .ToArray();

                            const string itemTypesSqlForSet = @"SELECT ItemId, ItemType FROM dbo.Item WHERE ItemId IN @ItemIds";
                            itemTypesForSet = connection
                                .Query<ItemTypeLookup>(itemTypesSqlForSet, new { ItemIds = distinctItemIdsForType }, transaction)
                                .ToDictionary(x => x.ItemId, x => x.ItemType);

                            foreach (var line in set.Items)
                            {
                                if (itemTypesForSet.TryGetValue(line.ItemId, out var t) && IsSoftwareOrServiceItemType(t))
                                {
                                    setType = t;
                                    break;
                                }
                            }

                            if (string.IsNullOrWhiteSpace(setType))
                            {
                                var firstItemId = set.Items[0].ItemId;
                                if (itemTypesForSet.TryGetValue(firstItemId, out var fallbackType) && !string.IsNullOrWhiteSpace(fallbackType))
                                {
                                    setType = fallbackType;
                                }
                            }
                        }

                        // CRITICAL: IsInvoice=1 marks this as an invoice (authoritative flag)
                        const string updateSetSql = @"
                            UPDATE dbo.[Set]
                            SET
                                DocumentNumber = @DocumentNumber,
                                ReferenceNumber = @ReferenceNumber,
                                DispatchDate = @DocumentDate,
                                StartDate = @StartDate,
                                EndDate = @EndDate,
                                SetType = @SetType,
                                Status = @Status,
                                ComId = @ComId,
                                CurrentBranchId = @BranchId,
                                DistributorId = @DistributorId,
                                IsInvoice = 1
                            WHERE SetId = @Id";

                        var headerParams = new
                        {
                            set.Id,
                            set.DocumentNumber,
                            set.ReferenceNumber,
                            set.DocumentDate,
                            set.StartDate,
                            set.EndDate,
                            SetType = setType,
                            Status = string.IsNullOrWhiteSpace(set.Status) ? "Draft" : set.Status.Trim(),
                            set.ComId,
                            set.BranchId,
                            set.DistributorId
                        };

                        connection.Execute(updateSetSql, headerParams, transaction);

                        // Replace all existing SetItem rows for this set
                        const string deleteItemsSql = "DELETE FROM dbo.SetItem WHERE SetId = @SetId";
                        connection.Execute(deleteItemsSql, new { SetId = set.Id }, transaction);

                        if (set.Items != null && set.Items.Count > 0)
                        {
                            const string insertItemSql = @"
                                INSERT INTO [dbo].[SetItem] (
                                    [SetId], [ItemId], [ItemCode],
                                    [Description], [Quantity], [UnitOfMeasure],
                                    [UnitPrice], [Amount], [LineStartDate],
                                    [LineEndDate], [SubType], [ReferenceCode], [BeginDate], [EndDate], [GroupId], [ParentTagGroupId], [CreatedBy], [CreatedAt]
                                ) VALUES (
                                    @SetId, @ItemId, @ItemCode,
                                    @Description, @Quantity, @UnitOfMeasure,
                                    @UnitPrice, @Amount, @LineStartDate,
                                    @LineEndDate, @SubType, @ReferenceCode, @BeginDate, @EndDate, @GroupId, @ParentTagGroupId, @CreatedBy, GETDATE()
                                )";

                            bool shouldCreateRenewals = set.StartDate.HasValue
                                && set.EndDate.HasValue;

                            const string insertRenewalSql = @"
                                INSERT INTO dbo.Renewals
                                (
                                    ItemId,
                                    RenewalStatus,
                                    RenewalCount,
                                    NewStartDate,
                                    NewEndDate,
                                    RenewalYears,
                                    RenewalAmount,
                                    IsArchived,
                                    CreatedBy,
                                    CreatedAt
                                )
                                SELECT
                                    @ItemId,
                                    'None',
                                    ISNULL((SELECT MAX(RenewalCount) FROM dbo.Renewals WHERE ItemId = @ItemId), 0) + 1,
                                    @NewStartDate,
                                    @NewEndDate,
                                    @RenewalYears,
                                    @RenewalAmount,
                                    0,
                                    @CreatedBy,
                                    GETDATE()
                                WHERE NOT EXISTS (SELECT 1 FROM dbo.Renewals WHERE ItemId = @ItemId AND IsArchived = 0);";

                            foreach (var item in set.Items)
                            {
                                // See CreateSoftwareServiceSet: BeginDate/EndDate win when supplied,
                                // otherwise the group inherits the line's own dates.
                                int? groupId = FindOrCreateSubTypeGroup(
                                    connection, transaction, set.Id, item.SubType, item.ReferenceCode,
                                    item.BeginDate ?? item.LineStartDate,
                                    item.EndDate ?? item.LineEndDate, set.CreatedByUserId,
                                    item.GroupVatPercent, item.GroupWhtPercent,
                                    item.GroupDiscountPercent, item.GroupSubtotalOverride);

                                int? parentTagGroupId = FindOrCreateParentTagGroup(
                                    connection, transaction, set.Id, item.ParentTagLabel, set.CreatedByUserId);

                                var itemParams = new
                                {
                                    SetId = set.Id,
                                    ItemId = item.ItemId,
                                    ItemCode = item.ItemCode,
                                    Description = item.Description,
                                    Quantity = item.Quantity,
                                    UnitOfMeasure = item.UnitOfMeasure,
                                    UnitPrice = item.UnitPrice,
                                    Amount = item.Amount,
                                    LineStartDate = item.LineStartDate,
                                    LineEndDate = item.LineEndDate,
                                    SubType = item.SubType,
                                    ReferenceCode = item.ReferenceCode,
                                    BeginDate = item.BeginDate ?? item.LineStartDate,
                                    EndDate = item.EndDate ?? item.LineEndDate,
                                    GroupId = groupId,
                                    ParentTagGroupId = parentTagGroupId,
                                    CreatedBy = set.CreatedByUserId
                                };

                                connection.Execute(insertItemSql, itemParams, transaction);

                                System.Diagnostics.Debug.WriteLine($"[Renewals/Update] ItemId={item.ItemId} shouldCreateRenewals={shouldCreateRenewals} StartDate={set.StartDate} EndDate={set.EndDate} CreatedBy={set.CreatedByUserId}");
                                if (shouldCreateRenewals)
                                {
                                    DateTime newStartDate = (item.LineStartDate ?? set.StartDate.Value);
                                    DateTime newEndDate = (item.LineEndDate ?? set.EndDate.Value);

                                    int renewalRows = connection.Execute(
                                        insertRenewalSql,
                                        new
                                        {
                                            ItemId = item.ItemId,
                                            NewStartDate = newStartDate,
                                            NewEndDate = newEndDate,
                                            RenewalYears = ComputeRenewalYears(newStartDate, newEndDate),
                                            RenewalAmount = item.Amount,
                                            CreatedBy = set.CreatedByUserId
                                        },
                                        transaction);
                                    System.Diagnostics.Debug.WriteLine($"[Renewals/Update] INSERT rows affected={renewalRows} for ItemId={item.ItemId}");
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Flags an existing Request-based dispatch Set (Item -> dbo.Request -> Set) as also
        /// being an invoice, in place — no dbo.SetItem rows are created. The invoice reuses
        /// the Set's existing dbo.Request rows for its line items (vw_Invoices/vw_InvoiceItems
        /// "PART A" already reads Request-based invoice Sets this way; InvoiceRepository's
        /// GetRequestItemsAsInvoiceItemsAsync is the matching read-side fallback for
        /// ViewInvoiceDetailPage). Guarded to only ever touch a Set that actually has a ReqId
        /// — a pure SetItem-based invoice Set must keep going through
        /// InsertSoftwareServiceSet/UpdateSoftwareServiceSet instead.
        /// </summary>
        public void MarkRequestSetAsInvoiceAsync(
            int setId, string documentNumber, string referenceNumber,
            decimal subtotal, decimal vatAmount, decimal whtAmount, decimal discountAmount, decimal totalAmountDue,
            int? comId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // An invoice is addressed to a SITE (company / department / branch), not to
                        // a person — a Request-based Set only knows its requester, so seed the Set's
                        // site columns from that requester (its linked Request, else that Request's
                        // Employee) whenever the Set doesn't already carry them. ViewInvoiceDetailPage's
                        // site builder then opens pre-filled instead of blank. An explicit @ComId
                        // from the caller still wins.
                        const string updateSql = @"
                            UPDATE s
                            SET
                                IsInvoice           = 1,
                                DocumentNumber      = @DocumentNumber,
                                ReferenceNumber     = @ReferenceNumber,
                                Subtotal            = @Subtotal,
                                VatAmount           = @VatAmount,
                                WhtAmount           = @WhtAmount,
                                DiscountAmount      = @DiscountAmount,
                                TotalAmountDue      = @TotalAmountDue,
                                ComId                 = COALESCE(@ComId, s.ComId, r.ComId, e.ComId),
                                CurrentDepartmentId   = COALESCE(s.CurrentDepartmentId, r.DeptId, e.DeptId),
                                CurrentBranchId       = COALESCE(s.CurrentBranchId, r.BranchId, e.BranchId),
                                InvoiceRequesterEmpId = COALESCE(s.InvoiceRequesterEmpId, r.EmpId)
                            FROM dbo.[Set] s
                            LEFT JOIN dbo.Request  r ON r.ReqId = s.ReqId
                            LEFT JOIN dbo.Employee e ON e.EmpId = r.EmpId
                            WHERE s.SetId = @SetId AND s.ReqId IS NOT NULL";

                        int rows = connection.Execute(updateSql, new
                        {
                            SetId = setId,
                            DocumentNumber = documentNumber,
                            ReferenceNumber = referenceNumber,
                            Subtotal = subtotal,
                            VatAmount = vatAmount,
                            WhtAmount = whtAmount,
                            DiscountAmount = discountAmount,
                            TotalAmountDue = totalAmountDue,
                            ComId = comId
                        }, transaction);

                        if (rows == 0)
                            throw new InvalidOperationException($"Set {setId} is not a Request-based dispatch Set (no ReqId) — cannot record it as an invoice this way.");

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Returns all SetItem rows (service/software items) for a given SetId.
        /// Used by the ViewInvoiceDetailPage to display invoice line items.
        /// </summary>
        public IEnumerable<Yakult.Inventory.App.Models.ServiceSetItemDto> GetServiceSetItems(int setId)
        {
            const string sql = @"
                SELECT
                    SetId,
                    ItemId,
                    ItemCode,
                    Description,
                    Quantity,
                    UnitOfMeasure,
                    UnitPrice,
                    Amount,
                    LineStartDate,
                    LineEndDate
                FROM dbo.SetItem
                WHERE SetId = @SetId
                ORDER BY SetId, ItemId";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                return connection.Query<Yakult.Inventory.App.Models.ServiceSetItemDto>(sql, new { SetId = setId }).ToList();
            }
        }

        /// <summary>
        /// Gets all companies for dropdown selection
        /// </summary>
        public List<CompanyDto> GetAllCompanies()
        {
            const string sql = @"
                SELECT ComId, Name AS CompanyName
                FROM dbo.Company
                WHERE Active = 1
                ORDER BY Name";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                return connection.Query<CompanyDto>(sql).ToList();
            }
        }

        /// <summary>
        /// Gets all branches for dropdown selection
        /// </summary>
        public List<Pages.BranchDto> GetAllBranches()
        {
            const string sql = @"
                SELECT BranchId, Name
                FROM dbo.Branch
                WHERE Active = 1
                ORDER BY Name";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                return connection.Query<Pages.BranchDto>(sql).ToList();
            }
        }

        /// <summary>
        /// Gets all distributors for dropdown selection
        /// </summary>
        public List<DistributorDto> GetAllDistributors()
        {
            const string sql = @"
                SELECT DistributorId, Name
                FROM dbo.Distributor
                WHERE IsActive = 1
                ORDER BY SortOrder, Name";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                return connection.Query<DistributorDto>(sql).ToList();
            }
        }

        /// <summary>
        /// Deletes a software/service set (invoice) and all of its SetItem rows.
        /// </summary>
        public void DeleteServiceSet(int setId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Delete line items first
                        const string deleteItemsSql = "DELETE FROM dbo.SetItem WHERE SetId = @SetId";
                        connection.Execute(deleteItemsSql, new { SetId = setId }, transaction);

                        // Delete the set header
                        const string deleteSetSql = "DELETE FROM dbo.[Set] WHERE SetId = @SetId";
                        connection.Execute(deleteSetSql, new { SetId = setId }, transaction);

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}