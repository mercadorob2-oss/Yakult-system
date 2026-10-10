using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using Newtonsoft.Json;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    public sealed class LicenseHistoricalPeriodRepository
    {
        private const decimal MaxMoney = 9999999999999999.99m;

        public LicenseHistoricalPeriodContext GetContext(int setId)
        {
            DatabaseConfig.EnsureConfigured();
            using (var connection = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                connection.Open();
                EnsureStorageAvailable(connection);
                var context = ResolveChain(connection, null, setId);
                context.CurrentItems = connection.Query<LicenseHistoricalPeriodItemDto>(@"
                    SELECT si.ItemId, COALESCE(NULLIF(si.Description, ''), i.Name) AS ItemName,
                           si.ItemCode, si.Quantity
                    FROM dbo.SetItem si
                    LEFT JOIN dbo.Item i ON i.ItemId = si.ItemId
                    WHERE si.SetId = @SetId
                    ORDER BY si.SetItemId", new { SetId = setId }).AsList();
                context.Periods = ReadPeriods(connection, null, context.SetIds);
                return context;
            }
        }

        public int Create(int setId, LicenseHistoricalPeriodDto period, int userId, bool allowOverlap = false)
        {
            Validate(period);
            if (userId <= 0) throw new InvalidOperationException("Please sign in before recording previous renewals.");
            DatabaseConfig.EnsureConfigured();
            using (var connection = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                connection.Open();
                EnsureStorageAvailable(connection);
                // Serialize the chain reads and duplicate checks with the insert. This also
                // prevents a concurrent invoice link from changing the chain during validation.
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    var context = ResolveChain(connection, transaction, setId);
                    if (!context.FirstRecordedStartDate.HasValue)
                        throw new InvalidOperationException("Set a coverage start date on the recorded invoice before adding previous renewals.");
                    if (period.EndDate.Date >= context.FirstRecordedStartDate.Value.Date)
                        throw new InvalidOperationException($"The historical period must end before {context.FirstRecordedStartDate:MMM d, yyyy}, the first recorded coverage date.");

                    var existing = ReadPeriods(connection, transaction, context.SetIds);
                    if (existing.Any(p => p.StartDate.Date == period.StartDate.Date && p.EndDate.Date == period.EndDate.Date))
                        throw new InvalidOperationException("This coverage period has already been recorded for this renewal chain.");
                    if (!allowOverlap && existing.Any(p => p.StartDate.Date <= period.EndDate.Date && p.EndDate.Date >= period.StartDate.Date))
                        throw new HistoricalPeriodOverlapException();

                    int id = connection.ExecuteScalar<int>(@"
                        INSERT INTO dbo.LicenseHistoricalPeriod
                            (AnchorSetId, StartDate, EndDate, ReferenceNumber, Amount, Notes, CreatedBy)
                        VALUES (@AnchorSetId, @StartDate, @EndDate, @ReferenceNumber, @Amount, @Notes, @CreatedBy);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);", new
                        {
                            AnchorSetId = context.RootSetId,
                            StartDate = period.StartDate.Date,
                            EndDate = period.EndDate.Date,
                            ReferenceNumber = TrimOrNull(period.ReferenceNumber),
                            period.Amount,
                            Notes = TrimOrNull(period.Notes),
                            CreatedBy = userId
                        }, transaction);

                    for (int index = 0; index < period.Items.Count; index++)
                    {
                        var item = period.Items[index];
                        connection.Execute(@"
                            INSERT INTO dbo.LicenseHistoricalPeriodItem
                                (HistoricalPeriodId, ItemId, ItemName, ItemCode, Quantity, DisplayOrder)
                            VALUES (@HistoricalPeriodId, @ItemId, @ItemName, @ItemCode, @Quantity, @DisplayOrder);", new
                            {
                                HistoricalPeriodId = id,
                                item.ItemId,
                                ItemName = item.ItemName.Trim(),
                                ItemCode = TrimOrNull(item.ItemCode),
                                item.Quantity,
                                DisplayOrder = index
                            }, transaction);
                    }

                    connection.Execute(@"
                        INSERT INTO dbo.AuditTrail
                            (Action, EntityId, EntityType, UserId, UserName, Timestamp, Notes, NewValues)
                        SELECT 'Create', @Id, 'LicenseHistoricalPeriod', UserId, Name, SYSUTCDATETIME(),
                               @Notes, @NewValues
                        FROM dbo.[User] WHERE UserId = @UserId;", new
                        {
                            Id = id,
                            UserId = userId,
                            Notes = $"Previous renewal coverage recorded for Set #{context.RootSetId}.",
                            NewValues = JsonConvert.SerializeObject(new
                            {
                                AnchorSetId = context.RootSetId,
                                period.StartDate,
                                period.EndDate,
                                period.ReferenceNumber,
                                period.Amount,
                                period.Notes,
                                period.Items
                            })
                        }, transaction);

                    transaction.Commit();
                    return id;
                }
            }
        }

        public static void Validate(LicenseHistoricalPeriodDto period)
        {
            if (period == null) throw new ArgumentNullException(nameof(period));
            if (period.StartDate.Date > period.EndDate.Date)
                throw new InvalidOperationException("Coverage end date must be on or after the start date.");
            if (period.Amount.HasValue && !IsValidDecimal(period.Amount.Value, false))
                throw new InvalidOperationException("Enter a nonnegative amount with up to two decimal places.");
            if ((period.ReferenceNumber?.Trim().Length ?? 0) > 100 || (period.Notes?.Trim().Length ?? 0) > 1000)
                throw new InvalidOperationException("Reference is limited to 100 characters and notes to 1,000 characters.");
            if (period.Items == null || period.Items.Count == 0)
                throw new InvalidOperationException("Add at least one item covered by this period.");
            foreach (var item in period.Items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemName) || item.ItemName.Trim().Length > 4000)
                    throw new InvalidOperationException("Every historical item needs a name or description of up to 4,000 characters.");
                if ((item.ItemCode?.Trim().Length ?? 0) > 800)
                    throw new InvalidOperationException("Item codes are limited to 800 characters.");
                if (!IsValidDecimal(item.Quantity, true))
                    throw new InvalidOperationException("Every quantity must be positive with up to two decimal places.");
                if (item.ItemId.HasValue && item.ItemId.Value <= 0)
                    throw new InvalidOperationException("Select a valid catalog item or leave the catalog link empty.");
            }
        }

        private static bool IsValidDecimal(decimal value, bool positive)
            => (positive ? value > 0 : value >= 0) && value <= MaxMoney && decimal.Round(value, 2) == value;

        private static string TrimOrNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static void EnsureStorageAvailable(SqlConnection connection)
        {
            if (!connection.ExecuteScalar<bool>(@"
                SELECT CAST(CASE WHEN OBJECT_ID('dbo.LicenseHistoricalPeriod', 'U') IS NOT NULL
                    AND OBJECT_ID('dbo.LicenseHistoricalPeriodItem', 'U') IS NOT NULL THEN 1 ELSE 0 END AS BIT)"))
                throw new InvalidOperationException("Previous renewal history is not available yet. Please contact your administrator.");
        }

        private sealed class ChainRow
        {
            public int SetId { get; set; }
            public int? RenewalOfSetId { get; set; }
            public bool HasCycle { get; set; }
        }

        private static LicenseHistoricalPeriodContext ResolveChain(SqlConnection connection, SqlTransaction transaction, int setId)
        {
            if (setId <= 0) throw new InvalidOperationException("Open an invoice-backed renewal to record previous periods.");
            string lockHint = transaction == null ? "" : " WITH (UPDLOCK, HOLDLOCK)";
            var visited = new HashSet<int>();
            int root = setId;
            while (true)
            {
                if (!visited.Add(root)) throw new InvalidOperationException("The renewal chain contains a circular link. Please contact your administrator.");
                var parent = connection.QuerySingleOrDefault<ChainRow>(
                    "SELECT SetId, RenewalOfSetId FROM dbo.[Set]" + lockHint + " WHERE SetId = @Id",
                    new { Id = root }, transaction);
                if (parent == null) throw new InvalidOperationException("The invoice is no longer available. Refresh the renewal list.");
                if (!parent.RenewalOfSetId.HasValue) break;
                root = parent.RenewalOfSetId.Value;
            }

            var chain = connection.Query<ChainRow>(@"
                ;WITH Chain AS (
                    SELECT s.SetId, CAST('/' + CAST(s.SetId AS VARCHAR(11)) + '/' AS VARCHAR(MAX)) AS Visited,
                           CAST(0 AS BIT) AS HasCycle
                    FROM dbo.[Set] s" + lockHint + @" WHERE s.SetId = @Root
                    UNION ALL
                    SELECT s.SetId, CAST(c.Visited + CAST(s.SetId AS VARCHAR(11)) + '/' AS VARCHAR(MAX)),
                           CAST(CASE WHEN CHARINDEX('/' + CAST(s.SetId AS VARCHAR(11)) + '/', c.Visited) > 0
                                THEN 1 ELSE 0 END AS BIT)
                    FROM dbo.[Set] s" + lockHint + @"
                    INNER JOIN Chain c ON s.RenewalOfSetId = c.SetId
                    WHERE c.HasCycle = 0
                )
                SELECT SetId, HasCycle FROM Chain OPTION (MAXRECURSION 32767);",
                new { Root = root }, transaction).AsList();
            if (chain.Any(c => c.HasCycle)) throw new InvalidOperationException("The renewal chain contains a circular link. Please contact your administrator.");
            var ids = chain.Select(c => c.SetId).Distinct().ToList();
            DateTime? firstStart = connection.ExecuteScalar<DateTime?>(@"
                SELECT MIN(CoverageStart) FROM (
                    SELECT StartDate AS CoverageStart FROM dbo.[Set] WHERE SetId IN @Ids
                    UNION ALL
                    SELECT LineStartDate FROM dbo.SetItem WHERE SetId IN @Ids
                ) dates;", new { Ids = ids }, transaction);
            return new LicenseHistoricalPeriodContext { RootSetId = root, SetIds = ids, FirstRecordedStartDate = firstStart };
        }

        private static List<LicenseHistoricalPeriodDto> ReadPeriods(SqlConnection connection, SqlTransaction transaction, List<int> setIds)
        {
            using (var results = connection.QueryMultiple(@"
                SELECT p.*, u.Name AS CreatedByName
                FROM dbo.LicenseHistoricalPeriod p
                LEFT JOIN dbo.[User] u ON u.UserId = p.CreatedBy
                WHERE p.AnchorSetId IN @Ids
                ORDER BY p.StartDate DESC, p.EndDate DESC, p.HistoricalPeriodId DESC;
                SELECT i.HistoricalPeriodId, i.ItemId, i.ItemName, i.ItemCode, i.Quantity, i.DisplayOrder
                FROM dbo.LicenseHistoricalPeriodItem i
                INNER JOIN dbo.LicenseHistoricalPeriod p ON p.HistoricalPeriodId = i.HistoricalPeriodId
                WHERE p.AnchorSetId IN @Ids
                ORDER BY i.DisplayOrder, i.HistoricalPeriodItemId;", new { Ids = setIds }, transaction))
            {
                var periods = results.Read<LicenseHistoricalPeriodDto>().AsList();
                var items = results.Read<LicenseHistoricalPeriodItemDto>().ToLookup(i => i.HistoricalPeriodId);
                foreach (var period in periods) period.Items = items[period.HistoricalPeriodId].ToList();
                return periods;
            }
        }
    }
}
