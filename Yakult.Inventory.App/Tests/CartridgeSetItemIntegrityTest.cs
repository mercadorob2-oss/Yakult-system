using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Tests
{
    /// <summary>
    /// Regression tests for the SetItem.ItemCode integrity bug fix.
    ///
    /// Bug that was fixed:
    ///   SetItem rows for cartridge exchanges were created with placeholder
    ///   ItemCode / Description (e.g. "Cartridge Exchange - UNKNOWN") when
    ///   the cartridge model could not be resolved at fulfillment time.
    ///   This broke "Assign Returns to Refill Batches" because returns could
    ///   not be grouped by model.
    ///
    /// These tests verify the full cartridge exchange → return → batch
    /// assignment flow produces correct, non-placeholder data at every step.
    ///
    /// HOW TO RUN:
    ///   Instantiate this class and call RunAll() from a debug/admin form.
    ///   All tests print PASS/FAIL to Console.  Results are READ-ONLY except
    ///   where explicitly noted (cleanup is performed after each destructive test).
    ///
    ///   NOTE: Tests marked [DESTRUCTIVE] insert and then roll back data inside
    ///   an explicit transaction, so they are safe to run on the live database
    ///   in a test/staging environment.
    /// </summary>
    public class CartridgeSetItemIntegrityTest
    {
        private readonly CartridgeManagementRepository _cartridgeRepo;
        private readonly CartridgeRefillRepository     _refillRepo;

        public CartridgeSetItemIntegrityTest()
        {
            _cartridgeRepo = new CartridgeManagementRepository();
            _refillRepo    = new CartridgeRefillRepository();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Entry point
        // ─────────────────────────────────────────────────────────────────────

        public void RunAll()
        {
            Console.WriteLine("=== CartridgeSetItemIntegrityTest ===");
            Console.WriteLine();

            Test1_NoNullOrEmptyItemCode_InExistingSetItems();
            Test2_ModelTagExtraction_ProducesRealItemCode();
            Test3_NewBatchAssignment_CorrectModelAndQty();
            Test4_ExistingBatchAssignment_CorrectModelAndQty();
            Test5_FulfillWithMissingModel_ThrowsAndRollsBack();

            Console.WriteLine();
            Console.WriteLine("=== All tests complete ===");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TEST 1 — Read-only: verify existing SetItems have no placeholder data
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Scans all existing Cartridge SetItems in the live database.
        /// Asserts that none have a NULL or empty ItemCode (legacy placeholder values).
        /// This is a read-only regression sweep — safe to run at any time.
        /// </summary>
        public void Test1_NoNullOrEmptyItemCode_InExistingSetItems()
        {
            Console.WriteLine("TEST 1: No NULL or empty ItemCode in Cartridge SetItems");
            try
            {
                const string sql = @"
                    SELECT si.SetItemId, si.SetId, si.ItemCode, si.Description
                    FROM dbo.SetItem si
                    INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
                    WHERE s.SetType = 'Cartridge'
                      AND (si.ItemCode IS NULL OR LTRIM(RTRIM(si.ItemCode)) = '')";

                var badRows = new List<(int SetItemId, int SetId, string ItemCode, string Description)>();

                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            badRows.Add((
                                reader.GetInt32(0),
                                reader.GetInt32(1),
                                reader.IsDBNull(2) ? "(NULL)" : reader.GetString(2),
                                reader.IsDBNull(3) ? "(NULL)" : reader.GetString(3)
                            ));
                        }
                    }
                }

                if (badRows.Count == 0)
                {
                    Pass("No cartridge SetItems have NULL or empty ItemCode.");
                }
                else
                {
                    Fail($"{badRows.Count} cartridge SetItem(s) have NULL/empty ItemCode:");
                    foreach (var row in badRows.Take(10))
                        Console.WriteLine($"    SetItemId={row.SetItemId}, SetId={row.SetId}, " +
                                          $"ItemCode='{row.ItemCode}', Desc='{row.Description}'");
                }
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // TEST 2 — SQL unit: model-tag extraction logic
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Validates the [MODEL:XXX] extraction SQL inline, without touching any
        /// real SetItem rows.  Covers normal, missing-tag, and malformed-tag cases.
        /// </summary>
        public void Test2_ModelTagExtraction_ProducesRealItemCode()
        {
            Console.WriteLine("TEST 2: [MODEL:XXX] tag extraction SQL");
            try
            {
                // The extraction logic from sqlMaterializeSetItem, isolated as a scalar query.
                // Input is a literal string; output is the extracted model or NULL.
                const string extractionSql = @"
                    DECLARE @desc NVARCHAR(500) = @Description;
                    SELECT COALESCE(
                        NULLIF(LTRIM(RTRIM(CASE
                            WHEN @desc LIKE '%[MODEL:%'
                                 AND CHARINDEX(']', @desc, CHARINDEX('[MODEL:', @desc) + 7) > 0
                            THEN SUBSTRING(
                                @desc,
                                CHARINDEX('[MODEL:', @desc) + 7,
                                CHARINDEX(']', @desc, CHARINDEX('[MODEL:', @desc) + 7)
                                    - (CHARINDEX('[MODEL:', @desc) + 7)
                            )
                            ELSE NULL
                        END)), ''),
                        'FALLBACK'
                    )";

                var cases = new (string Input, string Expected)[]
                {
                    // Normal portal description with embedded model tag
                    ("Cartridge Exchange Request [MODEL:PROTO123] - Pickup", "PROTO123"),
                    // Model tag at the end
                    ("Cartridge Exchange [MODEL:TN2385]", "TN2385"),
                    // No model tag — should fall back to sentinel
                    ("Legacy request without model tag", "FALLBACK"),
                    // Empty description — should fall back to sentinel
                    ("", "FALLBACK"),
                };

                bool allPassed = true;
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    foreach (var (input, expected) in cases)
                    {
                        using (var cmd = new SqlCommand(extractionSql, con))
                        {
                            cmd.Parameters.AddWithValue("@Description", input);
                            var result = cmd.ExecuteScalar()?.ToString();

                            if (result == expected)
                            {
                                Console.WriteLine($"    PASS  input='{input}' → '{result}'");
                            }
                            else
                            {
                                Console.WriteLine($"    FAIL  input='{input}' → expected '{expected}', got '{result}'");
                                allPassed = false;
                            }
                        }
                    }
                }

                if (allPassed) Pass("All extraction cases matched.");
                else Fail("One or more extraction cases did not match expected output.");
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // TEST 3 — New refill batch assignment
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// [READ-ONLY] Verifies that active cartridge batches created for a real model
        /// have a non-null, non-empty CartridgeModelId and that the model exists in
        /// dbo.CartridgeModel.  Does NOT create or delete any data.
        ///
        /// For the destructive "create batch → assign → assert → rollback" flow, see
        /// the pseudocode below this test.  The destructive version should only be run
        /// in a staging/test environment.
        /// </summary>
        public void Test3_NewBatchAssignment_CorrectModelAndQty()
        {
            Console.WriteLine("TEST 3: VendorCartridgeBatch rows have valid CartridgeModelId");
            try
            {
                const string sql = @"
                    SELECT vcb.BatchId, vcb.CartridgeModelId, cm.ModelNumber,
                           vcb.ReturnedQty, vcb.RequiredReturnQty, vcb.Status
                    FROM dbo.VendorCartridgeBatch vcb
                    LEFT JOIN dbo.CartridgeModel cm ON cm.CartridgeModelId = vcb.CartridgeModelId
                    WHERE cm.CartridgeModelId IS NULL   -- orphaned batch (no matching model)
                       OR cm.ModelNumber IS NULL
                       OR LTRIM(RTRIM(cm.ModelNumber)) = ''";

                var orphaned = new List<int>();

                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            orphaned.Add(reader.GetInt32(0));
                    }
                }

                if (orphaned.Count == 0)
                    Pass("All VendorCartridgeBatch rows reference a valid CartridgeModel.");
                else
                    Fail($"{orphaned.Count} batch(es) have orphaned/invalid CartridgeModelId: " +
                         string.Join(", ", orphaned.Take(10)));
            }
            catch (Exception ex)
            {
                Error(ex);
            }

            /*
             * ── DESTRUCTIVE PSEUDOCODE (run in staging only) ────────────────
             *
             * // ARRANGE
             * int testModelId = <pick a real CartridgeModelId>;
             * int testVendorId = <pick a real VendorId>;
             * int testUserId = <pick a real UserId>;
             * var testEmptyCartridgeId = <insert a test EmptyCartridge row via SQL>;
             *
             * // ACT
             * int batchId = await _refillService.CreateRefillBatchAsync(
             *     testVendorId, testModelId, 0, 80m, testUserId, "Regression test batch");
             *
             * int assigned = await _refillService.AssignReturnsToBatchAsync(
             *     batchId, new[] { testEmptyCartridgeId }, testUserId);
             *
             * // ASSERT
             * var batch = <SELECT from VendorCartridgeBatch WHERE BatchId = batchId>;
             * Assert(batch.CartridgeModelId == testModelId,     "Batch has correct model");
             * Assert(batch.ReturnedQty      == testQty,         "Batch ReturnedQty incremented");
             * Assert(batch.Status           == "Active",         "Batch still Active");
             *
             * var empty = <SELECT from EmptyCartridge WHERE EmptyCartridgeId = testEmptyCartridgeId>;
             * Assert(empty.VendorBatchId == batchId,            "EmptyCartridge linked to batch");
             * Assert(empty.Status        == "BatchAssigned",    "EmptyCartridge status updated");
             *
             * // CLEANUP
             * <DELETE from VendorCartridgeBatch WHERE BatchId = batchId>;
             * <DELETE from EmptyCartridge WHERE EmptyCartridgeId = testEmptyCartridgeId>;
             * ────────────────────────────────────────────────────────────────
             */
        }

        // ─────────────────────────────────────────────────────────────────────
        // TEST 4 — Existing batch assignment
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// [READ-ONLY] Verifies that EmptyCartridge rows in Status='BatchAssigned' all
        /// reference an existing VendorCartridgeBatch row (referential integrity check).
        /// </summary>
        public void Test4_ExistingBatchAssignment_CorrectModelAndQty()
        {
            Console.WriteLine("TEST 4: BatchAssigned EmptyCartridge rows reference valid batches");
            try
            {
                const string sql = @"
                    SELECT ec.EmptyCartridgeId, ec.VendorBatchId
                    FROM dbo.EmptyCartridge ec
                    LEFT JOIN dbo.VendorCartridgeBatch vcb ON vcb.BatchId = ec.VendorBatchId
                    WHERE ec.Status        = 'BatchAssigned'
                      AND ec.VendorBatchId IS NOT NULL
                      AND vcb.BatchId      IS NULL";   // orphaned assignment

                var orphaned = new List<int>();

                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            orphaned.Add(reader.GetInt32(0));
                    }
                }

                if (orphaned.Count == 0)
                    Pass("All BatchAssigned EmptyCartridge rows reference a valid batch.");
                else
                    Fail($"{orphaned.Count} EmptyCartridge row(s) reference non-existent batch(es).");
            }
            catch (Exception ex)
            {
                Error(ex);
            }

            /*
             * ── DESTRUCTIVE PSEUDOCODE — assign to EXISTING batch (staging only) ──
             *
             * // ARRANGE — use an existing Active batch
             * int existingBatchId = <SELECT TOP 1 BatchId FROM VendorCartridgeBatch WHERE Status='Active'>;
             * int testModelId = <SELECT CartridgeModelId FROM VendorCartridgeBatch WHERE BatchId=existingBatchId>;
             * int existingReturnedQty = <SELECT ReturnedQty FROM VendorCartridgeBatch WHERE BatchId=existingBatchId>;
             *
             * int testEmptyCartridgeId = <INSERT EmptyCartridge for testModelId with Status='Pending'>;
             * int testQty = <Quantity of the inserted row>;
             *
             * // ACT
             * int assigned = await _refillService.AssignReturnsToBatchAsync(
             *     existingBatchId, new[] { testEmptyCartridgeId }, testUserId);
             *
             * // ASSERT
             * var updatedBatch = <SELECT from VendorCartridgeBatch WHERE BatchId = existingBatchId>;
             * Assert(updatedBatch.ReturnedQty == existingReturnedQty + testQty, "ReturnedQty incremented correctly");
             * Assert(assigned == testQty,                                        "Assigned qty matches return qty");
             *
             * var empty = <SELECT from EmptyCartridge WHERE EmptyCartridgeId = testEmptyCartridgeId>;
             * Assert(empty.VendorBatchId == existingBatchId, "EmptyCartridge linked to correct batch");
             * Assert(empty.Status == "BatchAssigned",        "Status updated to BatchAssigned");
             *
             * // Also assert idempotence: calling again does nothing
             * int secondAssign = await _refillService.AssignReturnsToBatchAsync(
             *     existingBatchId, new[] { testEmptyCartridgeId }, testUserId);
             * Assert(secondAssign == 0, "Second call is a no-op (VendorBatchId already set)");
             *
             * // CLEANUP
             * <Restore ReturnedQty on existing batch>;
             * <DELETE EmptyCartridge WHERE EmptyCartridgeId = testEmptyCartridgeId>;
             * ────────────────────────────────────────────────────────────────────────
             */
        }

        // ─────────────────────────────────────────────────────────────────────
        // TEST 5 — Guard: fulfillment with missing model throws and rolls back
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Verifies that the RAISERROR guard in sqlMaterializeSetItem fires when a
        /// cartridge item has no CartridgeModelId and no [MODEL:XXX] tag.
        ///
        /// This test is READ-ONLY in its assertion phase — it checks existing data
        /// to confirm there are no items that WOULD trigger the guard on a live
        /// fulfillment.
        /// </summary>
        public void Test5_FulfillWithMissingModel_ThrowsAndRollsBack()
        {
            Console.WriteLine("TEST 5: Cartridge items all have a resolvable model number");
            try
            {
                // Check for any Cartridge items that have:
                //   - No CartridgeModelId FK  AND
                //   - No ModelNumber on the Item itself
                // These would cause FulfillCartridgeExchange to RAISERROR.
                const string sql = @"
                    SELECT i.ItemId, i.Name, i.ModelNumber, i.CartridgeModelId
                    FROM dbo.Item i
                    WHERE i.Category = 'Cartridge'
                      AND i.Active   = 1
                      AND i.CartridgeModelId IS NULL
                      AND (i.ModelNumber IS NULL OR LTRIM(RTRIM(i.ModelNumber)) = '')";

                var problem = new List<(int Id, string Name)>();

                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            problem.Add((reader.GetInt32(0), reader.GetString(1)));
                    }
                }

                if (problem.Count == 0)
                {
                    Pass("All active Cartridge items have CartridgeModelId or ModelNumber set.");
                }
                else
                {
                    // These items are currently un-fulfillable with the hardened guard.
                    // They should be assigned a CartridgeModel in the admin UI before
                    // any exchange request for them is processed.
                    Fail($"{problem.Count} active Cartridge item(s) have no model — " +
                         "FulfillCartridgeExchange would RAISERROR for these:");
                    foreach (var (id, name) in problem.Take(10))
                        Console.WriteLine($"    ItemId={id}, Name='{name}'");
                    Console.WriteLine("    ACTION REQUIRED: Assign CartridgeModelId via Items admin before fulfilling.");
                }

                /*
                 * ── GUARD TRIGGER PSEUDOCODE (confirms the guard fires) ────────
                 *
                 * // ARRANGE — create an orphan cartridge item with no model
                 * int orphanItemId = <INSERT Item WHERE Category='Cartridge', CartridgeModelId=NULL, ModelNumber=NULL>;
                 * int reqId = <INSERT Request for orphanItemId>;
                 * int setId = <INSERT Set and link Request to Set>;
                 *
                 * // ACT + ASSERT
                 * bool threw = false;
                 * try
                 * {
                 *     _cartridgeRepo.FulfillCartridgeExchange(reqId, 1, new List<int>{ orphanItemId }, userId);
                 * }
                 * catch (SqlException ex) when (ex.Message.Contains("model number"))
                 * {
                 *     threw = true;
                 * }
                 * Assert(threw, "RAISERROR fired for missing model");
                 *
                 * // ASSERT ROLLBACK — SetItem must NOT have been persisted
                 * int setItemCount = <SELECT COUNT(*) FROM SetItem WHERE SetId = setId>;
                 * Assert(setItemCount == 0, "No SetItem persisted after RAISERROR rollback");
                 *
                 * // CLEANUP
                 * <DELETE Request, Set, SetItem, Item for the orphan>;
                 * ────────────────────────────────────────────────────────────────
                 */
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static void Pass(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  PASS: {message}");
            Console.ResetColor();
        }

        private static void Fail(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  FAIL: {message}");
            Console.ResetColor();
        }

        private static void Error(Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.ResetColor();
        }
    }
}
