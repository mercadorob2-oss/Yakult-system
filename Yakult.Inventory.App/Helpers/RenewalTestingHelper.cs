using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Core;


namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// Developer-only helper class for testing renewal and auto-archive functionality
    /// </summary>
    public class RenewalTestingHelper
    {
        private readonly string _connectionString;
        private readonly RenewalRepository _renewalRepository;

        public RenewalTestingHelper()
        {
            _connectionString = DatabaseConfig.ConnectionString;
            _renewalRepository = new RenewalRepository();
        }

        /// <summary>
        /// Generates test renewal records with specified age (in months)
        /// Creates Sets with Items following the proper renewal workflow
        /// </summary>
        public int GenerateTestRenewals(int ageInMonths, int count, int createdByUserId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                int setsCreated = 0;
                DateTime testDate = DateTime.Now.AddMonths(-ageInMonths);

                // Get a valid CategoryId (first active category)
                int categoryId = connection.QueryFirstOrDefault<int>(@"
                    SELECT TOP 1 CategoryId FROM dbo.ItemCategory
                    WHERE Active = 1
                    ORDER BY CategoryId");

                if (categoryId == 0)
                {
                    throw new Exception("No active categories found. Please create at least one active ItemCategory first.");
                }

                // Get a valid ConditionID (first condition)
                int conditionId = connection.QueryFirstOrDefault<int>(@"
                    SELECT TOP 1 ConditionID FROM dbo.Condition
                    ORDER BY ConditionID");

                if (conditionId == 0)
                {
                    throw new Exception("No conditions found. Please create at least one Condition record first.");
                }

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        string testGuid = Guid.NewGuid().ToString().Substring(0, 8);

                        // Step 1: Create an Item (Software/License type)
                        string insertItemSql = @"
                            INSERT INTO dbo.Item (
                                [Name], Description, CategoryId, ModelNumber, ItemType,
                                UnitOfMeasure, ConditionID, DateCreated, CreatedBy, ModifiedBy, Active
                            )
                            VALUES (
                                @Name, @Description, @CategoryId, @ModelNumber, @ItemType,
                                @UnitOfMeasure, @ConditionID, @DateCreated, @CreatedBy, @ModifiedBy, 1
                            );
                            SELECT SCOPE_IDENTITY();";

                        int itemId = connection.ExecuteScalar<int>(insertItemSql, new
                        {
                            Name = $"TEST_RENEWAL_ITEM_{ageInMonths}M_{testGuid}",
                            Description = $"Test renewal item - {ageInMonths} months old",
                            CategoryId = categoryId,
                            ModelNumber = $"TEST-MODEL-{testGuid}",
                            ItemType = "Software/License",
                            UnitOfMeasure = "License",
                            ConditionID = conditionId,
                            DateCreated = testDate,
                            CreatedBy = createdByUserId,
                            ModifiedBy = createdByUserId
                        });

                        // Step 2: Create a Set (Software/License type)
                        string insertSetSql = @"
                            INSERT INTO dbo.[Set] (
                                CreatedBy, CreatedAt, SetType, Status, Active,
                                StartDate, EndDate, DocumentNumber, Remarks
                            )
                            VALUES (
                                @CreatedBy, @CreatedAt, 'Software/License', 'Active', 1,
                                @StartDate, @EndDate, @DocumentNumber, @Remarks
                            );
                            SELECT SCOPE_IDENTITY();";

                        int setId = connection.ExecuteScalar<int>(insertSetSql, new
                        {
                            CreatedBy = createdByUserId,
                            CreatedAt = testDate,
                            StartDate = testDate,
                            EndDate = testDate.AddYears(1),
                            DocumentNumber = $"TEST-DOC-{testGuid}",
                            Remarks = $"TEST RENEWAL - {ageInMonths} months old - Auto-generated test data"
                        });

                        // Step 3: Link Item to Set via SetItem
                        string insertSetItemSql = @"
                            INSERT INTO dbo.SetItem (
                                SetId, ItemId, Quantity, UnitPrice, Amount, CreatedAt
                            )
                            VALUES (
                                @SetId, @ItemId, 1, 1000.00, 1000.00, @CreatedAt
                            );";

                        connection.Execute(insertSetItemSql, new
                        {
                            SetId = setId,
                            ItemId = itemId,
                            CreatedAt = testDate
                        });

                        // Step 4: Create Renewal record for this Item with "On Hold" status
                        string insertRenewalSql = @"
                            INSERT INTO dbo.Renewals (
                                ItemId, RenewalStatus, OnHoldDate, RenewalCount,
                                IsArchived, CreatedBy, CreatedAt
                            )
                            VALUES (
                                @ItemId, 'On Hold', @OnHoldDate, 0, 0, @CreatedBy, @CreatedAt
                            );";

                        connection.Execute(insertRenewalSql, new
                        {
                            ItemId = itemId,
                            OnHoldDate = testDate,
                            CreatedBy = createdByUserId,
                            CreatedAt = testDate
                        });

                        setsCreated++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error creating test renewal: {ex.Message}");
                        throw; // Re-throw to see the actual error
                    }
                }

                return setsCreated;
            }
        }

        /// <summary>
        /// Runs the auto-archive process ONLY for test data (items starting with TEST_RENEWAL_)
        /// </summary>
        public int RunAutoArchiveTestDataOnly()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Get test renewals eligible for archiving
                        string selectSql = @"
                            SELECT r.RenewalId, r.ItemId
                            FROM dbo.Renewals r
                            INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                            WHERE r.RenewalStatus = 'On Hold'
                              AND r.IsArchived = 0
                              AND r.OnHoldDate IS NOT NULL
                              AND DATEDIFF(DAY, r.OnHoldDate, GETDATE()) > 365
                              AND i.[Name] LIKE 'TEST_RENEWAL_%'";

                        var renewalsToArchive = connection.Query<(int RenewalId, int ItemId)>(selectSql, transaction: transaction).ToList();

                        if (!renewalsToArchive.Any())
                        {
                            transaction.Commit();
                            return 0;
                        }

                        // Archive test renewals that have been On Hold for > 1 year
                        string archiveRenewalsSql = @"
                            UPDATE r
                            SET
                                IsArchived = 1,
                                ArchivedDate = GETDATE(),
                                ArchiveReason = 'Auto-archived by developer tools - On Hold > 1 year'
                            FROM dbo.Renewals r
                            INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                            WHERE r.RenewalStatus = 'On Hold'
                              AND r.IsArchived = 0
                              AND r.OnHoldDate IS NOT NULL
                              AND DATEDIFF(DAY, r.OnHoldDate, GETDATE()) > 365
                              AND i.[Name] LIKE 'TEST_RENEWAL_%'";

                        int archived = connection.Execute(archiveRenewalsSql, transaction: transaction);

                        // Insert into ArchiveStatus table for each archived renewal
                        string insertArchiveStatusSql = @"
                            INSERT INTO dbo.ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                            VALUES (@EntityType, @EntityId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                        foreach (var renewal in renewalsToArchive)
                        {
                            // Check if already exists in ArchiveStatus
                            var existsInArchive = connection.ExecuteScalar<int>(
                                "SELECT COUNT(*) FROM dbo.ArchiveStatus WHERE EntityType = 'Renewal' AND EntityId = @RenewalId",
                                new { RenewalId = renewal.RenewalId },
                                transaction: transaction) > 0;

                            if (!existsInArchive)
                            {
                                connection.Execute(insertArchiveStatusSql, new
                                {
                                    EntityType = "Renewal",
                                    EntityId = renewal.RenewalId,
                                    ArchivedBy = "Developer Tools (Auto-Archive)",
                                    ArchiveReason = "Auto-archived by developer tools - On Hold > 1 year"
                                }, transaction: transaction);
                            }
                        }

                        transaction.Commit();
                        return archived;
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
        /// Cleans up all test renewal data (Sets, Items, SetItems, and Renewals)
        /// </summary>
        public int CleanupTestData()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                // Get test item IDs
                var testItemIds = connection.Query<int>(@"
                    SELECT ItemId FROM dbo.Item
                    WHERE [Name] LIKE 'TEST_RENEWAL_%'").ToList();

                if (!testItemIds.Any())
                    return 0;

                // Get test set IDs
                var testSetIds = connection.Query<int>(@"
                    SELECT DISTINCT SetId FROM dbo.SetItem
                    WHERE ItemId IN @ItemIds", new { ItemIds = testItemIds }).ToList();

                // Delete in proper order to maintain referential integrity

                // 1. Delete Renewals
                connection.Execute(@"
                    DELETE FROM dbo.Renewals
                    WHERE ItemId IN @ItemIds", new { ItemIds = testItemIds });

                // 2. Delete SetItems
                connection.Execute(@"
                    DELETE FROM dbo.SetItem
                    WHERE ItemId IN @ItemIds", new { ItemIds = testItemIds });

                // 3. Delete Sets
                if (testSetIds.Any())
                {
                    connection.Execute(@"
                        DELETE FROM dbo.[Set]
                        WHERE SetId IN @SetIds", new { SetIds = testSetIds });
                }

                // 4. Delete Items
                int itemsDeleted = connection.Execute(@"
                    DELETE FROM dbo.Item
                    WHERE ItemId IN @ItemIds", new { ItemIds = testItemIds });

                return itemsDeleted;
            }
        }

        /// <summary>
        /// Gets count of test renewal records by status
        /// </summary>
        public Dictionary<string, int> GetTestRenewalStats()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string sql = @"
                    SELECT
                        r.RenewalStatus,
                        r.IsArchived,
                        COUNT(*) AS Count
                    FROM dbo.Renewals r
                    INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                    WHERE i.[Name] LIKE 'TEST_RENEWAL_%'
                    GROUP BY r.RenewalStatus, r.IsArchived;";

                var results = connection.Query<dynamic>(sql).ToList();

                var stats = new Dictionary<string, int>
                {
                    { "Total Test Items", results.Sum(r => (int)r.Count) },
                    { "On Hold (Not Archived)", results.Where(r => r.RenewalStatus == "On Hold" && r.IsArchived == false).Sum(r => (int)r.Count) },
                    { "On Hold (Archived)", results.Where(r => r.RenewalStatus == "On Hold" && r.IsArchived == true).Sum(r => (int)r.Count) },
                    { "Renewed", results.Where(r => r.RenewalStatus == "Renewed").Sum(r => (int)r.Count) },
                    { "None", results.Where(r => r.RenewalStatus == "None").Sum(r => (int)r.Count) }
                };

                return stats;
            }
        }

        /// <summary>
        /// Gets renewal records that would be subject to auto-archive (On Hold > 1 year)
        /// </summary>
        public List<TestRenewalInfo> GetRenewalsEligibleForArchive()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string sql = @"
                    SELECT
                        i.ItemId,
                        i.[Name] AS ItemName,
                        r.RenewalId,
                        r.RenewalStatus,
                        r.OnHoldDate,
                        r.IsArchived,
                        DATEDIFF(DAY, r.OnHoldDate, GETDATE()) AS DaysOnHold
                    FROM dbo.Renewals r
                    INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                    WHERE r.RenewalStatus = 'On Hold'
                      AND r.IsArchived = 0
                      AND r.OnHoldDate IS NOT NULL
                      AND DATEDIFF(DAY, r.OnHoldDate, GETDATE()) > 365
                      AND i.[Name] LIKE 'TEST_RENEWAL_%'
                    ORDER BY r.OnHoldDate ASC;";

                return connection.Query<TestRenewalInfo>(sql).ToList();
            }
        }
    }

    /// <summary>
    /// Information about test renewal records
    /// </summary>
    public class TestRenewalInfo
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public int RenewalId { get; set; }
        public string RenewalStatus { get; set; }
        public DateTime? OnHoldDate { get; set; }
        public bool IsArchived { get; set; }
        public int DaysOnHold { get; set; }
    }
}
